import type { PageServerLoad } from './$types';
import { Permissions, requirePermission, toGameIdentity } from '#lib/server/access';
import { listSessions } from '#lib/server/admin-api';

export const load: PageServerLoad = async ({ parent, locals, url }) => {
	// Await the group layout first: it admits both halves of the staff split, and this page then owns
	// the permission its own API call needs. Without this the page load races the layout and calls
	// the API for a request that is already being refused.
	const { access } = await parent();
	requirePermission(
		access,
		Permissions.ManageUsers,
		'Active sessions require user management permission.'
	);

	const regionId = url.searchParams.get('regionId');
	const zoneId = url.searchParams.get('zoneId');
	const result = await listSessions(toGameIdentity(locals.user!), regionId, zoneId);

	return {
		sessions: result.data ?? [],
		regionId: regionId ?? '',
		zoneId: zoneId ?? '',
		error: result.error
	};
};
