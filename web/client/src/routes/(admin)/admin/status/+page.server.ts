import type { PageServerLoad } from './$types';
import { toGameIdentity } from '#lib/server/access';
import { getServerStatus } from '#lib/server/admin-api';

export const load: PageServerLoad = async ({ locals }) => {
	const result = await getServerStatus(toGameIdentity(locals.user!));
	return { status: result.data, error: result.error };
};
