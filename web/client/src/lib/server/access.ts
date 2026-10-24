import { error, redirect } from '@sveltejs/kit';
import type { GameIdentity } from '#lib/server/game-request';
import { gameApiFetch } from '#lib/server/game-request';

/** Canonical role names mirrored from Veldrath.Server's `Roles` class. */
export const Roles = {
	Admin: 'Admin',
	Moderator: 'Moderator',
	Curator: 'Curator',
	GameMaster: 'GameMaster',
	Developer: 'Developer',
	ContentEditor: 'ContentEditor'
} as const;

/** Canonical permission strings mirrored from Veldrath.Server's `Permissions` class. */
export const Permissions = {
	BanPlayers: 'players.ban',
	KickPlayers: 'players.kick',
	WarnPlayers: 'players.warn',
	MutePlayers: 'players.mute',
	ViewPlayers: 'players.view',
	ManageContent: 'content.manage',
	ViewContent: 'content.view',
	ReviewSubmissions: 'content.review',
	ManageRoles: 'admin.roles',
	ManageUsers: 'admin.users',
	SendAnnouncements: 'admin.announce'
} as const;

/** Role and permission snapshot for the signed-in account. */
export interface AccountAccess {
	accountId: string;
	username: string;
	roles: string[];
	permissions: string[];
}

interface AccountProfileResponse {
	accountId: string;
	username: string;
	roles: string[];
	permissions: string[];
}

/** Redirects anonymous visitors to the login page and returns the signed-in user. */
export function requireUser(locals: App.Locals): AuthUser {
	if (!locals.user) {
		redirect(303, '/login');
	}

	return locals.user;
}

/** Builds the game-token identity for a signed-in Better Auth user. */
export function toGameIdentity(user: AuthUser): GameIdentity {
	return {
		id: user.id,
		username: user.username ?? user.name,
		email: user.email,
		emailVerified: user.emailVerified
	};
}

/** Loads the RBAC snapshot from the .NET account profile endpoint. */
export async function loadAccountAccess(user: AuthUser): Promise<AccountAccess | null> {
	const identity = toGameIdentity(user);

	const result = await gameApiFetch<AccountProfileResponse>(identity, '/api/account/profile');
	if (!result.data) {
		console.warn('[access] could not load the account profile.', result.error);
		return null;
	}

	return {
		accountId: result.data.accountId,
		username: result.data.username,
		roles: result.data.roles ?? [],
		permissions: result.data.permissions ?? []
	};
}

/** Returns `true` when the account holds the permission (Admins hold all permissions). */
export function hasPermission(access: AccountAccess | null, permission: string): boolean {
	if (!access) return false;
	return access.roles.includes(Roles.Admin) || access.permissions.includes(permission);
}

/** Throws a 403 unless the account holds the permission. */
export function requirePermission(
	access: AccountAccess | null,
	permission: string,
	message = 'You do not have permission to view this page.'
): AccountAccess {
	if (!access || !hasPermission(access, permission)) {
		error(403, message);
	}

	return access;
}

/** Returns `true` when the account holds any of the supplied permissions. */
export function hasAnyPermission(
	access: AccountAccess | null,
	permissions: readonly string[]
): boolean {
	return permissions.some((permission) => hasPermission(access, permission));
}
