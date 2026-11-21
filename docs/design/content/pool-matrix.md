# Spawn pool placement matrix

**Status: approved 2026-11-20, with five amendments applied. Revised and landed by card
`t_6c70dc04` (L3); amendment 5 applied by card `t_259ac697` (C4), which extended
`game-context/budget.yaml` to level 30 and re-weighted one pool.** Produced by card `t_af051fc2` (D4),
after D0 (`game-context/budget.yaml`, approved 2026-11-20) and D1
(`docs/design/content/archetypes.md`, approved 2026-11-20). It answers one
question — **where is the world dangerous, and how dangerous** — as data a pack importer can take
directly. The companion files are `docs/design/content/pools/<zone>.yaml`, one per world zone.

This document decides *placement only*. It does not decide stat lines (D0 owns those), does not
re-name an archetype (D1 owns the roster, and a slug is permanent identity), and changes no gameplay
code: the only code in this commit is three location traits and the three seeder pools they force
(amendment 3 and 4). The pools are not imported yet: a pack importer now exists, and it is why the 18
skeletons sit in the design set rather than under `content/` (§8).

## Amendments (2026-11-20)

The decision on card `t_af051fc2` approved this matrix with four revisions. All four are applied here
and in `docs/design/content/pools/*.yaml`, and the seeder changes that make them real land in the same
commit. A fifth amendment was added later the same day by C4 (`t_259ac697`), because publishing a band
above `b23-26` re-measures one tail pool; it is listed with the rest so the file has one amendment list.

1. **Band-tolerance cap.** No pool may hold more than one archetype from a band above its location,
   and that entry is `weight ≤ 2`. All 29 pools were audited entry by entry (§2, §3): **exactly one
   entry broke it** — `moor-wight` (D1 band 7–9) in `bone-hollow` (L4–6), at weight 2. It is now
   weight 1 (amendment 2). The other 21 entries that sit one band *above* their location's entry band
   are inside the location's own level range — native content, not tolerance — and §2 carries the list
   with the band evidence for each. After the change **zero pools breach the cap**.
2. **`bone-hollow` softened.** The `moor-wight` stays and drops to **weight 1**; it does not move to a
   7–9 room. The wight *is* the hollow's identity — bone stacked in a hollow, a cold that spoils a
   fire, behind a DC 8 passive check — and removing it would leave a room named for its wight holding
   only bandits and a boar. At weight 1 it remains the rarest thing in the pool: 1 draw in 9.
3. **`ash-shrine` becomes `SafeRespawn`, keeper-only, no pool.** The trait and the pool removal land in
   one commit (`ZoneLocationsSeeder.cs`), because `SeededEncounterCoverageTests` requires a pool on
   every non-safe, non-town, non-lobby location. Its three entries are gone from the packs; `ash-jack`
   and `forge-deserter` move to the battlefield (amendment 4).
4. **`scorched-battlefield` loses `SafeRespawn`; `obsidian-grove` gains it; the battlefield keeps a
   pool.** The grove is the node nearest the zone's arrival point — (30,20) against (32,24),
   `MapGraphSeeder.cs:342` — so it is the respawn a dead character walks out of; the battlefield becomes
   the zone's only hostile location and carries the grove's scavenger pool. After the swap `ashfields`
   holds **two `SafeRespawn` flags but exactly one respawn anchor**: the shrine is `IsHidden` +
   quest-locked and the resolver reads the hidden-filtered zone query
   (`EfCoreZoneLocationRepository.GetByZoneIdAsync`, `RespawnHubCommand.cs:169-188`), so the anchor is
   deterministically `obsidian-grove` — and `SeededEncounterCoverageTests` now asserts it.
5. **The band table reaches level 30, so the tail is measured on it (C4, `t_259ac697`).**
   `budget.yaml` publishes `b27-30` and covers 1–30; §3 relabels `kaldreks-heart` from "beyond `b23-26`"
   to `b27-30`. Publishing it splits the tail into two published bands, so `pyreling` (`b23-26`) at
   `kaldreks-heart` (`b27-30`) is one band apart and drops to **weight 2** — D4's cap number, applied by
   L6's decision 2 — and `maw-serpent` (28–30) carries the pool at **weight 6**, the weight `pyreling`
   held. It is the only pool this amendment touches: no hostile archetype was re-banded to fill
   `b27-30`, no boss is authored for it, and the other 27 pools are unchanged. §7 states what the
   extension changes and what it does not.

One consequence follows from amendments 3 and 4 rather than being an amendment itself, and is flagged
for D5 rather than decided here: `ashfields` no longer has a hostile location below level 21, so
`ash-jack` and `forge-deserter` (roster band 19–20) must be **seeded with `MaxLevel = 22`** or they can
appear nowhere — the engine's overlap guard requires the archetype's range to reach the battlefield's
`MinLevel` (`SeededEncounterCoverageTests.cs:132-143`). §6 carries the range; the roster band is
unchanged, which is the same pattern as the seven carried-over archetypes whose seeded range is wider
than their band.

## 1. What is authoritative, and what I measured

| Claim | Source | Verified how |
|---|---|---|
| 67 zone locations, 19 zones, their `MinLevel`/`MaxLevel`, `DangerLevel` and traits | `RealmEngine.Data/Seeders/ZoneLocationsSeeder.cs` | parsed the seeder's 67 `new ZoneLocation` blocks; every count below is a count over that parse |
| A pool is `IList<ActorPoolEntry>` on the location; entry weight is a relative integer; weight `<= 0` counts as 1 | `RealmEngine.Data/Entities/Content/ZoneLocation.cs:36`, `:94-101`, `Veldrath.Server/Features/Characters/Combat/ActorPoolResolver.cs:101-112` | read |
| An empty pool spawns nothing, by design — no fallback archetype | `ActorPoolResolver.cs:39-45` | read |
| A non-hostile pool entry is dropped from the roster with a warning | `ActorPoolResolver.cs:62-71` | read |
| Roster size is **2–4, globally** — `MinCount`/`MaxCount` are private consts, rolled once per location | `ActorPoolResolver.cs:13-14`, `:47` | read |
| The roster is rolled **once** into `EnemyInstances` and never re-rolled; respawn revives the same row | `Veldrath.Server/Features/Characters/Combat/EfCoreEnemyInstanceStore.cs:56-98` | D0 measured it; I re-read the method |
| Seeded archetypes and their real `MinLevel`/`MaxLevel` | `RealmEngine.Data/Seeders/ArchetypeSeeder.cs` | parsed all 17 archetypes (14 hostile, 3 non-hostile) |
| The archetype roster, its bands, and each archetype's zones | `docs/design/content/archetypes.md` (D1) | read, proposed 2026-11-20 |
| Level bands, spawn and loot semantics | `game-context/budget.yaml` (D0) | read, approved 2026-11-20 |

Three seed facts disagree with older design text. This document follows the seed:

- **`SafeRespawn` is on 17 world locations, not the 13 the plan counts.**
  `plans/content-seeding-pass.md:38` records 13; the seed carried the flag on 16 outside the playground
  before 2026-11-20, and the amendments moved one (`scorched-battlefield` → `obsidian-grove`) and added
  one (`ash-shrine`), so it is **17** now (18 with `playground-square`). Three the plan misses are
  `forge-quarter`, `lava-bridge` and — until amendment 4 — `scorched-battlefield`. Trusting the plan's
  list is exactly how a "safe" location ends up pooled, so §4 is built from the parse, not the plan.
- **`plans/content-seeding-pass.md:71` lists `sunken-estate` as a hostile candidate.** The seeder marks it
  `SafeRespawn = true` (`ZoneLocationsSeeder.cs:220-227`). It is excluded here; that plan line is stale.
- **`ashfields` has one respawn anchor, not two.** Since amendment 4 the flag sits on `obsidian-grove`
  (visible, the node nearest the zone's arrival point) and on `ash-shrine` (hidden, quest-locked, so never
  a candidate — the resolver reads `GetByZoneIdAsync`, which filters `IsHidden`). `scorched-battlefield`
  is hostile and pooled. Two flags, one anchor; §4 and the amendments record why.

