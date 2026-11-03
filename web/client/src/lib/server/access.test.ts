import { afterEach, describe, expect, it, vi } from 'vitest';
import { fakeUser } from '#lib/testing/event';
import {
	Permissions,
	Roles,
	hasAnyPermission,
	hasPermission,
	loadAccountAccess,
	requirePermission,
	requireUser,
	toGameIdentity
} from '#lib/server/access';
import type { AccountAccess } from '#lib/server/access';

/**
 * RBAC helpers are UX guards in front of pages that the .NET API protects anyway. A regression
 * here does not open data, but it does show the wrong page to the wrong person — or bounce a
 * staff member out of their own portal.
 */

const gameApiFetch = vi.hoisted(() => vi.fn());

vi.mock('#lib/server/game-request', () => ({ gameApiFetch }));

afterEach(() => {
	gameApiFetch.mockReset();
	vi.restoreAllMocks();
});

const access = (overrides: Partial<AccountAccess> = {}): AccountAccess => ({
	accountId: 'acc-1',
	username: 'tester',
	roles: [],
	permissions: [],
	...overrides
});

/** Captures the SvelteKit redirect/error object a guard throws. */
function thrown(fn: () => unknown): {
	status: number;
	location?: string;
	body?: { message: string };
} {
	try {
		fn();
	} catch (error) {
		return error as { status: number; location?: string; body?: { message: string } };
	}

	throw new Error('Expected the guard to throw.');
}

describe('requireUser', () => {
	it('redirects anonymous visitors to the login page', () => {
		const redirect = thrown(() => requireUser({}));

		expect(redirect.status).toBe(303);
		expect(redirect.location).toBe('/login');
	});

	it('returns the signed-in user', () => {
		const user = fakeUser();

		expect(requireUser({ user })).toBe(user);
	});
});

describe('toGameIdentity', () => {
	it('maps the display name onto the username when no username is set', () => {
		const identity = toGameIdentity(fakeUser({ username: null, name: 'Player One' }));

		expect(identity).toEqual({
			id: '11111111-1111-4111-8111-111111111111',
			username: 'Player One',
			email: 'player@example.com',
			emailVerified: true
		});
	});
});

describe('loadAccountAccess', () => {
	it('returns null and warns when the profile cannot be loaded', async () => {
		const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
		gameApiFetch.mockResolvedValue({ status: 503, data: null, error: 'down' });

		await expect(loadAccountAccess(fakeUser())).resolves.toBeNull();
		expect(warn).toHaveBeenCalled();
	});

	it('normalises missing role and permission arrays', async () => {
		gameApiFetch.mockResolvedValue({
			status: 200,
			data: { accountId: 'acc-1', username: 'tester' },
			error: null
		});

		await expect(loadAccountAccess(fakeUser())).resolves.toEqual({
			accountId: 'acc-1',
			username: 'tester',
			roles: [],
			permissions: []
		});
	});

	it('returns the snapshot from the account profile endpoint', async () => {
		gameApiFetch.mockResolvedValue({
			status: 200,
			data: {
				accountId: 'acc-1',
				username: 'tester',
				roles: [Roles.Moderator],
				permissions: [Permissions.ViewPlayers]
			},
			error: null
		});

		const snapshot = await loadAccountAccess(fakeUser());

		expect(snapshot).toMatchObject({ roles: ['Moderator'], permissions: ['players.view'] });
		expect(gameApiFetch.mock.calls[0][1]).toBe('/api/account/profile');
	});
});

describe('hasPermission', () => {
	it('is false without a snapshot', () => {
		expect(hasPermission(null, Permissions.ManageUsers)).toBe(false);
	});

	it('grants everything to Admins', () => {
		expect(hasPermission(access({ roles: [Roles.Admin] }), Permissions.BanPlayers)).toBe(true);
	});

	it('grants a listed permission and denies the rest', () => {
		const snapshot = access({ permissions: [Permissions.ViewPlayers] });

		expect(hasPermission(snapshot, Permissions.ViewPlayers)).toBe(true);
		expect(hasPermission(snapshot, Permissions.ManageUsers)).toBe(false);
	});
});

describe('requirePermission', () => {
	it('throws 403 with the supplied message', () => {
		const failure = thrown(() => requirePermission(null, Permissions.ManageUsers, 'Admins only.'));

		expect(failure.status).toBe(403);
		expect(failure.body?.message).toBe('Admins only.');
	});

	it('uses the default message when none is supplied', () => {
		const failure = thrown(() => requirePermission(access(), Permissions.ManageUsers));

		expect(failure.body?.message).toBe('You do not have permission to view this page.');
	});

	it('returns the snapshot when the permission is held', () => {
		const snapshot = access({ permissions: [Permissions.ManageContent] });

		expect(requirePermission(snapshot, Permissions.ManageContent)).toBe(snapshot);
	});
});

describe('hasAnyPermission', () => {
	it('is true when one of the permissions is held', () => {
		const snapshot = access({ permissions: [Permissions.ViewPlayers] });

		expect(hasAnyPermission(snapshot, [Permissions.ManageUsers, Permissions.ViewPlayers])).toBe(
			true
		);
	});

	it('is false for an empty snapshot or an empty list', () => {
		expect(hasAnyPermission(null, [Permissions.ViewPlayers])).toBe(false);
		expect(hasAnyPermission(access(), [])).toBe(false);
	});
});
