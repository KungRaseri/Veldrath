import { randomUUID } from 'node:crypto';
import { expect, test, type Locator, type Page } from '@playwright/test';
import {
	bagDiff,
	createDwarfWarrior,
	fightToVictory,
	openA1Database,
	readArchetypeReward,
	readCharacterState,
	readHealth,
	readItemNames,
	readLootTable,
	readZonePools,
	type PoolEntry
} from './support/a1-journey';
import { walkTo } from './support/map';
import { makeAccount, register, stackEnabled, stackSkipReason } from './support/stack';

/**
 * A1 — The Droveway plays, from a content pack to a character's bag.
 *
 * The zone's spawn pools are authored content again (`content/zonelocations/the-droveway.yaml`, translated from
 * `docs/design/content/pools/the-droveway.yaml` by pool-matrix.md §8), and a pack only becomes real when it is
 * imported — which `scripts/e2e-stack.sh` does after the API seeds the catalogue. So this spec is the whole chain in
 * one run, against the real client, the real hub and a real Postgres:
 *
 *   Crestfall Square → The Droveway → The Drove Road → an encounter drawn from the imported pool, its loot in the bag
 *   → The Weathered Waypost, the zone's SafeRespawn point → back to Crestfall Square.
 *
 * What it asserts, and why each one is the honest shape:
 *
 *  - **The pools are the pack's.** Read from `ZoneLocations.ActorPool`, entry for entry with their weights. A browser
 *    cannot see a weight, and a pool that is not stored is not the pool the spec walked into. This is also the
 *    import's fingerprint: the seeder's interim pair for `drove-road` is two entries written as percentage-looking
 *    weights (giant-rat 60 / goblin-scout 40), so a four-entry pool naming `drove-cur` and `peat-adder` cannot have
 *    come from anywhere but the pack.
 *  - **Every pooled reference resolves, and fits its location.** Each of the four archetypes the pack names exists,
 *    is hostile, binds an imported loot table with entries, and overlaps the level band of the location that pools
 *    it. That is the design's own rule checked against the *imported* catalogue rather than the seed.
 *  - **The roster came from the pool.** The persisted `EnemyInstances` rows at the Drove Road name their archetype,
 *    so every enemy a player can meet there is provably one the imported pool allows — and the roster size is the
 *    resolver's 2–4 roll (`ActorPoolResolver.MinCount`/`MaxCount`). `drove-road` has no authored placement, so
 *    every row is a pool draw.
 *  - **The reward is the imported content's.** Experience is exact and gold is the archetype's range, both read back
 *    from the row the import wrote; every item added to the bag is an entry of that archetype's imported loot table,
 *    within that entry's quantity bounds. Nothing is mocked and no drop rate is touched: which entries land is the
 *    production roll, so the spec keeps fighting the pool until one kill pays — the isolated stack only shortens the
 *    respawn window (`Enemies__*` in `scripts/e2e-stack.sh`), never the drop chance.
 *  - **The waypost is quiet.** `weathered-waypost` is the zone's `SafeRespawn` point and the design excludes it: its
 *    stored pool is empty and nothing hostile stands there, whatever the character does. That is the exclusion
 *    assertion, not an omission.
 *
 * Two things are deliberately *not* asserted, because pinning them would pin the dice rather than the content:
 *
 *  - **A specific archetype at the Drove Road.** A location's roster is one weighted roll per zone scope, created
 *    once and revived — not re-rolled — by the respawn sweeper (`EfCoreEnemyInstanceStore.GetAliveOrSpawnAsync`
 *    spawns only when the location has no rows at all, and `SweepExpiredRespawnsAsync` flips `IsAlive` back on the
 *    same rows). The zone scope is shared by every character in the zone (`zone:the-droveway`, `ZoneGroupName`), so
 *    demanding "a drove-cur appears" would either be a coin flip or a fight with whatever else is standing here.
 *    What the spec demands instead is that *every* draw is a member of the imported pool, and it prefers a
 *    `drove-cur`/`peat-adder` when the roll produced one — the two the seeder's interim pool never named.
 *  - **The Sunken Fields on foot.** It is pooled by the pack (checked from the stored row), but it is hidden behind a
 *    passive DC 5 check, so walking into it is a different slice's journey — the same reason the Ashlen Wood spec
 *    leaves its hidden location to the stored-pool check.
 */

