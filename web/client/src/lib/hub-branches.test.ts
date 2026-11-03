import { afterEach, describe, expect, it, vi } from 'vitest';

/**
 * The hub's game-token fetch.
 *
 * `hub.test.ts` covers the two configuration errors. The third way a connection dies before it
 * starts is the SvelteKit token endpoint answering with a non-2xx (an expired session, a server
 * restart). The unnamed version of that is a negotiation "NetworkError", so the failure must name
 * the status.
 */

const env = vi.hoisted(() => ({ PUBLIC_HUB_URL: '' }));

vi.mock('$app/env/public', () => ({
	get PUBLIC_HUB_URL() {
		return env.PUBLIC_HUB_URL;
	}
}));

const { createGameHub } = await import('#lib/hub');

afterEach(() => {
	env.PUBLIC_HUB_URL = '';
	vi.unstubAllGlobals();
	vi.restoreAllMocks();
});

describe('createGameHub token fetch', () => {
	it('names the HTTP status when the token endpoint rejects', async () => {
		env.PUBLIC_HUB_URL = 'https://api.example.com/hubs/game';
		const fetchSpy = vi.fn(async () => new Response('nope', { status: 503 }));
		vi.stubGlobal('fetch', fetchSpy);

		await expect(createGameHub().start()).rejects.toThrow('Game token request failed (503).');
		expect(fetchSpy).toHaveBeenCalled();
	});

	it('returns the token from a healthy token endpoint', async () => {
		env.PUBLIC_HUB_URL = 'https://api.example.com/hubs/game';
		const fetchSpy = vi.fn(async (input: RequestInfo | URL) => {
			const url = String(input);
			if (url.includes('/api/game-token')) {
				return new Response(JSON.stringify({ token: 'game-token-1' }), {
					status: 200,
					headers: { 'content-type': 'application/json' }
				});
			}

			// Negotiation is not the point here; a rejected target still proves the token was read.
			return new Response('not a hub', { status: 404 });
		});
		vi.stubGlobal('fetch', fetchSpy);

		await expect(createGameHub().start()).rejects.toThrow();

		expect(fetchSpy).toHaveBeenCalledWith('/api/game-token', { cache: 'no-store' });
	});
});
