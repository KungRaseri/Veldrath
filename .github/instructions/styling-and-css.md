# Styling & CSS

Scope: All projects with Blazor web UI — [`Veldrath.Web`](../../Veldrath.Web/), [`Veldrath.GameClient.Components`](../../Veldrath.GameClient.Components/), [`RealmFoundry`](../../RealmFoundry/).

**Does NOT apply to** the Avalonia desktop client ([`Veldrath.Client`](../../Veldrath.Client/)) or the DB editor ([`RealmForge`](../../RealmForge/)).

---

## Styling Priority Hierarchy

When styling any UI element, follow this strict priority order. Each level is a **fallback** — only move to the next when the current level cannot achieve the result.

| Priority | Approach | Example |
|---|---|---|
| 1 | **MudBlazor CSS utilities** | `Class="mt-4 mb-2 d-flex justify-space-between"` |
| 2 | **MudBlazor theme system** | `MudTheme` palette, typography, layout properties |
| 3 | **Custom CSS classes** | `.game-panel { ... }` in a `.css` file |
| 4 | **Inline `style=""` attributes** | `style="min-height: 60vh;"` — ABSOLUTE LAST RESORT |

---

## Level 1: MudBlazor CSS Utilities (Always First)

MudBlazor ships with a comprehensive set of CSS utility classes. These are the **first and primary** tool for spacing, layout, and visual adjustments. Do not write custom CSS for anything a MudBlazor utility already covers.

### Common Utility Classes

**Spacing (margin/padding):**

| Pattern | Meaning | Example |
|---|---|---|
| `m-{0..16}` | Margin all sides | `m-4` = 1rem margin |
| `mt-{0..16}`, `mb-`, `ml-`, `mr-` | Margin top/bottom/left/right | `mt-4` |
| `mx-{0..16}`, `my-{0..16}` | Margin x-axis / y-axis | `mx-auto` |
| `p-{0..16}`, `pt-`, `pb-`, `pl-`, `pr-`, `px-`, `py-` | Padding (same pattern) | `pa-4` = 1rem padding all sides |
| `gap-{0..16}` | Gap in flex/grid containers | `gap-4` |

**Flexbox (display: flex helpers):**

| Class | Effect |
|---|---|
| `d-flex` | `display: flex` |
| `d-inline-flex` | `display: inline-flex` |
| `flex-column` | `flex-direction: column` |
| `flex-row` | `flex-direction: row` (default) |
| `justify-center`, `justify-space-between`, `justify-space-around`, `justify-end`, `justify-start` | `justify-content` |
| `align-center`, `align-start`, `align-end`, `align-stretch` | `align-items` |
| `flex-grow-0`, `flex-grow-1` | `flex-grow` |
| `flex-shrink-0`, `flex-shrink-1` | `flex-shrink` |
| `flex-wrap`, `flex-nowrap` | `flex-wrap` |

**Grid:**

Use `MudGrid` + `MudItem` for CSS Grid layouts. The `MudItem` breakpoint parameters (`xs`, `sm`, `md`, `lg`, `xl`, `xxl`) define column spans on a 12-column grid.

**Display & visibility:**

| Class | Effect |
|---|---|
| `d-none` | `display: none` |
| `d-block` | `display: block` |
| `d-inline` | `display: inline` |
| `visually-hidden` | Screen-reader-only (visible to SR, hidden visually) |

**Typography:**

| Class | Effect |
|---|---|
| `text-left`, `text-center`, `text-right`, `text-justify` | Text alignment |
| `text-nowrap` | `white-space: nowrap` |
| `text-truncate` | Truncate with ellipsis |
| `text-lowercase`, `text-uppercase`, `text-capitalize` | Text transform |
| `font-weight-bold`, `font-weight-normal`, `font-weight-light` | Font weight |

**Colors (text/background):**

| Class | Effect |
|---|---|
| `mud-text-primary`, `mud-text-secondary`, `mud-text-disabled` | Text color from theme palette |
| `mud-text-error`, `mud-text-success`, `mud-text-warning`, `mud-text-info` | Semantic text colors |
| `mud-background-primary`, `mud-background-secondary` | Background from theme palette |

**Elevation (box-shadow):**

| Class | Effect |
|---|---|
| `mud-elevation-{0..25}` | Material Design elevation shadow |
| `mud-elevation-hover-{0..25}` | Elevation applied on hover |

