import { beforeEach, describe, expect, it, vi } from 'vitest';
import { callLoad, fakeUser, makeServerEvent, type FakeServerEvent } from '#lib/testing/event';
import { load as auditLoad } from './(admin)/admin/audit/+page.server';
import {
	actions as reportActions,
	load as reportsLoad
} from './(admin)/admin/reports/+page.server';
import { actions as serverActions } from './(admin)/admin/server/+page.server';
import { load as sessionsLoad } from './(admin)/admin/sessions/+page.server';
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
import {
	actions as modUserActions,
	load as modUserDetail
} from './(mod)/mod/users/[id]/+page.server';

/**
 * The rejected-write half of the admin and editorial server modules.
 *
 * The happy paths, the field validation, and the 404s are pinned in `staff-admin-editorial.test.ts`.
 * What is left is the boring but load-bearing half of every action: an upstream rejection must
 * become a `fail(400)` carrying the API's message instead of a 500, and every list page must fall
 * back to empty data (and the clamped page) when the API returns nothing. Those paths only run when
 * Veldrath.Server is unhappy, which is exactly when a silent 500 is hardest to diagnose.
 */

const adminApi = vi.hoisted(() => ({
	getUser: vi.fn(),
	getModUser: vi.fn(),
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

const ok = <T>(data: T) => ({ status: 200, data, error: null });
const failed = (error: string | null, status = 500) => ({ status, data: null, error });

const signedIn = (init: Parameters<typeof makeServerEvent>[0] = {}) =>
	makeServerEvent({ user: fakeUser(), ...init });

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

const userActions = [
	['assignRole', adminApi.assignRole, { role: 'Moderator' }],
	['revokeRole', adminApi.revokeRole, { role: 'Moderator' }],
	['grantPermission', adminApi.grantPermission, { permission: 'players.ban' }],
	['revokePermission', adminApi.revokePermission, { permission: 'players.ban' }],
	['kick', adminApi.kickPlayer, { reason: 'spam' }],
	['warn', adminApi.warnPlayer, { reason: 'disruptive' }],
	['mute', adminApi.mutePlayer, { reason: 'flooding', durationMinutes: '15' }],
	['unmute', adminApi.unmutePlayer, {}],
	['ban', adminApi.banPlayer, { reason: 'harassment', durationMinutes: '60' }],
	['unban', adminApi.unbanPlayer, {}]
] as const;

beforeEach(() => {
	vi.resetAllMocks();
	adminApi.getUser.mockResolvedValue(ok({ accountId: 'acct-2' }));
	adminApi.getModUser.mockResolvedValue(ok({ accountId: 'acct-2' }));
	adminApi.listReports.mockResolvedValue(ok(null));
	adminApi.getAuditLog.mockResolvedValue(ok(null));
	adminApi.listSessions.mockResolvedValue(ok([]));
	adminApi.resolveReport.mockResolvedValue(ok(null));
	adminApi.broadcastAnnouncement.mockResolvedValue(ok(null));
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

describe('admin user actions', () => {
	it.each(userActions)(
		'turns a rejected %s into a 400 with the API message',
		async (name, spy, form) => {
			spy.mockResolvedValue(failed('The player is offline.'));

			const result = (await action(adminUserActions[name], form, {
				params: { id: 'acct-2' }
			})) as { status: number; data: { error: string } };

			expect(result.status).toBe(400);
			expect(result.data.error).toBe('The player is offline.');
		}
	);

	it('warns a player and returns to the detail page', async () => {
		expect(
			await redirectTo(() =>
				action(adminUserActions.warn, { reason: 'disruptive' }, { params: { id: 'acct-2' } })
			)
		).toBe('/admin/users/acct-2');
		expect(adminApi.warnPlayer).toHaveBeenCalledWith(expect.anything(), 'acct-2', 'disruptive');
	});

	it('mutes without a reason, passing null', async () => {
		expect(
			await redirectTo(() =>
				action(adminUserActions.mute, { durationMinutes: '15' }, { params: { id: 'acct-2' } })
			)
		).toBe('/admin/users/acct-2');
		expect(adminApi.mutePlayer).toHaveBeenCalledWith(expect.anything(), 'acct-2', null, 15);
	});

	it('falls back to the default account 404 message when the API gives none', async () => {
		adminApi.getUser.mockResolvedValue(failed(null, 404));

		await expect(
			callLoad(adminUserDetail, signedIn({ params: { id: 'acct-2' } }))
		).rejects.toMatchObject({ body: { message: 'Account not found.' } });
	});
});

describe('admin list fallbacks', () => {
	it('asks for resolved reports explicitly, including the true filter', async () => {
		await callLoad(reportsLoad, signedIn({ url: '/admin/reports?resolved=true' }));

		expect(adminApi.listReports).toHaveBeenCalledWith(expect.anything(), true, 1, 50);
	});

	it('defaults the reports page and data when the API returns nothing', async () => {
		adminApi.listReports.mockResolvedValue(failed('reports are down'));

		const result = (await callLoad(reportsLoad, signedIn())) as Record<string, unknown>;

		expect(result).toEqual({
			reports: [],
			total: 0,
			page: 1,
			pageSize: 50,
			resolved: '',
			error: 'reports are down'
		});
		expect(adminApi.listReports).toHaveBeenCalledWith(expect.anything(), null, 1, 50);
	});

	it('turns a rejected report resolution into a 400', async () => {
		adminApi.resolveReport.mockResolvedValue(failed('Already resolved.'));

		const result = (await action(reportActions.resolve, { id: 'report-1' })) as {
			status: number;
			data: { error: string };
		};

		expect(result.status).toBe(400);
		expect(result.data.error).toBe('Already resolved.');
	});

	it('defaults the audit page, target, and entries', async () => {
		adminApi.getAuditLog.mockResolvedValue(failed('the log is down'));

		const result = (await callLoad(auditLoad, signedIn())) as Record<string, unknown>;

		expect(result).toEqual({
			entries: [],
			total: 0,
			page: 1,
			pageSize: 50,
			targetId: '',
			error: 'the log is down'
		});
		expect(adminApi.getAuditLog).toHaveBeenCalledWith(expect.anything(), 1, 50, null);
	});

	it('defaults the session filters when the API fails', async () => {
		adminApi.listSessions.mockResolvedValue(failed('sessions are down'));

		const result = (await callLoad(sessionsLoad, signedIn())) as Record<string, unknown>;

		expect(result).toEqual({
			sessions: [],
			regionId: '',
			zoneId: '',
			error: 'sessions are down'
		});
	});

	it('turns a rejected broadcast into a 400', async () => {
		adminApi.broadcastAnnouncement.mockResolvedValue(failed('No clients are connected.'));

		const result = (await action(serverActions.broadcast, { message: 'Restarting' })) as {
			status: number;
			data: { error: string };
		};

		expect(result.status).toBe(400);
		expect(result.data.error).toBe('No clients are connected.');
	});
});

describe('moderation user detail', () => {
	const detail = (init: Parameters<typeof makeServerEvent>[0] = {}) =>
		signedIn({ params: { id: 'acct-2' }, ...init });

	it('falls back to the default 404 message when the API gives none', async () => {
		adminApi.getModUser.mockResolvedValue(failed(null, 404));

		await expect(callLoad(modUserDetail, detail())).rejects.toMatchObject({
			body: { message: 'Account not found.' }
		});
	});

	it('turns a rejected warning into a 400', async () => {
		adminApi.warnPlayer.mockResolvedValue(failed('The player is offline.'));

		const result = (await action(
			modUserActions.warn,
			{ reason: 'disruptive' },
			{ params: { id: 'acct-2' } }
		)) as { status: number; data: { error: string } };

		expect(result.status).toBe(400);
		expect(result.data.error).toBe('The player is offline.');
	});

	it('mutes without a reason and turns a rejected mute into a 400', async () => {
		expect(
			await redirectTo(() =>
				action(modUserActions.mute, { durationMinutes: '30' }, { params: { id: 'acct-2' } })
			)
		).toBe('/mod/users/acct-2');
		expect(adminApi.mutePlayer).toHaveBeenCalledWith(expect.anything(), 'acct-2', null, 30);

		adminApi.mutePlayer.mockResolvedValue(failed('Already muted.'));

		const result = (await action(
			modUserActions.mute,
			{ reason: 'flooding', durationMinutes: '30' },
			{ params: { id: 'acct-2' } }
		)) as { status: number; data: { error: string } };

		expect(result.status).toBe(400);
		expect(result.data.error).toBe('Already muted.');
	});
});

describe('editorial list fallbacks', () => {
	it('defaults the lore list page and data', async () => {
		editorialApi.adminListLoreArticles.mockResolvedValue(failed('lore is down'));

		const result = (await callLoad(loreList, signedIn())) as Record<string, unknown>;

		expect(result).toEqual({
			articles: [],
			total: 0,
			page: 1,
			pageSize: 20,
			error: 'lore is down'
		});
		expect(editorialApi.adminListLoreArticles).toHaveBeenCalledWith(expect.anything(), 1, 20);
	});

	it('defaults the patch-note list page and data', async () => {
		editorialApi.adminListPatchNotes.mockResolvedValue(failed('patch notes are down'));

		const result = (await callLoad(noteList, signedIn())) as Record<string, unknown>;

		expect(result).toEqual({
			notes: [],
			total: 0,
			page: 1,
			pageSize: 20,
			error: 'patch notes are down'
		});
		expect(editorialApi.adminListPatchNotes).toHaveBeenCalledWith(expect.anything(), 1, 20);
	});

	it('defaults the announcement list data', async () => {
		editorialApi.adminListAnnouncements.mockResolvedValue(failed('announcements are down'));

		const result = (await callLoad(announcementList, signedIn())) as Record<string, unknown>;

		expect(result).toEqual({
			announcements: [],
			total: 0,
			page: 1,
			pageSize: 20,
			error: 'announcements are down'
		});
	});

	it('counts a failed list as zero and keeps the errors', async () => {
		editorialApi.adminListLoreArticles.mockResolvedValue(ok({ items: [], totalCount: 5 }));
		editorialApi.adminListPatchNotes.mockResolvedValue(failed('patch notes are down'));
		editorialApi.adminListAnnouncements.mockResolvedValue(failed('announcements are down'));

		const result = (await callLoad(editorialOverview, signedIn())) as {
			counts: Record<string, number>;
			errors: string[];
		};

		expect(result.counts).toEqual({ lore: 5, patchNotes: 0, announcements: 0 });
		expect(result.errors).toEqual(['patch notes are down', 'announcements are down']);
	});
});

describe('editorial detail editors', () => {
	it('opens lore, patch notes, and announcements in edit mode', async () => {
		editorialApi.adminGetLoreArticle.mockResolvedValue(ok({ id: 'lore-1' }));
		editorialApi.adminGetPatchNote.mockResolvedValue(ok({ id: 'note-1' }));
		editorialApi.adminGetAnnouncement.mockResolvedValue(ok({ id: 'ann-1' }));

		expect(await callLoad(loreDetail, signedIn({ params: { id: 'lore-1' } }))).toEqual({
			mode: 'edit',
			article: { id: 'lore-1' }
		});
		expect(await callLoad(noteDetail, signedIn({ params: { id: 'note-1' } }))).toEqual({
			mode: 'edit',
			note: { id: 'note-1' }
		});
		expect(await callLoad(announcementDetail, signedIn({ params: { id: 'ann-1' } }))).toEqual({
			mode: 'edit',
			announcement: { id: 'ann-1' }
		});
	});

	it('falls back to the default 404 message when the API gives none', async () => {
		editorialApi.adminGetLoreArticle.mockResolvedValue(failed(null, 404));
		editorialApi.adminGetPatchNote.mockResolvedValue(failed(null, 404));
		editorialApi.adminGetAnnouncement.mockResolvedValue(failed(null, 404));

		const detailWith = (load: unknown) =>
			callLoad(load as (event: never) => unknown, signedIn({ params: { id: 'missing' } }));

		await expect(detailWith(loreDetail)).rejects.toMatchObject({
			body: { message: 'Lore article not found.' }
		});
		await expect(detailWith(noteDetail)).rejects.toMatchObject({
			body: { message: 'Patch note not found.' }
		});
		await expect(detailWith(announcementDetail)).rejects.toMatchObject({
			body: { message: 'Announcement not found.' }
		});
	});

	it('defaults the create rejection message when the API gives none', async () => {
		editorialApi.adminCreateLoreArticle.mockResolvedValue(failed(null));
		editorialApi.adminCreatePatchNote.mockResolvedValue(failed(null));
		editorialApi.adminCreateAnnouncement.mockResolvedValue(failed(null));

		const article = { title: 'The Fall', summary: 'How it ended.', content: 'Long ago.' };
		const note = { ...article, version: '1.2.0' };
		const announcement = { title: 'Server event', body: 'Join us at eight.' };

		const lore = (await action(
			loreActions.save,
			{ ...article, slug: 'the-fall' },
			{ params: { id: 'new' } }
		)) as { status: number; data: { error: string } };
		const patchNote = (await action(
			noteActions.save,
			{ ...note, slug: '1-2-0' },
			{ params: { id: 'new' } }
		)) as { status: number; data: { error: string } };
		const news = (await action(announcementActions.save, announcement, {
			params: { id: 'new' }
		})) as { status: number; data: { error: string } };

		expect(lore).toMatchObject({ status: 400, data: { error: 'Could not create the article.' } });
		expect(patchNote).toMatchObject({
			status: 400,
			data: { error: 'Could not create the patch note.' }
		});
		expect(news).toMatchObject({
			status: 400,
			data: { error: 'Could not create the announcement.' }
		});
	});

	it('turns rejected updates into 400s', async () => {
		editorialApi.adminUpdateLoreArticle.mockResolvedValue(failed('Duplicate slug.'));
		editorialApi.adminUpdatePatchNote.mockResolvedValue(failed('Duplicate version.'));
		editorialApi.adminUpdateAnnouncement.mockResolvedValue(failed('Duplicate title.'));

		const lore = (await action(
			loreActions.save,
			{ title: 't', summary: 's', content: 'c' },
			{ params: { id: 'lore-1' } }
		)) as { status: number; data: { error: string } };
		const patchNote = (await action(
			noteActions.save,
			{ title: 't', summary: 's', content: 'c', version: '1.0.0' },
			{ params: { id: 'note-1' } }
		)) as { status: number; data: { error: string } };
		const news = (await action(
			announcementActions.save,
			{ title: 't', body: 'b' },
			{ params: { id: 'ann-1' } }
		)) as { status: number; data: { error: string } };

		expect(lore).toMatchObject({ status: 400, data: { error: 'Duplicate slug.' } });
		expect(patchNote).toMatchObject({ status: 400, data: { error: 'Duplicate version.' } });
		expect(news).toMatchObject({ status: 400, data: { error: 'Duplicate title.' } });
	});

	it('refuses a new patch note with no slug', async () => {
		const result = (await action(
			noteActions.save,
			{ title: '1.2.0', summary: 'Balance pass.', content: '- Something', version: '1.2.0' },
			{ params: { id: 'new' } }
		)) as { status: number; data: { error: string } };

		expect(result.status).toBe(400);
		expect(result.data.error).toBe('Slug is required.');
		expect(editorialApi.adminCreatePatchNote).not.toHaveBeenCalled();
	});

	it('turns rejected publishes and deletes into 400s', async () => {
		editorialApi.adminPublishLoreArticle.mockResolvedValue(failed('Lore is invalid.'));
		editorialApi.adminDeleteLoreArticle.mockResolvedValue(failed('Lore is protected.'));
		editorialApi.adminPublishPatchNote.mockResolvedValue(failed('Note is invalid.'));
		editorialApi.adminDeletePatchNote.mockResolvedValue(failed('Note is protected.'));
		editorialApi.adminPublishAnnouncement.mockResolvedValue(failed('Announcement is invalid.'));
		editorialApi.adminDeleteAnnouncement.mockResolvedValue(failed('Announcement is protected.'));

		await expect(
			action(loreActions.publish, {}, { params: { id: 'lore-1' } })
		).resolves.toMatchObject({ status: 400, data: { error: 'Lore is invalid.' } });
		await expect(
			action(loreActions.delete, {}, { params: { id: 'lore-1' } })
		).resolves.toMatchObject({ status: 400, data: { error: 'Lore is protected.' } });
		await expect(
			action(noteActions.publish, {}, { params: { id: 'note-1' } })
		).resolves.toMatchObject({ status: 400, data: { error: 'Note is invalid.' } });
		await expect(
			action(noteActions.delete, {}, { params: { id: 'note-1' } })
		).resolves.toMatchObject({ status: 400, data: { error: 'Note is protected.' } });
		await expect(
			action(announcementActions.publish, {}, { params: { id: 'ann-1' } })
		).resolves.toMatchObject({ status: 400, data: { error: 'Announcement is invalid.' } });
		await expect(
			action(announcementActions.delete, {}, { params: { id: 'ann-1' } })
		).resolves.toMatchObject({ status: 400, data: { error: 'Announcement is protected.' } });
	});
});
