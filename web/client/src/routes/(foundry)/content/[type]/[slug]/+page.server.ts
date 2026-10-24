import { error } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { getContentDetail } from '#lib/server/content-api';

export const load: PageServerLoad = async ({ params }) => {
	const result = await getContentDetail(params.type, params.slug);
	if (!result.data) {
		error(404, result.error ?? 'Content entry not found.');
	}

	return {
		contentType: params.type,
		slug: params.slug,
		summary: result.data.summary,
		payload: result.data.payload
	};
};
