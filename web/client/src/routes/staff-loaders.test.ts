import { beforeEach, describe, expect, it, vi } from 'vitest';
import { callLoad, fakeUser, makeServerEvent, type FakeServerEvent } from '#lib/testing/event';
import { load as adminLayout } from './(admin)/+layout.server';
import { load as editorialLayout } from './(editorial)/+layout.server';
import { load as foundryLayout } from './(foundry)/+layout.server';
import { load as modLayout } from './(mod)/+layout.server';
import { load as modOverview } from './(mod)/mod/+page.server';
import { actions as reportActions, load as modReports } from './(mod)/mod/reports/+page.server';
import { load as modUserList } from './(mod)/mod/users/+page.server';
import { actions as userActions, load as modUserDetail } from './(mod)/mod/users/[id]/+page.server';

/**
 * The staff route groups: admin, moderation, editorial, and Foundry.
 *
 * The group layouts are the only thing standing between a normal player and a moderation action, so
 * each one is asserted three ways: anonymous, signed in without the permission, and authorised. The
 * page modules then get their own load and form-action coverage. `#lib/server/admin-api` is mocked;
 * `requireUser` and `requirePermission` are real.
 */

const adminApi = vi.hoisted(() => ({
	listUsers: vi.fn(),
	listModUsers: vi.fn(),
	listModReports: vi.fn(),
	getModUser: vi.fn(),
	listReports: vi.fn(),
	kickPlayer: vi.fn(),
	warnPlayer: vi.fn(),
	mutePlayer: vi.fn(),
	unmutePlayer: vi.fn(),
	resolveModReport: vi.fn()
}));

/** The Foundry layout asks the inbox for a badge count on every page. */
const foundryApi = vi.hoisted(() => ({
	getUnreadNotificationCount: vi.fn()
}));

vi.mock('#lib/server/admin-api', () => adminApi);
vi.mock('#lib/server/foundry-api', () => foundryApi);

/** The RBAC snapshot the group layouts read from `parent()`. */
interface Access {
	accountId: string;
	username: string;
	roles: string[];
	permissions: string[];
}

const adminAccess: Access = {
	accountId: 'acct-1',
	username: 'kaleth',
	roles: ['Admin'],
	permissions: []
};

const playerAccess: Access = {
	accountId: 'acct-1',
	username: 'kaleth',
	roles: [],
	permissions: []
};

/** A signed-in event whose `parent()` hands back the given access snapshot. */
function staffEvent(
	access: Access | null = adminAccess,
	init: Parameters<typeof makeServerEvent>[0] = {}
): FakeServerEvent {
	const event = makeServerEvent({ user: fakeUser(), ...init });
	event.parent = (async () => ({ access })) as never;
	return event;
}

const ok = <T>(data: T) => ({ status: 200, data, error: null });
const failed = (error: string, status = 500) => ({ status, data: null, error });

/**
 * The message a SvelteKit `error(...)` was thrown with.
 *
 * `HttpError.message` is empty — the text lives in `body.message` — so `toThrow('…')` cannot be used
 * to assert a 403 or a 404 message.
 */
async function thrownMessage(run: () => Promise<unknown> | unknown): Promise<string> {
	try {
		await run();
	} catch (thrown) {
		const body = (thrown as { body?: { message?: string } }).body;
		return body?.message ?? '';
	}

	throw new Error('expected the call to throw');
}

const page = <T>(items: T[], total = items.length) => ({
	items,
	total,
	page: 1,
	pageSize: 25
});

const player = {
	accountId: 'acct-2',
	username: 'malfeasor',
	email: 'bad@example.com',
	roles: ['Player'],
	permissions: [],
	createdAt: '2026-01-02T03:04:05.000Z',
	lastSeenAt: null,
	status: 'Active'
};

beforeEach(() => {
	foundryApi.getUnreadNotificationCount.mockResolvedValue(ok({ count: 4 }));
	adminApi.listModUsers.mockResolvedValue(ok(page([player])));
	adminApi.listModReports.mockResolvedValue(ok(page([])));
	adminApi.getModUser.mockResolvedValue(ok(player));
	adminApi.kickPlayer.mockResolvedValue(ok(null));
	adminApi.warnPlayer.mockResolvedValue(ok(null));
	adminApi.mutePlayer.mockResolvedValue(ok(null));
	adminApi.unmutePlayer.mockResolvedValue(ok(null));
	adminApi.resolveModReport.mockResolvedValue(ok(null));
	adminApi.listUsers.mockResolvedValue(ok(page([player])));
	adminApi.listReports.mockResolvedValue(ok(page([])));
});