### The band ladder used here

Two band sets exist and they are not the same set. Both are used, for different jobs:

- **D0 budget bands** — `b1-3, b4-6, b7-10, b11-14, b15-18, b19-22, b23-26, b27-30`, `covers_levels: [1, 30]`
  since C4 (`t_259ac697`). These carry the stat lines. Every location in §3 is labelled with the budget
  band its `MinLevel` falls in.
- **D1 roster bands** — `1-3, 4-6, 7-9, 10-12, 13-15, 16-18, 19-20`, plus the **tail band `21-30`**. These
  band the *archetypes*, and they are what the one-band tolerance rule is measured on. D6/L6 added five
  tail rows to that roster without splitting the band; the split is D0's, not D1's.
- Levels 21–30 were D1's declared gap ("needs its own card"), and only two *seeded* archetypes sit there:
  `pyreling` (seeded 23–28) and `fire-ancient` (seeded 26–30, a boss). D6/L6 authored five tail rows on
  top of them, of which `ash-eater` (26–30) and `maw-serpent` (28–30) are above level 26. That gap — and
  the one pool the published band re-measures — is §7.

**The one-band rule, stated precisely** (the card asks for it; it needs a definition to be testable):
for a pool entry at location `L`, `|band(archetype) − band(L.MinLevel)| ≤ 1` on the D1 ladder, where
`band(21+) = tail`. Locations are the unit because a pool belongs to a location, not to a zone
(`ZoneLocation.ActorPool`), and a zone like Tidewrack spans three bands (L12–18). A zone-level reading of
the rule would forbid `gull-rider-brute` (band 16–18) from `tidewrack-flats` — which D1 itself places
there — so the location is the only reading that is self-consistent.

**The tolerance cap, stated just as precisely** (approved 2026-11-20; it needs a definition to be
testable): an entry is *above its location* when its band is above **every** band that location's own
level range occupies — `band(archetype) > band(L.MaxLevel)` — and a pool may hold at most one such
entry, at `weight ≤ 2`. The bound and the cap answer different questions and both are needed: the bound
keeps a 4–6 room from spawning 10–12 content, the cap stops a room from stacking content above its own
ceiling. Reading the cap off `band(L.MinLevel)` instead would forbid a location its own native content —
all three of `wrack-shore`'s 13–15 entries in a 12–18 room, the whole `fenland-crossing` pool in a 9–13
fen, and `bandit-ruffian` at the crossing its own identity line says it holds — so the location's own
range is the only reading that leaves D1's placements intact. §2 lists every adjacent entry so the
difference is measured rather than asserted.

**One addition for the tail, from C4 (`t_259ac697`).** D1's ladder has a single band above level 20, so
no two tail objects can be one band apart on it — which is why §3 could draw `kaldreks-heart` as legal.
D0's table now splits the tail, so a tail entry is measured there instead: `pyreling`'s `MinLevel` 23 puts
it in `b23-26` (`budget.yaml`, `b23-26.seeded_archetypes`) and `kaldreks-heart`'s floor of 28 puts the
location in `b27-30` — one band apart. D4's cap number (one band-tolerant entry per pool, `weight ≤ 2`)
is applied to that pair by L6's decision 2, and §7 records what it does and does not change.

## 2. The rules, and the check for each

**28** is the number of pooled locations after the amendments (was 29) and **74** the number of entries
(was 76) — two locations went quiet (amendments 3 and 4), one came into the pools, and amendment 5 adds
`maw-serpent` to `kaldreks-heart`. Every number below comes from the audit script described at the end of
this section, run over these files and the two seeders.

| Rule | Result |
|---|---|
| Every hostile location has **≥ 1 pool entry**; the card asks for ≥ 2 | **28 of 28** have 2–4 entries. `kaldreks-heart` was the one entry short until amendment 5 |
| Every roster archetype appears in at least one pool | **26 of 27** hostile D1 roster archetypes are pooled. `bandit-captain` is **withheld on purpose** — D0 approved "bosses ×2–4, pools held until a uniqueness mechanism exists", and nothing today stops a pooled boss from filling all 2–4 slots (`ActorPoolResolver.cs:47-51`). D6's five tail rows are outside that count; §6 and §7 carry them |
| No archetype appears more than one band from its location's entry band | **all 74 entries pass** on the D1 ladder + tail band. **3 entries** additionally break D1's per-archetype *zone* list (`cinderling` outside `ashfields`, §7) — a documented deviation, not an oversight |
| **Tolerance cap (2026-11-20):** at most **one** entry above its location's top band, that entry `weight ≤ 2` | **1 of 74 entries is above its location's top band** — `moor-wight` in `bone-hollow`, now weight 1 (amendment 2). **0 pools breach the cap.** The 30 adjacent entries are listed below with their bands, so the difference between "adjacent" and "above" is measured, not asserted. One entry is out of band without being above it: `pyreling` at `kaldreks-heart` is the same D1 tail band as its location and one band **below** it on the published `b27-30` table — amendment 5, weight 2 |
| Weights are relative integers, no zero, no percentages | **all 74 entries** are integers in `1..6`; no pool sums to 100 and none is normalised. The seeder's pre-amendment `60/40` pairs are the same ratios written as percentages-looking numbers, which is why these are not |
| Every pool entry survives the seeder's own overlap rule (`archetype.MinLevel <= L.MaxLevel && archetype.MaxLevel >= L.MinLevel`) | **all 74 pass** against the seeded ranges, and against the level range each new archetype must be seeded with (§6) — including the two whose seeded range is widened to `19–22` by the amendments |
| Every pooled archetype exists and is hostile | **not true yet, by construction**: 16 of the 28 pooled archetypes do not exist in `ArchetypeSeeder.cs` — the 15 D1 rows plus D6's `maw-serpent`. The import gate refuses them until they are seeded (§8) |

### The 30 adjacent entries, and why 29 of them are not tolerance

An *adjacent* entry is one band away from the band its location's `MinLevel` falls in — the D4 bound.
An entry is only *above* its location when its band is above the top band the location's own range
occupies. Every adjacent entry is listed here: **22 sit above the entry band and 8 below it; of the 22,
21 are still inside the location's own range** and are its native content, not tolerance. The 22nd is
`bone-hollow`'s wight, above the range entirely — which is the case the cap exists for.

