import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { installFetch, jsonResponse, textResponse, unauthorizedResponse } from '#lib/testing/fetch';
import { withEnv } from '#lib/testing/env';
import { describeAuthFailure, gameApiBaseUrl, gameApiFetch } from '#lib/server/game-request';

/**
 * `gameApiFetch` is the single transport between SvelteKit and Veldrath.Server, so every server
 * page depends on its error mapping. These cases pin the contract: base URL resolution, query
 * building, token minting, JSON parsing, and the message a page ends up showing the player.
 */

const mintGameToken = vi.hoisted(() => vi.fn(async () => 'minted-token'));

vi.mock('#lib/server/game-token', () => ({ mintGameToken }));

const identity = { id: 'user-1', username: 'tester', email: 'tester@example.com' };

const restores: Array<() => void> = [];

afterEach(() => {
	for (const restore of restores.splice(0)) restore();
	vi.restoreAllMocks();
});

beforeEach(() => {
	mintGameToken.mockClear();
	mintGameToken.mockResolvedValue('minted-token');
});

describe('gameApiBaseUrl', () => {
	it('is null when neither bridge nor public URL is configured', () => {
		restores.push(withEnv({ INTERNAL_BRIDGE_URL: undefined, PUBLIC_API_URL: undefined }));

		expect(gameApiBaseUrl()).toBeNull();
	});

	it('prefers the internal bridge and strips trailing slashes', () => {
		restores.push(
			withEnv({
				INTERNAL_BRIDGE_URL: 'http://bridge:8080/',
				PUBLIC_API_URL: 'https://api.example.com'
			})
		);

		expect(gameApiBaseUrl()).toBe('http://bridge:8080');
	});

	it('falls back to the public API URL', () => {
		restores.push(
			withEnv({ INTERNAL_BRIDGE_URL: undefined, PUBLIC_API_URL: 'https://api.example.com/' })
		);

		expect(gameApiBaseUrl()).toBe('https://api.example.com');
	});
});

describe('describeAuthFailure', () => {
	it('extracts the error description from the challenge header', () => {
		const response = unauthorizedResponse('The signature key was not found');

		expect(describeAuthFailure(response)).toBe('The signature key was not found');
	});

	it('falls back to the bare error code', () => {
		const response = new Response(null, {
			status: 401,
			headers: { 'www-authenticate': 'Bearer error="invalid_token"' }
		});

		expect(describeAuthFailure(response)).toBe('invalid_token');
	});

	it('returns null when the header is absent', () => {
		expect(describeAuthFailure(new Response(null, { status: 401 }))).toBeNull();
	});

	it('falls back to the raw header when it carries no error code', () => {
		const response = new Response(null, {
			status: 401,
			headers: { 'www-authenticate': 'Bearer realm="veldrath"' }
		});

		expect(describeAuthFailure(response)).toBe('Bearer realm="veldrath"');
	});
});

