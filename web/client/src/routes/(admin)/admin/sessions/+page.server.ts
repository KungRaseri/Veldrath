import type { PageServerLoad } from './$types';
import { toGameIdentity } from '#lib/server/access';
import { listSessions } from '#lib/server/admin-api';

export const load: PageServerLoad = async ({ locals, url }) => {
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
