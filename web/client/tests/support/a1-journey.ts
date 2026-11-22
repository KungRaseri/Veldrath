import { expect, type Locator, type Page } from '@playwright/test';
import postgres from 'postgres';
import { walkTo } from './map';

/**
 * The A1 journey's expectations and its isolated-state reader.
 *
 * The journey proves one thing: a spawn pool that lives in a content pack (`content/zonelocations/ashlen-wood.yaml`)
 * reaches a real character as a real encounter and a real reward. So the spec needs two kinds of truth —
 *
 *  1. the **authored** expectation, which is the design's (`docs/design/content/pools/ashlen-wood.yaml`,
 *     pool-matrix.md §8) and is written out here so a drift in the pack fails against a value, not against nothing;
 *  2. the **stored** state of the isolated stack, which is the only honest way to say "the catalogue was imported"
 *     and "the roster came from that pool" — a browser can see an enemy's name and a bag row, never its archetype
 *     slug or the weights behind it.
 *
 * The database here is the throwaway stack (`scripts/e2e-stack.sh`, Postgres on 55432), never a development or
 * production catalogue. Reads only: every write in this spec goes through the client and the real server.
 */

/** A pool entry as both the design and the pack author it: a relative integer weight, never a percentage. */
export interface PoolEntry {
	archetype: string;
	weight: number;
}

/**
 * The zone's pools, entry for entry from `content/zonelocations/ashlen-wood.yaml`.
 *
 * Order matters on purpose: the importer compares an owned collection in its authored order, so a re-ordered pool
 * would be an update — the re-import check in `scripts/e2e-stack.sh catalogue` is what catches that, and this
 * constant is the value the stored row is compared against.
 */
export const ASHLEN_WOOD_POOLS: Record<string, PoolEntry[]> = {
	'wood-road': [
		{ archetype: 'goblin-scout', weight: 6 },
		{ archetype: 'forest-wolf', weight: 4 }
	],
	'old-clearing': [
		{ archetype: 'forest-wolf', weight: 6 },
		{ archetype: 'goblin-scout', weight: 4 }
	],
	// Empty by design: SafeRespawn + hidden (passive DC 5). A safe island inside a hostile wood.
	'strangled-oak': []
};

/** The four Crestfall locations. All four are a town, an inn, a quarter or the respawn square — none may pool. */
export const CRESTFALL_LOCATIONS = [
	'aurelian-market',
	'crestfall-square',
	'hearthbound-inn',
	'low-quarter'
];

/** Every archetype the zone's pools name — the only thing a pool draw at either location may be. */
export const POOL_ARCHETYPES = ['goblin-scout', 'forest-wolf'];

/** The archetype's display name in the Local panel → its slug, which is what the database stores. */
export const ARCHETYPE_NAMES: Record<string, string> = {
	'Goblin Scout': 'goblin-scout',
	'Forest Wolf': 'forest-wolf'
};

/**
 * The placed instance that stands at old-clearing (`ActorInstancesSeeder`), not a pool draw.
 *
 * It is why the engagement has to be named rather than "the first Engage button": a level-5 instance with 120 health
 * and a damage override is not the pool, and a level-1 character that swings at it learns nothing about the pool.
 */
export const PLACED_INSTANCE = 'elder-goblin-chief';

/** The zone's two pooled locations, and the map walk the journey takes through them. */
export const ZONE_ID = 'ashlen-wood';
export const ENCOUNTER_LOCATION = 'old-clearing';
export const MAP_NODES = {
	arrival: 'Arrival',
	encounter: 'The Old Clearing',
	outOfZone: 'Road to Crestfall',
	townSquare: 'The Crestfall Square',
	woodGate: 'Road to The Ashlen Wood'
};

/** A loot-table row as the imported catalog stores it. */
export interface LootRow {
	itemSlug: string;
	quantityMin: number;
	quantityMax: number;
}

/** What a defeated archetype pays, read from the imported archetype row rather than copied from the pack. */
export interface ArchetypeReward {
	experience: number;
	goldMin: number;
	goldMax: number;
	lootTable: string | null;
}

/** A bag row: the slug the drop is keyed by, and how many the character carries. */
export interface BagRow {
	itemSlug: string;
	quantity: number;
}

