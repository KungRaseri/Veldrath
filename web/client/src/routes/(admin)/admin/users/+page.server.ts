import type { PageServerLoad } from './$types';
import { toGameIdentity } from '#lib/server/access';
import { listUsers } from '#lib/server/admin-api';

export const load: PageServerLoad = async ({ parent, locals, url }) => {
	// Await the group layout first: it owns the permission check, and without this the page load
	// races it and calls the API for a request that is already being refused.
	await parent();

	const filters = {
		search: url.searchParams.get('search'),
		role: url.searchParams.get('role'),
		status: url.searchParams.get('status'),
		sort: url.searchParams.get('sort'),
		page: Math.max(1, Number(url.searchParams.get('page') ?? 1))
	};

	const result = await listUsers(toGameIdentity(locals.user!), { ...filters, pageSize: 25 });
	return {
		users: result.data?.items ?? [],
		total: result.data?.total ?? 0,
		page: result.data?.page ?? filters.page,
		pageSize: result.data?.pageSize ?? 25,
		filters,
		error: result.error
	};
};