**Borders:**

| Class | Effect |
|---|---|
| `mud-border` | 1px solid border using `LinesDefault` color |
| `mud-border-primary`, `mud-border-secondary`, etc. | Themed border colors |
| `rounded-0`, `rounded-sm`, `rounded-lg`, `rounded-xl`, `rounded-circle` | Border radius |

Use `MudDivider` for separator lines rather than custom border CSS.

**Sizing:**

| Class | Effect |
|---|---|
| `w-100`, `w-75`, `w-50`, `w-25` | Width percentages |
| `h-100`, `h-75`, `h-50`, `h-25` | Height percentages |
| `w-auto`, `h-auto` | Auto width/height |

---

## Level 2: MudBlazor Theme Customization

When utilities are insufficient, adjust the global MudBlazor theme rather than writing one-off overrides.

- **Theme definition:** [`Veldrath.Web/Themes/VeldrathTheme.cs`](../../Veldrath.Web/Themes/VeldrathTheme.cs) — the single source of truth for the Veldrath `MudTheme`. All palette colors, typography defaults, and layout properties are defined here.
- **Palette:** `PaletteDark` defines every MudBlazor color token. Adding or changing a color here propagates to all components automatically.
- **Typography:** The `Typography` section defines font families, sizes, weights, and line heights for every MudBlazor typography level (`H1`–`H6`, `Body1`, `Body2`, `Caption`, `Button`, `Overline`, etc.).
- **Layout:** `LayoutProperties` controls global defaults like `DefaultBorderRadius`.

**When to modify the theme:**
- Adding a new semantic color used across multiple components
- Changing global font family, size, or weight defaults
- Adjusting border radius, elevation, or spacing defaults globally

**When NOT to modify the theme:**
- One-off color on a single component — use utilities instead
- Instance-specific spacing — use utility classes

For RealmFoundry and Veldrath.GameClient.Components, the host application (Veldrath.Web or the embedded host) provides the `MudThemeProvider`. These libraries consume the theme — they do not define their own.

---

## Level 3: Custom CSS Classes

Only write custom CSS when no MudBlazor utility or theme option exists. Custom CSS is a **fallback**, not the default.

### Where to Place CSS Files

| Project | Location | Purpose |
|---|---|---|
| [`Veldrath.Web`](../../Veldrath.Web/) | `wwwroot/app.css` | Global styles for the public website |
| [`Veldrath.GameClient.Components`](../../Veldrath.GameClient.Components/) | `wwwroot/css/tokens.css` | VDS design tokens (CSS custom properties) |
| [`Veldrath.GameClient.Components`](../../Veldrath.GameClient.Components/) | `wwwroot/css/game.css` | Game UI component styles |
| [`Veldrath.GameClient.Components`](../../Veldrath.GameClient.Components/) | `wwwroot/css/reconnect.css` | Reconnect overlay styles (injected by host) |
| [`RealmFoundry`](../../RealmFoundry/) | `wwwroot/app.css` | Global styles for the community portal |

**Rules:**
- Create new `.css` files only when the existing files do not logically fit the new styles.
- Do NOT create per-component `.css` files alongside `.razor` components. Group related styles in the appropriate shared CSS file.
- All CSS files live under `wwwroot/` and are referenced via `<link>` in the host application's `App.razor`.

### CSS Class Naming Conventions

- Use **lowercase with hyphens** (kebab-case) for all class names: `.game-panel`, `.sidebar-section`.
- Prefix game UI classes with the component area: `.game-*` for game view, `.shop-*` for shop, `.inventory-*` for inventory.
- Use **BEM-like naming** for variants and states:
  - Block: `.game-chat`
  - Element: `.game-chat-message`, `.game-chat-input`
  - Modifier: `.game-chat-message-system`, `.game-chat-pill-active`
- Do NOT use camelCase, PascalCase, or underscores in class names.

### VDS Design Tokens

The Veldrath Design System (VDS) defines CSS custom properties in the `:root` block. These are the **only** allowed source of design values in custom CSS.

**Key token categories:**

