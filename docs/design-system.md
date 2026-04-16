# Veldrath Design System

> **Status:** Living document — decisions recorded in [§ Decision Log](#9-decision-log).  
> **Surfaces covered:** Veldrath.Client (Avalonia desktop), RealmForge (Avalonia editor), RealmFoundry (Blazor Server portal), Veldrath.Web (Blazor Server website).  
> **Last updated:** 2026-04-16

---

## Table of Contents

1. [Overview & Design Principles](#1-overview--design-principles)
2. [Brand Identity](#2-brand-identity)
3. [Color System](#3-color-system)
4. [Typography](#4-typography)
5. [Spacing & Grid](#5-spacing--grid)
6. [Iconography](#6-iconography)
7. [Component Guidelines](#7-component-guidelines)
8. [Implementation Notes](#8-implementation-notes)
9. [Decision Log](#9-decision-log)

---

## 1. Overview & Design Principles

The Veldrath Design System (VDS) defines the shared visual language across every player-facing surface of Veldrath. It ensures that a player moving from the game client to the community portal to the website encounters a coherent identity — the same sense of place, weight, and atmosphere regardless of platform.

### Design Principles

| Principle | Description |
|---|---|
| **Dark-first** | All surfaces are built on a near-black background. Depth is created by layering lighter surfaces, not by introducing light-mode alternatives. |
| **Restrained drama** | The palette uses controlled doses of strong colour (Crimson Seal, Emberfall) against a muted dark canvas. Every accent earns its presence. |
| **Legibility before atmosphere** | Typography is always readable. Decorative fonts appear at display sizes only; Inter carries the functional UI load at every size. |
| **Platform-coherent, not identical** | Avalonia and Blazor surfaces share tokens and design intent but are permitted platform-appropriate implementations. A card in the game client and a card on the web are recognisably the same concept, even though one is AXAML and the other is CSS. |
| **Semantic colour** | Colour communicates meaning. Red = danger/combat. Orange-red = heat/energy. Green = health/success. Blue = information. These meanings must not be violated by decorative use. |

---

## 2. Brand Identity

**Name:** Veldrath  
**Tagline register:** Epic, grounded, slightly archaic — never ironic.  
**Tone:** Prestige dark fantasy. The world is old, dangerous, and beautiful. The interface should feel like it was carved from stone and lit by firelight.

### Voice guidelines

- Headings use title case with display fonts; they feel inscribed, not typed.
- Body copy is plain, direct, and slightly formal. No "Hey!" energy.
- Error messages name the problem without panic. "Unable to connect to server." not "Uh oh! Something went wrong!"
- Lore text (in-game books, Foundry articles) can use Lora for a manuscript warmth that Inter can't provide.

---

## 3. Color System

### 3.1 Current-state audit

Before the unified system, each surface has grown its own colours. This table captures the as-is state so the migration scope is visible.

| Token | Veldrath.Client | RealmForge | RealmFoundry | Veldrath.Web |
|---|---|---|---|---|
| Window/page background | `#0C0D13` ✅ | `#1e1e1e` ⚠️ hardcoded | `#0d1117` ⚠️ | `#0d0d0d` ⚠️ |
| Primary surface | `#14151F` ✅ | `#252526` ⚠️ | `#161b22` ⚠️ | `#111` ⚠️ |
| Secondary surface | `#1C1D2B` ✅ | `#252526` ⚠️ | `#161b22` ⚠️ | `#1a1a1a` ⚠️ |
| Primary text | `#F0EDE8` ✅ | `#e0e0e0` ⚠️ | `#c9d1d9` ⚠️ | `#e0e0e0` ⚠️ |
| Muted text | `#A8A09A` ✅ | `#888` ⚠️ | `#8b949e` ⚠️ | `#aaa` ⚠️ |
| Border | `#2A2B3D` ✅ | none | `#30363d` ⚠️ | `#2a2a2a` ⚠️ |
| Brand accent | `#C9A84C` gold 🔄 | `#6a4a20` gold-ish 🔄 | `#7c5cbf` purple 🔄 | `#7c3aed` purple 🔄 |
| Link color | — | — | `#58a6ff` ⚠️ | `#a78bfa` ⚠️ |

**Legend:** ✅ Keep as-is → promote to token · ⚠️ Replace with VDS token · 🔄 Replace with Crimson Seal

---

### 3.2 Background layer model

Five layers of depth. Layer 0 is the canvas — everything sits on top of it. Each layer is approximately 8–10 lightness points above the previous.

| Token | Hex | Avalonia key | CSS variable | Used for |
|---|---|---|---|---|
| **Layer 0** | `#0C0D13` | `GameBackground0Color` | `--vds-bg-0` | Window background, page canvas |
| **Layer 1** | `#14151F` | `GameBackground1Color` | `--vds-bg-1` | Sidebars, navigation panels |
| **Layer 2** | `#1C1D2B` | `GameBackground2Color` | `--vds-bg-2` | Cards, dialogs, secondary panels |
| **Layer 3** | `#262736` | `GameBackground3Color` | `--vds-bg-3` | Input fields, hover fills, pressed surfaces |
| **Layer 4** | `#2E2F42` | `GameBackground4Color` | `--vds-bg-4` | Tooltips, flyouts, top-level overlays |

---

### 3.3 Primary accent — Crimson Seal

The primary interactive accent. Used for: CTA buttons, active navigation states, selection highlights, focus rings, XP bars, links on dark surfaces, interactive element borders on hover/focus.

| Token | Hex | Avalonia key | CSS variable | Use |
|---|---|---|---|---|
| **Seal Light** | `#E05545` | `GameAccentLightColor` | `--vds-seal-light` | Hover state on Seal-filled elements |
| **Seal** | `#C0392B` | `GameAccentColor` | `--vds-seal` | Default primary accent |
| **Seal Dark** | `#8C2018` | `GameAccentDarkColor` | `--vds-seal-dark` | Pressed state, deep accent shadow |
| **Seal Muted** | `#3D1A16` | `GameAccentMutedColor` | `--vds-seal-muted` | Tinted surface bg (active card, badge fill) |

> **Contrast note:** Seal (`#C0392B`) on Layer 0 (`#0C0D13`) yields ~3.6:1 — sufficient for large text (≥18pt/14pt bold) but not for body-size labels. Always prefer Seal Light (`#E05545`, ~5.8:1) for any accent text rendered at 14px or smaller.

---

### 3.4 Secondary accent — Emberfall

The heat/energy accent. Used for: combat critical states, enchantment glows, warning-adjacent highlights, secondary CTA buttons, stamina bars, fire-aspected visual effects.

| Token | Hex | Avalonia key | CSS variable | Use |
|---|---|---|---|---|
| **Ember Light** | `#E05E35` | `GameSecondaryAccentLightColor` | `--vds-ember-light` | Hover on Emberfall elements |
| **Ember** | `#CC4125` | `GameSecondaryAccentColor` | `--vds-ember` | Default secondary accent |
| **Ember Dark** | `#943018` | `GameSecondaryAccentDarkColor` | `--vds-ember-dark` | Pressed Emberfall, depth |
| **Ember Muted** | `#361A10` | `GameSecondaryAccentMutedColor` | `--vds-ember-muted` | Heat-tinted surface (dungeon bg, loot glow) |

---

### 3.5 Text scale

Slightly warm white — easier on the eyes against the near-black background than a pure `#FFFFFF`.

| Token | Hex | Avalonia key | CSS variable | Use |
|---|---|---|---|---|
| **Primary** | `#F0EDE8` | `GameTextPrimaryColor` | `--vds-text` | All default body text |
| **Secondary** | `#A8A09A` | `GameTextSecondaryColor` | `--vds-text-muted` | Supporting copy, subtitles, metadata |
| **Tertiary** | `#6E6860` | `GameTextTertiaryColor` | `--vds-text-subtle` | Timestamps, captions, placeholders |
| **Disabled** | `#4A4540` | `GameTextDisabledColor` | `--vds-text-disabled` | Disabled controls, inactive labels |

---

### 3.6 Border scale

| Token | Hex | Avalonia key | CSS variable | Use |
|---|---|---|---|---|
| **Subtle** | `#1E1F2D` | `GameBorderSubtleColor` | `--vds-border-subtle` | Dividers that should barely register |
| **Default** | `#2A2B3D` | `GameBorderColor` | `--vds-border` | Standard card, input, panel borders |
| **Strong** | `#3D3F58` | `GameBorderStrongColor` | `--vds-border-strong` | Hover/focus state borders, active separators |

---

### 3.7 Semantic colors

These colors carry fixed meaning throughout the system. Do not repurpose them decoratively.

| Token | Hex | Muted bg | Avalonia key | CSS variable | Meaning |
|---|---|---|---|---|---|
| **Success** | `#5CAB7D` | `#1F3D2E` | `GameSuccessColor` | `--vds-success` | Health restored, buffs, positive outcomes |
| **Danger** | `#E05252` | `#4A1A1A` | `GameDangerColor` | `--vds-danger` | Errors, death, hostile UI, destructive actions |
| **Warning** | `#D4964A` | `#4A3018` | `GameWarningColor` | `--vds-warning` | Low resources, time limits, caution states |
| **Info** | `#5A9ED6` | `#1A3250` | `GameInfoColor` | `--vds-info` | Tooltips, lore text, neutral notifications |

> **Danger vs Seal:** `GameDangerColor` (`#E05252`) is the error/hostile semantic — it remains distinct from Crimson Seal (`#C0392B`). Seal is the brand accent; Danger is a functional signal. Do not conflate them.

---

### 3.8 Interactive state overlays

Applied as background fills over interactive items (list rows, nav items, table rows). These are not new colors — they are Layer 3/4 values used in context.

| Token | Hex | Avalonia key | CSS variable | Use |
|---|---|---|---|---|
| **Hover** | `#1E2030` | `GameInteractiveHoverColor` | `--vds-hover` | Pointer-over fill |
| **Pressed** | `#252639` | `GameInteractivePressedColor` | `--vds-pressed` | Active press fill |
| **Selected** | `#252639` | `GameInteractiveSelectedColor` | `--vds-selected` | Selected item fill |

For nav items and list items with a Seal accent, combine the hover fill with a Seal-left-border (2px) to indicate the active state.

---

### 3.9 OAuth provider colors

Fixed brand colors — do not substitute VDS tokens here.

| Provider | Hex | CSS variable |
|---|---|---|
| Discord | `#5865F2` | `--vds-oauth-discord` |
| Google | `#4285F4` | `--vds-oauth-google` |
| Microsoft | `#0078D4` | `--vds-oauth-microsoft` |

---

### 3.10 WCAG contrast reference

All pairings measured against WCAG 2.1. **AA** requires 4.5:1 at normal text (< 18pt), 3:1 at large text (≥ 18pt or 14pt bold). **AAA** requires 7:1.

| Foreground | Background | Ratio | Level | Notes |
|---|---|---|---|---|
| Text Primary `#F0EDE8` | Layer 0 `#0C0D13` | ~18:1 | AAA | Main text — excellent |
| Text Primary `#F0EDE8` | Layer 2 `#1C1D2B` | ~14.9:1 | AAA | Card text — excellent |
| Text Secondary `#A8A09A` | Layer 0 `#0C0D13` | ~8:1 | AAA | Muted text on canvas |
| Text Secondary `#A8A09A` | Layer 2 `#1C1D2B` | ~6.4:1 | AA | Muted text on cards |
| Seal `#C0392B` | Layer 0 `#0C0D13` | ~3.6:1 | AA large | Use Seal Light for small text |
| Seal Light `#E05545` | Layer 0 `#0C0D13` | ~5.8:1 | AA | Safe for accent labels |
| Seal Light `#E05545` | Layer 2 `#1C1D2B` | ~4.7:1 | AA | Safe for accent labels on cards |
| Success `#5CAB7D` | Layer 0 `#0C0D13` | ~5.4:1 | AA | Health/success labels |
| Danger `#E05252` | Layer 0 `#0C0D13` | ~4.9:1 | AA | Error labels |
| Warning `#D4964A` | Layer 0 `#0C0D13` | ~4.6:1 | AA | Caution labels |
| Info `#5A9ED6` | Layer 0 `#0C0D13` | ~5.2:1 | AA | Info labels |

---

## 4. Typography

### 4.1 The five-tier font system

| Tier | Font | Weight range | Where loaded | Used for |
|---|---|---|---|---|
| **T1 — Marquee** | Cinzel Decorative | Regular (400) | Google Fonts CDN / embedded | Logo, splash screens, chapter title cards — **never** for repeated UI elements |
| **T2 — Display** | Cinzel | Regular (400), SemiBold (600) | Google Fonts CDN / embedded | Section headings, UI panel titles, nav brand text, quest names |
| **T3 — Reading** | Lora | Regular (400), Italic (400i) | Google Fonts CDN | Lore text, long descriptions, Foundry/Web article body, in-game book excerpts |
| **T4 — UI Body** | Inter | Regular (400), Medium (500), SemiBold (600) | Avalonia.Fonts.Inter (NuGet) / Google Fonts CDN | All UI labels, stats, buttons, form fields, data tables, metadata |
| **T5 — Data** | JetBrains Mono | Regular (400), Medium (500) | Google Fonts CDN / embedded | RealmForge stat columns, schema/key displays, code snippets |

**Avalonia font resource keys:**

| Key | Font | Used by |
|---|---|---|
| `GameFontBody` | Inter | All standard controls |
| `GameFontTitle` | Cinzel | `.title`, `.title-large` classes, panel headers |
| `GameFontDisplay` | Cinzel Decorative | Splash/logo elements only |
| `GameFontReading` | Lora | Lore `TextBlock` wrappers (`.lore` class) |
| `GameFontMono` | JetBrains Mono | RealmForge data cells, schema fields |

**CSS font stack (web surfaces):**

```css
--vds-font-display:  'Cinzel Decorative', 'Cinzel', serif;
--vds-font-heading:  'Cinzel', serif;
--vds-font-reading:  'Lora', Georgia, serif;
--vds-font-body:     'Inter', system-ui, sans-serif;
--vds-font-mono:     'JetBrains Mono', 'Cascadia Code', monospace;
```

---

### 4.2 Type scale

| Token | Size | Line height | Weight | Avalonia key | CSS variable | Used for |
|---|---|---|---|---|---|---|
| **XSmall** | 11px | 1.4 | 400 | `GameFontSizeXSmall` | `--vds-text-xs` | Stat names, timestamps, tiny captions |
| **Small** | 12px | 1.4 | 400 | `GameFontSizeSmall` | `--vds-text-sm` | Labels, secondary metadata |
| **Body** | 14px | 1.5 | 400 | `GameFontSizeBody` | `--vds-text-base` | Default UI text, form fields |
| **Subtitle** | 16px | 1.5 | 500 | `GameFontSizeSubtitle` | `--vds-text-md` | Sub-headings, panel section labels |
| **Title** | 20px | 1.3 | 600 | `GameFontSizeTitle` | `--vds-text-lg` | Panel titles, section headings (Cinzel) |
| **Title Large** | 28px | 1.2 | 600 | `GameFontSizeTitleLarge` | `--vds-text-xl` | Screen titles, major headings (Cinzel) |
| **Hero** | 40px | 1.1 | 400 | `GameFontSizeHero` | `--vds-text-hero` | Splash screens, web hero — Cinzel Decorative only |

---

### 4.3 Named text classes (Avalonia)

| Class | Font | Size | Weight | Color | Use |
|---|---|---|---|---|---|
| _(default)_ | Inter | Body 14 | 400 | Text Primary | All unlabelled `TextBlock` |
| `.title` | Cinzel | Title 20 | SemiBold | Text Primary | Section headings |
| `.title-large` | Cinzel | Title Large 28 | Bold | Seal Light | Major screen titles |
| `.subtitle` | Inter | Subtitle 16 | Medium | Text Secondary | Sub-headings |
| `.muted` | Inter | Small 12 | 400 | Text Tertiary | Supporting copy |
| `.caption` | Inter | XSmall 11 | 400 | Text Tertiary | Stat labels, timestamps |
| `.accent` | Inter | Body 14 | SemiBold | Seal Light | Currency, key values |
| `.lore` | Lora | Body 14 | 400 | Text Primary | Lore/flavour text |
| `.mono` | JetBrains Mono | Small 12 | 400 | Text Secondary | RealmForge data, keys |
| `.success` | Inter | Body 14 | 400 | Success | Positive outcomes |
| `.danger` | Inter | Body 14 | 400 | Danger | Errors, threats |
| `.warning` | Inter | Body 14 | 400 | Warning | Caution states |
| `.info` | Inter | Body 14 | 400 | Info | Neutral notifications |

---

### 4.4 Named text classes (CSS — web surfaces)

```css
.text-title        { font-family: var(--vds-font-heading); font-size: var(--vds-text-lg);   font-weight: 600; color: var(--vds-text); }
.text-title-large  { font-family: var(--vds-font-heading); font-size: var(--vds-text-xl);   font-weight: 600; color: var(--vds-text); }
.text-hero         { font-family: var(--vds-font-display); font-size: var(--vds-text-hero); font-weight: 400; color: var(--vds-text); }
.text-subtitle     { font-size: var(--vds-text-md); font-weight: 500; color: var(--vds-text-muted); }
.text-muted        { font-size: var(--vds-text-sm); color: var(--vds-text-subtle); }
.text-caption      { font-size: var(--vds-text-xs); color: var(--vds-text-subtle); }
.text-accent       { font-weight: 600; color: var(--vds-seal-light); }
.text-lore         { font-family: var(--vds-font-reading); line-height: 1.7; color: var(--vds-text); }
.text-mono         { font-family: var(--vds-font-mono); font-size: var(--vds-text-sm); color: var(--vds-text-muted); }
.text-success      { color: var(--vds-success); }
.text-danger       { color: var(--vds-danger); }
.text-warning      { color: var(--vds-warning); }
.text-info         { color: var(--vds-info); }
```

---

### 4.5 Font loading — web surfaces

Add to `<head>` in Blazor `App.razor`:

```html
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=Cinzel+Decorative:wght@400&family=Cinzel:wght@400;600&family=Lora:ital,wght@0,400;1,400&family=Inter:wght@400;500;600&family=JetBrains+Mono:wght@400;500&display=swap" rel="stylesheet">
```

### 4.6 Font loading — Avalonia (Veldrath.Client, RealmForge)

- **Inter:** `Avalonia.Fonts.Inter` NuGet package, already referenced. Registered via `UseInterFont()`.
- **Cinzel / Cinzel Decorative / Lora / JetBrains Mono:** Embed `.ttf` files under `Assets/Fonts/` and declare in `App.axaml`:

```xml
<Application.Resources>
  <FontFamily x:Key="GameFontTitle">avares://Veldrath.Client/Assets/Fonts#Cinzel</FontFamily>
  <FontFamily x:Key="GameFontDisplay">avares://Veldrath.Client/Assets/Fonts#Cinzel Decorative</FontFamily>
  <FontFamily x:Key="GameFontReading">avares://Veldrath.Client/Assets/Fonts#Lora</FontFamily>
  <FontFamily x:Key="GameFontMono">avares://Veldrath.Client/Assets/Fonts#JetBrains Mono</FontFamily>
</Application.Resources>
```

> Font files can be downloaded from Google Fonts and placed at `Assets/Fonts/` in each Avalonia project. Cinzel and Cinzel Decorative are variable/static OFL-licensed fonts — no attribution required in binary distributions.

---

### 4.7 Alternatives log (not chosen — retained for reference)

| Font | Category | Why considered | Why not chosen |
|---|---|---|---|
| Almendra | Display | Medieval calligraphic warmth; illuminated manuscript feel | Distinctive but narrow legibility range; would conflict with Cinzel at shared heading sizes |
| DM Serif Display | Display | Crisp editorial serif, designed for screen | Less character than Cinzel for the RPG tone; Lora covers the "screen-readable serif" role |
| Philosopher | Display | Broad classical serif; ancient/authoritative | Good but eclipsed by Cinzel once it was on the table |
| Cormorant Garamond | Display | Extreme contrast, high fashion + fantasy overlap | Too fragile at body sizes; high risk of rendering inconsistency across platforms |
| Crimson Pro | Display | Robust old-style serif | Less distinctive than Cinzel; less warm than Lora |
| Spectral | Display | Modern editorial feel | Too neutral for the Veldrath tone |
| Playfair Display | Display | Dramatic at large sizes, popular in prestige games | Cinzel better fits the inscribed-stone direction |

---

## 5. Spacing & Grid

### 5.1 Base unit

All spacing values are multiples of **4px**. Never use values that are not on this scale except for precise pixel corrections in component templates.

| Token | Value | CSS variable | Avalonia usage |
|---|---|---|---|
| `space-1` | 4px | `--vds-space-1` | `Margin="4"` |
| `space-2` | 8px | `--vds-space-2` | `Margin="8"` |
| `space-3` | 12px | `--vds-space-3` | `Padding="12"` |
| `space-4` | 16px | `--vds-space-4` | `Padding="16"` (default button padding) |
| `space-5` | 20px | `--vds-space-5` | `Padding="20"` |
| `space-6` | 24px | `--vds-space-6` | `Padding="24"` (card padding) |
| `space-8` | 32px | `--vds-space-8` | `Padding="32"` |
| `space-10` | 40px | `--vds-space-10` | Section gaps |
| `space-12` | 48px | `--vds-space-12` | Page section padding |
| `space-16` | 64px | `--vds-space-16` | Hero padding |

### 5.2 Web layout grid

| Token | Value | CSS variable | Purpose |
|---|---|---|---|
| Content max-width | 1200px | `--vds-content-max` | `max-width` of `.site-content` |
| Sidebar width | 220px | `--vds-sidebar-w` | Fixed sidebar on Foundry/Web |
| Top bar height | 48px | `--vds-topbar-h` | Fixed top navigation bar |
| Page body padding | 24px / 40px | — | 24px on mobile, 40px on ≥ md |

### 5.3 Web breakpoints

| Name | Min-width | Purpose |
|---|---|---|
| `sm` | 640px | Single-column → two-column transitions |
| `md` | 768px | Sidebar becomes visible |
| `lg` | 1024px | Full layout locked in |
| `xl` | 1280px | Optional wider content columns |

### 5.4 Corner radius

| Token | Value | CSS variable | Avalonia | Use |
|---|---|---|---|---|
| `radius-sm` | 4px | `--vds-radius-sm` | `CornerRadius="4"` | Buttons, inputs, list items |
| `radius-md` | 6px | `--vds-radius-md` | `CornerRadius="6"` | Cards, panels |
| `radius-lg` | 8px | `--vds-radius-lg` | `CornerRadius="8"` | Dialogs, modals, feature cards |
| `radius-full` | 9999px | `--vds-radius-full` | `CornerRadius="9999"` | Pills, badge corners |

---

## 6. Iconography

### 6.1 Current asset inventory

Icons currently live at `RealmForge/Resources/Icons/`. They are used within the editor for toolbar actions and entity type indicators.

### 6.2 Style guidance

- **Grid:** 24×24px base grid with 2px safe margin (20px optic area).
- **Stroke:** Line-based (outline) style at 1.5px stroke weight, rounded end caps, rounded joins.
- **Filled vs outline:** Do not mix filled icons with outline icons on the same surface or toolbar. Pick one style per surface and be consistent.
- **Color:** Icons inherit the current `Foreground` color by default. Do not embed fill colors in SVG/AXAML icon paths — let the theme drive color.
- **Sizes in use:** 16px (inline text icons), 20px (toolbar/nav), 24px (primary actions), 32px (feature icons on web cards).

### 6.3 Prohibited

- Emoji used in place of icons anywhere outside lore/chat text.
- Multi-color icons outside the OAuth provider buttons (which use brand colors intentionally).
- Raster icons at sizes below 32px (always use vector).

---

## 7. Component Guidelines

Each component entry provides: **variants**, **states**, **Avalonia class** → **CSS class** cross-reference, and **usage rules**.

---

### 7.1 Buttons

**Variants:**

| Variant | Avalonia class | CSS class | Fill | Text | When to use |
|---|---|---|---|---|---|
| Default | _(none)_ | `.btn` | Layer 2 | Text Primary | Secondary / tertiary actions |
| Primary (Seal) | `.accent` | `.btn-primary` | Seal | Layer 0 | The single primary action on a screen |
| Secondary (Ember) | `.secondary` | `.btn-secondary` | Ember | Layer 0 | Alternate CTA; combat/energy actions |
| Ghost | `.ghost` | `.btn-ghost` | Transparent | Text Primary | Toolbar actions, low-priority options |
| Danger | `.danger` | `.btn-danger` | Danger Muted | Danger | Destructive actions (delete, leave) |
| Icon-only | `.icon` | `.btn-icon` | Transparent | Text Secondary | Compact toolbars, inline actions |

**States:** Default → Hover (lighter bg + Seal border) → Pressed (Layer 4 bg) → Disabled (Layer 1 bg + Disabled text).

**Rules:**
- Maximum one `.accent` button visible at a time on any given screen/panel.
- Danger buttons must always be separated from Seal buttons — never adjacent.
- Icon buttons must have an accessible `ToolTip` (Avalonia) or `title` attribute (HTML).

---

### 7.2 Text inputs

| State | Border | Background | Caret/selection |
|---|---|---|---|
| Default | Border Default | Layer 2 | Seal |
| Hover | Border Strong | Layer 2 | Seal |
| Focus | Seal | Layer 2 | Seal (caret), Seal Dark (selection) |
| Error | Danger | Danger Muted | Danger |
| Disabled | Border Subtle | Layer 1 | — |

**CSS custom properties for inputs:**

```css
input, select, textarea {
  background: var(--vds-bg-2);
  border: 1px solid var(--vds-border);
  color: var(--vds-text);
  border-radius: var(--vds-radius-sm);
  padding: 6px 10px;
  font-family: var(--vds-font-body);
  font-size: var(--vds-text-base);
}
input:hover  { border-color: var(--vds-border-strong); }
input:focus  { border-color: var(--vds-seal); outline: none; }
input.error  { border-color: var(--vds-danger); background: var(--vds-danger-muted); }
```

---

### 7.3 Cards

| Variant | Avalonia class | CSS class | Description |
|---|---|---|---|
| Default | `.card` | `.card` | Layer 2 bg, Default border, 6px radius, 24px padding |
| Elevated | `.card-elevated` | `.card-elevated` | Layer 2 bg, Strong border — visually above surrounding cards |
| Accent | `.card-accent` | `.card-accent` | Default card + 2px Seal left border — signals an active or featured item |

**CSS:**

```css
.card          { background: var(--vds-bg-2); border: 1px solid var(--vds-border); border-radius: var(--vds-radius-md); padding: var(--vds-space-6); }
.card-elevated { background: var(--vds-bg-2); border: 1px solid var(--vds-border-strong); border-radius: var(--vds-radius-md); padding: var(--vds-space-6); }
.card-accent   { border-left: 2px solid var(--vds-seal); }
.card h3       { font-family: var(--vds-font-heading); color: var(--vds-text); margin-top: 0; }
```

---

### 7.4 Navigation

**Sidebar nav item:**

| State | Background | Left border | Text color |
|---|---|---|---|
| Default | Transparent | None | Text Primary |
| Hover | Hover overlay | None | Text Primary |
| Active | Seal Muted | 2px Seal | Text Primary (white) |

**CSS:**

```css
.nav-item         { display: block; padding: 7px 12px; border-radius: var(--vds-radius-sm); color: var(--vds-text); border-left: 2px solid transparent; transition: background 0.15s; }
.nav-item:hover   { background: var(--vds-hover); text-decoration: none; }
.nav-item.active  { background: var(--vds-seal-muted); border-left-color: var(--vds-seal); color: #fff; }
```

**Brand text in sidebar/header:** Cinzel, `--vds-text-lg`, `--vds-seal-light` color.

---

### 7.5 Badges

| Variant | Avalonia class | CSS class | Background | Text |
|---|---|---|---|---|
| Success | `.badge-success` | `.badge-success` | Success Muted | Success |
| Danger | `.badge-danger` | `.badge-danger` | Danger Muted | Danger |
| Warning | `.badge-warning` | `.badge-warning` | Warning Muted | Warning |
| Info | `.badge-info` | `.badge-info` | Info Muted | Info |
| Seal (primary) | `.badge-accent` | `.badge-accent` | Seal Muted | Seal Light |
| Muted (neutral) | `.badge` | `.badge` | Layer 3 | Text Secondary |

**CSS base:**

```css
.badge         { display: inline-block; padding: 2px 8px; border-radius: var(--vds-radius-full); font-size: var(--vds-text-xs); font-weight: 500; }
.badge-success { background: var(--vds-success-muted); color: var(--vds-success); }
.badge-danger  { background: var(--vds-danger-muted);  color: var(--vds-danger); }
.badge-warning { background: var(--vds-warning-muted); color: var(--vds-warning); }
.badge-info    { background: var(--vds-info-muted);    color: var(--vds-info); }
.badge-accent  { background: var(--vds-seal-muted);    color: var(--vds-seal-light); }
.badge         { background: var(--vds-bg-3);          color: var(--vds-text-muted); }
```

---

### 7.6 Progress bars

| Variant | Avalonia class | Fill color | Used for |
|---|---|---|---|
| Health | `.health` | `#5CAB7D` (Success) | HP bar |
| Mana | `.mana` | `#5A9ED6` (Info) | MP bar |
| XP | `.xp` | Seal Light `#E05545` | Experience bar |
| Stamina | `.stamina` | Ember `#CC4125` | Stamina/energy bar |
| Danger | `.danger` | Danger `#E05252` | Low-health state override |

**Note:** XP bars previously used gold (`#C9A84C`). They now use Seal Light — a deliberate mapping of "reward/progress" onto the new brand accent.

---

### 7.7 Dialogs & Modals

- **Overlay:** `rgba(0,0,0,0.65)` behind the dialog.
- **Surface:** Layer 4 background, Strong border, 8px radius.
- **Top accent:** 1px Seal (`#C0392B`) top border on the dialog container — anchors the brand without overwhelming the content.
- **Header:** Cinzel, Title size (20px), Text Primary.
- **Action row:** Right-aligned. Primary action uses `.accent` button. Destructive action uses `.danger` button. Cancel uses `.ghost`.
- **Max-width:** 480px for confirmation dialogs; 720px for content dialogs.

---

### 7.8 Data tables (RealmForge + RealmFoundry)

- Header row: Layer 3 background, Small 12px, Text Secondary, Cinzel or Inter SemiBold.
- Body rows: Layer 1 background, Body 14px, Text Primary.
- Alternating rows: Layer 1 / Layer 2 (subtle stripe).
- Hover row: Hover overlay (`#1E2030`).
- Selected row: Selected overlay + Seal left border.
- Cell padding: `space-2` (8px) vertical, `space-3` (12px) horizontal.
- Numeric/key cells: JetBrains Mono, Text Secondary.

---

## 8. Implementation Notes

### 8.1 CSS custom property reference

Place the following `:root` block in `app.css` for both **RealmFoundry** and **Veldrath.Web**, replacing their current hardcoded values and `--color-*` variables.

```css
:root {
  /* Background layers */
  --vds-bg-0: #0C0D13;
  --vds-bg-1: #14151F;
  --vds-bg-2: #1C1D2B;
  --vds-bg-3: #262736;
  --vds-bg-4: #2E2F42;

  /* Primary accent — Crimson Seal */
  --vds-seal-light: #E05545;
  --vds-seal:       #C0392B;
  --vds-seal-dark:  #8C2018;
  --vds-seal-muted: #3D1A16;

  /* Secondary accent — Emberfall */
  --vds-ember-light: #E05E35;
  --vds-ember:       #CC4125;
  --vds-ember-dark:  #943018;
  --vds-ember-muted: #361A10;

  /* Text */
  --vds-text:          #F0EDE8;
  --vds-text-muted:    #A8A09A;
  --vds-text-subtle:   #6E6860;
  --vds-text-disabled: #4A4540;

  /* Borders */
  --vds-border-subtle: #1E1F2D;
  --vds-border:        #2A2B3D;
  --vds-border-strong: #3D3F58;

  /* Semantic */
  --vds-success:       #5CAB7D;
  --vds-success-muted: #1F3D2E;
  --vds-danger:        #E05252;
  --vds-danger-muted:  #4A1A1A;
  --vds-warning:       #D4964A;
  --vds-warning-muted: #4A3018;
  --vds-info:          #5A9ED6;
  --vds-info-muted:    #1A3250;

  /* Interactive overlays */
  --vds-hover:    #1E2030;
  --vds-pressed:  #252639;
  --vds-selected: #252639;

  /* OAuth providers */
  --vds-oauth-discord:   #5865F2;
  --vds-oauth-google:    #4285F4;
  --vds-oauth-microsoft: #0078D4;

  /* Typography */
  --vds-font-display: 'Cinzel Decorative', 'Cinzel', serif;
  --vds-font-heading: 'Cinzel', serif;
  --vds-font-reading: 'Lora', Georgia, serif;
  --vds-font-body:    'Inter', system-ui, sans-serif;
  --vds-font-mono:    'JetBrains Mono', 'Cascadia Code', monospace;

  /* Type scale */
  --vds-text-xs:   11px;
  --vds-text-sm:   12px;
  --vds-text-base: 14px;
  --vds-text-md:   16px;
  --vds-text-lg:   20px;
  --vds-text-xl:   28px;
  --vds-text-hero: 40px;

  /* Spacing */
  --vds-space-1:  4px;
  --vds-space-2:  8px;
  --vds-space-3:  12px;
  --vds-space-4:  16px;
  --vds-space-5:  20px;
  --vds-space-6:  24px;
  --vds-space-8:  32px;
  --vds-space-10: 40px;
  --vds-space-12: 48px;
  --vds-space-16: 64px;

  /* Border radius */
  --vds-radius-sm:   4px;
  --vds-radius-md:   6px;
  --vds-radius-lg:   8px;
  --vds-radius-full: 9999px;

  /* Layout */
  --vds-content-max: 1200px;
  --vds-sidebar-w:   220px;
  --vds-topbar-h:    48px;
}
```

---

### 8.2 Avalonia ResourceDictionary token map

The Avalonia token keys in `GameColors.axaml` map directly to VDS tokens:

| VDS Token | Current Avalonia key | Action needed |
|---|---|---|
| `--vds-seal` | `GameAccentColor` | Rename value `#C9A84C` → `#C0392B` |
| `--vds-seal-light` | `GameAccentLightColor` | Rename value → `#E05545` |
| `--vds-seal-dark` | `GameAccentDarkColor` | Rename value → `#8C2018` |
| `--vds-seal-muted` | `GameAccentMutedColor` | **Add** — new resource key |
| `--vds-ember` | `GameSecondaryAccentColor` | **Add** — new resource key |
| `--vds-ember-light` | `GameSecondaryAccentLightColor` | **Add** |
| `--vds-ember-dark` | `GameSecondaryAccentDarkColor` | **Add** |
| `--vds-ember-muted` | `GameSecondaryAccentMutedColor` | **Add** |
| `--vds-bg-0` through `--vds-bg-4` | `GameBackground0Color` – `GameBackground4Color` | Keep as-is — values already match |
| `--vds-text` through `--vds-text-disabled` | `GameTextPrimaryColor` – `GameTextDisabledColor` | Keep as-is |
| `--vds-border` / `--vds-border-subtle` / `--vds-border-strong` | `GameBorderColor` etc. | Keep as-is |
| All semantic tokens | `GameSuccessColor` etc. | Keep as-is |

> The `SystemAccentColor` family (keys `SystemAccentColor`, `SystemAccentColorLight1`...) in `GameColors.axaml` must also be updated from gold values to the Seal scale so FluentAvalonia's built-in controls (NavigationView, etc.) pick up the new accent.

---

### 8.3 RealmForge migration

RealmForge currently has no design token system — colors are hardcoded inline in AXAML views. Migration steps:

1. Add a `RealmForge/Assets/Themes/ForgeColors.axaml` ResourceDictionary that imports the VDS Avalonia token set (identical or near-identical to `Veldrath.Client/Assets/Themes/GameColors.axaml` once updated).
2. Add `ForgeTheme.axaml` with the same base control styles from `GameTheme.axaml` — minus game-specific UI (progress bars, etc. that RealmForge doesn't use).
3. Replace hardcoded `#1e1e1e`, `#252526`, `#e0e0e0`, `#888`, `#6a4a20` etc. in view markup with `DynamicResource` bindings.

---

### 8.4 RealmFoundry migration

1. Replace the current `:root` block (6 `--color-*` variables) with the full `--vds-*` block from § 8.1.
2. Find/replace `var(--color-bg)` → `var(--vds-bg-0)`, `var(--color-surface)` → `var(--vds-bg-1)`, `var(--color-border)` → `var(--vds-border)`, `var(--color-text)` → `var(--vds-text)`, `var(--color-muted)` → `var(--vds-text-muted)`, `var(--color-accent)` → `var(--vds-seal)`, `var(--color-link)` → `var(--vds-seal-light)`.
3. Replace `rgba(124, 92, 191, 0.2)` nav hover → `var(--vds-hover)` or `var(--vds-seal-muted)`.
4. Update badge colours to VDS semantic palette.
5. Add Google Fonts link for Cinzel, Cinzel Decorative, Lora, Inter, JetBrains Mono.
6. Apply Cinzel to `h1`, `h2`, `h3` and `.brand a`; apply Lora to `.lore-body` contexts.
7. Apply Inter as the base `font-family` on `body`.

---

### 8.5 Veldrath.Web migration

1. Replace `body { background: #0d0d0d }` and all hardcoded hex values with `var(--vds-*)` variables (add the `:root` block from § 8.1).
2. Replace purple `#7c3aed`, `#6d28d9`, `#c4b5fd`, `#a78bfa` occurrences with `--vds-seal`, `--vds-seal-dark`, `--vds-seal-light`, `--vds-seal-light` respectively.
3. Add Google Fonts link.
4. Apply Cinzel Decorative to `.brand`, Cinzel to `h1`/`h2`/`h3`, Inter to `body`.

---

## 9. Decision Log

| Date | Decision | Alternatives considered | Rationale |
|---|---|---|---|
| 2026-04-16 | **Primary accent: Crimson Seal `#C0392B`** | Runic Scarlet, Bloodmoon, Cardinal, Dragon's Blood, Ironblood, Sanguine, Voidfire, Emberfall, Ash Rose, original gold `#C9A84C`, purple `#7c5cbf` | Unifies the brand across all surfaces; cooler red reads as prestige rather than pure danger; distinct from the Danger semantic color. |
| 2026-04-16 | **Secondary accent: Emberfall `#CC4125`** | Single accent only | Warmer orange-red provides a natural heat/energy/combat register without conflicting with Crimson Seal; the two share a color family but read as clearly separate. |
| 2026-04-16 | **Display font: Cinzel** | Philosopher, Almendra, Playfair Display, Cormorant Garamond, Crimson Pro, Spectral, Rufina, IM Fell English, Eagle Lake, Metamorphous, Alegreya, EB Garamond | Carved-stone Roman capitals align with Veldrath's tone; excellent legibility at all heading sizes; strong typographic personality without sacrificing readability. |
| 2026-04-16 | **Marquee font: Cinzel Decorative** | Almendra (for marquee use) | Cinzel Decorative is Cinzel's natural companion for once-only hero moments; restricted to splash/logo use to prevent overuse. |
| 2026-04-16 | **Reading font: Lora** | Almendra, DM Serif Display | Screen-optimized serif; comfortable at body sizes for long-form lore text and Foundry/Web article copy; pairs cleanly with Cinzel and Inter. |
| 2026-04-16 | **Font pairing strategy: Option B** | A (Cinzel + Almendra + Lora — triple display), C (Cinzel + Inter only) | Option B (Cinzel headings + Lora reading) balances richness and maintainability; adds a meaningful typographic distinction between UI and content without the complexity of three competing display fonts. |
| 2026-04-16 | **Background layer model: kept from Veldrath.Client** | Flatten to 3 layers, redesign from scratch | The 5-layer model is already well-calibrated against the dark canvas and is in active use in the most mature surface; standardising on it eliminates drift rather than introducing it. |
