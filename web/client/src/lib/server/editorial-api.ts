import { env } from '$env/dynamic/private';
import { env as publicEnv } from '$env/dynamic/public';
import type { GameIdentity } from '$lib/server/game-request';
import { gameApiFetch } from '$lib/server/game-request';

export interface PagedResult<T> {
	items: T[];
	totalCount: number;
	page: number;
	pageSize: number;
}

export interface LoreArticleSummary {
	id: string;
	slug: string;
	title: string;
	summary: string;
	category: string;
	status: string;
	publishedAt?: string | null;
}

export interface LoreArticle extends LoreArticleSummary {
	content: string;
}

export interface PatchNoteSummary {
	id: string;
	slug: string;
	title: string;
	summary: string;
	version: string;
	status: string;
	publishedAt?: string | null;
}

export interface PatchNote extends PatchNoteSummary {
	content: string;
}

export interface EditorialAnnouncement {
	id: string;
	title: string;
	body: string;
	status: string;
	publishedAt?: string | null;
}

export interface PublicPlayerProfile {
	accountId: string;
	username: string;
	level: number;
	characterClass?: string | null;
	species?: string | null;
	currentZone?: string | null;
	registeredAt: string;
}

export interface ApiResult<T> {
	data: T | null;
	error: string | null;
}

function apiBaseUrl(): string | null {
	const base = env.INTERNAL_BRIDGE_URL ?? publicEnv.PUBLIC_API_URL;
	return base ? base.replace(/\/+$/, '') : null;
}

async function getJson<T>(path: string): Promise<ApiResult<T>> {
	const base = apiBaseUrl();
	if (!base) {
		return { data: null, error: 'The game server URL is not configured.' };
	}

	try {
		const response = await fetch(`${base}${path}`, {
			headers: { accept: 'application/json' },
			signal: AbortSignal.timeout(5000)
		});

		if (response.status === 404) {
			return { data: null, error: null };
		}

		if (!response.ok) {
			return { data: null, error: `Request failed (${response.status}).` };
		}

		return { data: (await response.json()) as T, error: null };
	} catch (error) {
		console.warn(`[editorial-api] ${path} failed.`, error);
		return { data: null, error: 'Could not reach the game server.' };
	}
}

const emptyPage = <T>(page: number, pageSize: number): PagedResult<T> => ({
	items: [],
	totalCount: 0,
	page,
	pageSize
});

export async function listLoreArticles(
	category: string | null = null,
	page = 1,
	pageSize = 20
): Promise<{ page: PagedResult<LoreArticleSummary>; error: string | null }> {
	const query = new URLSearchParams({ page: `${page}`, pageSize: `${pageSize}` });
	if (category) query.set('category', category);

	const result = await getJson<PagedResult<LoreArticleSummary>>(
		`/api/editorial/lore?${query.toString()}`
	);

	return {
		page: result.data ?? emptyPage<LoreArticleSummary>(page, pageSize),
		error: result.error
	};
}

export async function getLoreArticle(
	slug: string
): Promise<{ article: LoreArticle | null; error: string | null }> {
	const result = await getJson<LoreArticle>(`/api/editorial/lore/${encodeURIComponent(slug)}`);
	return { article: result.data, error: result.error };
}

export async function listPatchNotes(
	page = 1,
	pageSize = 20
): Promise<{ page: PagedResult<PatchNoteSummary>; error: string | null }> {
	const query = new URLSearchParams({ page: `${page}`, pageSize: `${pageSize}` });
	const result = await getJson<PagedResult<PatchNoteSummary>>(
		`/api/editorial/patch-notes?${query.toString()}`
	);

	return {
		page: result.data ?? emptyPage<PatchNoteSummary>(page, pageSize),
		error: result.error
	};
}

export async function getPatchNote(
	slug: string
): Promise<{ note: PatchNote | null; error: string | null }> {
	const result = await getJson<PatchNote>(`/api/editorial/patch-notes/${encodeURIComponent(slug)}`);
	return { note: result.data, error: result.error };
}

export async function listAnnouncements(
	page = 1,
	pageSize = 20
): Promise<{ page: PagedResult<EditorialAnnouncement>; error: string | null }> {
	const query = new URLSearchParams({ page: `${page}`, pageSize: `${pageSize}` });
	const result = await getJson<PagedResult<EditorialAnnouncement>>(
		`/api/editorial/announcements?${query.toString()}`
	);

	return {
		page: result.data ?? emptyPage<EditorialAnnouncement>(page, pageSize),
		error: result.error
	};
}

export async function getServerHealth(): Promise<boolean> {
	const base = apiBaseUrl();
	if (!base) return false;

	try {
		const response = await fetch(`${base}/health`, {
			signal: AbortSignal.timeout(3000)
		});
		return response.ok;
	} catch {
		return false;
	}
}

export async function getPlayerProfile(
	accountId: string
): Promise<{ profile: PublicPlayerProfile | null; error: string | null }> {
	const result = await getJson<PublicPlayerProfile>(
		`/api/players/${encodeURIComponent(accountId)}`
	);
	return { profile: result.data, error: result.error };
}

