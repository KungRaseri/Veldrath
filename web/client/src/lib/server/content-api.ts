import { gameApiFetch } from '#lib/server/game-request';
import type { GameIdentity } from '#lib/server/game-request';

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

/** One validation issue returned by the write path. */
export interface ContentWriteIssue {
	rule: string;
	message: string;
	severity: 'Warning' | 'Error';
	field: string | null;
	identifier: string | null;
}

/** Response body shared by `validate`, create and update. */
export interface ContentWriteResult {
	entity: Record<string, unknown>;
	isValid: boolean;
	errors: ContentWriteIssue[];
	warnings: ContentWriteIssue[];
}

/** Loads the editable field schema for one content type (public). */
export function getContentTypeSchema(contentType: string) {
	return gameApiFetch<import('#lib/foundry/content-editor').ContentTypeSchema>(
		null,
		`/api/content/${encodeURIComponent(contentType)}/schema`
	);
}

/** Dry-run validation for one entity. Requires `content.manage`. */
export function validateContent(identity: GameIdentity, contentType: string, slug: string) {
	return gameApiFetch<ContentWriteResult>(
		identity,
		`/api/content/${encodeURIComponent(contentType)}/${encodeURIComponent(slug)}/validate`
	);
}

/** Creates one entity. Requires `content.manage`. */
export function createContent(
	identity: GameIdentity,
	contentType: string,
	payload: Record<string, unknown>
) {
	return gameApiFetch<ContentWriteResult>(
		identity,
		`/api/content/${encodeURIComponent(contentType)}`,
		{
			method: 'POST',
			json: payload
		}
	);
}

/** Updates one entity. Requires `content.manage`; `expectedUpdatedAt` makes the write concurrency-safe. */
export function updateContent(
	identity: GameIdentity,
	contentType: string,
	slug: string,
	payload: Record<string, unknown>,
	expectedUpdatedAt: string | null
) {
	return gameApiFetch<ContentWriteResult>(
		identity,
		`/api/content/${encodeURIComponent(contentType)}/${encodeURIComponent(slug)}`,
		{ method: 'PUT', json: payload, query: { expectedUpdatedAt } }
	);
}

/** Deactivates one entity. Requires `content.manage`. */
export function deactivateContent(identity: GameIdentity, contentType: string, slug: string) {
	return gameApiFetch<{ type: string; slug: string; isActive: boolean }>(
		identity,
		`/api/content/${encodeURIComponent(contentType)}/${encodeURIComponent(slug)}`,
		{ method: 'DELETE' }
	);
}

/** Dry-run validation for an unsaved payload. Requires `content.manage`. */
export function validateContentPayload(
	identity: GameIdentity,
	contentType: string,
	slug: string,
	payload: Record<string, unknown>
) {
	return gameApiFetch<ContentWriteResult>(
		identity,
		`/api/content/${encodeURIComponent(contentType)}/${encodeURIComponent(slug)}/validate`,
		{ method: 'POST', json: payload }
	);
}