/** One living enemy at a location, as the server persisted it. */
export interface LiveEnemy {
	archetypeSlug: string;
	instanceSlug: string | null;
	name: string;
}

/**
 * The stack's Postgres, resolved the way `tests/support/stack.ts` resolves it.
 *
 * `DATABASE_URL` is deliberately the second choice: in CI it names the job's service container, and locally it is
 * usually unset — and when it is set for the unit suite it points at a host (`postgres`) that does not exist here.
 * `E2E_DB_URL` is what the authenticated job passes for exactly that reason.
 */
function databaseUrl(): string {
	const port = process.env.E2E_DB_PORT ?? '55432';
	return (
		process.env.E2E_DB_URL ??
		process.env.DATABASE_URL ??
		`postgres://veldrath:veldrath_dev@localhost:${port}/veldrath`
	);
}

/** Opens a single-connection reader over the stack's database. The caller closes it. */
export function openA1Database() {
	return postgres(databaseUrl(), { max: 1, onnotice: () => {} });
}

type Sql = ReturnType<typeof openA1Database>;

/**
 * Reads the persisted pool of every location in a zone, keyed by location slug.
 *
 * `ActorPool` is owned JSON on the row (`ZoneLocations.ActorPool`, a jsonb column), which is why this reads the column
 * instead of joining a child table: the pool *is* the row's authored value, and the importer replaces it wholesale.
 *
 * @param sql The open reader.
 * @param zoneId Zone to read.
 * @returns Pool entries by location slug; an empty array means the location pools nothing.
 */
export async function readZonePools(sql: Sql, zoneId: string): Promise<Record<string, PoolEntry[]>> {
	const rows = await sql<{ slug: string; pool: unknown }[]>`
		SELECT "Slug" AS slug, coalesce("ActorPool", '[]'::jsonb) AS pool
		FROM "ZoneLocations"
		WHERE "ZoneId" = ${zoneId}
	`;

	const pools: Record<string, PoolEntry[]> = {};

	for (const row of rows) {
		const entries = (row.pool ?? []) as { ArchetypeSlug?: string; Weight?: number }[];
		pools[row.slug] = entries.map((entry) => ({
			archetype: entry.ArchetypeSlug ?? '',
			weight: entry.Weight ?? 0
		}));
	}

	return pools;
}

/**
 * Reads what a defeated archetype pays, from the row the import wrote.
 *
 * The pack does not carry these numbers — the archetype content does (`content/archetypes/` binds the loot table, the
 * band packs carry the stat line). Reading them back is what makes the assertion "the reward the imported content
 * promises" instead of a number the spec copied from a file.
 *
 * @param sql The open reader.
 * @param slug Archetype slug.
 * @returns Its reward line, or `null` when the archetype is not in the catalog.
 */
export async function readArchetypeReward(sql: Sql, slug: string): Promise<ArchetypeReward | null> {
	const rows = await sql<{ stats: Record<string, number | null>; table: string | null }[]>`
		SELECT a."Stats" AS stats, t."Slug" AS table
		FROM "ActorArchetypes" a
		LEFT JOIN "LootTables" t ON t."Id" = a."LootTableId"
		WHERE a."Slug" = ${slug}
	`;

	const row = rows[0];
	if (!row) return null;

	return {
		experience: Number(row.stats?.ExperienceReward ?? 0),
		goldMin: Number(row.stats?.GoldRewardMin ?? 0),
		goldMax: Number(row.stats?.GoldRewardMax ?? 0),
		lootTable: row.table
	};
}

/**
 * Reads one loot table's rows — the only items a kill of its archetype may produce, with their quantity bounds.
 *
 * @param sql The open reader.
 * @param tableSlug Loot-table slug.
 * @returns The table's rows; empty when the table has none.
 */
export async function readLootTable(sql: Sql, tableSlug: string): Promise<LootRow[]> {
	const rows = await sql<
		{ item: string; min: number; max: number }[]
	>`
		SELECT e."ItemSlug" AS item, e."QuantityMin" AS min, e."QuantityMax" AS max
		FROM "LootTableEntries" e
		JOIN "LootTables" t ON t."Id" = e."LootTableId"
		WHERE t."Slug" = ${tableSlug}
	`;

	return rows.map((row) => ({
		itemSlug: row.item,
		quantityMin: Number(row.min),
		quantityMax: Number(row.max)
	}));
}

