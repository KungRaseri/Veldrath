import type { PageServerLoad } from './$types';
import { browseContent } from '#lib/server/content-api';

export const load: PageServerLoad = async ({ url }) => {
	const search = url.searchParams.get('search');
	const page = Math.max(1, Number(url.searchParams.get('page') ?? 1));
	const result = await browseContent('language', search, page, 24);

	return {
		languages: result.data?.items ?? [],
		total: result.data?.totalCount ?? 0,
		page: result.data?.page ?? page,
		pageSize: result.data?.pageSize ?? 24,
		search: search ?? '',
		error: result.error
	};
};
