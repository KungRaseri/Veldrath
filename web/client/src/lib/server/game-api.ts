import { toGameIdentity } from './access';
import { gameApiFetch } from './game-request';
import type { CharacterSummary } from '#lib/game/types';

export interface CharacterListResult {
	characters: CharacterSummary[];
	error: string | null;
}

/**
 * Loads the signed-in account's characters directly from Veldrath.Server.
 *
 * Runs during SSR with a freshly minted game token. Delegates to {@link gameApiFetch} so
 * the request uses the internal container URL when configured and reuses the shared
 * token/auth diagnostics.
 */
export async function fetchCharactersForUser(user: AuthUser): Promise<CharacterListResult> {
	const result = await gameApiFetch<CharacterSummary[]>(toGameIdentity(user), '/api/characters', {
		timeoutMs: 5000
	});

	if (!result.data) {
		return {
			characters: [],
			// status 0 means the request never reached the API (config or token minting problem).
			error: result.status
				? `Character list request failed (${result.status}).`
				: (result.error ?? 'Could not reach the game server.')
		};
	}

	return { characters: result.data, error: null };
}
