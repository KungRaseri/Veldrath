# Veldrath brand guide

> **Audience:** designers, illustrators and copywriters producing art for Veldrath.
> **What this covers:** what the mark is made of, which file to use where, how large it may go, what
> may not be done to it, and the art direction that new work must match.
> **What this does not cover:** file formats, export sizes and hand-off rules — those are in
> [Asset specs](asset-specs.md). Tokens and component rules are in the
> [design system](../design-system.md).

---

## 1. The mark

Veldrath's identity is a **heraldic crest and a logotype**, both built from the same materials.
Nothing in the mark is flat colour; it is lit metal and cloth.

### 1.1 The crest

A circular heraldic frame, roughly square in aspect, made of:

| Element | Description |
|---|---|
| **Frame** | Blackened iron ring, engraved with a runic band, studded with four- and eight-point barbed spikes that break the circle at the cardinal points |
| **Field** | Cracked black basalt with crimson fissures glowing from within |
| **Figure** | A hooded, faceless herald in spiked armour, centred, holding the seal |
| **Sigil** | A four-point compass star at the foot of the figure, with a crimson core |
| **Lighting** | Warm gold rim light on every edge, crimson emissive glow from the cracks and the sigil core, no ambient fill |

### 1.2 The logotype

`VELDRATH` set in a custom blackened-gold display treatment: tall Roman capitals with chiselled
facets, cracked-metal infill, barbed terminals and blood-red ember smoke burning off the letterforms.
The crest sits behind and above the word as a dark recessed circle.

**This logotype is not Cinzel.** Cinzel is the UI heading face and shares the Roman-capital lineage,
but the logo has bespoke barbed terminals, crack texture and ember smoke that no font can produce. Never
retypeset the logo in Cinzel, and never set body text in the logotype treatment.

### 1.3 Formats

All current masters are **raster PNG with alpha, 8-bit RGBA**, produced at roughly 1200–1900px on the
long edge. **No editable vector or layered source is committed to this repository.** If the mark is to
survive at favicon scale, on print or on merchandise, the first deliverable is a vector master — see
[Asset specs § 3](asset-specs.md).

---

## 2. Which file to use

Every file below lives in `web/client/static/images/`. The "used on" column is where the app places it
today; the "size" column is the rendered size, not the file's pixel size.

| File | Pixels | Rendered at | Used on | Verdict |
|---|---|---|---|---|
| `veldrath-final-crest-transparent.png` | 1208 × 1226 | 76px; 44px; 48px; 40px; hero behind content | Auth card on login / register / forgot / reset / verify, account header, character roster, character creation, landing hero | **Primary mark.** The only crest in service. |
| `veldrath-final-horizontal-transparent.png` | 1672 × 939 | 64px tall | Site footer | **Lockup.** Crest + wordmark, for contexts with horizontal room and dark background. |
| `veldrath-text-flat-true-transparent.png` | 1916 × 464 | 30px tall | Site header, every page | **Header wordmark.** Wordmark only, no crest — use where the crest would compete with navigation. |
| `favicon/*` | 16 – 512 | native | Browser tab, iOS home screen, Android install | **Icon set.** Regenerate from the vector master when it exists. |
| `veldrath-final-vertical-transparent.png` | 882 × 1254 | — | nothing | Crest over wordmark, unassigned |
| `veldrath-final-circular-transparent.png` | 1254 × 1254 | — | nothing | Unassigned |
| `veldrath-final-square-v-transparent.png` | 1254 × 1254 | — | nothing | Unassigned |
| `veldrath-full-logo-transparent.png` | 1254 × 1254 | — | nothing | Unassigned |
| `veldrath-icon-big-transparent.png` | 1254 × 1254 | — | nothing | Unassigned |
| `veldrath-text-energy-true-transparent.png` | 1907 × 579 | — | nothing | Ember variant of the wordmark, unassigned |
| `veldrath-v-clean-true-transparent.png` | 1115 × 1389 | — | nothing | Letter V study |
| `veldrath-v-molten-true-transparent.png` | 1066 × 1356 | — | nothing | Letter V study |
| `molten_metallic_v_logo_design_true_transparent.png` | 828 × 1102 | — | nothing | Earlier V study |
| `molten_v_with_fiery_aura_true_transparent.png` | 1025 × 1176 | — | nothing | Earlier V study |

The unassigned files are a brand kit, not a set of approved lockups. Before any of them ships, this
table gains the surface that uses it. The [asset inventory](asset-inventory.md) carries the decision
on whether they stay in the served image directory or move to a brand-kit folder.

### 2.1 Selection rule

1. **Is the wordmark enough?** Use the header wordmark. It is the quietest option and the default for
   navigation bars and email.
2. **Does the surface need heraldry?** Use the crest. Landing hero, auth card, empty roster.
3. **Is the surface wide and ceremonial?** Use the horizontal lockup. Footer, press, social preview.
4. **Anything else** is unassigned — do not reach for a variant because it exists.

---

## 3. Clear space and minimum size

**Clear space.** Reserve 25% of the mark's height on all four sides. Nothing — text, border, another
logo, a card edge — enters that zone. For the horizontal lockup, measure against the crest's height,
not the wordmark's.

**Minimum sizes.** The filigree is the constraint, not the silhouette.

