import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { readRepoFile, repoRoot, walkFiles } from '#lib/testing/source';

/**
 * Route coverage parity.
 *
 * Playwright cannot produce statement coverage, so "e2e coverage" is defined here as breadth:
 * every page route and every SvelteKit route handler must be referenced by a spec, or listed
 * below with a reason and the follow-up that owns it.
 *
 * This is the check that fails when somebody adds a route and forgets about it, which is the
 * failure mode a coverage number cannot catch.
 */

const ROUTES_DIR = 'web/client/src/routes';

/** Page and API routes, with route groups like `(admin)` removed. */
function discoverRoutes(): string[] {
	const routes = new Set<string>();
	const files = walkFiles(ROUTES_DIR, ['+page.svelte', '+server.ts']);

	for (const file of files) {
		const relative = file
			.slice(`${ROUTES_DIR}/`.length)
			.replace(/(\+page\.svelte|\+server\.ts)$/, '')
			.replace(/\/$/, '');
		const segments = relative.split('/').filter((segment) => segment && !/^\(.*\)$/.test(segment));
		routes.add(`/${segments.join('/')}`);
	}

	return [...routes].sort();
}

/** Every Playwright spec, concatenated: a route only has to be referenced, not driven. */
function specText(): string {
	return walkFiles('web/client/tests', ['.e2e.ts'])
		.map((file) => readRepoFile(file))
		.join('\n');
}

const escapeRegExp = (value: string) => value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

/** A route pattern where `[param]` and `[...rest]` match a single path segment. */
function routePattern(route: string): RegExp {
	const segments = route.split('/').map((segment) => {
		if (/^\[.+\]$/.test(segment)) return '[^/\\s\'"`]+';
		return escapeRegExp(segment);
	});

	return new RegExp(segments.join('/'));
}

const ROUTES = discoverRoutes();
const SPECS = specText();
const isReferenced = (route: string) => routePattern(route).test(SPECS);

/**
 * Routes with no spec reference, grouped by the reason they are exempt and the work item that
 * will cover them. Each pattern must still match a real, still-unreferenced route.
 */
const EXEMPTIONS: { pattern: RegExp; reason: string }[] = [
	{
		pattern: /^\/api\//,
		reason:
			'SvelteKit route handlers are covered by unit tests (game-token, creation proxies, Better Auth catch-all), not Playwright.'
	},
	{
		pattern: /^\/admin(\/|$)/,
		reason:
			'Admin pages: the (admin) layout guard is e2e-tested; per-page e2e happy paths are the route-tests item in the coverage plan.'
	},
	{
		pattern: /^\/mod(\/|$)/,
		reason:
			'Moderation pages: the (mod) layout guard is e2e-tested; per-page e2e happy paths are the route-tests item in the coverage plan.'
	},
	{
		pattern: /^\/editorial(\/|$)/,
		reason:
			'Editorial pages: guarded by the (editorial) layout; happy-path e2e lands with the route-tests item.'
	},
	{
		pattern: /^\/(content|languages|notifications|submissions)(\/|$)/,
		reason:
			'Foundry pages: guarded by the (foundry) layout and the schema-driven editor is unit-tested; e2e happy paths land with the route-tests item.'
	},
	{
		pattern: /^\/(lore|patch-notes)\/.+$/,
		reason:
			'Public detail pages render seeded server content; they get loader tests and an e2e mention with the public route-tests item.'
	},
	{
		pattern: /^\/(players|leaderboard)(\/|$)/,
		reason:
			'Player profile and leaderboard render seeded server content; loader tests plus an e2e mention land with the public route-tests item.'
	}
];

describe('route coverage parity', () => {
	it('discovers the route tree', () => {
		expect(ROUTES.length).toBeGreaterThan(40);
		expect(ROUTES).toContain('/characters');
		expect(ROUTES).toContain('/api/game-token');
	});

	it('references or exempts every route', () => {
		const unexplained = ROUTES.filter(
			(route) => !isReferenced(route) && !EXEMPTIONS.some((rule) => rule.pattern.test(route))
		);

		expect(
			unexplained,
			'add the route to a Playwright spec, or exempt it with the follow-up that will cover it'
		).toEqual([]);
	});

	it('keeps every exemption matching an uncovered route', () => {
		const stale = EXEMPTIONS.filter(
			(rule) => !ROUTES.some((route) => rule.pattern.test(route))
		).map((rule) => rule.pattern.source);

		// A rule is shadowed when every route it matches is already referenced by a spec, i.e. the
		// exemption no longer buys anything.
		const shadowed = EXEMPTIONS.filter((rule) => {
			const matched = ROUTES.filter((route) => rule.pattern.test(route));
			return matched.length > 0 && matched.every(isReferenced);
		}).map((rule) => rule.pattern.source);

		expect(stale, 'exemption matches no route any more').toEqual([]);
		expect(shadowed, 'exemption covers something a spec already references').toEqual([]);
	});

	it('gives a reason for every exemption', () => {
		const undocumented = EXEMPTIONS.filter((rule) => rule.reason.trim().length < 20).map(
			(rule) => rule.pattern.source
		);

		expect(undocumented).toEqual([]);
	});

	it('renders failures inside the app shell', () => {
		// No root error page means thrown loads and 404s fall through to SvelteKit's unstyled page.
		expect(existsSync(join(repoRoot(), 'web/client/src/routes/+error.svelte'))).toBe(true);
	});
});