/**
 * Reads a character's experience, gold and bag.
 *
 * Gold lives in the attributes blob (`CombatHelpers.DistributeRewards` writes `Gold` there), experience is its own
 * column, and the bag is `InventoryBlob` — the same store the sheet renders, which is why a drop that missed it would
 * be a real defect rather than a test artefact.
 *
 * @param sql The open reader.
 * @param characterId Character to read.
 * @returns Experience, gold and the bag; `null` when the row is gone.
 */
export async function readCharacterState(
	sql: Sql,
	characterId: string
): Promise<{ experience: number; gold: number; bag: BagRow[] } | null> {
	const rows = await sql<
		{ experience: string | number; attributes: string; bag: string }[]
	>`
		SELECT "Experience" AS experience, "Attributes" AS attributes, "InventoryBlob" AS bag
		FROM "Characters"
		WHERE "Id" = ${characterId}
	`;

	const row = rows[0];
	if (!row) return null;

	// Both blobs are `text` columns holding JSON (`CharacterBag` and `CombatHelpers.SerializeAttrs` write them), so
	// they arrive as strings and have to be parsed rather than read as a jsonb column would be.
	const attributes = parseJson<Record<string, number>>(row.attributes, {});
	const bag = parseJson<{ ItemRef?: string; Quantity?: number }[]>(row.bag, []);

	return {
		experience: Number(row.experience),
		gold: Number(attributes.Gold ?? 0),
		bag: bag.map((entry) => ({ itemSlug: entry.ItemRef ?? '', quantity: Number(entry.Quantity ?? 0) }))
	};
}

/**
 * Parses a stored text blob, falling back to a default instead of failing the spec on an empty column.
 *
 * @param blob The stored text.
 * @param fallback Value to use when the column is empty or unreadable.
 * @returns The parsed value.
 */
function parseJson<T>(blob: string | null, fallback: T): T {
	if (!blob || blob.trim() === '') return fallback;

	try {
		return JSON.parse(blob) as T;
	} catch {
		return fallback;
	}
}

/**
 * Reads the living roster the server persisted at a location.
 *
 * This is the evidence a browser cannot give: the Local panel shows a name and a level, while the row carries the
 * archetype slug the pool entry named and, for a placement, the instance slug it came from.
 *
 * @param sql The open reader.
 * @param locationSlug Location to read.
 * @returns The living enemies there.
 */
export async function readLiveRoster(sql: Sql, locationSlug: string): Promise<LiveEnemy[]> {
	const rows = await sql<
		{ archetype: string; instance: string | null; name: string }[]
	>`
		SELECT "ArchetypeSlug" AS archetype, "InstanceSlug" AS instance, "Name" AS name
		FROM "EnemyInstances"
		WHERE "LocationSlug" = ${locationSlug} AND "IsAlive" = true
		ORDER BY "ArchetypeSlug"
	`;

	return rows.map((row) => ({
		archetypeSlug: row.archetype,
		instanceSlug: row.instance,
		name: row.name
	}));
}

/**
 * Compares two bag snapshots and returns what was added, slug by slug.
 *
 * A kill may stack onto an item the character already carried (a health potion from a starting kit, say), so the
 * difference — not the bag itself — is the drop.
 *
 * @param before Bag before the fight.
 * @param after Bag after the fight.
 * @returns The added quantity per item slug.
 */
export function bagDiff(before: BagRow[], after: BagRow[]): BagRow[] {
	const baseline = new Map(before.map((row) => [row.itemSlug, row.quantity]));
	const added: BagRow[] = [];

	for (const row of after) {
		const delta = row.quantity - (baseline.get(row.itemSlug) ?? 0);
		if (delta > 0) added.push({ itemSlug: row.itemSlug, quantity: delta });
	}

	return added;
}

/**
 * Resolves item slugs to the display names the character sheet renders.
 *
 * The bag row's label is the catalog's `DisplayName` (`itemLabel` in `types.ts`), so this is what ties a persisted drop
 * slug to the row a player sees — the DOM assertion and the database assertion then describe the same object.
 *
 * @param sql The open reader.
 * @param slugs Item slugs to resolve.
 * @returns Display name by slug; a slug the catalog does not know is left out.
 */
