import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fakeUser, makeServerEvent, type FakeServerEvent } from '#lib/testing/event';
import { GET as gameToken } from './api/game-token/+server';
import { POST as beginCreation } from './api/characters/creation/+server';
import { DELETE as abandonCreation } from './api/characters/creation/[id]/+server';
import { GET as checkName } from './api/characters/creation/check-name/+server';
import {
	GET as creationPreview,
	PATCH as creationStep,
	POST as creationFinalize
} from './api/characters/creation/[id]/[step]/+server';
import * as authRoute from './api/auth/[...all]/+server';

/**
 * The `api/**` route handlers.
 *
 * These are the proxy between the browser and Veldrath.Server, so what matters is the contract, not
 * the transport: which hub/API call each verb makes, which field it validates first, and which
 * status code leaves the app. `gameResultResponse` and `requireUser` are exercised for real (they
 * are covered on their own as well) so a status mapping or a guard regression fails here too.
 */

const characterApi = vi.hoisted(() => ({
	beginCreationSession: vi.fn(),
	abandonCreationSession: vi.fn(),
	checkCharacterName: vi.fn(),
	setCreationName: vi.fn(),
	setCreationClass: vi.fn(),
	setCreationSpecies: vi.fn(),
	setCreationBackground: vi.fn(),
	setCreationAttributes: vi.fn(),
	setCreationEquipment: vi.fn(),
	getCreationPreview: vi.fn(),
	finalizeCreationSession: vi.fn()
}));

const tokens = vi.hoisted(() => ({
	GAME_TOKEN_TTL_SECONDS: 600,
	mintGameToken: vi.fn()
}));

const game = vi.hoisted(() => ({ gameApiFetch: vi.fn() }));

const auth = vi.hoisted(() => ({ getAuth: vi.fn() }));

vi.mock('#lib/server/character-api', () => characterApi);
vi.mock('#lib/server/game-token', () => tokens);
vi.mock('#lib/server/game-request', () => ({ gameApiFetch: game.gameApiFetch }));
vi.mock('#lib/server/auth', () => auth);

/** Calls a `RequestHandler`, keeping the route-specific event type out of the test bodies. */
function call(handler: unknown, event: FakeServerEvent): Promise<Response> {
	return (handler as (event: FakeServerEvent) => Promise<Response>)(event);
}

/** A successful upstream result with the given payload. */
function ok<T>(data: T, status = 200) {
	return { status, data, error: null };
}

const signedIn = (overrides: Parameters<typeof makeServerEvent>[0] = {}) =>
	makeServerEvent({ user: fakeUser(), ...overrides });

beforeEach(() => {
	characterApi.beginCreationSession.mockResolvedValue(ok({ sessionId: 'session-1' }));
	characterApi.abandonCreationSession.mockResolvedValue(ok(null, 204));
	characterApi.checkCharacterName.mockResolvedValue(ok({ isAvailable: true }));
	characterApi.setCreationName.mockResolvedValue(ok({ ok: true }));
	characterApi.setCreationClass.mockResolvedValue(ok({ ok: true }));
	characterApi.setCreationSpecies.mockResolvedValue(ok({ ok: true }));
	characterApi.setCreationBackground.mockResolvedValue(ok({ ok: true }));
	characterApi.setCreationAttributes.mockResolvedValue(ok({ ok: true }));
	characterApi.setCreationEquipment.mockResolvedValue(ok({ ok: true }));
	characterApi.getCreationPreview.mockResolvedValue(ok({ name: 'Kaleth' }));
	characterApi.finalizeCreationSession.mockResolvedValue(ok({ characterId: 'char-1' }));
	tokens.mintGameToken.mockResolvedValue('signed.token.value');
	game.gameApiFetch.mockResolvedValue(ok({ accountId: fakeUser().id }));
	auth.getAuth.mockReturnValue({ handler: vi.fn(async () => new Response('ok')) });
});

