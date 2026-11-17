import type { PageServerLoad } from './$types';
import { toGameIdentity } from '#lib/server/access';
import { listSessions } from '#lib/server/admin-api';

export const load: PageServerLoad = async ({ parent, locals, url }) => {
	// Await the group layout first: it owns the permission check, and without this the page load
	// races it and calls the API for a request that is already being refused.
	await parent();

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
