import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr';
import { env } from '$env/dynamic/public';

async function fetchGameToken(): Promise<string> {
	const response = await fetch('/api/game-token', { cache: 'no-store' });
	if (!response.ok) {
		throw new Error(`Game token request failed (${response.status}).`);
	}

	const payload = (await response.json()) as { token: string };
	return payload.token;
}

/** Optional hub connection overrides used by the desktop WebView host. */
export interface GameHubOptions {
	/** Overrides the configured hub URL (the desktop host uses a same-origin proxy). */
	hubUrl?: string;
	/** Supplies a token per request; omitted for the desktop proxy, which injects auth. */
	accessTokenFactory?: () => string | Promise<string>;
}

/**
 * Browser SignalR connection to Veldrath.Server.
 *
 * In the web app, tokens are minted by the SvelteKit server on demand so the browser
 * never holds a long-lived credential. In the desktop host, the local DesktopUiServer
 * forwards `/hubs` to the game server and injects the desktop JWT server-side.
 */
export function createGameHub(options: GameHubOptions = {}): HubConnection {
	const hubUrl = options.hubUrl ?? env.PUBLIC_HUB_URL;
	if (!hubUrl) {
		throw new Error('PUBLIC_HUB_URL is not configured.');
	}

	const urlOptions = options.accessTokenFactory
		? { accessTokenFactory: options.accessTokenFactory }
		: options.hubUrl
			? {}
			: { accessTokenFactory: fetchGameToken };

	return new HubConnectionBuilder()
		.withUrl(hubUrl, urlOptions)
		.withAutomaticReconnect()
		.configureLogging(LogLevel.Information)
		.build();
}
