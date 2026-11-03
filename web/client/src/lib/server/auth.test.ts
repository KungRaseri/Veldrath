// @vitest-environment node
import { afterEach, describe, expect, it } from 'vitest';
import { withEnv } from '#lib/testing/env';
import { getAuth } from '#lib/server/auth';

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