/** The zone's pools, entry for entry from `content/zonelocations/the-droveway.yaml`, in the pack's authored order. */
const THE_DROVEWAY_POOLS: Record<string, PoolEntry[]> = {
	'drove-road': [
		{ archetype: 'giant-rat', weight: 5 },
		{ archetype: 'goblin-scout', weight: 3 },
		{ archetype: 'drove-cur', weight: 3 },
		{ archetype: 'peat-adder', weight: 2 }
	],
	'sunken-fields': [
		{ archetype: 'peat-adder', weight: 5 },
		{ archetype: 'giant-rat', weight: 4 },
		{ archetype: 'drove-cur', weight: 2 }
	],
	// Empty by design: the zone's SafeRespawn point, where the wayfarer-elder stands as an NPC placement.
	'weathered-waypost': []
};

/** Every archetype the zone's pools name — the only thing a pool draw at either location may be. */
const POOL_ARCHETYPES = ['giant-rat', 'goblin-scout', 'drove-cur', 'peat-adder'];

/**
 * The two archetypes only the pack names.
 *
 * The seeder's interim `drove-road` pool was `giant-rat 60 / goblin-scout 40`; these two arrived with the roster, so a
 * roster row carrying one of them is a draw from the imported pool by construction. The spec prefers them as a target
 * but does not require one, for the reason in the header.
 */
const DESIGN_ONLY_ARCHETYPES = ['drove-cur', 'peat-adder'];

/** The zone's two pooled locations, its excluded one, and the map walk between them. */
const ZONE_ID = 'the-droveway';
const ENCOUNTER_LOCATION = 'drove-road';
const SAFE_LOCATION = 'weathered-waypost';

const MAP_NODES = {
	townSquare: 'The Crestfall Square',
	zoneGate: 'Road to The Droveway',
	arrival: 'Arrival',
	encounter: 'The Drove Road',
	safe: 'The Weathered Waypost',
	outOfZone: 'Road to Crestfall'
};

type Sql = ReturnType<typeof openA1Database>;

/** One living enemy at a location, with the combat participants the roster row already carries. */
interface RosterRow {
	id: string;
	archetype: string;
	instance: string | null;
	name: string;
	participants: number;
}

/**
 * Reads the living roster at a location, including how many characters are already in a fight with each row.
 *
 * `EnemyInstances` is shared by every character in the zone scope, and another spec in this suite fights at the Drove
 * Road too (`encounters.e2e.ts`). A row with a participant is somebody's current fight, and killing it would end
 * their combat — so the spec treats those as off limits rather than racing them.
 *
 * @param sql The open reader.
 * @param locationSlug Location to read.
 * @returns The living enemies there, oldest spawn first.
 */
async function readRoster(sql: Sql, locationSlug: string): Promise<RosterRow[]> {
	const rows = await sql<
		{ id: string; archetype: string; instance: string | null; name: string; participants: number }[]
	>`
		SELECT e."Id"::text AS id,
		       e."ArchetypeSlug" AS archetype,
		       e."InstanceSlug" AS instance,
		       e."Name" AS name,
		       (SELECT count(*)::int FROM "EnemyParticipants" p WHERE p."EnemyInstanceId" = e."Id") AS participants
		FROM "EnemyInstances" e
		WHERE e."LocationSlug" = ${locationSlug} AND e."IsAlive" = true
		ORDER BY e."SpawnedAt", e."Id"
	`;

	return rows.map((row) => ({
		id: row.id,
		archetype: row.archetype,
		instance: row.instance,
		name: row.name,
		participants: Number(row.participants)
	}));
}

/** One location's level band and authored traits, as the import stored them. */
interface LocationRow {
	slug: string;
	minLevel: number;
	maxLevel: number;
	safeRespawn: boolean;
	hidden: boolean;
}

/**
 * Reads a zone location's level band and the two traits this journey turns on.
 *
 * The band lives in the `Stats` blob and the traits in `Traits` — both are what the pack authored and the importer
 * replaced, so reading them here is reading the pack's own values back.
 *
 * @param sql The open reader.
 * @param slug Location slug.
 * @returns The location's band and traits, or `null` when the row is missing.
 */
