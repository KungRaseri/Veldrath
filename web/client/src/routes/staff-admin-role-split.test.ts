import { beforeEach, describe, expect, it, vi } from 'vitest';
import { callLoad, fakeUser, makeServerEvent, type FakeServerEvent } from '#lib/testing/event';
import {
	load as adminUserDetail,
	actions as adminUserActions
} from './(admin)/admin/users/[id]/+page.server';
import { actions as rolesActions, load as rolesLoad } from './(admin)/admin/roles/+page.server';

/**
 * The client half of the `admin.roles` split.
 *
 * `admin.roles` admits the four role/permission mutations and `admin.users` admits account
 * administration; neither implies the other. These tests pin the two halves from the outside: which
 * endpoint a page reads, which controls an operator is allowed to reach, and — the reason the split
 * exists — that an action for a permission the session does not hold never reaches the API.
 */

const adminApi = vi.hoisted(() => ({
	getUser: vi.fn(),
	getUserAuthority: vi.fn(),
	getUserAuthorityByUsername: vi.fn(),
	assignRole: vi.fn(),
	revokeRole: vi.fn(),
	grantPermission: vi.fn(),
	revokePermission: vi.fn(),
	kickPlayer: vi.fn(),
	warnPlayer: vi.fn(),
	mutePlayer: vi.fn(),
	unmutePlayer: vi.fn(),
	banPlayer: vi.fn(),
	unbanPlayer: vi.fn()
}));

/** The profile read behind `requireActionPermission`; the staff API itself stays mocked. */
const profileFetch = vi.hoisted(() => vi.fn());

vi.mock('#lib/server/admin-api', () => adminApi);
vi.mock('#lib/server/game-request', async (importOriginal) => ({
	...(await importOriginal<typeof import('#lib/server/game-request')>()),
	gameApiFetch: profileFetch
}));

const ok = <T>(data: T) => ({ status: 200, data, error: null });
const failed = (error: string | null, status = 500) => ({ status, data: null, error });

/** A snapshot holding exactly the supplied permissions, with no role that would grant the rest. */
const accessWith = (...permissions: string[]) => ({
	accountId: 'acct-actor',
	username: 'operator',
	roles: [] as string[],
	permissions
});

const rolesOnly = accessWith('admin.roles');
const usersOnly = accessWith('admin.users');

const authority = {
	id: 'acct-2',
	username: 'malfeasor',
	roles: ['Curator'],
	userPermissions: ['players.ban'],
	effectivePermissions: ['content.review', 'players.ban']
};

const account = {
	...authority,
	email: 'bad@example.com',
	isBanned: false,
	isBannedUntil: null,
	warnCount: 3,
	isMuted: false,
	createdAt: '2026-01-02T03:04:05.000Z',
	lastSeenAt: null
};

/** A signed-in event whose `parent()` hands back the given access snapshot. */
function staffEvent(
	access: unknown,
	init: Parameters<typeof makeServerEvent>[0] = {}
): FakeServerEvent {
	return makeServerEvent({ user: fakeUser(), parentData: { access }, ...init });
}

/** Invokes one of the account page's form actions with the supplied form fields. */
function accountAction(handler: unknown, form: Record<string, string>) {
	return (handler as (event: FakeServerEvent) => Promise<unknown>)(
		staffEvent(rolesOnly, { method: 'POST', form, params: { id: 'acct-2' } })
	);
}

/** Invokes the role page's `open` action as the supplied caller. */
function rolePageAction(
	handler: unknown,
	form: Record<string, string>,
	access: unknown = rolesOnly
) {
	return (handler as (event: FakeServerEvent) => Promise<unknown>)(
		staffEvent(access, { method: 'POST', form })
	);
}

/** The location a SvelteKit `redirect(...)` was thrown with. */
async function redirectTo(run: () => Promise<unknown> | unknown): Promise<string> {
	try {
		await run();
	} catch (thrown) {
		const location = (thrown as { location?: string }).location;
		if (location) return location;
		throw thrown;
	}

	throw new Error('expected a redirect');
}

beforeEach(() => {
	vi.resetAllMocks();
	profileFetch.mockResolvedValue(ok(rolesOnly));
	adminApi.getUser.mockResolvedValue(ok(account));
	adminApi.getUserAuthority.mockResolvedValue(ok(authority));
	adminApi.getUserAuthorityByUsername.mockResolvedValue(ok(authority));
	for (const spy of [
		adminApi.assignRole,
		adminApi.revokeRole,
		adminApi.grantPermission,
		adminApi.revokePermission,
		adminApi.kickPlayer,
		adminApi.warnPlayer,
		adminApi.mutePlayer,
		adminApi.unmutePlayer,
		adminApi.banPlayer,
		adminApi.unbanPlayer
	]) {
		spy.mockResolvedValue(ok(null));
	}
});

