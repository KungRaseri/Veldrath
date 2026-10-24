import { error } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { getLoreArticle } from '#lib/server/editorial-api';
import { renderMarkdown } from '#lib/server/markdown';

export const load: PageServerLoad = async ({ params }) => {
	const { article, error: apiError } = await getLoreArticle(params.slug);

	if (!article) {
		if (apiError) error(502, apiError);
		error(404, 'Lore article not found.');
	}

	return {
		article,
		html: renderMarkdown(article.content)
	};
};