async function readLocation(sql: Sql, slug: string): Promise<LocationRow | null> {
	const rows = await sql<
		{
			slug: string;
			minLevel: string | null;
			maxLevel: string | null;
			traits: Record<string, unknown>;
		}[]
	>`
		SELECT "Slug" AS slug,
		       "Stats"->>'MinLevel' AS "minLevel",
		       "Stats"->>'MaxLevel' AS "maxLevel",
		       coalesce("Traits", '{}'::jsonb) AS traits
		FROM "ZoneLocations"
		WHERE "Slug" = ${slug}
	`;

	const row = rows[0];
	if (!row) return null;

	const traits = row.traits ?? {};

	return {
		slug: row.slug,
		minLevel: Number(row.minLevel ?? 0),
		maxLevel: Number(row.maxLevel ?? 0),
		safeRespawn: Boolean(traits.SafeRespawn ?? traits.safeRespawn ?? false),
		hidden: Boolean(traits.IsHidden ?? traits.isHidden ?? false)
	};
}

/** One pooled archetype as the imported catalogue stores it. */
interface PooledArchetype {
	slug: string;
	hostile: boolean;
	minLevel: number;
	maxLevel: number;
	lootTable: string | null;
}

/**
 * Reads one pooled archetype: the row a spawn would be built from.
 *
 * @param sql The open reader.
 * @param slug Archetype slug.
 * @returns Its hostility, level band and bound loot table, or `null` when the catalogue does not know the slug.
 */
async function readPooledArchetype(sql: Sql, slug: string): Promise<PooledArchetype | null> {
	const rows = await sql<
		{
			slug: string;
			hostile: boolean | null;
			minLevel: number;
			maxLevel: number;
			table: string | null;
		}[]
	>`
		SELECT a."Slug" AS slug,
		       (a."Traits"->>'Hostile')::boolean AS hostile,
		       a."MinLevel" AS "minLevel",
		       a."MaxLevel" AS "maxLevel",
		       t."Slug" AS table
		FROM "ActorArchetypes" a
		LEFT JOIN "LootTables" t ON t."Id" = a."LootTableId"
		WHERE a."Slug" = ${slug}
	`;

	const row = rows[0];
	if (!row) return null;

	return {
		slug: row.slug,
		hostile: row.hostile ?? false,
		minLevel: Number(row.minLevel ?? 0),
		maxLevel: Number(row.maxLevel ?? 0),
		lootTable: row.table
	};
}

/** The Local panel's engageable rows — one per living enemy, in the order the server sent them. */
function localRows(page: Page): Locator {
	const local = page.locator('section', { has: page.getByRole('heading', { name: 'Local' }) });
	return local.locator('li', { has: page.getByRole('button', { name: 'Engage' }) });
}

/**
 * Finds the panel row for a chosen roster row: the last one carrying that enemy's name.
 *
 * Matching on the name is sound because the name is the archetype's display name, snapshotted at spawn; two rows of the
 * same archetype are interchangeable, and "the last one" is the choice that keeps this spec off the row
 * `encounters.e2e.ts` picks (it always takes the *first* Engage button).
 *
 * @param page The game page.
 * @param name The enemy name to match.
 * @returns The row locator, or `null` when the panel shows no such enemy.
 */
async function findRowByName(page: Page, name: string): Promise<Locator | null> {
	const rows = localRows(page);
	const count = await rows.count();

	for (let index = count - 1; index >= 0; index -= 1) {
		const row = rows.nth(index);
		const text = await row.innerText().catch(() => '');
		if (text.includes(name)) return row;
	}

	return null;
}

/**
 * Picks the enemy to fight: a design-only pool draw if one is free, otherwise the last free pool draw.
 *
 * "Free" means no character is already in a fight with that row — see {@link readRoster}. The Drove Road has no
 * authored placements, so every row it can offer is a pool draw by definition; this still filters on membership so a
 * placement added later cannot quietly become this spec's target.
 *
 * @param roster The living roster.
 * @returns The row to engage, or `null` when every draw is taken or there is none.
 */
function pickTarget(roster: RosterRow[]): RosterRow | null {
	const free = roster.filter(
		(row) => row.participants === 0 && POOL_ARCHETYPES.includes(row.archetype)
	);
	if (free.length === 0) return null;

	const designOnly = free.filter((row) => DESIGN_ONLY_ARCHETYPES.includes(row.archetype));
	const pool = designOnly.length > 0 ? designOnly : free;

	return pool[pool.length - 1];
}

