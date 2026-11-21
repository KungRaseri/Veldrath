# Veldrath — project context for content work

**Status: approved 2026-11-20.** §3 and §4 were decided the same day (proposal 9).

Read this before authoring anything. Every line below is sourced; anything the sources do
not contain is in **§8 TBD — needs approval** and is not written as fact.

## 1. What the game is

| | |
|---|---|
| **Engine** | RealmEngine — a framework-agnostic .NET RPG game-logic library. Every game operation is a MediatR command or query (`AGENTS.md`, "Project Overview"). |
| **Game** | Veldrath — "a multiplayer RPG with an ASP.NET Core server and a SvelteKit web app". Combat is turn-based (`AGENTS.md`; `.github/agent-memory/combat-loop-plan.md`). |
| **Genre** | Fantasy RPG. |
| **Subgenre** | Post-catastrophe fantasy: a world after the collapse of a magic-using empire, where magic no longer obeys anyone. Sourced from the locked world facts — "Since the Collapse: permanently unpredictable, behaves differently everywhere"; "No culture has re-established stable magical control" (`.github/agent-memory/world-lore-plan.md`, *Magic*). |
| **Starting position** | Region 1 of 5. Veldrath is not a chosen-one story: the arc is the player working out, by arc end, that the starting region was the edge of something much older (`world-lore-plan.md`, *Varenmark's Role*). |

## 2. Tone and register

- **The world is post-heroic.** Varenmark is farmland, drove roads and market towns; grandeur is something that happened *to* the place, and it shows in what people no longer do (`genre-fantasy` skill; `world-lore-plan.md`, *Varenmark's Role*: "Culturally far from the Wound; people here don't think of themselves as connected to the old empire").
- **Two vocabularies run side by side, always.** Scholars say *Ashen Age*, commoners say *Age of Reclaiming*; scholars say *the Unbound*, commoners say *the Ruin*; the era has *Kaelorneth* and *the Collapse*. The doubled naming is a locked feature, not a slip (`world-lore-plan.md`, *The Catastrophe*; `docs/lore/calethic-language.md` §5.1).
- **Language register is fixed.** Calethic is Hard + Formal: architectural consonants (K, R, TH, V, D); sibilants S and SH restricted to Aethori ceremonial speech only; no /u/ in the inventory; no S–/SH– word-initial (`calethic-language.md` §1.1–1.3, §3.1).
- **Place names decay.** Calethic names survive in three states only: intact where keepers remained (*Aethori*), corrupted by oral transmission (*Aetheld* → *Ashlen*; *Kaeldor* → *Halrow*), or replaced entirely by Common description (*Korrveld* → *the Droveway*) (`calethic-language.md` §1.5, §5.2).
- **Existing content speaks plainly.** Seeded archetypes, quests and locations are named in Common and describe behaviour, not mood: "Giant Rat", "Fen Lurker", "Gull-Rider Brute", "Defeat 5 Goblin Scouts on the Ashlen Wood roads" (`RealmEngine.Data/Seeders/ArchetypeSeeder.cs`, `QuestsSeeder.cs`).
- **The standard and the pillars are editorial.** §3's content standard and §4's pillars bind what content may say and how it is designed. They do not describe or constrain engine behaviour; the four engineering commitments in §4 are a separate list and are not pillars.

## 3. Content rating and what it forbids

**Teen-equivalent, self-imposed.** Veldrath is web-first with no store release, so this is an
editorial standard for writers, not a platform gate. The permitted and forbidden lists below are the
operative artefact; the rating letter is shorthand.

*Decided by Josh, 2026-11-20 — "Proposal 9 — decided by Josh" on card `t_5cd22693`.*

**Permitted.** Blood, corpses, dread, coercion, drowning, implied atrocity, and body horror
described by consequence.

**Written as testimony and aftermath, never as scene.** Two subjects in this world stay facts that
people live under and speak around:

- the **Aethori bloodline compulsion** — slavery by design;
- **whatever was tested in the Bone Hollow**.

Neither is a set piece.

**Not permitted.** Lingering gore · torture as spectacle · sexual content · on-screen harm to
children · addiction mechanics · real-world slurs.

**Language stays period-plain: no modern profanity.**

The standard fits the canon rather than cutting against it: the Collapse is already described by
consequence rather than by violence, and nothing in `docs/lore/calethic-language.md` — the only lore
file this worktree can read — lingers on an act of violence.

## 4. Design pillars

Four content pillars, each written as a test — a pillar that forbids nothing is decoration.

*Decided by Josh, 2026-11-20 — "Proposal 9 — decided by Josh" on card `t_5cd22693`.*

| # | Pillar | Fails when |
|---|---|---|
| 1 | **The world remembers; nobody explains it.** History shows in objects, names, habits and what a town steps over. | A card explains the Caleth in a paragraph instead of showing a slab people avoid. |
| 2 | **Consequence is legible before it lands.** Danger and cost are readable in advance — pool composition, level bands (`game-context/budget.yaml`), the death penalty surfaced to the client as `goldLost` / `xpLost`. | A card kills the player by surprise with no signal. |
| 3 | **Small people, large aftermath.** Grandeur is landscape and history; the player is a person, not a fulfilment. | A card makes the player the subject of a prophecy or the reason the world exists. |
| 4 | **Two literacies.** Every important fact has a scholarly telling and a common one, and they disagree about what it means. | A card states a fact once, in one register, as settled. |

Pillars 1–3 are evidenced by shipped behaviour rather than asserted: the failsafe rule ("degraded
behaviour is allowed only when it is loud"), the client being told `goldLost`/`xpLost`, rosters that
persist per location, and the doubled naming already locked in `world-lore-plan.md`. Pillar 4 is the
doubled-naming convention promoted from a style note to a rule — §2 states the same convention as
register.

Consequence for canon: no named deities (§7.3 already forbids them, so the shrine stays "the Ash
Shrine"), no prophecy device (§7.1), and no power fantasy of discovery — the player works out a
history, they are not handed it.

**The four *engineering* commitments below are sourced and constrain content, but they are not the
design pillars above, and they are not renumbered by them:**

1. Engine agnosticism — `RealmEngine.Core`, `.Shared`, `.Data` never depend on a UI framework (`AGENTS.md`, Hard Rule 2).
2. Web-first, one client — the SvelteKit app is the only client and the only editing surface; the Avalonia client and RealmForge editor were retired (`AGENTS.md`).
3. Content is data, edited in Foundry — Foundry inside the SvelteKit app is the only content editor; seeders are insert-only (`plans/content-pipeline.md` §1).
4. Turn-based combat and two ways to place a combat actor — a weighted pool entry or a named `ActorInstance`; a pool entry is always an attackable enemy, and NPCs are never in a pool (`ZoneLocationsSeeder.cs`, *SpawnPools* remarks).

## 5. Canon entry points

| Path | What it holds | Status in this worktree |
|---|---|---|
| `.github/agent-memory/world-lore-plan.md` | The locked world facts: the Collapse, the Caleth, the Aethori, the Vraen, the Unbound, magic, the five-region spine, and the 6 open items (numbered 2–7 in the plan; item 1 was resolved 2026-04-15) | present, read-only |
| `docs/lore/calethic-language.md` | The Calethic language: phonology, morphology, syntax, ~75-root dictionary, full named-entity pass. **The naming authority.** | present, read-only |
| `wiki/World-Veldrath.md`, `wiki/Region-Varenmark.md`, six Varenmark zone pages, `wiki/Calethic-Language.md` | Player-facing lore, two-tier (general layer + spoiler block) | **not present** — `wiki/` is a git submodule and is not checked out here. The list is taken from `world-lore-plan.md`'s own record of completed phases, not from reading the pages. |
| `RealmEngine.Data/Seeders/` | The shipped catalogue: 19 zones, 67 locations, 17 archetypes, 2 organisations, items, loot, quests, dialogue | present |
| `.github/agent-memory/veldrath-memory.md`, `engine-codebase.md`, `foundry-content-schema.md` | Server/client architecture, content schema, write endpoints | present |
| `plans/content-pipeline.md` | Validators, write path, the slug rule `^[a-z0-9]+(-[a-z0-9]+)*$` | present |

## 6. Regions

Five regions, one spine: **Varenmark → Greymoor → Saltcliff → Cinderplain → the Wound**
(`world-lore-plan.md`, *World Structure*). Region 2–4 names are **working placeholders**, locked
only in Phase 5+ (`world-lore-plan.md` Q2).

| Region | Seeder id | One line | Source |
|---|---|---|---|
| **Varenmark** | `varenmark` | Region 1 and the starting area: farmland, drove roads and market towns, sitting on Calethic experimental ground the locals do not recognise. Zones: `crestfall`, `the-droveway`, `ashlen-wood`, `grevenmire`, `the-halrow`, `drowning-pits`. | `world-lore-plan.md` *Varenmark's Role*; `MapGraphSeeder.cs` |
| **Greymoor** | `greymoor` | Working name. Highland region reached east from Varenmark: `aldenmere` (a waytown), `pale-moor` (fog moor, waymarks that shift), `soddenfen` (waterlogged fen, half-submerged ruins), `barrow-deeps` (burial network cut through the moorland). Character beyond that: undecided (Q2). | `MapGraphSeeder.cs`; Q2 |
| **Saltcliff** | `saltcliff` | Working name. Coastal region: `tolvaren` (port city), `tidewrack-flats` (tide-scoured mudflats), `saltcliff-heights` (clifftops, lighthouse ruins, rival gull-rider clans), `sunken-name` (a drowned ruin whose name is lost). Character beyond that: undecided (Q2). | `MapGraphSeeder.cs`; Q2 |
| **Cinderplain** | `cinderplain` | Working name. Volcanic region: `skarhold` (forge-city in a caldera), `ashfields` (ash plains), `smoldering-reach` (cracked lava field, gas vents), `kaldrek-maw` (vent complex). Character beyond that: undecided (Q2). | `MapGraphSeeder.cs`; Q2 |
| **The Wound** | — | Region 5, endgame. The former Calethic heartland, Calethic *Kaeltheld*, where the being retreated. **No zones, no locations and no map graph are seeded.** | `world-lore-plan.md` *World Structure*; absent from `MapGraphSeeder.cs` |

`playground` is a nineteenth seeded zone and is **dev-only** (`IsDevOnly`); it is never returned by
public APIs and never gets content.

## 7. Forbidden tropes — derived from what this canon actually does

1. **No chosen-one framing and no prophecy device.** The arc is a player working out a history, not a destiny being fulfilled (`world-lore-plan.md` *Varenmark's Role*).
2. **No "ancient evil awakening" as an event.** The being is not sleeping: it is "still here, diminished, partially contained — not gone". Content describes a condition, not a stirring (`world-lore-plan.md` *The Being*).
3. **No named gods, pantheons or temples to invented deities.** Canon religion is institutional, not devotional: the Aethori are a priestly *office* inside an empire, and no deity is named anywhere in the corpus. A god requires a canon proposal.
4. **No race-as-alignment.** Player species include elf, dwarf, halfling and gnome (`ActorSeeder.cs`); canon divisions are institutional — the Caleth (an empire), the Aethori (tiers of an office), the Vraen (a military designation). Never give a species a moral character.
5. **No stable, learnable, reliable spellcraft.** Magic is "permanently unpredictable", "actively wrong" near the Wound, and nobody has regained control (`world-lore-plan.md` *Magic*). A reliable spell economy contradicts a locked fact.
6. **No pseudo-Tolkien phonology in coined names.** No apostrophes; no `-iel`/`-dor` at whim; no S or SH in functional Calethic; no /u/ (`calethic-language.md` §1, §6).
7. **No empire-as-sympathetic.** The Caleth are locked as "single tyrannical civilisation — conquerors and priest-kings" (`world-lore-plan.md`). Do not soften them into a lost golden age.
8. **No fourth-wall address, no modern idiom, no irony at the player's expense.** House rule from the content-authoring and genre-fantasy skills; **not canon-sourced**, treat as binding until a human locks something different.

## 8. TBD — needs approval

| # | Gap | Why it blocks content | Where it would be recorded |
|---|---|---|---|
| 1 | **Greymoor / Saltcliff / Cinderplain character** (Q2) | Only zones and biomes exist; region prose cannot be written | `wiki/Region-*.md`, Phase 5+ |
| 2 | **The Wound's zone map** | Region 5 has no zones or locations at all; nothing can be placed there | `MapGraphSeeder.cs` (a later card) |
| 3 | **Whether the wiki pages listed in §5 exist as recorded** | The submodule is not checked out in this worktree, so the paths are attested, not verified | — |

Two gaps this section used to carry — the content rating and the four design pillars — were decided
2026-11-20 and are published in §3 and §4.

Three of the six open lore items (the Vraen, Aethori presence, what came back up from the Pits)
are untouched by this file; the archetype roster on card **t_5cd22693** raises them as numbered
proposals rather than assuming answers.
