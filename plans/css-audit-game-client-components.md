# CSS Audit: Veldrath.GameClient.Components — MudBlazor Utility First

> **Date:** 2026-07-22  
> **Scope:** `Veldrath.GameClient.Components/wwwroot/css/` (tokens.css, game.css, reconnect.css)  
> **Reference:** Already-audited `Veldrath.Web/wwwroot/app.css`, MudBlazor v9.7.0 utilities, VDS Design System (`docs/design-system.md`)  
> **Status:** Audit & Recommendations — no changes made

---

## Executive Summary

The three CSS files in `Veldrath.GameClient.Components` total ~2,834 lines of custom CSS. The audit identifies:

- **~30% of game.css rules** can be simplified or replaced by MudBlazor utility classes directly in Razor markup
- **~15 VDS tokens** directly overlap with MudBlazor palette custom properties and could be deprecated
- **The settings page** (`game-settings-*` classes, ~240 lines) is the biggest offender — it implements custom toggle switches, sliders, and selects that MudBlazor components already provide
- **reconnect.css** is 308 lines that could be reduced to ~80 lines by removing duplication with game.css and using MudBlazor utilities
- **tokens.css** is well-structured but has consistency gaps with `app.css` and includes tokens that duplicate MudBlazor palette values

The core tension in this audit: the Game UI is deeply custom by nature (tilemap, combat, chat, inventory) and many custom classes are **genuinely necessary**. The goal is not to eliminate custom CSS but to **stop using custom CSS for things MudBlazor already provides** — flex layouts, spacing, typography basics, and interactive component styling.

---

## Section 1: tokens.css Audit

### 1.1 Tokens That Overlap with MudBlazor Palette

These VDS tokens map directly to MudBlazor palette concepts. The recommendation is to **keep the VDS token** (it's the design system's canonical name) but **consider aliasing to MudBlazor palette values** where they represent the same intent.

| VDS Token | MudBlazor Equivalent | Overlap | Recommendation |
|---|---|---|---|
| `--vds-success` (`#5CAB7D`) | `--mud-palette-success` | Near-complete (both green, slightly different hex) | Keep VDS token; it's game-specific. Do not alias — the hex differs and VDS has WCAG-verified contrast against VDS backgrounds. |
| `--vds-danger` (`#E05252`) | `--mud-palette-error` | Semantic overlap | Keep VDS token; distinct from Seal brand accent per design rules. |
| `--vds-warning` (`#D4964A`) | `--mud-palette-warning` | Semantic overlap | Keep VDS token. |
| `--vds-info` (`#5A9ED6`) | `--mud-palette-info` | Semantic overlap | Keep VDS token. |
| `--vds-text` (`#F0EDE8`) | `--mud-palette-text-primary` | Same role | **Consider aliasing.** The VDS text primary is a warm white; if the MudBlazor theme's `TextPrimary` is set to match, these could be unified. Current `app.css` already uses `--mud-palette-text-primary` for body color. |
| `--vds-text-muted` (`#A8A09A`) | `--mud-palette-text-secondary` | Same role | Same as above — consider aliasing if theme is aligned. |
| `--vds-text-disabled` (`#4A4540`) | `--mud-palette-text-disabled` | Same role | Same as above. |
| `--vds-bg-0` through `--vds-bg-4` | `--mud-palette-background`, `--mud-palette-surface` | Background layering | The 5-layer model is unique to VDS and has no MudBlazor equivalent. **Keep all 5.** |

**Verdict:** The semantic overlap exists but is mostly benign. VDS semantic colors (`success`, `danger`, `warning`, `info`) have **different hex values** than MudBlazor defaults — they're tuned for the VDS dark background palette. Do not alias these.

The text color tokens (`--vds-text`, `--vds-text-muted`, `--vds-text-disabled`) are the strongest candidates for unification with MudBlazor palette — but only if the MudBlazor theme is configured to match VDS values.

### 1.2 Tokens That Are Genuinely VDS-Specific (Keep)

| Token Category | Tokens | Justification |
|---|---|---|
| Brand accents | `--vds-seal-*`, `--vds-ember-*` | No MudBlazor equivalent. These are the game's identity. |
| Font stacks | `--vds-font-*` (5 families) | No MudBlazor equivalent. Custom typography stack. |
| Text size scale | `--vds-text-xs` through `--vds-text-hero` | MudBlazor has typography classes but no equivalent CSS custom property scale. |
| Spacing scale | `--vds-space-1` through `--vds-space-16` | **Should be deprecated for web surfaces** per design-system.md §5.5. Spacing should use MudBlazor `pa-{n}`, `ma-{n}`, `gap-{n}` utilities. |
| Border radius | `--vds-radius-*` | MudBlazor has `rounded-sm`, `rounded`, `rounded-lg`, `rounded-xl`, `rounded-circle`, `rounded-pill`. VDS `radius-sm` (4px) ≈ MudBlazor `rounded-sm`, VDS `radius-md` (6px) has no MudBlazor equivalent, VDS `radius-lg` (8px) ≈ MudBlazor `rounded`, VDS `radius-full` ≈ MudBlazor `rounded-pill`. **Keep as convenience but prefer MudBlazor classes in markup.** |
| Layout tokens | `--vds-content-max`, `--vds-sidebar-w`, `--vds-topbar-h` | Application-specific layout constants. |
| Interactive states | `--vds-hover`, `--vds-pressed`, `--vds-selected` | No MudBlazor equivalent at the token level. |
| OAuth colors | `--vds-oauth-*` | Fixed brand colors — must stay. |
| `--vds-pink` | `#f472b6` | Used only for whisper chat coloring. Game-specific. |

