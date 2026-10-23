import type { GameIdentity } from '$lib/server/game-request';
import { gameApiFetch } from '$lib/server/game-request';

/** Row returned by the admin/mod user list endpoints. */
export interface PlayerSummary {
	id: string;
	username: string;
	email: string | null;
	roles: string[];
	isBanned: boolean;
	bannedUntil: string | null;
	warnCount: number;
	isMuted: boolean;
	createdAt: string;
	lastSeenAt: string | null;
}

/** Full account detail including effective permissions. */
export interface PlayerDetail extends PlayerSummary {
	effectivePermissions: string[];
	userPermissions: string[];
	banReason: string | null;
	mutedUntil: string | null;
	muteReason: string | null;
}

/** Admin user list response (`total` naming, unlike Foundry/Editorial). */
export interface AdminUserList {
	total: number;
	page: number;
	pageSize: number;
	items: PlayerSummary[];
}

/** Single audit trail entry. */
export interface AuditEntry {
	id: string;
	actorUsername: string;
	targetAccountId: string | null;
	targetUsername: string | null;
	action: string;
	details: string | null;
	occurredAt: string;
}

/** Player report submitted from the web portal or in-game. */
export interface PlayerReport {
	id: string;
	reporterName: string;
	targetName: string;
	reason: string;
	submittedAt: string;
	isResolved: boolean;
	resolvedAt: string | null;
}

/** Active player session snapshot. */
export interface ActiveSession {
	characterId: string;
	characterName: string;
	accountId: string;
	accountUsername: string;
	regionId: string;
	zoneId: string | null;
	enteredAt: string;
}

/** Zone population entry in the server status snapshot. */
export interface ZonePopulation {
	zoneId: string;
	zoneName: string;
	playerCount: number;
}

/** Health check entry in the server status snapshot. */
export interface HealthCheckEntry {
	name: string;
	status: string;
	description: string | null;
}

/** Live server status snapshot. */
export interface ServerStatus {
	connectedPlayers: number;
	zonePopulations: ZonePopulation[];
	serverStartedAt: string;
	registeredAccounts: number;
	totalCharacters: number;
	healthChecks: HealthCheckEntry[] | null;
}

/** Generic paged admin response (`total` naming). */
export interface AdminPagedResult<T> {
	total: number;
	page: number;
	pageSize: number;
	items: T[];
}

export interface UserFilters {
	search?: string | null;
	role?: string | null;
	status?: string | null;
	sort?: string | null;
	page?: number;
	pageSize?: number;
}

/** Lists users (admin `manage_users`). */
export function listUsers(identity: GameIdentity, filters: UserFilters = {}) {
	return gameApiFetch<AdminUserList>(identity, '/api/admin/users', {
		query: {
			search: filters.search,
			role: filters.role,
			status: filters.status,
			sort: filters.sort,
			page: filters.page ?? 1,
			pageSize: filters.pageSize ?? 25
		}
	});
}

/** Loads one user (admin `manage_users`). */
export function getUser(identity: GameIdentity, id: string) {
	return gameApiFetch<PlayerDetail>(identity, `/api/admin/users/${id}`);
}

/** Assigns a role (admin `manage_users`). */
export function assignRole(identity: GameIdentity, id: string, role: string) {
	return gameApiFetch<null>(identity, `/api/admin/users/${id}/roles`, { json: { role } });
}

/** Revokes a role (admin `manage_users`). */
export function revokeRole(identity: GameIdentity, id: string, role: string) {
	return gameApiFetch<null>(identity, `/api/admin/users/${id}/roles/${encodeURIComponent(role)}`, {
		method: 'DELETE'
	});
}

/** Grants a per-user permission (admin `manage_users`). */
export function grantPermission(identity: GameIdentity, id: string, permission: string) {
	return gameApiFetch<null>(identity, `/api/admin/users/${id}/permissions`, {
		json: { permission }
	});
}

/** Revokes a per-user permission (admin `manage_users`). */
export function revokePermission(identity: GameIdentity, id: string, permission: string) {
	return gameApiFetch<null>(
		identity,
		`/api/admin/users/${id}/permissions/${encodeURIComponent(permission)}`,
		{ method: 'DELETE' }
	);
}

