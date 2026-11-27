import { error, json } from '@sveltejs/kit';
import type { RequestHandler } from './$types';
import { toGameIdentity } from '#lib/server/access';
import { gameApiFetch } from '#lib/server/game-request';
import { GAME_TOKEN_TTL_SECONDS, mintGameToken } from '#lib/server/game-token';

/** The part of the account profile the preflight needs: the backend answering means the caller is admitted. */
interface SelfProfile {
	accountId: string;
}

export const GET: RequestHandler = async ({ locals }) => {
	if (!locals.user) {
		error(401, 'Not authenticated.');
	}

	// A Better Auth session is not the admission decision. Ask the game server for the caller's own authorized
	// profile first, with an internal bearer that never leaves the server: a banned account is refused there, and
	// an unreachable or failing backend must not become a browser bearer either. The backend stays authoritative
	// even if a ban lands between this preflight and the response.
	const profile = await gameApiFetch<SelfProfile>(
		toGameIdentity(locals.user),
		'/api/account/profile'
	);
	if (!profile.data) {
		if (profile.status === 401 || profile.status === 403) {
			error(403, 'Game access is not available for this account.');
		}

		error(503, 'The game server is unavailable.');
	}

	const username = locals.user.username ?? locals.user.name;
	const token = await mintGameToken(
		locals.user.id,
		username,
		locals.user.email,
		locals.user.emailVerified
	);

	return json(
		{ token, expiresIn: GAME_TOKEN_TTL_SECONDS },
		{ headers: { 'cache-control': 'no-store' } }
	);
};
