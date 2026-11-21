# Veldrath — archetype visual system

**Status: approved 2026-11-20.** Approved by Josh; the decision record is [§8](#8-decisions). This
supersedes the draft of the same date: every rule, ramp and class below is the approved text, and the
only changes made on landing are the mechanical consequences of the five decisions — listed, not
silently applied, in [§8](#8-decisions).

> **Audience:** whoever draws the portraits (`dev-creative`) and whoever accepts them.
> **Purpose:** make 34 roster rows look like one world rather than 34 commissions — a silhouette
> language, a per-region palette, and one asset class per row, expressed inside the budgets
> [`asset-specs.md`](../asset-specs.md) already sets.
> **Related:** [archetype roster](archetypes.md) (the 34 rows) · [brand guide](../brand-guide.md)
> (materials and light) · [asset specs](../asset-specs.md) (canvas, format, budget, naming) ·
> [tokens.json](../tokens.json) (the palette, `locked: true`).

---

## 0. What binds this document, and what it decides

| Source | What it fixes here | This document |
|---|---|---|
| [`asset-specs.md`](../asset-specs.md) | canvas sizes, formats, weight budgets, naming | quotes them, never re-decides them |
| [`brand-guide.md`](../brand-guide.md) | three materials, light from within and above, **no off-palette hue** | applies them per region |
| [`tokens.json`](../tokens.json) | `locked: true` — 49 tokens, no new colour without a human decision | uses 16 existing tokens, adds **none** |
| [`archetypes.md`](archetypes.md) | 34 rows, slugs, zones, classes, faction ties | classifies every row, invents no slug |
| [`GAME.md`](../../../game-context/GAME.md) §6, §8 | five regions; region *character* beyond biome is **TBD (Q2)** | ramps from seeded biome words only |

`designctx Veldrath/Veldrath` reports convention `docs/design`, 49 palette tokens, palette matching
this document's citations. Context is **bound**.

This document decides four things the sources do not: **the silhouette rules**, **the five region
ramps**, **P0/P1/P2 per row**, and **the shape of one bulk request**. Everything else is quoted.

---

## 1. The surface does not exist yet — read this before anything else

An asset with no surface does not ship ([asset-specs §6 step 1](../asset-specs.md)). Checked in this
worktree:

| Question | Evidence | Answer |
|---|---|---|
| Does an archetype entity carry an image path? | `RealmEngine.Data/Entities/ContentBase.cs` — `Id, Slug, TypeKey, DisplayName, RarityWeight, IsActive, Version, UpdatedAt`. `ActorArchetype` adds stats, traits, loot. | **No.** Nothing maps `giant-rat` to a file. |
| Does any screen draw an enemy portrait? | `web/client/src/lib/game/CombatPanel.svelte` renders `enemy.name`, `Level {enemy.level}`, a health bar and an ability list as text. | **No.** Text only. |
| Is there a precedent for art with no data column? | `web/client/src/lib/game/item-icons.ts` maps item type → Lucide glyph; [asset-specs §4.7](../asset-specs.md) requires that fallback. | **Yes** — a client-side slug map, not a schema column. |

So the art is `GENERATE` against a surface that is still three small engineering artifacts away:
a slug-keyed lookup (or a convention that the filename *is* the key), one `ArchetypePortrait`
component, and a Lucide fallback by class. **None of that is this card's work**, and none of it
blocks this document — but it does block the first delivery, and the tile contract has to be
written down now or the art is produced against a guess.

### The tile contract (proposed; the component must match it)

| Property | Value | Source |
|---|---|---|
| Rendered box | 64 × 64, square, `object-fit: contain` | this document (§2 R1: the silhouette must read at 64 px) |
| Export | 128 px (2×) and 64 px (1×) | asset-specs §4 preamble |
| Backdrop | `--vds-bg-2` #1c1d2b — the same cell colour item art sits on | asset-specs §4.7 |
| Frame | drawn by the component per R3, never in the art | this document §2 |
| Fallback | Lucide glyph by class (`PawPrint` beast, `Swords` humanoid, `Ghost` unquiet, `Flame` spirit, `UserRound` townsfolk) | asset-specs §4.7 |

### Measured: the tile behind the art matters more than the silhouette does

Contrast of every usable token against the `bg-2` tile it sits on, measured with
`tools/portrait_check.py`'s own luminance function:

| Token family | Values | vs `#1c1d2b` | Consequence |
|---|---|---|---|
| `background/*` | #080910 – #2e2f42 | **1.00–1.27 : 1** | a steel silhouette on its own **does not read** against the tile |
| `border/*` | #2a2b3d – #3d3f58 | 1.20–1.63 : 1 | same |
| `gold/gold-dark` … `gold-light` | #a68a2e – #d4b96a | **5.00–8.69 : 1** | the metal is what makes the shape legible |
| `seal/seal-dark` | #8c2018 | **1.85 : 1** | seam only, never a fill |
| `seal/seal` … `seal-light` | #c0392b – #e05545 | 3.06–4.41 : 1 | fills and light |
| `ember/ember-dark` … `ember-light` | #943018 – #e05e35 | 2.14–4.62 : 1 | `ember-dark` is only just legal |
| `text/text-subtle` … `text` | #6e6860 – #f0ede8 | 3.03–14.27 : 1 | bone and salt material |

Two consequences, both built into the rules below:

1. **The read comes from the lit mass, not the fill.** The gate measures the 75th-percentile
   luminance of the opaque core, not the mean — a dark figure with a gold rim is legible, a
   uniformly mid-grey figure is mud. This is the crest's own logic ("gold rim light on every edge,
   no ambient fill", brand-guide §1.1).
2. **The brand-guide's 60–70 % steel ratio cannot be the legible channel at 64 px.** It is the
   ground the figure sits in; the gold rim and the emissive core carry the shape. The ramps in §3
   put steel *behind* the silhouette, not in the outline.

---

## 2. The silhouette language — four rules

Every rule is checkable on the 64 px export. `tools/portrait_check.py` automates all four; its
self-test (9 synthetic fixtures, no generated art) is the proof it discriminates — see §2.5.

### R1 — Proportion and facing are set by class, and there are only five classes

`archetypes.md` already partitions the roster by combat role; `ArchetypeSeeder.cs` partitions it by
`TypeKey` (`beasts`, `humanoids`, `undead`, `spirits`, `elementals`, `townspeople`). Five silhouette
classes cover all of it, and the fifth (townsfolk) is the one the seeder gets exactly right.

| Class | Proportion | Facing | Opaque coverage at 64 px | Rows |
|---|---|---|---|---|
| **beast** | horizontal, mass low, longer axis ≥ 1.15× the vertical | profile, head leading **left** | 0.22–0.55 | 11 |
| **humanoid** | vertical, shoulders 0.60–0.75 of width, head in the top third | three-quarter, off-centre | 0.22–0.55 | 8 |
| **unquiet** | vertical but **no lower mass** — the figure frays out above the bottom margin | three-quarter, tilted | 0.22–0.55 | 4 |
| **spirit** | one non-limbed mass, no neck, no narrow waist (elementals share this class) | arbitrary, never symmetrical | 0.22–0.55 | 4 |
| **townsfolk** | vertical, square shoulders, head centred | **front-on and mirror-symmetric** | 0.22–0.55 | 7 |

**Facing is the hostility read.** Hostiles are three-quarter (symmetry ≤ 0.85); townsfolk are
front-on (symmetry ≥ 0.88). A player learns it in one screen: *front-on means talk, three-quarter
means fight.* It is also the cheapest rule to enforce, because a mirror-overlap of the alpha mask
measures it exactly.

The subject fills 80–92 % of the canvas on its longer axis and never less than 45 % on the shorter
one — asset-specs §4.7's "~85 %", given a floor so a thin creature is not a smear at 64 px.

### R2 — Exactly one outline break, at a bearing fixed by class

Not "add something interesting": one protrusion, in one sector, and nothing else may compete with
it. Bearing is measured as the direction of the outline's farthest point from the mask centroid,
0° = right, 90° = up.

| Class | Break | Sector | What it is |
|---|---|---|---|
| beast | head or snout, forward | **150°–210°** | the animal is going somewhere |
| humanoid | the held weapon | **20°–70°** | the weapon is the threat statement |
| unquiet | the shroud or tatter trailing down | **250°–290°** | it is being pulled under |
| spirit | column, plume or funnel | **70°–110°** | it rises; it has no feet |
| townsfolk | **none** — a closed outline | symmetry ≥ 0.88 | a closed shape reads as safe |

A second protrusion (a tail plus a snout, a blade plus a raised fist) is a review failure, not a
flourish: at 64 px two breaks average into a blob.

### R3 — Threat tier is the frame, not the paint

One portrait per slug, whatever its tier. The UI draws the tier:

| Tier | Frame | Rows today |
|---|---|---|
| boss | double gold ring plus four cardinal spikes — the crest's own frame language | `bandit-captain` (`Traits.Boss = true`) |
| elite | single 1.5 px `gold/gold-dark` ring | `storm-harrower` (`Traits.Elite = true`) |
| regular | no ring | 25 hostile rows |
| townsfolk | no ring, `background/bg-1` plate behind the bust | 7 rows |

This follows the precedent [asset-specs §4.7](../asset-specs.md) sets for item rarity — *"Not drawn
into the art. Rarity is a text colour and a future UI frame — art stays neutral so it can be
re-tiered without a redraw."* It also matters here: `budget.yaml` records that `Boss`/`Elite` are
inert labels with no stat effect yet, so the tier of a row may still change. A portrait that draws
tier would be wrong the moment a band moves.

### R4 — A faction mark only where a faction tie exists

Two rows have one: `gull-rider-skirmisher` and `gull-rider-brute`. Their mark is **the hooked
harpoon crossing the top-right break, plus up-draught tension at 12 o'clock** (rope and hair pulled
upward). That is the whole faction vocabulary.

The other 25 hostile rows are `none recorded` — 23 of them explicitly "no organisation". The roster
states that as a finding, not an omission (`archetypes.md`, "Only two rows tie to a real faction").
**No badge, crest, colour or motif is invented for a factionless row**, because a badge asserts
canon the roster withheld pending proposals 6–7.

### 2.5 The gate, and the proof it gates

`tools/portrait_check.py` (in `/home/kung/creative-assets/veldrath/tools/`, run against the 64 px
export):

| Check | Rule | Threshold |
|---|---|---|
| coverage | R1 | opaque fraction 0.22–0.55 |
| bbox | R1 | longer axis 0.80–0.92, shorter ≥ 0.45 |
| bearing | R2 | inside the class sector |
| facing | R1 | symmetry ≤ 0.85 hostile / ≥ 0.88 townsfolk |
| contrast | §1 | 75th-percentile core luminance ≥ 2.0:1 vs `bg-2` |
| alpha ring | asset-specs §2 | no soft-alpha pixel in the outer 1 px margin |

`tools/selftest_portrait_check.py` builds nine synthetic silhouettes — five conforming, four
deliberately broken (a slab that fills the frame, a beast with its snout on the wrong side, a
portrait with a baked glow, a front-on enemy) — and asserts the verdict. Last run:

```
ok   beast-ok         ok   humanoid-ok     ok   unquiet-ok
ok   spirit-ok        ok   townsfolk-ok
ok   slab             ok   beast-wrong-facing
ok   baked-glow       ok   front-on-enemy
9/9 cases behaved as specified
```

The gate found two real problems in the ramps before an artist saw them:
`seal/seal-dark` at 1.85:1 (Saltcliff rust may be a seam, never a mass) and every `background/*`
value at ≤1.27:1 (see §1).

**What the gate cannot do:** judge material, texture, or whether the thing looks like a rat. Those
are the review's job, against §2's rules and the prompt in §5.

---

## 3. Region palettes

**A region ramp is not an identification channel.** A player identifies hostiles by silhouette and
tier frame (R1–R3). The ramp exists so that 34 portraits look lit in the same world, and so that
the same archetype appearing in two regions is re-graded rather than redrawn.

Every value below is a token from `tokens.json`, cited by its own name. **No new colour token is
required, and none is proposed.** The five ramps are five different *selections and placements*
from the same 16 tokens — which is exactly what a locked palette should produce.

### 3.1 Varenmark — lantern, dry stone, dust

*Seeded words: dry-stone walls, pastures gone to seed, warm lanterns, merchant stalls, half-sunken
ruins, fog that does not lift (`MapGraphSeeder.cs` zone prose).*

| Role | Token | Hex | Use |
|---|---|---|---|
| ground | `background/bg-1` | #14151f | the figure's shadow side, the room it stands in |
| stone | `border/border-strong` | #3d3f58 | dry-stone wall, haft, cold iron |
| metal | `gold/gold-dark` | #a68a2e | brass, tool steel, worn gilding |
| rim | `gold/gold-light` | #d4b96a | the lantern key and every top edge |
| ember | `seal/seal-muted` | #3d1a16 | the only crimson: a fire under a pot, a cut, cloth in shadow |

**Light:** warm, low, at chest height — a lantern on a hook, not a sun. Ratio 60 % ground / 30 %
gold family / 10 % ember-as-light, which is the brand guide's own ratio unchanged.

### 3.2 Greymoor — fog, wet stone, bone

*Seeded words: fog-blanketed moors, waymarks that shift, grey stone, waterlogged fen, burial network
under the moorland.*

| Role | Token | Hex | Use |
|---|---|---|---|
| ground | `background/bg-2` | #1c1d2b | the fog depth |
| fog | `background/bg-4` | #2e2f42 | the *lightest* value in the region — fog is the light source here |
| heather | `text/text-subtle` | #6e6860 | dead-bronze scrub; **never purple** |
| dead gilding | `gold/gold-dark` | #a68a2e | gilding that has oxidised; no `gold-light` in this region |
| bone | `text/text-muted` | #a8a09a | the unquiet dead's only light, at the sternum |

**Light:** cold and internal — a bone-white core inside the chest. Gold appears only as oxidised
metal on buckles and grave-goods. **This is a deliberate deviation from brand-guide §5's "gold rim
plus crimson core"** — approved as decision 2, and recorded in `brand-guide.md` §5 itself so the
guide and the art cannot contradict each other.

### 3.3 Saltcliff — salt, storm, rust

*Seeded words: wind-blasted clifftops, lighthouse ruins, salt-caked port, tide-scoured mudflats,
shipwreck timber, kelp-draped bones, a flooded ruin.*

| Role | Token | Hex | Use |
|---|---|---|---|
| ground | `background/bg-0` | #0c0d13 | sea depth, the inside of a wreck |
| storm rock | `background/bg-3` | #262736 | wet basalt, sailcloth, storm sky |
| salt | `text/text-muted` | #a8a09a | salt bloom, bleached timber — a *material* here, not a light |
| rust | `seal/seal-dark` | #8c2018 | **seam only** (1.85:1 — never a mass) |
| brass | `gold/gold-dark` | #a68a2e | salvage fittings; nothing here is polished |
| light | `seal/seal` | #c0392b | the drowned empire's glow, from *below* the waterline |

**Light:** from underneath and behind — the only region where the key sits below the figure's
midline. That inversion is what makes a Saltcliff portrait identifiable as coast at a glance without
a single blue pixel.

### 3.4 Cinderplain — cinder, obsidian, forge

*Seeded words: ash plains to the horizon, fused obsidian trees, scorched skulls, a caldera
forge-city under sulphur skies, a cracked lava field, gas vents.*

| Role | Token | Hex | Use |
|---|---|---|---|
| ground | `background/bg-deeper` | #080910 | the darkest canvas in the set |
| obsidian | `background/bg-2` | #1c1d2b | glassy mass, cooled slag |
| ash | `ember/ember-muted` | #361a10 | the mid-ground dust |
| seam | `ember/ember-dark` | #943018 | cooled lava in fissures (2.14:1 — seam, not fill) |
| light | `ember/ember-light` | #e05e35 | internal fissures; the brightest emissive of the five |
| forge | `gold/gold-muted` | #3d3216 | work-hardened gilding, soot in the engraving |

**Light:** internal and hot, at the highest intensity in the set. Ratio 65 % ground / 20 % ember /
15 % gold-muted. Only region where `ember-light` is used.

### 3.5 The Wound — no metal left

*One locked fact, no zones: the former Calethic heartland, where magic is "actively wrong"
(`GAME.md` §6, `world-lore-plan.md`). Nothing is seeded there.*

| Role | Token | Hex | Use |
|---|---|---|---|
| ground | `background/bg-deeper` | #080910 | |
| fracture | `background/bg-1` | #14151f | |
| wound | `seal/seal-dark` | #8c2018 | |
| light | `seal/seal-light` | #e05545 | the only place the brightest seal runs unbroken |
| fracture light | `text/text` | #f0ede8 | bone-white break, not a glow |

**The statement of the region is the absence of gold.** Four regions light with metal; the fifth is
where nothing is forged any more. **Provisional:** no consumer exists, and the character of the
region is undecided (Q2). This ramp is written down so the next card does not invent a sixth
palette, and it should be re-decided when the Wound gets zones.

### 3.6 New tokens

| Asked | Answer |
|---|---|
| Which token names must be added? | **None.** All five ramps use 16 existing values. |
| Optional, for Josh | `region/varenmark`, `region/greymoor`, `region/saltcliff`, `region/cinderplain`, `region/wound` — five **UI** tokens (each the ramp's metal stop) for map tinting and chapter headers. The prompt asked for these only if they are new; as art values they are not needed. Adding them means a `docs/design/specs/` decision and a `theme.css` + `tokens.json` + `design-system.md` triple edit, per the file's own `$comment`. |

---

## 4. Asset classes

| Class | Meaning | Spec class ([README](../specs/README.md)) | Cost |
|---|---|---|---|
| **P0** | bespoke art now — the row is reachable in Varenmark, wave A1 | `GENERATE` | 1 generation |
| **P1** | art before its zone ships — a new shape decision, no existing base | `GENERATE` | 1 generation |
| **P2** | variant: **only what the figure holds and which region light it is graded to change** from an existing row | `GENERATE` anchored on the base | 1 generation, no design pass |

### The P2 test (applied, not waved at)

> If it needs a different body plan — quadruped ↔ biped, tall ↔ low, mass redistributed — it is
> **P1**, not P2. If it is the same body with a different held object and a re-grade, it is P2.

This is why `barrow-bear` is P1 and not a recoloured `mire-boar` (a bear is not a fat boar), and why
`bog-lantern` is P1 and not a recoloured `cinderling` (a column is not a knot). P2 is **not** a
pixel derivation: the pipeline has no rig and no layered source, so P2 means *one generation
anchored on the base's pose and scaffold*, which saves the design decision and the anchor review —
not the generation.

### 4.1 The roster, classified

Home region = the first zone the row belongs to in `archetypes.md`. A row that spans regions
(`moor-wight`, `drowned-revenant`, `storm-harrower`, both gull-riders) is graded to its home region
and **re-lit, never redrawn**, in the others.

| slug | band | home region | class | tier flag | asset class | why |
|---|---|---|---|---|---|---|
| `giant-rat` | 1–3 | varenmark | beast | — | **P0** | Varenmark opening pool enemy |
| `goblin-scout` | 1–3 | varenmark | humanoid | — | **P0** | Varenmark opening pool enemy |
| `drove-cur` | 1–3 | varenmark | beast | — | **P0** | Varenmark opening pool enemy |
| `peat-adder` | 1–3 | varenmark | beast | — | **P0** | Varenmark opening pool enemy |
| `forest-wolf` | 4–6 | varenmark | beast | — | **P0** | Varenmark wood-road pack enemy |
| `bandit-ruffian` | 4–6 | varenmark | humanoid | — | **P0** | Varenmark road-and-ruin enemy |
| `sump-crawler` | 4–6 | varenmark | beast | — | **P0** | Varenmark dungeon ambusher |
| `mire-boar` | 4–6 | varenmark | beast | — | **P0** | Varenmark bog bruiser |
| `moor-wight` | 7–9 | varenmark | unquiet | — | **P0** | Grevenmire caster; also Greymoor's base |
| `village-blacksmith` | — | varenmark | townsfolk | npc | **P1** | Crestfall shop; first townsfolk anchor |
| `tavern-keeper` | — | varenmark | townsfolk | npc | **P1** | Crestfall inn; every town's first face |
| `wayfarer-elder` | — | varenmark | townsfolk | npc | **P1** | Droveway quest hook |
| `market-factor` | — | varenmark | townsfolk | npc | **P1** | Trade surface, four towns |
| `gate-watch` | — | varenmark | townsfolk | npc | **P1** | Crestfall gate, toll check |
| `bandit-captain` | 7–9 | greymoor | humanoid | boss | **P1** | only boss in the roster; banditry anchor |
| `barrow-bear` | 7–9 | greymoor | beast | — | **P1** | a bear is not a boar; new body plan |
| `moor-tollman` | 7–9 | greymoor | humanoid | — | **P2** | ruffian kit, bow instead of blade |
| `cairn-haunt` | 10–12 | greymoor | unquiet | — | **P2** | moor-wight's standing-dead pose, stilled |
| `fen-lurker` | 10–12 | greymoor | beast | — | **P2** | sump-crawler's waterline pose, new water |
| `burrow-worm` | 10–12 | greymoor | beast | — | **P1** | limbless vertical mass, no parent shape |
| `bog-lantern` | 10–12 | greymoor | spirit | — | **P1** | a column is not a knot; new mass |
| `drowned-revenant` | 13–15 | greymoor | unquiet | — | **P1** | the drowned-dead base; spans two regions |
| `tide-strider` | 13–15 | saltcliff | humanoid | — | **P1** | long-legged wader; new proportion |
| `gull-rider-skirmisher` | 13–15 | saltcliff | humanoid | — | **P1** | faction anchor; R4's harpoon |
| `gull-rider-brute` | 16–18 | saltcliff | humanoid | — | **P2** | skirmisher kit, heavier build |
| `storm-harrower` | 16–18 | saltcliff | spirit | elite | **P1** | weather-form; no parent shape |
| `cliff-breaker` | 16–18 | saltcliff | beast | — | **P1** | winged artillery; new mass |
| `undertow` | 16–18 | saltcliff | spirit | — | **P1** | a current, not a body — see §6 item 7 |
| `drowned-cantor` | 19–20 | saltcliff | unquiet | — | **P2** | revenant's corpse, robed and singing |
| `dock-hand` | — | saltcliff | townsfolk | npc | **P2** | factor's civilian pose, dock kit |
| `cinderling` | 19–20 | cinderplain | spirit | — | **P1** | Cinderplain anchor; new emissive mass |
| `ash-jack` | 19–20 | cinderplain | beast | — | **P1** | a crow; no parent shape |
| `forge-deserter` | 19–20 | cinderplain | humanoid | — | **P1** | the forge kit *is* the identity |
| `shrine-keeper` | — | cinderplain | townsfolk | npc | **P2** | elder's robed pose, ash dedication |

**Counts: P0 9 · P1 18 · P2 7 = 34.** By region: Varenmark 14 (9 P0 / 5 P1), Greymoor 8 (5 P1 / 3 P2),
Saltcliff 8 (5 P1 / 3 P2), Cinderplain 4 (3 P1 / 1 P2). The five Varenmark townsfolk are P1 by
decision 3 ([§8](#8-decisions)) — the draft had them P0; nothing else in this table moved.

**P2 dependencies** — a P2 row cannot be requested before its base exists: `moor-tollman` ←
`bandit-ruffian` (P0) · `cairn-haunt` ← `moor-wight` (P0) · `fen-lurker` ← `sump-crawler` (P0) ·
`gull-rider-brute` ← `gull-rider-skirmisher` (P1) · `drowned-cantor` ← `drowned-revenant` (P1) ·
`dock-hand` ← `market-factor` (P1) · `shrine-keeper` ← `wayfarer-elder` (P1).

**Not yet in this table:** D3's card approved ten townspeople archetypes for `towns.md` which will be
absorbed into `archetypes.md` by a follow-up card. They are not roster rows yet, so they are not
classified. When they land, the same rule applies — a townsperson with a new held object is P2, one
with a new body plan is P1 — and this table gains ten rows. Expect 6 P2 / 4 P1 at a guess, and do
not quote that guess as a plan.

### 4.2 One correction to the seeder, not a visual decision

`goblin-scout` is seeded with `TypeKey = "beasts"` while it is a biped with a spear
(`ArchetypeSeeder.cs`). Its **visual** class is humanoid regardless of the TypeKey, because the
TypeKey is a content-taxonomy field, not a drawing instruction. Flagged for whoever owns the
seeder; this document does not rename anything.

---

## 5. The bulk brief

### 5.1 The request unit is the region, not the archetype

Four requests cover the roster: Varenmark (14 — 9 P0 hostiles, 5 P1 townsfolk), then Greymoor (8),
Saltcliff (8), Cinderplain (4). A region shares one light, one ramp and one style scaffold, so one
generation brief per region is coherent — 24 separate commissions is what that avoids.

The first P0 set is **the nine Varenmark hostiles** (decision 3, [§8](#8-decisions)): they are the
only archetypes reachable in wave A1 and they share one palette. The five townsfolk rows are P1 —
art before their towns ship, not wave A1.

**The five-asset pilot gate stays.** Five of the nine go first at `gpt-image-2-high`; the remaining
four run at `gpt-image-2-medium` only once the pilot reads as one region, and without a second
approval gate. The draft's pilot named `giant-rat`, `goblin-scout`, `bandit-ruffian`,
`village-blacksmith` and `tavern-keeper`; the last two are P1 now, so that exact five cannot stand.
**Which five hostiles form the pilot is settled on the production card**, not here — against one
requirement: it must still exercise every class and every break sector the nine contain — beast (R1
horizontal, profile, R2 snout break), humanoid (R1 three-quarter plus R2 weapon break) and unquiet
(R2 tatter break; `moor-wight` is the only unquiet P0 and is also Greymoor's base).

### 5.2 Naming convention — complete, one asset per slug

```
archetype-<slug>-portrait-<size>.<ext>
```

| File | Size | Format | Budget | Served |
|---|---|---|---|---|
| `archetype-giant-rat-portrait-256.png` | 256×256 | PNG-24, 8-bit RGBA | — (source) | no |
| `archetype-giant-rat-portrait-128.webp` | 128×128 | WebP q88 | **16 KB** (asset-specs §3) | yes, 2× |
| `archetype-giant-rat-portrait-64.webp` | 64×64 | WebP q88 | **8 KB** (asset-specs §3, item icon 64px) | yes, 1× |

`<slug>` is verbatim from [`archetypes.md`](archetypes.md): lowercase, hyphen-separated, unique, and
identical in the filename, the asset request and any future lookup. **No `@2x`, no variant suffix,
no colour suffix, no pose number.** The size slot follows asset-specs §5's own good example
(`crest-primary-152.png`) rather than the `@2x` form in the brief, because §5 is the contract.

Directory: `web/client/static/images/archetypes/` — a purpose directory, added to asset-specs §5
alongside marks, herald, items and favicons by decision 4. **All three files per slug are committed
there, the 256 PNG-24 master included**: asset-specs §2 makes PNG the raster master format, asset-specs
§6's pre-commit sweep (`file *.png */*.png`) expects committed PNG masters, and §7 criterion 1 counts
all 27 files in this directory. What the master is *not* is served — no `<img>`, `<picture>` or CSS
references it, and an unreferenced file under `static/` is not a delivery surface; it exists as the
export source, which is what the `Served: no` column above records. Only the
layered working file stays out of the repository, at
`/home/kung/creative-assets/veldrath/2d/archetypes/`.

**Why 32 px is dropped:** asset-specs §4.7 exports 128/64/32 because items render at 32 px in the
bag. No archetype surface renders below 64 px, and §4's preamble says to export at 2× the largest
rendered size — 128. If a 32 px surface appears later, add the export then; it is one line in the
derive plan.

### 5.3 The filled request

`/home/kung/creative-assets/veldrath/specs/archetype-portraits-p0.yaml` — the first filled
`asset-request.yaml` on this project, so it also fixes the schema for later requests (D5 checks
against it). Its `canvas`, `format` and `budgets` blocks quote asset-specs; the 128 px budget and the
directory are asset-specs §3 and §5 rather than proposals, and the two `PROPOSED` comments say so. The
file reads `approved 2026-11-20`: F2 (`t_4e58e1ec`) reconciled its status, its scope and its acceptance
counts with decisions 1–5 after D5 found it still described the draft it was approved from.

The parts that decide the art:

- **Style scaffold (verbatim, appended to every subject line):** *centred three-quarter bust, square
  1:1 framing, flat plain magenta #ff00ff background, painterly dark-fantasy rendering with engraved
  metal and worn surfaces, lit from the upper left with a warm gold rim on every edge and no ambient
  fill, high contrast, silhouette readable at 64 px, no text, no letters, no watermark, no border,
  no frame, no drop shadow, no baked glow.*
- **Subject line:** one per slug, all fourteen written out — no "and similar". Nine sit in `assets[]`
  (the P0 hostiles), five in `deferred[]` (the townsfolk, P1). `deferred[]` is not decoration:
  `build_derive_plan.py` reads `assets` wholesale, so a P1 row left there regenerates a 28-export plan
  against §7's 27 files.
- **Post-process, in order:** `assetkit alpha --tol 24` → `assetkit trim --pad 4` →
  `assetkit resize … --width 256 --height 256` → `assetkit derive` → `portrait_check.py`.
- **Anchor:** `veldrath-final-crest-transparent.png` for material and light only, never composition.

### 5.4 The export matrix, as a machine check

`specs/archetype-portraits-derive.json` is generated from the request by
`tools/build_derive_plan.py`, so the two cannot drift. Regenerated after D5's defect 2 went in, it
carries **9 sources × 2 exports = 18 files**, each with its dimension, format, quality, alpha
requirement and byte budget — the nine P0 hostiles and no row from `deferred[]`. The 28-export run
recorded below is the fourteen-row probe of 2026-11-20 that proved the machinery, not this document's
scope. *That* regenerated plan is the gate: 18 exports, one 256 PNG master per slug accompanying them.

```bash
# dry-run against throwaway masters — verified on 2026-11-20
cd /home/kung/creative-assets/veldrath
env -u PYTHONPATH ~/.hermes/tools/creative-venv/bin/python tools/_probe_plan.py \
    specs/archetype-portraits-derive.json make
assetkit derive specs/archetype-portraits-derive.json --out-dir /tmp/derive-out
#   28 exports, 0 failing, formats: webp        exit 0
# and with a 2 KB ceiling on the same input:   exit 1, "2.8 KB over 2 KB budget"
```

An over-budget export is a **failure, not a note**: `assetkit derive` exits 1 and the card does not
come back for review until it exits 0.

### 5.5 One slug, end to end (the review template)

| Step | Value |
|---|---|
| generation | `gpt-image-2-high`, subject + scaffold, 1024×1024 |
| key | `assetkit alpha raw.png keyed.png --tol 24` |
| master | `archetype-drove-cur-portrait-256.png`, subject inside 80–92 % |
| export | `-128.webp` ≤16 KB, `-64.webp` ≤8 KB |
| gate | `portrait_check.py …-64.webp --class beast` → 6/6 pass |
| realm | `assetkit compare archetype-giant-rat-portrait-64.webp archetype-drove-cur-portrait-64.webp` with IoU exempted (below) |
| review | the file, not the summary |

**IoU is exempt within a set** — these are deliberately distinct creatures, so `assetkit compare` is
run with `--min-iou 0` and only the material bands are read: ink weight 80–120 % of the anchor, gold
and ember shares within 50 % relative or 8 pp. The IoU gate in [specs/README](../specs/README.md)
exists for *derivations of one mark*, and applying 85 % IoU to 34 silhouettes would force them to be
the same creature.

---

## 6. What I refuse to specify

1. **A per-tier portrait.** Boss and elite get a frame (§2 R3), not a second file. Drawing tier
   doubles the set and makes a band change a redraw.
2. **Faction marks for factionless rows.** 25 of 27 hostile rows are `none recorded`. A badge would
   assert canon that proposals 6–7 explicitly withheld. Only the gull-rider harpoon exists.
3. **Region character beyond biome.** Greymoor, Saltcliff and Cinderplain are working names and
   their character is Q2 TBD (`GAME.md` §8 item 1). The ramps are built from the zones' *seeded biome
   words*; they will be re-decided when region prose exists. I will not smuggle "a proud seafaring
   culture" into a palette.
4. **Any hue the palette does not hold.** No green moorland, no purple heather, no blue sea, no cyan
   magic — brand-guide §4 excludes off-palette hues and `tokens.json` is `locked: true`. If a region
   cannot read without green, the answer is more value contrast and a stronger rim, not a new token.
5. **Animation of any kind.** asset-specs §7: no GIF, APNG or Lottie, no ambient loops. No idle
   frames, no attack frames, no blink.
6. **A second pose, a colour variant or an "alternate" per slug.** One asset per slug per size. A
   different look is a different slug.
7. **`undertow` as a pictured creature.** "A current in a drowned room that pulls the wrong way" has
   no body. I specify a *material* rule — a distortion of the ground plane with a `seal/seal-dark`
   core and a `text/text` fracture line — and flag that this row may need a scripted effect rather
   than art at all. It is P1 so the decision can be deferred to the Saltcliff card.
8. **Five townspeople from one body.** The pipeline's own failure mode is role drift: similar jobs
   collapse into one character. Each town NPC must differ in silhouette (apron, keys, staff, spear,
   ledger), which is why they are separate generations with separate subject lines and not kit swaps
   on one civilian base.

---

## 7. Acceptance criteria (for the production card)

1. The nine P0 slugs exist under `web/client/static/images/archetypes/` — one 256 PNG master plus a
   128 and a 64 WebP each: 27 files, all committed in that one directory (§5.2 — the masters are
   committed and referenced by no markup), of which the 18 WebP exports are what `assetkit derive`
   writes and enforces.
2. `assetkit derive specs/archetype-portraits-derive.json` exits 0 — dimensions, alpha and budgets.
3. `portrait_check.py` exits 0 for all nine at 64 px, with the class named in the request.
4. No pixel with 0 < alpha < 255 in the outer 1 px of any 64 px export (no baked glow).
5. Every 128 px export ≤ 16 KB; every 64 px export ≤ 8 KB.
6. No hue outside the five Varenmark stops; `assetkit compare` against the pilot anchor with
   `--min-iou 0`, ink 80–120 %, gold and ember bands within 50 % relative or 8 pp.
7. Facing holds: hostiles three-quarter, townsfolk front-on (§2 R1) — checked by the gate's
   symmetry number, and by eye for the ones the number cannot settle.
8. `manifest.json` carries one row per file with slug, class, size and the prompt used.

---

## 8. Decisions

**Approved by Josh, 2026-11-20**, recorded on card `t_5a09d099`. This section records what was decided
and, separately, what was noticed and left alone — it re-derives no rule and no palette.

| # | Decision | Outcome |
|---|---|---|
| 1 | Silhouette rules R1–R4 | **Approved as written.** Five classes, facing as the hostility read (three-quarter = fight, front-on = talk), one outline break at a fixed bearing per class, tier drawn as a frame rather than in the art, faction mark only where a tie exists. |
| 2 | Greymoor's region light | **Approved, and written into `brand-guide.md` §5** so the guide and the art cannot contradict each other. Cold bone core, no gold rim, no crimson; §3.2 is otherwise unchanged. |
| 3 | The P0 set | **Nine Varenmark hostiles P0**; the five Varenmark townsfolk move to P1; **the five-asset pilot gate stays** before the remaining four (§5.1). |
| 4 | The two `asset-specs.md` amendments | **Both approved** — a 128 px WebP portrait budget row at ≤ 16 KB, and `static/images/archetypes/` as a purpose directory. Prerequisites for the export matrix, not options. |
| 5 | Five `region/*` UI tokens | **Deferred to its own card** after this document lands: `theme.css` + `tokens.json` + `design-system.md` in one edit, plus a parity-test update. The art needs none of them. |

### Debt recorded, not fixed

Two findings were accepted as debt. Neither is fixed here, and neither withholds the approval.

- **`goblin-scout` is seeded `TypeKey = "beasts"` but is a biped with a spear** — a content-taxonomy
  bug, not a drawing question. Its **visual** class is humanoid (§4.2) and it is drawn as humanoid;
  renaming the type key is a content change with reference risk and belongs to whoever owns
  `ArchetypeSeeder.cs`.
- **No archetype carries an image path, and `CombatPanel.svelte` draws no portrait** (§1), so the art
  has no render target yet. Card **A1** opens the contract — portrait path by slug convention, a
  fallback when the path is absent, the tier frame in CSS. Until it lands this document is a
  specification with no consumer, not a shipped feature.

### Corrections applied on landing

The mechanical consequences of decisions 1–4, each visible in this diff. No rule, ramp or class
changed.

| Where | Change | Why |
|---|---|---|
| header | `DRAFT` → `approved 2026-11-20` | the decision record is this section |
| §3.2 | "is decision 2" → "approved as decision 2, recorded in `brand-guide.md` §5" | decision 2 |
| §4.1 | the five townsfolk rows P0 → P1; counts now P0 9 · P1 18 · P2 7 | decision 3 |
| §4.1 | the sixth column header `class` → `asset class`, which duplicated the fourth | the duplicate label made this section's P0/P1 edit ambiguous |
| §5.1 | first set is the nine hostiles; which five hostiles form the pilot is left to the production card | decision 3 — the draft's pilot named two townsfolk, now P1 |
| §5.2, §5.3 | the 128 px budget and the directory cite `asset-specs` §3 and §5 instead of `PROPOSED` | decision 4 |
| §7 | criterion 1 restated as nine slugs, 27 files | decision 3; and the draft's "28 files = 14 × {256, 128, 64}" did not add up (3 × 14 is 42, while the plan's 28 counts exports only) |
| §5.2, §5.4, §7 criterion 1, `asset-specs` §3 | one master convention stated once and cited three times: the 256 PNG-24 is **committed** and never served, only the layered working file stays in `~/creative-assets`; §5.4's "28" now names itself a probe of the still-draft fourteen-row request | review of this landing: `asset-specs` said the master was "kept out of the repository" while §7 criterion 1 counted 27 committed files |
| §5.3–§5.6, the request | the request reads `approved 2026-11-20`; `assets[]` holds the nine P0 hostiles, the five townsfolk sit in `deferred[]` at P1, the two `PROPOSED` comments cite `asset-specs` §3 and §5, and the acceptance block asks for 27 files — the same count as §7 | D5 defect 2 (`t_d805cdf4`), applied by F2 (`t_4e58e1ec`): the request still read `DRAFT`, listed fourteen P0 including the five townsfolk decision 3 had moved to P1, and asked for 28 files where §7 asks for 27 |

### Left open on purpose

- **Which five hostiles form the pilot** is settled on the production card (§5.1), against the
  requirement stated there.
- **The Wound's ramp stays provisional** — no zones, no consumer; re-decide it when the region gets
  zones (§3.5).
- **D3's ten townspeople** for `towns.md` are not roster rows yet and so are unclassified (§4.1).

---

### 5.6 Appendix — the request in full

Generated from `specs/archetype-portraits-p0.yaml`; the file is the working copy, this
block is the read-only copy that travels with the document.

<!-- BEGIN asset-request -->
```yaml
# asset-request.yaml — Veldrath archetype portraits, wave A1 (Varenmark)
#
# This is the first filled asset-request on this project, so it also defines the
# schema; D5 checks later requests against it. Field names sit on top of
# docs/design/asset-specs.md, which stays authoritative: every canvas, format and
# budget below is quoted from it, and anything not in it is marked PROPOSED.
#
# Schema (v1):
#   request{id,created,status,spec,owner,unit,class}
#   surface{component,tile,rendered,export,backdrop,fallback}
#   format{master,delivery,alpha}         # asset-specs §2
#   canvas{master,exports,subject_fill}   # asset-specs §4
#   budgets{...}                          # asset-specs §3, or PROPOSED
#   naming{pattern,files,directory,slugs} # asset-specs §5
#   palette{region,stops,material_ratio,light,forbidden}
#   style_scaffold{suffix,negatives,tier,reference}
#   assets[{slug,class,priority,pilot,base,subject,notes}]
#   deferred[{slug,class,priority,base,subject,notes}]   # P1 rows, outside wave A1's derive plan
#   post_process[...]
#   acceptance[...]
#   delivery{...}

request:
  id: archetype-portraits-p0-varenmark-2026-11-20
  created: 2026-11-20
  status: "approved 2026-11-20 — decisions 1-5 on card t_5a09d099; nine P0 hostiles, five townsfolk P1"
  spec: docs/design/content/archetype-visuals.md
  owner: "dev-creative (production), dev-designer (acceptance)"
  unit: one region is one request
  class: "GENERATE   # docs/design/specs/README.md: a surface that does not exist yet"

surface:
  component: "web/client/src/lib/game/ArchetypePortrait.svelte  # does not exist yet"
  tile: "64x64 square, object-fit contain, no border of its own"
  rendered: 64px
  export: "128px (2x) and 64px (1x)   # asset-specs §4 preamble"
  backdrop: "--vds-bg-2 #1c1d2b, the cell colour item art uses (asset-specs §4.7)"
  fallback: "Lucide glyph by class, never a placeholder rectangle (asset-specs §4.7)"

format:
  master: "PNG-24, 8-bit RGBA, transparent               # asset-specs §2"
  delivery: "WebP, quality 88                             # asset-specs §2"
  alpha: "required on every file; no baked glow, bloom, shadow or vignette   # asset-specs §2"

canvas:
  master: 256x256
  exports: [128, 64]
  subject_fill: "80-92% of the canvas on the longer axis  # asset-specs §4.7 ~85%"

budgets:
  "128": "16 KB   # asset-specs §3, 'Archetype portrait, 128px WebP'"
  "64": "8 KB     # asset-specs §3, 'Item icon, 64px'"

naming:
  pattern: "archetype-<slug>-portrait-<size>.<ext>      # asset-specs §5 <purpose>-<subject>-<size>"
  files: "archetype-<slug>-portrait-256.png (master, not served), archetype-<slug>-portrait-128.webp, archetype-<slug>-portrait-64.webp"
  directory: "web/client/static/images/archetypes/       # asset-specs §5, purpose directory"
  slugs: "verbatim from docs/design/content/archetypes.md; no aliases, no variants, one asset per slug"

palette:
  region: varenmark
  stops:
    ground: "background/bg-1 #14151f"
    stone: "border/border-strong #3d3f58"
    metal: "gold/gold-dark #a68a2e"
    rim: "gold/gold-light #d4b96a"
    ember: "seal/seal-muted #3d1a16"
  material_ratio: "60% ground / 30% gold family / 10% ember as light only"
  light: "key from upper-left at 10-11 o'clock; gold-light rim on the key edges; one emissive core at chest height, warm, low"
  forbidden: "no hue outside the five stops; no green, purple, teal or cyan glow; no flat crimson fill (brand-guide §4)"

style_scaffold:
  suffix: >-
    centred three-quarter bust, square 1:1 framing, flat plain magenta #ff00ff background,
    painterly dark-fantasy rendering with engraved metal and worn surfaces, lit from the upper
    left with a warm gold rim on every edge and no ambient fill, high contrast, silhouette
    readable at 64 px, no text, no letters, no watermark, no border, no frame, no drop shadow,
    no baked glow
  negatives: "no gradient background, no checkerboard, no vignette, no second figure, no UI chrome"
  tier: "gpt-image-2-high for the five pilot assets; gpt-image-2-medium for the remaining four"
  reference: "web/client/static/images/veldrath-final-crest-transparent.png — material and light only, never composition"

# The draft's pilot named five, two of them townsfolk that decision 3 moved to P1. The three that
# survive are flagged here; which five hostiles form the five-asset pilot is settled on the
# production card (§5.1), not in this file.
assets:
  - slug: giant-rat
    class: beast
    priority: P0
    pilot: true
    base: null
    subject: >-
      a bloated brown rat the size of a dog, matted fur, one torn ear, snout low and forward,
      heavy in the shoulders, wet grain caught in its whiskers
    notes: "profile left; the snout is the outline break"
  - slug: goblin-scout
    class: humanoid
    priority: P0
    pilot: true
    base: null
    subject: >-
      a small hunched goblin scout in a patched leather hood and a stolen cart-harness,
      a notched short spear held across the body, ears out, half-turned as if about to run
    notes: "3/4; the spear blade breaks the upper right"
  - slug: bandit-ruffian
    class: humanoid
    priority: P0
    pilot: true
    base: null
    subject: >-
      a road bandit in a hooded cloak of dyed sacking, one boiled-leather shoulder cap,
      a wrapped forearm, a short broad blade in the near hand held low and ready
    notes: "3/4; the blade breaks the upper right"
  - slug: drove-cur
    class: beast
    priority: P0
    pilot: false
    base: null
    subject: >-
      a feral working dog gone thin and hard, short brindle coat with the ribs showing,
      a rusted collar with no tag, sloping shoulders, mouth closed, eyes level
    notes: "profile left"
  - slug: peat-adder
    class: beast
    priority: P0
    pilot: false
    base: null
    subject: >-
      a fat low adder coiled on wet peat, jaws wide, two pale fangs bared, mud-caked scales
      banded in dull bronze and black, tail thick and muscular
    notes: "profile left; the open jaw is the break"
  - slug: forest-wolf
    class: beast
    priority: P0
    pilot: false
    base: null
    subject: >-
      a grey forest wolf in winter coat, head low and forward, hackles half-raised, one foreleg
      advanced, eyes catching light from under the brow
    notes: "profile left"
  - slug: sump-crawler
    class: beast
    priority: P0
    pilot: false
    base: null
    subject: >-
      a pale blind crawler rising out of black water, only head, clawed hands and shoulders
      above the surface, no eyes, gills flaring at the neck, weed in the joints
    notes: "front of mass toward the left; the body continues below a waterline"
  - slug: mire-boar
    class: beast
    priority: P0
    pilot: false
    base: null
    subject: >-
      a bog boar with a muddy hide and a ridge of coarse bristles, one tusk splintered,
      head down and driving forward, hooves planted in standing water
    notes: "profile left"
  - slug: moor-wight
    class: unquiet
    priority: P0
    pilot: false
    base: null
    subject: >-
      a standing corpse in waterlogged grave-cloth, arms at its sides, head tilted, no face
      under the hood, one cold light burning inside the chest
    notes: "front-on but asymmetric; the shroud tatters downward and never reaches the frame"

# P1 — the five Varenmark townsfolk: art before their towns ship, not wave A1 (decision 3). Their
# subject lines are kept so the P1 pass is a re-prioritisation, not a rewrite. build_derive_plan.py
# reads `assets` only, which is why these are not there: a P1 row left in `assets` regenerates a
# 28-export plan against a 27-file acceptance (§7).
deferred:
  - slug: village-blacksmith
    class: townsfolk
    priority: P1
    base: null
    subject: >-
      a broad-shouldered village blacksmith in a scorched leather apron over a wool shirt,
      sleeves rolled to the elbow, a hammer resting head-down in one fist, soot to the forearms
    notes: "front-on; the first townsfolk anchor, no outline break"
  - slug: tavern-keeper
    class: townsfolk
    priority: P1
    base: null
    subject: >-
      a shrewd innkeeper in a laced bodice and a work apron with a rag tucked at the waist,
      a ring of keys on the hip, arms folded, lit by a low hearth
    notes: "front-on; the hearth is the only warm source"
  - slug: wayfarer-elder
    class: townsfolk
    priority: P1
    base: null
    subject: >-
      an old road-elder in a travel cloak with a patched hood, a walking staff in one hand,
      the other raised as if reading a cut stone marker, weathered and calm
    notes: "front-on; the staff is vertical, inside the frame"
  - slug: market-factor
    class: townsfolk
    priority: P1
    base: null
    subject: >-
      a well-fed trade factor in a good coat worn plain, a board of tally-sticks under one arm,
      a ledger in the other hand, a seal-ring on one finger, quick behind the eyes
    notes: "front-on"
  - slug: gate-watch
    class: townsfolk
    priority: P1
    base: null
    subject: >-
      a town gate-watch in a padded jack and a kettle helm, a boar spear planted butt-down,
      a toll slate on a cord at the belt, bored but attentive
    notes: "front-on; the spear is vertical, inside the frame"

post_process:
  - "assetkit alpha raw.png keyed.png --tol 24   # magenta key, 4-corner flood fill"
  - "assetkit trim keyed.png trimmed.png --pad 4"
  - "assetkit resize trimmed.png archetype-<slug>-portrait-256.png --width 256 --height 256"
  - "assetkit derive specs/archetype-portraits-derive.json   # dimensions, alpha, budgets"
  - "python3 tools/portrait_check.py archetype-<slug>-portrait-64.webp --class <class>"
  - "if an export is over budget, step WebP quality to 82 before touching size (asset-specs §3)"

acceptance:
  - "27 files exist under web/client/static/images/archetypes/ (9 slugs x 256.png + 128.webp + 64.webp)"
  - "assetkit derive specs/archetype-portraits-derive.json exits 0"
  - "tools/portrait_check.py exits 0 for all nine at 64px with the class named in this request"
  - "no pixel with 0 < alpha < 255 in the outer 1px of any 64px export"
  - "every 128px export is 16 KB or less; every 64px export is 8 KB or less"
  - "no hue outside the five varenmark stops; assetkit compare against a pilot anchor, gold and ember bands within 50% relative or 8 pp"

delivery:
  to: "/home/kung/creative-assets/veldrath/2d/archetypes/ (masters) and web/client/static/images/archetypes/ (delivery)"
  report: "one manifest.json row per file {file, slug, class, size_px, model_or_script, prompt, created}"
  review: "kanban_request_review(reviewer=dev-designer, artifacts=[all 27 files])"
```
<!-- END asset-request -->
