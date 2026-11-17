# Content seeding pass

> Opened 2026-11-17 from the web e2e work, which could not write a combat spec — [§6 of the e2e plan](web-e2e-workflows.md)
> records the finding. This file is the checklist for the pass that fixes it, and the evidence it starts from.

## 1. Why this exists

The e2e suite drives every page route and most workflows. The one item it could not write is **combat and death**
(E13c/E13d), and the reason turned out not to be the test tooling: **nothing spawns anywhere in the seeded world**,
so a player can never meet an enemy and `CombatPanel` can never mount for anyone — real user or spec.

That makes this a game defect. The e2e gap closes on its own once the content exists.

## 2. The measured state (2026-11-17, from the seeded catalog)

Taken from the stack database (`scripts/e2e-stack.sh start`), which seeds on boot.

| What | Count | Note |
|---|---|---|
| Zones | 19 | |
| Maps | 25 | 21 `Zone`, 4 `Region` |
| Map nodes | 135 | every location has at least one node (0 orphans) |
| Zone locations | 67 | 37 `locations`, 17 `environments`, 13 `dungeons` |
| **Locations with an actor pool** | **0** | `jsonb_array_length("ActorPool") = 0` on every row |
| **Locations with `Traits.SafeRespawn = true`** | **0** | no safe respawn point and no dungeon lobby |
| Actor archetypes | 4 | `bandit-ruffian`, `goblin-scout` (hostile); `tavern-keeper`, `village-blacksmith` (NPCs) |
| Quests | 3 | for 67 locations |
| Dialogue | 4 | |
| Loot tables | 4 | |
| Items | 25 | |
| Recipes | 6 | |
| Organizations | 2 | |

## 3. The gaps, in priority order

### P0 — no encounter exists anywhere

`ZoneLocations.ActorPool` is empty on all 67 rows, and
[`ActorPoolResolver.SpawnRosterAsync`](../Veldrath.Server/Features/Characters/Combat/ActorPoolResolver.cs:35) returns
an **empty roster** for an empty pool *by design*: the old "one random `common` enemy" fallback was removed so a
missing pool could not be silently faked. The design is right; the content never followed. Consequences:

- `TravelToNodeHubCommand` and `EnterZoneLocationHubCommand` spawn nothing, so the HUD's "Local" panel always says
  "Nothing hostile is lurking here right now."
- `CombatPanel`, the combat log, engage/attack/defend/flee, ability use in combat, and kill rewards are all
  unreachable for a real player. The only coverage they have is unit tests.
