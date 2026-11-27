import { beforeEach, describe, expect, it, vi } from 'vitest';
import { callLoad, fakeUser, makeServerEvent, type FakeServerEvent } from '#lib/testing/event';
import { load as adminOverview } from './(admin)/admin/+page.server';
import { load as adminAudit } from './(admin)/admin/audit/+page.server';
import {
	actions as adminReportActions,
	load as adminReports
} from './(admin)/admin/reports/+page.server';
import { load as adminRoles } from './(admin)/admin/roles/+page.server';
import { actions as adminServerActions } from './(admin)/admin/server/+page.server';
import { load as adminSessions } from './(admin)/admin/sessions/+page.server';
import { load as adminStatus } from './(admin)/admin/status/+page.server';
import { load as adminUsers } from './(admin)/admin/users/+page.server';
import {
	actions as adminUserActions,
	load as adminUserDetail
} from './(admin)/admin/users/[id]/+page.server';
import { load as editorialOverview } from './(editorial)/editorial/+page.server';
import {
	actions as announcementActions,
	load as announcementDetail
} from './(editorial)/editorial/announcements/[id]/+page.server';
import { load as announcementList } from './(editorial)/editorial/announcements/+page.server';
import {
	actions as loreActions,
	load as loreDetail
} from './(editorial)/editorial/lore/[id]/+page.server';
import { load as loreList } from './(editorial)/editorial/lore/+page.server';
import {
	actions as noteActions,
	load as noteDetail
} from './(editorial)/editorial/patch-notes/[id]/+page.server';
import { load as noteList } from './(editorial)/editorial/patch-notes/+page.server';

/**
 * The admin and editorial page modules.
 *
 * Both groups are almost entirely loads and form actions, so the coverage is a matrix over what each
 * one validates, which API it calls, and what it returns: a redirect on success, a `fail(400)` with
 * the upstream message on a rejected write, and a `404` for a missing row. The group layouts (the
 * permission gates) are covered in `staff-loaders.test.ts`.
 */

const adminApi = vi.hoisted(() => ({
	getServerStatus: vi.fn(),
	listUsers: vi.fn(),
	getUser: vi.fn(),
	assignRole: vi.fn(),
	revokeRole: vi.fn(),
	grantPermission: vi.fn(),
	revokePermission: vi.fn(),
	kickPlayer: vi.fn(),
	warnPlayer: vi.fn(),
	mutePlayer: vi.fn(),
	unmutePlayer: vi.fn(),
	banPlayer: vi.fn(),
	unbanPlayer: vi.fn(),
	getAuditLog: vi.fn(),
	listReports: vi.fn(),
	resolveReport: vi.fn(),
	listSessions: vi.fn(),
	broadcastAnnouncement: vi.fn()
}));

const editorialApi = vi.hoisted(() => ({
	adminListLoreArticles: vi.fn(),
	adminGetLoreArticle: vi.fn(),
	adminCreateLoreArticle: vi.fn(),
	adminUpdateLoreArticle: vi.fn(),
	adminPublishLoreArticle: vi.fn(),
	adminDeleteLoreArticle: vi.fn(),
	adminListPatchNotes: vi.fn(),
	adminGetPatchNote: vi.fn(),
	adminCreatePatchNote: vi.fn(),
	adminUpdatePatchNote: vi.fn(),
	adminPublishPatchNote: vi.fn(),
	adminDeletePatchNote: vi.fn(),
	adminListAnnouncements: vi.fn(),
	adminGetAnnouncement: vi.fn(),
	adminCreateAnnouncement: vi.fn(),
	adminUpdateAnnouncement: vi.fn(),
	adminPublishAnnouncement: vi.fn(),
	adminDeleteAnnouncement: vi.fn()
}));

vi.mock('#lib/server/admin-api', () => adminApi);
vi.mock('#lib/server/editorial-api', () => editorialApi);

/**
 * The profile read behind `requireActionPermission`.
 *
 * Form actions run without layout data, so a gated action asks the profile endpoint for the caller's
 * snapshot before it touches the staff API. `#lib/server/access` is real here; its HTTP call is not.
 */
const profileFetch = vi.hoisted(() => vi.fn());

vi.mock('#lib/server/game-request', async (importOriginal) => ({
	...(await importOriginal<typeof import('#lib/server/game-request')>()),
	gameApiFetch: profileFetch
}));

/** The access snapshot the (admin) layout hands down, and the one every action re-reads. */
const staffAccess = {
	accountId: 'acct-1',
	username: 'kaleth',
	roles: ['Admin'],
	permissions: []
};

