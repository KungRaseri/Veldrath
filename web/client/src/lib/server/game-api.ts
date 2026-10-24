import { env } from '#lib/server/env';
import { PUBLIC_API_URL } from '$app/env/public';
import { mintGameToken } from './game-token';
import type { CharacterSummary } from '#lib/game/types';

export interface CharacterListResult {
	characters: CharacterSummary[];
	error: string | null;
}

/**
 * Loads the signed-in account's characters directly from Veldrath.Server.
 *
 * Runs during SSR with a freshly minted game token. The internal container URL is
 * preferred so the request never leaves the Docker network in production.
 */
export async function fetchCharactersForUser(user: AuthUser): Promise<CharacterListResult> {
	const baseUrl = env.INTERNAL_BRIDGE_URL ?? PUBLIC_API_URL;
	if (!baseUrl) {
		return { characters: [], error: 'The game server URL is not configured.' };
	}

	try {
		const token = await mintGameToken(
			user.id,
			user.username ?? user.name,
			user.email,
			user.emailVerified
		);

		const response = await fetch(`${baseUrl.replace(/\/+$/, '')}/api/characters`, {
			headers: { authorization: `Bearer ${token}` },
			signal: AbortSignal.timeout(5000)
		});

		if (!response.ok) {
			return {
				characters: [],
				error: `Character list request failed (${response.status}).`
			};
		}

		return { characters: (await response.json()) as CharacterSummary[], error: null };
	} catch (error) {
		console.warn('[game-api] character list request failed.', error);
		return { characters: [], error: 'Could not reach the game server.' };
	}
}
