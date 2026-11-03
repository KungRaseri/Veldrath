import { beforeEach, describe, expect, it, vi } from 'vitest';
import { callLoad, fakeUser, makeServerEvent } from '#lib/testing/event';
import { load as homeLoad } from './+page.server';
import { load as communityLoad } from './community/+page.server';
import { load as hardcoreLoad } from './leaderboard/hardcore/+page.server';
import { load as loreDetailLoad } from './lore/[slug]/+page.server';
import { load as loreListLoad } from './lore/+page.server';
import { load as patchNoteLoad } from './patch-notes/[slug]/+page.server';
import { load as patchNotesListLoad } from './patch-notes/+page.server';
import { load as playerLoad } from './players/[id]/+page.server';

/**
 * Public route loads.
 *
 * These are the SEO surface: they must render with an empty API, surface the upstream error
 * instead of an opaque 500, and 404 honestly on a missing or malformed slug. `callLoad` erases the
 * per-route event type; the data casts below erase the `void` arm that every guarded load carries.
 */

const editorial = vi.hoisted(() => ({
	listAnnouncements: vi.fn(),
	listLoreArticles: vi.fn(),
	listPatchNotes: vi.fn(),
	getLoreArticle: vi.fn(),
	getPatchNote: vi.fn(),
	getPlayerProfile: vi.fn(),
	getServerHealth: vi.fn()
}));

const characters = vi.hoisted(() => ({ fetchHardcoreLeaderboard: vi.fn() }));

vi.mock('#lib/server/editorial-api', () => editorial);
vi.mock('#lib/server/character-api', () => characters);

const page = <T>(items: T[], totalCount = items.length) => ({
	items,
	totalCount,
	page: 1,
	pageSize: 20
});

const data = (value: unknown) => value as Record<string, unknown>;

/** Captures the SvelteKit error a load rejects with (every load here is async). */
async function rejected(value: unknown): Promise<{ status: number; body: { message: string } }> {
	try {
		await value;
	} catch (error) {
		return error as { status: number; body: { message: string } };
	}

	throw new Error('Expected the load to reject.');
}

beforeEach(() => {
	editorial.listAnnouncements.mockResolvedValue({ page: page([]), error: null });
	editorial.listLoreArticles.mockResolvedValue({ page: page([]), error: null });
	editorial.listPatchNotes.mockResolvedValue({ page: page([]), error: null });
	editorial.getLoreArticle.mockResolvedValue({ article: null, error: null });
	editorial.getPatchNote.mockResolvedValue({ note: null, error: null });
	editorial.getPlayerProfile.mockResolvedValue({ profile: null, error: null });
	editorial.getServerHealth.mockResolvedValue(true);
	characters.fetchHardcoreLeaderboard.mockResolvedValue({ status: 200, data: null, error: null });
});

describe('home', () => {
	it('takes the two newest announcements and three of each article type', async () => {
		editorial.listAnnouncements.mockResolvedValue({
			page: page([{ id: 'a1' }, { id: 'a2' }, { id: 'a3' }]),
			error: null
		});
		editorial.listLoreArticles.mockResolvedValue({ page: page([{ id: 'l1' }]), error: null });
		editorial.listPatchNotes.mockResolvedValue({ page: page([{ id: 'p1' }]), error: null });

		const result = data(await callLoad(homeLoad, makeServerEvent()));

		expect(result.announcements).toEqual([{ id: 'a1' }, { id: 'a2' }]);
		expect(result.lore).toEqual([{ id: 'l1' }]);
		expect(result.patchNotes).toEqual([{ id: 'p1' }]);
		expect(editorial.listLoreArticles).toHaveBeenCalledWith(null, 1, 3);
		expect(editorial.listPatchNotes).toHaveBeenCalledWith(1, 3);
	});
});

