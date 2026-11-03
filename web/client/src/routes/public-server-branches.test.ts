import { beforeEach, describe, expect, it, vi } from 'vitest';
import { callLoad, fakeUser, makeServerEvent, type FakeServerEvent } from '#lib/testing/event';
import { load as rootLayout } from './+layout.server';
import { load as accountLoad } from './account/+page.server';
import { load as languagesLoad } from './(foundry)/languages/+page.server';
import { actions as newLanguageActions } from './(foundry)/languages/new/+page.server';
import {
	actions as submissionActions,
	load as submissionLoad
} from './(foundry)/submissions/[id]/+page.server';
import { load as hardcoreLoad } from './leaderboard/hardcore/+page.server';
import { load as patchNoteLoad } from './patch-notes/[slug]/+page.server';

/**
 * The non-staff server modules: the root layout, the account page, the Foundry language pages, the
 * hardcore board, and the public patch-note article.
 *
 * Each case pins a fallback that only runs when something is missing: an anonymous visitor on the
 * root layout, a Better Auth session row without an IP or user agent, a list endpoint with no page
 * parameter, a submission viewed by a player with no RBAC snapshot, and an upstream failure behind
 * a public page.
 */

const access = vi.hoisted(() => ({ loadAccountAccess: vi.fn() }));
const contentApi = vi.hoisted(() => ({ browseContent: vi.fn() }));
const foundryApi = vi.hoisted(() => ({
	createSubmission: vi.fn(),
	voteSubmission: vi.fn(),
	getSubmission: vi.fn()
}));
const characterApi = vi.hoisted(() => ({ fetchHardcoreLeaderboard: vi.fn() }));
const editorialApi = vi.hoisted(() => ({ getPatchNote: vi.fn() }));
const auth = vi.hoisted(() => ({ getAuth: vi.fn() }));
const account = vi.hoisted(() => ({ listLinkedProviders: vi.fn() }));

vi.mock('#lib/server/access', async (importOriginal) => ({
	...(await importOriginal<typeof import('#lib/server/access')>()),
	loadAccountAccess: access.loadAccountAccess
}));
vi.mock('#lib/server/content-api', () => contentApi);
vi.mock('#lib/server/foundry-api', () => foundryApi);
vi.mock('#lib/server/character-api', () => characterApi);
vi.mock('#lib/server/editorial-api', () => editorialApi);
vi.mock('#lib/server/auth', () => auth);
vi.mock('#lib/server/account', () => account);

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

beforeEach(() => {
	vi.resetAllMocks();
	access.loadAccountAccess.mockResolvedValue(null);
	contentApi.browseContent.mockResolvedValue(ok(null));
	foundryApi.createSubmission.mockResolvedValue(ok(null));
	foundryApi.voteSubmission.mockResolvedValue(ok(null));
	foundryApi.getSubmission.mockResolvedValue(ok(null));
	characterApi.fetchHardcoreLeaderboard.mockResolvedValue(ok(null));
	editorialApi.getPatchNote.mockResolvedValue({ note: null, error: null });
	auth.getAuth.mockReturnValue({ api: { listSessions: vi.fn(async () => []) } });
	account.listLinkedProviders.mockResolvedValue([]);
});

describe('root layout', () => {
	it('renders an anonymous visitor with no access and no staff flags', async () => {
		const result = (await callLoad(rootLayout, makeServerEvent())) as {
			user: unknown;
			access: unknown;
			staff: Record<string, boolean>;
		};

		expect(result.user).toBeNull();
		expect(result.access).toBeNull();
		expect(result.staff).toEqual({ foundry: false, admin: false, mod: false, editorial: false });
		expect(access.loadAccountAccess).not.toHaveBeenCalled();
	});

	it('loads the access snapshot for a signed-in user', async () => {
		access.loadAccountAccess.mockResolvedValue({
			accountId: 'acct-1',
			username: 'kaleth',
			roles: ['Admin'],
			permissions: []
		});
		const user = fakeUser();

		const result = (await callLoad(rootLayout, makeServerEvent({ user }))) as {
			user: unknown;
			access: unknown;
			staff: Record<string, boolean>;
		};

		expect(result.user).toEqual(user);
		expect(result.access).toMatchObject({ accountId: 'acct-1' });
		expect(result.staff).toEqual({ foundry: true, admin: true, mod: true, editorial: true });
		expect(access.loadAccountAccess).toHaveBeenCalledWith(user);
	});
});