| location | location levels | entry | entry band | weight | from entry band | inside the location's range? |
|---|---|---|---|---|---|---|
| `scorched-battlefield` | 21-25 | `cinderling` | 19-20 | 5 | -1 | yes |
| `scorched-battlefield` | 21-25 | `ash-jack` | 19-20 | 3 | -1 | yes |
| `scorched-battlefield` | 21-25 | `forge-deserter` | 19-20 | 3 | -1 | yes |
| `wood-road` | 1-5 | `forest-wolf` | 4-6 | 4 | +1 | yes |
| `old-clearing` | 2-5 | `forest-wolf` | 4-6 | 6 | +1 | yes |
| `ancestor-vault` | 11-14 | `drowned-revenant` | 13-15 | 5 | +1 | yes |
| `relic-chamber` | 12-14 | `drowned-revenant` | 13-15 | 4 | +1 | yes |
| `mire-crossing` | 3-6 | `mire-boar` | 4-6 | 4 | +1 | yes |
| `mire-crossing` | 3-6 | `bandit-ruffian` | 4-6 | 3 | +1 | yes |
| `mire-crossing` | 3-6 | `forest-wolf` | 4-6 | 2 | +1 | yes |
| `bone-hollow` | 4-6 | `moor-wight` | 7-9 | 1 | +1 | **no — above the top band** |
| `fire-ancients-chamber` | 26-30 | `cinderling` | 19-20 | 3 | -1 | yes |
| `ashveil-highlands` | 7-12 | `cairn-haunt` | 10-12 | 2 | +1 | yes |
| `shifting-waymark` | 8-14 | `cairn-haunt` | 10-12 | 4 | +1 | yes |
| `gull-rider-camp` | 15-20 | `gull-rider-brute` | 16-18 | 5 | +1 | yes |
| `gull-rider-camp` | 15-20 | `cliff-breaker` | 16-18 | 2 | +1 | yes |
| `vent-fields` | 23-26 | `cinderling` | 19-20 | 3 | -1 | yes |
| `pyreling-den` | 24-26 | `cinderling` | 19-20 | 2 | -1 | yes |
| `fenland-crossing` | 9-13 | `fen-lurker` | 10-12 | 5 | +1 | yes |
| `fenland-crossing` | 9-13 | `bog-lantern` | 10-12 | 3 | +1 | yes |
| `fenland-crossing` | 9-13 | `cairn-haunt` | 10-12 | 2 | +1 | yes |
| `submerged-ruins` | 10-14 | `drowned-revenant` | 13-15 | 4 | +1 | yes |
| `flooded-throne-room` | 16-20 | `drowned-revenant` | 13-15 | 4 | -1 | yes |
| `tidelocked-vault` | 17-20 | `drowned-cantor` | 19-20 | 4 | +1 | yes |
| `tidelocked-vault` | 17-20 | `drowned-revenant` | 13-15 | 2 | -1 | yes |
| `wrack-shore` | 12-18 | `tide-strider` | 13-15 | 4 | +1 | yes |
| `wrack-shore` | 12-18 | `drowned-revenant` | 13-15 | 3 | +1 | yes |
| `wrack-shore` | 12-18 | `gull-rider-skirmisher` | 13-15 | 3 | +1 | yes |
| `bone-strand` | 13-18 | `gull-rider-brute` | 16-18 | 3 | +1 | yes |
| `tidal-grotto` | 14-18 | `storm-harrower` | 16-18 | 3 | +1 | yes |

Verification was a script over the two seeder files and this matrix's own data, not by eye: it parses all
67 location blocks, all 17 seeded archetypes, the D1 roster and the 18 packs, then checks the rules above
plus the two seeder guard tests (`SeededEncounterCoverageTests.cs:28-52`, `:77-144`) entry by entry, and
proves the exclusion set equals town ∪ `SafeRespawn` ∪ lobby exactly. Result: `PROBLEMS: none`, with the
three `cinderling` zone-list deviations reported as warnings, not failures (§7).


## 3. The matrix

`band` is the D0 budget band of the location's `MinLevel`. `roster` is 2–4 for every row because the
engine has one global constant pair and no per-location field — the column exists so nobody adds one
without noticing (`ActorPoolResolver.cs:13-14`).

| zone | location | level range | band | pool entries (slug + weight) | roster | why this location is dangerous |
|---|---|---|---|---|---|---|
| `the-droveway` | `drove-road` | 1–4 | b1-3 | `giant-rat` 5, `goblin-scout` 3, `drove-cur` 3, `peat-adder` 2 | 2–4 | The cattle road north out of Crestfall (d2). Grain spills at the crossings and the rats that eat it have stopped running from people; goblin packs shadow the carts; the dry-stone walls hide curs and adders. |
| `the-droveway` | `sunken-fields` | 2–4 | b1-3 | `peat-adder` 5, `giant-rat` 4, `drove-cur` 2 | 2–4 | Pasture gone to standing water, behind a passive Perception check (DC 5). Nothing here is visible from the road, and the adders strike at anything that breaks the surface. |
| `ashlen-wood` | `wood-road` | 1–5 | b1-3 | `goblin-scout` 6, `forest-wolf` 4 | 2–4 | The cart road into the Ashlen Wood (d2), where the light is wrong. Goblin scouts shadow it and report back to somewhere; wolves hunt it in threes. |
| `ashlen-wood` | `old-clearing` | 2–5 | b1-3 | `forest-wolf` 6, `goblin-scout` 4 | 2–4 | A clearing the wood refuses to grow over (d3). Wolves know where the clearings end, and a lone traveller between two of them is the whole of their plan. |
| `grevenmire` | `mire-crossing` | 3–6 | b1-3 | `peat-adder` 4, `mire-boar` 4, `bandit-ruffian` 3, `forest-wolf` 2 | 2–4 | The one ford through Grevenmire (d4) — the only way in on foot. Bandits hold it and take a cut of what passes; boars charge the smell of smoke and metal; adders wait in the bog grass. |
| `grevenmire` | `bone-hollow` | 4–6 | b4-6 | `bandit-ruffian` 5, `mire-boar` 3, `moor-wight` **1** | 2–4 | A hollow of stacked bone behind a passive check (DC 8, d6). A wight walks it with a cold that spoils a fire, and it does not care who built the hollow. Weight 1: the wight is the room's identity, not its difficulty (amendment 2). |
| `the-halrow` | `rootbound-cellars` | 4–6 | b4-6 | `bandit-ruffian` 5, `sump-crawler` 5 | 2–4 | The Halrow's cellars, walls standing and roof long collapsed (d4). Bandits camp the dry rooms; the flooded lower half is somebody else's ground. |
| `the-halrow` | `collapsed-vault` | 5–6 | b4-6 | `sump-crawler` 6, `bandit-ruffian` 4 | 2–4 | Quest-gated (d5) vault under the Halrow. The bandit band's cache is in it, and so is the water that followed them down. |
| `drowning-pits` | `flooded-workings` | 4–6 | b4-6 | `sump-crawler` 6, `bandit-ruffian` 4 | 2–4 | The Drowning Pits' flooded upper workings (d5). Whatever was built here was not built for humans; what is in the workings is what came up around it. |
| `drowning-pits` | `deepest-chamber` | 5–6 | b4-6 | `bandit-ruffian` 5, `sump-crawler` 5 | 2–4 | Quest-gated (d6) bottom of the Pits. What is sealed below is atmosphere and a scripted encounter, never a pool enemy (roster proposal 3), so the roster here is the ordinary bandit-and-crawler content that reached the door. |
| `pale-moor` | `ashveil-highlands` | 7–12 | b7-10 | `moor-wight` 4, `barrow-bear` 4, `moor-tollman` 3, `cairn-haunt` 2 | 2–4 | Moor-high ground (d5) where the cairns stand at the edge of sight. Wights, a bear that digs the mounds open for the salt in the bones, and a tollman at a gate nobody built. |
| `pale-moor` | `shifting-waymark` | 8–14 | b7-10 | `moor-tollman` 5, `cairn-haunt` 4, `moor-wight` 2 | 2–4 | A waymark that is somewhere else each night, behind a passive check (DC 10, d6). The tollman collects there; a haunt stands on the cairn at dusk and does not move while you look at it. |
| `soddenfen` | `fenland-crossing` | 9–13 | b7-10 | `fen-lurker` 5, `bog-lantern` 3, `cairn-haunt` 2 | 2–4 | The only dry crossing of Soddenfen (d5). A lurker sits under the duckweed waiting for a horse to drink, and a bog lantern walks the path with you. |
| `soddenfen` | `submerged-ruins` | 10–14 | b7-10 | `drowned-revenant` 4, `bog-lantern` 3, `cairn-haunt` 3 | 2–4 | A drowned hall behind an active search (DC 12, d7). The drowned dead came back up still carrying what drowned them, and the lanterns moved in with them. |
| `barrow-deeps` | `ancestor-vault` | 11–14 | b11-14 | `drowned-revenant` 5, `burrow-worm` 5 | 2–4 | The Barrow Deeps' main vault (d7): ancestor-beasts in the aisles and a worm that tunnels the old burial network and surfaces in the middle of a camp. |
| `barrow-deeps` | `relic-chamber` | 12–14 | b11-14 | `burrow-worm` 6, `drowned-revenant` 4 | 2–4 | Quest-gated inner chamber (d8) where the worm's tunnels converge. The revenants followed the water down and have not left. |
| `tidewrack-flats` | `wrack-shore` | 12–18 | b11-14 | `tide-strider` 4, `drowned-revenant` 3, `gull-rider-skirmisher` 3 | 2–4 | Tidewrack's working shore (d5): wading predators on long legs and gull-riders who throw a harpoon on the way past. |
| `tidewrack-flats` | `bone-strand` | 13–18 | b11-14 | `gull-rider-skirmisher` 4, `tide-strider` 3, `gull-rider-brute` 3 | 2–4 | A strand of shipwreck timber and kelp-draped bones (d6). The clan sends its heavy members when harpoons are not enough. |
| `tidewrack-flats` | `tidal-grotto` | 14–18 | b11-14 | `drowned-revenant` 4, `tide-strider` 3, `storm-harrower` 3 | 2–4 | A sea cave behind an active search (DC 14, d7). The drowned rooms run inland from it and the wind turns with what is in them. |
| `saltcliff-heights` | `gull-rider-camp` | 15–20 | b15-18 | `gull-rider-brute` 5, `gull-rider-skirmisher` 3, `cliff-breaker` 2 | 2–4 | A working clan camp (d7), not a ruin: skirmishers on the updraught, brutes on the ground, and a cliff-breaker nesting above the only path in. |
| `saltcliff-heights` | `storm-watch-peak` | 16–20 | b15-18 | `storm-harrower` 6, `cliff-breaker` 4 | 2–4 | Achievement-gated summit (d8) where a shape in the rain turns the wind. Anything posted here can drop stone on the path beneath without coming down. |
| `sunken-name` | `flooded-throne-room` | 16–20 | b15-18 | `drowned-revenant` 4, `storm-harrower` 3, `undertow` 3 | 2–4 | The drowned empire's throne room (d9). Revenants stand in the water and the undertow pulls the wrong way — toward the dry ground, i.e. toward you. |
| `sunken-name` | `tidelocked-vault` | 17–20 | b15-18 | `undertow` 5, `drowned-cantor` 4, `drowned-revenant` 2 | 2–4 | The deepest room of the Sunken Name, quest-gated (d10). A cantor sings in it and the water carries the note further than the hall is long. |
| `ashfields` | `scorched-battlefield` | 21–25 | b19-22 | `cinderling` 5, `ash-jack` 3, `forge-deserter` 3 | 2–4 | The ash of the armies is still deep on the old battlefield (d9), and it is the zone's only hostile node since 2026-11-20. Cinder knots leave trails of fused ash across it, jackdaws gather where something is dying, and deserters work the field for gear that will still sell. |
| `smoldering-reach` | `vent-fields` | 23–26 | b23-26 | `pyreling` 5, `cinderling` 3 | 2–4 | A cracked lava field (d9) where superheated vents erupt without warning. Heat-adapted predators hunt at dusk and there is nowhere on the field to stand still. |
| `smoldering-reach` | `pyreling-den` | 24–26 | b23-26 | `pyreling` 6, `cinderling` 2 | 2–4 | The den itself (d10) — the densest pyreling ground in the world, and the reason the field around it is empty of anything else. |
| `kaldrek-maw` | `fire-ancients-chamber` | 26–30 | b23-26 | `pyreling` 5, `cinderling` 3 | 2–4 | The chamber the fire-ancient is named after (d10). Its occupant is withheld from the pool until a uniqueness mechanism exists (D0 bosses / gap G7), so the room is guarded by the pyrelings that den in it. |
| `kaldrek-maw` | `kaldreks-heart` | 28–30 | b27-30 | `maw-serpent` 6, `pyreling` 2 | 2–4 | The Maw's last room, quest-gated (d10). Amendment 5: `maw-serpent` (28–30) is in the room's own band and carries it at the weight `pyreling` held; `pyreling` stays as the one band-tolerant entry at weight 2, because it spawns at its `MinLevel` of 23 with no roll — a 23rd-level pyreling in a 28 room is the tolerance, so it is the rarest draw in the pool (1 in 4). |