describe('gameApiFetch', () => {
	it('reports a configuration problem without calling fetch', async () => {
		restores.push(withEnv({ INTERNAL_BRIDGE_URL: undefined, PUBLIC_API_URL: undefined }));
		const stub = installFetch(() => jsonResponse({}));
		restores.push(stub.restore);

		const result = await gameApiFetch(null, '/api/content/schema');

		expect(result.status).toBe(0);
		expect(result.error).toBe('The game server URL is not configured.');
		expect(stub.calls).toEqual([]);
	});

	it('sends an anonymous GET with query parameters and no authorization header', async () => {
		restores.push(withEnv({ INTERNAL_BRIDGE_URL: 'http://bridge' }));
		const stub = installFetch(() => jsonResponse({ ok: true }));
		restores.push(stub.restore);

		const result = await gameApiFetch<{ ok: boolean }>(null, '/api/editorial/lore', {
			query: { page: 2, category: 'history', empty: '', missing: null }
		});

		expect(result).toEqual({ status: 200, data: { ok: true }, error: null });
		const [call] = stub.calls;
		expect(call.url).toBe('http://bridge/api/editorial/lore?page=2&category=history');
		expect(call.method).toBe('GET');
		expect(call.headers.authorization).toBeUndefined();
		expect(call.headers.accept).toBe('application/json');
		expect(mintGameToken).not.toHaveBeenCalled();
	});

	it('mints a bearer token and posts JSON when an identity is supplied', async () => {
		restores.push(withEnv({ INTERNAL_BRIDGE_URL: 'http://bridge' }));
		const stub = installFetch(() => jsonResponse({ ok: true }, { status: 201 }));
		restores.push(stub.restore);

		await gameApiFetch(identity, '/api/characters', { json: { name: 'Tester' } });

		expect(mintGameToken).toHaveBeenCalledWith('user-1', 'tester', 'tester@example.com', false);
		const [call] = stub.calls;
		expect(call.method).toBe('POST');
		expect(call.headers.authorization).toBe('Bearer minted-token');
		expect(call.headers['content-type']).toBe('application/json');
		expect(call.body).toBe(JSON.stringify({ name: 'Tester' }));
	});

	it('respects an explicit method and a missing email', async () => {
		restores.push(withEnv({ INTERNAL_BRIDGE_URL: 'http://bridge' }));
		const stub = installFetch(() => new Response(null, { status: 204 }));
		restores.push(stub.restore);

		const result = await gameApiFetch(
			{ id: 'user-2', username: 'no-mail' },
			'/api/characters/last-session',
			{ method: 'DELETE' }
		);

		expect(result).toEqual({ status: 204, data: null, error: null });
		expect(mintGameToken).toHaveBeenCalledWith('user-2', 'no-mail', undefined, false);
		expect(stub.calls[0].method).toBe('DELETE');
	});

	it('reports a token minting failure as an unreachable call', async () => {
		restores.push(withEnv({ INTERNAL_BRIDGE_URL: 'http://bridge' }));
		mintGameToken.mockRejectedValueOnce(new Error('JWT_KEY is not configured.'));
		const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
		const stub = installFetch(() => jsonResponse({}));
		restores.push(stub.restore);

		const result = await gameApiFetch(identity, '/api/characters');

		expect(result).toEqual({ status: 0, data: null, error: 'Could not mint a game token.' });
		expect(warn).toHaveBeenCalled();
		expect(stub.calls).toEqual([]);
	});

	it('returns null data for a successful non-JSON response', async () => {
		restores.push(withEnv({ INTERNAL_BRIDGE_URL: 'http://bridge' }));
		const stub = installFetch(() => textResponse('plain'));
		restores.push(stub.restore);

		const result = await gameApiFetch(null, '/api/health');

		expect(result).toEqual({ status: 200, data: null, error: null });
	});

	it('reports invalid JSON from the API', async () => {
		restores.push(withEnv({ INTERNAL_BRIDGE_URL: 'http://bridge' }));
		const stub = installFetch(() =>
			textResponse('{ not json', { headers: { 'content-type': 'application/json' } })
		);
		restores.push(stub.restore);

		const result = await gameApiFetch(null, '/api/content/schema');

		expect(result.error).toBe('The game server returned invalid JSON.');
	});

	it.each([
		['{"error":"slug is taken"}', 400, 'slug is taken'],
		['{"detail":"conflict"}', 409, 'conflict'],
		['{"title":"Validation failed"}', 422, 'Validation failed'],
		['short failure', 500, 'short failure']
	])('surfaces a %s body as the error message', async (body, status, expected) => {
		restores.push(withEnv({ INTERNAL_BRIDGE_URL: 'http://bridge' }));
		const stub = installFetch(() => textResponse(body, { status }));
		restores.push(stub.restore);
		vi.spyOn(console, 'error').mockImplementation(() => {});

		const result = await gameApiFetch(null, '/api/characters');

		expect(result.status).toBe(status);
		expect(result.data).toBeNull();
		expect(result.error).toBe(expected);
	});

	it('falls back to the status when the body is empty or very long', async () => {
		restores.push(withEnv({ INTERNAL_BRIDGE_URL: 'http://bridge' }));
		let count = 0;
		const stub = installFetch(() => {
			count += 1;
			return count === 1
				? textResponse('', { status: 502 })
				: textResponse('x'.repeat(400), { status: 503 });
		});
		restores.push(stub.restore);
		vi.spyOn(console, 'error').mockImplementation(() => {});

		const empty = await gameApiFetch(null, '/api/characters');
		const long = await gameApiFetch(null, '/api/characters');

		expect(empty.error).toBe('Request failed (502).');
		expect(long.error).toBe('Request failed (503).');
	});

	it('logs the challenge reason on a 401 and still returns the body error', async () => {
		restores.push(withEnv({ INTERNAL_BRIDGE_URL: 'http://bridge' }));
		const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
		const stub = installFetch(() => unauthorizedResponse('IDX10214: Audience validation failed'));
		restores.push(stub.restore);

		const result = await gameApiFetch(identity, '/api/characters');

		expect(result.status).toBe(401);
		expect(warn.mock.calls.flat().join(' ')).toContain('IDX10214');
	});

	it('logs a 401 with no challenge detail without a suffix', async () => {
		restores.push(withEnv({ INTERNAL_BRIDGE_URL: 'http://bridge' }));
		const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
		const stub = installFetch(() => new Response(null, { status: 401 }));
		restores.push(stub.restore);

		await gameApiFetch(identity, '/api/characters');

		const message = warn.mock.calls.flat().join(' ');
		expect(message).toContain('responded 401');
		expect(message).not.toContain('401:');
	});

	it('logs the body of a 5xx response for the server stack trace', async () => {
		restores.push(withEnv({ INTERNAL_BRIDGE_URL: 'http://bridge' }));
		const error = vi.spyOn(console, 'error').mockImplementation(() => {});
		const stub = installFetch(() => textResponse('boom', { status: 500 }));
		restores.push(stub.restore);

		await gameApiFetch(null, '/api/content/schema');

		expect(error.mock.calls.flat().join(' ')).toContain('boom');
	});

	it('maps a transport failure to status 0', async () => {
		restores.push(withEnv({ INTERNAL_BRIDGE_URL: 'http://bridge' }));
		const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
		const stub = installFetch(() => {
			throw new Error('ECONNREFUSED');
		});
		restores.push(stub.restore);

		const result = await gameApiFetch(null, '/api/content/schema');

		expect(result).toEqual({ status: 0, data: null, error: 'Could not reach the game server.' });
		expect(warn).toHaveBeenCalled();
	});
});
