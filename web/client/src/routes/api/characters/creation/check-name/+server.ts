import type { RequestHandler } from './$types';
import { requireUser } from '#lib/server/access';
import { checkCharacterName } from '#lib/server/character-api';
import { gameResultResponse } from '#lib/server/game-response';

/** Checks character-name availability for the wizard. */
export const GET: RequestHandler = async ({ locals, url }) => {
	const user = requireUser(locals);
	const name = url.searchParams.get('name') ?? '';
	return gameResultResponse(await checkCharacterName(user, name));
};
