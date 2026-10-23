import type { PageServerLoad } from './$types';
import { toGameIdentity } from '$lib/server/access';
import { listSubmissions } from '$lib/server/foundry-api';

export const load: PageServerLoad = async ({ locals, url }) => {
	const filters = {
		status: url.searchParams.get('status'),
		contentType: url.searchParams.get('contentType'),
		search: url.searchParams.get('search'),
		page: Math.max(1, Number(url.searchParams.get('page') ?? 1))
	};

	const result = await listSubmissions(toGameIdentity(locals.user!), {
		...filters,
		pageSize: 20
	});

	return {
		submissions: result.data?.items ?? [],
		total: result.data?.totalCount ?? 0,
		page: result.data?.page ?? filters.page,
		pageSize: result.data?.pageSize ?? 20,
		filters,
		error: result.error
	};
};