- The content audit words an empty pool as a **warning** (`SpawnPoolEmpty`: "Safe locations are legitimate, so this
  is only a warning"), so a world where *everything* is empty audits clean apart from 67 warnings nobody reads.

**The fix:** author pools on the hostile locations and leave the settlements safe. The 30 `dungeons`/`environments`
rows are the natural candidates (e.g. `bone-hollow`, `submerged-ruins`, `tidelocked-vault`, `kaldreks-heart`,
`pyreling-den`, `wreck-shore`), and the `locations` rows that are inns/markets/squares stay empty on purpose.

### P1 — two hostile archetypes cannot carry 67 locations

`bandit-ruffian` and `goblin-scout` are the only hostile actors in the catalog. Two archetypes cannot express level
bands, zone identity or a dungeon's difficulty curve, and `EnemyGenerator` resolves a pool entry to one of them at
every tier. A pool per location would only reshuffle the same two enemies.

**The fix:** seed archetypes with level bands and zone flavour before or alongside the pools, so a pool entry means
something. `WorldAndActorValidators` already audits pool weights and unknown archetype slugs, so a wrong entry is
caught at audit time rather than at runtime.

### P1 — there is no safe respawn point, and every death logs an error

`Traits.SafeRespawn` is false on all 67 locations. Three call sites depend on it, and each one fails quietly:

- [`RespawnHubCommand`](../Veldrath.Server/Features/Characters/Combat/RespawnHubCommand.cs:178) looks for a
  `SafeRespawn` location in the character's zone; finding none it logs
  **`Zone {ZoneId} has no location flagged SafeRespawn; falling back to …`** at error level and respawns at
  `locations[0]` — whichever location the repository happens to return first. So every respawn in the shipped world
  is both an error line and an arbitrary destination, and the safe-respawn half of Milestone A2 has no authored
  target to be safe at.
- [`EnterZoneLocationHubCommand`](../Veldrath.Server/Features/Zones/EnterZoneLocationHubCommand.cs:146) skips the
  lobby redirect when the zone has no `SafeRespawn` location, so a dungeon entry never starts in a lobby.
- [`EnterDungeonHubCommand`](../Veldrath.Server/Features/Characters/EnterDungeonHubCommand.cs:130) and the map
  validator's dungeon-set derivation
  ([`MapEditorService`](../Veldrath.Server/Features/Content/MapEditorService.cs:358)) use the same flag, so the
  "a dungeon room plus a lobby" set `SeededContentValidationTests` guards is currently empty of lobbies.

**The fix:** mark one location per zone as `SafeRespawn` — the inns and squares are the obvious candidates — and give
each dungeon zone a lobby before its rooms carry pools. That turns the error-level fallback back into an unused
branch and restores the meaning of the CI dungeon guard.

> The dungeon-side design lives in [`dungeons.md`](dungeons.md) ("a lobby per dungeon", and the four content defects
> that make three dungeons inescapable); the death-side design is in
> [`death-and-first-hour.md`](death-and-first-hour.md) (A2). Both shipped the code and assumed the content.

### P2 — most locations have nothing to do

3 quests and 4 dialogues across 67 locations and 19 zones. `WorldAndActorValidators` audits spawn pools and loot
references, not coverage, so nothing tells us which zones are empty. The quest HUD, the quest turn-in toast, shops
and loot drops are all web pages and hub commands with a single seeded example each.

**The fix:** at minimum one quest per starter zone and one merchant whose stock comes from a real loot table, so the
existing surfaces (`/quests` HUD, `VisitShop`, `PickUpGroundItem`) have more than one example to behave against.

## 4. Guardrail: the world must not regress to "nothing spawns"

The audit cannot catch this today, because a legitimately safe town and a forgotten pool look identical to it. After
the content lands, add to [`SeededContentValidationTests`](../Veldrath.Server.Tests/Features/SeededContentValidationTests.cs)
an assertion in the spirit of the existing dungeon guard:

- at least one non-`SafeRespawn` location per starter zone has a non-empty pool;
- every pool entry resolves to an archetype that exists (already covered by `AuditSpawnPools`);
- every dungeon room's zone has exactly one `SafeRespawn` lobby.

That turns "combat exists in the shipped world" into a test, which is what E13c/E13d need anyway.

## 5. What this unblocks

| Content lands | Then |
|---|---|
| Pools on hostile locations + safe locations | E13c: travel into a known encounter → engage → victory; the "Local" panel lists a hostile; `CombatPanel` mounts |
| A safe respawn point + a death | E13d: die, see "You have been defeated", respawn at the safe location, death sickness |
| More archetypes | A pool's level band means something; combat can be tuned per zone |
| Quests and dialogue | Quest tracker, turn-in toast and the `/quests` surfaces exercised with real breadth |

## 6. How to re-measure

```sh
./scripts/e2e-stack.sh start            # or `start --fresh` for a clean seed
docker compose exec -T postgres psql -U veldrath -d veldrath -c \
  'select count(*) filter (where jsonb_array_length(coalesce("ActorPool", '\''[]'\''::jsonb)) > 0) as with_pool,
          count(*) filter (where "Traits"->>'\''safeRespawn'\'' = '\''true'\'') as safe,
          count(*) as locations
   from "ZoneLocations";'
```

The Foundry audit page (`/world-health`) and `GET /api/content/audit` report the same gaps as issues; a pool that
names an unknown archetype is an error there, an empty pool is a warning.

## 7. See also

- [Web e2e workflows](web-e2e-workflows.md) — §6 records the finding; E13c/E13d are the specs waiting on this.
- [`dungeons.md`](dungeons.md) — the lobby-per-dungeon model and the four inescapable-dungeon defects.
- [`death-and-first-hour.md`](death-and-first-hour.md) — A2, safe respawn and death sickness, shipped against a
  catalog with no safe location in it.
- [`content-pipeline.md`](content-pipeline.md) — the validators and the Foundry write path this pass will author
  through (or seed, if it lands as seed data).
- [Client UI architecture](../docs/client-ui-architecture.md) — the panel the encounters appear in.
- [`.github/agent-memory/veldrath-memory.md`](../.github/agent-memory/veldrath-memory.md) — the same finding in the
  session notes, with the code references.