const ok = <T>(data: T) => ({ status: 200, data, error: null });
const failed = (error: string, status = 500) => ({ status, data: null, error });

const page = <T>(items: T[], total = items.length, extra: Record<string, unknown> = {}) => ({
	items,
	total,
	page: 1,
	pageSize: 25,
	...extra
});

const paged = <T>(items: T[], total = items.length) => ({
	items,
	totalCount: total,
	page: 1,
	pageSize: 20
});

const signedIn = (init: Parameters<typeof makeServerEvent>[0] = {}) =>
	makeServerEvent({ user: fakeUser(), parentData: { access: staffAccess }, ...init });

/** Invokes a form action with a fake event carrying the given form fields. */
function action(
	handler: unknown,
	form: Record<string, string>,
	init: Parameters<typeof makeServerEvent>[0] = {}
): Promise<unknown> {
	const event = signedIn({ method: 'POST', form, ...init });
	return (handler as (event: FakeServerEvent) => Promise<unknown>)(event);
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

/** The message a SvelteKit `error(...)` was thrown with (`HttpError.message` is empty). */
async function thrownMessage(run: () => Promise<unknown> | unknown): Promise<string> {
	try {
		await run();
	} catch (thrown) {
		return ((thrown as { body?: { message?: string } }).body?.message ?? '') as string;
	}

	throw new Error('expected the call to throw');
}

const player = {
	accountId: 'acct-2',
	username: 'malfeasor',
	email: 'bad@example.com',
	roles: ['Player'],
	permissions: []
};

beforeEach(() => {
	vi.resetAllMocks();
	profileFetch.mockResolvedValue(ok(staffAccess));
	adminApi.getServerStatus.mockResolvedValue(ok({ status: 'Healthy' }));
	adminApi.listUsers.mockResolvedValue(ok(page([player], 42)));
	adminApi.getUser.mockResolvedValue(ok(player));
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
		adminApi.unbanPlayer,
		adminApi.resolveReport,
		adminApi.broadcastAnnouncement
	]) {
		spy.mockResolvedValue(ok(null));
	}
	adminApi.getAuditLog.mockResolvedValue(ok(page([{ id: 'audit-1' }], 7)));
	adminApi.listReports.mockResolvedValue(ok(page([])));
	adminApi.listSessions.mockResolvedValue(ok([{ characterId: 'char-1' }]));

	editorialApi.adminListLoreArticles.mockResolvedValue(ok(paged([{ id: 'lore-1' }], 5)));
	editorialApi.adminListPatchNotes.mockResolvedValue(ok(paged([{ id: 'note-1' }], 3)));
	editorialApi.adminListAnnouncements.mockResolvedValue(ok(paged([{ id: 'ann-1' }], 2)));
	editorialApi.adminGetLoreArticle.mockResolvedValue(ok({ id: 'lore-1' }));
	editorialApi.adminGetPatchNote.mockResolvedValue(ok({ id: 'note-1' }));
	editorialApi.adminGetAnnouncement.mockResolvedValue(ok({ id: 'ann-1' }));
	for (const spy of [
		editorialApi.adminPublishLoreArticle,
		editorialApi.adminDeleteLoreArticle,
		editorialApi.adminPublishPatchNote,
		editorialApi.adminDeletePatchNote,
		editorialApi.adminPublishAnnouncement,
		editorialApi.adminDeleteAnnouncement
	]) {
		spy.mockResolvedValue(ok(null));
	}
});

describe('admin dashboard and status', () => {
	it('passes the server status through, error included', async () => {
		const healthy = await callLoad(adminOverview, signedIn());
		expect(healthy).toMatchObject({ status: { status: 'Healthy' }, error: null });

		adminApi.getServerStatus.mockResolvedValue(failed('The server is unreachable.'));
		const broken = await callLoad(adminStatus, signedIn());

		expect(broken).toEqual({ status: null, error: 'The server is unreachable.' });
	});

	it('skips the status call for a role manager, who cannot read it', async () => {
		const rolesOnly = { ...staffAccess, roles: [], permissions: ['admin.roles'] };

		const result = await callLoad(adminOverview, signedIn({ parentData: { access: rolesOnly } }));

		expect(result).toMatchObject({
			status: null,
			error: null,
			can: { manageUsers: false, manageRoles: true }
		});
		expect(adminApi.getServerStatus).not.toHaveBeenCalled();
	});
});

