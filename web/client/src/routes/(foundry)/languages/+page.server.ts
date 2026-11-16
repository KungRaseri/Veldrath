import { redirect } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';

/**
 * Languages are a content type, not a parallel section.
 *
 * `/content/language` already browses them with the same search and paging as every other type, so this page
 * was a second implementation of the content browser with a hardcoded type — the kind of duplication that
 * drifts. It is a permanent redirect rather than a deletion so bookmarks and any stale in-repo link keep
 * working, and the query survives it so a search or page number is not silently dropped.
 *
 * `languages/new` deliberately stays until H4 gives every content type a create form; removing the only way
 * to create a language before the general one exists would be a regression.
 */
export const load: PageServerLoad = async ({ url }) => {
	const query = url.searchParams.toString();
	redirect(308, query ? `/content/language?${query}` : '/content/language');
};
