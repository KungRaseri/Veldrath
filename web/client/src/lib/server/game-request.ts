import { env } from '$env/dynamic/private';
import { mintGameToken } from '$lib/server/game-token';

/** Minimal identity needed to mint a Veldrath.Server game token. */
export interface GameIdentity {
	id: string;
	username: string;
	email?: string | null;
	emailVerified?: boolean;
}

/** Result of a server-to-server Veldrath API call. */
export interface GameApiResult<T> {
	status: number;
	data: T | null;
	error: string | null;
}

/** Options for {@link gameApiFetch}. */
export interface GameRequestOptions {
	method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
	json?: unknown;
	query?: Record<string, string | number | boolean | null | undefined>;
	timeoutMs?: number;
}

/** Resolves the server-to-server Veldrath API base URL, or `null` when unconfigured. */
export function gameApiBaseUrl(): string | null {
	const base = env.INTERNAL_BRIDGE_URL ?? env.PUBLIC_API_URL;
	return base ? base.replace(/\/+$/, '') : null;
}

function extractError(body: string, status: number): string {
	if (body) {
		try {
			const parsed = JSON.parse(body) as { error?: string; title?: string; detail?: string };
			const message = parsed.error ?? parsed.detail ?? parsed.title;
			if (message) return message;
		} catch {
			// Fall through to the raw body.
		}
		if (body.length <= 300) return body;
	}

	return `Request failed (${status}).`;
}

/**
 * Calls the Veldrath .NET API from a SvelteKit server load or action.
 *
 * Pass `null` as the identity for anonymous endpoints (content browsing, public
 * editorial reads). Authenticated calls mint a short-lived game JWT, which the
 * server enriches with the caller's RBAC role and permission claims.
 */
export async function gameApiFetch<T>(
	identity: GameIdentity | null,
	path: string,
	options: GameRequestOptions = {}
): Promise<GameApiResult<T>> {
	const base = gameApiBaseUrl();
	if (!base) {
		return { status: 0, data: null, error: 'The game server URL is not configured.' };
	}

	const url = new URL(`${base}${path}`);
	for (const [key, value] of Object.entries(options.query ?? {})) {
		if (value !== null && value !== undefined && value !== '') {
			url.searchParams.set(key, String(value));
		}
	}

	const headers: Record<string, string> = { accept: 'application/json' };
	if (options.json !== undefined) headers['content-type'] = 'application/json';

	if (identity) {
		try {
			headers.authorization = `Bearer ${await mintGameToken(
				identity.id,
				identity.username,
				identity.email ?? undefined,
				identity.emailVerified ?? false
			)}`;
		} catch (error) {
			console.warn('[game-request] could not mint a game token.', error);
			return { status: 0, data: null, error: 'Could not mint a game token.' };
		}
	}

	try {
		const response = await fetch(url, {
			method: options.method ?? (options.json === undefined ? 'GET' : 'POST'),
			headers,
			body: options.json === undefined ? undefined : JSON.stringify(options.json),
			signal: AbortSignal.timeout(options.timeoutMs ?? 8000)
		});

		if (response.status === 204) {
			return { status: 204, data: null, error: null };
		}

		const text = await response.text();
		const isJson = (response.headers.get('content-type') ?? '').includes('json');
		let data: T | null = null;
		if (text && isJson) {
			try {
				data = JSON.parse(text) as T;
			} catch {
				return {
					status: response.status,
					data: null,
					error: 'The game server returned invalid JSON.'
				};
			}
		}

		if (!response.ok) {
			return { status: response.status, data: null, error: extractError(text, response.status) };
		}

		return { status: response.status, data, error: null };
	} catch (error) {
		console.warn(`[game-request] ${path} failed.`, error);
		return { status: 0, data: null, error: 'Could not reach the game server.' };
	}
}
