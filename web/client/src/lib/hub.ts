import {
	HubConnectionBuilder,
	LogLevel,
	type HubConnection,
} from '@microsoft/signalr';
import { env } from '$env/dynamic/public';

async function fetchGameToken(): Promise<string> {
	const response = await fetch('/api/game-token', { cache: 'no-store' });
	if (!response.ok) {
		throw new Error(`Game token request failed (${response.status}).`);
	}

	const payload = (await response.json()) as { token: string };
	return payload.token;
}

/**
 * Browser SignalR connection to Veldrath.Server.
 *
 * Tokens are minted by the SvelteKit server on demand so the browser never
 * holds a long-lived credential. The server's CORS policy must allow the
 * SvelteKit origin.
 */
export function createGameHub(): HubConnection {
	const hubUrl = env.PUBLIC_HUB_URL;
	if (!hubUrl) {
		throw new Error('PUBLIC_HUB_URL is not configured.');
	}

	return new HubConnectionBuilder()
		.withUrl(hubUrl, {
			accessTokenFactory: fetchGameToken,
		})
		.withAutomaticReconnect()
		.configureLogging(LogLevel.Information)
		.build();
}