describe('group layouts', () => {
	it.each([
		['admin', adminLayout, 'Admin access requires user management permission.'],
		['editorial', editorialLayout, 'Editorial management requires content access.'],
		['moderation', modLayout, 'Moderation access requires the players.view permission.']
	])('guards the %s group', async (_name, load, message) => {
		await expect(callLoad(load, makeServerEvent())).rejects.toThrow();

		expect(await thrownMessage(() => callLoad(load, staffEvent(playerAccess)))).toBe(message);

		const result = await callLoad(load, staffEvent(adminAccess));
		expect(result).toEqual({ access: adminAccess });
	});

	it('lets a holder of the specific permission in without the Admin role', async () => {
		const editor = { ...playerAccess, permissions: ['content.manage'] };

		await expect(callLoad(editorialLayout, staffEvent(editor))).resolves.toEqual({
			access: editor
		});
	});

	it('only requires a session for the Foundry group and derives its nav flags', async () => {
		await expect(callLoad(foundryLayout, makeServerEvent())).rejects.toThrow();

		const result = (await callLoad(foundryLayout, makeServerEvent({ user: fakeUser() }))) as {
			user: unknown;
			canAudit: boolean;
			canManageContent: boolean;
			canReview: boolean;
			unreadNotifications: number;
		};

		expect(result.user).toEqual(fakeUser());
		// No RBAC snapshot in this event, so every capability is denied rather than assumed. Reading is not a
		// capability any more: the portal is open to every signed-in account.
		expect(result.canAudit).toBe(false);
		expect(result.canManageContent).toBe(false);
		expect(result.canReview).toBe(false);
		expect(result.unreadNotifications).toBe(4);
		// The layout hands out flags, not the whole access object.
		expect(result).not.toHaveProperty('access');
	});

	it('gates the review entry on the review permission, which is what the decisions require', async () => {
		const curator: Access = {
			accountId: 'acct-3',
			username: 'sereth',
			roles: ['Curator'],
			permissions: ['content.review', 'content.view']
		};

		const asCurator = (await callLoad(foundryLayout, staffEvent(curator))) as {
			canAudit: boolean;
			canReview: boolean;
		};

		expect(asCurator.canReview).toBe(true);
		expect(asCurator.canAudit).toBe(true);

		// An Admin holds every permission but not the Curator role. The decision endpoints check the
		// permission, so the queue is offered rather than hidden behind a role the account never needed.
		const asAdmin = (await callLoad(foundryLayout, staffEvent(adminAccess))) as {
			canReview: boolean;
		};

		expect(asAdmin.canReview).toBe(true);
	});

	it('degrades a failed unread count to zero instead of failing the whole group', async () => {
		foundryApi.getUnreadNotificationCount.mockResolvedValueOnce(failed('the inbox is down', 502));

		const result = (await callLoad(foundryLayout, staffEvent(playerAccess))) as {
			unreadNotifications: number;
		};

		expect(result.unreadNotifications).toBe(0);
	});
});

describe('moderation overview', () => {
	it('counts users and open reports', async () => {
		adminApi.listModUsers.mockResolvedValue(ok(page([player], 42)));
		adminApi.listModReports.mockResolvedValue(ok(page([], 7)));

		const result = await callLoad(modOverview, makeServerEvent({ user: fakeUser() }));

		expect(result).toEqual({ userCount: 42, openReports: 7, errors: [] });
		expect(adminApi.listModReports).toHaveBeenCalledWith(expect.anything(), false, 1, 1);
	});

	it('falls back to zero and collects the errors when both calls fail', async () => {
		adminApi.listModUsers.mockResolvedValue(failed('users are down'));
		adminApi.listModReports.mockResolvedValue(failed('reports are down'));

		const result = await callLoad(modOverview, makeServerEvent({ user: fakeUser() }));

		expect(result).toEqual({
			userCount: 0,
			openReports: 0,
			errors: ['users are down', 'reports are down']
		});
	});
});

describe('moderation user list', () => {
	it('passes the filters through and clamps the page', async () => {
		await callLoad(
			modUserList,
			makeServerEvent({ user: fakeUser(), url: '/mod/users?search=kal&status=Banned&page=0' })
		);

		expect(adminApi.listModUsers).toHaveBeenCalledWith(expect.anything(), {
			search: 'kal',
			status: 'Banned',
			sort: null,
			page: 1,
			pageSize: 25
		});
	});

	it('returns the fallbacks when the API fails', async () => {
		adminApi.listModUsers.mockResolvedValue(failed('nope'));

		const result = await callLoad(modUserList, makeServerEvent({ user: fakeUser() }));

		expect(result).toMatchObject({
			users: [],
			total: 0,
			page: 1,
			pageSize: 25,
			error: 'nope'
		});
	});
});

