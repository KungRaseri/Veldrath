import type { PageServerLoad } from './$types';
import { toGameIdentity } from '#lib/server/access';
import { getAuditLog } from '#lib/server/admin-api';

export const load: PageServerLoad = async ({ parent, locals, url }) => {
	// Await the group layout first: it owns the permission check, and without this the page load
	// races it and calls the API for a request that is already being refused.
	await parent();

	const page = Math.max(1, Number(url.searchParams.get('page') ?? 1));
	const targetId = url.searchParams.get('targetId');
	const result = await getAuditLog(toGameIdentity(locals.user!), page, 50, targetId);

	return {
		entries: result.data?.items ?? [],
		total: result.data?.total ?? 0,
		page: result.data?.page ?? page,
		pageSize: result.data?.pageSize ?? 50,
		targetId: targetId ?? '',
		error: result.error
	};
};
