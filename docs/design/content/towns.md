# Towns — who lives there and how they talk

**Status: approved 2026-11-20.** Approved by Josh on 2026-11-20 (card `t_71260df0`, D3). No engine rows
were changed, no pack was imported, no seeder was edited. Every row below is a placement (`LocationNpc`) or
a copy line, not a number.

- **Merchant band — landed 2026-11-20.** `game-context/budget.yaml` → `merchants:` bands each town's merchants by
  the **town's** own level, not the player's: `gold_by_town_level` (L0 [100, 250] through L18 [1300, 3200]) is
  what a merchant of that tier may hold, and the two pricing endpoints — `price_markup` [1.0, 1.5] and
  `buyback_rate` [0.40, 0.50] — are the same in every town, quoted at 1.0 and 0.50. `stock_size` [8, 16] is how
  many of the 24 purchasable items a town stocks. `MerchantBands` holds the band and `ShopPricing` quotes its
  endpoints; a merchant's `TradeGold` is the purse a single buyback may draw on; and `MerchantBandTests` parses
  `budget.yaml` and fails the build on drift. The shopkeeper rows below still carry no numbers of their own: a
  person sells the town's stock at the band's published prices (C5).
- **Per-zone shared shop stock — landed 2026-11-20.** Two merchants in one town sell from one authored shelf
  (`ZoneStockItems`, migration `20261121004324_AddZoneStockItems`) rather than the whole catalogue: nine rows at
  Crestfall, twelve at Aldenmere, eleven at Tolvaren, twelve at Skarhold. Per-NPC stock is still an engine
  change and is not in this wave (C6).
- **`playground` — excluded.** The fifth `IsTown` zone is `IsDevOnly` and is never returned by public
  APIs, so it carries no roster (§2).

**Sources read for this document:** `RealmEngine.Data/Seeders/ZoneLocationsSeeder.cs`,
`Veldrath.Server/Data/Seeders/ApplicationDataSeeder.cs` (the Zone rows),
`Veldrath.Server/Data/Seeders/LocationNpcsSeeder.cs`, `RealmEngine.Data/Seeders/DialogueSeeder.cs`,
`RealmEngine.Data/Seeders/ArchetypeSeeder.cs`, `Veldrath.Server/Features/Characters/Npcs/NpcHubCommands.cs`,
`Veldrath.Server/Features/Characters/Npcs/DialogueHubCommands.cs`,
`Veldrath.Server/Features/Characters/Combat/ActorPoolResolver.cs`,
`Veldrath.Server/Features/Characters/VisitShopHubCommand.cs`,
`.github/agent-memory/world-lore-plan.md`, `docs/lore/calethic-language.md`.

**Publication.** `docfx.json` publishes `docs/design/`, and the register notes below are spoilers.
`docs/design/content/**` is excluded from that build, so this file does not reach `docs.veldrath.com`.

---

## 1. What the engine already gives us (and what it refuses)

Read from code, with the line that says so. Every rule below shaped the roster.

