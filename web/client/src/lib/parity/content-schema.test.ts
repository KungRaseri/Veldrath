import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { matchAll, readRepoFile, repoRoot, stripComments } from '#lib/testing/source';

/**
 * Content vocabulary parity.
 *
 * `ContentSchemaRegistry` is what the Foundry content browser lists. Two server switches decide
 * whether a listed type can actually be opened and edited:
 *
 * - `ContentEndpoints.BrowseAsync` / `BrowseDetailAsync` for reads;
 * - `ContentEntityTypes` for the write path, the pack importer and the validator lookup.
 *
 * They are maintained by hand, and in this pass they disagreed: the registry advertised
 * `weapon` and `armor`, both switches omitted them, and the Foundry browser 404'd on the two
 * largest item catalogs. This test is what makes that impossible to reintroduce.
 *
 * `ContentEntityTypes` moved out of `Veldrath.Server/Features/Content/` into
 * `RealmEngine.Data/Content/` when the content-pack importer landed, so the offline importer
 * (`Veldrath.ContentPack`) can resolve a family without loading ASP.NET Core, and so there is one
 * map instead of a server copy and a tool copy that drift. Read it from the data library: there is
 * deliberately no server copy to fall back to. A stale path here fails the whole file at module
 * scope (ENOENT) and takes every assertion below with it — that is how this guard silently stopped
 * guarding on `development` once already, so a move must repoint this constant in the same commit.
 *
 * The web side is intentionally not checked against a hardcoded list: the Foundry portal reads
 * `GET /api/content/schema`, so the server registry is the single source of truth.
 */

const schemaSource = stripComments(readRepoFile('Veldrath.Contracts/Content/ContentSchema.cs'));
const endpointsSource = stripComments(
	readRepoFile('Veldrath.Server/Features/Content/ContentEndpoints.cs')
);
// The type-key → entity-type map. It lives in the data library so the HTTP write path, the pack
// validator and the offline importer share one map; there is no server copy to fall back to.
const entityTypesSource = stripComments(
	readRepoFile('RealmEngine.Data/Content/ContentEntityTypes.cs')
);
const contentApiSource = readRepoFile('web/client/src/lib/server/content-api.ts');

/** Keys between a `start` marker and the first `};` after it. */
function keysBetween(source: string, start: string, pattern: RegExp): string[] {
	const startIndex = source.indexOf(start);
	if (startIndex < 0) throw new Error(`Missing marker: ${start}`);

	const endIndex = source.indexOf('};', startIndex);
	const block = source.slice(startIndex, endIndex < 0 ? undefined : endIndex);

	return [...new Set(matchAll(block, pattern).map(([key]) => key))].sort();
}

const registryKeys = keysBetween(schemaSource, '_schemas =', /\["(\w+)"\]\s*=/g);
const browseKeys = keysBetween(endpointsSource, 'BrowseAsync(', /"(\w+)"\s*=>/g);
const detailKeys = keysBetween(endpointsSource, 'BrowseDetailAsync(', /^\s*"(\w+)"\s*=>/gm);
const writeKeys = keysBetween(entityTypesSource, '_byTypeKey =', /\["(\w+)"\]\s*=\s*typeof/g);

describe('content vocabulary parity', () => {
	it('reads the registry and both server switches', () => {
		expect(registryKeys.length).toBeGreaterThanOrEqual(20);
		expect(registryKeys).toContain('zonelocation');
		expect(registryKeys).toContain('language');
		expect(browseKeys.length).toBeGreaterThanOrEqual(registryKeys.length);
		expect(writeKeys.length).toBeGreaterThanOrEqual(registryKeys.length);
	});

	it('can browse every content type the registry advertises', () => {
		const missing = registryKeys.filter((key) => !browseKeys.includes(key));

		expect(
			missing,
			'ContentEndpoints.BrowseAsync has no switch arm for a type the Foundry browser lists'
		).toEqual([]);
	});

	it('can open the detail of every content type the registry advertises', () => {
		const missing = registryKeys.filter((key) => !detailKeys.includes(key));

		expect(
			missing,
			'ContentEndpoints.BrowseDetailAsync has no switch arm for a type the Foundry browser lists'
		).toEqual([]);
	});

	it('can write every content type the registry advertises', () => {
		const missing = registryKeys.filter((key) => !writeKeys.includes(key));

		expect(
			missing,
			'ContentEntityTypes._byTypeKey (RealmEngine.Data/Content) has no entity mapping for a type the Foundry editor can open'
		).toEqual([]);
	});

	it('keeps the type-key map in one place', () => {
		// The map moved to the data library so the offline importer can resolve a family without
		// loading ASP.NET Core. Restoring a copy under Veldrath.Server "to fix an ENOENT" would
		// bring back the second map this move removed, and the two would drift.
		expect(
			existsSync(join(repoRoot(), 'Veldrath.Server/Features/Content/ContentEntityTypes.cs')),
			'ContentEntityTypes has a second copy under Veldrath.Server — the map belongs to RealmEngine.Data/Content only'
		).toBe(false);
	});

	it('keeps read aliases explicit', () => {
		// `power` is a legacy alias for `ability`; the registry deliberately does not advertise it.
		const aliases = browseKeys.filter((key) => !registryKeys.includes(key));

		expect(aliases).toEqual(['power']);
	});

	it('keeps the Foundry portal schema-driven', () => {
		expect(contentApiSource).toContain('/api/content/schema');
		expect(contentApiSource).toContain('/api/content/browse');
		// A hardcoded type list here would drift from the registry; the portal must not have one.
		expect(contentApiSource).not.toContain("'zonelocation'");
	});
});
