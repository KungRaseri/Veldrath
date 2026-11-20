# Asset specs

> **Audience:** whoever produces and exports the files — designer, illustrator or developer.
> **Purpose:** the technical contract for an image that ships. What canvas, what format, what weight,
> what name, and how it becomes live.
> **Related:** [Brand guide](brand-guide.md) for what the mark is and how it may be used ·
> [Asset inventory](asset-inventory.md) for the current file-by-file status ·
> [Design system](../design-system.md) for tokens and components.

---

## 1. The problem this document fixes

Three files carry the entire brand today, and all three are far heavier than the size they render at:

| File | File pixels | Rendered at | File weight | Oversampling |
|---|---|---|---|---|
| `veldrath-text-flat-true-transparent.png` | 1916 × 464 | 30px tall (~124px wide) | **1.04 MB** | ~15× |
| `veldrath-final-crest-transparent.png` | 1208 × 1226 | 76px (auth), 48px (roster), 520px (hero at 18% opacity) | **1.65 MB** | up to 16× |
| `veldrath-final-horizontal-transparent.png` | 1672 × 939 | 64px tall | **1.85 MB** | ~15× |

Every page of the app loads the 1.65 MB crest; the header loads the 1.04 MB wordmark on every route.
A page that renders a 124px-wide logo must not download a 1916px one. The rules below exist so that
the next asset does not repeat this.

The second problem: **no editable source is committed.** The PNGs are the only masters. Any
correction — a favicon that reads at 16px, a print version, a colour variant — currently requires
redrawing the mark from scratch.

---

## 2. Formats

