# Enemy drop tables — waves A1–A4 (levels 1–30)

**Status: authored 2026-11-20; review rounds 1 and 2 corrections applied the same day.** Produced by card
`t_744220e4` (F4), after D0 (`game-context/budget.yaml`, approved 2026-11-20), D1 (`archetypes.md`),
D6 (the 21–30 tail) and D4 (`pool-matrix.md`). It answers one question — **what does a kill pay
out** — for every hostile archetype that can spawn between levels 1 and 30.

Before this file the answer was "the four archetypes the seeder happens to wire, and nothing else":
`ArchetypeSeeder.LootTableByArchetype` names a table for `goblin-scout`, `forest-wolf`,
`bandit-captain` and `fire-ancient`. The other 30 hostile archetypes in this set dropped nothing at
all — the 10 the seeder already contains without binding them, and the 20 roster rows it has not
seeded yet. A player could win a fight and receive no reward.

The tables are importable packs under `content/loottables/`, one file per budget band. This
document is the design record: the rules the tables were authored to, the per-band summary, and the
archetype→table binding the seeding card needs.

## 1. What is authored

| File | Band | Tables | Rows | P(at least one row) | Item EV per kill | Band gold |
|---|---|---|---|---|---|---|
| `content/loottables/b1-3.yaml` | b1-3 | 4 | 11 | 0.71–0.75 | 7.2–15.0 | [1, 15] |
| `content/loottables/b4-6.yaml` | b4-6 | 4 | 11 | 0.71–0.75 | 13.8–25.1 | [5, 20] |
| `content/loottables/b7-10.yaml` | b7-10 | 7 | 21 | 0.71–0.72 | 16.2–34.5 | [8, 30] |
| `content/loottables/b11-14.yaml` | b11-14 | 3 | 12 | 0.69 | 19.5–58.8 | [12, 45] |
| `content/loottables/b15-18.yaml` | b15-18 | 4 | 16 | 0.69 | 47.8–97.5 | [20, 55] |
| `content/loottables/b19-22.yaml` | b19-22 | 4 | 17 | 0.68–0.69 | 19.0–45.5 | [25, 60] |
| `content/loottables/b23-26.yaml` | b23-26 | 5 | 25 | 0.68 | 44.8–66.0 | [30, 75] |
| `content/loottables/b27-30.yaml` | b27-30 | 1 | 5 | 0.67 | 67.5 | [35, 90] |
| **total** | | **32** | **118** | | | |

Three clarifications the table invites:

- **P(at least one row)** is `1 − Π(1 − p)` over the table's rows, and it is consistently above the
  band's `drop_chance` ceiling. That is deliberate and it is what the band says: `drop_chance` is
  the probability **one** row lands (`budget.yaml` `units.drop_chance`), rows roll independently,
  and the seeded `goblin-common-drops` (60/40 → 0.76 on one kill) has always behaved this way. A win
  that pays out roughly seven times in ten is the answer to the hole this card exists to close.
- **Item EV** is the expected *printed* value of the items a kill drops, summed over the table
  (quantity averaged over `[min, max]`). It is not income: buyback pays 40–50 % of printed value
  (`budget.yaml` `merchants.buyback_rate`). The richest table by a wide margin is
  `storm-harrower-drops` (97.5), the only one in the set carrying two 150-gold gems; behind it come
  `maw-serpent-drops` (67.5), `ash-picker-drops` (66.0), `gull-rider-brute-drops` (60.0),
  `ash-eater-drops` (59.2) and `drowned-revenant-drops` (58.8), whose top item is an 80-gold
  iron-chestplate because b11-14's ceiling admits no 150-gold item (§2.4). Nothing exceeds the item
  ceiling in §2.4.
- **`content/loottables/`, not `content/loot/`.** The card names `content/loot/<band>.yaml`; the
  importer takes the directory name as the family and `loot` resolves to no content type. Run
  against a scratch root holding `loot/b1-3.yaml`, A0's CLI says:
  `Error pack-family-unknown: '…/loot/b1-3.yaml' belongs to family 'loot', which is not a content
  type the engine stores.` `loottables` folds onto the `loottable` family. Nothing else changed.

