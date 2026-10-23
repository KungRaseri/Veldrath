import type { PageServerLoad } from './$types';
import { toGameIdentity } from '$lib/server/access';
import { adminListLoreArticles } from '$lib/server/editorial-api';

export const load: PageServerLoad = async ({ locals, url }) => {
	const page = Math.max(1, Number(url.searchParams.get('page') ?? 1));
	const result = await adminListLoreArticles(toGameIdentity(locals.user!), page, 20);

	return {
		articles: result.data?.items ?? [],
		total: result.data?.totalCount ?? 0,
		page: result.data?.page ?? page,
		pageSize: result.data?.pageSize ?? 20,
		error: result.error
	};
};
