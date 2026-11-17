import type { PageServerLoad } from './$types';
import { toGameIdentity } from '#lib/server/access';
import { getServerStatus } from '#lib/server/admin-api';

export const load: PageServerLoad = async ({ parent, locals }) => {
	// Await the group layout first: it owns the permission check, and without this the page load
	// races it and calls the API for a request that is already being refused.
	await parent();

	const result = await getServerStatus(toGameIdentity(locals.user!));
	return { status: result.data, error: result.error };
};
