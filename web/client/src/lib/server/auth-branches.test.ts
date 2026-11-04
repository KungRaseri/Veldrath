// @vitest-environment node
import { afterEach, describe, expect, it, vi } from 'vitest';
import { installFetch, jsonResponse } from '#lib/testing/fetch';
import { withEnv } from '#lib/testing/env';
import { verifyIdentityPassword } from '#lib/server/auth-password';
import { sendTransactionalEmail } from '#lib/server/email';

/**
 * The lazy Better Auth singleton's less-travelled branches.
 *
 * `auth.test.ts` proves the happy path builds. This suite pins the parts that only run on an
 * unusual deployment or on a real sign-in: a provider configured with only half a credential pair,
 * a `baseURL` that falls back to `ORIGIN` (or to nothing), the reset/verification callbacks that
 * rewrite a Better Auth URL into a browser link, and the password verifier that has to tell a
 * Better Auth scrypt hash from a migrated Identity one.
 *
 * The auth instance is a module-scoped singleton, so each scenario resets the module registry
 * before importing it.
 */

const restores: Array<() => void> = [];
const fetchStubs: Array<() => void> = [];

// Each case builds a fresh Better Auth instance (drizzle adapter + scrypt hashing); under a loaded
// full-suite run that can outlast the default five-second timeout.
vi.setConfig({ testTimeout: 30_000, hookTimeout: 30_000 });

afterEach(() => {
	for (const restore of restores.splice(0).reverse()) restore();
	for (const restore of fetchStubs.splice(0)) restore();
	vi.resetModules();
	vi.restoreAllMocks();
});

const secret = 'test-better-auth-secret-change-me-0123456789abcdef';
const databaseUrl = 'postgres://user:***@127.0.0.1:5432/test';

/**
 * Clears every auth-related variable, applies the scenario, then imports a fresh auth module.
 *
 * Empty strings rather than `undefined`: importing `#lib/server/env` re-reads `.env`, which would
 * re-populate a deleted key, while a defined-but-empty value survives and is falsy to `||`.
 */
async function freshAuth(environment: Record<string, string | undefined>) {
	restores.push(
		withEnv({
			BETTER_AUTH_SECRET: '',
			BETTER_AUTH_URL: '',
			ORIGIN: '',
			DATABASE_URL: databaseUrl,
			DISCORD_CLIENT_ID: '',
			DISCORD_CLIENT_SECRET: '',
			GOOGLE_CLIENT_ID: '',
			GOOGLE_CLIENT_SECRET: '',
			MICROSOFT_CLIENT_ID: '',
			MICROSOFT_CLIENT_SECRET: '',
			MICROSOFT_TENANT_ID: '',
			INTERNAL_BRIDGE_URL: '',
			INTERNAL_BRIDGE_TOKEN: '',
			...environment
		})
	);

	const module = await import('#lib/server/auth');
	return module.getAuth();
}

describe('buildSocialProviders', () => {
	it('drops a provider whose credential pair is incomplete', async () => {
		const auth = await freshAuth({
			BETTER_AUTH_SECRET: secret,
			BETTER_AUTH_URL: 'https://veldrath.test',
			DISCORD_CLIENT_ID: 'discord-id',
			GOOGLE_CLIENT_SECRET: 'google-secret',
			MICROSOFT_CLIENT_ID: 'microsoft-id'
		});

		expect(auth.options.socialProviders).toEqual({});
		expect(auth.options.baseURL).toBe('https://veldrath.test');
	});

	it('adds the Microsoft tenant only when it is configured', async () => {
		const withTenant = await freshAuth({
			BETTER_AUTH_SECRET: secret,
			BETTER_AUTH_URL: 'https://veldrath.test',
			MICROSOFT_CLIENT_ID: 'microsoft-id',
			MICROSOFT_CLIENT_SECRET: 'microsoft-secret',
			MICROSOFT_TENANT_ID: 'common'
		});

		expect(withTenant.options.socialProviders?.microsoft).toEqual({
			clientId: 'microsoft-id',
			clientSecret: 'microsoft-secret',
			tenantId: 'common'
		});

		vi.resetModules();
		const withoutTenant = await freshAuth({
			BETTER_AUTH_SECRET: secret,
			BETTER_AUTH_URL: 'https://veldrath.test',
			MICROSOFT_CLIENT_ID: 'microsoft-id',
			MICROSOFT_CLIENT_SECRET: 'microsoft-secret'
		});

		expect(withoutTenant.options.socialProviders?.microsoft).toEqual({
			clientId: 'microsoft-id',
			clientSecret: 'microsoft-secret'
		});
	});

	it('leaves baseURL undefined when neither BETTER_AUTH_URL nor ORIGIN is set', async () => {
		const auth = await freshAuth({ BETTER_AUTH_SECRET: secret });

		expect(auth.options.socialProviders).toEqual({});
		expect(auth.options.baseURL).toBeUndefined();
	});
});

