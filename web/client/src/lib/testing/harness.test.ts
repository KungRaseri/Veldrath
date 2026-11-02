import { afterEach, describe, expect, it } from 'vitest';
import { fakeUser, makeServerEvent } from '#lib/testing/event';
import { installFetch, jsonResponse } from '#lib/testing/fetch';
import { withEnv } from '#lib/testing/env';
import { repoRoot, stripComments, walkFiles } from '#lib/testing/source';

/**
 * The harness is test-only code, so it is excluded from coverage and has no product tests of
 * its own. These cases exist so a broken helper fails loudly here instead of producing
 * confusing failures in every route test that depends on it.
 */

const restores: Array<() => void> = [];

afterEach(() => {
	for (const restore of restores.splice(0)) restore();
});

describe('fake server events', () => {
	it('builds an anonymous GET event by default', () => {
		const event = makeServerEvent();

		expect(event.url.pathname).toBe('/');
		expect(event.request.method).toBe('GET');
		expect(event.locals.user).toBeUndefined();
		expect(event.params).toEqual({});
	});

	it('attaches a user, a session, params, and a JSON body', async () => {
		const user = fakeUser({ username: 'tester' });
		const event = makeServerEvent({
			url: '/characters/new?step=2',
			user,
			params: { id: 'abc' },
			body: { name: 'Tester' }
		});

		expect(event.locals.user?.username).toBe('tester');
		expect(event.locals.session?.userId).toBe(user.id);
		expect(event.params.id).toBe('abc');
		expect(event.url.searchParams.get('step')).toBe('2');
		expect(event.request.method).toBe('POST');
		await expect(event.request.json()).resolves.toEqual({ name: 'Tester' });
	});
});

describe('fetch stub', () => {
	it('records the call and returns the handler response', async () => {
		const stub = installFetch(() => jsonResponse({ ok: true }, { status: 201 }));
		restores.push(stub.restore);

		const response = await fetch('http://localhost:9000/api/characters', {
			method: 'POST',
			headers: { 'content-type': 'application/json' },
			body: JSON.stringify({ name: 'Tester' })
		});

		expect(response.status).toBe(201);
		await expect(response.json()).resolves.toEqual({ ok: true });
		expect(stub.calls).toEqual([
			{
				url: 'http://localhost:9000/api/characters',
				method: 'POST',
				body: JSON.stringify({ name: 'Tester' }),
				headers: { 'content-type': 'application/json' }
			}
		]);
	});
});

describe('environment overrides', () => {
	it('sets values for the test and restores them afterwards', () => {
		const before = process.env.VELDRATH_TEST_FLAG;
		const restore = withEnv({ VELDRATH_TEST_FLAG: 'on' });
		expect(process.env.VELDRATH_TEST_FLAG).toBe('on');

		restore();
		expect(process.env.VELDRATH_TEST_FLAG).toBe(before);
	});
});

describe('source helpers', () => {
	it('locates the repository root and reads the client tree', () => {
		expect(repoRoot()).toContain('Veldrath');
		const components = walkFiles('web/client/src/lib/game', ['.svelte']);

		expect(components).toContain('web/client/src/lib/game/GameScreen.svelte');
		expect(stripComments('a(); // note\nb();')).not.toContain('note');
	});
});
