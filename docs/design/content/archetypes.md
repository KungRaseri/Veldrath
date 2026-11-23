# Veldrath — archetype roster, levels 1–30

The cast of the world, not their stat blocks. Every row names a zone it belongs to and a faction
tie, because an archetype with no place is a defect.

**Status: proposal.** This is the roster every later archetype and spawn-pool card draws from. It is
canon-adjacent design, not locked canon — the numbered proposals at the end are separate from the
table and must be decided by a human before the content that would depend on them is written.

## Status

**Approved 2026-11-20.** This section supersedes the `Status: proposal` note above. The roster, its
bands and its slugs are unchanged from what was approved.

- **Proposals 3–5 — accepted.** The Pits has no combat form; Vraen is off the level ladder; there is
  no Aethori archetype at levels 1–20.
- **Proposals 6–8 — declined.** No gull-rider lore entry, no geography for the seeded organisations,
  no Ash Shrine dedication.
- **The `gull-rider clans` faction tie is descriptive only.** It rests on the map seeder's assertion
  of rival clans, not on canon — do not treat it as an established organisation.
- **Proposal 9 — decided 2026-11-20.** The content standard is published in `game-context/GAME.md`
  §3 (Teen-equivalent and self-imposed, with the permitted, testimony-only and not-permitted lists)
  and the four design pillars with their fails-when clauses in §4. A borderline description is
  resolved against §3 and a design argument against §4; before this they were questions with no
  answer anywhere in the repository. The roster above was approved without them and nothing in it
  changed when they landed.
- **The townspeople are absorbed from the towns card (`towns.md`).** The ten `§` rows at the end of the
  table are D3's, adopted here because this document is the slug authority; D3 does not keep a parallel
  list. Non-hostile total goes from seven to seventeen.
- **P1–P4 approved 2026-11-20.** The four town canon proposals (`towns.md` §5) are locked: Crestfall
  stands at the *Korreldh* crossing; Aldenmere was reoccupied on Calethic *Aldeld*; Tolvaren's name has
  no surviving derivation and *Gaelvren* is its survey form; Skarhold was founded after the Collapse by
  smiths. Recorded in `world-lore-plan.md` under each region's settlements. P1 deliberately leaves open
  item 7 unanswered, and it stays open.
- **`aurelian-market` — the slug stays, the DisplayName changes.** A slug is permanent identity, so the
  zone keeps `aurelian-market`; its display name becomes The Korreld Exchange. The slug remains recorded
  as debt.
- **The retired-lore pass (`C2` in `towns.md`) landed 2026-11-20.** The seeded world text no longer
  carries Dravan, Thysmara, Kaldrek or the Age of Embers — `World.Era` is "The Ashen Age (the Age of
  Reclaiming)" — and `SeededWorldTextTests` fails the test suite if one comes back. The town registers
  below stay clear of them.
- **The merchant band landed 2026-11-20.** `budget.yaml`, *merchants* bands shop trade by the town's
  own level, not the player's — `gold_by_town_level` at L0/L5/L10/L18, `stock_size`, `price_markup`
  and `buyback_rate`. Shopkeeper prices and purses are authored there; *merchants.wiring* records
  which of them the live shop reads. A depleting purse — as opposed to a per-transaction cap — is
  still engine work.
- **Per-zone shared shop stock is accepted for this wave.** Two merchants in one town sell from one
  catalogue; per-NPC stock is an engine change and is not in this wave.

## How to read this table

- **`†` after a name** — already seeded in `RealmEngine.Data/Seeders/ArchetypeSeeder.cs`. Carried
  over unchanged, including its slug. A slug is permanent identity: none are renamed here.
- **`§` after a name** — non-hostile. NPCs exist so trade, quest and dialogue content has an actor
  to hang off. **They never appear in a spawn pool**: a pool entry becomes an attackable enemy
  (`ZoneLocationsSeeder.cs`, *SpawnPools* remarks).
- **Level band** — a band of at most three levels, and the only number a row carries. HP, damage, AC,
  XP, gold and drop weights come from `game-context/budget.yaml`, which now publishes the ladder as
  `b1-3` through `b27-30` — eight bands covering 1–30, the last added by C4 with the tail. **The 1–20 table
  below cites no band id**, because nothing was published when it was written; the tail section does
  cite band ids from `budget.yaml`. For the 1–20 rows the level range remains the only placement
  claim made here.
- **Placement check** — every band was checked against each location's seeded `MinLevel`/`MaxLevel`
  and every zone/archetype pair was checked against the seeded spawn pool for that location. Those
  seeded values are deliberately not reproduced in this document.
- **Zones** are zone slugs from `ZoneLocationsSeeder.cs` (19 zones, all verified). Where a zone
  contains no seeded spawn pool (towns, inns, markets, safe-respawn lobbies), an NPC rather than a
  hostile archetype carries the row.
- **Faction ties** use organisations that already exist in the seeders (`OrganizationsSeeder.cs`) or
  a group the seeder prose already asserts. `none recorded` means the tie is explicitly "no
  organisation" — it is a tie, not an omission. A new faction needs a numbered canon proposal.

**Only two rows tie to a real faction, and that is a finding, not an oversight.** The repository
seeds exactly two organisations — the Iron Anvil Guild and the Shadowhand Syndicate — and neither has
a geography in any lore file: no region, no zone, no city. Placing their members anywhere is a new
claim, so the guild- and syndicate-tied archetypes were held out and named in proposals 6–7. The
gull-rider clans are the one faction a row can tie to, because the map seeder already asserts them.
The faction picture fills in with the regional design, not with more archetypes.

## The roster

Two populations share one table. The **ladder** is the hostile cast, banded by level. The **townspeople**
are the `§` rows that hold the settlements at the foot of it: they carry no band, no pool entry and no
faction, and they exist so trade, dialogue and quest content has a person to attach to.