| Fact | Where | Consequence for this document |
|---|---|---|
| A person is placed by a `LocationNpc` row: `NpcId`, `ZoneId`, `LocationSlug`, `ArchetypeSlug`, optional `DisplayName`, `SortOrder` | `Veldrath.Server/Data/Entities/LocationNpc.cs`; seeded in `LocationNpcsSeeder.cs` | Every roster row here is a `LocationNpcs` entry, keyed by zone **and** location. An NPC is only meetable where the character's current zone *and* location match (`NpcHubCommands.cs:157-158`). |
| A spawn pool entry becomes an attackable enemy, and a non-hostile one is dropped with a warning | `ActorPoolResolver.cs:82-87` | No townsperson appears in any pool. Section 7 proves the four settlements have zero pool entries. |
| An empty pool means nothing spawns and nothing is substituted | `ActorPoolResolver.cs:56-61` | `SafeRespawn` towns keep an empty pool. That is authored content, not a gap. |
| Dialogue is keyed by **archetype slug**, not by placement | `DialogueHubCommands.cs` `PickEntryAsync` (`Speaker == slug`), `NpcGreetings.PickAsync` | Two people sharing one archetype share one voice. Distinct registers per town therefore require distinct archetype slugs — the main cost in this card. |
| `TalkToNpc` greets from entries with `TypeKey == "greetings"`; with none it falls back to "*{name} looks up as you approach*" | `NpcHubCommands.cs` `NpcGreetings` | A placed person with no greeting row is a person with nothing to say. One already exists today (C4). |
| The dialogue panel only opens on an entry that has `Stats.Options` | `DialogueHubCommands.cs` `PickEntryAsync` | Greetings-only NPCs are barked at, not conversed with. Conversations are wave B/C content; this card does not pretend to ship them. |
| Shops are **per zone and per placed person**: `VisitShopHubCommand(CharacterId, ZoneId)` and `GetShopCatalogHubCommand(Guid CharacterId, string ZoneId)` resolve the merchant from the character's own zone *and* location — the zone must carry `HasMerchant`, a `LocationNpc` whose archetype has `Traits.Shopkeeper` must stand at the character's location, and a `ZoneId` that is not the character's own is refused as `ZoneMismatch` | `ShopMerchantResolver.cs:36-122` (zone flag `:56-67`, placements `:69-84`, the `Shopkeeper` test `:96-113`, refusal `:122`); `VisitShopHubCommand.cs:13`; `GetShopCatalogHubCommand.cs:18` | A zone flag alone is not a shop, so the §4.2–4.4 rosters are load-bearing: a town whose shopkeeper is not placed refuses trade. The shelf is the zone's (`ZoneStockItems`), never the person's, so two merchants in one town share one stock (C6). |
| Role flags (`Shopkeeper`, `QuestGiver`, `HasDialogue`) come from the archetype's `Traits`; `Occupation` is the archetype's `TypeKey` | `NpcHubCommands.cs` `NpcPlacements.ToDto` | New townspeople need `TypeKey = "townspeople"` to read like the existing three. |
| `DisplayName` on the placement overrides the archetype's name | `LocationNpc.cs:40-41` | Used to give the three existing generic archetypes a person's name without minting new archetypes. |

---

## 2. The settlements, as the seeder lists them

`ZoneLocationsSeeder` marks 18 locations `IsTown = true` across five zones. Four are settlements; the fifth
is the dev playground.

| Zone | Level | Locations (`S` = SafeRespawn, `M` = HasMerchant) | NPCs here today |
|---|---|---|---|
| `crestfall` | 0 | `crestfall-square` [S], `aurelian-market` [M], `hearthbound-inn`, `low-quarter` | 3 |
| `aldenmere` | 5 | `ironhollow-keep` [SM], `aldenmere-marketplace` [M], `grey-cup` | 0 |
| `tolvaren` | 10 | `tolvaren-harbour` [S], `cliff-road-market` [M], `saltcrow-inn` | 0 |
| `skarhold` | 18 | `forge-quarter` [SM], `caldera-market` [M], `ashbrand-lodge` | 0 |
| `playground` | 0 | 5 dev locations (`IsDevOnly = true`, `IsDiscoverable = false`) | 0 |

**Deliberately not populated: `playground`.** It is the dev sandbox, is never returned by public APIs
(`ApplicationDataSeeder.cs:99`: `IsDevOnly = true`), and its "townspeople" would be fixture noise. Writing
people into it would be inventing settlements. `playground-npc-hall` stays the harness for NPC mechanics.

**One NPC per town location**, thirteen in total. A location with two people and one line each reads worse
than one person who has something to say, and every additional archetype is another voice to maintain.

---

## 3. Naming

Rules applied: `docs/lore/calethic-language.md` §2.5 (personal names are `[function root] + [affiliation root]`
or a single root plus an agentive suffix), §1.1 (no **S** or **SH** outside Aethori ritual speech, no /u/),
§1.3 (initial clusters only VR–, KR–, DR–, THL–; final clusters only –LD –LT –RN –TH –VN –RG –LN –ND;
syllable-split legalises junction clusters), §2.2 (the `–ern` agent is non-priestly; `–ori` is Aethori-only).

Every name was machine-checked against those rules and against all 213 seeded slugs — §7.