74 entries across 28 locations. 3 locations carry four entries, the rest two or three — after amendment 5
every pooled location carries at least two (§7).

## 4. Exclusions — 26 locations that are empty on purpose

Every one of the 26 is mechanically justified by a trait, not by taste. The importer must write an empty
pool for each, and a non-empty pool here is an error, not a preference. The list is the exact complement
of §3: 28 pooled + 26 quiet = the seeder's 54 world locations.

### Dungeon lobbies (5) — `IsDungeon = false` + `SafeRespawn`

| zone | location | level range | band | why it must stay empty |
|---|---|---|---|---|
| `the-halrow` | `halrow-threshold` | 4–6 | b4-6 | Dungeon lobby (IsDungeon=false + SafeRespawn). The Halrow's instance boundary and reset point. |
| `drowning-pits` | `pit-descent` | 4–6 | b4-6 | Dungeon lobby (IsDungeon=false + SafeRespawn). The Drowning Pits' instance boundary. |
| `barrow-deeps` | `deeps-entrance` | 11–14 | b11-14 | Dungeon lobby (IsDungeon=false + SafeRespawn). |
| `sunken-name` | `drowned-threshold` | 16–20 | b15-18 | Dungeon lobby (IsDungeon=false + SafeRespawn). |
| `kaldrek-maw` | `maw-descent` | 26–30 | b23-26 | Dungeon lobby (IsDungeon=false + SafeRespawn). Kaldrek's Maw instance boundary. |

### Respawn points and sanctuaries (11) — `SafeRespawn`

| zone | location | level range | band | why it must stay empty |
|---|---|---|---|---|
| `crestfall` | `crestfall-square` | 0+ | below b1-3 | SafeRespawn + IsTown. The Varenmark respawn point; respawning into hostiles is a death loop (plan §P0, SeededEncounterCoverageTests.cs:28-52). |
| `the-droveway` | `weathered-waypost` | 1–4 | b1-3 | SafeRespawn. The Droveway respawn point; the wayfarer-elder is placed here as an NPC. |
| `ashlen-wood` | `strangled-oak` | 2–5 | b1-3 | SafeRespawn + hidden (passive DC 5). A safe island inside a hostile wood — exactly where a player who fled needs it not to bite. |
| `grevenmire` | `sunken-estate` | 3–6 | b1-3 | SafeRespawn. Respawn point inside Grevenmire; note the seed lists it as a candidate hostile location in plans/content-seeding-pass.md:71 — that line is stale. |
| `aldenmere` | `ironhollow-keep` | 5–12 | b4-6 | SafeRespawn + IsTown. Aldenmere's keep and respawn point. |
| `pale-moor` | `moorstone-cairns` | 7–12 | b7-10 | SafeRespawn. Respawn point on the Pale Moor — the one safe ground in the zone the wights walk. |
| `tolvaren` | `tolvaren-harbour` | 10+ | b7-10 | SafeRespawn + IsTown. Saltcliff's respawn point; dock-hand is placed here as an NPC. |
| `saltcliff-heights` | `clifftop-ruins` | 14–20 | b11-14 | SafeRespawn. Respawn point on the Saltcliff Heights. |
| `skarhold` | `forge-quarter` | 18+ | b15-18 | SafeRespawn + IsTown. Skarhold's respawn point and forge/shop location. |
| `ashfields` | `obsidian-grove` | 20–25 | b19-22 | SafeRespawn since 2026-11-20 (amendment 4): the ashfields respawn point, moved here from `scorched-battlefield`. It is the node nearest the zone's arrival point, so it is what a dead character walks out of. |
| `smoldering-reach` | `lava-bridge` | 24–26 | b23-26 | SafeRespawn + hidden (active DC 18). The only crossing to the Maw is also its respawn point. |

### Keeper-only shrines (1) — `SafeRespawn` + the only population is an NPC

| zone | location | level range | band | why it must stay empty |
|---|---|---|---|---|
| `ashfields` | `ash-shrine` | 20–25 | b19-22 | SafeRespawn since 2026-11-20 (amendment 3): a keeper-only shrine is a rest point, not a spawn. `shrine-keeper` is a `LocationNpc` row (D1), never a pool entry. It is also `IsHidden` + quest-unlocked, so the hidden-filtered zone query never offers it as the ashfields respawn anchor — that stays `obsidian-grove`. |

### Settlements (9) — `IsTown`

