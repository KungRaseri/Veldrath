// @vitest-environment node
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { installFetch, jsonResponse, textResponse } from '#lib/testing/fetch';
import {
	adminCreateAnnouncement,
	adminCreateLoreArticle,
	adminCreatePatchNote,
	adminDeleteAnnouncement,
	adminDeleteLoreArticle,
	adminDeletePatchNote,
	adminGetAnnouncement,
	adminGetLoreArticle,
	adminGetPatchNote,
	adminListAnnouncements,
	adminListLoreArticles,
	adminListPatchNotes,
	adminPublishAnnouncement,
	adminPublishLoreArticle,
	adminPublishPatchNote,
	adminUpdateAnnouncement,
	adminUpdateLoreArticle,
	adminUpdatePatchNote,
	getLoreArticle,
	getPatchNote,
	getPlayerProfile,
	getServerHealth,
	listAnnouncements,
	listLoreArticles,
	listPatchNotes
} from '#lib/server/editorial-api';

/**
 * The public editorial reads translate a paged .NET response into the shape the pages render, and
 * a 404 must be a normal "not found", not an error banner. The server health probe feeds the home
 * page's status card and must never throw.
 */

const gameApiFetch = vi.hoisted(() => vi.fn());
const gameApiBaseUrl = vi.hoisted(() => vi.fn<() => string | null>(() => 'http://bridge'));

vi.mock('#lib/server/game-request', () => ({ gameApiFetch, gameApiBaseUrl }));

const identity = { id: 'user-1', username: 'editor' };

const page = (items: unknown[] = [], totalCount = 0) => ({
	items,
	totalCount,
	page: 1,
	pageSize: 20
});

const restores: Array<() => void> = [];

beforeEach(() => {
	gameApiFetch.mockResolvedValue({ status: 200, data: page(), error: null });
	gameApiBaseUrl.mockReturnValue('http://bridge');
});

afterEach(() => {
	gameApiFetch.mockReset();
	for (const restore of restores.splice(0)) restore();
	vi.restoreAllMocks();
});

const lastCall = () => {
	const call = gameApiFetch.mock.calls.at(-1) as unknown[];
	return { identity: call[0], path: call[1], options: call[2] } as {
		identity: unknown;
		path: string;
		options?: Record<string, unknown>;
	};
};

describe('public editorial reads', () => {
	it('lists lore with paging and no category filter', async () => {
		gameApiFetch.mockResolvedValue({
			status: 200,
			data: page([{ slug: 'emberfall' }], 1),
			error: null
		});

		const result = await listLoreArticles();

		expect(lastCall().path).toBe('/api/editorial/lore?page=1&pageSize=20');
		expect(result.page.items).toEqual([{ slug: 'emberfall' }]);
		expect(result.page.totalCount).toBe(1);
		expect(result.error).toBeNull();
	});

	it('adds the category filter', async () => {
		await listLoreArticles('history', 2, 5);

		expect(lastCall().path).toBe('/api/editorial/lore?page=2&pageSize=5&category=history');
	});

	it('returns an empty page with the upstream error when the request fails', async () => {
		gameApiFetch.mockResolvedValue({ status: 503, data: null, error: 'down' });

		// The first parameter is the category filter, not the page.
		const result = await listLoreArticles(null, 3, 10);

		expect(result.page).toEqual({ items: [], totalCount: 0, page: 3, pageSize: 10 });
		expect(result.error).toBe('Request failed (503).');
	});

	it('treats a 404 as a normal empty page', async () => {
		gameApiFetch.mockResolvedValue({ status: 404, data: null, error: null });

		await expect(listLoreArticles()).resolves.toMatchObject({ error: null });
	});

	it('passes a transport error through when no status is available', async () => {
		gameApiFetch.mockResolvedValue({
			status: 0,
			data: null,
			error: 'Could not reach the game server.'
		});

		await expect(listLoreArticles()).resolves.toMatchObject({
			error: 'Could not reach the game server.'
		});
	});

	it('loads one article and encodes the slug', async () => {
		gameApiFetch.mockResolvedValue({
			status: 200,
			data: { slug: 'the fall', content: '# Body' },
			error: null
		});

		const result = await getLoreArticle('the fall');

		expect(lastCall().path).toBe('/api/editorial/lore/the%20fall');
		expect(result.article).toMatchObject({ slug: 'the fall' });
	});

	it('reports a missing article as null without an error', async () => {
		gameApiFetch.mockResolvedValue({ status: 404, data: null, error: null });

		await expect(getLoreArticle('nope')).resolves.toEqual({ article: null, error: null });
	});

	it('falls back to empty pages for patch notes and announcements', async () => {
		gameApiFetch.mockResolvedValue({ status: 404, data: null, error: null });

		await expect(listPatchNotes(2, 5)).resolves.toMatchObject({
			page: { items: [], page: 2, pageSize: 5 },
			error: null
		});

		gameApiFetch.mockResolvedValue({ status: 500, data: null, error: 'boom' });
		await expect(listAnnouncements()).resolves.toMatchObject({
			page: { items: [], page: 1, pageSize: 20 },
			error: 'Request failed (500).'
		});
	});

	it('lists and loads patch notes and announcements', async () => {
		await listPatchNotes(2, 5);
		expect(lastCall().path).toBe('/api/editorial/patch-notes?page=2&pageSize=5');

		gameApiFetch.mockResolvedValue({ status: 200, data: { slug: 'v1' }, error: null });
		await expect(getPatchNote('v1')).resolves.toMatchObject({ note: { slug: 'v1' } });

		await listAnnouncements();
		expect(lastCall().path).toBe('/api/editorial/announcements?page=1&pageSize=20');
	});

	it('loads a public player profile', async () => {
		gameApiFetch.mockResolvedValue({
			status: 200,
			data: { accountId: 'acc-1', username: 'aria' },
			error: null
		});

		await expect(getPlayerProfile('acc-1')).resolves.toMatchObject({
			profile: { username: 'aria' }
		});
		expect(lastCall().path).toBe('/api/players/acc-1');
	});
});