| Asset type | Deliver | Never |
|---|---|---|
| Marks, glyphs, icons, anything with hard edges | **SVG**, hand-cleaned (no editor metadata, no embedded raster, no `style=""` blocks) | PNG for something that could be vector |
| Illustration, texture, photographic | **PNG-24 with alpha** for masters; **WebP** for delivery; **AVIF** optional | JPEG with a black matte; interlaced PNG |
| Social / OG cards | **PNG** (some scrapers still refuse WebP) at 1200 × 630 | — |
| Favicons | PNG at the exact sizes in [§4.6](#46-favicon-and-install-icons), plus an SVG when the mark allows | One 512px file scaled by the browser |
| Fonts | Not an asset deliverable. The five faces are already committed and licensed — new art does not introduce type | A second display face |

**Alpha, always.** No file in `web/client/static/images/` carries a background. No baked glow, bloom,
shadow or vignette around a mark — the interface applies its own shadows and a baked one doubles.

**Sources.** Deliver the layered or vector master alongside the raster exports, named per [§5](#5-naming-convention).

---

## 3. Weight budgets

Budgets are per file, measured after export and before commit. If an export exceeds its budget, reduce
colour depth, reduce the pixel size to the largest size actually rendered, or switch to WebP.

| Asset | Budget | Today |
|---|---|---|
| SVG mark | ≤ 40 KB | none exists |
| Crest, 2× (152px) | ≤ 60 KB | 1.65 MB at 1208px |
| Crest, hero (1040px wide) | ≤ 180 KB WebP | shares the same 1.65 MB PNG |
| Wordmark, 2× (90px tall) | ≤ 40 KB | 1.04 MB at 1916px |
| Horizontal lockup, 2× (192px tall) | ≤ 60 KB | 1.85 MB at 1672px |
| Favicon 512 | ≤ 60 KB | 597 KB |
| OG card 1200 × 630 | ≤ 200 KB | none exists |
| Illustration, large | ≤ 250 KB WebP | — |
| Archetype portrait, 128px WebP | ≤ 16 KB | none exists |
| Item icon, 64px | ≤ 8 KB | — |
| Email header, 600 × 200 | ≤ 60 KB | none exists |

An archetype portrait exports at 256px (PNG master, kept out of the repository), 128px and 64px WebP.
The 128px export owns the row above; the 64px export takes the **item icon** budget (≤ 8 KB), because
it renders in the same 64px tile and the same budget already governs that size. Canvas, format and
naming for the family are in
[the archetype visual system](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/development/docs/design/content/archetype-visuals.md)
§5.2 (a working document — it is in the repository, not on this site).

---

## 4. Canvas sizes

Rendered sizes come from CSS; export at **2×** the largest rendered size, and 3× only where the asset
is a crisp edge at small size (favicons, glyphs).

### 4.1 Marks

| Surface | Rendered | Export |
|---|---|---|
| Header wordmark | 30px tall | 60px and 90px tall, width auto; plus SVG |
| Footer lockup | 64px tall | 128px and 192px tall; plus SVG |
| Auth crest | 76px square box, `object-fit: contain` | 152 × 152 and 228 × 228 |
| Account header crest | 44px | 88 × 88 |
| Character roster crest | 48px | 96 × 96 |
| Character creation crest | 40px | 80 × 80 |
| Hero crest | max 520px wide, rendered at **18% opacity** | 1040px and 1560px wide |

Because the live crest file is used at five different sizes, the correct answer is an SVG plus one
large raster fallback — not five files.

### 4.2 Social card

`1200 × 630`, safe area inset 80px on all sides. The mark and any text must sit inside the safe area;
scrapers crop the edges. One card for the whole site today; per-branch cards later if needed.

### 4.3 Email header

`600 × 200`. Email clients do not honour `backdrop-filter`, `filter` or CSS gradients reliably — the
header must read correctly as a flat PNG on a `#0C0D13` body. No transparency-dependent lighting.

### 4.4 Illustrations

| Use | Canvas | Notes |
|---|---|---|
| Empty state (roster, bag, quest log) | 240 × 180 | Muted, low-contrast; it sits behind explanatory copy |
| Error / 404 | 320 × 240 | Can carry the crest; see the error page for the copy |
| Lore article hero | 1600 × 600 | The top 40% must survive a dark overlay, because the title sits over it |

### 4.5 Icons

Lucide is the icon library and there is no second icon set.

- Grid **24 × 24**, with a 2px safe margin — the optic area is 20 × 20.
- Stroke **1.5px** at 24px (Lucide's nominal 2px reads too heavy beside Cinzel at small sizes).
- Rounded caps and joins; no fills mixed into an outline set.
- Corners no sharper than 1px.
- No text inside an icon; no gradients; single colour, inheriting `currentColor`.

A custom glyph is only justified when Lucide has no equivalent. Adding one means adding a component,
not a PNG.

### 4.6 Favicon and install icons

| File | Size | Purpose |
|---|---|---|
| `favicon-16x16.png` | 16 | Browser tab |
| `favicon-32x32.png` | 32 | Browser tab, high-DPI |
| `apple-touch-icon.png` | 180 | iOS home screen (no transparency; iOS composites on white — supply a dark tile) |
| `android-chrome-192x192.png` | 192 | Android install |
| `android-chrome-512x512.png` | 512 | Android splash |
| `favicon.ico` | multi-size | Legacy |

At 16px, only the silhouette survives: a spiked ring with a bright core. Test the mark at 16px before
sign-off — the runic band and the herald figure will be gone.

### 4.7 Item art

Items currently render as **Lucide glyphs** chosen by item type and equipment slot in
`web/client/src/lib/game/item-icons.ts`, coloured by rarity text. No item artwork exists.

If item art is commissioned, the contract is:

| Point | Rule |
|---|---|
| Master canvas | 256 × 256, transparent, centred, subject fills ~85% of the canvas |
| Exports | 128px, 64px, 32px |
| Small-size rule | At 32px the subject must read as a silhouette; discard interior detail rather than letting it speckle |
| Rarity | **Not** drawn into the art. Rarity is a text colour and a future UI frame — art stays neutral so it can be re-tiered without a redraw |
| Background | None. The cell behind it is `--vds-bg-2` |
| Naming | `item-<type>-<material-or-name>-<size>.png`, e.g. `item-sword-iron-64.png` |
| Fallback | Until every item has art, the Lucide glyph stays the fallback. Never ship a placeholder rectangle |

---

## 5. Naming convention

```
<purpose>-<subject>[-<variant>][-<size>][@2x].<ext>
```

Lowercase, hyphen-separated, no spaces. The purpose leads so the directory sorts by use, not by
whatever the generator called it.

| Good | Bad | Why |
|---|---|---|
| `crest-primary-152.png` | `veldrath-final-crest-transparent.png` | No size, no purpose |
| `wordmark-header-90.png` | `veldrath-text-flat-true-transparent.png` | `true`/`final` are generation artifacts |
| `lockup-horizontal-192.png` | `veldrath-final-horizontal-transparent.png` | Same |
| `faction-ember-court-512.webp` | `new_emblem_2.png` | No meaning, no owner |
| `item-sword-iron-64.png` | `sword.png` | Collides on the second sword |

Keep `-transparent` only where the name would otherwise be ambiguous about alpha; alpha is the default
and does not need a suffix.

**Directories:** marks → `static/images/` · heraldic and faction art → `static/images/herald/` (or
`factions/` once there is more than a handful) · archetype portraits → `static/images/archetypes/` ·
items → `static/images/items/` · favicons → `static/images/favicon/`. Never nest by date or by
designer.

---

## 6. Delivering an asset

1. **Reference it, then export it.** Confirm the surface and its rendered size from
   [§4](#4-canvas-sizes). An asset with no surface does not ship.
2. **Export at the 2× size, check the budget.** If it is over budget, step down to the largest size
   actually rendered before touching quality.
3. **Name it** per [§5](#5-naming-convention) and put it in the right directory.
4. **Reference it with explicit dimensions** so the browser reserves the box: `width`/`height`
   attributes or a fixed CSS box. A logo without dimensions costs layout shift on every load.
5. **Below the fold** add `loading="lazy" decoding="async"`.
6. **Alt text**: decorative → `alt=""`; informative → a short noun phrase; the header brand link keeps
   `alt="Veldrath"` plus `aria-label` on the anchor.
7. **Update the tables.** Add the file and its surface to the [brand guide](brand-guide.md) and the
   [asset inventory](asset-inventory.md). A mark that is in the directory but in neither table is a
   file nobody may use.
8. **If it appears on the docs site**, put the 2× export in `docs/design/images/` — that directory is
   already mapped in the `resource` section of `docfx.json` and published at `/design/images`. Never
   copy a live app asset there to make a page work: the published documentation must not ship the
   1–2 MB originals this document exists to replace.

### Pre-commit check

```sh
# file dimensions and weight for everything in the image directory
cd web/client/static/images && file *.png */*.png && stat -c '%s %n' *.png */*.png | sort -n
```

Anything above its budget in [§3](#3-weight-budgets) is a regression, not an optimisation opportunity.

---

## 7. Motion

| Property | Value |
|---|---|
| Duration | 120–200ms for state changes; up to 400ms for a dialog entering |
| Easing | ease-out on entry, ease-in on exit |
| Ambient loops | **None** in the game shell — a live panel is not an ambient display |
| Glow pulses | Only as feedback for a state change (equipped, level-up, damage), never a repeating loop |
| Reduced motion | Every animation must be harmless under `prefers-reduced-motion: reduce`; ship the final state |

Animated assets (GIF/APNG/Lottie) are not accepted. If a motion is wanted, it is built in CSS from
static art, so it can be disabled.

---

## 8. Rights and provenance

- Fonts are OFL (SIL Open Font License); Cinzel, Cinzel Decorative, Lora and JetBrains Mono are
  redistributable and require no attribution in binary distribution. Inter is served from Google
  Fonts. Do not add a sixth face without updating the design system and the licence record.
- Every delivered asset needs a licence or an unambiguous provenance note — purchased licence, own
  work, or a generation record for AI-assisted art. Files with unclear rights do not enter the
  repository.
- Icon glyphs are Lucide (ISC licence) and move through the package, not through `static/images/`.