### 1.3 Consistency Gaps: tokens.css vs. app.css

| Issue | tokens.css | app.css | Action |
|---|---|---|---|
| **Missing tokens** | Has full VDS set (bg-0 through bg-4, seal, ember, text, border, semantic, spacing, radius, fonts, layout) | Has only a subset (bg-3, seal-muted, ember-muted, gold-muted, text-subtle, semantic-muted, hover/pressed/selected, oauth, fonts) | `app.css` should import the canonical `:root` block from `tokens.css`. Currently `app.css` duplicates a partial set. |
| **`--vds-gold-muted`** | Missing | `#3d3216` | Add to tokens.css if still in use; otherwise remove from app.css. |
| **Body defaults** | Uses `--vds-bg-0` and `--vds-text` | Uses `--mud-palette-background` and `--mud-palette-text-primary` | `app.css` correctly delegates to MudBlazor palette. `tokens.css` body rule is for the game UI context where MudBlazor theme may not be fully applied. **This divergence is intentional.** |
| **`#blazor-error-ui`** | Uses `--vds-danger-muted` and `--vds-danger` | Uses `--vds-danger-muted` but `--mud-palette-error` for border/color | `app.css` version is more correct — border/color should use MudBlazor palette tokens. Update `tokens.css` version to match. |

### 1.4 Base Element Styles

The `.btn` class in tokens.css (lines 100–117) implements a custom button system:

```css
.btn { display: inline-block; padding: .5rem 1.25rem; border-radius: var(--vds-radius-md); ... }
.btn-primary { background: var(--vds-seal); color: #fff; border-color: var(--vds-seal-dark); }
```

**Recommendation:** Replace `.btn` usage with MudBlazor `<MudButton>` components in Razor markup. The `.btn-oauth-*` pattern from `app.css` (which uses `!important` overrides on `.mud-button-filled`) is the correct approach. The `.btn-primary` class should map to `<MudButton Variant="Variant.Filled" Color="Color.Primary">`.

### 1.5 Spacing Tokens Deprecation

Per `docs/design-system.md` §5.5, spacing should be delegated to MudBlazor utilities. The `--vds-space-*` tokens exist in `tokens.css` because `game.css` references them heavily. **Recommendation:** Do NOT remove `--vds-space-*` tokens yet — they're referenced ~120+ times across game.css. Instead, make the migration a separate task: replace `var(--vds-space-N, ...)` with MudBlazor utility classes in Razor markup, then remove the tokens.

---

## Section 2: game.css Audit

### 2.1 Layout (`.game-layout`, `.game-container`, `.game-main`, `.game-sidebar`, `.game-center`)

**Lines:** 6–65

| Class | Current CSS | MudBlazor Replacement | Verdict |
|---|---|---|---|
| `.game-layout` | `position:fixed; inset:0; display:flex; flex-direction:column; background/color/font; overflow:hidden; z-index:10` | `mud-d-flex flex-column overflow-hidden` plus custom properties for bg/color/font | **Keep custom.** Combines too many properties; position fixed + inset + z-index has no utility equivalent. |
| `.game-container` | `display:flex; flex-direction:column; height:100vh; width:100vw; overflow:hidden` | `mud-d-flex flex-column mud-height-full mud-width-full overflow-hidden` | **Replace with utilities.** All 5 properties have MudBlazor equivalents. |
| `.game-main` | `display:flex; flex:1; overflow:hidden; min-height:0` | `mud-d-flex flex-1 overflow-hidden` + inline `min-height:0` | **Replace with utilities.** `min-height:0` is a flex child reset — may need to stay as inline style. |
| `.game-sidebar` | `width:260px; min-width:260px; background; border-right; display:flex; flex-direction:column; overflow-y:auto; overflow-x:hidden` | `mud-d-flex flex-column overflow-y-auto overflow-x-hidden` + custom width/background/border | **Mixed.** Flex utilities can replace the display/direction/overflow parts. Width and styling stay custom. |
| `.game-center` | `flex:1; display:flex; flex-direction:column; overflow:hidden; position:relative` | `flex-1 mud-d-flex flex-column overflow-hidden position-relative` | **Replace with utilities.** |
| `.game-loading-zone` | Complex centered flex with gap | `mud-d-flex flex-column align-items-center justify-center gap-4` | **Replace with utilities.** Most properties map directly. |

### 2.2 Game Header (`.game-header-*`)

**Lines:** 80–164

| Class | Key Properties | Recommendation |
|---|---|---|
| `.game-header` | `display:flex; align-items:center; padding; background; border-bottom; gap; flex-shrink:0; min-height:56px` | `mud-d-flex align-items-center gap-4 flex-shrink-0` can replace 4 properties. Padding/bg/border/min-height remain custom. |
| `.game-header-character` | `display:flex; align-items:center; gap` | **Replace entirely** with `mud-d-flex align-items-center gap-2`. |
| `.game-header-name` | Font family, size, weight, color | **Keep.** Typography class pattern. |
| `.game-header-class` | Font, padding, background, border-radius | **Keep.** Badge-like styling. |
| `.game-header-level` | Font, color, weight | **Keep.** Typography. |
| `.game-header-bars` | `display:flex; align-items:center; gap; flex:1; max-width` | `mud-d-flex align-items-center gap-3 flex-1` can replace 3 properties. |
| `.game-header-gold` | `display:flex; align-items:center; gap; font; color; margin-left:auto` | `mud-d-flex align-items-center gap-1 ml-auto` replaces layout. Typography stays. |
| `.game-header-zone-badge` | Font, color, padding, background, border-radius, weight, nowrap | **Keep.** Custom badge style. |

