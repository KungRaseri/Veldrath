import { randomUUID } from 'node:crypto';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { expect, test, type Locator, type Page } from '@playwright/test';
import {
	bagDiff,
	createDwarfWarrior,
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
import {
	apiBase,
	gameToken,
	makeAccount,
	register,
	stackEnabled,
	stackSkipReason
} from './support/stack';

/**
 * A1 — Grevenmire plays, from a content pack to a character's bag.
 *
 * The zone's spawn pools are authored content again (`content/zonelocations/grevenmire.yaml`, translated from
 * `docs/design/content/pools/grevenmire.yaml` by pool-matrix.md §8), and a pack only becomes real when it is
 * imported — which `scripts/e2e-stack.sh` does after the API seeds the catalogue. So this spec is the whole chain in
 * one run, against the real client, the real hub and a real Postgres:
 *
 *   Crestfall Square → the Ashlen Wood → Grevenmire → The Mire Crossing → an encounter drawn from the imported pool,
 *   its loot in the bag → The Sunken Estate, the zone's SafeRespawn point → back to Crestfall Square.
 *
 * What it asserts, and why each one is the honest shape:
 *
 *  - **The pools are the pack's.** Read from `ZoneLocations.ActorPool`, entry for entry with their weights and in the
 *    pack's authored order. A browser cannot see a weight, and a pool that is not stored is not the pool the spec
 *    walked into. This is also the import's fingerprint: the seeder's interim pools are two entries written as
 *    percentage-looking pairs (`mire-crossing`: bandit-ruffian 60 / forest-wolf 40; `bone-hollow`: moor-wight 60 /
 *    bandit-ruffian 40), so a four-entry pool naming `peat-adder` and `mire-boar` cannot have come from anywhere but
 *    the pack.
 *  - **Every pooled reference resolves, and fits its location.** Each of the five archetypes the pack names exists,
 *    is hostile, binds an imported loot table with entries, and overlaps the level band of the location that pools it.
 *    That is the design's own rule checked against the *imported* catalogue rather than the seed.
 *  - **The roster came from the pool.** The persisted `EnemyInstances` rows at the Mire Crossing name their archetype,
 *    so every enemy a player can meet there is provably one the imported pool allows — and the roster size is the
 *    resolver's 2–4 roll (`ActorPoolResolver.MinCount`/`MaxCount`). `mire-crossing` has no authored placement, so
 *    every row is a pool draw.
 *  - **The reward is the imported content's.** Experience is exact and gold is the archetype's range, both read back
 *    from the row the import wrote; every item added to the bag is an entry of that archetype's imported loot table,
 *    within that entry's quantity bounds. Nothing is mocked and no drop rate is touched: which entries land is the
 *    production roll, so the spec keeps fighting the pool until one kill pays — the isolated stack only shortens the
 *    respawn window (`Enemies__*` in `scripts/e2e-stack.sh`), never the drop chance.
 *  - **The Sunken Estate is quiet.** It is the zone's `SafeRespawn` point and the design excludes it: its stored pool is
 *    empty and nothing hostile stands there, whatever the character does. A death anywhere in the zone also lands the
 *    character here, which is the trait proving itself rather than a field read back.
 *
 * ## The character is prepared, and why
 *
 * Grevenmire's floor is level 3 and its pool is band 1–6: a mire-boar has 95 health, hits for 12 and is authored at
 * level 4. A freshly rolled level-1 wizard character has 120 health, swings for 8 and therefore dies with the boar on
 * 15 health — measured on this stack, not assumed. So the journey uses the "prepared level-appropriate character" the
 * card allows instead of repeating character creation and a grind:
 *
 *  1. character creation is the real seven-step wizard, so the species/background point buy is the game's;
 *  2. its level is raised through the real `GainExperience` hub command — the same handler a kill pays through
 *     (`GameHub.GainExperience` → `GainExperienceHubCommand`) — to the top of Grevenmire's own authored band
 *     (`3–6`). The level is then what the server says it is, and the sheet the journey walks with reads it.
 *
 * Nothing writes the database directly and no response is mocked: every fight and every reward after the preparation
 * is the production path. The browser is stepped off the play route first because the shell holds the character's
 * single active claim (`GameHub.SelectCharacter` → `IActiveCharacterTracker`) and would reject a second one; leaving
 * the route stops the hub cleanly, which releases the claim.
 *
 * Two things are deliberately *not* asserted, because pinning them would pin the dice rather than the content:
 *
 *  - **A specific archetype at the Mire Crossing.** A location's roster is one weighted roll per zone scope, created
 *    once and revived — not re-rolled — by the respawn sweeper (`EfCoreEnemyInstanceStore.GetAliveOrSpawnAsync` spawns
 *    only when the location has no rows at all, and `SweepExpiredRespawnsAsync` flips `IsAlive` back on the same rows).
 *    The zone scope is shared by every character in the zone (`zone:grevenmire_normal`, `ZoneGroupName`), so demanding
 *    "a mire-boar appears" would be a coin flip. What the spec demands instead is that *every* draw is a member of the
 *    imported pool, and it prefers a `mire-boar` — the card names it, and it is one of the two archetypes the seeder's
 *    interim pool never named — whenever the roll produced one and the bar is full.
 *  - **The Bone Hollow on foot.** It is pooled by the pack (checked from the stored row), but it is hidden behind a
 *    passive DC 8 check, so walking into it is a different slice's journey — the same reason the Ashlen Wood and
 *    Droveway specs leave their hidden locations to the stored-pool check.
 *  - **The exact route beyond the zone.** Grevenmire has no zone exit from Crestfall; the walk in goes through the
 *    Ashlen Wood's old clearing, which is the route the graph actually offers. The spec walks it as a player would and
 *    asserts the arrival, not the region map.
 */

/** The zone's pools, entry for entry from `content/zonelocations/grevenmire.yaml`, in the pack's authored order. */
const GREVENMIRE_POOLS: Record<string, PoolEntry[]> = {
	'mire-crossing': [
		{ archetype: 'peat-adder', weight: 4 },
		{ archetype: 'mire-boar', weight: 4 },
		{ archetype: 'bandit-ruffian', weight: 3 },
		{ archetype: 'forest-wolf', weight: 2 }
	],
	'bone-hollow': [
		{ archetype: 'bandit-ruffian', weight: 5 },
		{ archetype: 'mire-boar', weight: 3 },
		{ archetype: 'moor-wight', weight: 1 }
	],
	// Empty by design: the zone's SafeRespawn point. Nothing spawns here, and the design keeps it that way.
	'sunken-estate': []
};

/** Every archetype the zone's pools name — the only thing a pool draw at either location may be. */
const POOL_ARCHETYPES = ['peat-adder', 'mire-boar', 'bandit-ruffian', 'forest-wolf', 'moor-wight'];

/**
 * The archetypes in the order a wounded character fights them.
 *
 * This is a player's targeting, not a test trick: at level 6 the character swings for 11, while the pool's authored
 * damage is 2 (peat-adder), 5 (forest-wolf), 6 (bandit-ruffian) and 12 (mire-boar) — and nothing in Grevenmire heals
 * (the zone has no inn, so `RestAtLocation` is not offered). Cheapest first is the order that keeps the character
 * alive long enough for the pool to pay.
 */
const FIGHT_ORDER = ['peat-adder', 'forest-wolf', 'bandit-ruffian', 'mire-boar', 'moor-wight'];

/** The card's preferred representative encounter — fought first, while the bar is still full. */
const PREFERRED_ARCHETYPE = 'mire-boar';

/** The top of Grevenmire's authored level band (`minLevel: 3`, `maxLevel: 6`). */
const PREPARED_LEVEL = 6;

/** Experience the rolling `Level × 100` formula needs to reach {@link PREPARED_LEVEL}: 100+200+300+400+500. */
const PREPARED_EXPERIENCE = 1500;

/** How many fights the journey may take to find a paying kill, deaths included. */
const FIGHT_BUDGET = 10;

/** The zone's two pooled locations, its excluded one, and the map walk between them. */
const ZONE_ID = 'grevenmire';
const ENCOUNTER_LOCATION = 'mire-crossing';
const HIDDEN_LOCATION = 'bone-hollow';
const SAFE_LOCATION = 'sunken-estate';

const MAP_NODES = {
	townSquare: 'The Crestfall Square',
	woodGate: 'Road to The Ashlen Wood',
	oldClearing: 'The Old Clearing',
	zoneGate: 'Path to Grevenmire',
	encounter: 'The Mire Crossing',
	safe: 'The Sunken Estate',
	zoneExit: 'Track to Ashlen Wood',
	homeGate: 'Road to Crestfall'
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
 * `EnemyInstances` is shared by every character in the zone scope, so a row with a participant is somebody's current
 * fight; killing it would end their combat. The spec treats those as off limits rather than racing them.
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
	discoverThreshold: number;
}

/**
 * Reads a zone location's level band and the traits this journey turns on.
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
		hidden: Boolean(traits.IsHidden ?? traits.isHidden ?? false),
		discoverThreshold: Number(traits.DiscoverThreshold ?? traits.discoverThreshold ?? 0)
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

/**
 * Raises the freshly created character to the zone's own top level through the real experience hub.
 *
 * `GainExperienceHubCommand` is exactly what a kill pays through, so the level it produces is the level the game
 * produces — no row is written here and no payload is faked. The connection exists only because the browser never
 * calls this method (its parity allow-list records "Awarded by combat and quest handlers, never by the browser"); it
 * closes cleanly afterwards, which releases the character claim the shell needs back.
 *
 * @param characterId Character to prepare.
 * @param token The session's short-lived game token, minted by the web app.
 * @returns Nothing; the caller re-reads the level from the database.
 */
async function prepareJourneyCharacter(characterId: string, token: string): Promise<void> {
	const connection = new HubConnectionBuilder()
		.withUrl(`${apiBase}/hubs/game`, { accessTokenFactory: () => token })
		.configureLogging(LogLevel.None)
		.build();

	let alreadyActive: boolean | undefined;
	connection.on('CharacterAlreadyActive', () => {
		alreadyActive = true;
	});

	await connection.start();

	try {
		// Leaving the play route stops the shell's hub, but the release is a round trip — so claim, and retry
		// while the previous connection is still the recorded holder.
		let selected = false;
		for (let attempt = 0; attempt < 15 && !selected; attempt += 1) {
			alreadyActive = false;
			await connection.invoke('SelectCharacter', characterId);
			selected = !alreadyActive;

			if (!selected) await new Promise((resolve) => setTimeout(resolve, 1_000));
		}

		expect(selected, 'the preparation connection must be able to claim the character').toBe(true);

		await connection.invoke('GainExperience', {
			amount: PREPARED_EXPERIENCE,
			source: 'A1 journey preparation'
		});
	} finally {
		await connection.stop().catch(() => undefined);
	}
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
 * same archetype are interchangeable, and "the last one" keeps this spec off the row `encounters.e2e.ts` would take
 * (it always takes the *first* Engage button), should another spec ever stand here.
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
 * Picks the enemy to fight.
 *
 * With a full bar the character takes the card's representative encounter, a mire-boar, when the roll produced one.
 * Once the bar is no longer full it takes the cheapest draw standing ({@link FIGHT_ORDER}), because the pool's four
 * archetypes hit for 2, 5, 6 and 12 and nothing in Grevenmire heals. "Free" means no character is already in that
 * fight — see {@link readRoster}; the Mire Crossing has no authored placements, so every row is a pool draw, and the
 * membership filter is what keeps a later placement from quietly becoming this spec's target.
 *
 * @param roster The living roster.
 * @param health The character's current and maximum health.
 * @returns The row to engage, or `null` when every draw is taken or there is none.
 */
function pickTarget(
	roster: RosterRow[],
	health: { current: number; max: number }
): RosterRow | null {
	const free = roster.filter(
		(row) => row.participants === 0 && POOL_ARCHETYPES.includes(row.archetype)
	);
	if (free.length === 0) return null;

	const atFullStrength = health.current >= health.max * 0.9;
	const boar = free.filter((row) => row.archetype === PREFERRED_ARCHETYPE);
	if (atFullStrength && boar.length > 0) return boar[boar.length - 1];

	for (const archetype of FIGHT_ORDER) {
		const row = free.find((candidate) => candidate.archetype === archetype);
		if (row) return row;
	}

	return free[free.length - 1];
}

/**
 * Waits for a free pool draw to be standing at the Mire Crossing, stepping out and back as needed.
 *
 * The respawn sweeper is on its own clock (2s in the isolated stack), and a revived row reaches the Local panel only
 * when the client re-reads the location — so this refreshes, then polls, rather than assuming one round-trip is enough.
 *
 * @param page The game page.
 * @param sql The open reader.
 * @param health The character's current and maximum health.
 * @param attempts How many refresh/poll cycles to allow.
 * @returns The panel row to engage and the roster row behind it, or `null` when nothing free appeared.
 */
async function waitForTarget(
	page: Page,
	sql: Sql,
	health: { current: number; max: number },
	attempts = 8
): Promise<{ row: Locator; target: RosterRow } | null> {
	for (let attempt = 0; attempt < attempts; attempt += 1) {
		const target = pickTarget(await readRoster(sql, ENCOUNTER_LOCATION), health);

		if (target) {
			const row = await findRowByName(page, target.name);
			if (row) return { row, target };
		}

		await walkTo(page, MAP_NODES.safe);
		await walkTo(page, MAP_NODES.encounter);
		await page.waitForTimeout(1_500);
	}

	const target = pickTarget(await readRoster(sql, ENCOUNTER_LOCATION), health);
	if (!target) return null;

	const row = await findRowByName(page, target.name);
	return row ? { row, target } : null;
}

/**
 * Takes one turn-based fight to its conclusion, attacking or defending.
 *
 * The shared {@link fightToVictory} helper reads "the combat panel went away" as the only ending, which is true when
 * the *enemy* dies — but a player death leaves the panel on screen with the Respawn banner above it, so a lost fight
 * would fail that helper's final assertion instead of being reported as the loss it is. Here the death banner (or the
 * Respawn action) is the defeat signal and the panel hiding is the victory signal, both taken from the turn the
 * server broadcasts.
 *
 * @param page The game page.
 * @param mode `attack` to try to win, `defend` to lose on purpose.
 * @returns How the fight ended.
 */
async function fight(page: Page, mode: 'attack' | 'defend'): Promise<'victory' | 'defeat'> {
	const combat = page.getByRole('heading', { name: /Combat/ });
	const defeat = page.getByText('You have been defeated.').first();
	const respawn = page.getByRole('button', { name: 'Respawn' });
	const action = page.getByRole('button', {
		name: new RegExp(`^${mode === 'attack' ? 'Attack' : 'Defend'}$`)
	});

	await expect(combat).toBeVisible({ timeout: 20_000 });

	for (let round = 0; round < 80; round += 1) {
		if (await defeat.isVisible().catch(() => false)) break;
		if (await respawn.isVisible().catch(() => false)) break;
		if (!(await combat.isVisible().catch(() => false))) break;
		if (await action.isEnabled().catch(() => false)) await action.click();
		await page.waitForTimeout(300);
	}

	if (await defeat.isVisible().catch(() => false)) return 'defeat';
	if (await respawn.isVisible().catch(() => false)) return 'defeat';

	await expect(combat).toBeHidden({ timeout: 20_000 });
	return 'victory';
}

/**
 * Leaves a defeated character back in the fight.
 *
 * `Respawn` is the zone's own path: it restores a quarter of the bar and puts the character at the SafeRespawn
 * location, which is the Sunken Estate, and the walk back re-reads the Mire Crossing's roster.
 *
 * @param page The game page.
 */
async function respawnAndReturn(page: Page): Promise<void> {
	await page.getByRole('button', { name: 'Respawn' }).click();
	await expect(page.getByRole('button', { name: 'Respawn' })).toBeHidden({ timeout: 20_000 });

	// The death path landing here is the SafeRespawn trait proving itself, not a stored field read back.
	await expect(page.locator('button[aria-current="location"]')).toContainText('The Sunken Estate', {
		timeout: 20_000
	});

	expect(await walkTo(page, MAP_NODES.encounter)).toBe(true);
}

test.describe('A1 — Grevenmire pools', () => {
	test.skip(!stackEnabled, stackSkipReason);
	test.describe.configure({ mode: 'serial' });
	test.setTimeout(600_000);

	test('Crestfall Square → grevenmire → the Mire Crossing → a pool kill pays the imported reward → the Sunken Estate', async ({
		page
	}) => {
		await register(page, makeAccount('e2emire'));

		// The wizard accepts letters and spaces only ("2–20 characters"), so a stamp of digits would be rejected
		// before the availability check even runs.
		const name = `Mire${randomUUID()
			.replace(/[^a-z]/g, '')
			.slice(0, 6)}`.slice(0, 20);

		await createDwarfWarrior(page, name);

		const characterId = page.url().split('/').pop() ?? '';
		expect(characterId, 'the wizard must land the character in the world').toMatch(
			/^[0-9a-f-]{36}$/
		);

		// Step off the play route so the shell's hub closes cleanly and releases the character claim the
		// preparation connection needs, then raise the level through the same hub command combat pays through.
		await page.goto('/characters');
		await prepareJourneyCharacter(characterId, await gameToken(page));
		await page.goto(`/play/${characterId}`);
		await expect(page.locator('.map-node-cell button').first()).toBeVisible({ timeout: 30_000 });

		const sql = openA1Database();

		try {
			// The preparation is only real if the server recorded it: the character the journey walks with is
			// level 6 because `GainExperienceHubCommand` said so, and the shell re-read that row on load.
			const prepared = await readCharacterState(sql, characterId);
			expect(prepared, 'the character row must exist').not.toBeNull();
			expect(
				await readLevel(sql, characterId),
				'the character must be prepared to the zone band'
			).toBe(PREPARED_LEVEL);

			// ── The stored pools ─────────────────────────────────────────────────────────────────────────
			// Read before the walk: this is the catalogue the journey is about to meet, not a report of what it
			// met. A pack that never imported leaves the seeder's interim pairs here — a two-entry 60/40 pool at
			// the Mire Crossing — and this fails at once, which is the negative control for the whole spec.
			const pools = await readZonePools(sql, ZONE_ID);
			expect(Object.keys(pools).sort()).toEqual([
				HIDDEN_LOCATION,
				ENCOUNTER_LOCATION,
				SAFE_LOCATION
			]);

			for (const [slug, expected] of Object.entries(GREVENMIRE_POOLS)) {
				expect(pools[slug], `${slug} must carry the pack's pool, weights included`).toEqual(
					expected
				);
			}

			// ── The pooled references ───────────────────────────────────────────────────────────────────
			// Every archetype the pack names resolves in the imported catalogue, is something that can be
			// fought, binds a loot table with entries, and overlaps the band of the location that pools it.
			for (const [slug, expected] of Object.entries(GREVENMIRE_POOLS)) {
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
			expect(safe, 'the sunken estate must be in the imported catalogue').not.toBeNull();
			expect(safe!.safeRespawn, 'sunken-estate is the zone’s SafeRespawn point').toBe(true);
			expect(pools[SAFE_LOCATION], 'the sunken estate must pool nothing at all').toEqual([]);

			// The other pooled location keeps the trait that keeps it off this walk: it is hidden behind a passive
			// DC 8 check, so its pool is checked from the stored row rather than met on foot.
			const hollow = await readLocation(sql, HIDDEN_LOCATION);
			expect(hollow, 'bone-hollow must be in the imported catalogue').not.toBeNull();
			expect(hollow!.hidden, 'bone-hollow stays the hidden location the pack authored').toBe(true);
			expect(hollow!.discoverThreshold, 'bone-hollow keeps its authored passive DC').toBe(8);

			// ── The walk ────────────────────────────────────────────────────────────────────────────────
			// The wizard already placed the character on the square; walking there is the "already here" no-op
			// that proves the map knows where it is before a zone change paints a different map.
			expect(await walkTo(page, MAP_NODES.townSquare)).toBe(true);
			await expect(page.locator('button[aria-current="location"]')).toContainText(
				'Crestfall Square'
			);

			// Grevenmire has no exit from Crestfall; the ride in goes through the Ashlen Wood's old clearing,
			// which is the route the graph offers a player.
			expect(await walkTo(page, MAP_NODES.woodGate)).toBe(true);
			expect(await walkTo(page, MAP_NODES.oldClearing)).toBe(true);
			expect(await walkTo(page, MAP_NODES.zoneGate)).toBe(true);
			expect(await walkTo(page, MAP_NODES.encounter)).toBe(true);
			await expect(page.locator('button[aria-current="location"]')).toContainText(
				'The Mire Crossing'
			);

			// ── The roster the pool produced ────────────────────────────────────────────────────────────
			// A browser shows the enemy's name; the stored row carries the archetype slug the pool drew. The
			// Mire Crossing has no authored placement, so every row here is a draw.
			//
			// No upper bound on the count: the 2–4 roll (`ActorPoolResolver.MinCount`/`MaxCount`) governs one
			// spawn, and a shared location can hold more rows than that once several characters have arrived —
			// the store spawns whenever it sees no rows, and its unique index is per (archetype, spawn instant).
			// This suite runs its specs in parallel, so a count ceiling here would fail on scheduling, not on
			// content.
			const roster = await readRoster(sql, ENCOUNTER_LOCATION);
			expect(
				roster.length,
				'walking onto the Mire Crossing must spawn its roster from the imported pool'
			).toBeGreaterThanOrEqual(2);

			for (const enemy of roster) {
				expect(
					POOL_ARCHETYPES,
					`${enemy.name} is not one of the zone's pooled archetypes`
				).toContain(enemy.archetype);
				expect(
					enemy.instance,
					'the Mire Crossing authors no placement, so no row may carry an instance slug'
				).toBeNull();
			}

			// ── The fight, until the pool pays ──────────────────────────────────────────────────────────
			let state = prepared!;

			type Kill = { archetype: string; added: { itemSlug: string; quantity: number }[] };
			const kills: Kill[] = [];
			let payment: { archetype: string; table: string } | null = null;
			let respawnedAtSafeLocation = false;

			for (let attempt = 0; attempt < FIGHT_BUDGET && payment === null; attempt += 1) {
				const health = await readHealth(page);
				const target = await waitForTarget(page, sql, health);
				expect(
					target,
					`no pool enemy was free to fight at the Mire Crossing (attempt ${attempt + 1})`
				).not.toBeNull();

				const before = state;
				const archetype = target!.target.archetype;
				const reward = await readArchetypeReward(sql, archetype);
				expect(reward, `${archetype} must be in the imported catalogue`).not.toBeNull();
				expect(reward!.lootTable, `${archetype} must bind an imported loot table`).toBeTruthy();

				// Health is the journey's only budget, and nothing in the zone heals it. Below a quarter of the
				// bar the next victory would be the character's last anyway; take the zone's death path on
				// purpose instead of losing the attempt in the middle of a fight.
				const exhausted = health.current < health.max * 0.25;

				await target!.row.getByRole('button', { name: 'Engage' }).click();
				const outcome = await fight(page, exhausted ? 'defend' : 'attack');

				if (outcome === 'defeat') {
					// Not the assertion this spec is about; the encounter was simply lost. The respawn both
					// exercises the zone's SafeRespawn point and restores a quarter of the bar, and the walk
					// back re-reads the roster.
					await respawnAndReturn(page);
					respawnedAtSafeLocation = true;
					// A death also takes gold or experience (`DeathPenalties`), so the next reward is measured
					// from the row as the respawn left it, not from the pre-death snapshot.
					state = (await readCharacterState(sql, characterId)) ?? state;
					continue;
				}

				const after = await readCharacterState(sql, characterId);
				expect(after, 'the character row must still exist after a kill').not.toBeNull();

				// The reward path, checked against the content the import wrote: exact experience, gold inside the
				// archetype's own band. (At level 6 the next level costs 600, so no kill here can roll the
				// experience column over and make the difference look wrong.)
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
				state = after!;

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

			// ── The Sunken Estate: the zone's SafeRespawn point, and no random pool ────────────────────
			expect(await walkTo(page, MAP_NODES.safe)).toBe(true);
			await expect(page.locator('button[aria-current="location"]')).toContainText(
				'The Sunken Estate'
			);

			expect(
				await readRoster(sql, SAFE_LOCATION),
				'an excluded location must spawn nothing: no pool, no placements'
			).toEqual([]);

			// A death anywhere in the zone is the same claim from the other direction: this character has already
			// been sent here by the server once if the fight loop needed it.
			if (respawnedAtSafeLocation) {
				expect(
					pools[SAFE_LOCATION],
					'the respawn point is still the poolless safe location'
				).toEqual([]);
			}

			// ── And back, the way the journey came ─────────────────────────────────────────────────────
			expect(await walkTo(page, MAP_NODES.encounter)).toBe(true);
			expect(await walkTo(page, MAP_NODES.zoneExit)).toBe(true);
			expect(await walkTo(page, MAP_NODES.homeGate)).toBe(true);
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

/**
 * Reads a character's level from the row the experience hub wrote.
 *
 * @param sql The open reader.
 * @param characterId Character to read.
 * @returns The stored level, or 0 when the row is gone.
 */
async function readLevel(sql: Sql, characterId: string): Promise<number> {
	const rows = await sql<{ level: number }[]>`
		SELECT "Level" AS level FROM "Characters" WHERE "Id" = ${characterId}
	`;

	return Number(rows[0]?.level ?? 0);
}
