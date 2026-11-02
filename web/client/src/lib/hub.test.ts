import { afterEach, describe, expect, it, vi } from 'vitest';

/**
 * The browser's SignalR connection.
 *
 * Two misconfigurations account for almost every "the game will not connect" report, and both are
 * invisible in the browser: a missing hub URL and an `http://` hub on an `https://` page (the
 * browser blocks it and SignalR only says "Failed to complete negotiation"). The module turns both
 * into named errors, so they are pinned here.
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
});

describe('createGameHub', () => {
	it('names the missing configuration instead of failing in the browser', () => {
		expect(() => createGameHub()).toThrow('PUBLIC_HUB_URL is not configured.');
	});

	it('builds a connection for a configured hub', () => {
		env.PUBLIC_HUB_URL = 'http://127.0.0.1:9000/hubs/game';

		const hub = createGameHub();

		// The connection is real; only the transport is unavailable in jsdom.
		expect(typeof hub.start).toBe('function');
		expect(typeof hub.invoke).toBe('function');
		expect(typeof hub.onreconnecting).toBe('function');
	});

	it('explains an http hub on an https page', () => {
		env.PUBLIC_HUB_URL = 'http://api.example.com/hubs/game';
		vi.stubGlobal('location', { protocol: 'https:' });

		expect(() => createGameHub()).toThrow(/not HTTPS but this page is/);
	});

	it('allows an https hub on an https page', () => {
		env.PUBLIC_HUB_URL = 'https://api.example.com/hubs/game';
		vi.stubGlobal('location', { protocol: 'https:' });

		expect(typeof createGameHub().start).toBe('function');
	});
});