| Name | Derivation | Reads as |
|---|---|---|
| **Vaenern** | *vaen* (iron) + *–ern* (agent, non-priestly) | "the iron-worker" |
| **Velrek** | *vel* (endure) + *rek* (administer) | "one who keeps it standing" |
| **Tharrek** | *thar* (true, oath-bound) + *rek* (administer) | "the oath-bound keeper of the record" |
| **Korrern** | *korr* (passage) + *–ern* (agent) | "the passage-keeper" |
| **Kaenvren** | *kaen* (duty) + *vren* (frontier); *kaen.vren* splits N coda + VR onset | "duty at the frontier" |
| **Raekmeld** | *raek* (light-in-material) + *meld* (still water) | "glowing still-water" — a byname for mead |
| **Dornrek** | *dorn* (below) + *rek* (administer) | "keeper of what is below" |
| **Gaelrek** | *gael* (water, flow) + *rek* (administer, keep in place) | "keeper of the water" |
| **Laeldern** | *laeld* (root, the thing that holds ground) + *–ern* (agent) | "the anchor-keeper" |
| **Thaeldren** | *thael* (illuminate) + *vren* (frontier); formal –l + vr– junction: V-drop plus the euphonic –d– bridge of *veldrath* | "the light at the frontier" |
| **Daeldvaen** | *daeld* (ash) + *vaen* (iron); –ld + v– is two consonants, legal | "ash-iron" |
| **Raekdaeld** | *raek* (light-in-material) + *daeld* (ash) | "glowing ash" — ash-glass |
| **Kaendaeld** | *kaen* (duty) + *daeld* (ash) | "duty to the ash" |

Regression names are bynames the town gave them, not birth names — a smith called Vaenern is called that
because he works iron, which is how Common names survive in a place with no scribes.