describe('admin user list', () => {
	it('passes every filter and clamps the page', async () => {
		await callLoad(
			adminUsers,
			signedIn({ url: '/admin/users?search=mal&role=Moderator&status=Active&page=0' })
		);

		expect(adminApi.listUsers).toHaveBeenCalledWith(expect.anything(), {
			search: 'mal',
			role: 'Moderator',
			status: 'Active',
			sort: null,
			page: 1,
			pageSize: 25
		});
	});

	it('returns the fallbacks on failure', async () => {
		adminApi.listUsers.mockResolvedValue(failed('users are down'));

		const result = await callLoad(adminUsers, signedIn());

		expect(result).toMatchObject({
			users: [],
			total: 0,
			page: 1,
			pageSize: 25,
			error: 'users are down'
		});
	});
});

describe('admin user detail', () => {
	const detail = (init: Parameters<typeof makeServerEvent>[0] = {}) =>
		signedIn({ params: { id: 'acct-2' }, ...init });

	it('404s a missing account', async () => {
		adminApi.getUser.mockResolvedValue(failed('Account not found.', 404));

		expect(await thrownMessage(() => callLoad(adminUserDetail, detail()))).toBe(
			'Account not found.'
		);
	});

	it('returns the player and the role and permission catalogues', async () => {
		const result = (await callLoad(adminUserDetail, detail())) as {
			account: unknown;
			authority: { roles: string[] };
			allRoles: string[];
			allPermissions: string[];
		};

		expect(result.account).toEqual(player);
		expect(result.authority.roles).toEqual(player.roles);
		expect(result.allRoles).toContain('Admin');
		expect(result.allPermissions).toContain('content.manage');
	});
});

describe('admin user actions', () => {
	it('assigns and revokes roles and permissions', async () => {
		expect(
			await redirectTo(() =>
				action(adminUserActions.assignRole, { role: 'Moderator' }, { params: { id: 'acct-2' } })
			)
		).toBe('/admin/users/acct-2');
		expect(adminApi.assignRole).toHaveBeenCalledWith(expect.anything(), 'acct-2', 'Moderator');

		await redirectTo(() =>
			action(adminUserActions.revokeRole, { role: 'Moderator' }, { params: { id: 'acct-2' } })
		);
		expect(adminApi.revokeRole).toHaveBeenCalledWith(expect.anything(), 'acct-2', 'Moderator');

		await redirectTo(() =>
			action(
				adminUserActions.grantPermission,
				{ permission: 'players.ban' },
				{ params: { id: 'acct-2' } }
			)
		);
		expect(adminApi.grantPermission).toHaveBeenCalledWith(
			expect.anything(),
			'acct-2',
			'players.ban'
		);

		await redirectTo(() =>
			action(
				adminUserActions.revokePermission,
				{ permission: 'players.ban' },
				{ params: { id: 'acct-2' } }
			)
		);
		expect(adminApi.revokePermission).toHaveBeenCalledWith(
			expect.anything(),
			'acct-2',
			'players.ban'
		);
	});

	it('kicks with an optional reason', async () => {
		await redirectTo(() => action(adminUserActions.kick, {}, { params: { id: 'acct-2' } }));
		expect(adminApi.kickPlayer).toHaveBeenLastCalledWith(expect.anything(), 'acct-2', null);

		await redirectTo(() =>
			action(adminUserActions.kick, { reason: 'spam' }, { params: { id: 'acct-2' } })
		);
		expect(adminApi.kickPlayer).toHaveBeenLastCalledWith(expect.anything(), 'acct-2', 'spam');
	});

	it('mutes for a duration and unmutes', async () => {
		await redirectTo(() =>
			action(
				adminUserActions.mute,
				{ reason: 'flooding', durationMinutes: '15' },
				{ params: { id: 'acct-2' } }
			)
		);
		expect(adminApi.mutePlayer).toHaveBeenCalledWith(expect.anything(), 'acct-2', 'flooding', 15);

		await redirectTo(() => action(adminUserActions.unmute, {}, { params: { id: 'acct-2' } }));
		expect(adminApi.unmutePlayer).toHaveBeenCalledWith(expect.anything(), 'acct-2');
	});

	it('bans only with a reason and unbans', async () => {
		const noReason = (await action(adminUserActions.ban, {}, { params: { id: 'acct-2' } })) as {
			status: number;
			data: { error: string };
		};

		expect(noReason.status).toBe(400);
		expect(noReason.data.error).toBe('A ban reason is required.');
		expect(adminApi.banPlayer).not.toHaveBeenCalled();

		await redirectTo(() =>
			action(
				adminUserActions.ban,
				{ reason: 'harassment', durationMinutes: '1440' },
				{ params: { id: 'acct-2' } }
			)
		);
		expect(adminApi.banPlayer).toHaveBeenCalledWith(
			expect.anything(),
			'acct-2',
			'harassment',
			1440
		);

		await redirectTo(() => action(adminUserActions.unban, {}, { params: { id: 'acct-2' } }));
		expect(adminApi.unbanPlayer).toHaveBeenCalledWith(expect.anything(), 'acct-2');
	});

	it('refuses a warning with no reason', async () => {
		const result = (await action(
			adminUserActions.warn,
			{ reason: '  ' },
			{ params: { id: 'acct-2' } }
		)) as { status: number; data: { error: string } };

		expect(result.status).toBe(400);
		expect(result.data.error).toBe('A reason is required.');
		expect(adminApi.warnPlayer).not.toHaveBeenCalled();
	});

	it('turns an upstream rejection into a 400 with its message', async () => {
		adminApi.assignRole.mockResolvedValue(failed('That role does not exist.', 400));

		const result = (await action(
			adminUserActions.assignRole,
			{ role: 'Ghost' },
			{ params: { id: 'acct-2' } }
		)) as { status: number; data: { error: string } };

		expect(result.status).toBe(400);
		expect(result.data.error).toBe('That role does not exist.');
	});
});

