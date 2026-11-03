/**
 * Test double for `$app/state` (aliased in `vite.config.ts`).
 *
 * The real module is a SvelteKit virtual module whose client runtime reads
 * `globalThis.__sveltekit_dev`, which does not exist under Vitest. Components only read `page.url`,
 * `page.status`, `page.error`, and `page.data`, so a plain object is enough — call {@link setPage}
 * before rendering when a case depends on the URL or the error.
 */

export interface FakePage {
	url: URL;
	status: number;
	error: { message: string } | null;
	data: Record<string, unknown>;
	params: Record<string, string>;
	state: Record<string, unknown>;
}

function createPage(overrides: Partial<FakePage> = {}): FakePage {
	return {
		url: new URL('http://localhost/'),
		status: 200,
		error: null,
		data: {},
		params: {},
		state: {},
		...overrides
	};
}

/** The object components receive as `page`. Mutated in place by {@link setPage}. */
export const page: FakePage = createPage();

/**
 * Resets `page` to a known state.
 *
 * Called from tests rather than an `afterEach` hook so a suite can opt in, and because `page` is a
 * shared singleton for the whole module graph.
 */
export function setPage(overrides: Partial<FakePage> = {}): FakePage {
	const next = createPage(overrides);
	Object.assign(page, next);
	return page;
}
