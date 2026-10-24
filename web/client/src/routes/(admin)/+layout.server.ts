import type { LayoutServerLoad } from './$types';
import { Permissions, requirePermission, requireUser } from '#lib/server/access';

/** Admin routes require the `admin.users` permission. */
export const load: LayoutServerLoad = async ({ locals, parent }) => {
	requireUser(locals);
	const { access } = await parent();
	requirePermission(
		access,
		Permissions.ManageUsers,
		'Admin access requires user management permission.'
	);
	return { access };
};