**Overall for Header:** ~30% of rules can move to MudBlazor utilities. The typography and color rules are genuinely VDS-specific.

### 2.3 Status Bars (`.status-bar`, `.status-bar-track`, `.status-bar-fill`)

**Lines:** 166–207

These are custom progress-bar-like components. MudBlazor provides `<MudProgressLinear>` which could theoretically be used, but the game status bars have specific styling (monospace font, label + track + text layout, custom fill colors) that make a component approach impractical.

| Class | Recommendation |
|---|---|
| `.status-bar` | **Keep custom.** The flex layout with mono font and specific gap is intentional game UI. |
| `.status-bar-label` | **Keep.** Typography + layout. |
| `.status-bar-track` | **Keep.** Custom track styling with specific height (10px) and border. |
| `.status-bar-fill` | **Keep.** Transition + dynamic width. |
| `.status-bar-text` | **Keep.** Typography. |

### 2.4 Sidebar (`.sidebar-*`)

**Lines:** 209–280

| Class | Recommendation |
|---|---|
| `.sidebar-section` | **Keep.** Combines padding, border-bottom. |
| `.sidebar-section h3`, `h4`, `p` | **Keep.** Typography rules. |
| `.sidebar-list`, `.sidebar-list-item` | **Keep.** Custom list styling. Could theoretically use MudList, but the game sidebar has specific hover behavior and compact styling. |
| `.sidebar-actions` | `display:flex; flex-direction:column; gap` → `mud-d-flex flex-column gap-2` for flex parts. Padding stays. |
| `.sidebar-btn` | `width:100%; text-align:center` → `mud-width-full mud-text-center`. |

### 2.5 Tilemap Viewport

**Lines:** 282–466

The tilemap system is **genuinely custom** and cannot be replaced by MudBlazor utilities. It uses:

- CSS Grid (15×15) with responsive tile sizing via `--tile-size` custom property and media queries
- Custom tile type backgrounds (`.tile-grass`, `.tile-wall`, `.tile-water`, etc.) with hardcoded hex values
- Entity indicator circles with absolute positioning and animations (`.tile-indicator-player`, `.tile-indicator-enemy`, `.tile-indicator-occupant`)

**Issues found:**

1. **Hardcoded tile background colors** (lines 398–428): `#3a3a5c`, `#2a1a1a`, `#1a3a5c`, `#5c4a1a`, `#4a4a3a`, `#3a2a1a`, `#0d0d1a`, `#1a1a2e` — these are game content colors (grass=purple-gray, wall=dark-red, water=dark-blue, door=gold-brown, path=olive, dirt=brown, void=near-black, empty=navy). They should be normalized:
   - Define them as VDS tokens (e.g., `--vds-tile-grass`, `--vds-tile-wall`, etc.) or use semantic mappings
   - Currently they're magic numbers with no relationship to the design system

2. **Entity indicator colors** (lines 444–459): `#c9a84c` (gold, player), `#5a9ed6` (blue, occupant), `#e53935` (red, enemy) — `#5a9ed6` maps to `--vds-info`, `#e53935` is close to but not exactly `--vds-danger` (`#E05252`). These should reference VDS tokens.

3. **Hover info card** (lines 340–369): Uses `--vds-accent` on line 368 which **does not exist** in tokens.css. This is a bug — it falls through to the fallback `#60A5FA`. Should use `--vds-info` or `--vds-seal-light`.

**Verdict: Keep all tilemap CSS.** Fix the hardcoded colors and the `--vds-accent` bug.

### 2.6 Chat System

**Lines:** 468–662

The chat system is custom by design. It implements channel pills, message layout with timestamp/sender/text columns, and a custom input field.

| Class | Recommendation |
|---|---|
| `.game-chat` | `display:flex; flex-direction:column; flex-shrink:0` → utilities. Background and min/max height stay custom. |
| `.game-chat-pills` | `display:flex; gap:2px; flex-shrink:0` → utilities. |
| `.game-chat-pill` | **Keep.** Custom pill styling with complex hover/active transitions. |
| `.game-chat-messages` | `flex:1; overflow-y:auto; display:flex; flex-direction:column; gap:2px` → utilities for flex parts. Scrollbar styling stays. |
| `.game-chat-message` | **Keep.** Message row layout with baseline alignment. |
| `.game-chat-timestamp`, `.game-chat-channel`, `.game-chat-sender`, `.game-chat-text` | **Keep.** Typography + layout. |
| `.game-chat-message-system`, `-whisper`, `-global`, `-zone` | **Keep.** Semantic color overrides. |
| `.game-chat-input` | `display:flex; align-items:center; gap` → utilities. |
| `.game-chat-input-field` | **Keep.** Custom input styling matching VDS input component spec. |
| `.game-chat-send-btn` | **Keep.** Custom button styling, though this should really be a `<MudButton>` with VDS color overrides. |

**Issue:** `.game-chat-message-zone .game-chat-sender` (line 605) uses fallback `#94a3b8` which is Tailwind's `slate-400` — not a VDS color. Should be `var(--vds-text-muted, #A8A09A)`.

### 2.7 Combat UI

**Lines:** 664–762

Deeply custom game UI. The combat screen uses `radial-gradient` backgrounds, text shadows, and specific enemy/result/action layouts.

| Class | Recommendation |
|---|---|
| `.game-combat` | `flex:1; display:flex; flex-direction:column; align-items:center; justify-content:center` → utilities. The `radial-gradient` background and padding/gap stay custom. |
| `.game-combat-enemy h2` | **Keep.** Dramatic typography with text-shadow. |
| `.game-combat-log` | `text-align:center; display:flex; align-items:center; justify-content:center` → utilities. |
| `.game-combat-result` | **Keep.** Animation + typography. |
| `.game-combat-actions` | `display:flex; gap` → utilities. |
| `.game-combat-victory` | **Keep.** Fixed overlay with animation. |