describe('admin audit, reports, roles, sessions, broadcast', () => {
	it('reads the audit log with a page clamp and an optional target', async () => {
		const result = (await callLoad(
			adminAudit,
			signedIn({ url: '/admin/audit?page=0&targetId=acct-2' })
		)) as { page: number; targetId: string; entries: unknown[] };

		expect(adminApi.getAuditLog).toHaveBeenCalledWith(expect.anything(), 1, 50, 'acct-2');
		expect(result).toMatchObject({ page: 1, targetId: 'acct-2' });
		expect(result.entries).toHaveLength(1);
	});

	it('maps the resolved filter and resolves a report', async () => {
		await callLoad(adminReports, signedIn({ url: '/admin/reports?resolved=false&page=2' }));
		expect(adminApi.listReports).toHaveBeenLastCalledWith(expect.anything(), false, 2, 50);

		expect(await redirectTo(() => action(adminReportActions.resolve, { id: 'report-1' }))).toBe(
			'/admin/reports'
		);
		expect(adminApi.resolveReport).toHaveBeenCalledWith(expect.anything(), 'report-1');
	});

	it('publishes the static role catalogue', async () => {
		const result = (await callLoad(adminRoles, signedIn())) as {
			roles: { name: string; permissions: string[] }[];
			allPermissions: string[];
		};

		const names = result.roles.map((role) => role.name);
		expect(names).toEqual([
			'Admin',
			'Moderator',
			'Curator',
			'GameMaster',
			'Developer',
			'ContentEditor'
		]);
		// The Admin row must grant everything, or the catalogue lies about the role.
		expect(result.roles[0].permissions).toEqual(result.allPermissions);
	});

	it('lists sessions with the optional region and zone', async () => {
		const result = (await callLoad(
			adminSessions,
			signedIn({ url: '/admin/sessions?regionId=r1&zoneId=z1' })
		)) as { sessions: unknown[]; regionId: string; zoneId: string };

		expect(adminApi.listSessions).toHaveBeenCalledWith(expect.anything(), 'r1', 'z1');
		expect(result).toMatchObject({ regionId: 'r1', zoneId: 'z1' });
		expect(result.sessions).toHaveLength(1);
	});

	it('broadcasts an announcement, refusing an empty message', async () => {
		const empty = (await action(adminServerActions.broadcast, { message: '   ' })) as {
			status: number;
			data: { error: string };
		};
		expect(empty.status).toBe(400);
		expect(empty.data.error).toBe('An announcement message is required.');
		expect(adminApi.broadcastAnnouncement).not.toHaveBeenCalled();

		const result = (await action(adminServerActions.broadcast, {
			message: 'Server restarting',
			severity: 'warning'
		})) as { ok: boolean; message: string };

		expect(result.ok).toBe(true);
		expect(adminApi.broadcastAnnouncement).toHaveBeenCalledWith(
			expect.anything(),
			'Server restarting',
			'warning'
		);
	});
});

