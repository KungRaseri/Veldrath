# Content pass — a baseline a player (and a test) can actually run

> Opened 2026-11-17 from the web e2e work, which could not write a combat spec. Rewritten the same day after
> reading the seeders: **the world has safe respawn points** (my first measurement queried the JSON with the wrong
> keys — see §3.1). What it does not have is a single encounter.

## 1. Why this pass exists

The e2e suite drives every page route and most workflows. The one item it could not write is **combat and death**
(E13c/E13d), and the blocker is content, not tooling: nothing spawns anywhere, so a player can never meet an enemy
and `CombatPanel` never mounts — for a real user or for a spec. The same pass is the cheapest way to give every
other gameplay surface (quests, loot, dialogue, harvesting, shops) more than one seeded example.

## 2. Where content lives (read this before authoring anything)

| Thing | Where | Behaviour that matters |
|---|---|---|
| The seeded catalog | [`RealmEngine.Data/Seeders/`](../RealmEngine.Data/Seeders) — one seeder per content family, all C# | **Source of truth.** No JSON content files exist. |
| Idempotency | every seeder: `existing = db.X.Select(x => x.Slug).ToHashSet()` then `missing.Where(x => !existing.Contains(x.Slug))` | **It inserts missing slugs and never updates an existing row.** Editing a seeder record changes nothing on a database that already has that slug. |
| Consequence | — | A content change is only visible after `scripts/e2e-stack.sh start --fresh` locally, or in a database where the row is absent. **This is the first decision in §5.** |
| CI guard | [`SeededContentValidationTests`](../Veldrath.Server.Tests/Features/SeededContentValidationTests.cs) | Validates the seed through the same rule set Foundry uses, so a broken pool entry fails the build. |
| Foundry | the SvelteKit app, `/content` and `/maps` | Writes the same tables through the write path. Authoring in Foundry does **not** update the seeders. |
| Live-game data | `ApplicationDbContext` (characters, sessions, spawned enemies, unlocked locations) | Seeded content is `ContentDbContext`; keep the two apart. |

## 3. The measured state (2026-11-17, from the seeded stack database)

| What | Count | Note |
|---|---|---|
| Zones | 19 | across 4 regions: Varenmark, Greymoor, Saltcliff, Cinderplain |
| Maps | 25 | 21 `Zone`, 4 `Region`; 135 nodes; 0 locations without a node |
| Zone locations | 67 | 37 `locations`, 17 `environments`, 13 `dungeons` |
| Locations with an actor pool | **0** | `jsonb_array_length("ActorPool") = 0` on all 67 |
| Actor archetypes | **4** | `goblin-scout` (lvl 1–3), `bandit-ruffian` (lvl 3–6), `tavern-keeper`, `village-blacksmith` |
| Loot tables | 4 | `goblin-common-drops`, `wolf-common-drops`, `forest-chest-loot`, `bandit-boss-drops` |
| Quests | 3 | for 19 zones |
| Dialogue | 4 | |
| Items / recipes / materials | 25 / 6 / few | every merchant sells the whole item catalog |
| Zone locations with `Traits.SafeRespawn = true` | **13, one per dungeon zone plus town squares** | see §3.1 |

### 3.1 Correction to the earlier finding

The first version of this file said no location was flagged `SafeRespawn`, and claimed every death logs
`Zone {ZoneId} has no location flagged SafeRespawn`. **That was a measurement error, not a bug.**
`ContentModelConfiguration` stores `Traits` with `ToJson()`, so the JSON keys are the property names —
`SafeRespawn`, `IsDungeon`, `IsTown` — and my query used camelCase (`->>'safeRespawn'`), which matched nothing.

The seeders do set it: `crestfall-square`, `weathered-waypost`, `strangled-oak`, `sunken-estate`, `ironhollow-keep`,
`moorstone-cairns`, `tolvaren-harbour`, `clifftop-ruins` are safe, and each dungeon zone has a `locations` room
with `SafeRespawn = true` / `IsDungeon = false` that acts as its lobby (`halrow-threshold`, `pit-descent`,
`deeps-entrance`, `drowned-threshold`). `RespawnHubCommand` and the dungeon lobby lookup therefore work.

**Trap for anyone querying the catalog:** use `"Traits"->>'SafeRespawn'` (PascalCase), never `'safeRespawn'`.

## 4. The gaps, in priority order

### P0 — nothing spawns anywhere (verified)

