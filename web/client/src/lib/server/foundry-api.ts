import type { GameIdentity } from '$lib/server/game-request';
import { gameApiFetch } from '$lib/server/game-request';

/** Paged response returned by the Foundry submission list endpoint. */
export interface FoundryPagedResult<T> {
	items: T[];
	totalCount: number;
	page: number;
	pageSize: number;
}

/** Lightweight submission row. */
export interface FoundrySubmissionSummary {
	id: string;
	contentType: string;
	title: string;
	status: string;
	submitterName: string;
	voteScore: number;
	createdAt: string;
}

/** Full submission detail. */
export interface FoundrySubmission extends FoundrySubmissionSummary {
	description: string | null;
	payload: string;
	submitterId: string;
	reviewerName: string | null;
	reviewNotes: string | null;
	updatedAt: string;
	reviewedAt: string | null;
}

/** Notification delivered to a submitter. */
export interface FoundryNotification {
	id: string;
	submissionId: string;
	submissionTitle: string;
	message: string;
	isRead: boolean;
	createdAt: string;
}

/** Request body for creating a submission. */
export interface CreateSubmissionInput {
	contentType: string;
	title: string;
	payload: string;
	description?: string | null;
}

/** Filters accepted by the submission list. */
export interface SubmissionFilters {
	status?: string | null;
	contentType?: string | null;
	search?: string | null;
	page?: number;
	pageSize?: number;
}

/** Lists community submissions with optional filtering. */
export function listSubmissions(identity: GameIdentity | null, filters: SubmissionFilters = {}) {
	return gameApiFetch<FoundryPagedResult<FoundrySubmissionSummary>>(
		identity,
		'/api/foundry/submissions',
		{
			query: {
				status: filters.status,
				contentType: filters.contentType,
				search: filters.search,
				page: filters.page ?? 1,
				pageSize: filters.pageSize ?? 20
			}
		}
	);
}

/** Returns one submission by id. */
export function getSubmission(identity: GameIdentity | null, id: string) {
	return gameApiFetch<FoundrySubmission>(identity, `/api/foundry/submissions/${id}`);
}

/** Creates a submission. */
export function createSubmission(identity: GameIdentity, input: CreateSubmissionInput) {
	return gameApiFetch<FoundrySubmission>(identity, '/api/foundry/submissions', { json: input });
}

/** Casts a vote (+1 or -1) on a submission. */
export function voteSubmission(identity: GameIdentity, id: string, value: number) {
	return gameApiFetch<FoundrySubmissionSummary>(identity, `/api/foundry/submissions/${id}/vote`, {
		json: { value }
	});
}

/** Submits a curator review decision. */
export function reviewSubmission(
	identity: GameIdentity,
	id: string,
	approved: boolean,
	notes?: string | null
) {
	return gameApiFetch<FoundrySubmission>(identity, `/api/foundry/submissions/${id}/review`, {
		json: { approved, notes }
	});
}

/** Returns the signed-in user's notifications. */
export function listNotifications(identity: GameIdentity) {
	return gameApiFetch<FoundryNotification[]>(identity, '/api/foundry/notifications');
}

/** Marks one notification as read. */
export function markNotificationRead(identity: GameIdentity, id: string) {
	return gameApiFetch<null>(identity, `/api/foundry/notifications/${id}/read`, { method: 'POST' });
}
