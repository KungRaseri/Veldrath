import type { PageServerLoad } from './$types';
import { listLoreArticles } from '$lib/server/editorial-api';

export const load: PageServerLoad = async ({ url }) => {
	const category = url.searchParams.get('category');
	const { page, error } = await listLoreArticles(category);
	const categories = [...new Set(page.items.map((article) => article.category))].sort();

	return {
		category,
		categories,
		articles: page.items,
		total: page.totalCount,
		error
	};
};