`ZoneLocations.ActorPool` is empty on all 67 rows and
[`ActorPoolResolver.SpawnRosterAsync`](../Veldrath.Server/Features/Characters/Combat/ActorPoolResolver.cs:39) returns
an empty roster for an empty pool **by design** — the old "one random `common` enemy" fallback was removed so a
missing pool could not be silently faked. Consequences:

- `TravelToNodeHubCommand` and `EnterZoneLocationHubCommand` spawn nothing, so the HUD's "Local" panel can only say
  "Nothing hostile is lurking here right now."
- `CombatPanel`, the combat log, engage/attack/defend/flee, abilities in combat, kill rewards and death itself are
  unreachable for a real player. Only unit tests exercise them.
- The audit words an empty pool as a **warning** (`SpawnPoolEmpty`: "Safe locations are legitimate"), so a world
  where *everything* is empty passes the audit with 67 warnings nobody reads.

**Fix:** author pools on the hostile locations and leave settlements safe. Candidates by band, from the seeder:
`drove-road`, `wood-road`, `old-clearing` (1–5); `mire-crossing`, `bone-hollow`, `sunken-estate` (3–6);
`ashveil-highlands`, `moorstone-cairns`, `shifting-waymark` (7–14); `fenland-crossing`, `submerged-ruins` (9–14);
`wrack-shore`, `bone-strand`, `tidal-grotto` (12–18); `clifftop-ruins`, `gull-rider-camp` (14–20); the 13 dungeon
rooms (4–17). Towns, inns, markets and the `SafeRespawn` lobbies stay empty.

### P1 — two hostile archetypes cannot carry a level 1–20 world

`goblin-scout` tops out at level 3 and `bandit-ruffian` at 6. The zone ladder runs to level 20, so every zone from
`pale-moor` (7+) upward has no plausible enemy at all, and a pool there would be a lie.

**Fix:** seed archetypes per band — something like `giant-rat`/`forest-wolf` (1–3), `goblin-scout`/`bandit-ruffian`
(3–6, existing), `moor-wight`/`cairn-haunt` (6–10), `fen-lurker`/`drowned-revenant` (10–14),
`gull-rider-brute`/`ash-wraith` (14–20), plus one boss per dungeon (the loot tables already expect one). Each needs
`Stats` (health, damage, XP, gold) and `Traits` (`Hostile`, `Aggressive`, `PackHunter`, `Caster`, …).

### P1 — the loot tables are orphans

Four tables exist and **no archetype references any of them** (`ArchetypeSeeder` sets no `LootTableRef`), and two of
the four name content that does not exist: `wolf-common-drops` (there is no wolf archetype) and `bandit-boss-drops`
(there is no boss). `forest-chest-loot` has no chest to open.

**Fix:** wire each hostile archetype to a drop table, and either author the missing archetypes/boss/chest or retire
those tables. Keeping a table nothing can drop is the same class of bug as an empty spawn pool.

### P2 — quests do not survive contact with the world

`clear-the-forest` targets 5 `goblin-scout` kills "in the Darkwood Forest" — **no such zone or location exists in
the seed**, and with no pools the kills cannot happen. `gather-mushrooms` targets `glowcap-mushroom`, which is a real
item but has no source: no harvestable node is seeded and `NodeSpawnerService` is engine-only.
`the-lost-shipment` (min level 3) targets `lost-cargo`, which nothing drops.

**Fix:** either reword/retarget the three to places and actors that exist, or seed the missing source: a harvestable
`glowcap-mushroom` node in the woods locations, and a drop (`lost-cargo`) on a bandit. Three quests for 19 zones is
also thin: one per starter zone is the minimum for a passable baseline.

### P2 — dialogue and NPCs

4 dialogue rows for 67 locations, and no NPC is marked `QuestGiver` or `HasDialogue`. The `tavern-keeper` and
`village-blacksmith` NPCs exist as archetypes but are not placed in any location pool (see P0: pools are the only
placement mechanism) — so no NPC is ever visible either.

**Fix:** once pools exist, place the NPC archetypes in the town/inn pools; give at least the starter-zone quest giver
a dialogue with a quest offer, so the dialogue surface has a real path instead of one seeded example.

### P3 — every merchant sells the same 25 items

[`GetShopCatalogHubCommand`](../Veldrath.Server/Features/Shops/GetShopCatalogHubCommand.cs:36) dispatches
`GetItemCatalogQuery`, i.e. the whole priced catalog, gated only by the *zone's* `HasMerchant`. Not a blocker for a
test baseline (the shop panel works), but it is why every shop feels identical.

