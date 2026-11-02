import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { withEnv } from '#lib/testing/env';
import { callLoad, fakeSession, fakeUser, makeServerEvent } from '#lib/testing/event';
import { load as accountLoad } from './account/+page.server';
import { load as loginLoad } from './login/+page.server';
import { load as registerLoad } from './register/+page.server';
import { load as verifyEmailLoad } from './verify-email/+page.server';

/**
 * Auth and account pages.
 *
 * Three behaviours matter here and none of them are visible in the UI until they break: signed-in
 * visitors must not see the login form, a verification link must never throw at the player, and the
 * account page must still render when Better Auth cannot list sessions.
 */

const auth = vi.hoisted(() => ({ getAuth: vi.fn() }));
const account = vi.hoisted(() => ({ listLinkedProviders: vi.fn() }));

vi.mock('#lib/server/auth', () => auth);
vi.mock('#lib/server/account', () => account);

const restores: Array<() => void> = [];

const providerEnv = {
	DISCORD_CLIENT_ID: 'discord-id',
	DISCORD_CLIENT_SECRET: 'discord-secret',
	GOOGLE_CLIENT_ID: 'google-id',
	GOOGLE_CLIENT_SECRET: 'google-secret',
	MICROSOFT_CLIENT_ID: 'microsoft-id',
	MICROSOFT_CLIENT_SECRET: 'microsoft-secret'
};

beforeEach(() => {
	account.listLinkedProviders.mockResolvedValue([]);
	auth.getAuth.mockReturnValue({ api: { listSessions: vi.fn(async () => []) } });
});

afterEach(() => {
	account.listLinkedProviders.mockReset();
	auth.getAuth.mockReset();
	for (const restore of restores.splice(0)) restore();
	vi.restoreAllMocks();
});

describe('signed-in-only pages', () => {
	it.each([
		['login', loginLoad],
		['register', registerLoad]
	])('sends a signed-in visitor away from %s', (_name, load) => {
		expect(() => callLoad(load, makeServerEvent({ user: fakeUser() }))).toThrow();
	});

	it.each([
		['login', loginLoad],
		['register', registerLoad]
	])('lets an anonymous visitor see %s', (_name, load) => {
		expect(callLoad(load, makeServerEvent())).toBeUndefined();
	});
});

describe('verify email', () => {
	it('reports a missing token without calling Better Auth', async () => {
		await expect(
			callLoad(verifyEmailLoad, makeServerEvent({ url: '/verify-email' }))
		).resolves.toEqual({ status: 'missing' });
		expect(auth.getAuth).not.toHaveBeenCalled();
	});

	it('reports a successful verification', async () => {
		const verifyEmail = vi.fn(async () => ({}));
		auth.getAuth.mockReturnValue({ api: { verifyEmail } });

		await expect(
			callLoad(verifyEmailLoad, makeServerEvent({ url: '/verify-email?token=abc' }))
		).resolves.toEqual({ status: 'verified' });
		expect(verifyEmail).toHaveBeenCalledWith({ query: { token: 'abc' } });
	});

	it('reports an invalid link instead of throwing at the player', async () => {
		const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
		auth.getAuth.mockReturnValue({
			api: {
				verifyEmail: vi.fn(async () => {
					throw new Error('expired');
				})
			}
		});

		await expect(
			callLoad(verifyEmailLoad, makeServerEvent({ url: '/verify-email?token=stale' }))
		).resolves.toEqual({ status: 'invalid' });
		expect(warn).toHaveBeenCalled();
	});
});

describe('account page', () => {
	it('redirects anonymous visitors', async () => {
		await expect(callLoad(accountLoad, makeServerEvent())).rejects.toMatchObject({
			status: 303,
			location: '/login'
		});
	});

	it('maps linked providers, sessions, and configured providers', async () => {
		restores.push(withEnv(providerEnv));
		const user = fakeUser();
		account.listLinkedProviders.mockResolvedValue([
			{ id: 'acc-1', providerId: 'discord', createdAt: '2026-01-02T03:04:05.000Z' }
		]);
		auth.getAuth.mockReturnValue({
			api: {
				listSessions: vi.fn(async () => [
					{
						id: 'sess-1',
						token: 'token-1',
						createdAt: new Date('2026-01-01T00:00:00.000Z'),
						expiresAt: new Date('2026-01-08T00:00:00.000Z'),
						ipAddress: '127.0.0.1',
						userAgent: 'vitest'
					}
				])
			}
		});

		const data = (await callLoad(
			accountLoad,
			makeServerEvent({ user, session: fakeSession(user), url: '/account?error=access_denied' })
		)) as Record<string, unknown>;

		expect(data.providers).toEqual([
			{ id: 'acc-1', providerId: 'discord', createdAt: '2026-01-02T03:04:05.000Z' }
		]);
		expect(data.sessions).toEqual([
			{
				id: 'sess-1',
				token: 'token-1',
				createdAt: '2026-01-01T00:00:00.000Z',
				expiresAt: '2026-01-08T00:00:00.000Z',
				ipAddress: '127.0.0.1',
				userAgent: 'vitest'
			}
		]);
		expect(data.currentSessionId).toBe('22222222-2222-4222-8222-222222222222');
		expect(data.linkableProviders).toEqual([
			{ id: 'discord', label: 'Discord' },
			{ id: 'google', label: 'Google' },
			{ id: 'microsoft', label: 'Microsoft' }
		]);
		expect(data.oauthError).toBe('access_denied');
	});

	it('renders with empty lists when the provider and session lookups fail', async () => {
		restores.push(
			withEnv({
				DISCORD_CLIENT_ID: undefined,
				DISCORD_CLIENT_SECRET: undefined,
				GOOGLE_CLIENT_ID: 'google-id',
				GOOGLE_CLIENT_SECRET: undefined,
				MICROSOFT_CLIENT_ID: undefined,
				MICROSOFT_CLIENT_SECRET: undefined
			})
		);
		const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
		account.listLinkedProviders.mockRejectedValue(new Error('db down'));
		auth.getAuth.mockReturnValue({
			api: {
				listSessions: vi.fn(async () => {
					throw new Error('auth down');
				})
			}
		});

		const data = (await callLoad(
			accountLoad,
			makeServerEvent({ user: fakeUser(), session: null })
		)) as Record<string, unknown>;

		expect(data.providers).toEqual([]);
		expect(data.sessions).toEqual([]);
		expect(data.currentSessionId).toBeNull();
		// A provider is only linkable when both halves of its credentials are present.
		expect(data.linkableProviders).toEqual([]);
		expect(data.oauthError).toBeNull();
		expect(warn).toHaveBeenCalledTimes(2);
	});
});