### 2.8 Footer (`.game-footer-*`)

**Lines:** 764–862

| Class | Recommendation |
|---|---|
| `.game-footer` | `display:flex; align-items:center; padding; background; border-top; gap; flex-shrink:0; min-height; position:relative` | `mud-d-flex align-items-center gap-3 flex-shrink-0 position-relative` replaces 4 properties. |
| `.game-footer-left`, `.game-footer-center`, `.game-footer-right` | Flexbox utilities applicable. |
| `.game-footer-dot`, `.dot-connected`, `.dot-degraded`, `.dot-disconnected`, `.dot-reconnecting` | **Keep.** Custom status dot system with animations. |
| `.game-footer-reconnecting-banner` | **Keep.** Custom banner with gradient and slide-in animation. |
| `.game-footer-ping`, `.game-footer-connid` | **Keep.** Typography + layout. |

### 2.9 Reconnect Overlay & Toast (`.reconnect-*` in game.css)

**Lines:** 899–1034

These duplicate the functionality in `reconnect.css`. The game.css classes (`.reconnect-overlay`, `.reconnect-overlay-card`, etc.) are used by the `ReconnectOverlay.razor` Blazor component, while `reconnect.css` styles the framework-managed `#components-reconnect-modal`.

**Issue:** Both files define nearly identical visual styles (card with same bg, border, radius, padding, shadow; icon at 48px; title in Cinzel; countdown in monospace; button variants). This is ~135 lines of duplication.

**Recommendation:** Extract shared reconnect visual styles into a single set of CSS custom properties or a shared class that both the Razor component and the framework modal can reference.

### 2.10 Action Bar (`.action-bar`, `.action-btn-*`)

**Lines:** 1036–1106

Custom action buttons for the game. These should be MudBlazor `<MudButton>` components styled with VDS colors:

| Class | Current | MudBlazor Replacement |
|---|---|---|
| `.action-btn` (base) | Custom styling | `<MudButton Variant="Variant.Filled" Size="Size.Small">` with custom CSS overrides for VDS colors |
| `.action-btn-attack` | `--vds-seal` bg | `<MudButton Color="Color.Primary">` (if MudBlazor Primary is Seal) |
| `.action-btn-defend` | `--vds-info` bg, `#3a7ab6` border, `#6db0ea` hover | `<MudButton Color="Color.Info">` |
| `.action-btn-flee` | `--vds-bg-4` bg | `<MudButton Variant="Variant.Outlined">` |
| `.action-btn-respawn` | `--vds-warning` bg, `#b07830` border, `#e4a85a` hover | `<MudButton Color="Color.Warning">` |

**Hardcoded colors:** `#3a7ab6` (defend border), `#6db0ea` (defend hover), `#b07830` (respawn border), `#e4a85a` (respawn hover) — should be referenced via VDS tokens or derived from them.

### 2.11 Game Panel / Overlay System

**Lines:** 1108–1183

The `.game-panel-backdrop` + `.game-panel` + `.game-panel-header` + `.game-panel-body` system is essentially a custom modal implementation.

**Recommendation:** This should be replaced by **MudBlazor `<MudDialog>`**. The dialog system provides:
- Backdrop with configurable click behavior
- Card styling with elevation
- Header with title and close button
- Scrollable body content
- Animation support
- Focus trapping and accessibility

If replacing with MudDialog is not feasible (e.g., for in-game overlay panels that must coexist with the game UI), at minimum:
- Remove the custom backdrop implementation (`.game-panel-backdrop`)
- Use `<MudPaper>` for the card surface
- Use MudBlazor utilities for flex/gap within the panel

### 2.12 Character Select (`.cs-*`)

**Lines:** 1215–1312

| Class | Recommendation |
|---|---|
| `.cs-container` | `max-width:640px; margin:32px auto; padding:24px` — could use `mud-container` or `mx-auto` + `mud-width-*` utilities. |
| `.cs-card` | `display:flex; align-items:center; justify-content:space-between; padding; background; border; border-radius; transition; cursor:pointer` | Flex properties → utilities. Styling stays custom. |
| `.cs-card-selected` | Border + bg color | **Keep.** Semantically meaningful state. |
| `.cs-list` | `display:flex; flex-direction:column; gap` → utilities. |
| `.cs-footer` | `display:flex; gap; justify-content:center` → utilities. |

### 2.13 Game Map / Region View

**Lines:** 1341–1550

| Class | Recommendation |
|---|---|
| `.game-map-page` | `flex:1; display:flex; flex-direction:column; overflow-y:auto` → utilities. |
| `.game-map-header` | `display:flex; align-items:center; gap; margin-bottom; padding-bottom; border-bottom` | Flex/gap → utilities. Borders and padding stay. |
| `.game-map-zones` | CSS Grid with `repeat(auto-fill, minmax(180px, 1fr))` | **Keep.** This grid layout is specific. |
| `.game-map-zone-card` | Complex card with flex, gap, padding, transition, position | Flex parts → utilities. |
| `.game-map-legend` | `display:flex; gap; flex-wrap:wrap; justify-content:center` → utilities. |

### 2.14 Hotbar Ability Quick-Slots

**Lines:** 1552–1649

Genuinely custom game UI (48×48px ability slots with key labels, icons, cooldown overlays). **Keep entirely.** The flex/gap on `.action-bar-wrapper` and `.hotbar` can use utilities.

### 2.15 Inventory Overlay

**Lines:** 1681–1887