| slug | name | one-line identity | level band | faction tie | zones it belongs to | hostile? | combat role |
|---|---|---|---|---|---|---|---|
| `giant-rat` | Giant Rat † | Eats the spilled grain on the drove road and has stopped running from people. | 1–3 | none recorded (settlement refuse) | `the-droveway` | yes | swarm melee |
| `goblin-scout` | Goblin Scout † | A pack fighter that shadows carts on the wood road and reports back to somewhere. | 1–3 | none recorded (goblin packs) | `the-droveway`, `ashlen-wood` | yes | pack skirmisher |
| `drove-cur` | Drove Cur | Feral dog of the dry-stone walls; takes the weakest animal out of a passing drove. | 1–3 | none recorded (feral) | `the-droveway` | yes | pack melee, harries |
| `peat-adder` | Peat Adder | Coils in the standing water of the flooded pastures and strikes at anything that breaks the surface. | 1–3 | none recorded (fauna) | `the-droveway`, `grevenmire` | yes | ambush striker (venom) |
| `forest-wolf` | Forest Wolf † | Hunts the wood road in threes and knows where the clearings end. | 4–6 | none recorded (fauna) | `ashlen-wood`, `the-droveway`, `grevenmire` | yes | pack melee |
| `bandit-ruffian` | Bandit Ruffian † | Holds the crossings on the road out of Grevenmire and takes a cut of what passes. | 4–6 | unaffiliated banditry (no organisation) | `grevenmire`, `the-halrow`, `drowning-pits` | yes | melee, disengages |
| `sump-crawler` | Sump Crawler | Grew up in the flooded workings and cannot leave them; pulls at legs from below the waterline. | 4–6 | none recorded (fauna) | `drowning-pits`, `the-halrow` | yes | ambush melee |
| `mire-boar` | Mire Boar | Sows in the bog grass and charges the smell of smoke and metal. | 4–6 | none recorded (fauna) | `grevenmire` | yes | charger, bruiser |
| `bandit-captain` | Bandit Captain † | The one with a name, the ledgers, and the men who owe him. | 7–9 | unaffiliated banditry (no organisation) | `pale-moor`, `barrow-deeps` | yes | boss, melee |
| `moor-wight` | Moor Wight † | Walks the moor with a cold that spoils a fire, remembering a duty it cannot finish. | 7–9 | none recorded (the unquiet dead) | `grevenmire`, `pale-moor` | yes | caster, ranged |
| `barrow-bear` | Barrow Bear | Dens in the burial mounds and digs them open for the salt in the bones. | 7–9 | none recorded (fauna) | `pale-moor` | yes | bruiser |
| `moor-tollman` | Moor Tollman | Collects a toll at a gate nobody built, and shoots the people who ask who set it. | 7–9 | unaffiliated banditry (no organisation) | `pale-moor` | yes | ranged skirmisher |
| `cairn-haunt` | Cairn Haunt † | Stands on a cairn at dusk and does not move while you are looking at it. | 10–12 | none recorded (the unquiet dead) | `pale-moor`, `soddenfen` | yes | caster |
| `fen-lurker` | Fen Lurker † | Sits under the duckweed of the fen crossing and waits for a horse to drink. | 10–12 | none recorded (fauna) | `soddenfen` | yes | ambush melee (venom) |
| `burrow-worm` | Burrow Worm | Tunnels the old burial network and surfaces in the middle of a camp. | 10–12 | none recorded (fauna) | `barrow-deeps` | yes | burrowing melee |
| `bog-lantern` | Bog Lantern | A column of cold light over a sinkhole; it moves along the path with you. | 10–12 | none recorded | `soddenfen` | yes | controller, ranged |
| `drowned-revenant` | Drowned Revenant † | Came back up from the flooded rooms still carrying what drowned it. | 13–15 | none recorded (the drowned dead) | `soddenfen`, `barrow-deeps`, `tidewrack-flats`, `sunken-name` | yes | melee |
| `tide-strider` | Tide Strider | Wades the flats on long legs and spears what the ebb uncovers. | 13–15 | none recorded (fauna) | `tidewrack-flats` | yes | melee, reach |
| `gull-rider-skirmisher` | Gull-Rider Skirmisher | Rides the updraught off the cliff and throws a harpoon on the way past. | 13–15 | gull-rider clans | `saltcliff-heights`, `tidewrack-flats` | yes | ranged skirmisher |
| `gull-rider-brute` | Gull-Rider Brute † | The heavy member of the clan, brought out when harpoons are not enough. | 16–18 | gull-rider clans | `tidewrack-flats`, `saltcliff-heights` | yes | bruiser, ranged |
| `storm-harrower` | Storm Harrower † | A shape in the rain at Storm-Watch; the wind turns with it. | 16–18 | none recorded (storm-bound spirit) | `tidewrack-flats`, `saltcliff-heights`, `sunken-name` | yes | caster, ranged, elite |
| `cliff-breaker` | Cliff Breaker | Nests high on the clifftop and drops stone on anything walking the path beneath. | 16–18 | none recorded (fauna) | `saltcliff-heights` | yes | artillery |
| `undertow` | Undertow | A current in a drowned room that pulls the wrong way, toward the dry ground. | 16–18 | none recorded | `sunken-name` | yes | controller |
| `cinderling` | Cinderling † | A knot of cinder and heat that leaves a trail of fused ash behind it. | 19–20 | none recorded (Cinderplain elemental) | `ashfields` | yes | melee |
| `drowned-cantor` | Drowned Cantor | Sings in the drowned hall; the water carries the note further than the hall is long. | 19–20 | none recorded (the drowned dead) | `sunken-name` | yes | caster |
| `ash-jack` | Ash Jack | A crow of the ash plains that knows the sound of a dying animal and calls the rest. | 19–20 | none recorded (fauna) | `ashfields` | yes | pack skirmisher |
| `forge-deserter` | Forge Deserter | Took a master's tools out of the Forge Quarter and now makes for whoever pays and does not ask. | 19–20 | none recorded (Skarhold forge trade) | `ashfields` | yes | armoured melee |
| `village-blacksmith` | Village Blacksmith † § | Works the square at Crestfall and will not discuss the cellars. | — | none recorded (village trade) | `crestfall` | no | shop, trade |
| `tavern-keeper` | Tavern Keeper † § | Keeps the Hearthbound Inn and hears everything said at the bar. | — | none recorded (Crestfall) | `crestfall`, `aldenmere`, `tolvaren` | no | shop, quest hook |
| `wayfarer-elder` | Wayfarer Elder † § | Sits by the Weathered Waypost and can still read the old names cut into it. | — | none recorded | `the-droveway` | no | quest hook |
| `market-factor` | Market Factor § | Buys in one market and sells in the next, and keeps a written price list he honours. | — | none recorded (independent trader) | `crestfall`, `aldenmere`, `tolvaren`, `skarhold` | no | shop |
| `gate-watch` | Gate Watch § | Stands the north road at Crestfall and checks every cart for a toll mark. | — | none recorded (town watch) | `crestfall`, `aldenmere` | no | guard |
| `shrine-keeper` | Shrine Keeper § | Tends the Ash Shrine and will not say who is owed the ash. | — | none recorded (dedication undecided — see the proposals) | `ashfields` | no | quest hook |
| `dock-hand` | Dock Hand § | Loads and unloads at Tolvaren Harbour and knows which crates never get weighed. | — | none recorded | `tolvaren` | no | labourer, rumour |
| `road-carter` | Road Carter § | Hauls for hire out of the low quarter and will not carry a sealed load. | — | none recorded | `crestfall` | no | labourer, transport |
| `keep-castellan` | Keep Castellan § | Hired captain of Ironhollow Keep; sells the safe crossings of the moor, never the keep's lower floors. | — | none recorded | `aldenmere` | no | guard, teaches |
| `mead-seller` | Mead Seller § | Sells Aldenmere mead by the jug and wants the barrels gone before they turn. | — | none recorded | `aldenmere` | no | shop, trade |
| `cellar-keeper` | Cellar Keeper § | Keeps the Grey Cup's front room full and its deep cellar shut. | — | none recorded | `aldenmere` | no | shop, trade |
| `harbour-master` | Harbour Master § | Counts every hull at the turn of the tide and berths none whose papers carry no name. | — | none recorded | `tolvaren` | no | guard, teaches |
| `ship-chandler` | Ship Chandler § | Rope, pitch and lamp oil on the cliff road, and no line to a crew that will not say where it is bound. | — | none recorded | `tolvaren` | no | shop, trade |
| `port-innkeeper` | Port Innkeeper § | Keeps the Saltcrow and does not answer the harbour bell. | — | none recorded | `tolvaren` | no | shop, trade |
| `forge-master` | Forge Master § | Wants one order worth firing the big furnace for, and refuses work that must be hurried. | — | none recorded | `skarhold` | no | shop, trade, teaches |
| `ash-glass-seller` | Ash Glass Seller § | Trades ash-glass, crucibles and flux, and sells to nobody who calls the caldera dormant. | — | none recorded | `skarhold` | no | shop, trade |
| `lodge-keeper` | Lodge Keeper § | Runs the Ashbrand Lodge: bunk, board, and every brand entered in the book. | — | none recorded | `skarhold` | no | shop, trade |

