import { redirect } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';

const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export const load: PageServerLoad = async ({ locals, params }) => {
	if (!locals.user) {
		redirect(303, '/login');
	}

	if (!GUID_PATTERN.test(params.id)) {
		redirect(303, '/characters');
	}

	return { characterId: params.id };
};
