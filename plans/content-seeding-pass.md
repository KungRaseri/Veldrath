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

### P0 — nothing spawns anywhere (measured 2026-11-17; **fixed 2026-11-18** — pools on every hostile location)

`ZoneLocations.ActorPool` is empty on all 67 rows and
[`ActorPoolResolver.SpawnRosterAsync`](../Veldrath.Server/Features/Characters/Combat/ActorPoolResolver.cs:86-92) returns
an empty roster when a location has no pool **and** nothing placed — **by design**, so the old "one random `common` enemy"
fallback could not silently fake a missing pool. Hostile placements still spawn (`:111-115`). Consequences:

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

**Status (2026-11-20):** the counts above are the 2026-11-17 record. The pools landed in `b9788d00` (merged as
#76): 36 of 67 locations carry one, every key is a real location slug, and
`Every_other_location_can_spawn_something` fails the build if a hostile location goes quiet. Both arrival
commands spawn from the pool, and the empty-pool branch this section quotes now sits at
`ActorPoolResolver.cs:86-92`.

### P1 — two hostile archetypes cannot carry a level 1–20 world (**fixed 2026-11-18**)

`goblin-scout` tops out at level 3 and `bandit-ruffian` at 6. The zone ladder runs to level 20, so every zone from
`pale-moor` (7+) upward has no plausible enemy at all, and a pool there would be a lie.

**Fix:** seed archetypes per band — something like `giant-rat`/`forest-wolf` (1–3), `goblin-scout`/`bandit-ruffian`
(3–6, existing), `moor-wight`/`cairn-haunt` (6–10), `fen-lurker`/`drowned-revenant` (10–14),
`gull-rider-brute`/`ash-wraith` (14–20), plus one boss per dungeon (the loot tables already expect one). Each needs
`Stats` (health, damage, XP, gold) and `Traits` (`Hostile`, `Aggressive`, `PackHunter`, `Caster`, …).

**Status (2026-11-20):** fourteen hostile archetypes are seeded, banded `giant-rat` (1–3) through `fire-ancient`
(26–30) — the ladder runs to 30 now, not 20 ([`ArchetypeSeeder.cs`](../RealmEngine.Data/Seeders/ArchetypeSeeder.cs)).
`Every_hostile_band_has_an_archetype` and `Every_pooled_archetype_fits_its_location_level_band` keep a band from
going empty and keep a pool inside its own location's levels.

### P1 — the loot tables were orphans (**fixed 2026-11-18**)

Four tables exist and **no archetype references any of them** (`ArchetypeSeeder` sets no `LootTableRef`), and two of
the four name content that does not exist: `wolf-common-drops` (there is no wolf archetype) and `bandit-boss-drops`
(there is no boss). `forest-chest-loot` has no chest to open.

**Fix (shipped):** each hostile that should drop now names a table, and the kill path rolls it:
[`EfCoreEnemyRepository`](../RealmEngine.Data/Repositories/EfCoreEnemyRepository.cs) carries the table slug on the
`Enemy` model, `AttackEnemyHubCommandHandler` rolls each entry against its own weight (a guaranteed entry always
lands), adds the result to the killer's bag and returns it as `ItemsDropped`, and the client names it in the combat
log ("Found: 2× iron-ingot"). `lost-cargo` now rides on `bandit-boss-drops`, so `the-lost-shipment` has a source.
Both gaps this section left open were closed later: chests are authored and roaming (see **Chests** below) and
`gather-mushrooms` has five authored nodes (see **Harvesting**).

### P0 — a dead player was locked out: "Character is not dead" (found by the C6 spec, **fixed 2026-11-18**)

The fight works: walking onto `drove-road` spawns a roster, `Engage` starts a turn-based combat, and both sides trade
damage. Dying did not. The client showed "You have been defeated." and the log said "You cannot act while dead.",
while the persisted blob still held `CurrentHealth > 0` — so `RespawnHubCommand` refused with
**"Character is not dead"** and the player could neither act nor respawn.

**Cause and fix:** `CombatHelpers.HandleDeathIfNeededAsync` "restored 1 HP so the character isn't permanently dead",
which made the character look alive to the very command that respawns it. Death now leaves `CurrentHealth = 0`
(respawn is the recovery path, and it restores 25% of max health), the client sets its dead state from the
`CharacterSelected` snapshot so a relogged corpse still sees the button, and both are covered by the death tests in
`AttackEnemyHubCommandHandlerTests`. The e2e death spec passes.

**Related content rule this exposed:** a respawn point that spawns hostiles is a death loop. Locations flagged
`SafeRespawn` (and towns and lobbies) now carry no pool, and `SeededEncounterCoverageTests` asserts it.

### P1 — a level-1 character cannot win any fight (**fixed 2026-11-17**)

A new character has **10 HP** (the attribute blob's `MaxHealth`), and a location spawns **2–4** pool-drawn actors whose weakest
swing is 3–5. One exchange killed the level-1 warrior outright, so *victory* is arithmetically impossible for a new
character anywhere in the world. The engagement spec therefore asserts the exchange rather than a kill.

**Fix (needs a balance decision):** either raise the level-1 baseline (HP from constitution, not a flat 10), or make
the level 1–3 band genuinely weak (damage 1, one or two actors), or scale the roster by the character's level.

**Status (2026-11-20):** the cause was not the band. `POST /api/characters` wrote no attribute blob, so an API-made
character carried the `Level * 10` = 10 HP fallback while a wizard-made one carried `Con*10 + level*5`. The endpoint
writes the same keys now, `CharacterEndpointTests` asserts the stored blob, and `encounters.e2e.ts` engages a
`drove-road` roster on a level-1 warrior and wins.

### P1 — a location is not adjacent to where a character arrives (found writing C6; **partly fixed 2026-11-18**)

Each zone map links `entry ↔ exit:zone:x ↔ location:y`: the entry node's neighbours are the zone's **exits**, and
each exit touches the locations on that side. So walking from the arrival point to a location takes two or more hops,
and travelling to a `ZoneExit` node *leaves the zone* rather than staying on the map, which is why the e2e walk helper
in `tests/encounters.e2e.ts` never reaches its target. A player can get there — they click an exit, arrive elsewhere,
walk back — but it is not the one-click path the HUD implies.

**Also found:** resuming a session does **not** spawn a roster; only an arrival (`TravelToNode`, `EnterLocation`)
does. Teleporting onto a location and reloading therefore shows an empty "Local" panel.

**Fix (decision):** either author entry→location edges so a settlement/waypoint is one hop from its own map's
locations, or add a dev-only GM spawn (`POST /api/dev/spawn` → `IEnemyInstanceStore.GetAliveOrSpawnAsync` for the
character's current location) so a roster can be created deterministically for tests. The GM spawn is the cheaper of
the two and does not change the world.

**Status (2026-11-20):** the adjacency half is fixed — `MapGraphSeeder.ConnectZoneEntriesToLocations` chains
`entry → first → second → …`, so the first location of every zone is one hop from the arrival point and
`encounters.e2e.ts` walks to `drove-road` on foot from the arrival node (its dev teleport only puts the character in
the zone). The resume half is still open: `EnterZone` sends `ZoneEntered` and no roster, so a character who logs in
already standing on a location sees an empty Local panel until they move.

### P2 — quests do not survive contact with the world (**fixed 2026-11-18**; breadth still open)

`clear-the-forest` targets 5 `goblin-scout` kills "in the Darkwood Forest" — **no such zone or location exists in
the seed**, and with no pools the kills cannot happen. `gather-mushrooms` targets `glowcap-mushroom`, which is a real
item but has no source: no harvestable node is seeded and `NodeSpawnerService` is engine-only.
`the-lost-shipment` (min level 3) targets `lost-cargo`, which nothing drops.

**Fix:** either reword/retarget the three to places and actors that exist, or seed the missing source: a harvestable
`glowcap-mushroom` node in the woods locations, and a drop (`lost-cargo`) on a bandit. Three quests for 19 zones is
also thin: one per starter zone is the minimum for a passable baseline.

**Status (2026-11-20):** all three targets are producible now. `clear-the-forest` names the Ashlen Wood, where
`goblin-scout` pools on `wood-road` and `old-clearing`; `gather-mushrooms` has five harvestable `glowcap-cluster`
nodes in those two locations and a `forest-forage` table; `lost-cargo` is a guaranteed entry on
`bandit-boss-drops`, which `bandit-captain` drops. Still open: three quests for 19 zones.

### P2 — dialogue and NPCs (**fixed 2026-11-18**)

4 dialogue rows for 67 locations, and no NPC is marked `QuestGiver` or `HasDialogue`. The `tavern-keeper` and
`village-blacksmith` NPCs exist as archetypes but are not placed in any location pool (see P0: pools are the only
placement mechanism) — so no NPC is ever visible either.

**Fix:** once pools exist, place the NPC archetypes in the town/inn pools; give at least the starter-zone quest giver
a dialogue with a quest offer, so the dialogue surface has a real path instead of one seeded example.

**Status (2026-11-20):** people are their own placement row now (`LocationNpc`), not a pool entry —
`LocationNpcsSeeder` stands the smith at the Aurelian Market, the keeper at the Hearthbound Inn and the elder on the
Crestfall Square, and `TalkToNpc` refuses a person who is not where the character stands. `tavern-keeper` is a
`QuestGiver` with a quest-hook line and both shopkeepers carry `HasDialogue`. Still thin: four dialogue rows for 67
locations.

### P3 — every merchant sells the same 25 items (**fixed 2026-11-20**)

[`GetShopCatalogHubCommand`](../Veldrath.Server/Features/Shop/GetShopCatalogHubCommand.cs:36) dispatches
`GetItemCatalogQuery`, i.e. the whole priced catalog, gated only by the *zone's* `HasMerchant`. Not a blocker for a
test baseline (the shop panel works), but it is why every shop feels identical.

**Fix (later):** per-merchant stock — a loot-table-style table keyed by zone, or a `ShopStock` junction. Out of scope
for this pass unless the pass wants a shop test that can tell two merchants apart.

**Status (2026-11-20):** the junction exists. `ZoneStockSeeder` authors each merchant town's own list — Crestfall,
Aldenmere, Tolvaren, Skarhold, between eight and sixteen of the twenty-four purchasable items per the approved band —
and `GetShopCatalogHubCommand` reads that stock instead of dispatching the whole catalog, behind a merchant who has
to actually be standing with the character.

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
| C2 | Spawn pools: hostile locations only, weighted, matching each location's `MinLevel`/`MaxLevel` | the HUD's "Local" panel lists hostiles in every band; a `--fresh` run spawns its rolled 2–4 per visit |
| C3 | NPC placement: innkeepers and smiths in town/inn pools; one quest giver with dialogue | a player can see and talk to an NPC without a GM |
| C4 | Loot and quests: wire drops, retire or author the orphan tables, fix the three quests' targets and text, add one quest per starter zone | every quest's objective target exists and can be produced |
| C5 | Harvesting: seed at least one harvestable node per woods location so `gather-mushrooms` is completable | a collect objective can be finished in game |
| C6 | Guardrail tests + e2e: the seed assertions in §7 (shipped), then E13c (engage → victory) and E13d (die → respawn at the safe location) | the gate is green and both specs pass against a fresh stack — **parked** on the P1 adjacency/spawn finding above; the specs exist as `test.fixme` with the reason |

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

## Harvesting — the content half is landed; the hub slice is next (2026-11-18)

**Landed.** A `harvesting` loot table (`forest-forage`, guaranteed `glowcap-mushroom` 1–2) and five authored nodes
in the Ashlen Wood ([`HarvestableNodesSeeder`](../Veldrath.Server/Data/Seeders/HarvestableNodesSeeder.cs), seeded
from `Program.cs`, idempotent by node id so a restart never restores a depleted node). Four guard tests keep the
two halves attached: the nodes are unique and live in the wood, and every node names a seeded harvesting table
that yields the quest item.

**Not landed, and why it is not a copy-paste.** The engine's `HarvestNodeCommand` cannot be reused: it reads a
single global "current save" (`ISaveGameService.GetCurrentSave`) and writes the bag through the save-game store,
which is not the store the game client reads — in a server holding several characters that is wrong by
construction. The server needs its own command composing the *stateless* pieces (`HarvestCalculatorService`,
`CriticalHarvestService`, `ToolValidationService`, `LootTableService.RollHarvestingDrops`, `INodeRepository`) with
the server's `IInventoryService.AddItemAsync(characterName, itemRef, quantity)` — the call combat loot already
makes.

**Decisions already made for that slice, so it is mechanical:**

- Node source is authored rows, not `NodeSpawnerService`: procedural spawning by biome cannot promise that the
  item a specific quest names exists.
- The location guard is `node.LocationId == character.CurrentZoneLocationSlug`; otherwise a client could harvest
  the world from one spot.
- Tools: every authored node is `MinToolTier = 0`, and a tool-gated node answers "needs a proper tool" until
  tools exist. Skill rank is 0 (common-tier nodes); skill XP is deliberately **not** awarded — that is a separate
  path (`AwardSkillXpHubCommand`), not a silent default.
- `collect` objectives advance with **both** the slug and the full item reference, the same allowance the kill
  path makes for archetype slugs versus display names.
- Events: `LocationNodes` on `EnterLocation`, `Harvested` after a successful harvest, and the list re-sent after
  failures too (depletion moves it). The web half (`types.ts`, `session.svelte.ts`, a Gather section) has to land
  in the same commit, because the parity suites fail when a hub method has no consumer.

**Both halves are now in (2026-11-18).** `GetLocationNodes`/`HarvestNode` on the hub, `LocationNodes` pushed on
arrival, `Harvested` after each attempt, a **Gatherable** section in the Local panel, and `collect` objectives
advanced per drop. `harvest.e2e.ts` walks the wizard, crosses into the forest, gathers, and asserts the mushroom
is in the bag — the whole loop is green against the isolated stack, and the database shows `glowcap-mushroom` in
the character's `InventoryBlob`.

**What making that spec pass uncovered (all measured, not inferred):**

- **Region → zone travel was broken**, and it was not a level gate: the region's `Zone` nodes carried no
  `TargetMapId`, and `TravelToNodeHubCommand` only loads another map when a node names one. A player could leave
  their town, walk the region map, travel "to" another zone and stay standing on the region map. Fixed by
  `LinkRegionZonesToZoneMaps` in the map seeder, with a guard test that every region zone node targets its zone
  map. Because the seeder skips an existing graph, this needs a `--fresh` database.
- **The engine's `IInventoryService` does not write the player's bag.** The registered `EfCoreInventoryService`
  writes save-game records; the bag the client reads is `Characters.InventoryBlob`. Both the harvest and — the
  same defect, invisible because no spec checked the bag after a fight — **combat loot** now write the blob
  through [`CharacterBag`](../Veldrath.Server/Features/Characters/CharacterBag.cs) and save the character row,
  so a broadcast drop is a stored drop.
- **Zone maps chain `entry → first location → second …`**, so the second location of a zone is two hops from the
  arrival point. Not a defect, but a spec that expects "arrive → stand at any location" is wrong.
- **Nodes never regenerate on their own.** `NodeSpawnerService.RespawnNode` exists and nothing calls it. Rather
  than a background job, `GetLocationNodesHubCommand` applies a lazy rule on read: a node that cannot be
  harvested and whose `LastHarvestedAt` is older than `HarvestingOptions.RegrowMinutes` (default 30) comes back
  full. It is clock-driven and needs no scheduler, and a depleted node now recovers while the player is away.
- **`session.zone` went stale on a map travel that changes zones**: the banner read "Crestfall · The Old
  Clearing" after arriving in the Ashlen Wood, because only `EnterZone` sent the zone snapshot while travel sent
  only the location. `NodeTraveled` carries the zone id and nothing else (no name, no inn/merchant flags, no
  occupants), so the hub now follows a cross-zone arrival with the same `ZoneEntered` snapshot `EnterZone` sends
  (`GameHub.SyncArrivedZoneAsync`), and the client clears its zone when an arrival reports none.

**The suspected blocker was not a level gate.** A probe with a real character showed zone → region travel working
and region → zone travel silently not happening — the region's `Zone` nodes carried no `TargetMapId`, so
`TravelToNodeHubCommand` never loaded the target map. The zone-progress gate only governs *hidden location
discovery*; it never gated walking. With the seeder fix in place, `harvest.e2e.ts` runs inside the parallel stack
suite and walks the whole route (`The Crestfall Square` → `Road to The Ashlen Wood` → `The Old Clearing`) with no
bypass, no teleport and no bypass endpoint.

## Chests — authored and roaming, swept rather than timed (2026-11-18)

The table existed (`forest-chest-loot`, `TypeKey = "chests"`, `IsChest`) and `RollChestDrops` could already
merge several lookups, but nothing anywhere was a chest: no entity, no endpoint, no way to reach the loot. The
design that landed answers two questions at once — where a chest lives, and why it should not live there
forever.

**Where it lives.** [`LootChest`](../Veldrath.Server/Data/Entities/LootChest.cs) sits in
`ApplicationDbContext` beside `GroundItems` and `ZoneInstances`, because it is live world state, not content.
Two kinds share the table:

- **Authored** — seeded by [`LootChestsSeeder`](../Veldrath.Server/Data/Seeders/LootChestsSeeder.cs), idempotent
  by chest id, live state preserved. These are the chests a quest or a room names, so the mossy strongbox is in
  the Old Clearing and the traveller's cache is on the Wood Road, always.
- **Roaming** — placed by [`ChestService`](../Veldrath.Server/Features/Characters/Chests/ChestService.cs) from
  the zone's own `ZoneLocation` rows: not hidden, not a safe respawn, not instanced, level floor met, not
  already occupied, and the zone itself is not a town. The site is drawn by `RarityWeight` and the loot table is
  drawn from every `chests` table by its `RarityWeight`, so "which chest" and "where" vary together with no new
  content types.

**Why it is swept.** The sweeper mirrors `EnemyRespawnService`: a `PeriodicTimer` reads durable columns and
writes conditionally. `ReopenAt` decides when an emptied chest comes back (`Chests:ReopenAfterMinutes`, default
180); a zone gains a roaming chest when `max(SpawnedAt, OpenedAt)` across its roaming rows is older than
`Chests:SpawnEveryMinutes` (default 90) and it holds fewer than `Chests:MaxRoamingPerZone` (default 1). Opened
roaming rows are kept as tombstones on purpose — they *are* the clock, they record what was looted where, and a
reopen only has to clear `IsOpened` on the row still standing. Node regrowth moved onto the same sweep in this
pass, so a depleted node now recovers on time whether or not anyone is looking at the location.

**First open wins.** `OpenChestHubCommandHandler` rolls the loot after checking who is standing where, then
claims the row with `ExecuteUpdateAsync ... WHERE IsOpened = false`; the loser is told "someone else got there
first" instead of being paid twice. Loot lands in `Characters.InventoryBlob` through `CharacterBag` (the same
store as harvest and combat loot); a `gold`-domain entry would go through `AddGoldHubCommand`, which is the one
writer of the attributes gold key.

**Instance scope.** A chest may carry `ZoneInstanceId` and cascades with its run, the way floor loot does, so a
delve's chests are per attempt while a world chest is shared. Reads are scoped by the session's run as well as
by zone and location.

**Known gaps, recorded rather than hidden:**

- `forest-chest-loot` has no `gold` domain entry, so chests pay items only today.
- The sweeper does not broadcast: a chest that appears or reopens while a player stands there shows up on their
  next arrival or pull. Broadcasting would need the zone's difficulty suffix, which lives per connection
  (`zone:{id}_{mode}`), not on the chest row.
- The e2e stack runs with `Chests__ReopenAfterMinutes=1` so a re-run recovers the authored chests without a
  `--fresh`; the spec walks to the road's cache when the clearing's strongbox is still inside its window, and
  skips with that reason if both are.

## People — placement, dialogue, and a guard against hittable shopkeepers (2026-11-18)

The content side of NPCs was already there and unreachable. `ActorArchetype` replaced the old separate enemy and
NPC tables, so hostility is a trait (`Hostile`, `Shopkeeper`, `QuestGiver`, `HasDialogue`, `Immortal`), and three
townspeople are seeded: `village-blacksmith`, `tavern-keeper`, `wayfarer-elder`. `DialogueSeeder` writes their
greeting and farewell lines keyed by the speaker slug, and `IDialogueRepository.GetBySpeakerAsync(slug)` reads
them. What did not exist was any statement that *this* person stands *here* — so the main-story quest's third
objective, `talk:village-blacksmith` ("Return the cargo to the Village Blacksmith"), had nobody to talk to. That is
the same shape as `gather-mushrooms` before the nodes existed: content that names a thing the world cannot offer.

**Placement is its own row, not a spawn-pool entry.** [`LocationNpc`](../Veldrath.Server/Data/Entities/LocationNpc.cs)
holds `ZoneId` + `LocationSlug` + `ArchetypeSlug` (+ optional display-name override and sort order), in
`ApplicationDbContext` beside chests. A pool entry means "what may spawn here, weighted", and a shopkeeper present
a quarter of the time is not a shopkeeper — worse, every pool entry becomes an attackable enemy in the roster.
[`LocationNpcsSeeder`](../Veldrath.Server/Data/Seeders/LocationNpcsSeeder.cs) places the smith at the Aurelian
Market, the keeper at the Hearthbound Inn and the elder on the Crestfall Square, idempotent by placement id.

**Read and talk.** `GetLocationNpcs` → `LocationNpcs` on location entry; each entry carries the archetype slug,
name, occupation and the role flags read from the content archetype (the shared `NPC` projection has no roles, so
the handler reads `ContentDbContext` rather than widening a contract the engine does not use). `TalkToNpc(slug)`
now checks the person is placed where the character stands, answers with one of their own greeting lines (falling
back to a neutral line so an unauthored speaker still answers), sends `NpcTalked`, and only then advances
`talk:<slug>` objectives — the slug is the archetype, the speaker key and the objective key at once.

**One guard, so this cannot happen again.** `EfCoreEnemyRepository` now carries hostility into the `Enemy`
projection (`traits["hostile"]`, defaulting to hostile when the archetype says nothing), and
[`ActorPoolResolver`](../Veldrath.Server/Features/Characters/Combat/ActorPoolResolver.cs:133-139) leaves a non-hostile
archetype out of the combat roster and says so in the log. A future content edit that drops a blacksmith into a
spawn pool therefore produces a warning, not a punching bag.

**Also fixed on the way:** the client only ever pulled location-scoped lists on a *move*, so a resumed character
saw no nodes, chests or people until they walked somewhere. `handleCharacterSelected` now asks for ground items,
nodes, chests and people once, after entering the zone.

**Covered by:** `NpcPlacementTests` (seed guards, the read path with role flags, the talk line, refusals for a
person elsewhere and for a blank slug, and both the hostile and non-hostile roster cases) and the talk step in
`harvest.e2e.ts`, which speaks to the elder on the square before walking into the wood.