| Asset | Minimum | Notes |
|---|---|---|
| Crest | **48px** | At 48px the runic band is lost but the silhouette still reads. 76px is the design size. Below 48px use the V-only mark or the favicon, never the full crest. |
| Horizontal lockup | **160px wide** | The wordmark needs ~24px of cap height to stay legible |
| Header wordmark | **24px tall** | 30px is the design size |
| V-only mark | **24px** | Permitted at small sizes; the full crest is not |
| Favicon | 16px | Hand-checked, not auto-scaled |

**The crest is not an icon.** It has more detail than a 24px glyph can carry. If a design needs a
small Veldrath identifier in a panel header, that is already solved with a Lucide glyph in gold — see
[design system §6](../design-system.md#6-iconography).

---

## 4. Colour roles

Art is composed from the same three materials as the interface, in roughly this ratio:

| Share | Role | Values |
|---|---|---|
| **60–70%** | blackened steel and basalt | `#080910` – `#2E2F42` |
| **20–30%** | aged gold metal, engraving, rim light | `#A68A2E` → `#C9A84C` → `#D4B96A` |
| **5–10%** | ember and blood, as light sources only | `#8C2018` / `#C0392B` / `#CC4125`, cores up to `#E05545` |

Crimson is **light, not paint**. It lives in cracks, cores, smoke and lens flares; it is never a large
flat fill, never a background wash and never a UI surface — the interface reserves `#C0392B` for
buttons and active state, and art that spends it decoratively makes the UI unreadable as action.

Off-palette hues are excluded by default: no purple, no green, no teal, no cyan glow. The one
exception is rarity colour on item art, which belongs to the interface, not to the artwork.

---

## 5. Art direction

New artwork must look like it was lit in the same room as the existing crest.

**Do**

- Compose symmetrically and heraldically. Circles, rings, spiked frames, vertical axes.
- Engrave detail into metal: filigree, rune bands, rivets, chiselled facets, thorny barbs.
- Light from within and from the upper edge: gold rim light plus a crimson emissive core.
- Use an aged, pitted, oxidised surface. Metal with no wear reads as plastic.
- Keep the silhouette clean at 48px even when the interior is dense.
- Let cloth and smoke have weight — heavy, slow, gravity-bound.

**Don't**

- Don't add gradients that are not describing light on a surface.
- Don't draw flat vector-style art with 2px strokes and no texture — that is the icon system's job,
  not illustration's.
- Don't add baked glow, bloom or drop shadow around a mark. The app applies its own
  (`filter: drop-shadow(0 0 40px rgba(192,57,43,0.35))` behind the hero crest, `0 0 18px` on the auth
  crest); a baked halo doubles it and shows a rectangular edge.
- Don't bake a background into a mark. Alpha only, ever.
- Don't introduce a second typeface into the art. Lettering inside artwork is ornament; readable
  words belong to Cinzel.
- Don't use the crest as a texture, watermark or repeating pattern at large scale.

**Composition constraints from the interface**

| Surface | Constraint |
|---|---|
| Hero crest | Sits behind content at **18% opacity** with a 40px crimson shadow, max 520px. Art must survive being that faint — high-contrast subjects turn into mud at 18%. |
| Auth crest | **76px square box**, `object-fit: contain`, crimson shadow applied by CSS. The mark's own aspect ratio (~1:1) is fine; a wide lockup is not. |
| Header wordmark | **30px tall**, actual width ~124px. Supplied at 1916px today — 15× oversampled. |
| Footer lockup | **64px tall**, sits beside body copy in a four-column footer. |

---

## 6. Accessibility

- The mark is decorative where a heading already names Veldrath: `alt=""` and `aria-hidden`. This is
  what the hero, the auth card and the favicon set do.
- The mark is informative where it *is* the link or the only label: the header brand link carries
  `alt="Veldrath"` plus `aria-label="Veldrath home"` on the anchor. Never rely on the image to carry
  meaning that text does not.
- Do not place the mark on a mid-tone background where `#C0392B`-lit edges vanish. The mark assumes
  Layer 0–2 (`#0C0D13` – `#1C1D2B`).
- Any text rendered inside an image must meet 4.5:1 against that image. Prefer no text in art; the
  interface sets its own.

---

## 7. Voice and copy

| Do | Don't |
|---|---|
| Plain, direct, slightly formal | "Hey adventurer!" |
| Errors that name the problem: "Unable to connect to server." | "Uh oh! Something went wrong!" |
| Title case in headings, inscribed register | ALL CAPS for emphasis (reserved for the hero kicker and eyebrow labels) |
| Lore prose in Lora, second person only when addressing the player | Ironic or modern slang in world text |

---

## 8. Before you deliver

1. Does this asset have a surface in the tables above? If not, it is not shippable yet.
2. Is it on the palette in [§4](#4-colour-roles)?
3. Does the mark keep 25% clear space and meet its minimum size?
4. Is there alpha, with no baked glow or background?
5. Is there a vector or layered source, and is it named to the convention in
   [Asset specs](asset-specs.md)?
6. If the file replaces one in `web/client/static/images/`, have you told the team the rendered size
   it needs to cover?

Full export, naming and weight rules: [Asset specs](asset-specs.md). Current file-by-file status:
[Asset inventory](asset-inventory.md).
