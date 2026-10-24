import type { RequestHandler } from './$types';
import { requireUser } from '#lib/server/access';
import { beginCreationSession } from '#lib/server/character-api';
import { gameResultResponse } from '#lib/server/game-response';

/** Starts a character creation session for the signed-in account. */
export const POST: RequestHandler = async ({ locals }) => {
	const user = requireUser(locals);
	return gameResultResponse(await beginCreationSession(user));
};