export async function readItemNames(sql: Sql, slugs: string[]): Promise<Record<string, string>> {
	if (slugs.length === 0) return {};

	const rows = await sql<{ slug: string; name: string }[]>`
		SELECT "Slug" AS slug, "DisplayName" AS name
		FROM "Items"
		WHERE "Slug" IN ${sql(slugs)}
	`;

	return Object.fromEntries(rows.map((row) => [row.slug, row.name]));
}

/**
 * Builds the character the journey walks with, through the real seven-step wizard.
 *
 * The class and species are chosen rather than taken from the top of the list, and the point-buy budget goes into
 * Strength and Constitution ahead of everything else, because what the journey needs is a level-1 character that can
 * survive the encounter it is about to pick: the zone's own floor is level 1 and its `old-clearing` roster carries a
 * level-5 placed instance beside the pool draws. That is a player's choice, not a test-only shortcut — nothing here
 * writes to the server, and the fight is the production fight.
 *
 * The name is generated: the wizard refuses a duplicate, and every run of this spec creates an account.
 *
 * @param page A signed-in page.
 * @param name A wizard-acceptable character name (letters and spaces, 2–20 characters).
 */
export async function createDwarfWarrior(page: Page, name: string): Promise<void> {
	await page.goto('/characters/new');

	await page.getByLabel('Character name').fill(name);
	await expect(page.getByText('That name is available.')).toBeVisible({ timeout: 20_000 });
	await page.getByRole('button', { name: 'Continue' }).click();

	// Class: Warrior — the melee class, whose attack does not depend on mana the way a mage's does.
	await expect(page.getByText(/Step 2 of 7/)).toBeVisible({ timeout: 20_000 });
	await page.getByRole('button', { name: 'Warrior', exact: true }).click();
	await page.getByRole('button', { name: 'Continue' }).click();

	// Species: Dwarf (+2 Constitution, +1 Strength) — the durable choice the pools' level range wants.
	await expect(page.getByText(/Step 3 of 7/)).toBeVisible({ timeout: 20_000 });
	await page.getByRole('button', { name: /^Dwarf/ }).click();
	await page.getByRole('button', { name: 'Continue' }).click();

	// Background: Soldier (+2 Strength, +1 Constitution) — the same two stats again.
	await expect(page.getByText(/Step 4 of 7/)).toBeVisible({ timeout: 20_000 });
	await page.getByRole('button', { name: /^Soldier/ }).click();
	await page.getByRole('button', { name: 'Continue' }).click();

	// Attributes: spend the whole budget, Strength and Constitution first, then anywhere that still accepts a point.
	await expect(page.getByText(/Step 5 of 7/)).toBeVisible({ timeout: 20_000 });
	for (let pass = 0; pass < 4; pass += 1) {
		for (const stat of ['Strength', 'Constitution', 'Dexterity', 'Intelligence', 'Wisdom', 'Charisma']) {
			const increase = page.getByRole('button', { name: `Increase ${stat}` });
			for (let click = 0; click < 20 && (await increase.isEnabled()); click += 1) {
				await increase.click();
			}
		}

		if ((await page.getByText(/Spend every point to continue/).count()) === 0) break;
	}

	await expect(page.getByText(/Spend every point to continue/)).toHaveCount(0);
	await page.getByRole('button', { name: 'Continue' }).click();

	// Equipment keeps the class kit, and the review step creates the character into the world.
	await expect(page.getByText(/Step 6 of 7/)).toBeVisible({ timeout: 20_000 });
	await page.getByRole('button', { name: 'Continue' }).click();
	await expect(page.getByText(/Step 7 of 7/)).toBeVisible({ timeout: 20_000 });
	await page.getByRole('button', { name: 'Create character' }).click();

	// Creating drops the player straight into the world, so the assertion is the world, not the roster.
	await expect(page).toHaveURL(/\/play\/[0-9a-f-]{36}$/, { timeout: 30_000 });
	await expect(page.locator('.map-node-cell button').first()).toBeVisible({ timeout: 30_000 });
}

/**
 * Reads the character's current and maximum health off the sheet.
 *
 * Health is the journey's only budget: every fight costs it and nothing in the wood heals it, so the loop that keeps
 * looking for a drop has to watch it rather than discover the limit by dying.
 *
 * @param page The game page.
 * @returns Current and maximum health, as the HUD shows them.
 */
