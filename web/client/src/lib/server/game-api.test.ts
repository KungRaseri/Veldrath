import { afterEach, describe, expect, it, vi } from 'vitest';
import { fakeUser } from '#lib/testing/event';
import { fetchCharactersForUser } from '#lib/server/game-api';

/**
 * The roster load is the first authenticated .NET call a player sees. Its failure copy is what
 * tells an operator whether the API rejected the token or never answered at all.
 */

const gameApiFetch = vi.hoisted(() => vi.fn());

vi.mock('#lib/server/game-request', () => ({ gameApiFetch }));

afterEach(() => {
	gameApiFetch.mockReset();
});

describe('fetchCharactersForUser', () => {
	it('returns the roster from the characters endpoint', async () => {
		const characters = [{ id: 'c1', name: 'Tester', level: 3, className: 'Warrior' }];
		gameApiFetch.mockResolvedValue({ status: 200, data: characters, error: null });

		await expect(fetchCharactersForUser(fakeUser())).resolves.toEqual({ characters, error: null });
		expect(gameApiFetch.mock.calls[0][1]).toBe('/api/characters');
		expect(gameApiFetch.mock.calls[0][2]).toMatchObject({ timeoutMs: 5000 });
	});

	it('names the status when the API answered with an error', async () => {
		gameApiFetch.mockResolvedValue({ status: 403, data: null, error: 'forbidden' });

		await expect(fetchCharactersForUser(fakeUser())).resolves.toEqual({
			characters: [],
			error: 'Character list request failed (403).'
		});
	});

	it('passes the transport error through when the API was never reached', async () => {
		gameApiFetch.mockResolvedValue({
			status: 0,
			data: null,
			error: 'Could not mint a game token.'
		});

		await expect(fetchCharactersForUser(fakeUser())).resolves.toEqual({
			characters: [],
			error: 'Could not mint a game token.'
		});
	});

	it('falls back to the generic message for an empty transport error', async () => {
		gameApiFetch.mockResolvedValue({ status: 0, data: null, error: null });

		await expect(fetchCharactersForUser(fakeUser())).resolves.toEqual({
			characters: [],
			error: 'Could not reach the game server.'
		});
	});
});
