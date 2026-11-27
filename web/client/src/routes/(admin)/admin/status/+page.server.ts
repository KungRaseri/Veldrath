import type { PageServerLoad } from './$types';
import { Permissions, requirePermission, toGameIdentity } from '#lib/server/access';
import { getServerStatus } from '#lib/server/admin-api';

export const load: PageServerLoad = async ({ parent, locals }) => {
	// Await the group layout first: it admits both halves of the staff split, and this page then owns
	// the permission its own API call needs. Without this the page load races the layout and calls
	// the API for a request that is already being refused.
	const { access } = await parent();
	requirePermission(
		access,
		Permissions.ManageUsers,
		'Server status requires user management permission.'
	);

	const result = await getServerStatus(toGameIdentity(locals.user!));
	return { status: result.data, error: result.error };
};
