import type { RequestHandler } from './$types';
import { requireUser } from '#lib/server/access';
import { abandonCreationSession } from '#lib/server/character-api';
import { gameResultResponse } from '#lib/server/game-response';

/** Abandons an in-progress character creation session. */
export const DELETE: RequestHandler = async ({ locals, params }) => {
	const user = requireUser(locals);
	return gameResultResponse(await abandonCreationSession(user, params.id));
};
