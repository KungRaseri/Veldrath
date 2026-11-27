import { error } from '@sveltejs/kit';
import type { LayoutServerLoad } from './$types';
import { Permissions, hasAnyPermission, requireUser } from '#lib/server/access';

/**
 * The admin group admits either half of the staff split: `admin.users` administers accounts,
 * `admin.roles` hands out authority. Neither implies the other, so the group cannot name a single
 * permission — each page below gates its own API with the permission that API actually requires.
 *
 * @see plans/sveltekit-migration.md — "(admin) requires `admin.users` or `admin.roles`".
 */
export const load: LayoutServerLoad = async ({ locals, parent }) => {
	requireUser(locals);
	const { access } = await parent();

	if (!hasAnyPermission(access, [Permissions.ManageUsers, Permissions.ManageRoles])) {
		error(403, 'Admin access requires user or role management permission.');
	}

	return { access };
};