describe('editorial overview and lists', () => {
	it('summarises the three content counts and collects the errors', async () => {
		const result = (await callLoad(editorialOverview, signedIn())) as {
			counts: Record<string, number>;
			errors: string[];
		};

		expect(result.counts).toEqual({ lore: 5, patchNotes: 3, announcements: 2 });
		expect(result.errors).toEqual([]);

		editorialApi.adminListLoreArticles.mockResolvedValue(failed('lore is down'));
		const broken = (await callLoad(editorialOverview, signedIn())) as { errors: string[] };
		expect(broken.errors).toEqual(['lore is down']);
	});

	it('clamps the page on all three lists', async () => {
		await callLoad(loreList, signedIn({ url: '/editorial/lore?page=0' }));
		expect(editorialApi.adminListLoreArticles).toHaveBeenLastCalledWith(expect.anything(), 1, 20);

		await callLoad(noteList, signedIn({ url: '/editorial/patch-notes?page=3' }));
		expect(editorialApi.adminListPatchNotes).toHaveBeenLastCalledWith(expect.anything(), 3, 20);

		await callLoad(announcementList, signedIn());
		expect(editorialApi.adminListAnnouncements).toHaveBeenLastCalledWith(expect.anything(), 1, 20);
	});
});

describe('editorial lore editor', () => {
	const detail = (init: Parameters<typeof makeServerEvent>[0] = {}) =>
		signedIn({ params: { id: 'lore-1' }, ...init });

	const article = {
		title: 'The Fall of Caleth',
		summary: 'How the first age ended.',
		content: 'Long ago…',
		category: 'history'
	};

	it('opens an empty form for the new route and 404s a missing article', async () => {
		expect(await callLoad(loreDetail, signedIn({ params: { id: 'new' } }))).toEqual({
			mode: 'create',
			article: null
		});

		editorialApi.adminGetLoreArticle.mockResolvedValue(failed('Lore article not found.', 404));
		expect(await thrownMessage(() => callLoad(loreDetail, detail()))).toBe(
			'Lore article not found.'
		);
	});

	it('requires the mandatory fields before writing', async () => {
		const result = (await action(
			loreActions.save,
			{ title: 'x' },
			{ params: { id: 'lore-1' } }
		)) as {
			status: number;
			data: { error: string };
		};

		expect(result.status).toBe(400);
		expect(result.data.error).toBe('Title, summary, and content are required.');
		expect(editorialApi.adminUpdateLoreArticle).not.toHaveBeenCalled();
	});

	it('creates an article and redirects to it', async () => {
		editorialApi.adminCreateLoreArticle.mockResolvedValue(ok({ id: 'lore-9' }));

		expect(
			await redirectTo(() =>
				action(loreActions.save, { ...article, slug: 'the-fall' }, { params: { id: 'new' } })
			)
		).toBe('/editorial/lore/lore-9');
		expect(editorialApi.adminCreateLoreArticle).toHaveBeenCalledWith(
			expect.anything(),
			expect.objectContaining({ slug: 'the-fall', title: article.title })
		);
	});

	it('refuses a new article with no slug', async () => {
		const result = (await action(loreActions.save, article, { params: { id: 'new' } })) as {
			status: number;
			data: { error: string };
		};

		expect(result.data.error).toBe('Slug is required.');
		expect(editorialApi.adminCreateLoreArticle).not.toHaveBeenCalled();
	});

	it('saves an edit and reports the upstream rejection', async () => {
		editorialApi.adminUpdateLoreArticle.mockResolvedValue(ok({ id: 'lore-1' }));
		const saved = (await action(loreActions.save, article, { params: { id: 'lore-1' } })) as {
			ok: boolean;
			message: string;
		};

		expect(saved).toEqual({ ok: true, message: 'Article saved.' });
		expect(editorialApi.adminUpdateLoreArticle).toHaveBeenCalledWith(
			expect.anything(),
			'lore-1',
			expect.objectContaining({ status: 'Draft' })
		);

		editorialApi.adminUpdateLoreArticle.mockResolvedValue(failed('Duplicate slug.'));
		const rejected = (await action(loreActions.save, article, { params: { id: 'lore-1' } })) as {
			status: number;
			data: { error: string };
		};
		expect(rejected.status).toBe(400);
		expect(rejected.data.error).toBe('Duplicate slug.');
	});

	it('publishes and deletes', async () => {
		expect(
			await redirectTo(() => action(loreActions.publish, {}, { params: { id: 'lore-1' } }))
		).toBe('/editorial/lore/lore-1');
		expect(editorialApi.adminPublishLoreArticle).toHaveBeenCalledWith(expect.anything(), 'lore-1');

		expect(
			await redirectTo(() => action(loreActions.delete, {}, { params: { id: 'lore-1' } }))
		).toBe('/editorial/lore');
		expect(editorialApi.adminDeleteLoreArticle).toHaveBeenCalledWith(expect.anything(), 'lore-1');
	});
});