*`—` in the level-band column marks a non-hostile archetype: it has no combat band, and the level
tier of the zone it stands in governs where it appears. Zone slugs are the only zone-level reference
used anywhere in this table.*

## Band coverage

Hostile archetypes only. The floor is three per band; the target was to leave margin so a later pool
card has a choice.

| band | hostile archetypes | count |
|---|---|---|
| 1–3 | giant-rat, goblin-scout, drove-cur, peat-adder | 4 |
| 4–6 | forest-wolf, bandit-ruffian, sump-crawler, mire-boar | 4 |
| 7–9 | bandit-captain, moor-wight, barrow-bear, moor-tollman | 4 |
| 10–12 | cairn-haunt, fen-lurker, burrow-worm, bog-lantern | 4 |
| 13–15 | drowned-revenant, tide-strider, gull-rider-skirmisher | 3 |
| 16–18 | gull-rider-brute, storm-harrower, cliff-breaker, undertow | 4 |
| 19–20 | cinderling, drowned-cantor, ash-jack, forge-deserter | 4 |
| **total hostile** | | **27** |
| **total non-hostile** | | **17** |

**Levels 1–20 are covered with no gap.** Level 19 sits on Saltcliff's top ground only: six locations
run to level 20 — `clifftop-ruins`, `gull-rider-camp`, `storm-watch-peak` (saltcliff-heights) and
`drowned-threshold`, `flooded-throne-room`, `tidelocked-vault` (sunken-name) — and `drowned-cantor`
is the one archetype banded at 19. Cinderplain opens at 20, which is why `ash-jack` and
`forge-deserter` are banded 19–20 rather than 20+: they are the two archetypes that carry a player
across that threshold.

### Carried-over archetypes whose seeded range is wider than their roster band

A carried-over archetype keeps its slug and its seeded `MinLevel`/`MaxLevel`; the band in this table
is the roster's **intended** placement. For seven of the twelve carried-over hostiles the seeded range
is wider than one 3-level band, and tightening it (or moving the pool) is a job for the pool card, not
this one:

| archetype | seeded range | roster band |
|---|---|---|
| `bandit-captain` | 6–12 | 7–9 |
| `moor-wight` | 6–10 | 7–9 |
| `cairn-haunt` | 8–14 | 10–12 |
| `fen-lurker` | 9–14 | 10–12 |
| `drowned-revenant` | 11–18 | 13–15 |
| `gull-rider-brute` | 13–18 | 16–18 |
| `storm-harrower` | 15–20 | 16–18 |

Two consequences worth stating rather than discovering later:

- **`cinderling` cannot appear at level 19.** Its seeded range starts at 20, so the level-19 slot is
  carried by `drowned-cantor` alone. Band 19–20 still has four archetypes; only one of them reaches
  level 19.
- **`bandit-captain` is seeded only in `barrow-deeps` (levels 11–14)** while his own range starts at
  level 6, so today he is a level-11 boss wearing a level-6 statline. The roster places him on the
  `pale-moor` road at 7–9 as well, which is where a boss for the moor's tollmen belongs; whoever
  writes the pool card should decide whether the barrow-deeps placement stays.

### What this roster does not cover, and why

| Gap | Reason |
|---|---|
| Levels 21–30 | Answered by the tail section below (five hostile rows, approved 2026-11-20). The band table reaches 30 with `b27-30` (C4); what remains outside this document is statting the two rows above 26, their loot tables and their seeding. |
| The Wound (Region 5) | No zones, no locations, no map graph exist. Nothing can be placed. See `game-context/GAME.md` §8. |
| `deepest-chamber`, `collapsed-vault` | Both are quest-gated Varenmark dungeons whose pools are existing bandit/wight content. What lives at the bottom is open lore item 4 — see proposal 3. |
| A Shadowhand Syndicate archetype | Reserved. The organisation is seeded (`OrganizationsSeeder.cs`) but has no geography in any lore file, so placing its members anywhere is a new claim. Held for proposal 7. |
| An Iron Anvil Guild archetype | Reserved, same reason as the syndicate: the guild has shops and members in the seeder but no city. A guild-tied hostile (an enforcer, a bailiff, a deserter) needs the guild placed first. Held for proposal 7. |
| A named god's priest | Canon contains no pantheon. `shrine-keeper` exists, refuses to name the object of veneration, and does not assert one. See proposal 8. |

## Naming — derivation notes

**No new Calethic coinage was needed for this roster, and none was made.** The Caleth named places,
offices and institutions, not beasts; `calethic-language.md` §2.5 reserves personal-name formation
for function + affiliation, and §6 says to coin only when needed. Naming a rat in Calethic would be
a false-register error. Every coined name below is therefore a **Common coinage**, and the derivation
given is its Common morphology plus the Calethic substrate of the ground it lives on — the
information a quest or dialogue writer needs to place the word locally.

Precedent for Common coinage of creatures: `giant-rat`, `forest-wolf`, `bandit-ruffian`,
`fen-lurker`, `gull-rider-brute` — every seeded archetype name is Common.

| name | derivation |
|---|---|
| Drove Cur | Common: *drove* (the cattle march that gives the Droveway its name — `calethic-language.md` §5.2: "The word for 'drove' (livestock march) is Common; locals renamed the route for its most visible daily function") + *cur*, a working dog. No Calethic form. |
| Peat Adder | Common: *peat* + *adder*. Lives on `sunken-fields` and `mire-crossing`; the Calethic word for standing water is *meld*, which survives only as a survey descriptor (§4.5) — locals do not know it, so they say *peat*. |
| Sump Crawler | Common: *sump* (the flooded lower workings) + *crawler*. Ground is the Drowning Pits, Calethic *Dornkeld* — "dorn– (deep) + keld (hollow)" (§5.2), wholly replaced in Common. Deliberately **not** named after *dorn*: a Calethic-derived name here would imply the Caleth left it, which is open item 4. |
| Mire Boar | Common: *mire* is the Common word locals appended to *Grevel* to make Grevenmire (§5.2) — the same appended word, applied to an animal that lives in it. |
| Barrow Bear | Common: *barrow* + *bear*. Ground is `pale-moor`; no Calethic name exists for the region (working name, Q2). |
| Moor Tollman | Common: *moor* + *tollman*. Same zone, same reason. |
| Burrow Worm | Common. Ground is `barrow-deeps`, which has no Calethic name in canon; naming it *Korn-keld* would be wrong — that is Bone Hollow, in Grevenmire. |
| Bog Lantern | Common: *bog* + *lantern*, from the behaviour, not from a light-root. Calethic distinguishes *raek* (light in material, §4.5) from *thael* (metaphysical revelation); this thing is neither, and giving it a Calethic light-name would over-grade it. |
| Tide Strider | Common: *tide* + *strider*. Ground is `tidewrack-flats`. Working name region (Q2). |
| Gull-Rider Skirmisher | Common + the clan name already in canon: *gull-rider* appears in `MapGraphSeeder.cs` ("contested by rival gull-rider clans") and in the seeded `gull-rider-brute`. The archetype extends an existing name; it does not coin one. |
| Cliff Breaker | Common: *cliff* + *breaker*. Ground is `saltcliff-heights`; `Saltcliff` carries the place-holder Calethic sketch *Gaeldrak* (*gael* water + *drak* stone, §5.2), which describes the coast and not the animal. |
| Undertow | Common, a maritime word, from the behaviour: the pull runs *against* the water it should obey. |
| Ash Jack | Common: *ash* + *jack* (jackdaw). Ground is `ashfields`, Calethic *Daeld-Ald* ("ash-plain", §5.2) — the one region whose Calethic name is a plain translation of the Common one, so the Common animal name is the local name. |
| Forge Deserter | Common, from the forge trade of Skarhold's Forge Quarter rather than from place. |
| Drowned Cantor | Common: *cantor* (one who leads singing). Deliberately **not** Aethori or Calethic: sibilants are ceremonial-register only (§1.1), and naming a drowned thing with priestly vocabulary would imply an Aethori connection that canon has not decided (open item 3). |
| Market Factor | Common: *factor* is the trade word for an agent who buys and sells on another's behalf. Register check — the seeded catalogue already uses plain commercial Common (*Korreld Exchange*, "merchant stalls"), so no Calethic term is wanted; §5.2 records that no Calethic root for trade or commerce exists. |
| Gate Watch | Common: *gate* + *watch*, the ordinary word for a town guard. Calethic has administrative keepers (*rekern*, §4.2) but a Common watch-post would not use it, and inventing a Calethic office for a level-1 town would over-grade it. |
| Shrine Keeper | Common: *shrine* + *keeper*. Deliberately not *–ori* (§2.2, the priestly agentive, Aethori-tier only) — a shrine keeper is not Aethori and must not carry their suffix. |
| Dock Hand | Common, a working noun. Tolvaren is a Saltcliff working-name region (Q2) with no Calethic form; the harbour is named in Common. |