| Class | Recommendation |
|---|---|
| `.inventory-overlay` | `display:flex; flex-direction:column; gap` → utilities. |
| `.equipment-grid` | `display:grid; grid-template-columns:1fr 1fr; gap` → CSS Grid stays. Gap → utility. |
| `.equipment-slot` | Complex card-like slot | **Keep.** Custom styling. |
| `.inventory-grid` | `display:grid; grid-template-columns:repeat(auto-fill, minmax(120px, 1fr)); gap` | **Keep grid.** Gap → utility. |
| `.inventory-slot` | Card-like item slot with hover | **Keep.** |
| `.inventory-tooltip-*` | Tooltip card | **Keep.** Custom tooltip styling. |
| `.rarity-common` through `.rarity-legendary` | Text colors | **Keep.** Game-specific rarity colors. But they should reference VDS tokens. |

**Issue:** Rarity colors (`.rarity-common` through `.rarity-legendary`) use hardcoded hex values (`#C0C0C0`, `#4ADE80`, `#60A5FA`, `#C084FC`, `#FBBF24`). These are game content colors and should be defined as VDS tokens (e.g., `--vds-rarity-common`, `--vds-rarity-uncommon`, etc.).

### 2.16 Shop Overlay

**Lines:** 1888–2000

Similar pattern to inventory — custom game UI with items, pricing, messages. **Keep custom.** The flex/gap patterns can use utilities.

### 2.17 Journal / Quest Log

**Lines:** 2002–2156

Custom quest tracking UI. **Keep custom.** Flex/gap patterns can use utilities.

### 2.18 Settings Page (`*.game-settings-*`)

**Lines:** 2158–2378 — **~220 lines — Biggest Offender**

The settings page implements entirely custom form controls:
- `.settings-slider` — custom range slider with WebKit/Moz thumb styling
- `.settings-toggle` — custom toggle switch with ::before pseudo-element
- `.settings-select` — custom select dropdown
- `.settings-row`, `.settings-label`, `.settings-control`, `.settings-value` — custom form layout

**All of these have MudBlazor equivalents:**
- `<MudSlider<T>>` for the slider
- `<MudSwitch<T>>` for the toggle
- `<MudSelect<T>>` for the dropdown
- `<MudTextField<T>>` for any text inputs
- `<MudForm>` + `<MudGrid>`/`<MudStack>` for form layout

**Recommendation: Delete all `game-settings-*` CSS classes and rebuild the settings page using MudBlazor form components.** This would eliminate ~220 lines of CSS and provide better accessibility (keyboard navigation, screen reader support, focus management) for free.

### 2.19 Responsive Breakpoints

**Lines:** 1651–1670, 2382–2398

Two `@media (max-width: 1024px)` blocks. These adjust sidebar width, status bar min-width, header bar max-width, combat enemy bar width, equipment grid columns, inventory grid columns, and settings padding.

**Recommendation:** These are fine for game-specific responsive adjustments. MudBlazor's responsive utilities (`mud-hidden-*-up/down`) can supplement but not replace these layout-specific overrides.

### 2.20 Duplicate Utility Patterns

These patterns appear repeatedly and could be extracted to shared utility classes:

| Pattern | Occurrences | Shared Class Suggestion |
|---|---|---|
| `display:flex; flex-direction:column; gap: var(--vds-space-N)` | ~15+ times | Use MudBlazor `mud-d-flex flex-column gap-{n}` in markup |
| `display:flex; align-items:center; gap: var(--vds-space-N)` | ~20+ times | Use MudBlazor `mud-d-flex align-items-center gap-{n}` in markup |
| `color: var(--vds-text-subtle); font-style: italic` | ~8 times (empty states) | Extract `.game-empty-state` class |
| `border: 1px solid var(--vds-border); border-radius: var(--vds-radius-md); padding: var(--vds-space-3)` | ~6 times (section panels) | Use `<MudPaper Elevation="0" Outlined="true">` or extract `.game-section` |
| `font-family: var(--vds-font-heading); font-size: var(--vds-text-sm); text-transform: uppercase; letter-spacing: 1px; color: var(--vds-text-muted)` | ~5 times (h4 labels) | Extract `.game-section-label` class |

### 2.21 Hardcoded Color Values

All hardcoded hex values in `game.css` that should reference design tokens:

| Line(s) | Current Value | Should Reference | Context |
|---|---|---|---|
| 398–428 | `#3a3a5c`, `#2a1a1a`, `#1a3a5c`, `#5c4a1a`, `#4a4a3a`, `#3a2a1a`, `#0d0d1a`, `#1a1a2e` | New `--vds-tile-*` tokens | Tile type backgrounds |
| 444 | `#c9a84c` | `--vds-warning` or new token | Player indicator (gold) |
| 452 | `#5a9ed6` | `--vds-info` | Occupant indicator |
| 457 | `#e53935` | `--vds-danger` (different hex: `#E05252`) | Enemy indicator |
| 368 | `--vds-accent` | **Bug** — token doesn't exist. Use `--vds-info` | Tile info card entity |
| 473 | `#0d0d1a` | Should be `--vds-bg-0` or match tile-void | Chat background |
| 486 | `#0a0a14` | Custom or `--vds-bg-0` | Chat pills bar bg |
| 617 | `#0f0f1e` | Custom or `--vds-bg-1` | Chat input bg |
| 1080 | `#3a7ab6` | Derived from `--vds-info` | Defend button border |
| 1085 | `#6db0ea` | Derived from `--vds-info` | Defend button hover |
| 1100 | `#b07830` | Derived from `--vds-warning` | Respawn button border |
| 1101 | `#1a1a2e` | `--vds-bg-0` or custom | Respawn button text |
| 1105 | `#e4a85a` | Derived from `--vds-warning` | Respawn button hover |
| 1629 | `#6dcb8d` | Derived from `--vds-success` | Hotbar ready hover border |
| 1807–1821 | `#C0C0C0`, `#4ADE80`, `#60A5FA`, `#C084FC`, `#FBBF24` | New `--vds-rarity-*` tokens | Rarity colors |