describe('account sessions', () => {
	it('maps a session row missing its address and agent to nulls', async () => {
		auth.getAuth.mockReturnValue({
			api: {
				listSessions: vi.fn(async () => [
					{
						id: 'sess-1',
						token: 'token-1',
						createdAt: new Date('2026-01-01T00:00:00.000Z'),
						expiresAt: new Date('2026-01-08T00:00:00.000Z')
					}
				])
			}
		});

		const result = (await callLoad(accountLoad, signedIn())) as {
			sessions: { ipAddress: string | null; userAgent: string | null }[];
		};

		expect(result.sessions).toEqual([
			{
				id: 'sess-1',
				token: 'token-1',
				createdAt: '2026-01-01T00:00:00.000Z',
				expiresAt: '2026-01-08T00:00:00.000Z',
				ipAddress: null,
				userAgent: null
			}
		]);
	});
});

describe('foundry language pages', () => {
	it('defaults the language browser search, page, and data', async () => {
		contentApi.browseContent.mockResolvedValue(failed('content is down'));

		const result = (await callLoad(
			languagesLoad,
			makeServerEvent({ url: '/languages' })
		)) as Record<string, unknown>;

		expect(result).toEqual({
			languages: [],
			total: 0,
			page: 1,
			pageSize: 24,
			search: '',
			error: 'content is down'
		});
		expect(contentApi.browseContent).toHaveBeenCalledWith('language', null, 1, 24);
	});

	it('defaults the rejected language submission message', async () => {
		foundryApi.createSubmission.mockResolvedValue(failed(null));

		const result = (await action(newLanguageActions.default, {
			displayName: 'Calethic',
			slug: 'calethic'
		})) as { status: number; data: { error: string } };

		expect(result.status).toBe(400);
		expect(result.data.error).toBe('Could not submit the language.');
	});
});

describe('foundry submission detail', () => {
	/** A signed-in event whose `parent()` hands back the given RBAC snapshot. */
	function withAccess(snapshot: unknown): FakeServerEvent {
		const event = signedIn({ params: { id: 'new' } });
		event.parent = (async () => ({ access: snapshot })) as never;
		return event;
	}

	it('treats a missing access snapshot as no review rights', async () => {
		const result = (await callLoad(submissionLoad, withAccess(null))) as { canReview: boolean };

		expect(result.canReview).toBe(false);
	});

	it('turns a rejected vote into a 400', async () => {
		foundryApi.voteSubmission.mockResolvedValue(failed('Voting is closed.'));

		const result = (await action(
			submissionActions.vote,
			{ value: '1' },
			{ params: { id: 'sub-1' } }
		)) as { status: number; data: { error: string } };

		expect(result.status).toBe(400);
		expect(result.data.error).toBe('Voting is closed.');
	});
});

describe('hardcore board', () => {
	it('defaults the page when the query string omits it', async () => {
		await callLoad(hardcoreLoad, signedIn({ url: '/leaderboard/hardcore' }));

		expect(characterApi.fetchHardcoreLeaderboard).toHaveBeenCalledWith(expect.anything(), 1);
	});
});

describe('public patch note article', () => {
	it('maps an upstream failure to a 502 instead of a 404', async () => {
		editorialApi.getPatchNote.mockResolvedValue({ note: null, error: 'upstream down' });

		await expect(
			callLoad(patchNoteLoad, makeServerEvent({ params: { slug: 'v9' } }))
		).rejects.toMatchObject({ status: 502, body: { message: 'upstream down' } });
	});
});