| zone | location | level range | band | why it must stay empty |
|---|---|---|---|---|
| `crestfall` | `aurelian-market` | 1+ | b1-3 | IsTown. Merchant location; its population is NPCs and shop stock, placed through LocationNpc, never a pool entry. |
| `crestfall` | `hearthbound-inn` | 1+ | b1-3 | IsTown. Inn: rest and dialogue. The tavern-keeper is a LocationNpc row. |
| `crestfall` | `low-quarter` | 1+ | b1-3 | IsTown. Residential quarter; NPC-only population. |
| `aldenmere` | `aldenmere-marketplace` | 5+ | b4-6 | IsTown. Merchant location. |
| `aldenmere` | `grey-cup` | 5+ | b4-6 | IsTown. Inn. |
| `tolvaren` | `cliff-road-market` | 10+ | b7-10 | IsTown. Merchant location. |
| `tolvaren` | `saltcrow-inn` | 10+ | b7-10 | IsTown. Inn. |
| `skarhold` | `caldera-market` | 18+ | b15-18 | IsTown. Merchant location. |
| `skarhold` | `ashbrand-lodge` | 18+ | b15-18 | IsTown. Inn. |

9 locations are excluded by more than one trait at once (`crestfall-square` is both the town square and
the respawn point; `ironhollow-keep`, `tolvaren-harbour`, `forge-quarter` likewise); each is counted once,
under its strongest reason. The four classes add up to the 26: **5 lobbies + 11 sanctuaries + 1 shrine +
9 settlements**.

### The fourth exclusion class: "only population is an NPC"

That test adds **one location, and it is counted above rather than here** — `ash-shrine`, which is
keeper-only and is counted under the shrine class so it is not counted twice. Before 2026-11-20 the class
had no live member: every NPC-only location in the seed is also a town (inn, market, square, lodge,
quarter), and NPCs are placed by `LocationNpc` rows keyed by zone + location, never by pool entries
(`plans/content-seeding-pass.md` §People). D1 marks every non-hostile roster row `§` and says it never
enters a pool — 17 rows once D3's ten townspeople landed. **None of them may enter a pool either way**,
so the class changes no pool. It exists in this file so a future NPC-only location is excluded by rule
rather than by memory — which is exactly how `ash-shrine` got its pool removed.

## 5. Coverage

`locations` is the seeder's own per-zone count and `hostile` means "not excluded"; `pooled` must equal
`hostile` in every zone, and does.

| zone | region | locations | hostile | pooled | excluded |
|---|---|---|---|---|---|
| `crestfall` | varenmark | 4 | 0 | **0** | 4 |
| `the-droveway` | varenmark | 3 | 2 | **2** | 1 |
| `ashlen-wood` | varenmark | 3 | 2 | **2** | 1 |
| `grevenmire` | varenmark | 3 | 2 | **2** | 1 |
| `the-halrow` | varenmark | 3 | 2 | **2** | 1 |
| `drowning-pits` | varenmark | 3 | 2 | **2** | 1 |
| `aldenmere` | greymoor | 3 | 0 | **0** | 3 |
| `pale-moor` | greymoor | 3 | 2 | **2** | 1 |
| `soddenfen` | greymoor | 2 | 2 | **2** | 0 |
| `barrow-deeps` | greymoor | 3 | 2 | **2** | 1 |
| `tolvaren` | saltcliff | 3 | 0 | **0** | 3 |
| `tidewrack-flats` | saltcliff | 3 | 3 | **3** | 0 |
| `saltcliff-heights` | saltcliff | 3 | 2 | **2** | 1 |
| `sunken-name` | saltcliff | 3 | 2 | **2** | 1 |
| `skarhold` | cinderplain | 3 | 0 | **0** | 3 |
| `ashfields` | cinderplain | 3 | 1 | **1** | 2 |
| `smoldering-reach` | cinderplain | 3 | 2 | **2** | 1 |
| `kaldrek-maw` | cinderplain | 3 | 2 | **2** | 1 |
| **18 world zones** | | **54** | **28** | **28** | **26** |
| `playground` (dev-only, 19th zone) | — | 13 | — | 0 | 13 |
| **total seeded locations** | | **67** | | | |

`28 + 26 = 54` world locations, plus the 13 `playground` fixtures =
**67**, which is the seeder's 67 (`ZoneLocationsSeeder.cs`, parsed block count).
`playground` is a dev-only zone — `Zone.IsDevOnly` (`Veldrath.Server/Data/Entities/Zone.cs:42`), seeded
with `IsDevOnly = true` in `Veldrath.Server/Data/Seeders/ApplicationDataSeeder.cs:98-99` and excluded from
every public zone query (`ZoneRepository.cs:10-22`). Its eight token pools are
e2e fixtures, not world content, and this matrix neither covers nor changes them. Every one of the 18
world zones is fully accounted for: no hostile location is unpooled, no safe location is pooled.

## 6. Archetype coverage — every roster archetype gets a place

| archetype | band | pools containing it | count |
|---|---|---|---|
| `drove-cur` | 1-3 | `drove-road`, `sunken-fields` | 2 |
| `giant-rat` | 1-3 | `drove-road`, `sunken-fields` | 2 |
| `goblin-scout` | 1-3 | `drove-road`, `wood-road`, `old-clearing` | 3 |
| `peat-adder` | 1-3 | `drove-road`, `sunken-fields`, `mire-crossing` | 3 |
| `bandit-ruffian` | 4-6 | `mire-crossing`, `bone-hollow`, `rootbound-cellars`, `collapsed-vault`, `flooded-workings`, `deepest-chamber` | 6 |
| `forest-wolf` | 4-6 | `wood-road`, `old-clearing`, `mire-crossing` | 3 |
| `mire-boar` | 4-6 | `mire-crossing`, `bone-hollow` | 2 |
| `sump-crawler` | 4-6 | `rootbound-cellars`, `collapsed-vault`, `flooded-workings`, `deepest-chamber` | 4 |
| `bandit-captain` | 7-9 | **withheld** — boss; D0 holds boss pools until a uniqueness mechanism exists | 0 |
| `barrow-bear` | 7-9 | `ashveil-highlands` | 1 |
| `moor-tollman` | 7-9 | `ashveil-highlands`, `shifting-waymark` | 2 |
| `moor-wight` | 7-9 | `bone-hollow`, `ashveil-highlands`, `shifting-waymark` | 3 |
| `bog-lantern` | 10-12 | `fenland-crossing`, `submerged-ruins` | 2 |
| `burrow-worm` | 10-12 | `ancestor-vault`, `relic-chamber` | 2 |
| `cairn-haunt` | 10-12 | `ashveil-highlands`, `shifting-waymark`, `fenland-crossing`, `submerged-ruins` | 4 |
| `fen-lurker` | 10-12 | `fenland-crossing` | 1 |
| `drowned-revenant` | 13-15 | `submerged-ruins`, `ancestor-vault`, `relic-chamber`, `wrack-shore`, `tidal-grotto`, `flooded-throne-room`, `tidelocked-vault` | 7 |
| `gull-rider-skirmisher` | 13-15 | `wrack-shore`, `bone-strand`, `gull-rider-camp` | 3 |
| `tide-strider` | 13-15 | `wrack-shore`, `bone-strand`, `tidal-grotto` | 3 |
| `cliff-breaker` | 16-18 | `gull-rider-camp`, `storm-watch-peak` | 2 |
| `gull-rider-brute` | 16-18 | `bone-strand`, `gull-rider-camp` | 2 |
| `storm-harrower` | 16-18 | `tidal-grotto`, `storm-watch-peak`, `flooded-throne-room` | 3 |
| `undertow` | 16-18 | `flooded-throne-room`, `tidelocked-vault` | 2 |
| `ash-jack` | 19-20 | `scorched-battlefield` | 1 |
| `cinderling` | 19-20 | `scorched-battlefield`, `vent-fields`, `pyreling-den`, `fire-ancients-chamber` | 4 |
| `drowned-cantor` | 19-20 | `tidelocked-vault` | 1 |
| `forge-deserter` | 19-20 | `scorched-battlefield` | 1 |
| `fire-ancient` | 21-30 | **withheld** — boss; D0 holds boss pools until a uniqueness mechanism exists | 0 |
| `pyreling` | 21-30 | `vent-fields`, `pyreling-den`, `fire-ancients-chamber`, `kaldreks-heart` | 4 |
| `maw-serpent` | 28-30 | `kaldreks-heart` (amendment 5) | 1 |

