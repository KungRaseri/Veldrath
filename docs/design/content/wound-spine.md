# The Wound — zone spine for the endgame region

**Status: approved 2026-11-20.** This is a spine, not a place: five zones, their names, their bands,
their verbs, the entry condition, the shape of the ending, and the list of things nobody may answer.
It authors **no locations, no map graph, no NPCs, no archetypes, no loot, no quests**. The five
decisions in §9 are taken; §7 and §9 list what is **not** landed here and waits for the region's
implementation wave.

Everything below is either sourced to a line or marked as a design claim. Where the engine cannot
express a claim, the gap is named in §1 and §7 rather than papered over.

**Sources read for this document:** `.github/agent-memory/world-lore-plan.md` (world facts, open
items 2–7); `docs/lore/calethic-language.md` (phonology, junction rules, corruption rules, root
dictionary, the named-entity pass — **the naming authority**); `game-context/GAME.md` §3–§4 (content
standard and the four pillars, landed by PR #104) and §6 (regions); `game-context/budget.yaml` (band
`b23-26`); `docs/design/content/archetypes.md` (the approved 1–20 roster and its explicit "levels 21–30
out of scope" note); `RealmEngine.Data/Seeders/MapGraphSeeder.cs`, `ZoneLocationsSeeder.cs`,
`ArchetypeSeeder.cs`; `Veldrath.Server/Data/Seeders/ApplicationDataSeeder.cs`;
`RealmEngine.Data/Validation/MapGraphValidator.cs`;
`Veldrath.Server/Features/Map/TravelToNodeHubCommand.cs`,
`Veldrath.Server/Features/Characters/Combat/AttackEnemyHubCommand.cs`,
`Veldrath.Server/Features/Zones/EfCoreZoneInstanceService.cs`,
`Veldrath.Server/Features/Zones/UnlockZoneLocationHubCommand.cs`, `Veldrath.Server/Hubs/GameHub.cs`.

**Publication.** `docfx.json` publishes `docs/design/**/*.md` and excludes `docs/design/content/**`
(landed in PR #101, `fd105299`, now on `development`). This file holds endgame spoilers and stays off
`docs.veldrath.com` by that exclusion.

---

## 1. What the engine already gives us, and what it refuses

Read from code, with the line that says so. Every row shaped a decision below.

| Fact | Where | Consequence for the spine |
|---|---|---|
| **The level gate is advisory.** `Zone.MinLevel` is described as "minimum character level to enter" yet nothing refuses entry; the handler's own comment says "the level gates are advisory … nothing downstream refuses entry" | `Veldrath.Server/Data/Entities/Zone.cs` (*MinLevel*); `TravelToNodeHubCommand.cs:564-582` | A zone's `MinLevel` cannot be the thing that keeps a level-1 character out of the endgame. §2 names the gate that does. |
| The hard gate on travel is **node visibility**: a node that is not discovered cannot be travelled to | `TravelToNodeHubCommand.cs:213-218` | The endgame entrance must be a hidden node, not a high-level node. |
| Visibility of a hidden node keys on **one string**: `node.RefId` must appear in the character's unlocked-location set | `TravelToNodeHubCommand.cs:522-537` | Only a node whose `RefId` is a **location** slug can be gated. See the next three rows. |
| A `RegionExit` node's `RefId` must be a known **region** slug (validator error otherwise) | `MapGraphValidator.cs:543`, `:552` | A region exit **cannot** be content-gated: region slugs never appear in the unlock set. This is why §2 routes the entrance through a location, not a region exit. |
| A `Location` node's `RefId` must be a known location slug, and a `Location` node may carry `TargetMapId`; travel follows it | `MapGraphValidator.cs:542`; `TravelToNodeHubCommand.cs:280-303` | A hidden **location** can therefore be both the gate and the door. |
| Clearing a room unlocks hidden locations **in the same zone** whose `UnlockKey` equals the cleared room's slug | `EfCoreZoneInstanceService.cs:274-291` | The "you cleared the last room and the floor gave way" pattern is expressible — with the next two rows' caveat. |
| …but that path only runs when the kill carries a `ZoneInstanceId` | `AttackEnemyHubCommand.cs:254-261` | It fires only for an **instanced** location. |
| **No seeded location is instanced.** `IsInstanced` is nullable, read at two places, and set by no seeder — every dungeon room is public world state | `RealmEngine.Data/Entities/Content/ZoneLocation.cs:90`; `EfCoreZoneLocationRepository.cs:96`; `TravelToNodeHubCommand.cs:551`; (no `IsInstanced` assignment in `ZoneLocationsSeeder.cs`) | The clear→unlock path is **dead in shipped content**. Setting the flag on the Maw's dungeon rooms is a content change, not an engine change — and it is what makes the entry condition an *act* rather than a number. |
| **The unlock hub method trusts the client**: `UnlockZoneLocation(LocationSlug, UnlockSource)` writes an unlock with no verification that the character holds the item or finished the quest | `GameHub.cs:2695-2705`; `UnlockZoneLocationHubCommand.cs:69-113` | Any content gate is spoofable until a server-side source check exists. §2 says so plainly instead of promising a wall it cannot build. |
| The one **non-spoofable** unlock is the passive discovery check: it compares the character's real level against `DiscoverThreshold` server-side and writes the unlock itself | `TravelToNodeHubCommand.cs:474-516` | A level wall is the only enforceable gate today. Use it as the backstop under the act-gate. |
| Spawn level is the archetype's `MinLevel` and nothing else | `EfCoreEnemyRepository.cs` — *MapToModel*, `Level = e.MinLevel` | Within band `b23-26` only levels **23** (`ArchetypeSeeder.cs:613`, pyreling) and **26** (`:658`, fire-ancient) can spawn. Zones recommended at 24–25 have nothing to populate them today. |
| The approved roster covers **1–20** and says so; levels 21–30 and the Wound are named as out of scope | `docs/design/content/archetypes.md`, §"What this roster does not cover" | The Wound's cast does not exist. That is a dependency (§7), not something this card may invent. |
| Band `b23-26`: HP 300–380, damage 30–44 per hit, XP 180–380, gold 30–75, drop chance 0.08–0.30 | `game-context/budget.yaml`, band `b23-26` | The endgame shares `b23-26` with region 4's last zones; `b27-30` is the ladder's top band. Escalation cannot come from numbers (§5). |
| `RegionType` has five members — Forest, Highland, Coastal, Volcanic, Countryside — and none of them is the Wound | `Veldrath.Server/Data/Entities/Region.cs:46-62` | A sixth member is needed. It is **free**: `Region.Type` is stored via `HasConversion<string>()`, so no migration is required (`ApplicationDbContext.cs:130`). |
| `Region.IsDiscoverable` and `Zone.IsDiscoverable` are never read | `Region.cs:28`; `Zone.cs:36`; no reader in `Features/` or the repositories | Set them honestly, but do not claim they gate anything. |
| Every zone must flag exactly one `SafeRespawn` location, or the respawn path logs an `Error` and falls back to the zone's first location | `ZoneLocation.cs` (*SafeRespawn*); `RespawnHubCommand.cs:178-183` | Each of the five zones owes exactly one safe-respawn location to the content pass. Not zero ("lawless endgame") — zero is a logged error. |
| An empty spawn pool spawns nothing and substitutes nothing **only where nothing is placed**; a location with hostile placements and no pool spawns exactly those, never topped up | `ActorPoolResolver.cs:86-92`, `:111-115` | A zone with no cast yet is authorable and does not throw. The spine can land before the roster does. |

---

## 2. The region

| | |
|---|---|
| **Seeder id** | `wound` — region ids are the Common working names (`varenmark`, `greymoor`, `saltcliff`, `cinderplain`; `ApplicationDataSeeder.cs:42-45`), and "the Wound" is the name the world uses. |
| **Display name** | The Wound |
| **Calethic form** | **Kaeltheld** — *kael* (great, first/paramount) + *theld* (controlled entry point, administered threshold) = "the paramount administered threshold"; the Caleth's name for the heartland from which every threshold was commanded (`calethic-language.md:588-596`) |
| **Type** | a new `RegionType` member — `Volcanic` is region 4's and is wrong; `Forest`/`Highland`/`Coastal`/`Countryside` are all wrong (§9, decision 4) |
| **Band** | `b23-26`; `b27-30` is the ladder's top band (`game-context/budget.yaml`) |
| **`MinLevel` / `MaxLevel`** | 23 / 30. The geography runs 0–30 (`ZoneLocationsSeeder.cs`, the `SpawnPools` remarks); the archetype ladder stops at 26 today. |
| **`IsStarter` / `IsDiscoverable`** | `false` / `false`. The second is inert (§1) but it is the honest value. |
| **Connections** | `cinderplain → wound` and `wound → cinderplain` (`RegionConnection`, `ApplicationDataSeeder.cs:58-63`). |
| **Shape** | Five zones, three of them named as thresholds and two as grounds — the Caleth's own land-survey taxonomy, not a poetic choice (§4). |

### 2.1 Entry condition — what the player must have done

**Design.** The player must have **cleared the deepest room of the Maw**, in Cinderplain, as a run:
the region-4 dungeon whose stock description already calls it "final test of the worthy"
(`MapGraphSeeder.cs:167`). The Wound is not a road out of the ash plains; it is what is behind the
last door in region 4. They arrive at the heartland carrying the fact that they got that far, and
nothing else.

This is deliberately an **act, not a number**. It is the last thing the game asks the player to do
before it stops rewarding them.

**Mechanism today.** Three parts, two of which exist:

1. **The door is a hidden `Location` node** on the `kaldrek-maw` zone map, whose location row in
   `kaldrek-maw` carries `IsHidden = true` and `UnlockKey = "fire-ancients-chamber"`, and whose node
   carries `TargetMapId = "region:wound"`. Legal and traversable per §1 (a `Location` node may carry
   a target; only its `RefId` is cross-checked).
2. **The clear must be recorded.** The act is *clearing the room* — every enemy alive at the location
   dead (`AttackEnemyHubCommand.cs:245`), not killing a named boss; the room's own pool is a 70/30
   split (`ZoneLocationsSeeder.cs`, the `fire-ancients-chamber` pool entry), so a run can clear it
   without the boss appearing and the gate must not depend on that. Recording the clear requires
   `IsInstanced = true` on the cleared room, `fire-ancients-chamber`: the kill's `ZoneInstanceId` is
   what fires the unlock loop, so the flag goes on the room that is cleared and **not** on the hidden
   gate node, which carries no instance of its own. No seeded location sets the flag (§1), so today
   the loop never fires. One content flag — no engine change — closes this.
3. **The backstop is a level wall.** The same location carries `UnlockType = "skill_check_passive"` and
   `DiscoverThreshold = 23`, which is the only server-side, non-spoofable gate that exists
   (`TravelToNodeHubCommand.cs:489-503`). A level-1 character is refused by the server, not discouraged
   by a warning.

**What does not work, and why it is not proposed.** A hidden `RegionExit` on Cinderplain's region map
was the obvious shape and it is impossible: the validator requires a `RegionExit`'s `RefId` to be a
region slug (`MapGraphValidator.cs:543`), and visibility is decided by matching `RefId` against
*unlocked location slugs* (`TravelToNodeHubCommand.cs:522-537`), so a region exit cannot be gated by content at all.

### 2.2 "Nothing here may be reachable at level 1"

Stated honestly, because the code does not make it true by itself:

- **True against an honest client**, on two independent counts: the chain runs through a room whose own
  locations are L26 and whose pool carries the top of the band at a 30% weight
  (`ZoneLocationsSeeder.cs`, the `fire-ancients-chamber` pool entry), and the threshold location is
  invisible to any character below level 23 by a server-side comparison.
- **Not true against a modified client**: `UnlockZoneLocation` accepts a location slug and an unlock
  source from the caller (`GameHub.cs:2695-2705`) and writes the unlock with no proof. Nothing in this
  spine can fix that. It is listed as a dependency in §7 and it is the same class of hole as the
  advisory level gate in §1.
- **Not true of the region row itself**: `Region.MinLevel = 23` gates nothing (`Zone.cs` *MinLevel*,
  `TravelToNodeHubCommand.cs:564-582`); it is documentation for the client.

### 2.3 Leaving

The way it was entered: back through the threshold into the Maw. The region has no second exit by
design — a scholar's route in from region 3 would make the Wound a corridor, and pillar 1 ("the world
remembers; nobody explains it") wants the heartland to be a place you can only arrive at from the
place the empire pushed hardest against.

**Not a route:** the Drowning Pits. Whether *Dornkeld* connects under the continent to the heartland
is open lore item 4 and this spine neither uses nor denies it (§6, §7).

---

## 3. The five zones

All five sit inside band `b23-26`. The "band" column is the sub-band the zone's own locations should
carry; "zone L" is the single number the `Zone` row takes, matching the seeded convention
(`ZoneLocationsSeeder.cs`: every zone is commented `(Type, L<number>)`).

| # | slug | display name (Calethic) | one-line character | band | zone L | what the player does there | what it refuses to explain |
|---|---|---|---|---|---|---|---|
| 1 | `vaelraeth` | **Vaelraeth** | The last ordinary ground, cut by a survey-marked threshold that still says the ordinary stops here. | 23–24 | 23 | Crosses a boundary whose line is not where the ground changes, and finds the first Calethic marks. Everything they fight drifted in from Cinderplain and none of it has drifted out. | Where the boundary actually is — the marks and the change in the ground do not agree, and the zone never says which came first. |
| 2 | `rektheld` | **Rektheld** | The outer administrative ring: the ground that processed arrivals, still processing, with nothing arriving. | 23–25 | 24 | Walks a route built as a queue: doors that open on nothing, counters with no clerks, the only written record of the heartland's own names. | What was being processed. The forms carry a field with no label, and it stays unlabelled. |
| 3 | `orndaeld` | **Orndaeld** | The ground the unmaking left: where the rules of magic landed, and the wrongness is local — the same act gives a different answer in the same place. | 24–26 | 25 | Crosses the densest wrongness on foot and is asked, for the only time in the game, **not to use their kit**: steel, rope and nerve still behave, abilities are a hazard. | Why the change settled here — whether it was aimed, or dropped, or left where a resealing attempt failed. |
| 4 | `draekeld` | **Draekeld** | The channel-ground: the one place in the heartland built to put a question across the compact. It was used. | 25–26 | 26 | Performs the region's single verb before the door: leaves something at the channel — a thing, a name, a record of their own. The game records the giving and returns nothing. | Whether anything received it, and whether the channel ever answered anyone. |
| 5 | `naelditheld` | **Naelditheld** | The seal-threshold: a door the Caleth built to be closed, standing open, with an antechamber in front of it instead of a chamber. | 26 | 26 | Arrives, reads what the door was built for, and performs the region's last and only act: they close it. They cannot open, break, bargain, or be answered. Nothing stands in front of the door to test them. | What is behind the door, what the closing does to it, and whether anything on the far side notices. |

**The escalation is subtraction, not addition.** Each zone removes one thing the player has relied on:
the map (1: the ground disagrees with it), the record (2: it is complete and silent on the one field
that matters), their own kit (3), their own voice (4), and finally the confirmation (5: they close the
door and nothing tells them it was the right thing to do). This is why a flat band is not a flaw (§5):
the difficulty curve of the Wound is *what the player may trust*, and that curve cannot be expressed in
HP. That is also pillar 2 satisfied at region scale — the removal is telegraphed in the zone that
precedes it, not sprung.

**Authoring constraint inherited from §1:** each zone needs exactly one `SafeRespawn` location
(`RespawnHubCommand.cs:178-183`), and each zone may ship with **empty** pools without throwing
(`ActorPoolResolver.cs:86-92`: no pool **and** nothing placed is an empty roster, not an error). Zone types: 1–3 `Wilderness`, 4–5 `Dungeon` (built interiors whose
cast is not a guardian, §5).

---

## 4. Naming and derivations

**Rule applied.** The interior has no Common oral tradition at all — no one lives there and no one
comes back — so the corruption pipeline of `calethic-language.md` §1.5 never ran inside the region.
The names reach the player through the one channel that survived the Collapse: **written records**.
They are therefore **intact Calethic**, and that is a fact about the region, not a stylistic choice.

The naming pattern is the Caleth's own land-survey: `[function root] + [type root]`, function first
(`calethic-language.md` §2.5). Three zones are **thresholds** (*raeth*, *theld*), two are **grounds** —
*Draekeld* by its suffix (*–eld*), *Orndaeld* by what the ground kept (*daeld*).
The repetition is deliberate and is itself pillar-1 content: the archive is legible in the names.

| slug | Calethic | roots | junction / rule | derivation | status | Canon/source |
|---|---|---|---|---|---|---|
| `vaelraeth` | **Vaelraeth** | *vaeld* (proximity, the approach, border-of-the-sacred) + *raeth* (threshold, boundary marker) | `–d + r–` → D-drop: *vaeld + raeth* → **Vaelraeth** (`:72`) | "the approach-threshold" — the marked point where the ordinary ends | **attested** — the root dictionary's worked example for *vaeld*, one form only (note below) | `calethic-language.md:283` (*vaeld*), junction table `:72`, note `:76-78`, §2.6 |
| `rektheld` | **Rektheld** | *rek* (administer, keep in place) + *theld* (controlled entry point, administered threshold) | `–k + TH–` = two phonemes, legal onset; syllabifies *rek.theld*; no reduction | "the administered threshold" — where arrivals were admitted, recorded and passed on | **coinage** — no attested word exists for the processing threshold; coined by §2.5 + §6 | `calethic-language.md:296` (*rek*), `:330` (*theld*), `:204-210` (§2.5), `:646-652` |
| `orndaeld` | **Orndaeld** | *orn* (unmake, dissolve a structure) + *daeld* (ash, what-remains-after-fire — the trace left when a structured thing is undone) | `–n + d–` is two consonants at the junction, legal; syllabifies *orn.daeld*, D– being a legal onset | "the remains of the unmaking" — the trace the unmaking left, which is why the wrongness sits in the ground and not in the act | **coinage** — no attested word exists for what an unmaking leaves behind; coined by §2.5 + §6 | `calethic-language.md:276` (*orn*), `:339` (*daeld*), root form *Orneth* `:418` |
| `draekeld` | **Draekeld** | *draek* (channel, medium, the interface) + *–eld* (ground, site) | `–k + e–`; syllabifies *drae.keld*; DR– is a legal onset (`:62`) | "the channel-ground" — the priestly interface, as a place | **coinage** — the dictionary's *draek* example is a role (*Draekori*), not a site | `calethic-language.md:282`, `:136` |
| `naelditheld` | **Naelditheld** | *naeld* (seal permanently, close without exit) + *theld* (controlled entry point) | `–ld + TH–`: D preserved, epenthetic `–i–` liaison inserted → *naeldi.th–* (`:74`); syllabifies *nael.di.theld* | "the sealed threshold" — a controlled entry point whose designed end-state is sealed, with no key | **coinage**, governed by an attested rule | `calethic-language.md:280` (*naeld*), `:330` (*theld*), junction row `:74`, worked example *Naeldithorn* |

**Spelling, settled in the authority.** *vaeld + raeth* is one word — **Vaelraeth**, the merged
proper name (`calethic-language.md:283`, §2.6), with D-drop at the junction (`:72`). The naming
authority now writes that form in the junction table too and records beneath the table that the
lower-case string was a rendering convention, not a second spelling; there is no register variant
(the administrative cell reads "same"). This spine cites the canonical form.

**Renamed 2026-11-20, and why the coin is a coin.** Zone 3 stood as **Orneld**, *orn* + *–eld*, which
is one letter from **Orveld**, the being's Calethic name (`calethic-language.md:277`, `:432`) — and a
zone that reads as a typo of the antagonist deflates the reveal the region is built on. The fix is not
a letter moved: the name is re-coined off a different second root, *daeld* (what a structured thing
leaves when it is undone) instead of *–eld* (site), and the sense moves with it — the survey marked
what the unmaking **left**, not the act of unmaking, which is also the only thing a surveyor could see
without answering §3's "why here". Measured at edit distance: **3 from *Orveld*** (7 from *Vaelraeth*
and from *Kael-Raeth*); no attested form in `calethic-language.md` is closer than 3 (*Dornkeld*,
*Korn-Keld*, *Orveld*) and no other zone name in §3 is closer than 4 (*Draekeld*). *(F2, card
`t_4e58e1ec`.)*

**The two literacies, at region scale (pillar 4).** The scholarly telling is five exact names and a
survey. The common telling is one noun — *the Wound* — and a refusal to name anything inside it. The
people of Skarhold, the nearest settlement and ten levels below it, do not say "Vaelraeth"; they say
*the marks*, *the counting house*, *the wrong ground*, *the door*. Common names things by the use it
has for the speaker (`calethic-language.md` §1.5, *Replacement*: *Korrveld* → the Droveway). The
disagreement pillar 4 requires is therefore not two names for one place but **whether there is an
inside at all** — and the player only ever learns the Calethic names, because they read them.

Consequence a coder must not undo: **the zone display names in the UI are the Calethic forms.** The
map changing language when the player crosses into the region is the point. The Common handles above
are prose and must never become `DisplayName`.

---

## 5. The endgame shape: a refusal the player performs

**Chosen: a refusal the player performs.** The region ends at a threshold that is whole and can be
closed. No confrontation resolves it and no discovery explains it; the player arrives, learns what the
door is for, and the last act the game asks of them is to shut it. They are not turned away by a door
sealed from the far side — the refusal is the act, and the act is theirs.

### Why not the other two

- **A final confrontation** requires something to fight, and the canon's central figure is not
  fightable: the being is "still here, diminished, partially contained — not gone"
  (`world-lore-plan.md:108`), and GAME.md §7 item 2 forbids "ancient evil awakening" as an event. Give
  it a health bar and the empire's mistake becomes a monster problem — and, worse, the player becomes
  the reason the world is safe again, which is exactly pillar 3's fails-when: *"A card makes the player
  the subject of a prophecy or the reason the world exists."*
- **A final discovery** turns the last region into exposition: the player reads the answer. It fails
  pillar 1's fails-when (*"A card explains the Caleth in a paragraph"*) and GAME.md §4's consequence
  clause — "no power fantasy of discovery — the player works out a history, they are not handed it."

### What the refusal is

Three facts, in order, and all three are already true of the canon:

1. The Caleth **fractured along self-preservation lines attempting to reseal the being**
   (`world-lore-plan.md:79`). A seal is therefore not a heroic artefact; it is the residue of an
   argument among people who lost.
2. The player **arrives from the place their own region pushed against** — the last room of the Maw —
   and finds the heartland open, with its last threshold unclosed.
3. The last act is the player's, and it is a refusal: at `naelditheld` the threshold can be closed,
   and closing it is the only thing the region will accept from them. `draekeld` accepts a gift and
   returns nothing; the door accepts a closing and returns nothing. Both verbs are recorded by the
   game and neither moves a number.

### What it costs the player, plainly

- **No final boss.** Their power curve ends where region 4's boss ended. The last region is spent, not
  won. The only thing they fight in it is what followed them in.
- **No answer to the question the arc taught them to ask.** The questions §6 marks stay unanswered
  *because* the player closes the door unopened — this is the one place where "we are not telling
  you" is the designed experience rather than a gap.
- **Nothing they can see changes.** They close a door they cannot see behind, and no voice, record or
  change in the room tells them the closing held or what it held back. The act is theirs; the world
  is not saved by it, and no one outside will believe it happened.
- **No rank that means anything outside the room.** If an achievement exists it is a record, not a
  status: pillar 3 does not allow the player to become large.
- **What they get instead:** the region-1 payoff inverted. They began at the edge of something older
  and ended at its centre, and cannot be believed by anyone who did not stand there — the scholar's
  telling is now theirs and unverifiable, while the common telling ("you don't go there") is the one
  their world actually holds.

### What it must never become

- A boss behind the door — pillar 3's fails-when.
- "The player was the seal all along", or any twist that makes them the reason the world exists — the
  same fails-when, wearing a hat.
- A lore dump delivered by the door, or by a survivor standing next to it — pillar 1's fails-when.
- A branch that grades the decision — rewards sealing, punishes walking away, or resolves the region
  differently on a second playthrough. The door can always be closed and closing it is the last act;
  nothing in the game keeps score of whether the player did.

---

## 6. What stays unanswered on purpose

Cross-referenced to the open items in `world-lore-plan.md:13-19` so a later writer cannot resolve them
by accident. **Items 2 and 4 are not resolved anywhere in this spine**; where the spine touches them,
the dependency is listed in §7 instead.

| id | The question | Cross-ref | A writer must not |
|---|---|---|---|
| O2 | Who the Vraen were, and whether they held anything beyond the frontier | open item **2** (`world-lore-plan.md:14`) | Name the wardens of the thresholds as the Vraen, or imply an order garrisoned the heartland. The Vraen are *frontier*-sworn (`calethic-language.md:476-484`); the heartland is not the frontier. |
| O4 | What came back up from the Pits (*Dornkeld*) | open item **4** (`world-lore-plan.md:16`) | Make the Pits a second route into the Wound, or state that they are not one. Whether the deep access point connects to the heartland is item 4 and stays open. |
| O3 | Whether any Aethori remain in the heartland | adjacent to open item **3** (`:15`), which is about *Varenmark* — this is a new question, not that one | Place a living, speaking Aethori in the region. The *Scattered / purposeless* tier "carrying old directives with no surviving context" (`world-lore-plan.md:88-93`, the tier itself at `:91`) — that is a hook, not a character. |
| O5 | What was tested in Grevenmire's Bone Hollow | open item **5** (`:17`) | Claim the heartland supplied, ordered, or received those subjects. Grevenmire's function is its own open item. |
| O6 | How Calethic recognition is threaded through the arc | open item **6** (`:18`) | Decide whether recognising a root is the whole mechanism or whether a trigger object (a record, a stone) delivers it. The spine assumes only the vocabulary layer, which the language pass already provides. |
| W1 | What is behind the door — the being's reduced presence, or something the Caleth put there | new | Answer it, in any zone, including zone 5 — the player closes the door without learning what is behind it. |
| W2 | Why the Caleth left the last threshold unclosed, and who argued for which side | new; adjacent to the locked fact at `world-lore-plan.md:79` | Attribute it to a named faction, a named person, or "the being itself" — the empire's fracture is enough. The player closes the door; they do not learn why it was left to them. |
| W3 | What was being processed in `rektheld` — arrivals, tribute, or people | new | Fill in the unlabelled field on the forms. It is the zone's one refusal and it borders open item 5. |
| W4 | Why the change settled where it did, and whether the region has an edge that moves | new | Answer it in zone 3. The locked magic fact says only *"In wound-adjacent areas: actively wrong"* (`world-lore-plan.md:114`) — adjacent, not bounded. |
| W5 | Whether the record the player leaves at the channel, or the closing they perform at the door, is legible to anything else | new | Confirm receipt. A confirmation turns an act of faith into a transaction and empties zones 4 and 5. |
| W6 | What the player's account of the door is worth to the outside world | new | Make it a quest reward, a title, or a reputation change. Its worth is that nobody believes it. |

---

## 7. Dependencies this spine cannot resolve

| # | Dependency | Why the spine cannot answer it | Consequence if it lands differently |
|---|---|---|---|
| 1 | **A 21–30 archetype roster** (the approved roster stops at 20 and says so) | This card may not author archetypes | The five zones are authorable with empty pools and nothing placed (`ActorPoolResolver.cs:86-92` — an empty roster, not an error) and unplayable as combat content. Zones 2–4 want levels 24–25, where **nothing** spawns today (§1). |
| 2 | **`IsInstanced = true` on the cleared `fire-ancients-chamber` room** | No seeder sets it, so the clear→unlock path never fires; the flag does nothing on the hidden gate node, which has no `ZoneInstanceId` to report | Without it the entry condition degrades to the level wall alone: a number, not an act. |
| 3 | **A server-side unlock source check** | `UnlockZoneLocation` trusts the client (`GameHub.cs:2695-2705`) | "Not reachable at level 1" holds only against an honest client (§2.2). |
| 4 | **`RegionType` gains a member** | Named entity decision, not a zone decision; free of migrations (`ApplicationDbContext.cs:130`) | The region row cannot be seeded correctly. |
| 5 | **The map graph for region 5** (a later card) | Out of scope by this card's rules | Nothing is reachable; the spine stays a spine. |
| 6 | **Open lore item 4** (what came back from the Pits) | Must not be resolved (§6 O4) | If it resolves toward a connection, §2.3 is superseded and the Wound acquires a second door — which would break the "one way in" shape §5 depends on. |
| 7 | **The `kaldreks-heart` `UnlockKey` fix** | Its `UnlockKey` equals its own slug (`MapGraphSeeder.cs:542`; `ZoneLocationsSeeder.cs`, the `kaldreks-heart` block), so the room can never unlock | K3 decided it is a bug, not content. The entrance is not built on it (§2.1), but the room stays unreachable until it is fixed. |

---

## 8. Out of scope for this card, deliberately

- **No locations.** Five zones, no sub-locations, no safe-respawn placements, no dungeon lobbies.
- **No map graph.** No nodes, edges, layouts, or `TargetMapId` wiring beyond the entry sketch in §2.1.
- **No NPCs**, no placements, no dialogue, no greetings, no quest hooks.
- **No archetypes**, no rosters, no spawn pools, no loot tables, no quests.
- **No numbers beyond the band**: no HP, damage, XP, gold or drop values. They come from
  `game-context/budget.yaml`, band `b23-26`.

---

## 9. Decisions

Taken by Josh, 2026-11-20 (K3, `t_cf8d3250`). The proposal listed five open choices; these are their
outcomes, and downstream content may now depend on them.

| # | Decision | Outcome |
|---|---|---|
| 1 | Endgame shape | **A refusal the player performs.** The last act is the player deciding to seal it — not being turned away at a door sealed from the far side. |
| 2 | Band | **Held inside `b23–26`**, difficulty carried by subtraction (each zone removes something the player trusts), not by stats. No ladder extension for the Wound. |
| 3 | Entry | **Approved** — a hidden location in `kaldrek-maw`, gated on clearing `fire-ancients-chamber`, backstopped by `DiscoverThreshold 23`. `IsInstanced` goes on the cleared `fire-ancients-chamber` room, whose `ZoneInstanceId` fires the clear→unlock loop — **not** on the hidden gate location, which carries no instance and flags nothing. |
| 4 | Region type | **A sixth `RegionType` member.** `Volcanic` belongs to Cinderplain; the name is the region wave's to pick. |
| 5 | Naming | **Five Calethic names approved**, four of them coinages. The `Vaelraeth` spelling is settled in the naming authority, and this spine cites it (§4). |

### Not landed here — the region's implementation wave

This card lands the spine and the naming correction and nothing else. The following belong to the
region's implementation wave, and no card may treat them as done:

- the sixth `RegionType` member (§2, §7 #4);
- `IsInstanced = true` on the cleared `fire-ancients-chamber` room, so the clear→unlock path fires
  (§2.1, §7 #2);
- the `kaldreks-heart` `UnlockKey` fix — self-referential, decided a bug rather than content (§7 #7);
- every zone row, location row and map-graph node for the five zones (§7 #5).

Still dependencies rather than deferrals: the 21–30 roster (§7 #1), the server-side unlock check
(§7 #3), and open lore item 4 (§7 #6).
