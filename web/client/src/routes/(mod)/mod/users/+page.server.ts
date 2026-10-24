import type { PageServerLoad } from './$types';
import { toGameIdentity } from '#lib/server/access';
import { listModUsers } from '#lib/server/admin-api';

export const load: PageServerLoad = async ({ locals, url }) => {
	const filters = {
		search: url.searchParams.get('search'),
		status: url.searchParams.get('status'),
		sort: url.searchParams.get('sort'),
		page: Math.max(1, Number(url.searchParams.get('page') ?? 1))
	};

	const result = await listModUsers(toGameIdentity(locals.user!), { ...filters, pageSize: 25 });
	return {
		users: result.data?.items ?? [],
		total: result.data?.total ?? 0,
		page: result.data?.page ?? filters.page,
		pageSize: result.data?.pageSize ?? 25,
		filters,
		error: result.error
	};
};
