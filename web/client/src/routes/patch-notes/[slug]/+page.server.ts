import { error } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { getPatchNote } from '#lib/server/editorial-api';
import { renderMarkdown } from '#lib/server/markdown';

export const load: PageServerLoad = async ({ params }) => {
	const { note, error: apiError } = await getPatchNote(params.slug);

	if (!note) {
		if (apiError) error(502, apiError);
		error(404, 'Patch note not found.');
	}

	return {
		note,
		html: renderMarkdown(note.content)
	};
};