**What must exist before these pools can be imported.** 16 of the pooled archetypes are roster rows that
`ArchetypeSeeder.cs` does not contain yet — the 15 D1 rows below plus D6's `maw-serpent` (28–30, §7),
which C4 pooled under amendment 5. Each needs the level range in this table, or the pool entry fails the
two guards that already exist:

| new archetype | band to seed |
|---|---|
| `drove-cur` | 1–3 |
| `peat-adder` | 1–3 |
| `sump-crawler` | 4–6 |
| `mire-boar` | 4–6 |
| `barrow-bear` | 7–9 |
| `moor-tollman` | 7–9 |
| `burrow-worm` | 10–12 |
| `bog-lantern` | 10–12 |
| `tide-strider` | 13–15 |
| `gull-rider-skirmisher` | 13–15 |
| `cliff-breaker` | 16–18 |
| `undertow` | 16–18 |
| `drowned-cantor` | 19–20 |
| `ash-jack` | 19–22 |
| `forge-deserter` | 19–22 |
| `maw-serpent` | 28–30 |

The two `19–22` rows are the amendment consequence (§Amendments): the roster bands them 19–20, but after
amendments 3 and 4 the only hostile location left in their zone (`scorched-battlefield`) starts at level
21, and the overlap guard needs `MaxLevel >= 21`. Seed them at 19–22 and both pools work; seed them at
19–20 and the import fails `SeededEncounterCoverageTests.cs:132-143` for two of the three entries.

The ranges are D1's bands, not new numbers: D1 says a row's band is its level band, and the seeder needs
`MinLevel`/`MaxLevel` to run the overlap check. A pool entry whose archetype is missing logs an Error and
spawns nothing (`ActorPoolResolver.cs:52-60`) and fails `SeededEncounterCoverageTests.cs:77-110`, so the
order of landing is fixed: **archetypes first, then pools.**

## 7. The tail — levels 21–30, where this matrix cannot fully meet its own rules

D1 declared 21–30 out of scope and said so honestly ("the tail is not empty — but it needs its own card").
Three consequences landed on this card, and they are reported rather than papered over. D6/L6 answered the
first two on 2026-11-20 and C4 published the band that re-measures the second (§7.1, §7.2); the third is
still D6's to close or D5's to accept.

1. **There are exactly two *seeded* archetypes above level 22**: `pyreling` (seeded 23–28) and
   `fire-ancient` (26–30). One of the two is a boss, which D0 holds out of every pool. D6/L6 authored five
   non-boss tail rows on top of them (23–26, 26–30 and 28–30); none is seeded yet (§6).
2. **`kaldreks-heart` (L28–30) had a single-entry pool.** `pyreling` was the only non-boss archetype whose
   seeded range reached 28; `cinderling` stops at 26 and would fail the overlap guard. It reached two
   entries with amendment 5 (§7.2), and without using the boss.
3. **`cinderling` is used outside its D1 zone list in three tail locations** (`vent-fields`,
   `pyreling-den`, `fire-ancients-chamber`). D1 lists `ashfields` only. The in-band test passes (D1 band
   19–20 is one step from the tail band, and the seeded range 20–26 overlaps all three locations), and
   the region is right — it is a Cinderplain elemental — but the zone list is D1's, and this is a
   deviation. Without it, all four tail locations would be pyreling alone or would need the boss.

### 7.1 What the band extension changed, and what it did not

`budget.yaml` published `b27-30` and now covers 1–30 (C4, `t_259ac697`). The band is anchored on the only
things that exist at that height:

- **`fire-ancient` is the only seeded archetype there** — 900 HP, 42 damage, 1200 XP, 150–300 gold,
  seeded 26–30 (`ArchetypeSeeder.cs:662-669`). It is a boss and stays out of every pool, so it sets the
  band's *ceiling* rather than its population: `hp.max` is 450 exactly because 900 / 2 is the floor of
  D0's approved `bosses.hp_multiplier`, and `xp` and `gold` check out against the same multipliers.
- **`ash-eater` (26–30) and `maw-serpent` (28–30) are the only authored entries** (D6/L6,
  `archetypes.md`), and neither carries a stat line yet. Every value in `b27-30` is therefore a range
  continued from `b23-26` by the step the ladder last took — one basis line per value, in `budget.yaml`
  itself, so the arithmetic can be re-run rather than re-argued.
- **No hostile archetype was re-banded to fill it.** The band exists because the rooms run to level 30
  (`ZoneLocationsSeeder.cs:590, :598, :606`), not because a slot needed a tenant. A band whose only seeded
  occupant is a boss is the honest state of the ladder at 27–30.
- **No boss is authored for 27–30.** `fire-ancient` already occupies the tier and stays unpooled: boss
  placement is still held for the uniqueness mechanism D0 named.

This is also why the tail needed **two** authored rows above 26 instead of one 26–30 row. Spawn level is
the archetype's `MinLevel` with no roll (`budget.yaml`, *wiring.level*; `EfCoreEnemyRepository.cs:52`), so
a 26–30 archetype spawns at 26 in every room it is pooled in: `ash-eater` covers
`fire-ancients-chamber`'s floor of 26, and only `maw-serpent` (28–30) can stand on `kaldreks-heart`'s
floor of 28.

### 7.2 `kaldreks-heart` after `b27-30` — the one pool the extension re-measures

On D1's single `21-30` tail band, `pyreling` and `kaldreks-heart` are the same band, which is why §3 drew
the pool as legal. On the published D0 table they are not: `kaldreks-heart` (floor 28) is `b27-30` and
`pyreling` is `b23-26` — one band apart. L6's decision 2 applies D4's cap number to that pair:

| entry | band | weight before | weight now | why |
|---|---|---|---|---|
| `maw-serpent` | 28–30 | — | **6** | in the room's own band; it takes the weight D4 drew for `pyreling` |
| `pyreling` | `b23-26` | 6 | **2** | one band below the room, and it spawns at 23 because nothing rolls; the cap's maximum is its ceiling |

The ratio is 3:1, so a Heart roster draws a pyreling about one slot in four. The archetype is still in the
room — it dens in it (§3) — but it is no longer what the room *is*. Nothing else moved: the other 27
pools, the other six bands, `merchants:` and every seeder trait are untouched.

**Reference closure, stated for the consolidation pass.** Every pool slug resolves at runtime against
`ArchetypeSeeder.cs`, and 16 of them need to be added there first (§6). `pyreling` resolves there today
but is **not** in `archetypes.md`, because D1 put 21–30 out of scope — so a reference-closure check that
reads only the roster will report it as missing. It is not missing; it is seeded and out of roster scope.
D6/L6 added their five new tail rows but did not add `pyreling`, so the tail landing does not close this:
it is a roster edit, not a pool edit.

**Status of the tail after C4.** Five tail locations, five tail pools, all ≥ 2 entries. Three are unchanged
since L3: `vent-fields`, `pyreling-den` and `fire-ancients-chamber` as drawn — the last still drawing the
deviant `cinderling`, because `ash-eater` is pooled nowhere yet (§10). `scorched-battlefield` (L21–25)
remains the fifth tail location, taking the grove's band-19–20 scavenger pool, which is why `ash-jack` and
`forge-deserter` must be seeded to 19–22 (§6). `kaldreks-heart` is the one that moved (§7.2).

## 8. The pack shape — and why the 18 files live in the design set for now

