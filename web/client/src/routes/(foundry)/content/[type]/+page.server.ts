import type { PageServerLoad } from './$types';
import { Permissions, requirePermission } from '#lib/server/access';
import { browseContent } from '#lib/server/content-api';

export const load: PageServerLoad = async ({ parent, params, url }) => {
	const { access } = await parent();
	requirePermission(
		access,
		Permissions.ViewContent,
		'Foundry needs the content permission. Ask a Curator for access.'
	);

	const search = url.searchParams.get('search');
	const page = Math.max(1, Number(url.searchParams.get('page') ?? 1));
	const result = await browseContent(params.type, search, page, 24);

	return {
		contentType: params.type,
		entries: result.data?.items ?? [],
		total: result.data?.totalCount ?? 0,
		page: result.data?.page ?? page,
		pageSize: result.data?.pageSize ?? 24,
		search: search ?? '',
		error: result.error
	};
};