### 2.22 Animations

| Animation | Lines | Duplicated? | Recommendation |
|---|---|---|---|
| `@keyframes spin` | 76–78 | Used by `.spinner` and `.spinner-small` | **Keep.** Single definition, two consumers. |
| `@keyframes pulse-enemy` | 463–466 | No | **Keep.** |
| `@keyframes fade-in-combat` | 720–723 | Similar to `fade-in` but includes translateY | **Keep.** Distinct animation. |
| `@keyframes fade-in` | 1121–1124 | Used by `.game-panel-backdrop`, `.game-combat-victory`, `.reconnect-overlay`, `.shop-message`, `.game-settings-status` | **Keep.** Widely reused. |
| `@keyframes panel-enter` | 1139–1142 | Used by `.game-panel`, `.reconnect-overlay-card`, `.inventory-tooltip`, `.journal-quest-detail` | **Keep.** Widely reused. |
| `@keyframes pulse-degraded` | 840–843 | Similar to `pulse-connecting` | **Could unify** into a single `pulse` animation with CSS variable for colors. |
| `@keyframes pulse-reconnecting` | 845–848 | Unique (includes scale) | **Keep.** |
| `@keyframes slide-down-banner` | 884–887 | No | **Keep.** |
| `@keyframes slide-in-toast` | 1026–1029 | No | **Keep.** |
| `@keyframes fade-out-toast` | 1031–1034 | No | **Keep.** |
| `@keyframes pulse-connecting` | 1210–1213 | Similar to `pulse-degraded` | **Could unify.** |

The three pulse animations (`pulse-enemy`, `pulse-degraded`, `pulse-connecting`, `pulse-reconnecting`) are all variations on the same concept. They could be unified, but the different timing and property variations make unification low-value. **Leave as-is.**

---

## Section 3: reconnect.css Audit

### 3.1 Can This Be Replaced by MudBlazor Components?

**No.** The `#components-reconnect-modal` is managed by `blazor.web.js` (the Blazor framework). It adds/removes CSS classes (`.components-reconnect-show`, `.components-reconnect-hide`, `.components-reconnect-failed`, `.components-reconnect-rejected`) based on the SignalR circuit state. You cannot replace the DOM element or its class toggling behavior.

However, the **content inside** the modal is Razor markup (`ReconnectModal.razor`), which could use MudBlazor components internally (e.g., `<MudButton>` for the action buttons, `<MudText>` for typography, `<MudProgressCircular>` for the waiting indicator). This would reduce the CSS needed for those elements.

### 3.2 Duplication with game.css

| reconnect.css | game.css equivalent | Duplication |
|---|---|---|
| `.reconnect-modal-overlay` (lines 24–35) | `.reconnect-overlay` (lines 901–913) | Both are `position:fixed; inset:0; display:flex; flex-direction:column; align-items:center; justify-content:center` with a dark backdrop. reconnect.css uses `rgba(0,0,0,0.65)` + `blur(6px)`, game.css uses `rgba(0,0,0,0.8)` + `blur(4px)`. |
| `.reconnect-modal-card` (lines 39–54) | `.reconnect-overlay-card` (lines 915–928) | Nearly identical: same bg, border, border-radius, padding, flex, gap, min/max width, shadow. reconnect.css adds a Seal glow to the box-shadow. |
| `.reconnect-modal-title` (lines 79–86) | `.reconnect-overlay-title` (lines 936–943) | Identical typography. |
| `.reconnect-modal-message` (lines 90–96) | `.reconnect-overlay-message` (lines 945–951) | Identical typography. |
| `.reconnect-modal-countdown` (lines 100–113) | `.reconnect-overlay-countdown` (lines 953–963) | Nearly identical. reconnect.css adds `display:flex` wrapper. |
| `.reconnect-modal-btn` (lines 164–181) | `.reconnect-overlay-btn` (lines 973–984) | Similar but not identical. reconnect.css uses `display:inline-flex`, Cinzel font, smaller font-size, `min-width:140px`. game.css uses `display` not specified (inherits block), Inter font, larger font-size. |
| `.reconnect-modal-btn-primary` (lines 183–193) | `.reconnect-overlay-btn-primary` (lines 986–994) | Same Seal bg + hover pattern. reconnect.css adds `box-shadow` on hover. |
| `.reconnect-modal-btn-secondary` (lines 195–205) | `.reconnect-overlay-btn-secondary` (lines 996–1004) | Same bg-3 + hover pattern. reconnect.css adds `border-color: var(--vds-border-strong)`. |

**~200 lines of duplication between the two files.** The visual intent is identical — both show a reconnection modal in the VDS style.

### 3.3 Simplification Opportunities

| Current CSS | MudBlazor Utility Replacement |
|---|---|
| `.reconnect-modal-overlay` flex properties | `mud-d-flex flex-column align-items-center justify-center` in markup |
| `.reconnect-modal-card` flex properties | `mud-d-flex flex-column align-items-center gap-4` in markup |
| `.reconnect-modal-actions` flex properties | `mud-d-flex gap-3 justify-center mud-width-full flex-wrap` in markup |
| `.reconnect-modal-btn` display | `mud-d-inline-flex align-items-center justify-center` in markup |
| `.reconnect-modal-countdown` flex | `mud-d-flex align-items-center gap-1 justify-center` in markup |
| `.reconnect-modal-dots` display | `mud-d-inline-flex gap-1` in markup (though dots are very custom) |
| State visibility rules (lines 209–242) | **Keep.** These are framework state selectors — cannot be moved to utilities. |
| Animations (lines 246–274) | **Keep.** These are specific to framework state classes. |

