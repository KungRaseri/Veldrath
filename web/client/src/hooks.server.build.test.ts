import { describe, expect, it, vi } from 'vitest';

/**
 * `handle` while SvelteKit is prerendering.
 *
 * `building` is a compile-time flag, so the only way to reach the early return is to import the
 * hook in a module graph where `$app/env` reports a build. That is why this lives in its own file
 * next to `hooks.server.test.ts`: branch coverage merges across files, but a mock cannot differ
 * between two imports inside one file.
 */

const { getAuth } = vi.hoisted(() => ({ getAuth: vi.fn() }));
const { svelteKitHandler } = vi.hoisted(() => ({ svelteKitHandler: vi.fn() }));

vi.mock('$app/env', () => ({ building: true }));
vi.mock('better-auth/svelte-kit', () => ({ svelteKitHandler }));
vi.mock('#lib/server/auth', () => ({ getAuth }));

import { handle } from './hooks.server';

describe('handle while building', () => {
	it('renders straight through without touching auth or the database', async () => {
		const resolve = vi.fn(async () => new Response('prerendered'));
		const request = {
			request: new Request('http://localhost/'),
			url: new URL('http://localhost/'),
			locals: {}
		};

		const response = await handle({ event: request, resolve } as never);

		expect(await (response as Response).text()).toBe('prerendered');
		expect(getAuth).not.toHaveBeenCalled();
		expect(svelteKitHandler).not.toHaveBeenCalled();
	});
});
