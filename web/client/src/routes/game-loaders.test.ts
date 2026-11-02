import { beforeEach, describe, expect, it, vi } from 'vitest';
import { callLoad, fakeUser, makeServerEvent } from '#lib/testing/event';
import { actions, load as charactersLoad } from './characters/+page.server';
import { load as gameLoad } from './game/+page.server';
import { load as playLoad } from './play/[id]/+page.server';

/**
 * Roster and play-route guards.
 *
 * `/characters` is the first authenticated page: it fans out to four .NET calls and has to render
 * partial data when one of them fails. `/play/[id]` trusts nothing from the URL, and the delete
 * action must not delete anything without an id.
 */

const gameApi = vi.hoisted(() => ({ fetchCharactersForUser: vi.fn() }));
const characterApi = vi.hoisted(() => ({
	deleteCharacter: vi.fn(),
	fetchCharacterSlots: vi.fn(),
	fetchLastSession: vi.fn(),
	fetchMemorial: vi.fn()
}));

vi.mock('#lib/server/game-api', () => gameApi);
vi.mock('#lib/server/character-api', () => characterApi);

const user = fakeUser();
const characterId = '11111111-1111-4111-8111-111111111111';
const data = (value: unknown) => value as Record<string, unknown>;

beforeEach(() => {
	gameApi.fetchCharactersForUser.mockResolvedValue({ characters: [], error: null });
	characterApi.fetchCharacterSlots.mockResolvedValue({
		status: 200,
		data: { used: 1, max: 5, remaining: 4 },
		error: null
	});
	characterApi.fetchLastSession.mockResolvedValue({ status: 204, data: null, error: null });
	characterApi.fetchMemorial.mockResolvedValue({ status: 200, data: null, error: null });
	characterApi.deleteCharacter.mockResolvedValue({ status: 204, data: null, error: null });
});

describe('game index', () => {
	it('redirects to the roster', () => {
		// The load is synchronous: it throws the redirect before returning anything.
		expect(() => callLoad(gameLoad, makeServerEvent())).toThrow();
	});
});

describe('play route', () => {
	it('redirects anonymous visitors to login', async () => {
		await expect(
			callLoad(playLoad, makeServerEvent({ params: { id: characterId } }))
		).rejects.toMatchObject({ status: 303, location: '/login' });
	});

	it('sends a malformed character id back to the roster', async () => {
		await expect(
			callLoad(playLoad, makeServerEvent({ params: { id: 'not-a-guid' }, user }))
		).rejects.toMatchObject({ status: 303, location: '/characters' });
	});

	it('passes the character id to the shell', async () => {
		await expect(
			callLoad(playLoad, makeServerEvent({ params: { id: characterId }, user }))
		).resolves.toEqual({ characterId });
	});
});

describe('characters roster', () => {
	it('redirects anonymous visitors', async () => {
		await expect(callLoad(charactersLoad, makeServerEvent())).rejects.toMatchObject({
			status: 303,
			location: '/login'
		});
	});

	it('merges the four API results', async () => {
		gameApi.fetchCharactersForUser.mockResolvedValue({
			characters: [{ id: characterId, name: 'Thorn' }],
			error: null
		});
		characterApi.fetchMemorial.mockResolvedValue({
			status: 200,
			data: [{ id: 'mem-1', name: 'Fallen' }],
			error: null
		});

		const result = data(await callLoad(charactersLoad, makeServerEvent({ user })));

		expect(result.characters).toEqual([{ id: characterId, name: 'Thorn' }]);
		expect(result.memorial).toEqual([{ id: 'mem-1', name: 'Fallen' }]);
		expect(result.slots).toEqual({ used: 1, max: 5, remaining: 4 });
		expect(result.emailVerified).toBe(true);
	});

	it('defaults the memorial to an empty list and passes each error through', async () => {
		gameApi.fetchCharactersForUser.mockResolvedValue({ characters: [], error: 'roster down' });
		characterApi.fetchCharacterSlots.mockResolvedValue({
			status: 0,
			data: null,
			error: 'slots down'
		});
		characterApi.fetchLastSession.mockResolvedValue({
			status: 0,
			data: null,
			error: 'session down'
		});
		characterApi.fetchMemorial.mockResolvedValue({ status: 0, data: null, error: 'memorial down' });

		const result = data(await callLoad(charactersLoad, makeServerEvent({ user })));

		expect(result.memorial).toEqual([]);
		expect(result.loadError).toBe('roster down');
		expect(result.slotsError).toBe('slots down');
		expect(result.lastSessionError).toBe('session down');
		expect(result.memorialError).toBe('memorial down');
	});
});

describe('characters delete action', () => {
	const submit = (form: Record<string, string>) =>
		callLoad(actions.delete, makeServerEvent({ user, form })) as Promise<unknown>;

	it('redirects anonymous visitors', async () => {
		await expect(
			callLoad(actions.delete, makeServerEvent({ form: { id: characterId } }))
		).rejects.toMatchObject({ status: 303, location: '/login' });
	});

	it('refuses an empty id', async () => {
		const failure = (await submit({ id: '   ' })) as { status: number; data: { error: string } };

		expect(failure.status).toBe(400);
		expect(failure.data.error).toBe('Character ID is required.');
		expect(characterApi.deleteCharacter).not.toHaveBeenCalled();
	});

	it('keeps a client-error status from the API', async () => {
		characterApi.deleteCharacter.mockResolvedValue({
			status: 403,
			data: null,
			error: 'Cannot delete the active character.'
		});

		const failure = (await submit({ id: characterId })) as {
			status: number;
			data: { error: string };
		};

		expect(failure.status).toBe(403);
		expect(failure.data.error).toBe('Cannot delete the active character.');
	});

	it('maps a transport failure to 400', async () => {
		characterApi.deleteCharacter.mockResolvedValue({
			status: 0,
			data: null,
			error: 'Could not reach the game server.'
		});

		const failure = (await submit({ id: characterId })) as { status: number };

		expect(failure.status).toBe(400);
	});

	it('redirects back to the roster on success', async () => {
		await expect(submit({ id: characterId })).rejects.toMatchObject({
			status: 303,
			location: '/characters'
		});
		expect(characterApi.deleteCharacter).toHaveBeenCalledWith(user, characterId);
	});
});