describe('game token endpoint', () => {
	it('mints a token for the signed-in account and forbids caching it', async () => {
		const response = await call(gameToken, signedIn());

		expect(response.status).toBe(200);
		expect(response.headers.get('cache-control')).toBe('no-store');
		expect(await response.json()).toEqual({ token: 'signed.token.value', expiresIn: 600 });
		expect(game.gameApiFetch).toHaveBeenCalledWith(
			expect.objectContaining({ id: fakeUser().id }),
			'/api/account/profile'
		);
		expect(tokens.mintGameToken).toHaveBeenCalledWith(
			fakeUser().id,
			'player-one',
			'player@example.com',
			true
		);
	});

	it('falls back to the display name when the account has no username', async () => {
		await call(gameToken, makeServerEvent({ user: fakeUser({ username: null }) }));

		expect(tokens.mintGameToken).toHaveBeenCalledWith(
			fakeUser().id,
			'Player One',
			'player@example.com',
			true
		);
	});

	it('sends an anonymous caller to the login page', async () => {
		await expect(call(gameToken, makeServerEvent())).rejects.toThrow();

		expect(tokens.mintGameToken).not.toHaveBeenCalled();
	});

	it('does not return a bearer when game access is denied', async () => {
		game.gameApiFetch.mockResolvedValue({ status: 401, data: null, error: 'Unauthorized' });

		await expect(call(gameToken, signedIn())).rejects.toMatchObject({
			status: 403,
			body: { message: 'Game access is not available for this account.' }
		});
		expect(tokens.mintGameToken).not.toHaveBeenCalled();
	});

	it('does not return a bearer when the game server cannot be reached', async () => {
		game.gameApiFetch.mockResolvedValue({
			status: 0,
			data: null,
			error: 'Could not reach the game server.'
		});

		await expect(call(gameToken, signedIn())).rejects.toMatchObject({ status: 503 });
		expect(tokens.mintGameToken).not.toHaveBeenCalled();
	});
});

describe('creation session endpoints', () => {
	it('starts a session for the signed-in account', async () => {
		const response = await call(beginCreation, signedIn());

		expect(response.status).toBe(200);
		expect(await response.json()).toEqual({ sessionId: 'session-1' });
		expect(characterApi.beginCreationSession).toHaveBeenCalledWith(
			expect.objectContaining({ id: fakeUser().id })
		);
	});

	it('passes an upstream client error through with its message', async () => {
		characterApi.beginCreationSession.mockResolvedValue({
			status: 409,
			data: null,
			error: 'You already have a character in creation.'
		});

		const response = await call(beginCreation, signedIn());

		expect(response.status).toBe(409);
		expect(await response.json()).toEqual({ error: 'You already have a character in creation.' });
	});

	it('abandons the session named in the path', async () => {
		const response = await call(abandonCreation, signedIn({ params: { id: 'session-9' } }));

		expect(response.status).toBe(204);
		expect(characterApi.abandonCreationSession).toHaveBeenCalledWith(
			expect.objectContaining({ id: fakeUser().id }),
			'session-9'
		);
	});

	it('checks the name from the query string, defaulting to empty', async () => {
		await call(checkName, signedIn({ url: '/api/characters/creation/check-name?name=Kaleth' }));
		expect(characterApi.checkCharacterName).toHaveBeenCalledWith(expect.anything(), 'Kaleth');

		await call(checkName, signedIn());
		expect(characterApi.checkCharacterName).toHaveBeenLastCalledWith(expect.anything(), '');
	});
});