**Renamed 2026-11-20 — `Gaelraeth` → `Gaelrek`.** The harbour-master's byname stood as *gael* + *raeth*,
which put it one letter from **Kael-Raeth** ("compact's threshold", `calethic-language.md` *kael* entry)
and from **Vaelraeth**, the Wound's first zone. D3's check tested the 213 seeded slugs, so the lexicon was
never in scope and the collision escaped. The replacement keeps the water root and takes *rek*
(administer, keep in place) — the root *Dornrek* and *Velrek* already use — so the byname says what the
officer does instead of sitting one letter off a threshold. Edit distance: **4 from *Kael-Raeth***, 4 from
*Vaelraeth*; the nearest attested form is *Gaeldrak*/*Vaelren* at 2 and the nearest name in this table is
*Velrek* at 2, so no listed form is within one edit. *(F2, card `t_4e58e1ec`.)*

---

## 4. The four towns

### 4.1 Crestfall — the tally at the crossing

Seeder facts: level 0, four locations, `crestfall-square` is `SafeRespawn`, `aurelian-market` is the merchant
location. Zone prose: a crossroads market town where three old drove roads meet.

**Placement: all four are `LocationNpcs` rows.** `crestfall-square` / `aurelian-market` / `hearthbound-inn` /
`low-quarter` carry no `ActorPool` entry; `crestfall-square` is `SafeRespawn` and its pool stays empty.

| `slug` (archetype) | placement id | name | role | wants | sells / teaches / refuses | dialogue id |
|---|---|---|---|---|---|---|
| `village-blacksmith` *(exists)* | `crestfall-aurelian-blacksmith` *(exists)* | Vaenern | smith, market stall | work that is not horseshoes; ore he can trust | sells forged iron and tool repair; refuses to arm anyone he has not seen fight | `blacksmith-greeting` *(exists)* |
| `tavern-keeper` *(exists)* | `crestfall-hearthbound-keeper` *(exists)* | Velrek | keeper, the Hearthbound Inn | a full house and no trouble under his roof | sells bed, board, small ale; refuses credit, and refuses to talk about the north road after dark | `tavernkeeper-greeting` *(exists)* |
| `wayfarer-elder` *(exists)* | `crestfall-square-elder` *(exists)* | Tharrek | tally-keeper of the square | every name on the road written down before the light goes | teaches where the roads lead; refuses to say who laid the slab under the square | `wayfarer-elder-greeting` **(new)** |
| `road-carter` **(new)** | `crestfall-lowquarter-carter` **(new)** | Korrern | carter, low quarter | one more haul before the light goes | sells a place on the cart; refuses to carry anything sealed | `road-carter-greeting` **(new)** |

**Register.** Crestfall talks in numbers: how many carts, which road, what it cost. Sentences are short and
end in a fact. Nobody here talks about the slab in the square — they call it *the doorstep*, step over it,
and change the subject to the weather, which is the only subject they consider polite.

**Ambient set:** (enter or idle; not quest hooks)

1. "Road's dry past the old clearing. Give it a day and it won't be."
2. "The elder keeps the tally. He'll want your name before your coin."
3. "Two carts north, one south, and none since noon. Quiet is not the same as safe."
4. "Wipe your boots on the doorstep. Everyone does."
5. "Buy before the light goes. The stalls pack up early here."

**Canon:** P1.

---

### 4.2 Aldenmere — the town that was reoccupied

Seeder facts: level 5, three locations, `ironhollow-keep` is `SafeRespawn` + merchant, `aldenmere-marketplace`
is merchant, `grey-cup` is an inn. Zone prose: grey-stone waytown on old foundations, known for mead,
mercenaries and secrets.

**Placement: all three are `LocationNpcs` rows.** No `ActorPool` entry at any of the three; `ironhollow-keep`
is `SafeRespawn` and its pool stays empty.

| `slug` (archetype) | placement id | name | role | wants | sells / teaches / refuses | dialogue id |
|---|---|---|---|---|---|---|
| `keep-castellan` **(new)** | `aldenmere-keep-castellan` **(new)** | Kaenvren | hired captain, Ironhollow Keep | the keep's pay chest filled before the moor freezes | teaches the safe crossings of the moor, for coin; refuses to say who holds the keep's lower floors | `keep-castellan-greeting` **(new)** |
| `mead-seller` **(new)** | `aldenmere-marketplace-mead` **(new)** | Raekmeld | mead-seller, marketplace | her barrels emptied before they turn | sells Aldenmere mead by the jug; refuses credit to anyone wearing guild colours | `mead-seller-greeting` **(new)** |
| `cellar-keeper` **(new)** | `aldenmere-greycup-keeper` **(new)** | Dornrek | keeper, the Grey Cup | the front room full and the deep cellar left alone | sells board and a dark corner; refuses the deep cellar at any price | `cellar-keeper-greeting` **(new)** |

**Register.** Aldenmere talks in contracts: who is paying, for how long, and what happens if the work runs
long. Hospitality is a transaction and everyone is content to be priced. What it will not discuss is the
foundations — the vault, the deep cellar, the masonry older than the walls. Ask directly and you get a
price for something else.

**Ambient set:**

1. "Mead first, contracts after. Nothing is signed on an empty stomach."
2. "The moor moves the waymarks. Hire a guide, or do not cross."
3. "Grey stone needs no repair. It was old before the walls went up around it."
4. "Keep to the streets after dark. The cellars stay shut for good reason."
5. "You want a blade? Half this town is for hire and the other half is drinking."

**Canon:** P2.

---

### 4.3 Tolvaren — the harbour that does not say its own name

Seeder facts: level 10, three locations, `tolvaren-harbour` is `SafeRespawn`, `cliff-road-market` is merchant,
`saltcrow-inn` is an inn. Zone prose: a salt-caked port clinging to the cliff face, harbour full of storm-worn
ships and bold traders.

**Placement: all three are `LocationNpcs` rows.** No `ActorPool` entry; `tolvaren-harbour` is `SafeRespawn` and
its pool stays empty.

| `slug` (archetype) | placement id | name | role | wants | sells / teaches / refuses | dialogue id |
|---|---|---|---|---|---|---|
| `harbour-master` **(new)** | `tolvaren-harbour-master` **(new)** | Gaelrek | harbour-master | every hull accounted for at the turn of the tide | teaches the tide line and the safe moorings; refuses berth to a ship with no name on its papers | `harbour-master-greeting` **(new)** |
| `ship-chandler` **(new)** | `tolvaren-cliffroad-chandler` **(new)** | Laeldern | chandler, cliff road market | nothing on her shelves the sea has already ruined | sells rope, pitch and lamp oil; refuses line to a crew that will not say where it is bound | `ship-chandler-greeting` **(new)** |
| `port-innkeeper` **(new)** | `tolvaren-saltcrow-keeper` **(new)** | Thaeldren | keeper, the Saltcrow | the cliff-road lamp burning from dusk to dawn | sells a bed and a hot meal; refuses to answer the harbour bell, or to say why | `port-innkeeper-greeting` **(new)** |

**Register.** Tolvaren is loud and bargaining and entirely about the sea: tide, cargo, weather, the price of
rope. Sentences are shouted across water and get to the number fast. What it will not discuss is the water
beneath the water — the bell nobody rings, the drowned city under the harbour. It is not superstition; it is
an agreement not to look.

**Ambient set:**

1. "Tide turns at dusk. Be off the rocks before it does."
2. "Everything in Tolvaren comes up wet or goes out salted."
3. "Nobody rings the harbour bell. Do not go looking for the rope."
4. "Rope, pitch, lamp oil. The sea takes all three and asks for more."
5. "Storm off the point, third this month. The cliffs know before we do."

**Canon:** P3.

---

### 4.4 Skarhold — the town with no old stone under it

Seeder facts: level 18, three locations, `forge-quarter` is `SafeRespawn` + merchant, `caldera-market` is
merchant, `ashbrand-lodge` is a lodge. Zone prose: a forge-city inside a dormant caldera, best smiths in the
world under sulphur skies.

**Placement: all three are `LocationNpcs` rows.** No `ActorPool` entry; `forge-quarter` is `SafeRespawn` and its
pool stays empty.

| `slug` (archetype) | placement id | name | role | wants | sells / teaches / refuses | dialogue id |
|---|---|---|---|---|---|---|
| `forge-master` **(new)** | `skarhold-forge-master` **(new)** | Daeldvaen | forge-master, forge quarter | one order worth firing the big furnace for | teaches the ash-steel quench; refuses work that must be finished in a hurry | `forge-master-greeting` **(new)** |
| `ash-glass-seller` **(new)** | `skarhold-caldera-glass` **(new)** | Raekdaeld | ash-glass trader, caldera market | a buyer who knows what ash-glass is for | sells ash-glass, crucibles and flux; refuses to sell to anyone who calls the caldera dormant | `ash-glass-seller-greeting` **(new)** |
| `lodge-keeper` **(new)** | `skarhold-ashbrand-keeper` **(new)** | Kaendaeld | keeper, the Ashbrand Lodge | every smith rested and every brand entered in the book | sells bunk and board; refuses a bed to anyone who will not take the brand | `lodge-keeper-greeting` **(new)** |

**Register.** Skarhold talks about work and nothing else: heat, slag, orders, what the vents are doing today.
It is the only settlement in the world with no pre-Collapse stone under it, so it is the only one that can
discuss the Collapse plainly — and does, as geology. What it will not say is that the caldera is not dormant.
Everyone knows. Everyone says "quiet since the founding" and then talks about the price of flux.

**Ambient set:**

1. "Mind the vents. They breathe when the work is hot."
2. "Ash on everything. You stop noticing by the second day."
3. "Bring me slag and I will bring you steel. Bring me excuses and I will bring you the door."
4. "Caldera's been quiet since the founding. Best not to say it out loud."
5. "Everything here is built from what the mountain threw up. Waste nothing."

**Canon:** P4.

---

## 5. Canon proposals

Each is a numbered claim the content above depends on. Nothing else in this document depends on an unapproved
item — the rosters and barks were written so that each proposal can be rejected and the town still works, at
the cost of the stated row.

**P1 — Crestfall was settled at the crossing, not at the stone.**
*Claim:* Crestfall was settled by drovers at the *Korreldh* crossing during the Reclaiming. The slab in the
square is that crossing's threshold marker (*raeth*), worn blank by three generations of feet; the town has no
record of who laid it and does not want one. The town's founding claim is the crossing.
*Reason:* The starter town needs a stated origin for the elder's tally and the "step over the doorstep" register
to refer to, and the language pass already established *Korreldh* as Crestfall's Calethic antecedent. This
deliberately does **not** answer open item #7 (whether the marker stone is the extent of Crestfall's Calethic
history) — it fixes only what is above the ground.
*Touches:* Crestfall zone prose; `wayfarer-elder` row; barks 3–4.
*Destination:* `.github/agent-memory/world-lore-plan.md` → Locked World Facts → Varenmark settlements
(mirror: `wiki/Varenmark-Zone-Crestfall.md`).

**P2 — Aldenmere was reoccupied, not founded.**
*Claim:* Aldenmere stands on a Calethic administered waypoint whose name was *Aldeld* (*ald*, open ground +
*–eld*, site: "the cleared site"). The name corrupted to *Alden* by the same step as *Greveld* → *Greven*;
Common *mere* was appended later for the wet ground under it. The "old foundations" the zone prose already
mentions are that masonry.
*Reason:* The seeder already gives Aldenmere old foundations, a vault and a deep cellar, and none of it has an
origin; and the corruption path needs no new rule — it is the Grevenmire step applied once more.
*Touches:* Aldenmere zone prose; `keep-castellan`, `cellar-keeper` rows; register note.
*Deliberately leaves open:* what is underneath. This adds a thread rather than closing one.
*Destination:* `.github/agent-memory/world-lore-plan.md` → Locked World Facts, new "Greymoor settlements"
subsection (mirror: `wiki/World-Veldrath.md`).

**P3 — Tolvaren's name has no surviving derivation.**
*Claim:* Tolvaren is a Common name of no surviving derivation, coined by the fisher families who resettled the
anchorage during the Reclaiming. Its Calethic survey name was *Gaelvren* (*gael* water + *vren* frontier, "the
water's edge"), which survives in coastal records and is never spoken in the town.
*Reason:* The town needs a name history and there is no Calethic root that yields "tol-"; coining one would
expand the root dictionary for a single place name. The Droveway already sets the precedent that Common
speakers coin a name and discard the Calethic one entirely.
*Touches:* `harbour-master` row; register note ("nobody knows what the name means").
*Adds no new people and no new language.*
*Destination:* `.github/agent-memory/world-lore-plan.md` → Locked World Facts, new "Saltcliff settlements"
subsection (mirror: `wiki/World-Veldrath.md`).

**P4 — Skarhold is the only settlement with no pre-Collapse foundation.**
*Claim:* Skarhold was founded after the Collapse by smiths drawn to the caldera's heat, and it has no Calethic
antecedent. The Caleth surveyed the ash-plain (*Daeld-Ald*) but did not settle it; the town's own name is a
Common coinage, which is why it carries a sibilant that no Calethic word may.
*Reason:* It gives the Cinderplain a settlement that can talk about the Collapse plainly, and it explains an
S-initial place name without inventing a Calethic root that could not contain S.
*Touches:* Skarhold zone prose; `forge-master`, `ash-glass-seller` rows; register note.
*Leaves untouched:* the Kaldrek question. `kaldrek-maw` is a separate dungeon name at level 26 and this
proposal neither uses nor retires it.
*Destination:* `.github/agent-memory/world-lore-plan.md` → Locked World Facts, new "Cinderplain settlements"
subsection (mirror: `wiki/World-Veldrath.md`).

---

## 6. Canon conflicts found while writing this

These are not proposals — they are places where two things already written disagree, or where player-facing
text refers to something that no longer exists. Entries marked *resolved* below keep the claim that was wrong
when this document was written and name the change that landed the fix.

**C1 — Player-facing text names a deleted region.** `blacksmith-greeting` line 3 reads "Finest ironwork in
Thornveil, guaranteed." `thornveil` was the pre-Crestfall starter region and was replaced by `varenmark` /
`crestfall`; the slug survives only in old memory notes and a serializer test fixture. A player can be told
about a region that does not exist. Seeders are insert-only, so a fix must go through the importer/upsert, not
by editing the row.

**C2 — Retired lore is still in the Zone and Region prose. (Resolved 2026-11-20, `c213f409`.)** When this
document was written, `World.Era = "The Age of Embers"`; Aldenmere was "a grey-stoned **Dravan** waytown";
Tolvaren "a salt-caked **Thysmara** port city"; Cinderplain "**Kaldrek's** scorched expanse"; Greymoor
"fog-wreathed **Dravan** highlands". The world-lore plan (2026-04-14 entry) records that lore as *replaced* by
the Collapse / Ashen Age / Calethic Empire. `c213f409` rewrote both seeded copies: `World.Era = "The Ashen Age
(the Age of Reclaiming)"`, Aldenmere "a grey-stoned waytown", Tolvaren "a salt-caked port city", Cinderplain "a
scorched expanse of ash and cooled lava", Greymoor "fog-wreathed highlands" — and `SeededWorldTextTests` fails
the build on a retired token or on the two copies drifting apart. The slugs stay: `kaldrek-maw` is an opaque
key whose display name is now "The Maw".

**C3 — A resolved rename never landed.** `calethic-language.md` §5.2 marks "The Aurelian Market" as **Resolved**
and replaced by "The Korreld Exchange" (*Korreldh* → *Korreld* + Common *Exchange*). The seeder still creates
`aurelian-market` / "The Aurelian Market", and `LocationNpcsSeeder` places the blacksmith there. Renaming the
slug is not a text fix: it breaks that placement row and any stored `CurrentZoneLocationSlug`.

**C4 — A placed person with nothing to say.** `wayfarer-elder` is placed at `crestfall-square` with
`HasDialogue = true`, but no `Dialogue` row has `Speaker = "wayfarer-elder"`. `TalkToNpc` falls back to
"*Tharrek looks up as you approach.*" every time. Also note the existing speaker slugs are inconsistent:
`blacksmith-greeting` is hyphenated, `tavernkeeper-greeting` is not.

**C5 — No merchant band exists. (Resolved 2026-11-20.)** `budget.yaml` published a `merchants:` section, and the
shop applies its two price endpoints. `MerchantBands` carries the purse tiers, the stock size and those
endpoints; `ShopPricing` prices every catalogue row with them; a merchant's `TradeGold` is the purse a single
buyback may draw on, refused as `ShopRefusal.MerchantCannotAfford` when it will not cover the payout. The purse
tiers and `stock_size` are not consulted when a trade happens — no handler looks a town up in the band — so
`MerchantBandTests` is what keeps them true: it parses the YAML, compares it with `MerchantBands`, and fails the
build when a seeded shopkeeper's purse leaves its town's tier. What is still unwired is a *depleting* purse — the
authored figure caps each transaction and is never written back — and a per-town markup above the band's floor.
The shopkeeper rows in §4 still carry no numbers of their own, by design: a person sells the town's stock at the
band's published prices.

**C6 — One shop catalog per zone.** The shop keys on `ZoneId`, not on the person: `VisitShopHubCommand` and
`GetShopCatalogHubCommand` resolve the zone's merchant and read the zone's authored shelf (`ZoneStockItems`, one
row per item per town, seeded ten / twelve / eleven / twelve), so Aldenmere's keep and its marketplace sell from
the same stock, and so do Skarhold's. "What they sell" here distinguishes the people, not the inventory; per-NPC
stock needs an engine change.

---

## 7. Validation

Both scripts are in `~/.hermes/profiles/dev-content/cache/scratch/`; their inputs are the seeders themselves.

```
== slug pattern + collision ==
  checked 31 slugs against 213 seeded slugs
== name phonology ==
  checked 13 names

RESULT: PASS (0 failures)
```
`check_names.py` — every new archetype slug, dialogue slug and placement id matches
`^[a-z0-9]+(-[a-z0-9]+)*$`, none exists in the seed, and none is one edit away from a seeded slug
(Levenshtein). Every born name passes the Calethic checks: no S/SH, no /u/, no illegal initial or final
cluster, and every three-consonant run has a legal syllable split.

```
locations parsed: 67   pools parsed: 37
IsTown locations: 18

== settlements by zone (from the seeder, not from memory) ==
  aldenmere    3  aldenmere-marketplace[M]  grey-cup[-]  ironhollow-keep[SM]
  crestfall    4  aurelian-market[M]  crestfall-square[S]  hearthbound-inn[-]  low-quarter[-]
  playground   5  playground-forge[-]  playground-npc-hall[-]  playground-shop[M]  playground-square[S]  playground-tavern[-]
  skarhold     3  ashbrand-lodge[-]  caldera-market[M]  forge-quarter[SM]
  tolvaren     3  cliff-road-market[M]  saltcrow-inn[-]  tolvaren-harbour[S]

== all four documented settlements are real IsTown zones ==
  OK crestfall: 4 locations
  OK aldenmere: 3 locations
  OK tolvaren: 3 locations
  OK skarhold: 3 locations

== a town location never appears in a spawn pool ==
  OK 18 town locations, 0 in pools

== SafeRespawn towns keep an empty pool (authored, not a gap) ==
  crestfall-square       SafeRespawn=True  in_pool=False
  forge-quarter          SafeRespawn=True  in_pool=False
  ironhollow-keep        SafeRespawn=True  in_pool=False
  playground-square      SafeRespawn=True  in_pool=False
  tolvaren-harbour       SafeRespawn=True  in_pool=False

== claimed placements resolve to a real zone + location ==
  seeded placements: 3
  OK crestfall-aurelian-blacksmith -> crestfall/aurelian-market
  OK crestfall-hearthbound-keeper -> crestfall/hearthbound-inn
  OK crestfall-square-elder -> crestfall/crestfall-square

RESULT: PASS (0 failures)
```
`check_placement.py` — the settlement list is parsed out of `ZoneLocationsSeeder`, not recalled; every
`SafeRespawn` town is confirmed absent from `SpawnPools`; and each of the three existing placements resolves to
a zone that actually owns its location.

```
roster rows parsed: 13

== one NPC per settlement location, no duplicates ==
  13 NPCs / 13 settlement locations

== every archetype slug exists today or is marked new ==
  exists  village-blacksmith / tavern-keeper / wayfarer-elder
  NEW     road-carter, keep-castellan, mead-seller, cellar-keeper, harbour-master,
          ship-chandler, port-innkeeper, forge-master, ash-glass-seller, lodge-keeper

== dialogue ids exist today or are marked new ==
  2 seeded, 11 new

== barks: 4-6 per town, <= 2 sentences each ==
  Crestfall    5 barks, max 79 chars
  Aldenmere    5 barks, max 76 chars
  Tolvaren     5 barks, max 68 chars
  Skarhold     5 barks, max 89 chars

== canon proposals: numbered, each with a destination file ==
  P1: Crestfall was settled at the crossing, not at the stone
  P2: Aldenmere was reoccupied, not founded
  P3: Tolvaren's name has no surviving derivation
  P4: Skarhold is the only settlement with no pre-Collapse foundation

== pool/pool exclusion statements present ==
  LocationNpcs mentions: 8
  'pool stays empty' statements: 4 (4 towns expected)

RESULT: PASS (0 failures)
```
`check_doc.py` — parses this file back and checks the claims: one NPC per settlement location with no
duplicate slug, name or dialogue id; every archetype slug either seeded or marked `(new)`; bark count and
sentence length; one destination file per proposal.

The two content defects these checks caught are already fixed above: a three-sentence Tolvaren bark, and a
miscounted dialogue total in §8.

---

## 8. What must ship for this to be true

| Thing | Count | Owner | State |
|---|---|---|---|
| `LocationNpc` placements | 13 (3 exist, 10 new) | `LocationNpcsSeeder` / importer | placements 3 live, 10 designed here |
| Townspeople archetypes (`ActorArchetype`, `TypeKey = "townspeople"`, non-hostile, immortal, `HasDialogue = true`) | 13 (3 exist, 10 new) | **D1 roster card** — it owns the 1–20 archetype roster, so it must adopt these slugs or this card and D1 disagree | 3 live, 10 proposed |
| Dialogue greeting rows (`TypeKey = "greetings"`, `Speaker` = archetype slug) | 13 needed, one per NPC: 2 live (`blacksmith-greeting`, `tavernkeeper-greeting`), 11 new — of which `wayfarer-elder-greeting` closes C4 | wave B dialogue pass | 2 live, 11 proposed |
| Display-name overrides | 3 (one per existing generic archetype) | placement rows | designed here |

Every reference in this document closes: the 13 archetype slugs are either seeded today or named here as new;
the 13 placement ids resolve to zone + location pairs proven in §7; the dialogue ids are either seeded today or
carry `(new)` and a `-greeting` slug the dialogue pass creates. There are no arrows to files that do not exist
and no numbers that do not trace to a band — because no numbers are used at all.

## 9. Acceptance

| Criterion | Answer |
|---|---|
| Every settlement in the seeder has a roster; none invented | Four settlements parsed from the seeder (18 `IsTown` locations, 13 in settlements). The fifth `IsTown` zone, `playground`, is dev-only and deliberately excluded with the reason given in §2. |
| Every NPC explicitly placed in `LocationNpcs`, none in a pool | 13 placements, one per settlement location; §7 proves 0 of 18 town locations appear in `SpawnPools`, and all five `SafeRespawn` towns keep empty pools. |
| 4–6 barks per town, none a quest hook | 5 per town, 20 total. None names an NPC, a reward, a destination or a task; each is enter/idle flavour. |
| Every canon proposal numbered with a destination file | P1–P4, each with one destination in `world-lore-plan.md` and a wiki mirror. |
| No name collides with an existing NPC, archetype or zone slug | 31 slugs and 13 names checked against all 213 seeded slugs, including edit-distance-1: PASS. |
