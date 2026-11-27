// @vitest-environment node
import { afterEach, describe, expect, it } from 'vitest';
import { betterAuth } from 'better-auth';
import { memoryAdapter } from 'better-auth/adapters/memory';
import { withEnv } from '#lib/testing/env';
import { buildAuthOptions, getAuth } from '#lib/server/auth';

/**
 * `getAuth` is the lazy Better Auth singleton. It must refuse to build without a secret, and it
 * must build with every provider configured — the provider branches are the part that silently
 * disables a login button when an env var is missing.
 */

const restores: Array<() => void> = [];

afterEach(() => {
	for (const restore of restores.splice(0)) restore();
});

const secret = 'test-better-auth-secret-change-me-0123456789abcdef';

describe('getAuth', () => {
	it('refuses to build without a secret', () => {
		restores.push(withEnv({ BETTER_AUTH_SECRET: undefined }));

		expect(() => getAuth()).toThrow('BETTER_AUTH_SECRET is not configured.');
	});

	it('builds a singleton with OAuth providers configured', () => {
		restores.push(
			withEnv({
				BETTER_AUTH_SECRET: secret,
				BETTER_AUTH_URL: 'http://localhost:5173',
				ORIGIN: 'http://localhost:5173',
				DATABASE_URL: 'postgres://user:pass@127.0.0.1:5432/test',
				DISCORD_CLIENT_ID: 'discord-id',
				DISCORD_CLIENT_SECRET: 'discord-secret',
				GOOGLE_CLIENT_ID: 'google-id',
				GOOGLE_CLIENT_SECRET: 'google-secret',
				MICROSOFT_CLIENT_ID: 'microsoft-id',
				MICROSOFT_CLIENT_SECRET: 'microsoft-secret',
				MICROSOFT_TENANT_ID: 'common'
			})
		);

		const first = getAuth();

		expect(first).toBe(getAuth());
		expect(first.options.appName).toBe('Veldrath');
		expect(first.options.emailAndPassword?.enabled).toBe(true);
		expect(Object.keys(first.options.socialProviders ?? {}).sort()).toEqual([
			'discord',
			'google',
			'microsoft'
		]);
	});
});

/**
 * Sign-up is the one auth surface a stranger can reach without an account, so it must not answer the
 * question "does this address already have an account here?".
 *
 * The test runs the production option set (`buildAuthOptions`) against the in-memory adapter — the
 * behaviour under test is a property of those options, and a copy of them would prove nothing — and
 * compares a first sign-up with a second one for the same address.
 */
describe('sign-up enumeration', () => {
	it('answers an address that already has an account exactly like a new one', async () => {
		const origin = 'http://localhost:5173';

		restores.push(
			withEnv({
				BETTER_AUTH_SECRET: secret,
				BETTER_AUTH_URL: origin,
				ORIGIN: origin,
				DATABASE_URL: 'postgres://user:***@127.0.0.1:5432/test',
				AUTH_RATE_LIMIT_DISABLED: 'true',
				INTERNAL_BRIDGE_URL: '',
				INTERNAL_BRIDGE_TOKEN: ''
			})
		);

		const auth = betterAuth(
			buildAuthOptions(memoryAdapter({ user: [], session: [], account: [], verification: [] }))
		);

		// Each probe needs its own username: usernames are public identifiers, and the username plugin
		// openly rejects a taken one (400 USERNAME_IS_ALREADY_TAKEN) before the email is considered.
		// The email is the attribute an attacker is trying to map, so it stays the only variable.
		const signUp = (email: string, username: string) =>
			auth.handler(
				new Request(`${origin}/api/auth/sign-up/email`, {
					method: 'POST',
					headers: { 'content-type': 'application/json', origin },
					// A profile image is a field the schema always returns (OAuth accounts set one, the
					// password form leaves it null). The in-memory adapter stores only what it is given
					// while Postgres returns every column, so sending one keeps the real and synthetic
					// user objects comparable for the right reason — an adapter artifact, not a leak.
					body: JSON.stringify({
						email,
						password: 'Str0ng-pass-123',
						name: 'Ash',
						username,
						image: 'https://example.test/avatar.png'
					})
				})
			);

		const first = await signUp('taken@example.test', 'firstcaller');
		const duplicate = await signUp('taken@example.test', 'secondcaller');
		const fresh = await signUp('brand-new@example.test', 'thirdcaller');
		const duplicateBody = await duplicate.text();
		const freshBody = await fresh.text();

		expect(first.status).toBe(200);
		expect(duplicate.status).toBe(200);
		expect(fresh.status).toBe(200);

		// A session on one response and not the other would be the leak, whatever the body says.
		for (const response of [first, duplicate, fresh]) {
			expect(response.headers.get('set-cookie')).toBeNull();
		}

		// The stock duplicate path returns 422 "User already exists. Use another email." — the exact
		// sentence an address-enumeration script looks for.
		expect(duplicateBody).not.toMatch(/already exists|USER_ALREADY_EXISTS/i);

		// Same status, same fields, same null token: the only differences left are the ones any two
		// sign-ups have (fresh GUIDs and timestamps) and the values the caller supplied.
		expect(Object.keys(JSON.parse(duplicateBody).user).sort()).toEqual(
			Object.keys(JSON.parse(freshBody).user).sort()
		);
		expect(normalize(duplicateBody)).toEqual(normalize(freshBody));
	});
});

/** Drops per-user and caller-supplied fields, so two responses can be compared. */
function normalize(raw: string): { token: unknown; user: Record<string, unknown> } {
	const body = JSON.parse(raw) as { token: unknown; user: Record<string, unknown> };
	const user: Record<string, unknown> = { ...body.user };
	for (const field of [
		'id',
		'createdAt',
		'updatedAt',
		// Caller-supplied, and echoed by both responses.
		'email',
		'name',
		'username',
		'displayUsername'
	]) {
		delete user[field];
	}
	return { token: body.token, user };
}
