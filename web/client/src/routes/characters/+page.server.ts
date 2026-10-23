import { redirect } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { fetchCharactersForUser } from '$lib/server/game-api';

export const load: PageServerLoad = async ({ locals }) => {
	if (!locals.user) {
		redirect(303, '/login');
	}

	const result = await fetchCharactersForUser(locals.user);
	return { characters: result.characters, loadError: result.error };
};