### 3.4 Duplicate Animation Definitions

`reconnect.css` defines `@keyframes reconnect-fade-in` (lines 261–264) which is identical to `@keyframes fade-in` in game.css (lines 1121–1124). Both are simple `opacity: 0 → 1`. **These can be unified** — use a single `fade-in` keyframe.

`@keyframes reconnect-card-enter` (lines 271–274) is similar to `@keyframes panel-enter` (lines 1139–1142) but uses different transform values:
- `reconnect-card-enter`: `translateY(8px) scale(0.97)`
- `panel-enter`: `scale(0.95) translateY(10px)`

These could be unified by accepting the different values, but the visual difference is intentional. **Keep separate** but document the variation.

### 3.5 Recommendation for reconnect.css

1. **Extract shared reconnect token set** to a CSS file both the Razor component and the framework modal can reference:
   ```css
   :root {
     --reconnect-overlay-bg: rgba(0, 0, 0, 0.65);
     --reconnect-card-bg: var(--vds-bg-2);
     --reconnect-card-border: var(--vds-border-strong);
     --reconnect-card-radius: var(--vds-radius-lg);
     --reconnect-card-padding: var(--vds-space-8);
     --reconnect-card-min-w: 320px;
     --reconnect-card-max-w: 420px;
   }
   ```

2. **Use MudBlazor `<MudButton>` inside the reconnect modal** to eliminate `reconnect-modal-btn-*` classes.

3. **Unify `reconnect-fade-in` with `fade-in`** from game.css.

4. **Inline flex/gap properties** into Razor markup via MudBlazor utilities.

**Estimated reduction:** 308 lines → ~80 lines (state visibility rules + framework-specific animations + custom dots).

---

## Section 4: Cross-Cutting Recommendations

### 4.1 Token Unification Strategy

**Current state:**
- `tokens.css` — full VDS `:root` block (all tokens, 83 lines)
- `app.css` — partial VDS `:root` block (subset of tokens, 30 lines)
- `game.css` — references tokens via `var(--vds-*, fallback)` pattern
- `reconnect.css` — references tokens via `var(--vds-*, fallback)` pattern

**Target state:**
- `tokens.css` — **canonical** `:root` block with ALL VDS tokens (add `--vds-gold-muted`, `--vds-rarity-*`, `--vds-tile-*`, `--vds-pink` already exists)
- `app.css` — remove the partial `:root` block; instead, import or rely on the fact that `tokens.css` is loaded via the RCL
- `game.css` — no changes needed to token references
- `reconnect.css` — no changes needed to token references

**Action:** Verify that `tokens.css` is loaded in both `Veldrath.Web` and `RealmFoundry` before their `app.css`. If so, remove the duplicate `:root` block from `app.css`.

### 4.2 Which MudBlazor Utilities Should Become Standard

These utility categories should be the **first choice** for developers working on RCL components:

| Category | Primary Utilities | When to Use |
|---|---|---|
| **Layout** | `mud-d-flex`, `flex-column`, `align-items-center`, `justify-center`, `justify-space-between`, `flex-1`, `flex-shrink-0`, `flex-grow-0` | Any time you need flex layout |
| **Spacing** | `gap-{n}`, `pa-{n}`, `pt-{n}`, `pb-{n}`, `pl-{n}`, `pr-{n}`, `ma-{n}`, `mt-{n}`, `mb-{n}`, `ml-{n}`, `mr-{n}` | Any margin, padding, or flex gap |
| **Sizing** | `mud-width-full`, `mud-height-full` | Full-width/height elements |
| **Text** | `mud-text-center`, `mud-text-left`, `mud-text-right`, `mud-text-nowrap` | Text alignment and wrapping |
| **Overflow** | `overflow-hidden`, `overflow-y-auto`, `overflow-x-hidden` | Scroll control |
| **Visibility** | `mud-hidden`, `mud-d-none` | Toggling visibility |
| **Position** | `position-relative`, `position-absolute`, `position-fixed` | Positioning context |

**Anti-pattern:** Do NOT create custom CSS classes that only set `display: flex`, `gap: 8px`, `padding: 16px`, etc. Use MudBlazor utilities directly in the Razor `.razor` file's `class` attribute.

**Acceptable pattern:** Custom CSS classes that combine **multiple properties including VDS-specific values** (colors, fonts, borders) where the combination is semantically meaningful (e.g., `.game-header` combines bg, border, padding, gap, flex-shrink, min-height).

### 4.3 Migration Strategy (Prioritized)

**Phase 1 — Low Risk, High Impact (start here):**

1. **Delete settings page CSS** (lines 2158–2378, ~220 lines) and rebuild with MudBlazor components
2. **Replace `.action-btn-*` classes** with `<MudButton>` components styled with VDS colors
3. **Replace `.btn` / `.btn-primary`** usage in RCL components with `<MudButton>`
4. **Fix `--vds-accent` bug** on line 368 (add token or replace with existing token)

**Phase 2 — Unification (medium risk):**

5. **Unify reconnect styles** — extract shared tokens, reduce reconnect.css
6. **Add missing tokens** to tokens.css (`--vds-gold-muted`, `--vds-rarity-*`, `--vds-tile-*`)
7. **Normalize hardcoded colors** — replace all magic hex values with VDS token references (see §2.21)
8. **Remove duplicate `:root` block** from `app.css` — verify tokens.css is loaded first

