import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr';
import { PUBLIC_HUB_URL } from '$app/env/public';

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
 * The SvelteKit server mints short-lived game tokens on demand, so the browser never holds a
 * long-lived credential.
 */
export function createGameHub(): HubConnection {
	const hubUrl = PUBLIC_HUB_URL;
	if (!hubUrl) {
		throw new Error('PUBLIC_HUB_URL is not configured.');
	}

	// A HTTPS page cannot reach an http:// hub — the browser blocks it and SignalR only reports
	// "Failed to complete negotiation with the server: NetworkError". Name the real cause.
	if (
		typeof location !== 'undefined' &&
		location.protocol === 'https:' &&
		hubUrl.startsWith('http:')
	) {
		throw new Error(
			`PUBLIC_HUB_URL (${hubUrl}) is not HTTPS but this page is; rebuild the web image with ` +
				`PUBLIC_HUB_URL=https://<api-host>/hubs/game (Docker build arg).`
		);
	}

	return new HubConnectionBuilder()
		.withUrl(hubUrl, { accessTokenFactory: fetchGameToken })
		.withAutomaticReconnect()
		.configureLogging(LogLevel.Information)
		.build();
}