describe('the account page reads the half of the split the caller holds', () => {
	const detail = (access: unknown) => staffEvent(access, { params: { id: 'acct-2' } });

	it('reads the authority endpoint for a role manager, never the account-admin one', async () => {
		const result = (await callLoad(adminUserDetail, detail(rolesOnly))) as {
			account: unknown;
			authority: typeof authority;
			canManageUsers: boolean;
			canManageRoles: boolean;
		};

		expect(adminApi.getUserAuthority).toHaveBeenCalledWith(expect.anything(), 'acct-2');
		expect(adminApi.getUser).not.toHaveBeenCalled();
		expect(result.account).toBeNull();
		expect(result.authority).toEqual(authority);
		expect(result.canManageUsers).toBe(false);
		expect(result.canManageRoles).toBe(true);
	});

	it('reads the account detail for an account administrator', async () => {
		const result = (await callLoad(adminUserDetail, detail(usersOnly))) as {
			account: typeof account;
			authority: typeof authority;
			canManageUsers: boolean;
			canManageRoles: boolean;
		};

		expect(adminApi.getUser).toHaveBeenCalledWith(expect.anything(), 'acct-2');
		expect(adminApi.getUserAuthority).not.toHaveBeenCalled();
		expect(result.account).toEqual(account);
		// The roles and grants still render, read-only: the controls are what admin.roles owns.
		expect(result.authority.roles).toEqual(['Curator']);
		expect(result.canManageUsers).toBe(true);
		expect(result.canManageRoles).toBe(false);
	});

	it('refuses a caller with neither permission without calling the API', async () => {
		await expect(
			callLoad(adminUserDetail, detail(accessWith('players.view')))
		).rejects.toMatchObject({ status: 403 });

		expect(adminApi.getUser).not.toHaveBeenCalled();
		expect(adminApi.getUserAuthority).not.toHaveBeenCalled();
	});
});

describe('role and permission actions require admin.roles', () => {
	it.each([
		['assignRole', adminApi.assignRole, { role: 'Curator' }],
		['revokeRole', adminApi.revokeRole, { role: 'Curator' }],
		['grantPermission', adminApi.grantPermission, { permission: 'players.ban' }],
		['revokePermission', adminApi.revokePermission, { permission: 'players.ban' }]
	] as const)('lets a role manager %s', async (name, spy, form) => {
		expect(await redirectTo(() => accountAction(adminUserActions[name], form))).toBe(
			'/admin/users/acct-2'
		);
		expect(spy).toHaveBeenCalledWith(expect.anything(), 'acct-2', Object.values(form)[0]);
	});

	it.each([
		['assignRole', adminApi.assignRole, { role: 'Curator' }],
		['grantPermission', adminApi.grantPermission, { permission: 'players.ban' }]
	] as const)(
		'refuses %s for an account administrator and makes no call',
		async (name, spy, form) => {
			profileFetch.mockResolvedValue(ok(usersOnly));

			await expect(accountAction(adminUserActions[name], form)).rejects.toMatchObject({
				status: 403
			});

			expect(spy).not.toHaveBeenCalled();
		}
	);
});

describe('account actions require admin.users', () => {
	it.each([
		['kick', adminApi.kickPlayer, { reason: 'spam' }],
		['ban', adminApi.banPlayer, { reason: 'harassment' }]
	] as const)('refuses %s for a role manager and makes no call', async (name, spy, form) => {
		await expect(accountAction(adminUserActions[name], form)).rejects.toMatchObject({
			status: 403
		});

		expect(spy).not.toHaveBeenCalled();
	});
});

describe('the role page opens an account by username', () => {
	it('offers the lookup to a role manager and reports both halves of the split', async () => {
		const result = (await callLoad(rolesLoad, staffEvent(rolesOnly))) as {
			canManageRoles: boolean;
			canManageUsers: boolean;
		};

		expect(result.canManageRoles).toBe(true);
		expect(result.canManageUsers).toBe(false);
	});

	it('redirects a role manager to the account page that owns the forms', async () => {
		expect(
			await redirectTo(() => rolePageAction(rolesActions.open, { username: 'malfeasor' }))
		).toBe('/admin/users/acct-2');
		expect(adminApi.getUserAuthorityByUsername).toHaveBeenCalledWith(
			expect.anything(),
			'malfeasor'
		);
	});

	it('refuses the lookup for an account administrator', async () => {
		profileFetch.mockResolvedValue(ok(usersOnly));

		await expect(
			rolePageAction(rolesActions.open, { username: 'malfeasor' }, usersOnly)
		).rejects.toMatchObject({ status: 403 });

		expect(adminApi.getUserAuthorityByUsername).not.toHaveBeenCalled();
	});

	it('reports an unknown account without redirecting', async () => {
		adminApi.getUserAuthorityByUsername.mockResolvedValue(failed('Account not found.', 404));

		const result = (await rolePageAction(rolesActions.open, { username: 'nobody' })) as {
			status: number;
			data: { error: string };
		};

		expect(result.status).toBe(404);
		expect(result.data.error).toBe('Account not found.');
	});

	it('requires a username', async () => {
		const result = (await rolePageAction(rolesActions.open, { username: '  ' })) as {
			status: number;
			data: { error: string };
		};

		expect(result.status).toBe(400);
		expect(adminApi.getUserAuthorityByUsername).not.toHaveBeenCalled();
	});
});