### Townspeople (absorbed from `towns.md` §3)

These ten are people, not beasts, so their names are Calethic where the creature names above are Common:
`calethic-language.md` §2.5 forms a personal name from a function root plus an affiliation root, or one
root plus an agentive suffix, and §1.1 forbids **S**/**SH** and /u/ outside Aethori ritual speech. Every
derivation is reproduced from `towns.md` §3, and each name is a byname the town gave its bearer rather
than a birth name.

| name | derivation |
|---|---|
| Korrern | *korr* (passage) + *–ern* (agent, non-priestly) — "the passage-keeper" |
| Kaenvren | *kaen* (duty) + *vren* (frontier); *kaen.vren* splits an N coda against a VR onset — "duty at the frontier" |
| Raekmeld | *raek* (light-in-material) + *meld* (still water) — "glowing still-water", a byname for mead |
| Dornrek | *dorn* (below) + *rek* (administer) — "keeper of what is below" |
| Gaelrek | *gael* (water, flow) + *rek* (administer, keep in place) — "keeper of the water" |
| Laeldern | *laeld* (root, the thing that holds ground) + *–ern* (agent) — "the anchor-keeper" |
| Thaeldren | *thael* (illuminate) + *vren* (frontier); the formal –l + vr– junction takes the V-drop and the euphonic –d– bridge of *veldrath* — "the light at the frontier" |
| Daeldvaen | *daeld* (ash) + *vaen* (iron); –ld + v– is two consonants, legal — "ash-iron" |
| Raekdaeld | *raek* (light-in-material) + *daeld* (ash) — "glowing ash", the byname for ash-glass |
| Kaendaeld | *kaen* (duty) + *daeld* (ash) — "duty to the ash" |

## The two roster rows this card gave a stand

`towns.md` §4 places fifteen people. Fourteen of them stand at a settlement location — the thirteen D3
townspeople, plus `dock-hand` at `tolvaren-harbour`, which is `IsTown` and is the harbour's second stand.
The fifteenth is `shrine-keeper`, at the Ash Shrine, and is the only one of the fifteen who stands in no
town. Both `§` rows in the table above existed before this card, with their identity lines and naming
derivations unchanged and approved as written; what did not exist was a stand, the role flags a seeder
needs, and a voice. Those are `towns.md` §4.3 (Tolvaren's harbour) and §4.5 (the Ash Shrine), and the
table below is the roster-side statement of the same decision so that this document remains the place a
seeder author checks a `§` row against.

| slug | type key | traits | stands at | that location's band | `SortOrder` | renders as |
|---|---|---|---|---|---|---|
| `dock-hand` | `townspeople` | non-hostile, immortal, `HasDialogue`; `Shopkeeper` **false**, `QuestGiver` **false** | `tolvaren` / `tolvaren-harbour` | b7-10 (`MinLevel` 10, no `MaxLevel`) | 20 | **Geldern** — the archetype's `DisplayName` carries the byname, as every D3 row does |
| `shrine-keeper` | `townspeople` | non-hostile, immortal, `HasDialogue`; `Shopkeeper` **false**, `QuestGiver` **true** | `ashfields` / `ash-shrine` | b19-22 (`MinLevel` 20, `MaxLevel` 25) | 10 | **Shrine Keeper** — no byname, deliberately; `towns.md` §4.5 gives the reason |

Three things in that table are decisions, and each has a reason worth stating rather than inferring:

- **No band is added to either row, and the location's band is context only.** A `§` row's level-band cell
  is `—` by *How to read this table*, and the seeded shape for a townsperson is `MinLevel`/`MaxLevel` 1 —
  all thirteen seeded ones carry it, including the six who stand in the level-10 and level-18 towns. A
  non-hostile archetype never enters a pool, so the overlap guard that reads a range never reads this
  one. The band above is the band the location's own `MinLevel` falls in, quoted so a seeder author can
  see the stand is legal — not so a range can be invented out of it. The seeding card copies
  `Townsperson(...)` for that shape and its non-hostile traits, and sets `QuestGiver` per person: the
  helper hardcodes it `false`, which matters for `shrine-keeper` alone.
- **`QuestGiver` splits the two.** The roster calls `shrine-keeper` a quest hook and `dock-hand` a labourer
  with a rumour, and `QuestGiver` is a trait the client reads to offer a quest
  (`NpcHubCommands.NpcPlacements.ToDto`). A rumour with no quest behind it stays `false` until a quest
  names him; turning it on now would offer a quest that does not exist.
- **`Shopkeeper` is false for both, and at Tolvaren that is load-bearing.** The zone flag is on
  (`tolvaren` seeds `HasMerchant = true`) and `ShopMerchantResolver` then resolves a shop from a placement
  at the character's own location whose archetype carries `Shopkeeper`. The shelf is the zone's, not the
  shopkeeper's — `ZoneStockItems` is keyed by `ZoneId`, so every merchant in a town draws from the one
  list (C6) — and a `Shopkeeper` standing at `tolvaren-harbour` would open the town's whole catalog at its
  respawn point. Neither of these two takes coin, and `harbour-master` is the roster's existing
  precedent for a Tolvaren townsperson who trades nothing.

**Neither slug is in `ArchetypeSeeder.cs`**, which is why `towns.md` marks both `(new)` while this roster
already lists them: the roster row is authored, the seeded row belongs to a later card. The order is
archetypes, then placements, then greetings, because each row is written in terms of the one before it. A
placement whose archetype row is missing still lists the person — `NpcPlacements.ToDto` takes a nullable
archetype — but renders them by the placement's own `DisplayName` or the bare slug, with no `TypeKey` and
every role flag read as false. A greeting is matched on the speaker slug alone (`NpcGreetings.PickAsync`
reads the dialogue catalog and never the archetypes), so a greeting authored for a slug no placement
carries has no one to be picked for. Neither is a validator error — placements are seeded from C#, and
`MissingSpawnArchetype` audits `SpawnPoolReference` only — so the order is a discipline, not an enforced
reference. `pool-matrix.md` §6 states the same order for the hostile rows.

## Numbered canon proposals

Separate from the table above. **Nothing in the table depends on proposals 3, 4, 5, 7 or 8** — those
are listed because the roster surfaced them, not because content was written against them. Any
archetype that would have depended on an unapproved proposal was held out and named in "what this
roster does not cover".

### Conflicts first — locked facts against the code

**Proposal 1 — the retired names were still live in seeded prose. (Resolved 2026-11-20, `c213f409`;
correction, not new lore.)**
When this document was written, `ApplicationDataSeeder.cs` and the map nodes that mirror it in
`MapGraphSeeder.cs` described Greymoor as "Dravan highlands", the barrow-deeps as holding "ancient
Dravan relics", Saltcliff as "the drowned remnants of a sunken empire" and Cinderplain as "Kaldrek's
scorched expanse". `world-lore-plan.md` records that the "old Sundering/Age of Embers/Dravan/Thysmara/
Kaldrek lore" was *replaced* by the Collapse / Ashen Age / Calethic Empire / Aethori / Unbound
material (session log, 2026-04-14), so the prose was stale and a content worker reading the seeders
would write against dead lore — I nearly did. `c213f409` rewrote both seeded copies: Greymoor is
"fog-wreathed highlands where ancient burial mounds dot the heather", Saltcliff "the drowned remains
of whatever stood here before them", Cinderplain "a scorched expanse of ash and cooled lava", and
`World.Era` is "The Ashen Age (the Age of Reclaiming)".
`SeededWorldTextTests` now fails the build on a retired token or on the two copies drifting apart.
Its token list is Sundering, Age of Embers, Dravan, Thysmara and Kaldrek — "sunken empire" is not on
it, so that phrase alone could return unnoticed.
**No decision from this proposal is outstanding.** The seeded display names carry no retired token —
"The Maw", "The Maw's Heart", "The Fire-Ancient's Chamber", "Fire-Ancient" (`ApplicationDataSeeder.cs`,
`MapGraphSeeder.cs`, `ZoneLocationsSeeder.cs`, `ArchetypeSeeder.cs`) — and the Cinderplain display work
that remains is K1's table under "The Cinderplain naming question" below. **The slugs stay:**
`kaldrek-maw`, `kaldreks-heart`, `fire-ancients-chamber` and the archetype `fire-ancient` are opaque
permanent keys — ids are not canon, display names are, and renaming a slug is a cross-database
migration with its own card (`world-lore-plan.md`).
**Recorded in:** `towns.md` §6 C2 and the seeded text; the guard in `SeededWorldTextTests` is the
evidence.

**Proposal 2 — `aurelian-market` versus The Korreld Exchange.**
`calethic-language.md` §5.2 records "Aurelian Market" as **resolved and renamed to The Korreld
Exchange", on the grounds that Aurelian has no derivable origin in Calethic or in the established
Common naming pattern. `ZoneLocationsSeeder.cs` still seeds `Slug = "aurelian-market"`,
`DisplayName = "The Aurelian Market"`, and `MapGraphSeeder.cs` places a node for it. **Claim:** the
display name is wrong; the slug is a legacy alias that must not be changed in place. **Reason:** two
locked sources disagree and the roster references this zone for `market-factor`. **Touches:**
`crestfall`, the `aurelian-market` location, `MapGraphSeeder` node `zone:crestfall:location:aurelian-market`.
**Recorded in:** the `aurelian-market` bullet under *Status* and `towns.md` §6 C3. **Decided:** the
slug stays and the display name becomes The Korreld Exchange; the seeded zone still reads "The
Aurelian Market" until that rename lands.

### Proposals to the open questions in `world-lore-plan.md`

**Proposal 3 — what came back up from the Pits has no combat form. (Proposal to open item 4.)**
**Claim:** the thing sealed below (or come up from) the Collapsed Vault and the Deepest Chamber is
atmospheric and scripted; it never appears as a pool enemy and never gets a stat line. Its presence
is expressed by what the existing pools do around it and by a scripted encounter. **Reason:** a boss
statline would have to be banded, and any band converts the arc's central unknown into a level the
roster has no basis to set. **Touches:** `drowning-pits` (`deepest-chamber`), `the-halrow`
(`collapsed-vault`), the Varenmark arc payoff. **Recorded in:** `world-lore-plan.md` item 4, then
`wiki/Varenmark-Zone-The-Halrow.md` and `wiki/Varenmark-Zone-Drowning-Pits.md` (both already carry
`<details>` spoiler blocks).

**Proposal 4 — the Vraen stay off the 1–20 ladder. (Proposal to open item 2.)**
**Claim:** no Vraen archetype exists at any level in this roster; they are history in Varenmark, not
a live order a player meets before the Wound arc. **Reason:** the roster needs to know whether the
Vraen can be encountered at all, and the plan reserves them for a later reveal — decide the boundary
now and no archetype card has to guess. **Touches:** Varenmark, the Wound arc. **Recorded in:**
`world-lore-plan.md` item 2.

**Proposal 5 — no Aethori appear at levels 1–20, in any region. (Proposal to open item 3.)**
**Claim:** the Aethori surfaces as encounterable content only in the Wound arc, and each of the three
tiers is decided then. **Reason:** item 3 asks only about Varenmark, but the roster places enemies in
four regions and needs an answer of the same shape for all of them. **Touches:** all regions; the
Aethori entry in `world-lore-plan.md`. **Recorded in:** `world-lore-plan.md` item 3.

### Faction proposals the roster would use once approved

**Proposal 6 — the gull-rider clans get a lore entry.**
**Claim:** Saltcliff supports rival clans who work the cliff updraughts, fight each other over
wreck-salvage rights, and are a plurality with no single leader. **Reason:** two roster rows tie to
them, and the only existing source is seeder prose ("contested by rival gull-rider clans") plus the
seeded `gull-rider-brute` — accurate, but not written down anywhere a lore reader will look.
**Touches:** `saltcliff` region, `saltcliff-heights`, `tidewrack-flats`. **Recorded in:**
`wiki/Region-Saltcliff.md` at Phase 5+, and as a note in `OrganizationsSeeder.cs` if the clans are
promoted to an Organisation row.

**Proposal 7 — both seeded organisations get a geography.**
**Claim:** the Shadowhand Syndicate's money comes through `tolvaren` and the roads feeding it; the
Iron Anvil Guild's forge trade is centred on `skarhold` and its shops reach the four market towns.
**Reason:** the repository seeds exactly two organisations with wealth, shops, quest hooks and
reputation thresholds, and gives neither a single region, zone or city. Until they are placed, no
archetype — hostile or not — can be tied to either, and two otherwise-obvious rows
(`syndicate-collector`, a guild enforcer or bailiff) stay out of the roster. **Touches:**
`saltcliff`, `tolvaren`, `tidewrack-flats`, the roads west to `greymoor`, `cinderplain`, `skarhold`,
`crestfall`, `aldenmere`. **Recorded in:** `wiki/Region-Saltcliff.md` and
`wiki/Region-Cinderplain.md` at Phase 5+, plus the `wiki` organisation pages.

### Proposals for the gaps in `game-context/GAME.md`

**Proposal 8 — the Ash Shrine keeps the ash of the dead, not a god.**
**Claim:** the keeper at `ash-shrine` tends the remains of the armies on `scorched-battlefield`; there
is no deity, and the keeper will not say what is owed because there is nobody to owe.
**Reason:** the roster needs a priest archetype and canon contains no pantheon; this keeps the
priest without inventing a religion. **Touches:** `ashfields` (`ash-shrine`, `scorched-battlefield`),
the `shrine-keeper` row. **Recorded in:** `docs/lore/` (a Cinderplain note) and the region page at
Phase 5+.

**Proposal 9 — publish a content rating and four design pillars. (Decided 2026-11-20, `09288312`.)**
**Claim:** Veldrath should state a content rating and four design pillars. **Reason:** when this was
written, `game-context/GAME.md` §3 and §4 were both TBD — neither existed anywhere in the
repository, so no content worker could resolve a borderline description or argue a design choice.
Josh decided both on 2026-11-20 (card `t_5cd22693`) and C3 (`t_3790a7c7`) transcribed them in
`09288312`: §3 carries the Teen-equivalent, self-imposed standard with its permitted,
testimony-only and not-permitted lists, and §4 the four pillars, each written as a test with a
fails-when clause. **No decision from this proposal is outstanding.** **Touches:** every content
card. **Recorded in:** `game-context/GAME.md` §3 (content rating) and §4 (design pillars).

---

## Tail (21–30)

**Status: approved 2026-11-20.** Appended by card `t_98def8d8` (D6) after D4 authorised a tail roster
(its decision 5), and approved into this document by card `t_3ea8256b` (L6). **The 1–20 roster rows, the
band-coverage table and proposals 1–9 are untouched.** Landing the tail did amend three things above
this line, and only these: the document title (1–20 → 1–30), the band-id note in *How to read this
table* (which said no band id could be cited because `budget.yaml` did not exist), and the gap row that
read "Levels 21–30 | Out of scope for this card", which now points here. The decisions this section was
approved under are recorded in [`## Tail decisions`](#tail-decisions) at the foot of this document.

Five hostile archetypes, no bosses, bands only. They exist to close five location-level gaps that D4
measured and could not fill, the worst of which is `kaldreks-heart`, which has one pool entry and needs
two.

### What the tail was built from

- **Seeded prose, not invention.** `smoldering-reach` is already described in the seeder as "a cracked
  lava field where vents of superheated gas erupt without warning and **heat-adapted predators hunt at
  dusk**" (`ApplicationDataSeeder.cs:96`); `ashfields` as "grey ash plains … dotted with fused obsidian
  trees and the scorched skulls of fallen armies" (`:95`); `cinderplain` as "a scorched expanse of
  ash and cooled lava, where fire-touched creatures and those who work the heat stake their claim"
  (`:45`). Three of the five rows below are those sentences turned into an enemy.
- **Nothing in the tail is Calethic-built.** The Caleth surveyed the ash-plain (*Daeld-Ald*) and did
  not settle it (P4, locked), so no row is a construct, a guardian or a marker left by them. Proposal
  11 asks for that to be recorded as a rule rather than left to inference.
- **No faction.** The two seeded organisations still have no geography (proposal 7 was declined), and
  this card forbids inventing a faction in the endgame region. Every row is `none recorded`, and each
  says explicitly what the tie is *not*.
- **Bands.** `budget.yaml` published seven bands to 26, so `b23-26` was the top of the ladder when this
  section was first written. **The band table now extends to 1–30 with `b27-30` in the same wave as this
  approval (C4)**, which is why two rows sit above 26. No tail row is banded `19–22`: the ash plain's
  19–20 tier already has three D1 archetypes and a row there would have no location gap to close.
- **Spawn level is `MinLevel` and nothing else** (`EfCoreEnemyRepository.cs:81`; `budget.yaml`,
  *wiring.level* — there is no level roll). That is why there are two rows above 26 instead of one
  26–30 row: an archetype banded 26–30 spawns at 26 in every room it is pooled in, including
  `kaldreks-heart`, whose floor is 28, and only the 28–30 row can actually stand on that floor.

### The tail roster

| slug | name | one-line identity | level band | faction tie | zones it belongs to | hostile? | combat role |
|---|---|---|---|---|---|---|---|
| `vent-stalker` | Vent Stalker | Long-legged and the colour of cooled crust; it crosses the vent field under the noise of a blow and is gone before the air clears. | 23–26 (`b23-26`) | none recorded (Cinderplain fauna) | `smoldering-reach` — `vent-fields` | yes | ambusher, melee |
| `crust-crawler` | Crust Crawler | Dens under the floor of the pyreling den and surfaces where the heat has thinned the crust. | 23–26 (`b23-26`) | none recorded (Cinderplain fauna) | `smoldering-reach` — `vent-fields`, `pyreling-den` | yes | burrowing ambusher |
| `ash-picker` | Ash Picker | Breaks the cooled flow and the battlefield ash for glass and iron, and treats a claim on either as a debt owed. | 23–26 (`b23-26`) | none recorded (independent vent-claimants — no organisation) | `ashfields` — `scorched-battlefield`; `smoldering-reach` — `vent-fields` | yes | melee, disengages |
| `ash-eater` | Ash Eater | Works the chamber for what the fire leaves and has grown too heavy to leave with it. | 26–30 (spans `b23-26` and `b27-30`) | none recorded (Cinderplain fauna) | `kaldrek-maw` — `fire-ancients-chamber` | yes | bruiser |
| `maw-serpent` | Maw Serpent | Lies in the seam under the Maw's last floor, where the heat comes up, and does not need to move until something else does. | 28–30 (`b27-30`) | none recorded (Cinderplain fauna) | `kaldrek-maw` — `kaldreks-heart` (The Heart of the Maw) | yes | melee, reach |

No row is a boss. No row carries a stat number. Roster totals including the tail: **32 hostile, 17
non-hostile** (49 rows) — the totals table above is left as approved for 1–20 and is not rewritten.

### Band coverage — tail only

| band | archetypes | count |
|---|---|---|
| 23–26 (`b23-26`) | vent-stalker, crust-crawler, ash-picker | 3 |
| 27–30 (`b27-30`) | ash-eater (banded 26–30, spanning `b23-26`/`b27-30`), maw-serpent (28–30) | 2 |
| **total tail hostile** | | **5** |

`b27-30` is the approved band id, and **C4 published it in `budget.yaml` in the same wave as this
section**: the file now runs to `b27-30`, eight bands covering 1–30.

### Which pool each row unblocks

D4's matrix (`t_af051fc2`, `docs/design/content/pool-matrix.md` at L3) is the source for "as drawn".
"Pool after" is the entry set only; **weights stay L3's**, with the two consequences listed below it.

| zone | location | levels | gap as drawn by D4 | row that closes it | pool after (entries, no weights) |
|---|---|---|---|---|---|
| `ashfields` | `scorched-battlefield` | 21–25 | **No entries at all.** D4 left it `SafeRespawn` and empty; its decision 4 moves the respawn to `obsidian-grove`, which makes this the zone's only pooled location and empties `obsidian-grove`. Of the 1–20 roster only `cinderling` (20–26) reaches level 21 — `ash-jack` and `forge-deserter` stop at 20. | `ash-picker` | `cinderling` + `ash-picker` = 2 |
| `smoldering-reach` | `vent-fields` | 23–26 | `pyreling` + `cinderling` — and `cinderling` is a D1 zone-list deviation here (D1 gives it `ashfields` only). Losing it leaves one entry. | `vent-stalker`, `crust-crawler`, `ash-picker` | `pyreling`, `vent-stalker`, `crust-crawler`, `ash-picker` = 4; `cinderling` returns to `ashfields` |
| `smoldering-reach` | `pyreling-den` | 24–26 | The same `pyreling` + deviant `cinderling` pair. | `crust-crawler` | `pyreling` + `crust-crawler` = 2 |
| `kaldrek-maw` | `fire-ancients-chamber` | 26–30 | `pyreling` + `cinderling`, the second a one-band deviation sitting at its seeded ceiling (26). | `ash-eater` | `pyreling` + `ash-eater` = 2; `cinderling` returns |
| `kaldrek-maw` | `kaldreks-heart` (The Heart of the Maw) | 28–30 | **`pyreling` alone** — the gap this card exists to close. | `maw-serpent` | `pyreling` + `maw-serpent` = 2, both non-boss |

Two weight consequences for L3, neither decided here:

- **`ash-picker` at `scorched-battlefield` is one band above the location** (`b23-26` against a
  location whose `MinLevel` 21 falls in `b19-22`). D4's approved cap allows one band-tolerant entry
  per pool at **weight ≤ 2**, so `cinderling` must carry that pool's weight.
- **`b27-30` is approved (C4), so `pyreling` at `kaldreks-heart` is now a one-band-tolerant entry** and
  the same cap drops it to **weight ≤ 2**; `maw-serpent` carries the Heart, where D4 currently draws
  `pyreling` at 6. Under the bands as published today (`b27-30` top) that pool is **not** legal as
  drawn: when D4 drew it `b23-26` was the ladder's top, so both the band and the weight change are
  C4's.

### Placement checks actually run

Every row's seeded range against every location it names, using the seeder's own overlap rule
(`archetype.MinLevel <= location.MaxLevel && archetype.MaxLevel >= location.MinLevel`):

| archetype | seeded range it must carry | location levels | overlap | band distance |
|---|---|---|---|---|
| `vent-stalker` | 23–26 | 23–26 | yes | same band |
| `crust-crawler` | 23–26 | 23–26; 24–26 | yes, both | same band |
| `ash-picker` | 23–26 | 21–25; 23–26 | yes, both (23–25 and 23–26) | one step above at `scorched-battlefield`, same band at `vent-fields` |
| `ash-eater` | 26–30 | 26–30 | yes | same band |
| `maw-serpent` | 28–30 | 28–30 | yes | same band |

Band distance is D4's rule, measured on the D1 ladder where every location above level 21 is band
`21–30`; on that ladder every row here is in-band and the tail needs no tolerance at all. The
`scorched-battlefield` line is the one place two published D0 bands meet (`b19-22` at the location,
`b23-26` at the archetype), and it is flagged above as a weight consequence rather than hidden.

### What the tail still does not cover

| Gap | Reason |
|---|---|
| Bosses | `fire-ancient` (26–30) is a boss and stays out of every pool. Its placement row belongs to the boss-placement card gated on `t_14fa5465`, not here. D4's conclusion that the Heart needed "the boss or a new archetype" is now answered with an archetype. |
| The Wound (Region 5) | Still no zones, locations or map graph. Nothing can be placed there. |
| Loot | Each archetype needs exactly one loot table (`budget.yaml`, *loot.tables_per_archetype*). None is authored here. |
| Seeding | These five slugs do not exist in `ArchetypeSeeder.cs`. They must be seeded **before** any pool names them, or `SeededEncounterCoverageTests` fails and the entry logs an Error at runtime (`ActorPoolResolver.cs:121-128`). |
| Statlines for `ash-eater` and `maw-serpent` | The band is published, not deferred: `b27-30` carries hp 340–450, damage 34–48, xp 210–460, gold 35–90 and drop 0.06–0.25 (`budget.yaml`, *bands*, C4). What the tail still lacks is the assignment — neither row has values authored inside those ranges. |

### Naming — tail derivation notes

**No Calethic coinage was made for the tail either, and none was needed.** The Caleth named places,
offices and institutions; `calethic-language.md` §6 says to coin only when needed, and D1's precedent
is that every creature name in the shipped catalogue is Common. The Calethic restriction that is real
(S/SH and /u/ outside Aethori ceremonial register, §1.1) governs **Calethic** vocabulary; the S in
*Stalker* and *Serpent* is Common and unrestricted, exactly as it is in the seeded *Sump Crawler*.

| name | derivation |
|---|---|
| Vent Stalker | Common: *vent*, the seeder's own word for the gas fissures ("vents of superheated gas erupt without warning"), + *stalker*. The zone description already supplies the fact — "heat-adapted predators hunt at dusk" — so the name describes a seeded animal rather than adding one. Ground is Cinderplain, Calethic *Daeld-Ald* (§5.2), which names the region and not the animal. |
| Crust Crawler | Common: *crust* (the cooled surface of a flow) + *crawler*, the same morphology as the seeded `sump-crawler`. No Calethic form: a species is not a place, and *daeld-* would claim the Caleth named it (see proposal 11). |
| Ash Picker | Common: *ash* + *picker*, the working noun for someone who breaks a face for what is in it — the register of `ship-chandler` and `market-factor`. Deliberately not *daeld-* or a Calethic trade form; §5.2 records that no Calethic root for trade or commerce exists. |
| Ash Eater | Common, from behaviour, not from a light- or fire-root. Calethic distinguishes *raek* (light in material) from *thael* (revelation); this thing is neither, and a Calethic name would over-grade a scavenger. |
| Maw Serpent | Common: *Maw* is the zone's own name — `kaldrek-maw` is seeded with the display name **The Maw** (`ApplicationDataSeeder.cs:97`) — and the display name both Cinderplain reconciliation sources agree on; *serpent* is the plain animal word. No Kaldrek token, no Calethic coinage. |

### The Cinderplain naming question, as it stands

- **The four Cinderplain slugs keep their identifiers:** `kaldrek-maw`, `kaldreks-heart`,
  `fire-ancients-chamber`, `fire-ancient`. A slug is permanent identity; **ids are not canon, display
  names are** (recorded by `t_7a2beb7d`, decision 1).
- **The display prose is K1's table** (`t_fbc6e04b`, approved 2026-11-20; the table is that card's
  comment, never a file in this tree): R12 `Kaldrek's Maw` → **The Maw**, shipped in `c213f409`;
  R13 drops "Kaldrek's" and "final test of the worthy" from the Maw's description; R15 rewords the
  chamber's map description to `The Fire-Ancient's Chamber, the first hall inside the Maw`; R22 keeps
  `Fire-Ancient` and "Fire-Ancient Drops" unchanged.
- **The Heart's display name is decided: `kaldreks-heart` is "The Heart of the Maw".** K1's row **R16
  governs**, and the earlier architect wording (`t_7a2beb7d` decision 1, "The Maw's Heart") does not —
  R16 supersedes it. The slug stays `kaldreks-heart` everywhere in this document; only the display
  string is set, and L3 must import that string with the matrix.
- **The tail extends no retired framing.** None of the five names carries Dravan, Thysmara, Kaldrek,
  Sundering or "Age of Embers"; none calls anything a servant, heir or remnant of the ancient; none
  implies an empire or a settlement in Cinderplain that canon does not have.

### Numbered canon proposals — the tail

Numbering continues from proposal 9. These are the world facts the five rows stand on; the rows
themselves are proposed by this section's table and are approved or rejected with it.

**Proposals 10–12 are accepted (2026-11-20).** Each is recorded in full below with its claim, its
reason and where it is recorded.

**Proposal 10 — Cinderplain's heat-workers are independent claimants, not an organisation. (Accepted.)**
**Claim:** outside `skarhold`, the people who work the heat break ash-glass and iron off the cooled
flow and the battlefield, mark a claim with a pick-cut in the rock, and have no guild, no written
title and no organisation behind them. **Reason:** `ash-picker` needs a social fact to stand on, and
proposal 7 (geography for the two seeded organisations) was declined — so the row may not be
guild-tied, and the seeded region prose already has "those who work the heat" staking a claim.
**Touches:** `ashfields` (`scorched-battlefield`), `smoldering-reach` (`vent-fields`), `skarhold`'s
ash-glass trade (`ash-glass-seller`), the Cinderplain region page. **Recorded in:**
`world-lore-plan.md` under *Cinderplain settlements*, and `wiki/Region-Cinderplain.md` at Phase 5+.

**Proposal 11 — nothing standing in Cinderplain was built by the Caleth. (Accepted.)**
**Claim:** the Caleth surveyed the ash-plain and did not settle it (P4, locked), so no structure,
guardian, construct, inscription or road in the region is theirs; what stands there is volcanic,
animal, or post-Collapse human. **Reason:** the tail has no gap a Calethic ruin would fill, and
without the rule a later author will fill one with a Calethic guardian and read the `fire-ancient` as
their work — which is exactly the framing the Collapse rewrite removed. **Touches:** `ashfields`,
`smoldering-reach`, `kaldrek-maw`, the `fire-ancient`'s identity, every future Cinderplain card.
**Recorded in:** `world-lore-plan.md` (the same Cinderplain note as proposal 10); region page at
Phase 5+.

**Proposal 12 — the pyrelings do not serve the Fire-Ancient. (Accepted.)**
**Claim:** `pyreling` clusters where the vents are hottest; its relation to the `fire-ancient` is
shared ground, not service, command or cult. **Reason:** D4's `fire-ancients-chamber` row originally
described the room as guarded by pyrelings that served it — a faction-shaped claim inside the
endgame zone, the one thing this card forbids inventing, and a later quest or dialogue will build on
it. The row now reads "the room is guarded by the pyrelings that den in it" (`pool-matrix.md` §3,
the `kaldrek-maw` / `fire-ancients-chamber` row), corrected in `a857dcf0` (C4); no faction-shaped
claim may be built on the old wording. **Touches:** the `fire-ancients-chamber` prose in
`pool-matrix.md` (corrected there to "the pyrelings that den in it"), the `fire-ancient` placement
card. **Recorded in:** `pool-matrix.md` §3, the `kaldrek-maw` / `fire-ancients-chamber` row, which
carries the correction — see the decisions block below.

### How this section was checked

Run against the seeder and this document, not by eye: every new slug against
`^[a-z0-9]+(-[a-z0-9]+)*$` and against `ArchetypeSeeder.cs` for collision (five fresh, zero
collisions); every zone and location slug named here against `ZoneLocationsSeeder.cs` with its
seeded level range; every row's range against every location's range using the seeder's own overlap
rule; the tail table against a no-stat-digits and eight-column check; `kaldreks-heart` resolved to two
non-boss entries; and `git diff` confirming the change is an append with zero lines removed above it.
The markdown link check runs over the whole file. Results are quoted on card `t_98def8d8`.

**Landing check (L6, `t_3ea8256b`).** The append was re-applied against `development` after the
rebase: the base blob of `archetypes.md` matched the one D6 wrote the section against byte for byte
(`f31911d0`), so no 1–20 roster row moved; the markdown link check ran clean over this file; and the
tables were re-checked for column count, band consistency against `budget.yaml` and the absence of
stat digits. Inside the section the only prose changed is the band state (`b27-30` approved), the
Heart's display name and the proposals' acceptance marks; above it, only the three edits named at the
top of the section.

## Tail decisions

**Approved 2026-11-20** by card `t_3ea8256b` (L6), on D6's proposal (`t_98def8d8`, commit `28e27487`)
and D4's authorisation of a tail roster (its decision 5). This block is the authority for the tail
section above; where the two disagree, this block governs.

1. **The five tail rows are approved as written.** `vent-stalker`, `crust-crawler` and `ash-picker` at
   23–26 (`b23-26`); `ash-eater` at 26–30; `maw-serpent` at 28–30. Slugs, names, one-line identities,
   faction ties, zones, the hostile flag and combat roles are unchanged from the proposal — **no row is
   a boss and no row carries a stat number.**
2. **The band table is extended to 1–30, with `b27-30` in the same wave (C4).** `b23-26` is no longer the
   top of the ladder. Because spawn level is the archetype's `MinLevel` and nothing else, that is also
   why two rows sit above 26 instead of one 26–30 row: `ash-eater` (26–30) spawns at 26 for
   `fire-ancients-chamber`, and only `maw-serpent` (28–30) can stand on `kaldreks-heart`'s floor of 28.
   Under `b27-30`, `pyreling` at `kaldreks-heart` becomes a one-band-tolerant entry and **drops to
   weight ≤ 2**; `maw-serpent` carries that pool. **C4 owns both the band and the weight change.**
3. **Proposals 10, 11 and 12 are accepted.**
   - **10 — Cinderplain's heat-workers are independent claimants with no organisation.** They break
     ash-glass and iron off the cooled flow and the battlefield, mark a claim with a pick-cut in the
     rock, and have no guild and no written title. `ash-picker` ties to no faction, and that is a tie.
   - **11 — nothing standing in Cinderplain was built by the Caleth.** They surveyed the ash-plain and
     did not settle it, so no structure, guardian, construct, inscription or road in the region is
     theirs; what stands there is volcanic, animal or post-Collapse human.
   - **12 — the pyrelings do not serve the Fire-Ancient.** Their relation to it is shared ground, not
     service, command or cult, and no faction-shaped claim may be built on the old wording.
4. **`kaldreks-heart`'s display name is "The Heart of the Maw".** **K1's row R16 governs**; the earlier
   architect wording (`t_7a2beb7d` decision 1, "The Maw's Heart") does not, and is superseded. The slug
   `kaldreks-heart` is permanent identity and does not change.
5. **Proposal 12's correction to `pool-matrix.md` is binding, and it has landed.** D4's
   `fire-ancients-chamber` row described the room as guarded by pyrelings that served it; it now
   reads "the room is guarded by the pyrelings that den in it" (`pool-matrix.md` §3, the
   `kaldrek-maw` / `fire-ancients-chamber` row), corrected in `a857dcf0` (C4). The obligation this
   decision carried is discharged.