/** Reads the admin audit log. */
export function getAuditLog(
	identity: GameIdentity,
	page = 1,
	pageSize = 50,
	targetId?: string | null
) {
	return gameApiFetch<AdminPagedResult<AuditEntry>>(identity, '/api/admin/audit', {
		query: { targetId, page, pageSize }
	});
}

/** Lists active sessions. */
export function listSessions(
	identity: GameIdentity,
	regionId?: string | null,
	zoneId?: string | null
) {
	return gameApiFetch<ActiveSession[]>(identity, '/api/admin/sessions', {
		query: { regionId, zoneId }
	});
}

/** Loads the live server status snapshot. */
export function getServerStatus(identity: GameIdentity) {
	return gameApiFetch<ServerStatus>(identity, '/api/admin/status');
}

/** Lists player reports (admin). */
export function listReports(
	identity: GameIdentity,
	resolved: boolean | null = null,
	page = 1,
	pageSize = 50
) {
	return gameApiFetch<AdminPagedResult<PlayerReport>>(identity, '/api/admin/reports', {
		query: { resolved, page, pageSize }
	});
}

/** Resolves a player report (admin). */
export function resolveReport(identity: GameIdentity, id: string) {
	return gameApiFetch<null>(identity, `/api/admin/reports/${id}/resolve`, { method: 'PUT' });
}

/** Kicks an account's sessions. */
export function kickPlayer(identity: GameIdentity, accountId: string, reason?: string | null) {
	return gameApiFetch<null>(identity, '/api/admin/players/kick', {
		json: { accountId, reason }
	});
}

/** Bans an account. */
export function banPlayer(
	identity: GameIdentity,
	accountId: string,
	reason: string,
	durationMinutes?: number | null
) {
	return gameApiFetch<null>(identity, '/api/admin/players/ban', {
		json: { accountId, reason, durationMinutes }
	});
}

/** Lifts an account ban. */
export function unbanPlayer(identity: GameIdentity, accountId: string) {
	return gameApiFetch<null>(identity, '/api/admin/players/unban', {
		json: { accountId }
	});
}

/** Issues a formal warning. */
export function warnPlayer(identity: GameIdentity, accountId: string, reason: string) {
	return gameApiFetch<null>(identity, '/api/admin/players/warn', {
		json: { accountId, reason }
	});
}

/** Mutes an account. */
export function mutePlayer(
	identity: GameIdentity,
	accountId: string,
	reason?: string | null,
	durationMinutes?: number | null
) {
	return gameApiFetch<null>(identity, '/api/admin/players/mute', {
		json: { accountId, reason, durationMinutes }
	});
}

/** Lifts an account mute. */
export function unmutePlayer(identity: GameIdentity, accountId: string) {
	return gameApiFetch<null>(identity, '/api/admin/players/unmute', {
		json: { accountId }
	});
}

/** Broadcasts an in-game announcement. */
export function broadcastAnnouncement(identity: GameIdentity, message: string, severity = 'info') {
	return gameApiFetch<null>(identity, '/api/admin/broadcast', {
		json: { message, severity }
	});
}

/** Lists users through the moderator read-only endpoint (`players.view`). */
export function listModUsers(identity: GameIdentity, filters: UserFilters = {}) {
	return gameApiFetch<AdminUserList>(identity, '/api/mod/users', {
		query: {
			search: filters.search,
			status: filters.status,
			sort: filters.sort,
			page: filters.page ?? 1,
			pageSize: filters.pageSize ?? 25
		}
	});
}

/** Loads one user through the moderator read-only endpoint. */
export function getModUser(identity: GameIdentity, id: string) {
	return gameApiFetch<PlayerDetail>(identity, `/api/mod/users/${id}`);
}

/** Lists reports through the moderator endpoint. */
export function listModReports(
	identity: GameIdentity,
	resolved: boolean | null = null,
	page = 1,
	pageSize = 50
) {
	return gameApiFetch<AdminPagedResult<PlayerReport>>(identity, '/api/mod/reports', {
		query: { resolved, page, pageSize }
	});
}

/** Resolves a report through the moderator endpoint. */
export function resolveModReport(identity: GameIdentity, id: string) {
	return gameApiFetch<null>(identity, `/api/mod/reports/${id}/resolve`, { method: 'PUT' });
}

/** Submits a report from the web portal. */
export function submitReport(identity: GameIdentity, targetUsername: string, reason: string) {
	return gameApiFetch<null>(identity, '/api/reports', {
		json: { targetUsername, reason }
	});
}