The card was written before a pack importer existed. **One landed on 2026-11-20**
(`RealmEngine.Data/Authoring/ContentPacks/`, card `t_f41880d2`, PR #108) with a CI guard:
[`ci-content.yml`](../../../.github/workflows/ci-content.yml) runs
`dotnet run --project Veldrath.ContentPack -- validate content/` on every pull request that touches
`content/**`. The importer takes one **family** per directory — `content/<family>/<batch>.yaml` with
`family:` and `entities:` at the top — and `zonelocation` is a family (`ContentEntityTypes`,
`docs/content-packs.md`). The shape below is close to it, and the two differences are both
Error-severity, which is why these files do not sit under `content/`:

| This document | The importer | Rule that fires |
|---|---|---|
| `content/pools/<zone>.yaml` | the directory name is the family, and `pools` resolves to no content type | `pack-family-unknown` — **Error** (`ContentPackRules.FamilyUnknown`, `ContentPackReader.ResolveFamily`) |
| `pool[].archetype` may name a roster archetype that is not seeded yet | every spawn entry must resolve in the catalogue | `missing-spawn-archetype` — **Error** (`ContentReferenceRules.AuditSpawnPools`) |

The second is the real blocker, and it is the landing-order problem this document has always named: 16 of
the 28 pooled archetypes do not exist yet (§6), so every one of the 18 files fails the guard as written.

**So the 18 files land at `docs/design/content/pools/<zone>.yaml`** — the design set, which `docfx.json`
already excludes from the published site — as design data, not importable packs. They move to
`content/zonelocations/<zone>.yaml` in the shape below once the archetypes are seeded.

Both failures were reproduced rather than assumed, by running the guard over a scratch pack root holding
one file of each shape:

```
Error   pack-family-unknown: '…/pools/ashfields.yaml' belongs to family 'pools', which is not a content type the engine stores. (field: family)
Error   missing-spawn-archetype [scorched-battlefield]: Zone location 'scorched-battlefield' spawns unknown actor archetype 'ash-jack'. (field: actorPool)
```

```yaml
# docs/design/content/pools/the-droveway.yaml (abridged)
schema_version: 1
zone: the-droveway
region: varenmark
locations:
  - slug: drove-road
    # band: b1-3 (D0 budget) | location L1-4
    level_range: [1, 4]
    pool:
      - archetype: giant-rat
        weight: 5   # band: 1-3 | seeded 1-3
      - archetype: goblin-scout
        weight: 3   # band: 1-3 | seeded 1-3
      - archetype: drove-cur
        weight: 3   # band: 1-3 | to be seeded 1-3
      - archetype: peat-adder
        weight: 2   # band: 1-3 | to be seeded 1-3
  - slug: weathered-waypost
    # band: b1-3 (D0 budget) | location L1-4
    level_range: [1, 4]
    excluded: true
    exclusion_class: safe_respawn
    exclusion_reason: 'SafeRespawn. The Droveway respawn point; the wayfarer-elder is placed here as an NPC.'
    pool: []   # empty by design
```

**What the design file means** — `zone`; `locations[].slug` (must resolve to a seeded `ZoneLocation`, and
to one in that zone); `locations[].pool[].archetype` → `ActorPoolEntry.ArchetypeSlug`;
`locations[].pool[].weight` → `ActorPoolEntry.Weight`; `locations[].excluded` as a validation assertion
(it writes nothing, but the exclusion must match the seeded traits).

**Everything else is annotation for reviewers and validators** and nothing must guess at it: `region`,
`level_range`, `exclusion_class`, `exclusion_reason`, the `# band:` comments, `schema_version`.

### The translation, written out

```yaml
# content/zonelocations/the-droveway.yaml — the import shape, once the archetypes exist
family: zonelocation
entities:
  - slug: drove-road
    displayName: The Drove Road      # required: display_name_missing
    typeKey: locations               # required: type_key_missing
    zoneId: the-droveway
    actorPool:
      - archetypeSlug: giant-rat
        weight: 5
  - slug: weathered-waypost
    displayName: The Weathered Waypost
    typeKey: locations
    zoneId: the-droveway
    traits:
      safeRespawn: true
    actorPool: []
```

Two things the translation has to respect, both from the importer's own documentation:

- **One family per file, family named by the directory** — `content/zonelocations/<zone>.yaml`, and the
  directory wins over a `family:` header that disagrees with it.
- **A pack entity owns the whole row.** An import replaces the stored entity's authored values, so a pack
  carrying only `actorPool` would blank the seeder's `displayName`, `stats` and `traits`. Generate the
  pack from `ZoneLocationsSeeder` rather than hand-writing the pool alone — which the seeder's insert-only
  behaviour makes necessary anyway.

**What the pack must satisfy, and the rule that enforces it** (existing rules, cited — not a wish list):

| Requirement | Rule / because |
|---|---|
| An entry whose archetype slug does not resolve is an Error | `missing-spawn-archetype`, `ContentReferenceRules.AuditSpawnPools`; `ActorPoolResolver.cs:52-71` |
| `weight <= 0` is an Error — the runtime would treat it as 1 | `loot_weight_invalid`, `ZoneLocationValidator`; `ActorPoolResolver.cs:103`, `:108` |
| An empty pool is only a **Warning**, because a safe location is legitimate | `spawn_pool_empty`, `ZoneLocationValidator`; `ActorPoolResolver.cs:39-45` |
| `displayName` and `typeKey` are required on every entity | `display_name_missing`, `type_key_missing`, `ContentShapeRules.ValidateShared` |
| A town, `SafeRespawn` location or lobby must not spawn anything | `SeededEncounterCoverageTests.cs:28-52`, and the same rule at runtime |
| The level-overlap check still runs | `SeededEncounterCoverageTests.cs:112-144` |

**Landing order — this is the part that will bite.** The pools name 16 archetypes that do not exist yet,
and the seeder only *inserts* (`plans/content-seeding-pass.md` §2: an existing slug is never updated). So:

0. **landed 2026-11-20 (L3, t_6c70dc04):** the three trait changes and the three seeder pools they force.
   `scorched-battlefield` is now pooled and hostile, `obsidian-grove` is the respawn point with no pool,
   and `ash-shrine` is `SafeRespawn` with no pool. The battlefield's *seeded* pool is the interim
   `cinderling 70 / pyreling 30` — the pool the grove used to carry, moved with the flag — because the
   designed entries (`ash-jack`, `forge-deserter`) do not exist yet. §9 records the difference;
1. seed the 16 archetypes (with hostility and the level ranges in §6) and merge that PR — `maw-serpent` is
   the sixteenth and it must land before `kaldreks-heart`'s pool imports;
2. then translate the 18 design files into `content/zonelocations/<zone>.yaml` (§8) and import them, which
   replaces that interim pair with the designed three;
3. then run `dotnet test Veldrath.Server.slnx --filter Category=Validation` —
   `SeededEncounterCoverageTests` is the gate that proves this document:
   `Every_pooled_archetype_exists_and_is_hostile`, `Every_pooled_archetype_fits_its_location_level_band`,
   `Settlements_and_dungeon_lobbies_never_spawn_anything` and `Every_other_location_can_spawn_something`.

Step 3 was run for the trait changes already, and it is what proves amendment 3 and 4: 28 pools, 26 quiet
locations, every hostile location pooled, and exactly one visible `SafeRespawn` node in `ashfields`.

A local `./scripts/e2e-stack.sh start --fresh` is required to see any of it, because the seeders do not
update existing rows.

## 9. What this changes against the seeder before 2026-11-20

The 29 pools the seeder carried before this commit referenced 14 archetypes and were written before the
roster existed. The change per location, before → proposed, with the three rows this commit already moved
marked:

| location | seeder pool before 2026-11-20 | proposed |
|---|---|---|
| `drove-road` | `giant-rat` 60, `goblin-scout` 40 | `giant-rat` 5, `goblin-scout` 3, `drove-cur` 3, `peat-adder` 2 |
| `sunken-fields` | `giant-rat` 50, `forest-wolf` 50 | `peat-adder` 5, `giant-rat` 4, `drove-cur` 2 |
| `wood-road` | `giant-rat` 40, `goblin-scout` 40, `forest-wolf` 20 | `goblin-scout` 6, `forest-wolf` 4 |
| `old-clearing` | `forest-wolf` 70, `goblin-scout` 30 | `forest-wolf` 6, `goblin-scout` 4 |
| `mire-crossing` | `bandit-ruffian` 60, `forest-wolf` 40 | `peat-adder` 4, `mire-boar` 4, `bandit-ruffian` 3, `forest-wolf` 2 |
| `bone-hollow` | `moor-wight` 60, `bandit-ruffian` 40 | `bandit-ruffian` 5, `mire-boar` 3, `moor-wight` 1 |
| `rootbound-cellars` | `bandit-ruffian` 70, `moor-wight` 30 | `bandit-ruffian` 5, `sump-crawler` 5 |
| `collapsed-vault` | `bandit-ruffian` 70, `moor-wight` 30 | `sump-crawler` 6, `bandit-ruffian` 4 |
| `flooded-workings` | `bandit-ruffian` 50, `moor-wight` 50 | `sump-crawler` 6, `bandit-ruffian` 4 |
| `deepest-chamber` | `moor-wight` 60, `bandit-ruffian` 40 | `bandit-ruffian` 5, `sump-crawler` 5 |
| `ashveil-highlands` | `moor-wight` 70, `cairn-haunt` 30 | `moor-wight` 4, `barrow-bear` 4, `moor-tollman` 3, `cairn-haunt` 2 |
| `shifting-waymark` | `cairn-haunt` 70, `moor-wight` 30 | `moor-tollman` 5, `cairn-haunt` 4, `moor-wight` 2 |
| `fenland-crossing` | `fen-lurker` 70, `cairn-haunt` 30 | `fen-lurker` 5, `bog-lantern` 3, `cairn-haunt` 2 |
| `submerged-ruins` | `fen-lurker` 60, `drowned-revenant` 40 | `drowned-revenant` 4, `bog-lantern` 3, `cairn-haunt` 3 |
| `ancestor-vault` | `drowned-revenant` 60, `cairn-haunt` 25, `bandit-captain` 15 | `drowned-revenant` 5, `burrow-worm` 5 |
| `relic-chamber` | `drowned-revenant` 55, `bandit-captain` 45 | `burrow-worm` 6, `drowned-revenant` 4 |
| `wrack-shore` | `gull-rider-brute` 70, `drowned-revenant` 30 | `tide-strider` 4, `drowned-revenant` 3, `gull-rider-skirmisher` 3 |
| `bone-strand` | `gull-rider-brute` 55, `storm-harrower` 45 | `gull-rider-skirmisher` 4, `tide-strider` 3, `gull-rider-brute` 3 |
| `tidal-grotto` | `storm-harrower` 60, `gull-rider-brute` 40 | `drowned-revenant` 4, `tide-strider` 3, `storm-harrower` 3 |
| `gull-rider-camp` | `gull-rider-brute` 80, `storm-harrower` 20 | `gull-rider-brute` 5, `gull-rider-skirmisher` 3, `cliff-breaker` 2 |
| `storm-watch-peak` | `storm-harrower` 100 | `storm-harrower` 6, `cliff-breaker` 4 |
| `flooded-throne-room` | `drowned-revenant` 60, `storm-harrower` 40 | `drowned-revenant` 4, `storm-harrower` 3, `undertow` 3 |
| `tidelocked-vault` | `storm-harrower` 70, `drowned-revenant` 30 | `undertow` 5, `drowned-cantor` 4, `drowned-revenant` 2 |
| `obsidian-grove` | `cinderling` 70, `pyreling` 30 | **none** — `SafeRespawn` since 2026-11-20 (amendment 4); its pool is now the battlefield's |
| `ash-shrine` | `pyreling` 60, `cinderling` 40 | **none** — `SafeRespawn`, keeper-only since 2026-11-20 (amendment 3) |
| `scorched-battlefield` | *(no pool — `SafeRespawn`)* | `cinderling` 5, `ash-jack` 3, `forge-deserter` 3 |
| `vent-fields` | `pyreling` 70, `cinderling` 30 | `pyreling` 5, `cinderling` 3 |
| `pyreling-den` | `pyreling` 100 | `pyreling` 6, `cinderling` 2 |
| `fire-ancients-chamber` | `cinderling` 70, `fire-ancient` 30 | `pyreling` 5, `cinderling` 3 |
| `kaldreks-heart` | `fire-ancient` 100 | `maw-serpent` 6, `pyreling` 2 (amendment 5) |

26 of the 28 proposed pools change composition — two of them by going quiet (`obsidian-grove`,
`ash-shrine`) — and `scorched-battlefield` joins the matrix with the pool the grove used to carry, so the
matrix's own count stays near where it was: 29 pools before, 28 after. The two locations that keep
exactly the same archetypes (`old-clearing`, `vent-fields`) only scale their weights to small relative
integers, so their distribution is unchanged — the resolver only ever compares the ratio. The two boss
entries the seeder carries today are removed: `bandit-captain` from `ancestor-vault`/`relic-chamber` and
`fire-ancient` from `fire-ancients-chamber`/`kaldreks-heart`, on D0's approved instruction. That is a real
behaviour change: until a uniqueness mechanism exists, those two rooms are ordinary rooms, not boss rooms.

**What this commit changed in the live seeder** is only what amendments 3 and 4 require, because the
pools cannot be imported before their archetypes exist (§8): three traits (`obsidian-grove` gains
`SafeRespawn`, `scorched-battlefield` loses it, `ash-shrine` gains it) and three `SpawnPools` entries
(`obsidian-grove` and `ash-shrine` removed; `scorched-battlefield` takes the interim
`cinderling 70 / pyreling 30`). The rest of the table above is the import target, not the current state.

## 10. The five decisions, as taken (2026-11-20)

All five were decided on card `t_af051fc2`. The first four are the amendments at the top of this file; the
fifth is a separate card. Nothing here is still an open question about *this* document.

| # | Question | Decision | Where it landed |
|---|---|---|---|
| 1 | Difficulty curve as drawn | Approved **with a cap**: at most one band-tolerant entry per pool, weight ≤ 2 | Amendments §1; the audit in §2; `bone-hollow` at weight 1 |
| 2 | Zones to soften | **`bone-hollow` only.** `ashveil-highlands` keeps four archetypes from two bands and `storm-watch-peak` keeps its two-entry achievement-gated pool — both are deliberate difficulty, not defects | Amendment §2; both rows unchanged in §3 |
| 3 | Locations left empty | `ash-shrine` **becomes `SafeRespawn`**, keeper-only, no pool; trait and pool land in one commit | Amendment §3; the shrine class in §4; `ZoneLocationsSeeder.cs` |
| 4 | `scorched-battlefield` | **Respawn moves to `obsidian-grove`; the battlefield keeps a pool** | Amendment §4; §4 sanctuaries; the battlefield row in §3 |
| 5 | The 21–30 tail | **Authorise a small tail roster** — 4–6 archetypes, levels 21–30, no bosses | Card `t_98def8d8` (D6); §7 status |

Three things stay open, and none is decided here: the `cinderling` zone-list deviation (§7 item 3) is D6's
to close or D5's to accept; `ash-eater` — the other D6 row above 26 — is pooled nowhere yet, so
`fire-ancients-chamber` still draws `cinderling` (§7.1); and one consequence of amendments 3 and 4 needs a
D5 ruling rather than a decision in this file: `ash-jack` and `forge-deserter` must be seeded to `19–22`
(§6) or their zone has no place for them.

## 11. See also

- `game-context/budget.yaml` — the bands and the spawn/loot semantics these pools sit on (D0).
- `docs/design/content/archetypes.md` — the roster, its bands, and each archetype's zones (D1).
- `plans/content-seeding-pass.md` — the measured gaps; note the stale `sunken-estate` line at :71.
- `docs/design/content/pools/*.yaml` — the machine-readable form of §3 and §4, 18 files, parked in the
  design set until their archetypes exist (§8).
- `Veldrath.Server.Tests/Features/SeededEncounterCoverageTests.cs` — the gate that proves this document.
