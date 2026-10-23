import { gameApiFetch } from '$lib/server/game-request';

/** Summary row returned by the content browse endpoint. */
export interface ContentSummary {
	id: string;
	slug: string;
	displayName: string | null;
	typeKey: string;
	contentType: string;
	rarityWeight: number;
	isActive: boolean;
	updatedAt: string;
}

/** Full content entity; `payload` is the raw camelCase entity JSON. */
export interface ContentDetail {
	summary: ContentSummary;
	payload: Record<string, unknown>;
}

/** Registered content type metadata. */
export interface ContentTypeInfo {
	contentType: string;
	displayLabel: string;
	description: string;
}

/** Paged content browse response. */
export interface ContentPagedResult {
	items: ContentSummary[];
	totalCount: number;
	page: number;
	pageSize: number;
}

/** Lists all registered content type schemas (public). */
export function listContentTypes() {
	return gameApiFetch<ContentTypeInfo[]>(null, '/api/content/schema');
}

/** Browses published content entries of one type (public). */
export function browseContent(
	contentType: string,
	search: string | null = null,
	page = 1,
	pageSize = 20
) {
	return gameApiFetch<ContentPagedResult>(null, '/api/content/browse', {
		query: { type: contentType, search, page, pageSize }
	});
}

/** Loads one published content entry by type and slug (public). */
export function getContentDetail(contentType: string, slug: string) {
	return gameApiFetch<ContentDetail>(
		null,
		`/api/content/browse/${encodeURIComponent(contentType)}/${encodeURIComponent(slug)}`
	);
}
