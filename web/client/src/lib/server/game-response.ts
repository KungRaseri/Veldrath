import { json } from '@sveltejs/kit';
import type { GameApiResult } from './game-request';

/**
 * Converts a {@link GameApiResult} from the .NET client helpers into a SvelteKit response.
 *
 * Keeps the upstream status for client errors (400/403/404/409) so the creation wizard can
 * show the server's validation message, and maps unreachable-server results to 502.
 */
export function gameResultResponse<T>(result: GameApiResult<T>): Response {
	if (result.status === 204) {
		return new Response(null, { status: 204 });
	}

	if (result.error) {
		return json({ error: result.error }, { status: result.status >= 400 ? result.status : 502 });
	}

	return json(result.data, { status: result.status || 200 });
}
