import { error, json } from '@sveltejs/kit';
import type { RequestHandler } from './$types';
import { GAME_TOKEN_TTL_SECONDS, mintGameToken } from '$lib/server/game-token';

export const GET: RequestHandler = async ({ locals }) => {
	if (!locals.user) {
		error(401, 'Not authenticated.');
	}

	const username = locals.user.username ?? locals.user.name;
	const token = await mintGameToken(locals.user.id, username);

	return json(
		{ token, expiresIn: GAME_TOKEN_TTL_SECONDS },
		{ headers: { 'cache-control': 'no-store' } },
	);
};