describe('Better Auth callbacks', () => {
	it('rewrites a Better Auth token into a browser link and survives a missing origin', async () => {
		const auth = await freshAuth({
			BETTER_AUTH_SECRET: secret,
			ORIGIN: 'http://localhost:5173/',
			INTERNAL_BRIDGE_URL: '',
			INTERNAL_BRIDGE_TOKEN: ''
		});
		const info = vi.spyOn(console, 'info').mockImplementation(() => {});

		expect(auth.options.baseURL).toBe('http://localhost:5173/');

		const sendReset = auth.options.emailAndPassword?.sendResetPassword;
		await sendReset?.({
			user: { email: 'player@example.com', name: 'Player One' },
			url: '/reset-password?token=abc',
			token: 'token-1'
		} as never);

		expect(info).toHaveBeenCalledWith(
			expect.stringContaining('http://localhost:5173/reset-password?token=token-1')
		);

		// With no origin at all the raw Better Auth URL is handed through unchanged.
		restores.push(withEnv({ ORIGIN: '', BETTER_AUTH_URL: '' }));
		await sendReset?.({
			user: { email: 'player@example.com', name: 'Player One' },
			url: '/reset-password?token=abc',
			token: 'token-2'
		} as never);

		expect(info).toHaveBeenLastCalledWith(expect.stringContaining('/reset-password?token=abc'));

		const sendVerification = auth.options.emailVerification?.sendVerificationEmail;
		await sendVerification?.({
			user: { email: 'player@example.com', name: 'Player One' },
			url: '/verify-email?x',
			token: 'token-3'
		} as never);

		expect(info).toHaveBeenLastCalledWith(expect.stringContaining('/verify-email'));
	});

	it('verifies a scrypt hash and falls through to the Identity verifier', async () => {
		const auth = await freshAuth({
			BETTER_AUTH_SECRET: secret,
			BETTER_AUTH_URL: 'http://localhost'
		});
		const { hashPassword } = await import('better-auth/crypto');

		const verify = auth.options.emailAndPassword?.password?.verify;
		const scrypt = await hashPassword('correct horse battery staple');

		await expect(
			verify?.({ hash: scrypt, password: 'correct horse battery staple' } as never)
		).resolves.toBe(true);
		await expect(
			verify?.({ hash: 'not-an-identity-hash', password: 'anything' } as never)
		).resolves.toBe(false);
	});

	it('generates a GUID account id', async () => {
		const auth = await freshAuth({
			BETTER_AUTH_SECRET: secret,
			BETTER_AUTH_URL: 'http://localhost'
		});
		const generateId = auth.options.advanced?.database?.generateId;

		expect(generateId?.()).toMatch(
			/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/
		);
	});
});

/** Builds a raw Identity v3 hash so a corrupted header can be exercised. */
function identityHash(options: {
	prf: number;
	iterations: number;
	saltLength: number;
	salt?: Buffer;
	subkey?: Buffer;
}): string {
	const header = Buffer.alloc(13);
	header[0] = 0x01;
	header.writeUInt32BE(options.prf, 1);
	header.writeUInt32BE(options.iterations, 5);
	header.writeUInt32BE(options.saltLength, 9);

	return Buffer.concat([
		header,
		options.salt ?? Buffer.from('0123456789abcdef', 'utf8'),
		options.subkey ?? Buffer.alloc(32, 0xff)
	]).toString('base64');
}

describe('verifyIdentityPassword guards', () => {
	it('rejects a hash with zero iterations or a zero salt length', async () => {
		const noIterations = identityHash({ prf: 2, iterations: 0, saltLength: 16 });
		const noSalt = identityHash({ prf: 2, iterations: 10_000, saltLength: 0 });

		await expect(verifyIdentityPassword(noIterations, 'anything')).resolves.toBe(false);
		await expect(verifyIdentityPassword(noSalt, 'anything')).resolves.toBe(false);
	});

	it('rejects a hash whose declared salt runs past the payload', async () => {
		const truncated = identityHash({
			prf: 2,
			iterations: 10_000,
			saltLength: 64,
			salt: Buffer.from('short', 'utf8'),
			subkey: Buffer.alloc(2, 0x01)
		});

		await expect(verifyIdentityPassword(truncated, 'anything')).resolves.toBe(false);
	});

	it('rejects an unknown PRF marker', async () => {
		const unknownPrf = identityHash({ prf: 99, iterations: 10_000, saltLength: 16 });

		await expect(verifyIdentityPassword(unknownPrf, 'anything')).resolves.toBe(false);
	});
});

describe('sendTransactionalEmail development fallback', () => {
	it('echoes the single-use link to the console when the bridge is absent', async () => {
		const info = vi.spyOn(console, 'info').mockImplementation(() => {});
		restores.push(withEnv({ INTERNAL_BRIDGE_URL: undefined, INTERNAL_BRIDGE_TOKEN: undefined }));

		await expect(
			sendTransactionalEmail('password-reset', { to: 'player@example.com', url: 'https://v/x' })
		).resolves.toBe(false);

		expect(info).toHaveBeenCalledWith(
			expect.stringContaining('[email:password-reset] bridge not configured')
		);
	});

	it('nulls the greeting when the reset email has no user name', async () => {
		restores.push(
			withEnv({ INTERNAL_BRIDGE_URL: 'http://bridge', INTERNAL_BRIDGE_TOKEN: 'bridge-secret' })
		);
		const stub = installFetch(() => jsonResponse({ sent: true }));
		fetchStubs.push(stub.restore);

		await expect(
			sendTransactionalEmail('password-reset', { to: 'player@example.com', url: 'https://v/x' })
		).resolves.toBe(true);

		expect(JSON.parse(stub.calls[0].body ?? '{}')).toEqual({
			to: 'player@example.com',
			userName: null,
			resetUrl: 'https://v/x'
		});
	});
});