describe('lore', () => {
	it('derives the category list from the returned page', async () => {
		editorial.listLoreArticles.mockResolvedValue({
			page: page([
				{ id: '1', category: 'History' },
				{ id: '2', category: 'Factions' },
				{ id: '3', category: 'History' }
			]),
			error: null
		});

		const result = data(
			await callLoad(loreListLoad, makeServerEvent({ url: '/lore?category=History' }))
		);

		expect(result.category).toBe('History');
		expect(result.categories).toEqual(['Factions', 'History']);
		expect(editorial.listLoreArticles).toHaveBeenCalledWith('History');
	});

	it('renders an article as sanitized HTML', async () => {
		editorial.getLoreArticle.mockResolvedValue({
			article: { slug: 'fall', content: '# The Fall' },
			error: null
		});

		const result = data(
			await callLoad(loreDetailLoad, makeServerEvent({ params: { slug: 'fall' } }))
		);

		expect(result.html).toContain('<h1>The Fall</h1>');
	});

	it('maps a missing article to 404 and an API failure to 502', async () => {
		const missing = await rejected(
			callLoad(loreDetailLoad, makeServerEvent({ params: { slug: 'gone' } }))
		);
		expect(missing.status).toBe(404);

		editorial.getLoreArticle.mockResolvedValue({ article: null, error: 'upstream down' });
		const failure = await rejected(
			callLoad(loreDetailLoad, makeServerEvent({ params: { slug: 'gone' } }))
		);
		expect(failure.status).toBe(502);
		expect(failure.body.message).toBe('upstream down');
	});
});

describe('patch notes', () => {
	it('passes the page through', async () => {
		editorial.listPatchNotes.mockResolvedValue({
			page: page([{ id: 'p1' }], 11),
			error: 'partial'
		});

		await expect(callLoad(patchNotesListLoad, makeServerEvent())).resolves.toEqual({
			notes: [{ id: 'p1' }],
			total: 11,
			error: 'partial'
		});
	});

	it('renders a note and 404s a missing one', async () => {
		editorial.getPatchNote.mockResolvedValue({
			note: { slug: 'v1', content: '- fixed a thing' },
			error: null
		});

		const result = data(await callLoad(patchNoteLoad, makeServerEvent({ params: { slug: 'v1' } })));
		expect(result.html).toContain('<li>fixed a thing</li>');

		editorial.getPatchNote.mockResolvedValue({ note: null, error: null });
		const missing = await rejected(
			callLoad(patchNoteLoad, makeServerEvent({ params: { slug: 'v2' } }))
		);
		expect(missing.status).toBe(404);
	});
});

describe('community', () => {
	it('reports the server health alongside the announcements', async () => {
		editorial.listAnnouncements.mockResolvedValue({ page: page([{ id: 'a1' }]), error: null });
		editorial.getServerHealth.mockResolvedValue(false);

		await expect(callLoad(communityLoad, makeServerEvent())).resolves.toEqual({
			announcements: [{ id: 'a1' }],
			error: null,
			online: false
		});
	});
});

describe('player profile', () => {
	const id = '11111111-1111-4111-8111-111111111111';

	it('rejects a malformed id with 404 before calling the API', async () => {
		await expect(
			callLoad(playerLoad, makeServerEvent({ params: { id: 'not-a-guid' } }))
		).rejects.toMatchObject({ status: 404 });
		expect(editorial.getPlayerProfile).not.toHaveBeenCalled();
	});

	it('returns the profile', async () => {
		editorial.getPlayerProfile.mockResolvedValue({
			profile: { accountId: id, username: 'aria' },
			error: null
		});

		await expect(callLoad(playerLoad, makeServerEvent({ params: { id } }))).resolves.toMatchObject({
			profile: { username: 'aria' }
		});
	});

	it('distinguishes a missing profile from an API failure', async () => {
		editorial.getPlayerProfile.mockResolvedValue({ profile: null, error: null });
		await expect(
			rejected(callLoad(playerLoad, makeServerEvent({ params: { id } })))
		).resolves.toMatchObject({ status: 404 });

		editorial.getPlayerProfile.mockResolvedValue({ profile: null, error: 'upstream down' });
		await expect(
			rejected(callLoad(playerLoad, makeServerEvent({ params: { id } })))
		).resolves.toMatchObject({ status: 502 });
	});
});

describe('hardcore leaderboard', () => {
	it('redirects anonymous visitors', async () => {
		await expect(
			callLoad(hardcoreLoad, makeServerEvent({ url: '/leaderboard/hardcore' }))
		).rejects.toMatchObject({ status: 303, location: '/login' });
	});

	it('clamps an invalid page to 1', async () => {
		await callLoad(
			hardcoreLoad,
			makeServerEvent({ url: '/leaderboard/hardcore?page=nonsense', user: fakeUser() })
		);

		expect(characters.fetchHardcoreLeaderboard).toHaveBeenCalledWith(expect.anything(), 1);
	});

	it('passes a valid page through', async () => {
		await callLoad(
			hardcoreLoad,
			makeServerEvent({ url: '/leaderboard/hardcore?page=3', user: fakeUser() })
		);

		expect(characters.fetchHardcoreLeaderboard).toHaveBeenCalledWith(expect.anything(), 3);
	});
});
