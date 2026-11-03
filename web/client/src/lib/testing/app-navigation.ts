import { vi } from 'vitest';

/**
 * Test double for `$app/navigation` (aliased in `vite.config.ts`).
 *
 * The real module needs a running SvelteKit client. These are plain spies, so a test asserts
 * `expect(goto).toHaveBeenCalledWith('/characters')` after a submit instead of observing a router.
 * The implementation stubs take no parameters: the signatures below are what callers see, and
 * ESLint rejects unused parameters, so the bodies stay empty.
 */

/** Navigates to a route. */
export const goto = vi.fn<(url: string | URL, opts?: unknown) => Promise<void>>(async () => {});

/** Re-runs every `load` on the page. */
export const invalidateAll = vi.fn<() => Promise<void>>(async () => {});

/** Re-runs the `load` functions that depend on the given keys. */
export const invalidate = vi.fn<(resource: string | URL) => Promise<void>>(async () => {});

/** Re-runs `load` for the given url and returns the result. */
export const preloadData = vi.fn<(url: URL) => Promise<{ type: 'loaded'; status: number }>>(
	async () => ({ type: 'loaded' as const, status: 200 })
);

/** Resolves a route id to a URL. */
export const preloadCode = vi.fn<(...urls: string[]) => Promise<void>>(async () => {});

/** Pushes a history entry. */
export const pushState = vi.fn<(url: string | URL, state: unknown) => void>(() => {});

/** Replaces the current history entry. */
export const replaceState = vi.fn<(url: string | URL, state: unknown) => void>(() => {});

/** Disables SvelteKit's scroll handling for the next navigation. */
export const disableScrollHandling = vi.fn<() => void>(() => {});

/** Hooks run before and after navigation; tests rarely need them, but layouts import them. */
export const beforeNavigate = vi.fn<(...args: unknown[]) => void>(() => {});
export const afterNavigate = vi.fn<(...args: unknown[]) => void>(() => {});

/** Clears every navigation spy. */
export function resetNavigation(): void {
	const spies: { mockClear(): void }[] = [
		goto,
		invalidateAll,
		invalidate,
		preloadData,
		preloadCode,
		pushState,
		replaceState,
		disableScrollHandling,
		beforeNavigate,
		afterNavigate
	];

	for (const spy of spies) spy.mockClear();
}
