import { error } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { getPlayerProfile } from '$lib/server/editorial-api';

const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export const load: PageServerLoad = async ({ params }) => {
	if (!GUID_PATTERN.test(params.id)) {
		error(404, 'Player not found.');
	}

	const { profile, error: apiError } = await getPlayerProfile(params.id);
	if (!profile) {
		if (apiError) error(502, apiError);
		error(404, 'Player not found.');
	}

	return { profile };
};