describe('moderation user detail', () => {
	const detail = (init: Parameters<typeof makeServerEvent>[0] = {}) =>
		makeServerEvent({ user: fakeUser(), params: { id: 'acct-2' }, ...init });

	it('404s a missing account', async () => {
		adminApi.getModUser.mockResolvedValue(failed('Account not found.', 404));

		expect(await thrownMessage(() => callLoad(modUserDetail, detail()))).toBe('Account not found.');
	});

	it('returns the player', async () => {
		const result = (await callLoad(modUserDetail, detail())) as { player: unknown };

		expect(result.player).toEqual(player);
		expect(adminApi.getModUser).toHaveBeenCalledWith(expect.anything(), 'acct-2');
	});

	it('kicks with an optional reason and returns to the page', async () => {
		await expect(userActions.kick.call(undefined, detail({ form: {} }) as never)).rejects.toThrow();

		expect(adminApi.kickPlayer).toHaveBeenCalledWith(expect.anything(), 'acct-2', null);

		await expect(
			userActions.kick.call(undefined, detail({ form: { reason: 'spam' } }) as never)
		).rejects.toThrow();
		expect(adminApi.kickPlayer).toHaveBeenLastCalledWith(expect.anything(), 'acct-2', 'spam');
	});

	it('reports a failed kick as a 400', async () => {
		adminApi.kickPlayer.mockResolvedValue(failed('Player is offline'));

		const result = (await userActions.kick.call(
			undefined,
			detail({ form: { reason: 'spam' } }) as never
		)) as { status: number; data: { error: string } };

		expect(result.status).toBe(400);
		expect(result.data.error).toBe('Player is offline');
	});

	it('requires a warning reason', async () => {
		const result = (await userActions.warn.call(
			undefined,
			detail({ form: { reason: '   ' } }) as never
		)) as { status: number; data: { error: string } };

		expect(result.status).toBe(400);
		expect(result.data.error).toBe('A reason is required.');
		expect(adminApi.warnPlayer).not.toHaveBeenCalled();
	});

	it('warns with the trimmed reason', async () => {
		await expect(
			userActions.warn.call(undefined, detail({ form: { reason: 'disruptive' } }) as never)
		).rejects.toThrow();

		expect(adminApi.warnPlayer).toHaveBeenCalledWith(expect.anything(), 'acct-2', 'disruptive');
	});

	it('mutes with a duration and unmutes', async () => {
		await expect(
			userActions.mute.call(
				undefined,
				detail({ form: { reason: 'flooding', durationMinutes: '30' } }) as never
			)
		).rejects.toThrow();

		expect(adminApi.mutePlayer).toHaveBeenCalledWith(expect.anything(), 'acct-2', 'flooding', 30);

		await expect(userActions.unmute.call(undefined, detail() as never)).rejects.toThrow();
		expect(adminApi.unmutePlayer).toHaveBeenCalledWith(expect.anything(), 'acct-2');
	});

	it('reports a failed unmute as a 400', async () => {
		adminApi.unmutePlayer.mockResolvedValue(failed('Not muted'));

		const result = (await userActions.unmute.call(undefined, detail() as never)) as {
			status: number;
			data: { error: string };
		};

		expect(result.status).toBe(400);
		expect(result.data.error).toBe('Not muted');
	});
});

describe('moderation reports', () => {
	it.each([
		['true', true],
		['false', false],
		[null, null]
	])('maps ?resolved=%s to %s', async (query, expected) => {
		await callLoad(
			modReports,
			makeServerEvent({
				user: fakeUser(),
				url: query === null ? '/mod/reports' : `/mod/reports?resolved=${query}`
			})
		);

		expect(adminApi.listModReports).toHaveBeenLastCalledWith(expect.anything(), expected, 1, 50);
	});

	it('passes the page through and defaults on failure', async () => {
		await callLoad(modReports, makeServerEvent({ user: fakeUser(), url: '/mod/reports?page=3' }));
		expect(adminApi.listModReports).toHaveBeenLastCalledWith(expect.anything(), null, 3, 50);

		adminApi.listModReports.mockResolvedValue(failed('reports are down'));
		const result = await callLoad(modReports, makeServerEvent({ user: fakeUser() }));

		expect(result).toMatchObject({
			reports: [],
			total: 0,
			page: 1,
			pageSize: 50,
			error: 'reports are down'
		});
	});

	it('resolves a report and returns to the list', async () => {
		await expect(
			reportActions.resolve.call(
				undefined,
				makeServerEvent({ user: fakeUser(), form: { id: 'report-1' } }) as never
			)
		).rejects.toThrow();

		expect(adminApi.resolveModReport).toHaveBeenCalledWith(expect.anything(), 'report-1');
	});

	it('reports a failed resolve as a 400', async () => {
		adminApi.resolveModReport.mockResolvedValue(failed('Already resolved'));

		const result = (await reportActions.resolve.call(
			undefined,
			makeServerEvent({ user: fakeUser(), form: { id: 'report-1' } }) as never
		)) as { status: number; data: { error: string } };

		expect(result.status).toBe(400);
		expect(result.data.error).toBe('Already resolved');
	});
});
