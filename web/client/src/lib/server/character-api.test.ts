import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fakeUser } from '#lib/testing/event';
import {
	abandonCreationSession,
	beginCreationSession,
	checkCharacterName,
	clearLastSession,
	deleteCharacter,
	fetchCharacterSlots,
	fetchCreationCatalog,
	fetchHardcoreLeaderboard,
	fetchLastSession,
	fetchMemorial,
	finalizeCreationSession,
	getCreationPreview,
	setCreationAttributes,
	setCreationBackground,
	setCreationClass,
	setCreationEquipment,
	setCreationName,
	setCreationSpecies
} from '#lib/server/character-api';

/**
 * The roster and the seven-step creation wizard both go through these helpers. The identity must
 * be derived from the signed-in user (never passed through from the browser) and the creation
 * endpoints must stay under `/api/character-creation/sessions`.
 */

const gameApiFetch = vi.hoisted(() => vi.fn());

vi.mock('#lib/server/game-request', () => ({ gameApiFetch }));

const user = fakeUser({ username: 'tester', email: 'tester@example.com' });

const identity = {
	id: user.id,
	username: 'tester',
	email: 'tester@example.com',
	emailVerified: true
};

beforeEach(() => {
	gameApiFetch.mockResolvedValue({ status: 200, data: {}, error: null });
});

afterEach(() => {
	gameApiFetch.mockReset();
});

/** Last recorded call, padded to three entries so `options` can be asserted as `undefined`. */
const lastCall = () => {
	const call = gameApiFetch.mock.calls.at(-1) as unknown[];
	return [call[0], call[1], call[2]] as [unknown, string, unknown];
};

describe('roster reads', () => {
	it('loads slot usage as the signed-in account', () => {
		fetchCharacterSlots(user);

		expect(lastCall()).toEqual([identity, '/api/characters/slots', undefined]);
	});

	it('loads the memorial', () => {
		fetchMemorial(user);

		expect(lastCall()[1]).toBe('/api/characters/memorial');
	});

	it('loads the hardcore board with default paging', () => {
		fetchHardcoreLeaderboard(user);

		expect(lastCall()[1]).toBe('/api/leaderboard/hardcore?page=1&pageSize=25');
	});

	it('loads the hardcore board with explicit paging', () => {
		fetchHardcoreLeaderboard(user, 3, 10);

		expect(lastCall()[1]).toBe('/api/leaderboard/hardcore?page=3&pageSize=10');
	});

	it('turns a 204 last-session into a null result', async () => {
		gameApiFetch.mockResolvedValue({ status: 204, data: null, error: null });

		await expect(fetchLastSession(user)).resolves.toEqual({
			status: 204,
			data: null,
			error: null
		});
	});

	it('passes a loaded last-session through', async () => {
		const session = { characterId: 'c1', name: 'Tester' };
		gameApiFetch.mockResolvedValue({ status: 200, data: session, error: null });

		await expect(fetchLastSession(user)).resolves.toMatchObject({ data: session });
	});

	it('soft-deletes a character and clears the last session', () => {
		deleteCharacter(user, 'c1');
		expect(lastCall()).toEqual([identity, '/api/characters/c1', { method: 'DELETE' }]);

		clearLastSession(user);
		expect(lastCall()).toEqual([identity, '/api/characters/last-session', { method: 'DELETE' }]);
	});
});

describe('creation catalog', () => {
	it('merges the three catalogs and reports no error', async () => {
		gameApiFetch
			.mockResolvedValueOnce({ status: 200, data: [{ slug: 'warrior' }], error: null })
			.mockResolvedValueOnce({ status: 200, data: [{ slug: 'human' }], error: null })
			.mockResolvedValueOnce({ status: 200, data: [{ slug: 'noble' }], error: null });

		await expect(fetchCreationCatalog(user)).resolves.toEqual({
			catalog: {
				classes: [{ slug: 'warrior' }],
				species: [{ slug: 'human' }],
				backgrounds: [{ slug: 'noble' }]
			},
			error: null
		});
	});

	it('falls back to empty lists and surfaces the first error', async () => {
		gameApiFetch
			.mockResolvedValueOnce({ status: 0, data: null, error: 'api down' })
			.mockResolvedValueOnce({ status: 0, data: null, error: 'api down' })
			.mockResolvedValueOnce({ status: 0, data: null, error: 'api down' });

		const result = await fetchCreationCatalog(user);

		expect(result.error).toBe('api down');
		expect(result.catalog).toEqual({ classes: [], species: [], backgrounds: [] });
	});
});

describe('creation session writes', () => {
	it('begins a session with POST', () => {
		beginCreationSession(user);

		expect(lastCall()).toEqual([identity, '/api/character-creation/sessions', { method: 'POST' }]);
	});

	it('checks a name with a query parameter', () => {
		checkCharacterName(user, 'Aria');

		expect(lastCall()).toEqual([
			identity,
			'/api/character-creation/sessions/check-name',
			{ query: { name: 'Aria' } }
		]);
	});

	it.each([
		['name', setCreationName, 'Aria', { characterName: 'Aria' }],
		['class', setCreationClass, 'warrior', { className: 'warrior' }],
		['species', setCreationSpecies, 'human', { speciesSlug: 'human' }],
		['background', setCreationBackground, 'noble', { backgroundId: 'noble' }]
	])('patches the %s step', (step, setter, value, json) => {
		(setter as (u: AuthUser, id: string, v: string) => unknown)(user, 'sess-1', value);

		expect(lastCall()).toEqual([
			identity,
			`/api/character-creation/sessions/sess-1/${step}`,
			{ method: 'PATCH', json }
		]);
	});

	it('patches attribute allocations and equipment preferences', () => {
		setCreationAttributes(user, 'sess-1', { strength: 2 });
		expect(lastCall()).toEqual([
			identity,
			'/api/character-creation/sessions/sess-1/attributes',
			{ method: 'PATCH', json: { allocations: { strength: 2 } } }
		]);

		const preferences = {
			preferredWeaponType: 'sword',
			preferredArmorType: 'light',
			includeShield: true
		};
		setCreationEquipment(user, 'sess-1', preferences);
		expect(lastCall()).toEqual([
			identity,
			'/api/character-creation/sessions/sess-1/equipment',
			{ method: 'PATCH', json: preferences }
		]);
	});

	it('loads the preview', () => {
		getCreationPreview(user, 'sess-1');

		expect(lastCall()[1]).toBe('/api/character-creation/sessions/sess-1/preview');
	});

	it('finalizes with the difficulty mode', () => {
		finalizeCreationSession(user, 'sess-1', 'Aria', 'hardcore');

		expect(lastCall()).toEqual([
			identity,
			'/api/character-creation/sessions/sess-1/finalize',
			{ method: 'POST', json: { characterName: 'Aria', difficultyMode: 'hardcore' } }
		]);
	});

	it('abandons the session', () => {
		abandonCreationSession(user, 'sess-1');

		expect(lastCall()).toEqual([
			identity,
			'/api/character-creation/sessions/sess-1',
			{ method: 'DELETE' }
		]);
	});
});