describe('editorial patch notes editor', () => {
	const note = {
		title: '1.2.0',
		summary: 'Balance pass.',
		content: '- Something',
		version: '1.2.0'
	};

	it('opens the create form and 404s a missing note', async () => {
		expect(await callLoad(noteDetail, signedIn({ params: { id: 'new' } }))).toEqual({
			mode: 'create',
			note: null
		});

		editorialApi.adminGetPatchNote.mockResolvedValue(failed('Patch note not found.', 404));
		expect(
			await thrownMessage(() => callLoad(noteDetail, signedIn({ params: { id: 'note-1' } })))
		).toBe('Patch note not found.');
	});

	it('requires a version as well as the shared fields', async () => {
		const result = (await action(
			noteActions.save,
			{ ...note, version: '' },
			{ params: { id: 'n' } }
		)) as {
			status: number;
			data: { error: string };
		};

		expect(result.data.error).toBe('Title, summary, version, and content are required.');
	});

	it('creates, saves, publishes and deletes', async () => {
		editorialApi.adminCreatePatchNote.mockResolvedValue(ok({ id: 'note-9' }));
		expect(
			await redirectTo(() =>
				action(noteActions.save, { ...note, slug: '1-2-0' }, { params: { id: 'new' } })
			)
		).toBe('/editorial/patch-notes/note-9');

		editorialApi.adminUpdatePatchNote.mockResolvedValue(ok({ id: 'note-1' }));
		expect(await action(noteActions.save, note, { params: { id: 'note-1' } })).toEqual({
			ok: true,
			message: 'Patch note saved.'
		});

		expect(
			await redirectTo(() => action(noteActions.publish, {}, { params: { id: 'note-1' } }))
		).toBe('/editorial/patch-notes/note-1');
		expect(
			await redirectTo(() => action(noteActions.delete, {}, { params: { id: 'note-1' } }))
		).toBe('/editorial/patch-notes');
	});
});

describe('editorial announcements editor', () => {
	const announcement = { title: 'Server event', body: 'Join us at eight.' };

	it('opens the create form and 404s a missing announcement', async () => {
		expect(await callLoad(announcementDetail, signedIn({ params: { id: 'new' } }))).toEqual({
			mode: 'create',
			announcement: null
		});

		editorialApi.adminGetAnnouncement.mockResolvedValue(failed('Announcement not found.', 404));
		expect(
			await thrownMessage(() => callLoad(announcementDetail, signedIn({ params: { id: 'ann-1' } })))
		).toBe('Announcement not found.');
	});

	it('requires a title and a body', async () => {
		const result = (await action(
			announcementActions.save,
			{ title: 'x' },
			{ params: { id: 'a' } }
		)) as {
			status: number;
			data: { error: string };
		};

		expect(result.data.error).toBe('Title and body are required.');
	});

	it('creates, saves, publishes and deletes', async () => {
		editorialApi.adminCreateAnnouncement.mockResolvedValue(ok({ id: 'ann-9' }));
		expect(
			await redirectTo(() =>
				action(announcementActions.save, announcement, { params: { id: 'new' } })
			)
		).toBe('/editorial/announcements/ann-9');

		editorialApi.adminUpdateAnnouncement.mockResolvedValue(ok({ id: 'ann-1' }));
		expect(
			await action(announcementActions.save, announcement, { params: { id: 'ann-1' } })
		).toEqual({ ok: true, message: 'Announcement saved.' });

		expect(
			await redirectTo(() => action(announcementActions.publish, {}, { params: { id: 'ann-1' } }))
		).toBe('/editorial/announcements/ann-1');
		expect(
			await redirectTo(() => action(announcementActions.delete, {}, { params: { id: 'ann-1' } }))
		).toBe('/editorial/announcements');
	});

	it('turns a rejected create into a 400 with the upstream message', async () => {
		editorialApi.adminCreateAnnouncement.mockResolvedValue(failed('Title already used.'));

		const result = (await action(announcementActions.save, announcement, {
			params: { id: 'new' }
		})) as { status: number; data: { error: string } };

		expect(result.status).toBe(400);
		expect(result.data.error).toBe('Title already used.');
	});
});