describe('creation step PATCH', () => {
	const step = (name: string, options: Parameters<typeof makeServerEvent>[0] = {}) =>
		signedIn({ method: 'PATCH', params: { id: 'session-1', step: name }, ...options });

	it('rejects a step that does not exist', async () => {
		const response = await call(creationStep, step('hair-colour', { body: {} }));

		expect(response.status).toBe(404);
		expect(await response.json()).toEqual({ error: 'Unknown creation step.' });
	});

	it('rejects a missing or non-object body', async () => {
		const missing = await call(creationStep, step('name'));
		expect(missing.status).toBe(400);
		expect(await missing.json()).toEqual({ error: 'A JSON request body is required.' });

		const array = await call(creationStep, step('name', { body: [1, 2, 3] }));
		expect(array.status).toBe(400);
	});

	it.each([
		['name', 'characterName', characterApi.setCreationName, 'Kaleth'],
		['class', 'className', characterApi.setCreationClass, 'Warrior'],
		['species', 'speciesSlug', characterApi.setCreationSpecies, 'human'],
		['background', 'backgroundId', characterApi.setCreationBackground, 'bg-1']
	])('records the %s choice', async (route, field, setter, value) => {
		const accepted = await call(creationStep, step(route, { body: { [field]: value } }));
		expect(accepted.status).toBe(200);
		expect(setter).toHaveBeenCalledWith(expect.anything(), 'session-1', value);

		setter.mockClear();
		const rejected = await call(creationStep, step(route, { body: {} }));
		expect(rejected.status).toBe(400);
		expect(await rejected.json()).toEqual({ error: `${field} is required.` });
		expect(setter).not.toHaveBeenCalled();
	});

	it('keeps only the integer allocations', async () => {
		const response = await call(
			creationStep,
			step('attributes', {
				body: { allocations: { strength: 2, luck: 'lots', fate: 1.5, agility: 1 } }
			})
		);

		expect(response.status).toBe(200);
		expect(characterApi.setCreationAttributes).toHaveBeenCalledWith(
			expect.anything(),
			'session-1',
			{ strength: 2, agility: 1 }
		);
	});

	it('rejects an allocation object with nothing usable in it', async () => {
		const absent = await call(creationStep, step('attributes', { body: {} }));
		expect(absent.status).toBe(400);
		expect(await absent.json()).toEqual({ error: 'allocations is required.' });

		const empty = await call(creationStep, step('attributes', { body: { allocations: {} } }));
		expect(empty.status).toBe(400);
		expect(await empty.json()).toEqual({ error: 'allocations must contain at least one stat.' });

		const wrongTypes = await call(
			creationStep,
			step('attributes', { body: { allocations: { strength: 'two' } } })
		);
		expect(wrongTypes.status).toBe(400);
		expect(characterApi.setCreationAttributes).not.toHaveBeenCalled();
	});

	it('records equipment preferences, normalising empty strings to null', async () => {
		await call(creationStep, step('equipment', { body: {} }));

		expect(characterApi.setCreationEquipment).toHaveBeenCalledWith(expect.anything(), 'session-1', {
			preferredArmorType: null,
			preferredWeaponType: null,
			includeShield: false
		});

		await call(
			creationStep,
			step('equipment', {
				body: {
					preferredArmorType: 'heavy',
					preferredWeaponType: '',
					includeShield: true
				}
			})
		);

		expect(characterApi.setCreationEquipment).toHaveBeenLastCalledWith(
			expect.anything(),
			'session-1',
			{ preferredArmorType: 'heavy', preferredWeaponType: null, includeShield: true }
		);
	});

	it('lets an upstream validation failure choose the status', async () => {
		characterApi.setCreationName.mockResolvedValue({
			status: 422,
			data: null,
			error: 'That name is taken.'
		});

		const response = await call(creationStep, step('name', { body: { characterName: 'Kaleth' } }));

		expect(response.status).toBe(422);
		expect(await response.json()).toEqual({ error: 'That name is taken.' });
	});
});

describe('creation preview and finalize', () => {
	it('previews only on the preview step', async () => {
		const wrong = await call(creationPreview, signedIn({ params: { id: 's1', step: 'name' } }));
		expect(wrong.status).toBe(404);

		const response = await call(
			creationPreview,
			signedIn({ params: { id: 's1', step: 'preview' } })
		);
		expect(await response.json()).toEqual({ name: 'Kaleth' });
		expect(characterApi.getCreationPreview).toHaveBeenCalledWith(expect.anything(), 's1');
	});

	it('finalizes only on the finalize step', async () => {
		const wrong = await call(
			creationFinalize,
			signedIn({ params: { id: 's1', step: 'preview' }, body: { characterName: 'Kaleth' } })
		);
		expect(wrong.status).toBe(404);

		const response = await call(
			creationFinalize,
			signedIn({ params: { id: 's1', step: 'finalize' }, body: { characterName: 'Kaleth' } })
		);
		expect(await response.json()).toEqual({ characterId: 'char-1' });
		expect(characterApi.finalizeCreationSession).toHaveBeenCalledWith(
			expect.anything(),
			's1',
			'Kaleth',
			'normal'
		);
	});

	it('requires a non-blank name and trims the one it accepts', async () => {
		const blank = await call(
			creationFinalize,
			signedIn({ params: { id: 's1', step: 'finalize' }, body: { characterName: '   ' } })
		);
		expect(blank.status).toBe(400);
		expect(await blank.json()).toEqual({ error: 'characterName is required.' });

		const missing = await call(
			creationFinalize,
			signedIn({ params: { id: 's1', step: 'finalize' } })
		);
		expect(missing.status).toBe(400);

		await call(
			creationFinalize,
			signedIn({
				params: { id: 's1', step: 'finalize' },
				body: { characterName: '  Kaleth  ', difficultyMode: 'hardcore' }
			})
		);
		expect(characterApi.finalizeCreationSession).toHaveBeenLastCalledWith(
			expect.anything(),
			's1',
			'Kaleth',
			'hardcore'
		);
	});
});

describe('better auth catch-all', () => {
	it.each([
		['GET', authRoute.GET],
		['POST', authRoute.POST],
		['PUT', authRoute.PUT],
		['PATCH', authRoute.PATCH],
		['DELETE', authRoute.DELETE]
	])('forwards %s to the Better Auth handler', async (method, handler) => {
		const event = makeServerEvent({ method, url: '/api/auth/sign-in/email' });
		const response = await call(handler, event);

		const betterAuth = auth.getAuth();
		expect(betterAuth.handler).toHaveBeenCalledWith(event.request);
		expect(await response.text()).toBe('ok');
	});
});