**Fix (later):** per-merchant stock — a loot-table-style table keyed by zone, or a `ShopStock` junction. Out of scope
for this pass unless the pass wants a shop test that can tell two merchants apart.

## 5. Decisions to take before authoring (see the question list)

1. **Authoring channel and the update problem.** Seeders are the source of truth and the seeder only *inserts*.
   Options: (a) seeders only, accept `start --fresh` locally and a seed bump in real environments; (b) seeders only
   plus an opt-in upsert mode (`SeedMode.Refresh`) that updates seeded rows by slug; (c) author in Foundry and
   back-port the resulting payloads into seeders. (b) is the most useful, (a) is the smallest change.
2. **Reach.** Encounters in roughly 10–12 locations (the dungeon zones and the wilderness around them) for a
   testable baseline, or the full 1–20 ladder across all 19 zones.
3. **Archetype breadth.** New archetypes per level band (10–14 new rows), or scale the two existing ones per zone.
4. **Scope of this pass.** Encounters only (unblocks E13c/E13d immediately) versus encounters + loot wiring +
   quest fixes + harvest node + NPC placement.

## 6. Phases

| Phase | Work | Done when |
|---|---|---|
| C0 | Branch off `development`; correct the two documents that carry the bad `SafeRespawn` finding; record the PascalCase JSON trap | `plans/` and the memory agree with the code |
| C1 | Archetypes per level band, with stats, traits and a `LootTableRef` each | `SeededContentValidationTests` passes; every archetype's loot table resolves |
| C2 | Spawn pools: hostile locations only, weighted, matching each location's `MinLevel`/`MaxLevel` | the HUD's "Local" panel lists hostiles in every band; a `--fresh` run spawns 2–4 per visit |
| C3 | NPC placement: innkeepers and smiths in town/inn pools; one quest giver with dialogue | a player can see and talk to an NPC without a GM |
| C4 | Loot and quests: wire drops, retire or author the orphan tables, fix the three quests' targets and text, add one quest per starter zone | every quest's objective target exists and can be produced |
| C5 | Harvesting: seed at least one harvestable node per woods location so `gather-mushrooms` is completable | a collect objective can be finished in game |
| C6 | Guardrail tests + e2e: the seed assertions in §7, then E13c (engage → victory) and E13d (die → respawn at the safe location) | the gate is green and both specs pass against a fresh stack |

## 7. Guardrail: the world must not regress to "nothing spawns"

The audit cannot catch this, because a legitimately safe town and a forgotten pool look identical to it. Add to
[`SeededContentValidationTests`](../Veldrath.Server.Tests/Features/SeededContentValidationTests.cs), in the spirit of
the existing dungeon guard (which derives the dungeon set from a room plus a lobby):

- at least one non-`SafeRespawn` location per zone has a non-empty pool;
- every pooled archetype exists and is `Hostile` for a non-safe location (and not for a town);
- every `SafeRespawn` location has an empty pool, so a town cannot start murdering people;
- every archetype's `LootTableRef`, when set, resolves to a seeded loot table;
- every quest objective's target is either a seeded item or a seeded archetype that some pool can spawn.

## 8. How to measure

```sh
./scripts/e2e-stack.sh start --fresh        # reseed, or editing a seeder changes nothing (see §2)
docker compose exec -T postgres psql -U veldrath -d veldrath -c '
select count(*) filter (where jsonb_array_length(coalesce("ActorPool", $q$[]$q$::jsonb)) > 0) as with_pool,
       count(*) filter (where "Traits"->>$q$SafeRespawn$q$ = $q$true$q$) as safe,
       count(*) as locations
from "ZoneLocations";'
```

The Foundry audit page (`/world-health`) and `GET /api/content/audit` report the same data as issues: an unknown
archetype or loot slug is an error there, an empty pool is a warning.

## 9. See also

- [Web e2e workflows](web-e2e-workflows.md) — §6 records why E13c/E13d are parked here.
- [`dungeons.md`](dungeons.md) — the lobby-per-dungeon model these seeders already follow.
- [`death-and-first-hour.md`](death-and-first-hour.md) — A2 safe respawn, which the seed satisfies.
- [`content-pipeline.md`](content-pipeline.md) — the validators and the Foundry write path this pass authors through.
- [Client UI architecture](../docs/client-ui-architecture.md) — the "Local" panel the encounters appear in.
- [`.github/agent-memory/veldrath-memory.md`](../.github/agent-memory/veldrath-memory.md) — session notes, including
  the JSON-key trap.