/**
 * Waits for a free pool draw to be standing at the Drove Road, stepping out and back as needed.
 *
 * The respawn sweeper is on its own clock (2s in the isolated stack), and a revived row reaches the Local panel only
 * when the client re-reads the location — so this refreshes, then polls, rather than assuming one round-trip is enough.
 *
 * @param page The game page.
 * @param sql The open reader.
 * @param attempts How many refresh/poll cycles to allow.
 * @returns The panel row to engage and the roster row behind it, or `null` when nothing free appeared.
 */
async function waitForTarget(
	page: Page,
	sql: Sql,
	attempts = 8
): Promise<{ row: Locator; target: RosterRow } | null> {
	for (let attempt = 0; attempt < attempts; attempt += 1) {
		const target = pickTarget(await readRoster(sql, ENCOUNTER_LOCATION));

		if (target) {
			const row = await findRowByName(page, target.name);
			if (row) return { row, target };
		}

		await walkTo(page, MAP_NODES.arrival);
		await walkTo(page, MAP_NODES.encounter);
		await page.waitForTimeout(1_500);
	}

	const target = pickTarget(await readRoster(sql, ENCOUNTER_LOCATION));
	if (!target) return null;

	const row = await findRowByName(page, target.name);
	return row ? { row, target } : null;
}