export async function readHealth(page: Page): Promise<{ current: number; max: number }> {
	const sheet = page.getByRole('complementary', { name: 'Character sheet' });
	const text = await sheet.innerText();
	const match = text.match(/Health\s*(\d+)\s*\/\s*(\d+)/);

	if (!match) throw new Error('the character sheet did not show a health value');

	return { current: Number(match[1]), max: Number(match[2]) };
}

/**
 * Finds the Local panel's row for one of the zone's pool archetypes and returns what engaging it would mean.
 *
 * The panel lists the whole roster, placed instances included, and the rows carry no archetype slug — only the name.
 * So the slug comes from the name, through {@link ARCHETYPE_NAMES}, and anything that is not a pool archetype is
 * skipped rather than guessed at: that includes the level-5 instance standing at `old-clearing`, which is content but
 * not pool content.
 *
 * @param page The game page.
 * @returns The row locator, its slug and its display name, or `null` when no pool enemy is standing here.
 */
export async function findPoolEnemy(
	page: Page
): Promise<{ row: Locator; archetype: string; name: string } | null> {
	const local = page.locator('section', { has: page.getByRole('heading', { name: 'Local' }) });
	const rows = local.locator('li', { has: page.getByRole('button', { name: 'Engage' }) });

	const count = await rows.count();
	for (let index = 0; index < count; index += 1) {
		const row = rows.nth(index);
		const text = (await row.innerText()).trim();

		for (const [displayName, archetype] of Object.entries(ARCHETYPE_NAMES)) {
			if (text.includes(displayName)) return { row, archetype, name: displayName };
		}
	}

	return null;
}

/**
 * Fights the enemy the caller already engaged, to a conclusion.
 *
 * Attack is the whole strategy on purpose: a level-1 character's power list costs mana it cannot spare, and the point
 * of the assertion is the kill and its reward, not the tactics.
 *
 * @param page The game page.
 * @returns How the fight ended.
 */
export async function fightToVictory(page: Page): Promise<'victory' | 'defeat'> {
	const combat = page.getByRole('heading', { name: /Combat/ });
	const defeat = page.getByText('You have been defeated.').first();

	await expect(combat).toBeVisible({ timeout: 20_000 });

	for (let round = 0; round < 80; round += 1) {
		if (!(await combat.isVisible().catch(() => false))) break;
		if (await defeat.isVisible().catch(() => false)) break;

		const attack = page.getByRole('button', { name: /^Attack$/ });
		if (await attack.isVisible().catch(() => false)) await attack.click();

		await page.waitForTimeout(300);
	}

	await expect(combat).toBeHidden({ timeout: 20_000 });

	return (await defeat.isVisible().catch(() => false)) ? 'defeat' : 'victory';
}

/**
 * Walks back to the arrival point and returns, which is how a fresh roster reaches the HUD.
 *
 * The server rolls a location's roster once and keeps the rows, and the respawn sweeper revives a defeated row after
 * its delay — so "kill, step out, step back" is the smallest honest way to meet the same pool again. The isolated
 * stack shortens that delay (`Enemies__*` in `scripts/e2e-stack.sh`); production keeps the 60s/180s windows.
 *
 * @param page The game page.
 */
export async function refreshRoster(page: Page): Promise<void> {
	await walkTo(page, MAP_NODES.arrival);
	await walkTo(page, MAP_NODES.encounter);
}

/**
 * Waits for a pool archetype to be standing in the Local panel, stepping out and back as needed.
 *
 * A revived row reaches the panel only when the client re-reads the location, and the respawn sweeper is on its own
 * clock — so this refreshes, then polls, rather than assuming one round-trip is enough.
 *
 * @param page The game page.
 * @param attempts How many refresh/poll cycles to allow.
 * @returns The row to engage, or `null` when no pool enemy appeared.
 */
export async function waitForPoolEnemy(
	page: Page,
	attempts = 6
): Promise<{ row: Locator; archetype: string; name: string } | null> {
	for (let attempt = 0; attempt < attempts; attempt += 1) {
		const found = await findPoolEnemy(page);
		if (found) return found;

		await refreshRoster(page);
		await page.waitForTimeout(1_500);
	}

	return findPoolEnemy(page);
}