**Phase 3 — Utility Migration (higher effort, per-component):**

9. **Inline MudBlazor utilities** for flex, gap, padding, alignment — component by component
10. **Extract shared empty-state class** (`.game-empty-state`) for the 8 repeated `color:subtle; font-style:italic` patterns
11. **Extract shared section-label class** (`.game-section-label`) for the 5 repeated h4 patterns

**Phase 4 — Architecture (evaluate before committing):**

12. **Consider MudDialog for game panels** — evaluate whether in-game overlay panels can use MudBlazor Dialog infrastructure
13. **Evaluate MudProgressLinear** for status bars — if the component can be styled to match VDS, it would provide accessibility benefits
14. **Document CSS architecture conventions** in a new `.github/instructions/` file

### 4.4 New CSS Architecture Conventions

Document these rules for the RCL:

1. **Prefer MudBlazor utilities for:** `display:flex`, `flex-direction`, `align-items`, `justify-content`, `gap`, `padding`, `margin`, `text-align`, `overflow`, `width`/`height`, `position`
2. **Use VDS custom properties for:** colors, fonts, radii, and any property that must match the design system
3. **Custom CSS classes are for:** combining VDS tokens into semantic game UI patterns that don't have a MudBlazor equivalent
4. **Never hardcode hex colors** — always reference a `--vds-*` or `--mud-palette-*` custom property
5. **Never write custom toggle/switch/slider/select CSS** — use MudBlazor `<MudSwitch>`, `<MudSlider>`, `<MudSelect>` components
6. **One CSS class = one semantic concept** — don't create `.game-header-and-footer-shared` that mixes concerns
7. **Reconnect visibility is framework-managed** — don't duplicate reconnect styles; share via CSS custom properties

### 4.5 Summary Metrics

| File | Current Lines | Estimated Savings | Primary Method |
|---|---|---|---|
| `tokens.css` | 126 | ~10 lines | Remove duplicate `:root` block from app.css; add missing tokens |
| `game.css` | 2,400 | ~500–700 lines | MudBlazor utilities in markup (~300 lines), delete settings page (~220 lines), extract shared patterns (~50 lines), normalize hardcoded colors (~30 lines) |
| `reconnect.css` | 308 | ~200 lines | Unify with game.css reconnect styles, MudBlazor utilities, MudButton components |
| **Total** | **~2,834** | **~710–910 lines (25–32%)** | |

---

## Appendix A: Complete Hardcoded Color Inventory

| Hex Value | File | Line(s) | Semantic Meaning | Recommended Token |
|---|---|---|---|---|
| `#3a3a5c` | game.css | 398 | Tile: grass | `--vds-tile-grass` |
| `#2a1a1a` | game.css | 401 | Tile: wall | `--vds-tile-wall` |
| `#1a3a5c` | game.css | 405 | Tile: water | `--vds-tile-water` |
| `#5c4a1a` | game.css | 409 | Tile: door | `--vds-tile-door` |
| `#4a4a3a` | game.css | 413 | Tile: path | `--vds-tile-path` |
| `#3a2a1a` | game.css | 417 | Tile: dirt | `--vds-tile-dirt` |
| `#0d0d1a` | game.css | 421, 473 | Tile: void / Chat bg | `--vds-tile-void` |
| `#1a1a2e` | game.css | 425, 1101 | Tile: empty / Respawn text | `--vds-tile-empty` |
| `#c9a84c` | game.css | 444 | Player indicator | `--vds-warning` (or new `--vds-indicator-player`) |
| `#5a9ed6` | game.css | 452 | Occupant indicator | `--vds-info` |
| `#e53935` | game.css | 457 | Enemy indicator | `--vds-danger` (note: different hex `#E05252`) |
| `#0a0a14` | game.css | 486 | Chat pills bar bg | `--vds-bg-0` or new token |
| `#0f0f1e` | game.css | 617 | Chat input bg | `--vds-bg-1` or new token |
| `#3a7ab6` | game.css | 1080 | Defend button border | Derived from `--vds-info` |
| `#6db0ea` | game.css | 1085 | Defend button hover | Derived from `--vds-info` |
| `#b07830` | game.css | 1100 | Respawn button border | Derived from `--vds-warning` |
| `#e4a85a` | game.css | 1105 | Respawn button hover | Derived from `--vds-warning` |
| `#6dcb8d` | game.css | 1629 | Hotbar ready hover border | Derived from `--vds-success` |
| `#C0C0C0` | game.css | 1807 | Rarity: common | `--vds-rarity-common` |
| `#4ADE80` | game.css | 1810 | Rarity: uncommon | `--vds-rarity-uncommon` |
| `#60A5FA` | game.css | 1813 | Rarity: rare | `--vds-rarity-rare` |
| `#C084FC` | game.css | 1816 | Rarity: epic | `--vds-rarity-epic` |
| `#FBBF24` | game.css | 1819 | Rarity: legendary | `--vds-rarity-legendary` |
| `#94a3b8` | game.css | 605 | Zone chat sender fallback | `--vds-text-muted` |

## Appendix B: Identified Bugs

| # | File | Line | Issue | Fix |
|---|---|---|---|---|
| 1 | game.css | 368 | `var(--vds-accent, #60A5FA)` — `--vds-accent` does not exist in tokens.css | Replace with `var(--vds-info, #5A9ED6)` |
| 2 | game.css | 605 | Fallback `#94a3b8` is Tailwind slate-400, not VDS | Replace with `var(--vds-text-muted, #A8A09A)` |
| 3 | tokens.css vs app.css | — | `--vds-gold-muted` exists in app.css but not tokens.css | Add to tokens.css or remove from app.css |