test.describe('A1 — The Droveway pools', () => {
	test.skip(!stackEnabled, stackSkipReason);
	test.describe.configure({ mode: 'serial' });
	test.setTimeout(480_000);

	test('Crestfall Square → the droveway → the Drove Road → a pool kill pays the imported reward → the waypost', async ({
		page
	}) => {
		await register(page, makeAccount('e2edrove'));

		// The wizard accepts letters and spaces only ("2–20 characters"), so a stamp of digits would be rejected
		// before the availability check even runs.
		const name = `Drove${randomUUID()
			.replace(/[^a-z]/g, '')
			.slice(0, 6)}`.slice(0, 20);

		await createDwarfWarrior(page, name);

		const characterId = page.url().split('/').pop() ?? '';
		expect(characterId, 'the wizard must land the character in the world').toMatch(
			/^[0-9a-f-]{36}$/
		);

		const sql = openA1Database();

		try {
			// ── The stored pools ─────────────────────────────────────────────────────────────────────────
			// Read before the walk: this is the catalogue the journey is about to meet, not a report of what it
			// met. A pack that never imported leaves the seeder's interim pairs here — a two-entry 60/40 pool at
			// the Drove Road — and this fails at once, which is the negative control for the whole spec.
			const pools = await readZonePools(sql, ZONE_ID);
			expect(Object.keys(pools).sort()).toEqual([
				ENCOUNTER_LOCATION,
				'sunken-fields',
				SAFE_LOCATION
			]);

			for (const [slug, expected] of Object.entries(THE_DROVEWAY_POOLS)) {
				expect(pools[slug], `${slug} must carry the pack's pool, weights included`).toEqual(
					expected
				);
			}

			// ── The pooled references ───────────────────────────────────────────────────────────────────
			// Every archetype the pack names resolves in the imported catalogue, is something that can be
			// fought, binds a loot table with entries, and overlaps the band of the location that pools it.
			for (const [slug, expected] of Object.entries(THE_DROVEWAY_POOLS)) {
				if (expected.length === 0) continue;

				const location = await readLocation(sql, slug);
				expect(location, `${slug} must be in the imported catalogue`).not.toBeNull();
				expect(location!.minLevel, `${slug} must keep the band the pack authored`).toBeGreaterThan(
					0
				);

				for (const entry of expected) {
					const archetype = await readPooledArchetype(sql, entry.archetype);
					expect(
						archetype,
						`${slug} pools ${entry.archetype}, which must resolve in the imported catalogue`
					).not.toBeNull();
					expect(archetype!.hostile, `${entry.archetype} must be hostile to be in a pool`).toBe(
						true
					);
					expect(
						archetype!.lootTable,
						`${entry.archetype} must bind an imported loot table`
					).toBeTruthy();

					const table = await readLootTable(sql, archetype!.lootTable!);
					expect(table.length, `${archetype!.lootTable} must have entries`).toBeGreaterThan(0);

					// The design's overlap rule: the archetype's own band must intersect the location's.
					expect(
						Math.max(archetype!.minLevel, location!.minLevel),
						`${entry.archetype} must fit ${slug}'s level band`
					).toBeLessThanOrEqual(Math.min(archetype!.maxLevel, location!.maxLevel));
				}
			}

			// The excluded location is excluded by its authored traits *and* by an empty pool, which is the pair
			// the design reads as "deliberately spawns nothing" rather than "nobody filled this in".
			const safe = await readLocation(sql, SAFE_LOCATION);
			expect(safe, 'the waypost must be in the imported catalogue').not.toBeNull();
			expect(safe!.safeRespawn, 'weathered-waypost is the zone’s SafeRespawn point').toBe(true);
			expect(pools[SAFE_LOCATION], 'the waypost must pool nothing at all').toEqual([]);

			// The other pooled location keeps the trait that keeps it off this walk: it is hidden behind a passive
			// DC 5 check, so its pool is checked from the stored row rather than met on foot.
			const sunken = await readLocation(sql, 'sunken-fields');
			expect(sunken, 'sunken-fields must be in the imported catalogue').not.toBeNull();
			expect(sunken!.hidden, 'sunken-fields stays the hidden location the pack authored').toBe(
				true
			);

			// ── The walk ────────────────────────────────────────────────────────────────────────────────
			// The wizard already placed the character on the square; walking there is the "already here" no-op
			// that proves the map knows where it is before a zone change paints a different map.
			expect(await walkTo(page, MAP_NODES.townSquare)).toBe(true);
			await expect(page.locator('button[aria-current="location"]')).toContainText(
				'Crestfall Square'
			);

			expect(await walkTo(page, MAP_NODES.zoneGate)).toBe(true);
			expect(await walkTo(page, MAP_NODES.arrival)).toBe(true);
			expect(await walkTo(page, MAP_NODES.encounter)).toBe(true);
			await expect(page.locator('button[aria-current="location"]')).toContainText('The Drove Road');

			// ── The roster the pool produced ────────────────────────────────────────────────────────────
			// A browser shows the enemy's name; the stored row carries the archetype slug the pool drew. The
			// Drove Road has no authored placement, so every row here is a draw.
			//
			// No upper bound on the count: the 2–4 roll (`ActorPoolResolver.MinCount`/`MaxCount`) governs one
			// spawn, and a shared location can hold more rows than that once several characters have arrived —
			// the store spawns whenever it sees no rows, and its unique index is per (archetype, spawn instant).
			// This suite runs its specs in parallel, so a count ceiling here would fail on scheduling, not on content.
			const roster = await readRoster(sql, ENCOUNTER_LOCATION);
			expect(
				roster.length,
				'walking onto the drove road must spawn its roster from the imported pool'
			).toBeGreaterThanOrEqual(2);

			for (const enemy of roster) {
				expect(
					POOL_ARCHETYPES,
					`${enemy.name} is not one of the zone's pooled archetypes`
				).toContain(enemy.archetype);
				expect(
					enemy.instance,
					'the drove road authors no placement, so no row may carry an instance slug'
				).toBeNull();
			}

			// ── The fight, until the pool pays ──────────────────────────────────────────────────────────
			let state = await readCharacterState(sql, characterId);
			expect(state, 'the character row must exist').not.toBeNull();

			type Kill = { archetype: string; added: { itemSlug: string; quantity: number }[] };
			const kills: Kill[] = [];
			let payment: { archetype: string; table: string } | null = null;

			for (let attempt = 0; attempt < 10 && payment === null; attempt += 1) {
				const target = await waitForTarget(page, sql);
				expect(
					target,
					`no pool enemy was free to fight at the drove road (attempt ${attempt + 1})`
				).not.toBeNull();

				// Health is the only budget the journey has: nothing in the droveway heals it. A quarter is the
				// floor that keeps a bad run of draws from ending the walk before the reward is exercised.
				const health = await readHealth(page);
				expect(
					health.current,
					'the character ran out of health before the pool paid out; the draw was too hostile for a level 1'
				).toBeGreaterThan(health.max * 0.25);

				const before = state!;
				const archetype = target!.target.archetype;
				const reward = await readArchetypeReward(sql, archetype);
				expect(reward, `${archetype} must be in the imported catalogue`).not.toBeNull();
				expect(reward!.lootTable, `${archetype} must bind an imported loot table`).toBeTruthy();

				await target!.row.getByRole('button', { name: 'Engage' }).click();
				const outcome = await fightToVictory(page);

				if (outcome === 'defeat') {
					// Not the assertion this spec is about; the encounter was simply lost. Respawn puts the
					// character at the zone's SafeRespawn location, and the walk back re-reads the roster.
					await page.getByRole('button', { name: 'Respawn' }).click();
					expect(await walkTo(page, MAP_NODES.encounter)).toBe(true);
					continue;
				}

				const after = await readCharacterState(sql, characterId);
				expect(after, 'the character row must still exist after a kill').not.toBeNull();

				// The reward path, checked against the content the import wrote: exact experience, gold inside the
				// archetype's own band.
				expect(
					after!.experience - before.experience,
					`${archetype} pays its experience reward`
				).toBe(reward!.experience);

				const gold = after!.gold - before.gold;
				expect(gold, `${archetype} pays gold inside its authored range`).toBeGreaterThanOrEqual(
					reward!.goldMin
				);
				expect(gold, `${archetype} pays gold inside its authored range`).toBeLessThanOrEqual(
					reward!.goldMax
				);

				// Whatever landed must be an entry of that archetype's imported table, in that entry's quantity
				// bounds. Which entries land is the production roll and is deliberately not pinned.
				const table = await readLootTable(sql, reward!.lootTable!);
				const added = bagDiff(before.bag, after!.bag);
				for (const row of added) {
					const entry = table.find((candidate) => candidate.itemSlug === row.itemSlug);
					expect(
						entry,
						`${row.itemSlug} is not an entry of ${reward!.lootTable}, which is all ${archetype} may drop`
					).toBeTruthy();
					expect(row.quantity).toBeGreaterThanOrEqual(entry!.quantityMin);
					expect(row.quantity).toBeLessThanOrEqual(entry!.quantityMax);
				}

				kills.push({ archetype, added });
				state = after;

				if (added.length > 0) payment = { archetype, table: reward!.lootTable! };
			}

			expect(
				payment,
				`no kill of the pool dropped an item in ${kills.length} kills (${POOL_ARCHETYPES.join(', ')})`
			).not.toBeNull();

			// ── The reward is in the bag, on screen ────────────────────────────────────────────────────
			const dropped = kills.flatMap((kill) => kill.added);
			expect(dropped.length, 'a paid kill must have added an item').toBeGreaterThan(0);

			const names = await readItemNames(
				sql,
				dropped.map((row) => row.itemSlug)
			);

			const sheet = page.getByRole('complementary', { name: 'Character sheet' });
			for (const row of dropped) {
				const label = names[row.itemSlug] ?? row.itemSlug;
				await expect(sheet.locator('.bag-row', { hasText: label }).first()).toBeVisible({
					timeout: 20_000
				});
			}

			// ── The waypost: the zone's SafeRespawn point, and no random pool ──────────────────────────
			expect(await walkTo(page, MAP_NODES.safe)).toBe(true);
			await expect(page.locator('button[aria-current="location"]')).toContainText(
				'The Weathered Waypost'
			);

			expect(
				await readRoster(sql, SAFE_LOCATION),
				'an excluded location must spawn nothing: no pool, no placements'
			).toEqual([]);

			// ── And back, the way the journey came ─────────────────────────────────────────────────────
			expect(await walkTo(page, MAP_NODES.encounter)).toBe(true);
			expect(await walkTo(page, MAP_NODES.arrival)).toBe(true);
			expect(await walkTo(page, MAP_NODES.outOfZone)).toBe(true);
			expect(await walkTo(page, MAP_NODES.townSquare)).toBe(true);
			await expect(page.locator('button[aria-current="location"]')).toContainText(
				'Crestfall Square'
			);

			// The walk home changes nothing about what the kill paid.
			for (const row of dropped) {
				const label = names[row.itemSlug] ?? row.itemSlug;
				await expect(sheet.locator('.bag-row', { hasText: label }).first()).toBeVisible({
					timeout: 20_000
				});
			}
		} finally {
			await sql.end({ timeout: 5 });
		}
	});
});
