import { describe, expect, it } from 'vitest';
import { readRepoFile, stripComments, walkFiles } from '#lib/testing/source';

/**
 * REST route parity.
 *
 * `Veldrath.Server` exposes ~140 minimal-API routes; the SvelteKit server calls the ones it needs
 * through `#lib/server/*`. A route with no web consumer is not automatically wrong (developer
 * endpoints, alternates for external consumers), but it must be a decision, not an oversight.
 *
 * Both directions are checked:
 * - every path the web calls must exist on the server, which catches a renamed endpoint at build
 *   time instead of at runtime;
 * - every server route must be called or listed in {@link UNCONSUMED} with a reason.
 */

/** Server route shapes, for example `GET /api/content/{type}/schema`. */
function serverRoutes(): string[] {
	const routes = new Set<string>();

	for (const file of walkFiles('Veldrath.Server/Features', ['.cs'])) {
		const source = stripComments(readRepoFile(file));
		let prefix = '';

		for (const line of source.split('\n')) {
			const group = /MapGroup\(\s*"([^"]*)"/.exec(line);
			if (group) {
				prefix = group[1];
				continue;
			}

			const route = /Map(Get|Post|Put|Delete|Patch)\(\s*"([^"]*)"/.exec(line);
			if (route) {
				routes.add(`${route[1].toUpperCase()} ${normalize(`${prefix}${route[2]}`)}`);
			}
		}
	}

	return [...routes].sort();
}

/** Replaces every dynamic segment with `*` so route shapes from both sides compare. */
function normalize(path: string): string {
	// Template interpolations first: `${id}` contains `{id}`, so the plain-brace rule would leave
	// a stray `$` behind and the two sides would never compare equal. Query strings are dropped
	// because a route matches on its path.
	return path
		.split('?')[0]
		.replace(/\/+$/, '')
		.replace(/\$\{[^}]*\}/g, '*')
		.replace(/\{[^}]*\}/g, '*')
		.replace(/:[a-z]+/, '*')
		.replace(/\/+/g, '/');
}

/**
 * Resolves `const BASE = '/api/…'` style constants so composed template paths are still visible.
 *
 * Without this, `character-api.ts` builds `/api/character-creation/sessions/${id}/${step}` from a
 * constant and the creation endpoints look unconsumed.
 */
function resolveConstants(source: string): string {
	const constants = new Map<string, string>();

	for (const match of source.matchAll(/\bconst\s+([A-Z][A-Z0-9_]*)\s*=\s*['"`]([^'"`$]*)['"`]/g)) {
		constants.set(match[1], match[2]);
	}

	let resolved = source;
	for (let pass = 0; pass < 3; pass += 1) {
		for (const [name, value] of constants) {
			resolved = resolved.replaceAll(`\${${name}}`, value);
		}
	}

	return resolved;
}

/** Every `/api/…` path the web app references, normalised to a shape. */
function clientRoutes(): string[] {
	const paths = new Set<string>();

	for (const file of walkFiles('web/client/src', ['.ts', '.svelte'])) {
		if (/\.(test|spec)\.ts$/.test(file)) continue;
		if (file.includes('/lib/parity/') || file.includes('/lib/testing/')) continue;

		const source = resolveConstants(readRepoFile(file));
		// Stop only at whitespace or a quote: template interpolations such as
		// `${encodeURIComponent(type)}` contain parentheses and braces the path needs.
		for (const match of source.matchAll(/\/api\/[^\s'"`]*/g)) {
			paths.add(normalize(match[0]));
		}
	}

	return [...paths].sort();
}

/** True when a server route shape is matched by a client path shape (`*` matches one segment). */
function isConsumed(route: string, clientPaths: string[]): boolean {
	const routeSegments = route.split(' ')[1].split('/').filter(Boolean);

	return clientPaths.some((clientPath) => {
		const clientSegments = clientPath.split('/').filter(Boolean);
		if (clientSegments.length !== routeSegments.length) return false;

		return routeSegments.every(
			(segment, index) =>
				segment === '*' || clientSegments[index] === '*' || segment === clientSegments[index]
		);
	});
}