describe('getServerHealth', () => {
	it('is false when the API URL is not configured', async () => {
		gameApiBaseUrl.mockReturnValue(null);

		await expect(getServerHealth()).resolves.toBe(false);
	});

	it('is true for a 2xx health probe', async () => {
		const stub = installFetch(() => jsonResponse({ status: 'ok' }));
		restores.push(stub.restore);

		await expect(getServerHealth()).resolves.toBe(true);
		expect(stub.calls[0].url).toBe('http://bridge/health');
	});

	it('is false for a non-2xx response', async () => {
		const stub = installFetch(() => textResponse('down', { status: 503 }));
		restores.push(stub.restore);

		await expect(getServerHealth()).resolves.toBe(false);
	});

	it('is false when the probe throws', async () => {
		const stub = installFetch(() => {
			throw new Error('ECONNREFUSED');
		});
		restores.push(stub.restore);

		await expect(getServerHealth()).resolves.toBe(false);
	});
});

describe('editorial administration', () => {
	it('lists the three admin collections', () => {
		adminListPatchNotes(identity, 2, 5);
		expect(lastCall()).toMatchObject({
			identity,
			path: '/api/editorial/admin/patch-notes',
			options: { query: { page: 2, pageSize: 5 } }
		});

		adminListLoreArticles(identity);
		expect(lastCall().path).toBe('/api/editorial/admin/lore');

		adminListAnnouncements(identity);
		expect(lastCall().path).toBe('/api/editorial/admin/announcements');
	});

	it('loads one entry of each collection', () => {
		adminGetPatchNote(identity, 'id-1');
		expect(lastCall().path).toBe('/api/editorial/admin/patch-notes/id-1');

		adminGetLoreArticle(identity, 'id-2');
		expect(lastCall().path).toBe('/api/editorial/admin/lore/id-2');

		adminGetAnnouncement(identity, 'id-3');
		expect(lastCall().path).toBe('/api/editorial/admin/announcements/id-3');
	});

	it('creates entries with POST payloads', () => {
		adminCreatePatchNote(identity, {
			slug: 'v1',
			title: 'v1',
			content: 'body',
			summary: 'summary',
			version: '1.0.0'
		});
		expect(lastCall().options).toMatchObject({ json: { slug: 'v1' } });

		adminCreateLoreArticle(identity, {
			slug: 'lore',
			title: 'Lore',
			content: 'body',
			summary: 'summary',
			category: 'history'
		});
		expect(lastCall().path).toBe('/api/editorial/admin/lore');

		adminCreateAnnouncement(identity, { title: 'News', body: 'Body' });
		expect(lastCall().path).toBe('/api/editorial/admin/announcements');
		expect(lastCall().options).toMatchObject({ json: { title: 'News', body: 'Body' } });
	});

	it('updates entries with PUT payloads', () => {
		adminUpdatePatchNote(identity, 'id-1', {
			title: 'v1',
			content: 'body',
			summary: 'summary',
			version: '1.0.1',
			status: 'Draft'
		});
		expect(lastCall()).toMatchObject({
			path: '/api/editorial/admin/patch-notes/id-1',
			options: { method: 'PUT' }
		});

		adminUpdateLoreArticle(identity, 'id-2', {
			title: 'Lore',
			content: 'body',
			summary: 'summary',
			category: 'history',
			status: 'Draft'
		});
		expect(lastCall().options).toMatchObject({ method: 'PUT' });

		adminUpdateAnnouncement(identity, 'id-3', { title: 'News', body: 'Body', status: 'Draft' });
		expect(lastCall()).toMatchObject({
			path: '/api/editorial/admin/announcements/id-3',
			options: { method: 'PUT' }
		});
	});

	it('publishes entries with POST', () => {
		adminPublishPatchNote(identity, 'id-1');
		expect(lastCall()).toMatchObject({
			path: '/api/editorial/admin/patch-notes/id-1/publish',
			options: { method: 'POST' }
		});

		adminPublishLoreArticle(identity, 'id-2');
		expect(lastCall().path).toBe('/api/editorial/admin/lore/id-2/publish');

		adminPublishAnnouncement(identity, 'id-3');
		expect(lastCall().path).toBe('/api/editorial/admin/announcements/id-3/publish');
	});

	it('deletes entries with DELETE', () => {
		adminDeletePatchNote(identity, 'id-1');
		expect(lastCall().options).toMatchObject({ method: 'DELETE' });

		adminDeleteLoreArticle(identity, 'id-2');
		expect(lastCall().path).toBe('/api/editorial/admin/lore/id-2');

		adminDeleteAnnouncement(identity, 'id-3');
		expect(lastCall()).toMatchObject({
			path: '/api/editorial/admin/announcements/id-3',
			options: { method: 'DELETE' }
		});
	});
});