## 2. The rules the tables were authored to

### 2.1 The band is the band containing the archetype's roster floor

A roster band of 1–3 sits in `b1-3`; 4–6 in `b4-6`; 7–9 in `b7-10`; 10–12 in `b7-10` (level 10 is
b7-10's last level); 13–15 in `b11-14`; 16–18 in `b15-18`; 19–20 in `b19-22`; 23–26 in `b23-26`;
26–30 in `b23-26`; 28–30 in `b27-30`. That single rule places all 32 tables, and it is why
`cairn-haunt`, `fen-lurker`, `burrow-worm` and `bog-lantern` (roster 10–12) are in `b7-10.yaml`
rather than a file of their own.

It is the archetype's band, not the location's, because stat lines and drop chance are per
archetype: `drowned-cantor` is banded 19–20 and drops at `tidelocked-vault` (L17–20), and its 19
floor puts it in b19-22.

A roster band that crosses a budget band is placed by its floor, and the floor is the level it
actually spawns at: `EfCoreEnemyRepository.cs:113` sets `Level = e.MinLevel`, so `ash-eater` (26–30)
is always level 26 and only b23-26's `drop_chance` window applies to it, and `pyreling` (23–28) is
always level 23 and sits in b23-26 for the same reason. The bands above their floor are never
reached by these two, so their tables are not shaped around a ceiling they cannot spawn under.

### 2.2 Every table sums to 100, so a weight is a percentage

`loot.weight_semantics`: each row rolls independently against the table total. Summing each table
to 100 makes `DropWeight` read directly as that row's landing probability — the convention the
seeded common tables already use — and makes the `drop_chance` comparison auditable by eye.

### 2.3 No row exceeds the band's `drop_chance` ceiling, and none falls under its floor

Every non-guaranteed row lands with `p` inside the band's `[min, max]`: 0.30–0.60 at b1-3 down to
0.06–0.25 at b27-30. This is the tight reading — the band bounds each row, not the table's best
row — and it is what "do not exceed the drop_chance band for the tier" asks for.

The consequence is worth stating plainly: **the window is narrow, so rarity is expressed by where
a row sits inside it, not by dropping a row to 2 %.** A b1-3 table has three rows at 0.40 / 0.30 /
0.30; a b23-26 table has five at 0.30 / 0.20 / 0.20 / 0.15 / 0.15. The taper across the ladder is
the band's own taper, and it is real: the same item that lands four times in ten at level 2 lands
once in seven at level 28.

### 2.4 No row out-values three kills

A row's item is capped at 3 × the band's gold maximum — 45 at b1-3, 90 at b7-10, 180 at b19-22,
270 at b27-30. Without a bound, a level-3 rat could drop a 400-gold orb. With it, the palette
widens band by band and does so predictably:

| Band | Item ceiling | What first becomes droppable |
|---|---|---|
| b1-3 | 45 | glowcap, ingots, potions, antidote, leather cap, the two 25–35 gold weapons |
| b4-6 | 60 | scroll-of-fireball (60), essence-of-shadow (50) |
| b7-10 | 90 | rune-of-power, iron-chestplate, elixir-of-strength (80 each) |
| b11-14 | 135 | nothing new — the catalogue has no item between 80 and 150 |
| b15-18 | 165 | storm-topaz, void-shard (150 each) |
| b19-22 | 180 | nothing new |
| b23-26 | 225 | fire-ruby, frost-sapphire (200 each) |
| b27-30 | 270 | nothing new — orbs are 300 and 400 and stay out |

### 2.5 Within a table, rows are ordered by `RarityWeight` and weights never rise against it

This is the literal answer to "common drops common and rare drops rare": `ItemsSeeder` already
declares how common each item is — health-potion, mana-potion and glowcap-mushroom 90, antidote and
leather-cap 85, iron-ingot and iron-sword 80, hunter's bow 75, iron-chestplate and essence-of-fire
70, rune-of-fire, rune-of-frost and essence-of-nature 60, soul-crystal 50, essence-of-shadow 45,
elixir-of-strength and scroll-of-fireball 40, rune-of-power 35, storm-topaz 30, fire-ruby and
frost-sapphire 25, void-shard and orb-of-force 20, orb-of-decay 15, lost-cargo 1.

Every table writes its rows in descending `RarityWeight`, and `DropWeight` never rises along that
order — ties are allowed, and there are several — so the commonest item in a table's palette always
carries the largest weight and no rarer item out-weighs a commoner one. The narrative decides which
items are in a palette; this rule decides their order and their relative weight. It is checked
mechanically over all 32 tables by the same audit that produced §1, not asserted by eye.

### 2.6 No guaranteed row

`loot.guaranteed_rows`: a guaranteed row skips the roll but still counts in the denominator, so it
dilutes every other row. Nothing in the design says a kill always yields something. The seed's five
guaranteed rows sit on the two boss tables (`bandit-boss-drops` — iron-sword and lost-cargo;
`fire-ancient-drops` — fire-ruby), on a chest (`forest-chest-loot`) and on a resource node
(`forest-forage`); neither common enemy table has one. So **no table in this set has one**: 118 of 118
rows roll.

### 2.7 No boss table

Card rule, and D0's: bosses stay out of pools until a uniqueness mechanism exists. `bandit-captain`
and `fire-ancient` keep their seeded tables (`bandit-boss-drops`, `fire-ancient-drops`) and are not
re-authored here.

## 3. The archetype → table binding

The pack names the table; the archetype names the pack. `ActorArchetype.LootTableId` is a GUID and
`tables_per_archetype` is exactly one, so each archetype resolves one slug:

| Band | Archetype | Table slug |
|---|---|---|
| b1-3 | `giant-rat` | `giant-rat-drops` |
| b1-3 | `goblin-scout` | `goblin-common-drops` *(seeded slug, kept)* |
| b1-3 | `drove-cur` | `drove-cur-drops` |
| b1-3 | `peat-adder` | `peat-adder-drops` |
| b4-6 | `forest-wolf` | `wolf-common-drops` *(seeded slug, kept)* |
| b4-6 | `bandit-ruffian` | `bandit-ruffian-drops` |
| b4-6 | `sump-crawler` | `sump-crawler-drops` |
| b4-6 | `mire-boar` | `mire-boar-drops` |
| b7-10 | `moor-wight` | `moor-wight-drops` |
| b7-10 | `barrow-bear` | `barrow-bear-drops` |
| b7-10 | `moor-tollman` | `moor-tollman-drops` |
| b7-10 | `cairn-haunt` | `cairn-haunt-drops` |
| b7-10 | `fen-lurker` | `fen-lurker-drops` |
| b7-10 | `burrow-worm` | `burrow-worm-drops` |
| b7-10 | `bog-lantern` | `bog-lantern-drops` |
| b11-14 | `drowned-revenant` | `drowned-revenant-drops` |
| b11-14 | `tide-strider` | `tide-strider-drops` |
| b11-14 | `gull-rider-skirmisher` | `gull-rider-skirmisher-drops` |
| b15-18 | `gull-rider-brute` | `gull-rider-brute-drops` |
| b15-18 | `storm-harrower` | `storm-harrower-drops` |
| b15-18 | `cliff-breaker` | `cliff-breaker-drops` |
| b15-18 | `undertow` | `undertow-drops` |
| b19-22 | `cinderling` | `cinderling-drops` |
| b19-22 | `drowned-cantor` | `drowned-cantor-drops` |
| b19-22 | `ash-jack` | `ash-jack-drops` |
| b19-22 | `forge-deserter` | `forge-deserter-drops` |
| b23-26 | `pyreling` | `pyreling-drops` |
| b23-26 | `vent-stalker` | `vent-stalker-drops` |
| b23-26 | `crust-crawler` | `crust-crawler-drops` |
| b23-26 | `ash-picker` | `ash-picker-drops` |
| b23-26 | `ash-eater` | `ash-eater-drops` |
| b27-30 | `maw-serpent` | `maw-serpent-drops` |

**Not authored, by design:** `bandit-captain` (boss, keeps `bandit-boss-drops`) and `fire-ancient`
(boss, keeps `fire-ancient-drops`). They are the only two hostile archetypes without a table in
this set, and both already had one.

**The binding is wired, in two runs.** `LootTableByArchetype` in `RealmEngine.Data/Seeders/ArchetypeSeeder.cs` now
names all 32 rows above plus the two boss tables they leave out (a boss with no table fails the seed guard), and the
20 roster rows this set had not been seeded with exist. A seeder is insert-only and its own catalogue holds only the
four seeded tables, so it binds what it can find and leaves the rest null: the binding that reaches every archetype —
including the 10 that were seeded before these tables existed — is the archetype pack, `content/archetypes/`, which
names the same slug in `lootTableId`. The order is: import `content/loottables/`, then `content/archetypes/`
(`docs/content-packs.md`, *Two runs, in dependency order*). Pools remain the next card's job.

## 4. Two seeded tables are re-authored here

`goblin-common-drops` and `wolf-common-drops` keep their slugs — a slug is permanent identity — and
are written in the pack so the pack, not the insert-only seeder, is the authoring surface for them.
Both keep exactly the two items the seeder gave them:

| Table | Seeded weights | Authored weights | Why |
|---|---|---|---|
| `goblin-common-drops` | health-potion 60 / iron-ingot 40 | 55 / 45 | 0.60 sits on the b1-3 ceiling; the band's taper needs room below it |
| `wolf-common-drops` | essence-of-nature 70 / health-potion 30 | health-potion 55 / essence-of-nature 45 | **0.70 breaches the b4-6 ceiling of 0.55.** The seeded table breaks its own band |

The wolf is a real defect the seeding never noticed, and it is worth naming: a b4-6 enemy dropped
its item on seven kills in ten against a published ceiling of 0.55.

Three things change on first import. They are written down here because the import is an upsert on
slug while the seeder is insert-only, so nothing else records them:

- **`goblin-common-drops` 60/40 → 55/45.** The same two items stay in the same order: health-potion
  is still the common drop.
- **`wolf-common-drops` 70/30 → 55/45, and the common drop flips.** The seeder carries
  essence-of-nature 70 / health-potion 30; the pack carries health-potion 55 / essence-of-nature 45,
  because health-potion is the commoner item in the catalogue (`RarityWeight` 90 against 60).
- **`wolf-common-drops` `RarityWeight` 60 → 80.** The pack also writes the table's own rarity, and it
  takes the value `goblin-common-drops` already carried.

`LootTableByArchetype` already binds `goblin-scout` and `forest-wolf`, and those are the only two
non-boss archetypes bound to a table today — `bandit-captain` and `fire-ancient` keep their seeded
boss tables (§2.7), so the map names four slugs in all, but only these two are re-authored here. So
**these two payouts change in live play the moment the pack is imported**, before any of the rest of
the binding exists. Every other table in this set pays out nothing until the archetype-seeding card
lands (§3).

## 5. Open items

| # | Item | Owner |
|---|---|---|
| 1 | **`b27-30` is published** — C4 (`a857dcf0`) merged to development after this set was authored and carries exactly the band the pack assumed: levels [27, 30], gold [35, 90], `drop_chance` [0.06, 0.25]. `maw-serpent-drops`' rows land at 0.15–0.25, inside that window, so no re-weight is needed. Closed. | closed |
| 2 | **The catalogue has nothing between 200 and 300 gold,** so b27-30's palette is identical to b23-26's and the two orbs never drop from an enemy at any band. The tail of the ladder is not visibly richer than its middle. Wave C owns the item catalogue. | Wave C |
| 3 | **The catalogue has no cheap themed material** — no bone, hide, feather, salt, ash or scrap below 5 gold. Low-band tables therefore converge on iron-ingot, glowcap-mushroom and essence-of-nature: **6 of the 15 tables at b1-10 carry two or more of those three**. Flavour at the bottom of the ladder is limited by the catalogue, not by the weights. | Wave C |
| 4 | **The binding is wired** (§3). Closed by card `t_107630df`: the 20 unseeded roster rows exist, `LootTableByArchetype` names 34 archetypes, and `content/archetypes/` binds all 32 rows above on an already-seeded catalogue. | closed |
