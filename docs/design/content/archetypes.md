# Veldrath — archetype roster, levels 1–20

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
- **Proposal 9 — open.** Content rating and the four design pillars are undecided, so `GAME.md` §3–§4
  stay TBD.

## How to read this table

- **`†` after a name** — already seeded in `RealmEngine.Data/Seeders/ArchetypeSeeder.cs`. Carried
  over unchanged, including its slug. A slug is permanent identity: none are renamed here.
- **`§` after a name** — non-hostile. NPCs exist so trade, quest and dialogue content has an actor
  to hang off. **They never appear in a spawn pool**: a pool entry becomes an attackable enemy
  (`ZoneLocationsSeeder.cs`, *SpawnPools* remarks).
- **Level band** — a band of at most three levels, and the only number a row carries. HP, damage, AC,
  XP, gold and drop weights come from `game-context/budget.yaml`, which does not exist in this
  worktree (D0 is publishing it separately). **No band id is cited, because there is nothing to
  cite**; the level range is the only placement claim made here.
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
| **total non-hostile** | | **7** |

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
| Levels 21–30 | Out of scope for this card. Cinderplain already has `pyreling` (23–28) and `fire-ancient` (26–30) seeded, so the tail is not empty — but it is one region's worth of ladder and needs its own card. |
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

## Numbered canon proposals

Separate from the table above. **Nothing in the table depends on proposals 3, 4, 5, 7 or 8** — those
are listed because the roster surfaced them, not because content was written against them. Any
archetype that would have depended on an unapproved proposal was held out and named in "what this
roster does not cover".

### Conflicts first — two locked facts disagree with the code

**Proposal 1 — the retired names are still live in seeded prose. (Correction, not new lore.)**
`MapGraphSeeder.cs` and `ZoneLocationsSeeder.cs` describe Greymoor as "Dravan highlands" (three
times), the barrow-deeps as holding "ancient Dravan relics", Saltcliff as "the drowned remnants of a
sunken empire" (twice) and Cinderplain as "Kaldrek's scorched expanse" (three times), and seed
`kaldrek-maw`, `kaldreks-heart`, `fire-ancients-chamber` and the archetype `fire-ancient`.
`world-lore-plan.md` records that the "old Sundering/Age of Embers/Dravan/Thysmara/Kaldrek lore" was
*replaced* by the Collapse / Ashen Age / Calethic Empire / Aethori / Unbound material (session log,
2026-04-14). **Claim:** prose that still says Dravan, Thysmara, Kaldrek or "sunken empire" is stale
and should be rewritten to the Collapse vocabulary. **Reason:** a content worker reading the seeders
will treat these as canon and write against dead lore — I nearly did. **Touches:** the four region
descriptions, `barrow-deeps`, `sunken-name`, `saltcliff`, and four Cinderplain slugs whose *display
names* use Kaldrek. **Slugs cannot be renamed** (permanent identity), so this needs a decision about
display names, not slugs. **Recorded in:** a follow-up card against `MapGraphSeeder.cs`; evidence in
this document.

**Proposal 2 — `aurelian-market` versus The Korreld Exchange.**
`calethic-language.md` §5.2 records "Aurelian Market" as **resolved and renamed to The Korreld
Exchange", on the grounds that Aurelian has no derivable origin in Calethic or in the established
Common naming pattern. `ZoneLocationsSeeder.cs` still seeds `Slug = "aurelian-market"`,
`DisplayName = "The Aurelian Market"`, and `MapGraphSeeder.cs` places a node for it. **Claim:** the
display name is wrong; the slug is a legacy alias that must not be changed in place. **Reason:** two
locked sources disagree and the roster references this zone for `market-factor`. **Touches:**
`crestfall`, the `aurelian-market` location, `MapGraphSeeder` node `zone:crestfall:location:aurelian-market`.
**Recorded in:** the same follow-up card as proposal 1. **Decision needed:** revert the rename, or
change the display name and keep the slug.

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

**Proposal 9 — publish a content rating and four design pillars.**
**Claim:** Veldrath should state a content rating and four design pillars. **Reason:**
`game-context/GAME.md` §3 and §4 are both TBD because neither exists anywhere in the repository; no
content worker can resolve a borderline description or argue a design choice without them.
**Touches:** every content card. **Recorded in:** `game-context/GAME.md` §3–4, once a human approves
the text.
