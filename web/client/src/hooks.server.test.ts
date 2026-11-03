import { beforeEach, describe, expect, it, vi } from 'vitest';

/**
 * `handle` / `handleError` with a normal request (not prerendering).
 *
 * The hook is the only place the Better Auth session is copied onto `locals`, so every load and
 * action downstream depends on it. `$app/env` is mocked because `building` is injected by the
 * SvelteKit build and there is no dev server under Vitest.
 */

const { getAuth } = vi.hoisted(() => ({ getAuth: vi.fn() }));
const { svelteKitHandler } = vi.hoisted(() => ({
	svelteKitHandler: vi.fn(async ({ resolve }: { resolve: () => Promise<unknown> }) => resolve())
}));

vi.mock('$app/env', () => ({ building: false }));
vi.mock('better-auth/svelte-kit', () => ({ svelteKitHandler }));
vi.mock('#lib/server/auth', () => ({ getAuth }));

import { handle, handleError } from './hooks.server';

function authWith(session: unknown) {
	return { api: { getSession: vi.fn(async () => session) } };
}

function event() {
	return {
		request: new Request('http://localhost/characters'),
		url: new URL('http://localhost/characters'),
		locals: {} as App.Locals
	};
}

const authSession = {
	session: { id: 'session-1', userId: 'acct-1' },
	user: { id: 'acct-1', email: 'kaleth@example.com', name: 'Kaleth' }
};

beforeEach(() => {
	getAuth.mockReset();
	svelteKitHandler.mockClear();
});

describe('handle', () => {
	it('copies the Better Auth session onto locals and renders through Kit', async () => {
		getAuth.mockReturnValue(authWith(authSession));
		const request = event();
		const resolve = vi.fn(async () => new Response('ok'));

		const response = await handle({ event: request, resolve } as never);

		expect((response as Response).status).toBe(200);
		expect((request.locals as { user?: unknown }).user).toEqual(authSession.user);
		expect((request.locals as { session?: unknown }).session).toEqual(authSession.session);
		expect(svelteKitHandler).toHaveBeenCalledOnce();
	});

	it('leaves locals empty for an anonymous visitor', async () => {
		getAuth.mockReturnValue(authWith(null));
		const request = event();
		const resolve = vi.fn(async () => new Response('ok'));

		await handle({ event: request, resolve } as never);

		expect(request.locals).toEqual({});
		// The request still reaches SvelteKit, which is what serves the public pages.
		expect(svelteKitHandler).toHaveBeenCalledOnce();
	});
});

describe('handleError', () => {
	it('logs the stack of an Error', () => {
		const spy = vi.spyOn(console, 'error').mockImplementation(() => {});
		const error = new Error('database is on fire');

		handleError({
			kind: 'app',
			error,
			event: event(),
			status: 500,
			message: 'Internal Error'
		} as never);

		expect(spy).toHaveBeenCalledWith(
			expect.stringContaining('[handleError] app GET /characters:'),
			error.stack
		);
		spy.mockRestore();
	});

	it('logs a thrown value that is not an Error as-is', () => {
		const spy = vi.spyOn(console, 'error').mockImplementation(() => {});

		handleError({
			kind: 'unknown',
			error: 'a bare string',
			event: event(),
			status: 500,
			message: 'Internal Error'
		} as never);

		expect(spy).toHaveBeenCalledWith(
			expect.stringContaining('[handleError] unknown GET /characters:'),
			'a bare string'
		);
		spy.mockRestore();
	});
});