/**
 * Server routes the web app does not call, grouped by reason.
 *
 * Every pattern must still match at least one real route — a stale rule fails the test, so this
 * cannot rot into a blanket exemption.
 */
const UNCONSUMED: { pattern: RegExp; reason: string }[] = [
	{
		pattern: /^\/api\/dev\//,
		reason: 'Developer-only endpoints, exercised from curl and docs, never from the browser.'
	},
	{
		pattern: /^\/api\/admin\/instances\b/,
		reason:
			'Operator lever for instance runs: list open runs and purge a stuck one. It is deliberately not a player surface, and the web admin has no instance screen yet — the operator reads it from the Forgejo/Coolify console with a staff token. Recorded in plans/dungeons.md §F2.6.'
	},
	{
		pattern: /^\/api\/announcements$/,
		reason:
			'Legacy announcement feed superseded by /api/editorial/announcements, which carries status and publish state. Nothing in this repo calls it: removal candidate recorded in plans/web-feature-parity-and-coverage.md.'
	},
	{
		pattern: /^\/api\/(zones|regions|worlds)\b/,
		reason:
			'World/zone REST reads. The game client receives the map graph over the hub; these serve external consumers.'
	},
	{
		pattern:
			/^\/api\/content\/(powers|enemies|npcs|quests|recipes|loot-tables|skills|items|enchantments|materials|organizations|zone-locations|dialogues|actor-instances|material-properties|traits|languages)\b/,
		reason:
			'Typed per-family read endpoints. Foundry browses through /api/content/browse and /api/content/schema, which are schema-driven.'
	},
	{
		pattern: /^\/api\/content\/browse\/\*\/\*$/,
		reason: 'Detail read is consumed; listed only if the editor switches to the typed endpoint.'
	}
];

/** Client paths that are served by SvelteKit itself, not by Veldrath.Server. */
const CLIENT_ONLY: { pattern: RegExp; reason: string }[] = [
	{
		pattern: /^\/api\/game-token$/,
		reason: 'SvelteKit mints the game JWT; the .NET server validates it.'
	},
	{
		pattern: /^\/api\/auth\b/,
		reason: 'Better Auth handler mounted inside SvelteKit.'
	},
	{
		pattern: /^\/api\/characters\/creation\b/,
		reason: 'SvelteKit proxy routes that translate browser calls into the .NET creation API.'
	}
];

describe('REST route parity', () => {
	const server = serverRoutes();
	const client = clientRoutes();

	it('finds routes on both sides', () => {
		expect(server.length).toBeGreaterThan(100);
		expect(server).toContain('GET /api/content/schema');
		expect(client.length).toBeGreaterThan(40);
		expect(client).toContain('/api/content/schema');
	});

	it('calls only paths the server exposes or SvelteKit owns', () => {
		const orphans = client.filter(
			(path) =>
				!CLIENT_ONLY.some((rule) => rule.pattern.test(path)) &&
				!server.some((route) => isConsumed(route, [path]))
		);

		expect(orphans, 'a web call points at a path no endpoint declares').toEqual([]);
	});

	it('consumes every server route unless there is a documented reason not to', () => {
		const unconsumed = server.filter(
			(route) =>
				!isConsumed(route, client) &&
				!UNCONSUMED.some((rule) => rule.pattern.test(route.split(' ')[1]))
		);

		expect(unconsumed, 'wire the endpoint into #lib/server, or add a rule with a reason').toEqual(
			[]
		);
	});

	it('keeps every unconsumed rule matching a real endpoint', () => {
		const stale = UNCONSUMED.filter(
			(rule) => !server.some((route) => rule.pattern.test(route.split(' ')[1]))
		).map((rule) => rule.pattern.source);

		expect(stale).toEqual([]);
	});

	it('gives a reason for every exemption', () => {
		const undocumented = [...UNCONSUMED, ...CLIENT_ONLY]
			.filter((rule) => rule.reason.trim().length < 10)
			.map((rule) => rule.pattern.source);

		expect(undocumented).toEqual([]);
	});
});
