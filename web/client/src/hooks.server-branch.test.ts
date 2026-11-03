import { describe, expect, it, vi } from 'vitest';

/**
 * The `handleError` logging fallback.
 *
 * `hooks.server.test.ts` covers an `Error` with a stack and a thrown non-Error value. The third
 * shape is an `Error` whose `stack` is missing (some libraries delete it before re-throwing), where
 * the message is the only thing left worth logging.
 */

vi.mock('$app/env', () => ({ building: false }));
vi.mock('better-auth/svelte-kit', () => ({ svelteKitHandler: vi.fn() }));
vi.mock('#lib/server/auth', () => ({ getAuth: vi.fn() }));

const { handleError } = await import('./hooks.server');

describe('handleError', () => {
	it('falls back to the message when an Error has no stack', () => {
		const spy = vi.spyOn(console, 'error').mockImplementation(() => {});
		const error = new Error('the stack was stripped');
		Object.defineProperty(error, 'stack', { value: undefined });

		handleError({
			kind: 'framework',
			error,
			event: {
				request: new Request('http://localhost/characters'),
				url: new URL('http://localhost/characters')
			},
			status: 500,
			message: 'Internal Error'
		} as never);

		expect(spy).toHaveBeenCalledWith(
			expect.stringContaining('[handleError] framework GET /characters:'),
			'the stack was stripped'
		);
		spy.mockRestore();
	});
});
