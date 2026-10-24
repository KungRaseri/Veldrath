import type { CharacterSummary } from '#lib/game/types';

/** Result of loading the signed-in account's characters through the desktop proxy. */
export interface DesktopCharacterLoad {
	authenticated: boolean;
	characters: CharacterSummary[];
	error: string | null;
}

/**
 * Loads characters through the DesktopUiServer's same-origin `/api` proxy.
 *
 * The proxy injects the desktop client's JWT, so the WebView never handles the token.
 * A 401/403 means the desktop session is missing or expired.
 */
export async function loadDesktopCharacters(): Promise<DesktopCharacterLoad> {
	try {
		const response = await fetch('/api/characters', {
			headers: { accept: 'application/json' },
			cache: 'no-store'
		});

		if (response.status === 401 || response.status === 403) {
			return { authenticated: false, characters: [], error: null };
		}

		if (!response.ok) {
			return {
				authenticated: true,
				characters: [],
				error: `Character list request failed (${response.status}).`
			};
		}

		return {
			authenticated: true,
			characters: (await response.json()) as CharacterSummary[],
			error: null
		};
	} catch {
		return { authenticated: false, characters: [], error: 'Could not reach the game server.' };
	}
}
