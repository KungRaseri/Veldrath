# Veldrath Design System

> **Status:** Living document — decisions are recorded in [§9 Decision log](#9-decision-log).
> **Surfaces covered:** the SvelteKit app only (`web/`) — public site, accounts, characters, the game
> client, Foundry, editorial, admin and moderation.
> **Source of truth:** the tokens themselves live in CSS, not here. `theme.css` holds every raw token
> and the Skeleton v5 ramp block; `brand.css` holds the brand layer and the component primitives.
> This document explains them and states the rules — when the two disagree, the CSS wins and this
> document is wrong.
> **Last verified against code:** 2026-11-19.
> **Related:** [Brand guide](design/brand-guide.md) · [Asset specs](design/asset-specs.md) ·
> [Asset inventory](design/asset-inventory.md) · [Client UI architecture](client-ui-architecture.md).

---

## Table of contents

1. [Overview and design principles](#1-overview-and-design-principles)
2. [Brand identity](#2-brand-identity)
3. [Colour](#3-colour)
4. [Typography](#4-typography)
5. [Spacing, layout and radius](#5-spacing-layout-and-radius)
6. [Iconography](#6-iconography)
7. [Components](#7-components)
8. [Implementation notes](#8-implementation-notes)
9. [Decision log](#9-decision-log)
10. [History — retired surfaces](#10-history--retired-surfaces)

---

## 1. Overview and design principles

The Veldrath Design System (VDS) is the visual language of a single surface: the SvelteKit app.
There is no second platform to keep in sync, so a token is defined once, mapped once into the
Skeleton ramp block, and consumed either through a Skeleton preset class or through a component
primitive in `brand.css`.

| Principle | What it means in practice |
|---|---|
| **Dark-first** | Every surface sits on a near-black canvas. Depth comes from layering lighter surfaces, never from a light-mode variant. |
| **Restrained drama** | Strong colour appears in controlled doses — Crimson Seal for action, Gold for metal and structure, Emberfall for heat. Every accent earns its presence. |
| **Legibility before atmosphere** | Inter carries all functional text at 11–16px. Display faces appear at heading sizes only. No decorative face is ever used for a label, a value or a button. |
| **Semantic colour** | Red means danger, gold means metal and standing, green means health, blue means information. Decorative use must not violate those meanings. |
| **One implementation** | A colour, radius or spacing value exists once. A component that needs a raw hex is a bug, not a style. |

---

## 2. Brand identity

**Name:** Veldrath
**Register:** epic, grounded, slightly archaic — never ironic.
**Tone:** prestige dark fantasy. The world is old, dangerous and beautiful; the interface should feel
carved from stone and lit by firelight.

**Material story.** The identity is built from three materials, and every visual decision can be
traced to one of them:

| Material | Meaning | Token | Where it appears |
|---|---|---|---|
| **Blackened steel** | the world's age and weight | background layers, `--vds-border-*` | canvases, panels, dividers, the crest's frame |
| **Aged gold** | standing, craft, the mark itself | `--vds-gold*` | the logo, panel-header icons, nav glyphs, hero kickers, equipped markers, prose emphasis |
| **Ember and blood** | danger, heat, life | `--vds-seal*`, `--vds-ember*` | primary actions, active state, combat and energy, the crest's fissures |

> **Gold and Seal are different jobs.** Gold is the *metal* — it is allowed to be permanent,
> decorative and quiet. Crimson Seal is the *action* — it is only allowed where a player can press,
> select or be alerted. A page with gold icons and one seal button reads correctly; a page with seal
> icons and a seal button reads as an error state. See §3.4 below and the
> [brand guide](design/brand-guide.md).

**Voice.** Headings use title case and read as inscribed rather than typed. Body copy is plain,
direct and slightly formal — no "Hey!" energy. Errors name the problem without panic: "Unable to
connect to server." Lore and editorial prose may use Lora for manuscript warmth.

---

## 3. Colour

### 3.1 How the colour system is built

Three layers, in this order:

1. **Raw VDS tokens** — the `:root` block in `theme.css`. These are the palette. Nothing else may
   introduce a colour.
2. **The Skeleton ramp block** — `[data-theme='veldrath']` in the same file. It maps brand values
   onto Skeleton v5's `primary` / `secondary` / `tertiary` / `success` / `warning` / `error` /
   `surface` scales (stops 50–950 plus contrast pairs) so Skeleton components, `text-*` utilities and
   `preset-*` classes render in Veldrath colours with no per-component overrides.
3. **Component primitives** — `brand.css`. Layout, chrome and the game HUD, all built from layer 1.

**Rule:** new UI consumes layer 2 or a layer 3 primitive. A component never declares a hex.

### 3.2 Background layers

The canvas is a five-step ladder. Each step is roughly 8–10 lightness points above the previous one,
so a stack of panels stays readable without borders doing all the work.

| Layer | Token | Hex | Used for |
|---|---|---|---|
| **Layer 0** | `--vds-bg-0` | `#0C0D13` | Page canvas, game shell background |
| **Layer 1** | `--vds-bg-1` | `#14151F` | Panels, cards, dialogs, sidebars, top bar |
| **Layer 2** | `--vds-bg-2` | `#1C1D2B` | Equipment slots, nested blocks inside a panel |
| **Layer 3** | `--vds-bg-3` | `#262736` | Hover fills on rows and slots, input wells |
| **Layer 4** | `--vds-bg-4` | `#2E2F42` | Overlays and flyouts above a dialog |

`--vds-bg-deeper` (`#080910`) is declared but no component consumes it — see [§8.3](#83-declared-but-unconsumed-tokens).

### 3.3 Crimson Seal — primary accent

The action colour. Used for primary buttons, active navigation, focus rings, selection, XP, link
colour on dark surfaces, and the equipped-item border.

| Token | Hex | Use |
|---|---|---|
| `--vds-seal-light` | `#E05545` | Seal-coloured **text** at any size; hover on seal-filled elements |
| `--vds-seal` | `#C0392B` | Default fill for a primary button, active border, glow |
| `--vds-seal-dark` | `#8C2018` | Pressed fill, deep accent shadow |
| `--vds-seal-muted` | `#3D1A16` | Tinted surface behind active or selected state |

**Rules**
- Seal at `#C0392B` is 3.6:1 on Layer 0 — large text only. Any seal-coloured **text** below 18pt
  uses `--vds-seal-light` (5.1:1).
- One seal-filled button per screen region. Everything else is tonal or ghost.
- Seal is the brand accent, not an error signal — see [§3.8](#38-semantic-colours).

### 3.4 Veldrath Gold — metal accent

The identity's permanent metal: the logo, the crest frame, and every glyph that describes a
*structure* rather than an action. Gold is quiet and always present; it is not interactive.

| Token | Hex | Use |
|---|---|---|
| `--vds-gold-light` | `#D4B96A` | Prose emphasis, link hover, footer headings |
| `--vds-gold` | `#C9A84C` | Panel-header icons, nav glyphs, hero kicker, item-slot glyphs, equipped marker |
| `--vds-gold-dark` | `#A68A2E` | Shadow and pressed tone for gold elements |
| `--vds-gold-muted` | `#3D3216` | Gold-tinted fill: blockquote rule, scrollbar hover |

Gold is `secondary` in the Skeleton ramp, so `text-secondary-*` and `preset-tonal-secondary` resolve
to this scale. Gold at 500 measures 8.5:1 on Layer 0 (AAA) — but do not use it for body copy; it is
a metal, not a text colour.

### 3.5 Emberfall — heat accent

The heat and energy register: critical combat states, stamina, enchantment glow, fire-aspected
effects, and warning-adjacent highlights that are not warnings.

| Token | Hex | Use |
|---|---|---|
| `--vds-ember-light` | `#E05E35` | Ember text at small sizes; hover on ember elements |
| `--vds-ember` | `#CC4125` | Default ember fill |
| `--vds-ember-dark` | `#943018` | Pressed ember, depth |
| `--vds-ember-muted` | `#361A10` | Heat-tinted surface |

Ember is `tertiary` in the Skeleton ramp.

### 3.6 Text scale

Slightly warm white, easier on the eye than pure `#FFFFFF` against near-black.

| Token | Hex | Use | On Layer 0 |
|---|---|---|---|
| `--vds-text` | `#F0EDE8` | All default text | 16.6:1 |
| `--vds-text-muted` | `#A8A09A` | Supporting copy, subtitles, metadata, labels | 7.5:1 |
| `--vds-text-subtle` | `#6E6860` | Timestamps, captions, placeholders | 3.5:1 — large text only |
| `--vds-text-disabled` | `#4A4540` | Disabled controls | not a reading colour |

### 3.7 Border scale

| Token | Hex | Use |
|---|---|---|
| `--vds-border-subtle` | `#1E1F2D` | Dividers that should barely register |
| `--vds-border` | `#2A2B3D` | Standard panel, card, input borders |
| `--vds-border-strong` | `#3D3F58` | Hover and focus borders, dialog edge, active separators |

### 3.8 Semantic colours

Fixed meanings. Do not repurpose them, and do not use Seal where a semantic is meant.

| Meaning | Token | Hex | Muted fill | Used for |
|---|---|---|---|---|
| Success | `--vds-success` | `#5CAB7D` | `--vds-success-muted` `#1F3D2E` | Health restored, buffs, positive outcomes, online state |
| Danger | `--vds-danger` | `#E05252` | `--vds-danger-muted` `#4A1A1A` | Errors, death, destructive actions, offline state |
| Warning | `--vds-warning` | `#D4964A` | `--vds-warning-muted` `#4A3018` | Low resources, caution, system chat, reconnecting |
| Info | `--vds-info` | `#5A9ED6` | `--vds-info-muted` `#1A3250` | Neutral notifications, lore links, global chat |

> **Danger is not Seal.** `#E05252` is the functional error signal; `#C0392B` is the brand action
> colour. They are deliberately different and never interchangeable.

### 3.9 Rarity scale

Item rarity is a five-stop scale, applied as a **text colour only** today (`.rarity-common` …
`.rarity-legendary` in `brand.css`). There is no rarity frame, border or glow treatment yet.

| Rarity | Token | Hex | On Layer 1 |
|---|---|---|---|
| Common | `--vds-rarity-common` | `#C0C0C0` | 10.0:1 |
| Uncommon | `--vds-rarity-uncommon` | `#4ADE80` | 10.4:1 |
| Rare | `--vds-rarity-rare` | `#60A5FA` | 7.1:1 |
| Epic | `--vds-rarity-epic` | `#C084FC` | 6.9:1 |
| Legendary | `--vds-rarity-legendary` | `#FBBF24` | 10.9:1 |

Rarity hues are independent of the semantic palette: epic purple is not "info" and uncommon green is
not "success". Keep them that way.

### 3.10 Chat and map accents

| Token | Hex | Use |
|---|---|---|
| `--vds-pink` | `#F472B6` | Whisper chat text only |
| `--vds-indicator-player` | `#C9A84C` | Player marker on the map graph (gold — "you are the mark") |
| `--vds-indicator-occupant` | `#5A9ED6` | Occupant marker |
| `--vds-indicator-enemy` | `#E53935` | Hostile marker; distinct from `--vds-danger` |

`--vds-indicator-*` and `--vds-accent` (`#60A5FA`) are declared but not yet consumed by a component.

### 3.11 Interactive overlays

Fills applied over interactive rows, never standalone colours.

| Token | Hex | Use |
|---|---|---|
| `--vds-hover` | `#1E2030` | Pointer-over fill |
| `--vds-pressed` | `#252639` | Active press |
| `--vds-selected` | `#252639` | Selected row |

In practice the game primitives use the Layer 3 fill (`--vds-bg-3`) for row and slot hover, and a
`--vds-seal` left or full border for the selected/equipped state. Prefer those.

### 3.12 OAuth provider colours

Brand colours of third parties. Never substitute VDS tokens here.

| Provider | Token | Hex |
|---|---|---|
| Discord | `--vds-oauth-discord` | `#5865F2` |
| Google | `--vds-oauth-google` | `#4285F4` |
| Microsoft | `--vds-oauth-microsoft` | `#0078D4` |

### 3.13 Skeleton v5 ramps

Every family below is defined at `theme.css` in `[data-theme='veldrath']`, with stops 50–950 and a
matching `*-contrast-*` pair. The 500 stop is the brand value.

| Skeleton family | 500 stop | Source token |
|---|---|---|
| `primary` | `#C0392B` | Crimson Seal |
| `secondary` | `#C9A84C` | Veldrath Gold |
| `tertiary` | `#CC4125` | Emberfall |
| `success` | `#5CAB7D` | Success |
| `warning` | `#D4964A` | Warning |
| `error` | `#E05252` | Danger |
| `surface` | — | neutral ladder, below |

`surface` is the neutral ramp, and it only partly mirrors the background layers:

| Stop | Hex | Equals |
|---|---|---|
| `surface-950` | `#0C0D13` | Layer 0 |
| `surface-900` | `#1C1D2B` | Layer 2 |
| `surface-800` | `#2E2F42` | Layer 4 |
| `surface-700` | `#4A4540` | text-disabled |
| `surface-600` | `#6E6860` | text-subtle |
| `surface-400` | `#A8A09A` | text-muted |

There is **no surface stop equal to Layer 1 or Layer 3**. A panel surface is `--vds-bg-1`; a hover
fill is `--vds-bg-3`. Use `text-surface-600-400` for muted copy, but do not try to express a panel
background through the surface ramp.

### 3.14 Contrast reference

Measured with the WCAG 2.1 relative-luminance formula against the layer named. **AA** is 4.5:1 at
normal text and 3:1 at large text (≥18pt, or 14pt bold); **AAA** is 7:1. Re-measure when a token
changes — this table was recomputed on 2026-11-19 and the previous edition of this document carried
several inflated accent values.

| Foreground | Background | Ratio | Level |
|---|---|---|---|
| Text primary `#F0EDE8` | Layer 0 `#0C0D13` | 16.6:1 | AAA |
| Text primary `#F0EDE8` | Layer 2 `#1C1D2B` | 14.3:1 | AAA |
| Text secondary `#A8A09A` | Layer 0 `#0C0D13` | 7.5:1 | AAA |
| Text secondary `#A8A09A` | Layer 2 `#1C1D2B` | 6.5:1 | AA |
| Text subtle `#6E6860` | Layer 0 `#0C0D13` | 3.5:1 | AA large |
| Gold `#C9A84C` | Layer 0 `#0C0D13` | 8.5:1 | AAA |
| Gold `#C9A84C` | Layer 1 `#14151F` | 7.9:1 | AAA |
| Gold light `#D4B96A` | Layer 0 `#0C0D13` | 10.1:1 | AAA |
| Seal `#C0392B` | Layer 0 `#0C0D13` | 3.6:1 | AA large |
| Seal `#C0392B` | Layer 1 `#14151F` | 3.3:1 | AA large |
| Seal light `#E05545` | Layer 0 `#0C0D13` | 5.1:1 | AA |
| Seal light `#E05545` | Layer 2 `#1C1D2B` | 4.4:1 | AA large |
| Ember `#CC4125` | Layer 0 `#0C0D13` | 4.0:1 | AA large |
| Ember light `#E05E35` | Layer 0 `#0C0D13` | 5.4:1 | AA |
| Success `#5CAB7D` | Layer 0 `#0C0D13` | 7.0:1 | AA |
| Danger `#E05252` | Layer 0 `#0C0D13` | 5.1:1 | AA |
| Warning `#D4964A` | Layer 0 `#0C0D13` | 7.6:1 | AAA |
| Info `#5A9ED6` | Layer 0 `#0C0D13` | 6.7:1 | AA |

---

## 4. Typography

### 4.1 The five tiers

| Tier | Font | Weight | Loaded from | Used for |
|---|---|---|---|---|
| **T1 — Marquee** | Cinzel Decorative | 400 | self-hosted `CinzelDecorative-Regular.ttf` | Logo and splash moments only. Never a repeated element, never a button. |
| **T2 — Display** | Cinzel | 400–900 variable | self-hosted `Cinzel-VariableFont_wght.ttf` | Section and panel headings, nav brand text, quest names, dialog titles |
| **T3 — Reading** | Lora | 400–700, roman + italic | self-hosted variable + italic | Lore, long descriptions, article body, in-game book text |
| **T4 — UI body** | Inter | 400 / 500 / 600 | Google Fonts (network) | Every label, value, button, form field, table cell, metadata string |
| **T5 — Data** | JetBrains Mono | 100–800 variable | self-hosted variable | Keys, schema values, code, numeric stat columns |

Cinzel, Cinzel Decorative, Lora and JetBrains Mono are OFL-licensed and shipped in
`web/client/static/fonts/`. Inter is the single network font; there is no offline desktop surface to
fall back for, only the `system-ui` fallback in the stack.

### 4.2 Type scale

| Token | Size | Line height | Used for |
|---|---|---|---|
| `--vds-text-xs` | 11px | 1.4 | Captions, stat labels, timestamps, badges |
| `--vds-text-sm` | 12px | 1.4 | Metadata, secondary labels, mono values |
| `--vds-text-base` | 14px | 1.5 | Default UI text and body |
| `--vds-text-md` | 16px | 1.5 | Sub-headings, panel header titles |
| `--vds-text-lg` | 20px | 1.3 | Section headings, `.text-title` |
| `--vds-text-xl` | 28px | 1.2 | Screen titles, `.text-title-large` |
| `--vds-text-hero` | 40px | 1.1 | Hero and splash, Cinzel Decorative only |

Two conventions coexist, and both are sanctioned:

- **Utility scale** — the `--vds-text-*` tokens, used for small and mid sizes through the `.text-*`
  classes and by component primitives.
- **Semantic headings** — `.h1` / `.h2` / `.h3` are defined in `rem` (2rem / 1.5rem / 1.25rem,
  line-height 1.3, weight 600, Cinzel). They are what page templates actually use for headings, so a
  heading is **not** `--vds-text-xl`; it is `.h2` or `.h1`.

### 4.3 Heading classes

| Class | Font | Size | Colour |
|---|---|---|---|
| `.h1` | Cinzel | 2rem | `--vds-text` |
| `.h2` | Cinzel | 1.5rem | `--vds-text` |
| `.h3` | Cinzel | 1.25rem | `--vds-text` |
| `.h4` – `.h6` | Inter | inherited from the element | `--vds-text` |

`h1`–`h3` and the matching classes carry Cinzel with `letter-spacing: 0.01em`; `h4`–`h6` deliberately
switch to Inter, because small headings set in a display face become illegible.

### 4.4 Text utilities

The `.text-*` classes in `brand.css` are the sanctioned way to set type without reaching for a token.

| Class | Effect |
|---|---|
| `.text-hero` | Cinzel Decorative, `--vds-text-hero`, 400 |
| `.text-title` | Cinzel, `--vds-text-lg`, 600 |
| `.text-title-large` | Cinzel, `--vds-text-xl`, 600 |
| `.text-subtitle` | 16px, 500, muted |
| `.text-muted` | 12px, subtle |
| `.text-caption` | 11px, subtle |
| `.text-accent` | 600, seal-light |
| `.text-gold` | gold |
| `.text-lore` | Lora, line-height 1.7 |
| `.text-mono` | JetBrains Mono, 12px, muted |
| `.text-info` / `.text-success` / `.text-danger` / `.text-warning` | semantic colour |

### 4.5 Font loading

Exactly one mechanism:

- `@font-face` rules at the top of `brand.css` point at `/fonts/*.ttf` in
  `web/client/static/fonts/`, with `font-display: swap`.
- `app.html` preloads the Cinzel and Lora variable files and links the Google Fonts CSS for Inter.
- The docs site reuses the same `/fonts` directory and `theme.css` through `docfx.json`, so the
  published documentation and the game cannot drift in typography.

**Font tokens** — the stacks themselves, defined in `theme.css`:

| Token | Stack |
|---|---|
| `--vds-font-display` | Cinzel Decorative, Cinzel, serif |
| `--vds-font-heading` | Cinzel, serif |
| `--vds-font-reading` | Lora, Georgia, serif |
| `--vds-font-body` | Inter, system-ui, sans-serif |
| `--vds-font-mono` | JetBrains Mono, Cascadia Code, monospace |

### 4.6 Alternatives log

These were considered and rejected when the pairing was chosen. Kept so the decision is not
re-litigated.

| Font | Category | Why rejected |
|---|---|---|
| Almendra | Display | Narrow legibility range; conflicts with Cinzel at shared heading sizes |
| DM Serif Display | Display | Less character than Cinzel for the tone |
| Philosopher | Display | Good, but eclipsed by Cinzel |
| Cormorant Garamond | Display | Too fragile at body sizes; rendering risk |
| Crimson Pro | Display | Less distinctive than Cinzel, less warm than Lora |
| Spectral | Display | Too neutral |
| Playfair Display | Display | Cinzel better fits the inscribed-stone direction |

---

## 5. Spacing, layout and radius

### 5.1 Spacing scale

A 4px base. Tokens exist, and Tailwind utilities (`p-4`, `gap-2`, `space-y-3`) are used alongside
them — both are on the same 4px grid, so they compose without drift. Component primitives use the
tokens; page templates mostly use utilities.

| Token | Value | Typical use |
|---|---|---|
| `--vds-space-1` | 4px | Icon-to-label gap |
| `--vds-space-2` | 8px | Row gaps, tooltip padding |
| `--vds-space-3` | 12px | Panel inner gaps, game shell gutters |
| `--vds-space-4` | 16px | Default padding, dialog padding |
| `--vds-space-5` | 20px | Section padding |
| `--vds-space-6` | 24px | Page body padding |
| `--vds-space-8` | 32px | Card padding, large gaps |
| `--vds-space-10` | 40px | Section gaps, desktop page padding |
| `--vds-space-12` | 48px | Block separation |
| `--vds-space-16` | 64px | Hero and footer separation |

### 5.2 Layout constants

| Token | Value | Purpose |
|---|---|---|
| `--vds-content-max` | 1200px | Content column max-width |
| `--vds-sidebar-w` | 220px | Fixed staff-tool sidebar |
| `--vds-topbar-h` | 48px | Site top bar |

### 5.3 Game shell grid

`.game-shell` is a three-column CSS grid with fixed jobs per column. It is defined once in
`brand.css` and never re-declared per screen:

| Breakpoint | Columns |
|---|---|
| > 1200px | `minmax(220px, 260px)` · `1fr` · `minmax(280px, 340px)` |
| 900–1200px | left + centre only; the social column is hidden |
| < 900px | single column, all three stack |

The layout contract — which panel owns what — is in
[Client UI architecture](client-ui-architecture.md).

### 5.4 Breakpoints

| Name | Min-width | Meaning |
|---|---|---|
| `sm` | 640px | Single column becomes two |
| `md` | 768px | Sidebar and multi-column layouts appear |
| `lg` | 1024px | Full desktop layout |
| `xl` | 1280px | Wider content columns |

Tailwind defaults are unchanged, so `sm:` / `md:` / `lg:` mean what they usually mean.

### 5.5 Radius

| Token | Value | Use |
|---|---|---|
| `--vds-radius-sm` | 4px | Buttons, inputs, equipment slots, nav items |
| `--vds-radius-md` | 6px | Panels, cards, tooltips |
| `--vds-radius-lg` | 8px | Dialogs, auth card, feature cards |
| `--vds-radius-full` | 9999px | Pills, badges, status dots |

---

## 6. Iconography

### 6.1 Library

Lucide is the icon library, consumed as `@lucide/svelte` outline glyphs. There is no mixed icon set,
no sprite sheet and no icon font.

| Concern | Implementation |
|---|---|
| UI glyphs | `@lucide/svelte`, imported per component |
| Item glyphs | `item-icons.ts` maps equipment slot and item type to a Lucide component, plus a slot label; an empty slot renders a dashed cell with the slot glyph |
| Brand glyphs | `brand-icons.ts` vendors the Discord and GitHub paths (24×24 viewBox, Simple Icons) so the SSR bundle does not pull a whole icon set |
| OAuth buttons | Provider brand colour background with a vendored white glyph |

Illustration is not iconography. The four herald emblems in `web/client/static/images/herald/` are
1200–2500px illustration-grade artwork: they are not usable at panel sizes and are not part of the
icon system. See the [asset specs](design/asset-specs.md).

### 6.2 Sizes and stroke

| Size | Use |
|---|---|
| 14–15px | Inline actions inside dense rows |
| 16px | Inline text icons, form submit glyphs |
| 18–20px | Panel headers, primary navigation |
| 24px+ | Empty-state and feature illustrations |

Lucide's 24×24 grid with 2px nominal stroke is the reference. Keep the stroke optical weight at 1.5px
of a 24px canvas when drawing custom glyphs; rounded caps and joins; no fills mixed into an outline
set.

### 6.3 Rules

- Icons inherit `currentColor`. Never embed a fill in an icon path.
- Gold is the default glyph colour for panel headers, nav items and sheet menus
  (`.game-panel-header svg`, `.sheet-menu-item svg`, `.site-nav-link svg`). A glyph that means
  *action* inherits the button's foreground instead.
- Every icon-only control carries an accessible name (`aria-label` or `title`).

### 6.4 Prohibited

- Emoji as an icon outside lore and chat text.
- Multi-colour icons outside OAuth provider buttons.
- Raster icons below 32px — use vector.
- Mixing filled and outline glyphs in the same toolbar.

---

## 7. Components

Two layers exist, and they must not be confused:

- **Skeleton presets** — buttons, inputs, badges and surfaces come from Skeleton v5. The class
  vocabulary is `btn` plus a `preset-*` modifier, and `badge` plus a `preset-*` modifier. There are no
  `.btn-primary`, `.btn-ghost` or `.badge-accent` classes in this codebase.
- **Brand primitives** — the site chrome and the game HUD in `brand.css`. These are the classes a
  designer can rely on existing.

### 7.1 Buttons

Composed from Skeleton. The vocabulary actually in use:

| Intent | Classes | Notes |
|---|---|---|
| Primary action | `btn preset-filled-primary-500` | One per screen region |
| Secondary / neutral | `btn preset-tonal-surface` | The default for most controls |
| Destructive | `btn preset-filled-error-500` or `btn preset-tonal-error` | Never adjacent to a primary button |
| Dense variant | add `btn-sm` | In lists, toolbars, panel headers |
| Icon-only | `btn` + fixed box utilities, e.g. `h-8 w-8 p-0` | Requires `aria-label` |

**States** come from Skeleton: hover lightens the fill, focus draws the ring from
`--default-ring-width`, disabled lowers opacity and blocks pointer events. Do not hand-roll states.

### 7.2 Inputs

Skeleton inputs inside a `.card` or `.game-panel`, sized by utilities. On focus the field uses the
primary border; `color-scheme: dark` is set globally so native controls, date pickers and scrollbars
render dark. An input that needs an inline glyph uses `.input-icon`, which absolutely positions the
SVG and pads the field to 38px.

### 7.3 Panels and cards

| Class | Surface | Border | Radius | Padding |
|---|---|---|---|---|
| `.card` | `--vds-bg-1` | `--vds-border` | md | **none** — callers add `p-5`, `p-6` |
| `.game-panel` | `--vds-bg-1` | `--vds-border` | md | inner blocks supply their own |
| `.game-header` | `--vds-bg-1` | `--vds-border` | md | top bar for the game shell |
| `.game-panel-header` | — | — | — | Cinzel 16px/600 with a gold glyph |

A card is Layer 1 on a Layer 0 canvas. Layer 2 is for nested blocks *inside* a panel — equipment
slots, sub-sections — not for cards sitting on the page.

### 7.4 Site chrome

| Class | Behaviour |
|---|---|
| `.site-header` | Sticky, 92%-opaque Layer 0 with `backdrop-filter: blur(12px)`, 1px bottom border |
| `.site-brand img` | The wordmark image, 30px tall |
| `.site-nav-link` | Tonal hover and `aria-current` fill; glyphs in gold |
| `.site-footer` | Layer 0 with a top border 64px below content; headings in Cinzel gold-light |
| `.hero` | Relative container with a decorative crest positioned absolutely behind `.hero-content` |
| `.hero-kicker` | Cinzel, uppercase, gold — the small line above a hero heading |
| `.feature-card` | Card with a decorative top accent (`::before`) and a gold glyph |

### 7.5 Equipment slots

`.equip-grid` is a two-column grid. Each `.equip-slot` is Layer 2 with a 1px border, 4px radius and
5px/6px padding; hover and focus move it to Layer 3 with a strong border. An empty slot is
`.equip-slot-empty`: transparent with a **dashed** border, so "nothing here" reads as a placeholder
rather than a disabled control. Inside, `.equip-slot-head` pairs the type glyph with the item name,
and `.equip-slot-slot` is the muted slot line below.

### 7.6 Bag rows

`.bag-row` is the inventory of record, three lines deep: name, status, actions.

| Element | Rule |
|---|---|
| `.bag-row-name` | Leads the row, coloured by rarity |
| `.bag-icon` | Small supporting glyph at 85% gold — supports the name, does not lead it |
| `.bag-row-qty` | Right-aligned count |
| `.bag-row-status` | Equipped badge, rarity, type, slot, durability |
| `.bag-row-actions` | Icon-only, on their own line |
| `.bag-row-equipped` | `--vds-seal` border, plus an equipped mark in gold |

Hover and focus use Layer 3. Discard is a two-step inline confirm — never a single click.

### 7.7 Item tooltip

`.item-tooltip` is `position: fixed`, flipped and viewport-clamped so it stays readable out of the
narrow left column. Layer 4 surface, strong border, radius md. Contents: Cinzel item name in the
rarity colour, a caption line of rarity/type/slot, description, a two-column stat block, and a footer
of requirements. Bonus lines are info-blue; met requirements are muted, unmet are danger; equipped
items are marked in gold.

### 7.8 Dialogs

Secondary surfaces are centred dialogs, not drawers. Built on Bits UI `Dialog` with two primitives:

| Class | Spec |
|---|---|
| `.game-dialog-backdrop` | Fixed inset 0, `rgb(4 5 10 / 72%)`, `backdrop-filter: blur(2px)` |
| `.game-dialog` | Centred, `min(40rem, 100vw - 2rem)` wide, `min(80vh, 44rem)` tall, padding 16px, Layer 1, strong border, 8px radius, `0 24px 64px rgb(0 0 0 / 55%)` shadow |
| `.game-dialog-header` | Title left, close control right |

One scroll container per dialog; the shell behind it never scrolls. Confirmation dialogs (destructive
actions) use the same primitive with a primary and a tonal button.

### 7.9 Badges

`badge` plus a Skeleton preset, almost always with `text-xs`:

| Intent | Classes |
|---|---|
| Neutral count or tag | `badge preset-tonal-surface` |
| Positive state, e.g. `Live` | `badge preset-tonal-primary` |
| Attention, e.g. `unsaved changes` | `badge preset-tonal-warning` |
| Error severity | `badge preset-tonal-error` |

Badges are full-radius pills at 11–12px. They carry a word or a number, never an icon alone.

### 7.10 Status dots

`.status-dot` is an 8px full-radius dot. `-online` is success with a 20%-opacity success ring,
`-reconnecting` is warning, `-offline` is danger. The default fill is text-disabled. Always pair the
dot with a text label — colour alone is not an accessible state.

### 7.11 Rarity labels

Five classes, text colour only: `.rarity-common`, `.rarity-uncommon`, `.rarity-rare`,
`.rarity-epic`, `.rarity-legendary`. They are applied to names in rows, tooltips and dialogs.
Do not invent a sixth rarity and do not add borders, frames or glows without a token change.

### 7.12 Map graph rendering

The world is a cardinal graph, drawn from HTML and CSS rather than a canvas:

| Class | Spec |
|---|---|
| `.map-node-cell` | Positioned container for one node |
| `.map-connector-line` | 2px line at `--vds-border-strong` |
| `.map-connector-east` / `-west` | 13px horizontal stub |
| `.map-connector-north` / `-south` | 13px vertical stub |
| `.map-connector-blocked` | `--vds-danger-muted` |
| `.map-connector-hidden` | 30% opacity |

Layout rules live in `cardinal-layout.ts` and are unit tested; the classes render what it produces. Do
not add a diagonal connector style — diagonals have no inferable direction in the data model.

### 7.13 Long-form and lore typography

Prose is `.markdown` (see `layout.css`): links hover to gold-light, `strong` is gold-light, list
markers are gold, and blockquotes carry a 3px gold-muted rule. Lore blocks may use `.text-lore` for
Lora at 1.7 line-height. Article body never uses Cinzel below a heading.

### 7.14 Auth surfaces

`.auth-card` is the branded account surface: Layer 1 with a seal-tinted radial gradient at the top
(`rgba(192, 57, 43, .25)` fading out by 55%), 1px border, 8px radius, and the crest above the form
(`.auth-crest` — 76px, `object-fit: contain`, with a 35% seal drop-shadow). The same treatment carries
login, register, forgot-password, reset-password, verify-email, the account header and the character
roster.

---

## 8. Implementation notes

### 8.1 Where to change what

| To change | Edit | Then |
|---|---|---|
| A palette value | `:root` in `theme.css` | Re-measure [§3.14](#314-contrast-reference); the parity suite will flag the doc |
| A Skeleton component's colour | the ramp block in `theme.css` | Never override a preset in a component |
| A component's surface, border or spacing | `brand.css` | Prefer a token over a literal |
| A heading size | `.h1`/`.h2`/`.h3` in `brand.css` | Keep the rem convention |
| A colour inside a `.svelte` file | don't | Add or use a token instead |

### 8.2 Machine-readable tokens

[`tokens.json`](design/tokens.json) is the same token set in a form a design tool can import. It is a
hand-maintained mirror of `theme.css`, and the parity suite
[`design-tokens.test.ts`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/web/client/src/lib/parity/design-tokens.test.ts)
fails when `theme.css`, `tokens.json` and this document disagree. Add a token in all three places, or
the build tells you which one is behind.

### 8.3 Declared-but-unconsumed tokens

Present in `theme.css`, referenced by no component. They are reserved or leftovers; either give them
a job or delete them, but do not document them as live guidance:

| Token | Value | Status |
|---|---|---|
| `--vds-bg-deeper` | `#080910` | unused — candidate for full-bleed overlay use |
| `--vds-accent` | `#60A5FA` | unused — duplicates info/rare blue |
| `--vds-indicator-player` | `#C9A84C` | unused — map markers are drawn by the layout without it today |
| `--vds-indicator-occupant` | `#5A9ED6` | unused |
| `--vds-indicator-enemy` | `#E53935` | unused |
| `--vds-game-footer-h` | `44px` | unused — the game footer was deleted |

### 8.4 Do not

- Do not hardcode a hex, a radius or a spacing value in a component.
- Do not add a second icon library, icon font or sprite sheet.
- Do not introduce a light theme.
- Do not repurpose rarity hues as semantics or semantics as rarity.
- Do not reintroduce a desktop UI toolkit or its guidance — see [§10](#10-history--retired-surfaces).

---

## 9. Decision log

| Date | Decision | Alternatives considered | Rationale |
|---|---|---|---|
| 2026-04-16 | **Primary accent: Crimson Seal `#C0392B`** | Runic Scarlet, Bloodmoon, Cardinal, Dragon's Blood, Ironblood, Sanguine, Voidfire, Emberfall, Ash Rose, original gold `#C9A84C`, purple `#7c5cbf` | Unifies the brand across surfaces; a cooler red reads as prestige rather than pure danger; distinct from the danger semantic. |
| 2026-04-16 | **Secondary accent: Emberfall `#CC4125`** | Single accent only | A warmer orange-red gives a heat register without competing with Seal. |
| 2026-04-16 | **Display font: Cinzel** | Philosopher, Almendra, Playfair Display, Cormorant Garamond, Crimson Pro, Spectral, Rufina, IM Fell English, Eagle Lake, Metamorphous, Alegreya, EB Garamond | Carved-stone Roman capitals fit the tone and stay legible at every heading size. |
| 2026-04-16 | **Marquee font: Cinzel Decorative** | Almendra in the marquee role | Cinzel Decorative is Cinzel's natural companion for once-only hero moments; restricted to logo and splash to prevent overuse. |
| 2026-04-16 | **Reading font: Lora** | Almendra, DM Serif Display | Screen-optimised serif for long-form lore; pairs cleanly with Cinzel and Inter. |
| 2026-04-16 | **Font pairing: Cinzel + Lora + Inter** | Cinzel + Almendra + Lora; Cinzel + Inter only | Balances richness with maintainability; separates UI from long-form content without three competing display faces. |
| 2026-04-16 | **Five-layer background model** | Flatten to three layers | The ladder is calibrated against the dark canvas and eliminates drift rather than introducing it. |
| 2026-10-24 | **SvelteKit theme: Skeleton ramps mapped to VDS** | Keep Skeleton defaults; theme per page | One `[data-theme='veldrath']` block plus a brand layer restores the palette, the VDS scale and the self-hosted fonts everywhere. |
| 2026-10-24 | **Retire the Blazor/MudBlazor guidance** | Keep it as live rules beside SvelteKit guidance | Those apps are deleted; keeping MudBlazor rules makes agents write Razor for a surface that does not exist. |
| 2026-10-25 | **Retire the desktop-toolkit guidance** | Keep the old token tables as live rules | The desktop client and its content editor are deleted; Foundry in the SvelteKit app is the only editor. The historical tables were later removed entirely — see 2026-11-19. |
| 2026-11-19 | **Gold documented as a first-class token** | Leave it as an undocumented value | `--vds-gold*` is the dominant metal in the chrome — logo, panel icons, nav glyphs, kickers, equipped marks, prose emphasis — and was absent from the token reference, which is how components came to be written against a palette the doc did not describe. |
| 2026-11-19 | **Retired-surface tables deleted, not merely labelled historical** | Keep them behind a "historical" banner | A labelled table is still read as guidance. Git history and the migration plan hold the record; the document now states the rule and nothing else. |
| 2026-11-19 | **Component guidance re-expressed as Skeleton presets** | Document the hypothetical `.btn-*` / `.badge-*` vocabulary | The documented class vocabulary never existed in the codebase. Guidance now names the classes a reader can grep for. |
| 2026-11-19 | **Design docs published as a Design tab** | Keep them repo-only | The creative team works outside the repository; an unpublished brand and asset specification is a specification nobody follows. |

---

## 10. History — retired surfaces

Veldrath is web-first. Four surfaces existed before the SvelteKit app and are deleted: the
`Veldrath.Web` portal, `RealmFoundry` and `Veldrath.Auth.Blazor` (Blazor + MudBlazor), and the
Avalonia desktop client with its `RealmForge` content editor.

- **The live surface** is the SvelteKit app: `theme.css` and `brand.css`.
- **The record** is git history plus `plans/sveltekit-migration.md`, with session notes in
  `.github/agent-memory/`.
- **The rule:** do not reintroduce Razor/MudBlazor or AXAML guidance for UI, and do not add a second
  UI toolkit "for now". New UI work is Svelte 5 runes, Skeleton presets and VDS tokens.