| Category | Prefix | Example |
|---|---|---|
| Background layers | `--vds-bg-{0..4}` | `var(--vds-bg-2)` |
| Seal (primary red) | `--vds-seal*` | `var(--vds-seal)` |
| Ember (tertiary orange) | `--vds-ember*` | `var(--vds-ember-light)` |
| Text | `--vds-text*` | `var(--vds-text-muted)` |
| Border | `--vds-border*` | `var(--vds-border)` |
| Semantic | `--vds-success`, `--vds-danger`, `--vds-warning`, `--vds-info` | `var(--vds-success-muted)` |
| Typography | `--vds-font-{display,heading,reading,body,mono}` | `var(--vds-font-heading)` |
| Font sizes | `--vds-text-{xs,sm,base,md,lg,xl,hero}` | `var(--vds-text-sm)` |
| Spacing | `--vds-space-{1..16}` | `var(--vds-space-4)` |
| Border radius | `--vds-radius-{sm,md,lg,full}` | `var(--vds-radius-md)` |

**Rules for custom CSS:**

- Always reference VDS tokens instead of hardcoded values. Never write `#1C1D2B` — write `var(--vds-bg-2)`.
- If a value you need does not have a VDS token, first consider whether MudBlazor already handles it (via theme palette or utility). If truly new, add the token to both `tokens.css` and `app.css` `:root`.
- Never override MudBlazor's `--mud-*` CSS custom properties in custom CSS. Those are generated by `MudThemeProvider`. Use the VeldrathTheme `MudTheme` to set them programmatically.
- Only fall back to hardcoded values in `tokens.css` variable definitions themselves (after the comma in `var(--vds-foo, #fallback)`).

### Valid Examples of Custom CSS

```css
/* Game-specific layout that MudBlazor utilities cannot express */
.game-layout {
    position: fixed;
    inset: 0;
    display: flex;
    flex-direction: column;
    background: var(--vds-bg-0);
    color: var(--vds-text);
    overflow: hidden;
    z-index: 10;
}

/* Component state variant using VDS tokens */
.cs-card-selected {
    border-color: var(--vds-seal);
    background: var(--vds-seal-muted);
}

/* Keyframe animation (MudBlazor has no animation utilities) */
@keyframes fade-in {
    from { opacity: 0; }
    to   { opacity: 1; }
}
```

### Invalid Examples

```css
/* BAD: overriding a MudBlazor-generated variable */
--mud-palette-primary: #ff0000;

/* BAD: hardcoding a color when a VDS token exists */
background: #1C1D2B;

/* BAD: styling that MudBlazor utilities already provide */
.my-button-spacing { margin-top: 16px; }  /* Use mt-4 instead */
```

---

## Level 4: Inline `style=""` Attributes (Absolute Last Resort)

Inline styles are the **worst option** and must only be used when:

1. No MudBlazor utility class covers the property, AND
2. The value is truly dynamic (computed at render time from C# logic), AND
3. No reasonable custom CSS class can be written (e.g., the value changes per instance in ways a class cannot express).

**Acceptable use:**

```html
@* Dynamic width based on game state — can't be done with utilities or static CSS *@
<div style="width: @(HealthPercent)%;">
```

**Unacceptable use:**

```html
@* BAD: static value that can be done with utilities *@
<MudStack style="margin-top: 16px;">  <!-- Use Class="mt-4" -->

@* BAD: static color that can be done with a VDS token class *@
<div style="color: #F0EDE8;">  <!-- Use MudText or VDS token -->
```

**If you find yourself writing an inline `style=""`, ask:**
1. Can MudBlazor utility classes do this? (Almost always yes for spacing, layout, display)
2. Can a MudBlazor component parameter do this? (`Elevation`, `Color`, `Size`, `Variant`, etc.)
3. Can the MudBlazor theme be adjusted instead?
4. Can a custom CSS class with VDS tokens do this?
5. Is the value truly dynamic from C#? If not, do not use inline styles.

---

## Reference

- [`llms.txt`](../../llms.txt) — Comprehensive MudBlazor component catalog and API reference. Consult this first for any MudBlazor question.
- [`Veldrath.Web/Themes/VeldrathTheme.cs`](../../Veldrath.Web/Themes/VeldrathTheme.cs) — Canonical `MudTheme` definition for all web projects.
- [`Veldrath.GameClient.Components/wwwroot/css/tokens.css`](../../Veldrath.GameClient.Components/wwwroot/css/tokens.css) — VDS design token definitions (CSS custom properties).
- [`docs/design-system.md`](../../docs/design-system.md) — Full Veldrath Design System reference.