// ── Editorial administration (requires content.manage) ───────────────────────

export interface CreatePatchNoteInput {
	slug: string;
	title: string;
	content: string;
	summary: string;
	version: string;
}

export interface UpdatePatchNoteInput {
	title: string;
	content: string;
	summary: string;
	version: string;
	status: string;
}

export interface CreateLoreArticleInput {
	slug: string;
	title: string;
	content: string;
	summary: string;
	category: string;
}

export interface UpdateLoreArticleInput {
	title: string;
	content: string;
	summary: string;
	category: string;
	status: string;
}

export interface CreateAnnouncementInput {
	title: string;
	body: string;
}

export interface UpdateAnnouncementInput {
	title: string;
	body: string;
	status: string;
}

export function adminListPatchNotes(identity: GameIdentity, page = 1, pageSize = 20) {
	return gameApiFetch<PagedResult<PatchNoteSummary>>(identity, '/api/editorial/admin/patch-notes', {
		query: { page, pageSize }
	});
}

export function adminGetPatchNote(identity: GameIdentity, id: string) {
	return gameApiFetch<PatchNote>(identity, `/api/editorial/admin/patch-notes/${id}`);
}

export function adminCreatePatchNote(identity: GameIdentity, input: CreatePatchNoteInput) {
	return gameApiFetch<PatchNote>(identity, '/api/editorial/admin/patch-notes', { json: input });
}

export function adminUpdatePatchNote(
	identity: GameIdentity,
	id: string,
	input: UpdatePatchNoteInput
) {
	return gameApiFetch<PatchNote>(identity, `/api/editorial/admin/patch-notes/${id}`, {
		method: 'PUT',
		json: input
	});
}

export function adminPublishPatchNote(identity: GameIdentity, id: string) {
	return gameApiFetch<PatchNote>(identity, `/api/editorial/admin/patch-notes/${id}/publish`, {
		method: 'POST'
	});
}

export function adminDeletePatchNote(identity: GameIdentity, id: string) {
	return gameApiFetch<null>(identity, `/api/editorial/admin/patch-notes/${id}`, {
		method: 'DELETE'
	});
}

export function adminListLoreArticles(identity: GameIdentity, page = 1, pageSize = 20) {
	return gameApiFetch<PagedResult<LoreArticleSummary>>(identity, '/api/editorial/admin/lore', {
		query: { page, pageSize }
	});
}

export function adminGetLoreArticle(identity: GameIdentity, id: string) {
	return gameApiFetch<LoreArticle>(identity, `/api/editorial/admin/lore/${id}`);
}

export function adminCreateLoreArticle(identity: GameIdentity, input: CreateLoreArticleInput) {
	return gameApiFetch<LoreArticle>(identity, '/api/editorial/admin/lore', { json: input });
}

export function adminUpdateLoreArticle(
	identity: GameIdentity,
	id: string,
	input: UpdateLoreArticleInput
) {
	return gameApiFetch<LoreArticle>(identity, `/api/editorial/admin/lore/${id}`, {
		method: 'PUT',
		json: input
	});
}

export function adminPublishLoreArticle(identity: GameIdentity, id: string) {
	return gameApiFetch<LoreArticle>(identity, `/api/editorial/admin/lore/${id}/publish`, {
		method: 'POST'
	});
}

export function adminDeleteLoreArticle(identity: GameIdentity, id: string) {
	return gameApiFetch<null>(identity, `/api/editorial/admin/lore/${id}`, { method: 'DELETE' });
}

export function adminListAnnouncements(identity: GameIdentity, page = 1, pageSize = 20) {
	return gameApiFetch<PagedResult<EditorialAnnouncement>>(
		identity,
		'/api/editorial/admin/announcements',
		{
			query: { page, pageSize }
		}
	);
}

export function adminGetAnnouncement(identity: GameIdentity, id: string) {
	return gameApiFetch<EditorialAnnouncement>(identity, `/api/editorial/admin/announcements/${id}`);
}

export function adminCreateAnnouncement(identity: GameIdentity, input: CreateAnnouncementInput) {
	return gameApiFetch<EditorialAnnouncement>(identity, '/api/editorial/admin/announcements', {
		json: input
	});
}

export function adminUpdateAnnouncement(
	identity: GameIdentity,
	id: string,
	input: UpdateAnnouncementInput
) {
	return gameApiFetch<EditorialAnnouncement>(identity, `/api/editorial/admin/announcements/${id}`, {
		method: 'PUT',
		json: input
	});
}

export function adminPublishAnnouncement(identity: GameIdentity, id: string) {
	return gameApiFetch<EditorialAnnouncement>(
		identity,
		`/api/editorial/admin/announcements/${id}/publish`,
		{ method: 'POST' }
	);
}

export function adminDeleteAnnouncement(identity: GameIdentity, id: string) {
	return gameApiFetch<null>(identity, `/api/editorial/admin/announcements/${id}`, {
		method: 'DELETE'
	});
}
