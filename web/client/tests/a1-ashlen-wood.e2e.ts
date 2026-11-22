import { randomUUID } from 'node:crypto';
import { expect, test } from '@playwright/test';
import {
	ASHLEN_WOOD_POOLS,
	CRESTFALL_LOCATIONS,
	ENCOUNTER_LOCATION,
	MAP_NODES,
	PLACED_INSTANCE,
	POOL_ARCHETYPES,
	ZONE_ID,
	bagDiff,
	createDwarfWarrior,
	fightToVictory,
	openA1Database,
	readArchetypeReward,
	readCharacterState,
	readHealth,
	readItemNames,
	readLiveRoster,
	readLootTable,
	readZonePools,
	waitForPoolEnemy
} from './support/a1-journey';
import { walkTo } from './support/map';
import { makeAccount, register, stackEnabled, stackSkipReason } from './support/stack';

/**
 * A1 — the Ashlen Wood plays, from a content pack to a character's bag.
 *
 * The zone's spawn pools are authored content again (`content/zonelocations/ashlen-wood.yaml`, translated from
 * `docs/design/content/pools/ashlen-wood.yaml` by pool-matrix.md §8), and a pack only becomes real when it is
 * imported — which `scripts/e2e-stack.sh` does after the API seeds the catalogue. So this spec is the whole chain in
 * one run, against the real client, the real hub and a real Postgres:
 *
 *   Crestfall Square → the Ashlen Wood → the Old Clearing → an encounter drawn from the imported pool, its loot in the
 *   bag → back to Crestfall Square.
 *
 * What it asserts, and why each one is the honest shape:
 *
 *  - **The pools are the pack's.** Read from `ZoneLocations.ActorPool`, entry for entry with their weights. A browser
 *    cannot see a weight, and a pool that is not stored is not the pool the spec walked into.
 *  - **The town pools nothing.** All four Crestfall locations, because "safe" is a rule, not a mood.
 *  - **The roster came from the pool.** The persisted `EnemyInstances` rows at the location name their archetype, so
 *    the enemy that is engaged is provably one the imported pool allows — not a fallback and not the placed instance.
 *  - **The reward is the imported content's.** Experience is exact and gold is the archetype's range, both read back
 *    from the archetype row the import wrote; every item added to the bag is an entry of that archetype's imported
 *    loot table, within that entry's quantity bounds. Nothing is mocked and no drop rate is touched: which entries
 *    land is the production roll, so the spec keeps fighting the pool until one kill pays — the isolated stack only
 *    shortens the respawn window (`Enemies__*` in `scripts/e2e-stack.sh`), never the drop chance.
 *
 * Not asserted here, on purpose: the Wood Road's pool is covered by the stored-pool check, because reaching it on foot
 * means passing `strangled-oak`, which is hidden until its passive skill check is passed — a different slice's walk.
 */
test.describe('A1 — the Ashlen Wood pools', () => {
	test.skip(!stackEnabled, stackSkipReason);
	test.describe.configure({ mode: 'serial' });
	test.setTimeout(420_000);

	test('Crestfall Square → the wood → the Old Clearing → a pool kill pays the imported reward → back', async ({
		page
	}) => {
		await register(page, makeAccount('e2ea1'));

		// The wizard accepts letters and spaces only ("2–20 characters"), so a stamp of digits would be rejected
		// before the availability check even runs.
		const name = `Wood${randomUUID()
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
			// met. A pack that never imported leaves the seeder's interim pairs here, and this fails at once.
			const crestfall = await readZonePools(sql, 'crestfall');
			expect(Object.keys(crestfall).sort()).toEqual([...CRESTFALL_LOCATIONS].sort());

			for (const slug of CRESTFALL_LOCATIONS) {
				expect(crestfall[slug], `${slug} is a town or the respawn square and must pool nothing`).toEqual(
					[]
				);
			}

			const pools = await readZonePools(sql, ZONE_ID);
			for (const [slug, expected] of Object.entries(ASHLEN_WOOD_POOLS)) {
				expect(pools[slug], `${slug} must carry the pack's pool, weights included`).toEqual(expected);
			}

			// ── The walk ────────────────────────────────────────────────────────────────────────────────
			// The wizard already placed the character on the square; walking there is the "already here" no-op
			// that proves the map knows where it is before a zone change paints a different map.
			expect(await walkTo(page, MAP_NODES.townSquare)).toBe(true);
			await expect(page.locator('button[aria-current="location"]')).toContainText('Crestfall Square');

			expect(await walkTo(page, MAP_NODES.woodGate)).toBe(true);
			expect(await walkTo(page, MAP_NODES.encounter)).toBe(true);
			await expect(page.locator('button[aria-current="location"]')).toContainText('The Old Clearing');

			// ── The roster the pool produced ────────────────────────────────────────────────────────────
			// A browser shows the enemy's name; the stored row carries the archetype slug the pool drew. The placed
			// instance stands here too — it is content, and it must not be counted as a pool draw.
			const roster = await readLiveRoster(sql, ENCOUNTER_LOCATION);
			expect(roster.length, 'walking onto the clearing must spawn its roster').toBeGreaterThan(0);

			const poolDrawn = roster.filter((enemy) => enemy.instanceSlug === null);
			expect(poolDrawn.length, 'the pool must have filled the roster, not only the placement').toBeGreaterThan(0);

			for (const enemy of poolDrawn) {
				expect(
					POOL_ARCHETYPES,
					`${enemy.name} is not one of the zone's pooled archetypes`
				).toContain(enemy.archetypeSlug);
			}

			expect(
				roster.some((enemy) => enemy.instanceSlug === PLACED_INSTANCE),
				'the authored instance at this location is content the pack does not own'
			).toBe(true);

			// ── The fight, until the pool pays ──────────────────────────────────────────────────────────
			let state = await readCharacterState(sql, characterId);
			expect(state, 'the character row must exist').not.toBeNull();

			type Kill = { archetype: string; added: { itemSlug: string; quantity: number }[] };
			const kills: Kill[] = [];
			let payment: { archetype: string; table: string } | null = null;

			for (let attempt = 0; attempt < 8 && payment === null; attempt += 1) {
				const target = await waitForPoolEnemy(page);
				expect(target, `no pool enemy was standing at the clearing (attempt ${attempt + 1})`).not.toBeNull();

				// Health is the only budget the journey has: nothing in the wood heals it. A quarter is the floor
				// that keeps a bad roll of draws from ending the run before the reward is exercised.
				const health = await readHealth(page);
				expect(
					health.current,
					'the character ran out of health before the pool paid out; the draw was too hostile for a level 1'
				).toBeGreaterThan(health.max * 0.25);

				const before = state!;
				const archetype = target!.archetype;
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
				expect(after!.experience - before.experience, `${archetype} pays its experience reward`).toBe(
					reward!.experience
				);

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
				expect(table.length, `${reward!.lootTable} must have entries`).toBeGreaterThan(0);

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

			// ── The reward is in the bag, on screen and after the walk home ────────────────────────────
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

			// ── And back, the way the journey came ─────────────────────────────────────────────────────
			expect(await walkTo(page, MAP_NODES.arrival)).toBe(true);
			expect(await walkTo(page, MAP_NODES.outOfZone)).toBe(true);
			expect(await walkTo(page, MAP_NODES.townSquare)).toBe(true);
			await expect(page.locator('button[aria-current="location"]')).toContainText('Crestfall Square');

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
