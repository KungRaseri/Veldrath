import { redirect } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { fetchHardcoreLeaderboard } from '#lib/server/character-api';

export const load: PageServerLoad = async ({ locals, url }) => {
	if (!locals.user) {
		redirect(303, '/login');
	}

	const requested = Number(url.searchParams.get('page') ?? '1');
	const page = Number.isFinite(requested) && requested > 0 ? Math.floor(requested) : 1;
	const board = await fetchHardcoreLeaderboard(locals.user, page);

	return {
		board: board.data,
		loadError: board.error
	};
};
