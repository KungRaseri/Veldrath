# Media Breakpoint Strategy — Veldrath Web Projects

> **Date:** 2026-07-01  
> **Status:** Plan / For Review  
> **Scope:** RealmFoundry (Blazor Server community portal) + Veldrath.Web (Blazor Server website) + forward-looking game layout provisions

---

## Table of Contents

1. [Background & Current State](#1-background--current-state)
2. [Breakpoint Conventions](#2-breakpoint-conventions)
3. [RealmFoundry — Responsive Changes](#3-realmfoundry--responsive-changes)
4. [Veldrath.Web — Responsive Changes](#4-veldrathweb--responsive-changes)
5. [Game Layout Provisions (Forward-Looking)](#5-game-layout-provisions-forward-looking)
6. [Implementation Order](#6-implementation-order)
7. [Testing Guidance](#7-testing-guidance)

---

## 1. Background & Current State

### 1.1 What We're Starting From

Neither [`RealmFoundry/wwwroot/app.css`](RealmFoundry/wwwroot/app.css) nor [`Veldrath.Web/wwwroot/app.css`](Veldrath.Web/wwwroot/app.css) contains **any** `@media` queries. Both projects use pure custom CSS with shared VDS design tokens (CSS custom properties prefixed `--vds-`). No Bootstrap, no Tailwind.

Additionally, [`Veldrath.GameClient.Components/wwwroot/css/game.css`](Veldrath.GameClient.Components/wwwroot/css/game.css) defines the embedded (Avalonia-hosted) game UI with a fixed 260px sidebar and no responsive handling — but is currently also **lacking any `@media` queries**.

### 1.2 Design System Breakpoints

From [`docs/design-system.md`](docs/design-system.md) §5.2–5.3:

| Name | Min-width | Purpose |
|---|---|---|
| `sm` | 640px | Single-column → two-column transitions |
| `md` | 768px | Sidebar becomes visible |
| `lg` | 1024px | Full layout locked in — **primary target** |
| `xl` | 1280px | Optional wider content columns |

### 1.3 Already Responsive (No Changes Needed)

These components use `auto-fill` / `auto-fit` CSS Grid and naturally reflow across all viewport widths:

- **RealmFoundry:** `.content-type-grid`, `.feature-cards`
- **Veldrath.Web:** `.features`, `.community-links`
- **Veldrath.Web:** `.feature` cards

### 1.4 Critical Break Risks

1. **RealmFoundry `.content-detail-shell`** — CSS Grid `1fr + 420px` split pane. At ~620px, the detail pane overlaps. Needs stacking fallback.
2. **RealmFoundry fixed sidebar** — 220px always visible with no collapse behavior.
3. **RealmFoundry `.page-body`** — `max-width: 960px` with fixed padding.
4. **RealmFoundry tables** — `.data-table`, `.content-list-table` have no horizontal overflow handling.
5. **Veldrath.Web nav** — Horizontal `.main-nav` with 5-7 links, no hamburger menu.
6. **Veldrath.Web `.site-content`** — `max-width: 1200px`, needs graceful shrinkage.
7. **Planned game layout** — 260-280px sidebar + game area at `100vh` with no responsive provisions.

---

## 2. Breakpoint Conventions

### 2.1 CSS Custom Property Naming

Add four breakpoint custom properties to the `:root` block of each project's `app.css`:

```css
:root {
  /* ── Breakpoints ─────────────────────────────────── */
  --vds-bp-sm:  640px;
  --vds-bp-md:  768px;
  --vds-bp-lg: 1024px;
  --vds-bp-xl: 1280px;
}
```

**Note:** CSS custom properties cannot be used inside `@media` query condition expressions. The spec doesn't allow `@media (min-width: var(--vds-bp-sm))`. These tokens exist for **documentation** and for use in `calc()` or other property values where a width reference is useful. In `@media` blocks, hardcode the pixel values to keep the token values as the single source of truth comment:

```css
/* sm: 640px — see :root --vds-bp-sm */
@media (min-width: 640px) { ... }
```

### 2.2 Mobile-First Approach

All responsive rules follow a **mobile-first** pattern:

```
Base styles → smallest screens (no media query)
  ↓
@media (min-width: 640px)  { ... }  /* sm — two-column transitions */
  ↓
@media (min-width: 768px)  { ... }  /* md — sidebar visible */
  ↓
@media (min-width: 1024px) { ... }  /* lg — full layout */
  ↓
@media (min-width: 1280px) { ... }  /* xl — wider content columns (optional) */
```

This means:
- **Base styles** (outside any `@media` block) target phones in portrait mode (< 640px)
- Each breakpoint **adds** or **overrides** properties — never subtracts
- Each breakpoint builds cumulatively on the previous one

### 2.3 File Organization

All responsive rules live in the same `app.css` as the base styles they override. Each `@media` block is placed immediately after the base rule it modifies — not in a separate "responsive" section at the bottom. This keeps related rules together and makes maintenance easier.

Example pattern:

```css
/* Base: phone portrait */
.sidebar { display: none; }

/* md: sidebar visible */
@media (min-width: 768px) {
    .sidebar { display: flex; }
}
```

---

## 3. RealmFoundry — Responsive Changes

### 3.1 File: [`RealmFoundry/wwwroot/app.css`](RealmFoundry/wwwroot/app.css)

### 3.1.1 Breakpoint Tokens

**Add** to `:root` block:

```css
--vds-bp-sm:  640px;
--vds-bp-md:  768px;
--vds-bp-lg: 1024px;
--vds-bp-xl: 1280px;
```

### 3.1.2 Sidebar Collapse Strategy

**Current behavior:** `.sidebar` is a 220px fixed-width flex child, always visible. `.layout` uses `display: flex` with sidebar + content side by side.

**Target behavior:**
- **< 768px (no md):** Sidebar hidden (push to off-canvas or hide entirely). A hamburger toggle button appears in a top bar. Opening the sidebar overlays it.
- **≥ 768px (md):** Sidebar visible as fixed flex child (current behavior).

**Implementation plan:**

```css
/* Base (mobile): sidebar hidden */
.sidebar {
    display: none;  /* override the current display: flex */
}

/* Mobile top bar with hamburger */
.top-bar-mobile {
    display: flex;
    align-items: center;
    gap: 0.75rem;
    padding: 0.5rem 1rem;
    background: var(--vds-bg-1);
    border-bottom: 1px solid var(--vds-border);
}

.sidebar-toggle {
    background: transparent;
    border: 1px solid var(--vds-border);
    border-radius: var(--vds-radius-sm);
    color: var(--vds-text);
    cursor: pointer;
    padding: 0.25rem 0.5rem;
    font-size: 1.2rem;
    line-height: 1;
}

/* Sidebar overlay (when toggled open on mobile) */
.sidebar.open {
    display: flex;
    position: fixed;
    inset: 0;
    z-index: 100;
    width: 100%;
    max-width: 280px;
    box-shadow: 4px 0 24px rgba(0,0,0,0.5);
}

.sidebar-backdrop {
    display: none;
    position: fixed;
    inset: 0;
    background: rgba(0,0,0,0.5);
    z-index: 99;
}

.sidebar.open ~ .sidebar-backdrop,
.sidebar-backdrop.active {
    display: block;
}

/* md (≥768px): sidebar always visible */
@media (min-width: 768px) {
    .sidebar {
        display: flex;
        position: static;
        width: var(--vds-sidebar-w);
        max-width: none;
        box-shadow: none;
    }

    .top-bar-mobile { display: none; }
    .sidebar-toggle   { display: none; }
    .sidebar-backdrop { display: none !important; }
}
```

**Blazor interaction note:** The hamburger toggle requires a small JavaScript interop or a Blazor `@onclick` handler that toggles a CSS class on the sidebar element. The simplest approach: a C# `bool _sidebarOpen` field in the layout component that conditionally adds the `.open` class.

**CSS-only alternative (no Blazor changes):** Use the `:target` pseudo-class or a hidden checkbox hack if no JS/C# interaction is desired. However, Blazor's `@onclick` is the cleaner approach and more maintainable.

### 3.1.3 Content Detail Split-Pane Stacking

**Current behavior:** `.content-detail-shell` uses `grid-template-columns: 1fr 420px` — two columns side by side. Below ~620px, the 420px detail pane squeezes against the list pane.

**Target behavior:**
- **≥ 768px:** Two-column split (current behavior)
- **< 768px:** Single column — show the list first, detail below (or detail replaces list via navigation)

**Implementation plan:**

```css
/* Base (mobile): stack list above detail */
.content-detail-shell {
    display: flex;
    flex-direction: column;
    flex: 1;
    overflow: hidden;
}

/* On mobile, list pane fills available space */
.content-list-pane {
    flex: 1;
    border-right: none;
    border-bottom: 1px solid var(--vds-border);
}

/* Detail pane is hidden or minimized on mobile until a row is selected */
.content-detail-pane {
    flex: 1;
    display: none;  /* hidden until selection */
}

.content-detail-pane.active {
    display: block;
}

/* md (≥768px): side-by-side grid */
@media (min-width: 768px) {
    .content-detail-shell {
        display: grid;
        grid-template-columns: 1fr 420px;
    }

    .content-list-pane {
        border-right: 1px solid var(--vds-border);
        border-bottom: none;
    }

    .content-detail-pane {
        display: block;  /* always visible in split view */
    }
}
```

**Navigation alternative:** On mobile, selecting a row navigates to a dedicated detail route (e.g., `/Submissions/{id}`) rather than showing it in the split pane. This avoids the complexity of toggling panes and works better for deep linking. If this approach is chosen, the split-pane media query simply doesn't render the detail pane on mobile and the list's click handler navigates instead.

### 3.1.4 Page Body Padding / Max-Width

**Current behavior:** `.page-body` has `max-width: 960px` and `padding: 2rem 1.5rem`.

**Target behavior:** Progressive padding increase and max-width relaxation at larger viewports.

```css
.page-body {
    padding: 1.25rem 1rem;
    max-width: 100%;  /* full width on mobile */
}

/* sm (≥640px): slightly more breathing room */
@media (min-width: 640px) {
    .page-body {
        padding: 1.5rem 1.5rem;
    }
}

/* md (≥768px): restore max-width constraint */
@media (min-width: 768px) {
    .page-body {
        padding: 2rem 2rem;
        max-width: 960px;
    }
}
```

### 3.1.5 Tables — Responsive Strategy

**Current behavior:** `.data-table` and `.content-list-table` use standard `<table>` with no overflow handling. On narrow screens, tables will overflow horizontally, breaking the layout.

**Target behavior:** Horizontal scroll wrapper on small screens. No column hiding needed for RealmFoundry tables (they typically have 3-5 columns).

**Implementation plan:**

Wrap tables in a scrollable container. The simplest approach: add a wrapper class and an `@media` rule.

```css
/* Base: always allow horizontal scroll for tables */
.table-responsive {
    width: 100%;
    overflow-x: auto;
    -webkit-overflow-scrolling: touch;
}

/* Ensure tables inside the wrapper don't collapse weirdly */
.table-responsive table {
    min-width: 100%;
}

/* md (≥768px): remove the visual scroll hint when screen is wide enough */
@media (min-width: 768px) {
    .table-responsive {
        overflow-x: visible;
    }
}
```

**Note:** The content-list-table has sticky headers (`position: sticky; top: 0`). Ensure the scroll wrapper doesn't break sticky positioning — the wrapper must be the scroll container (not the table itself). The current `.list-scroll` already handles this correctly for the list pane table.

### 3.1.6 Forms — Width Adjustments

**Current behavior:** `.submission-form-shell` has `max-width: 720px`. `.auth-card` has `max-width: 400px`.

**Target behavior:** Both are already narrow enough for mobile. Minor padding adjustments.

```css
/* Base (mobile): slightly tighter padding */
.auth-card {
    margin: 1.5rem auto;
    padding: 1.5rem;
}

.submission-form-shell {
    max-width: 100%;
    padding: 0 0.5rem;
}

/* md (≥768px): restore desktop padding */
@media (min-width: 768px) {
    .auth-card {
        margin: 3rem auto;
        padding: 2rem;
    }

    .submission-form-shell {
        max-width: 720px;
        padding: 0;
    }
}
```

### 3.1.7 Top Bar (`.top-bar`)

**Current behavior:** `.top-bar` has `padding: 0.75rem 1.5rem`.

**Target behavior:** Adjust padding for smaller screens.

```css
.top-bar {
    padding: 0.5rem 1rem;
    font-size: 0.8rem;
}

@media (min-width: 768px) {
    .top-bar {
        padding: 0.75rem 1.5rem;
        font-size: 0.85rem;
    }
}
```

### 3.1.8 Field List (`.field-list`) — Detail Pane

**Current behavior:** `.field-list` uses `grid-template-columns: 130px 1fr` — a two-column key-value layout.

**Target behavior:** On very narrow screens (< 400px), stack to single column.

```css
.field-list {
    display: grid;
    gap: 0.4rem 0.75rem;
    grid-template-columns: 1fr;  /* single column on mobile */
}

.field-list dt {
    color: var(--vds-text-muted);
    font-size: 0.72rem;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.06em;
}

@media (min-width: 400px) {
    .field-list {
        grid-template-columns: 130px 1fr;
    }

    .field-list dt {
        padding-top: 0.1rem;
        font-size: 0.8rem;
        text-transform: none;
        letter-spacing: 0;
    }
}
```

---

## 4. Veldrath.Web — Responsive Changes

### 4.1 File: [`Veldrath.Web/wwwroot/app.css`](Veldrath.Web/wwwroot/app.css)

### 4.1.1 Breakpoint Tokens

**Add** to `:root` block:

```css
--vds-bp-sm:  640px;
--vds-bp-md:  768px;
--vds-bp-lg: 1024px;
--vds-bp-xl: 1280px;
```

### 4.1.2 Header / Nav — Hamburger Menu

**Current behavior:** `.main-nav` is a horizontal flex container with links. At narrow widths (< ~500px), the links overflow and wrap awkwardly.

**Target behavior:**
- **< 768px:** Nav links hidden behind a hamburger toggle. Brand stays visible.
- **≥ 768px:** Full horizontal nav restored (current behavior).

**Implementation plan:**

```css
/* Base (mobile): header with hamburger */
.site-header {
    display: flex;
    align-items: center;
    gap: 0.75rem;
    padding: 0.5rem 1rem;
    background: var(--vds-bg-1);
    border-bottom: 1px solid var(--vds-border);
    position: relative;
}

.brand {
    font-family: var(--vds-font-heading);
    font-size: 1rem;
    font-weight: 600;
    color: var(--vds-seal-light);
    flex-shrink: 0;
}

/* Hamburger button */
.nav-toggle {
    display: flex;
    align-items: center;
    justify-content: center;
    margin-left: auto;
    width: 36px;
    height: 36px;
    background: transparent;
    border: 1px solid var(--vds-border);
    border-radius: var(--vds-radius-sm);
    color: var(--vds-text);
    cursor: pointer;
    font-size: 1.2rem;
}

/* Nav links — hidden by default on mobile */
.main-nav {
    display: none;
    flex-direction: column;
    gap: 0;
    margin-left: 0;
    width: 100%;
}

/* When open */
.main-nav.open {
    display: flex;
    position: absolute;
    top: 100%;
    left: 0;
    right: 0;
    background: var(--vds-bg-1);
    border-bottom: 1px solid var(--vds-border);
    padding: 0.5rem 1rem 0.75rem;
    z-index: 50;
    box-shadow: 0 8px 16px rgba(0,0,0,0.3);
}

.main-nav a {
    display: block;
    padding: 0.5rem 0;
    color: var(--vds-text);
    border-bottom: 1px solid var(--vds-border-subtle);
}

.main-nav a:last-child {
    border-bottom: none;
}

/* md (≥768px): full horizontal nav */
@media (min-width: 768px) {
    .site-header {
        padding: 0.75rem 2rem;
    }

    .brand {
        font-size: var(--vds-text-lg);
    }

    .nav-toggle {
        display: none;
    }

    .main-nav {
        display: flex;
        flex-direction: row;
        gap: 1rem;
        margin-left: auto;
        position: static;
        width: auto;
        padding: 0;
        box-shadow: none;
        background: transparent;
        border-bottom: none;
    }

    .main-nav a {
        padding: 0;
        border-bottom: none;
    }
}
```

**Blazor interaction:** Same as RealmFoundry sidebar — a `bool _navOpen` field in `MainLayout.razor` toggled by `@onclick` on the hamburger button.

### 4.1.3 Site Content — Padding / Max-Width

**Current behavior:** `.site-content` has `padding: 2rem; max-width: var(--vds-content-max);`

**Target behavior:** Progressive padding increase and max-width adaptation.

```css
.site-content {
    flex: 1;
    padding: 1rem;
    max-width: 100%;
    margin: 0 auto;
    width: 100%;
}

/* sm (≥640px): a bit more padding */
@media (min-width: 640px) {
    .site-content {
        padding: 1.5rem;
    }
}

/* md (≥768px): restore max-width */
@media (min-width: 768px) {
    .site-content {
        padding: 2rem;
        max-width: var(--vds-content-max);
    }
}
```

### 4.1.4 Hero Section — Font / Spacing

**Current behavior:** `.hero` has `padding: 4rem 1rem` and `h1` has `font-size: 3rem`.

**Target behavior:** Scale down hero text and padding on mobile.

```css
.hero {
    text-align: center;
    padding: 2rem 1rem;
}

.hero h1 {
    font-size: 1.75rem;
    margin-bottom: 0.75rem;
    color: var(--vds-text);
}

.lead {
    font-size: 1rem;
    color: var(--vds-text-muted);
    margin-bottom: 1.5rem;
}

/* sm (≥640px): increase hero size */
@media (min-width: 640px) {
    .hero {
        padding: 3rem 1.5rem;
    }

    .hero h1 {
        font-size: 2.25rem;
    }

    .lead {
        font-size: 1.1rem;
    }
}

/* md (≥768px): full desktop hero */
@media (min-width: 768px) {
    .hero {
        padding: 4rem 1rem;
    }

    .hero h1 {
        font-size: 3rem;
        margin-bottom: 1rem;
    }

    .lead {
        font-size: 1.2rem;
        margin-bottom: 2rem;
    }
}
```

### 4.1.5 Auth Card

**Current behavior:** `.auth-card` has `max-width: 480px; margin: 4rem auto; padding: var(--vds-space-8);`

**Assessment:** Already narrow enough (480px). Only minor padding adjustment needed.

```css
.auth-card {
    max-width: 480px;
    margin: 2rem auto;
    background: var(--vds-bg-2);
    padding: var(--vds-space-6);
    border-radius: var(--vds-radius-lg);
    border: 1px solid var(--vds-border);
}

@media (min-width: 768px) {
    .auth-card {
        margin: 4rem auto;
        padding: var(--vds-space-8);
    }
}
```

### 4.1.6 Feature Cards (`.feature`)

Already responsive via `auto-fit` grid. No changes needed to the grid itself, but consider adding min-height consistency:

```css
/* Optional: ensure cards have consistent height within their row */
.features {
    align-items: start;  /* cards align top, not stretch */
}
```

### 4.1.7 Site Footer

**Current behavior:** `.site-footer` has `padding: 1rem 2rem`.

```css
.site-footer {
    padding: 0.75rem 1rem;
    font-size: var(--vds-text-xs);
}

@media (min-width: 768px) {
    .site-footer {
        padding: 1rem 2rem;
        font-size: var(--vds-text-sm);
    }
}
```

---

## 5. Game Layout Provisions (Forward-Looking)

### 5.1 Scope

This section covers the game CSS in two files:

| File | Project | Status |
|------|---------|--------|
| [`Veldrath.GameClient.Components/wwwroot/css/game.css`](Veldrath.GameClient.Components/wwwroot/css/game.css) | Veldrath.GameClient.Components | Exists, used by embedded Avalonia-hosted Blazor view |
| `Veldrath.Web/wwwroot/css/game.css` | Veldrath.Web | Not yet created (planned in [web-client-architecture.md](plans/web-client-architecture.md)) |

The embedded game client (`Veldrath.GameClient.Components`) renders inside an Avalonia `BlazorWebView` — responsiveness matters here only for window resizing, not mobile phones. The web-based game client (`Veldrath.Web`) renders in a browser and needs full responsive handling.

### 5.2 Current Game Layout (from `game.css`)

```css
.game-sidebar {
    width: 260px;
    min-width: 260px;
    /* ... */
}

.game-center {
    flex: 1;
    /* ... */
}
```

And from the architecture plan, the proposed web game layout:

```css
.game-layout {
    display: grid;
    grid-template-areas:
        "header  header  header"
        "sidebar center  center"
        "footer  footer  footer";
    grid-template-columns: 280px 1fr;
    grid-template-rows: auto 1fr auto;
    height: 100vh;
    gap: 4px;
}
```

### 5.3 Sidebar Collapse Strategy

The game sidebar contains: chat panel, combat panel, player list. On narrow viewports, these need to collapse or become overlays.

**Recommendation: Two-tier approach.**

#### Tier 1: Collapse to icon strip (≥ 640px, < 1024px)

```css
/* sm-md range: shrink sidebar to icon strip */
@media (min-width: 640px) and (max-width: 1023px) {
    .game-sidebar {
        width: 48px;
        min-width: 48px;
        overflow: hidden;
    }

    /* Hide text labels, show icons only */
    .game-sidebar .sidebar-label { display: none; }
    .game-sidebar .sidebar-icon  { display: block; }

    /* Expand on hover */
    .game-sidebar:hover {
        width: 260px;
        min-width: 260px;
        position: absolute;
        z-index: 20;
        box-shadow: 4px 0 16px rgba(0,0,0,0.4);
    }
}
```

#### Tier 2: Overlay drawer (< 640px)

```css
/* Below sm: overlay sidebar */
@media (max-width: 639px) {
    .game-sidebar {
        position: fixed;
        left: 0;
        top: 0;
        bottom: 0;
        width: 260px;
        z-index: 50;
        transform: translateX(-100%);
        transition: transform 0.2s ease;
    }

    .game-sidebar.open {
        transform: translateX(0);
    }

    /* Toggle button always visible in header */
    .game-sidebar-toggle {
        display: flex;
    }

    .game-center {
        width: 100%;
    }
}
```

### 5.4 Game Area Adaptation

The tilemap (CSS Grid-based) is the centerpiece. Key considerations:

```css
/* Ensure tilemap doesn't overflow on small screens */
.tilemap-container {
    overflow: auto;
    max-width: 100%;
    max-height: 100%;
}

/* Scale tile size down on smaller viewports */
:root {
    --tile-size: 40px;
}

@media (max-width: 639px) {
    :root {
        --tile-size: 28px;
    }
}

@media (min-width: 640px) and (max-width: 1023px) {
    :root {
        --tile-size: 34px;
    }
}
```

### 5.5 Header / Footer Adaptation

```css
.game-header {
    padding: 0.25rem 0.5rem;
    gap: 0.5rem;
}

@media (min-width: 640px) {
    .game-header {
        padding: 0.5rem 1rem;
        gap: 1rem;
    }
}

/* Status bars should wrap on narrow screens */
.status-bars {
    display: flex;
    flex-wrap: wrap;
    gap: 0.5rem;
}

.status-bar {
    min-width: 120px;
    flex: 1;
}
```

### 5.6 When to Implement

The game layout responsiveness should be implemented:

1. **`Veldrath.GameClient.Components/wwwroot/css/game.css`** — can be done immediately. The embedded Blazor view is already functional and would benefit from window-resize responsiveness when the Avalonia host window is resized.
2. **`Veldrath.Web/wwwroot/css/game.css`** — when the web game client is implemented (per [web-client-architecture.md](plans/web-client-architecture.md)). The responsive rules from the embedded version should be shared/copied.

---

## 6. Implementation Order

### Phase A: Foundation — Both Projects

| Step | File | Change | Depends On |
|------|------|--------|------------|
| A1 | [`RealmFoundry/wwwroot/app.css`](RealmFoundry/wwwroot/app.css) | Add `--vds-bp-*` tokens to `:root` | — |
| A2 | [`Veldrath.Web/wwwroot/app.css`](Veldrath.Web/wwwroot/app.css) | Add `--vds-bp-*` tokens to `:root` | — |

**Rationale:** Tokens must exist before any `@media` blocks reference them in comments. Do both at once for consistency.

### Phase B: RealmFoundry

| Step | Target | What | Risk |
|------|--------|------|------|
| B1 | `.page-body` | Mobile-first padding/max-width adjustments (§3.1.4) | Low — simple padding changes |
| B2 | `.top-bar` | Mobile padding adjustments (§3.1.7) | Low |
| B3 | `.auth-card`, `.submission-form-shell` | Mobile padding adjustments (§3.1.6) | Low |
| B4 | `.field-list` | Stack on narrow screens (§3.1.8) | Low |
| B5 | `.data-table`, `.content-list-table` | Add `.table-responsive` wrappers (§3.1.5) | Medium — must wrap tables in new `<div>` in Razor components |
| B6 | `.layout`, `.sidebar` | Sidebar collapse + hamburger (§3.1.2) | Medium — requires Blazor `@onclick` handler in layout |
| B7 | `.content-detail-shell` | Split-pane stacking (§3.1.3) | Medium — requires component changes for mobile selection behavior |

**Rationale:** Low-risk padding changes first to build momentum. Table wrap requires Razor changes. Sidebar and split-pane require both CSS and Blazor interop — save for last.

### Phase C: Veldrath.Web

| Step | Target | What | Risk |
|------|--------|------|------|
| C1 | `.site-content` | Mobile-first padding/max-width (§4.1.3) | Low |
| C2 | `.hero`, `.hero h1`, `.lead` | Scale down on mobile (§4.1.4) | Low |
| C3 | `.auth-card` | Mobile padding (§4.1.5) | Low |
| C4 | `.site-footer` | Mobile padding (§4.1.7) | Low |
| C5 | `.site-header`, `.main-nav`, `.brand` | Hamburger menu (§4.1.2) | Medium — requires Blazor `@onclick` handler in `MainLayout.razor` |

**Rationale:** Same pattern — low-risk first, interactive nav last.

### Phase D: Game Layout (Forward-Looking)

| Step | Target | What | Risk |
|------|--------|------|------|
| D1 | [`Veldrath.GameClient.Components/wwwroot/css/game.css`](Veldrath.GameClient.Components/wwwroot/css/game.css) | Add breakpoint tokens, sidebar collapse strategy (§5.3-5.5) | Medium — game UI is complex |
| D2 | `Veldrath.Web/wwwroot/css/game.css` (future) | Copy responsive rules from embedded version when created | Low — copy/paste with adjustments |

### Dependency Graph

```
Phase A (Tokens)
    ├── Phase B1-B5 (RealmFoundry low-risk)
    │       └── Phase B6-B7 (RealmFoundry interop)
    └── Phase C1-C4 (Veldrath.Web low-risk)
            └── Phase C5 (Veldrath.Web nav interop)

Phase D (Game layout) — independent, can happen anytime
```

---

## 7. Testing Guidance

### 7.1 General Method

Use browser DevTools responsive mode (F12 → Toggle Device Toolbar) at the following widths:

| Width | Breakpoint | What to Check |
|-------|-----------|---------------|
| 375px | Below `sm` | iPhone SE — verify nothing overflows, no horizontal scroll, hamburger menus functional |
| 640px | `sm` | At threshold — verify transitions activate, no visual glitches |
| 768px | `md` | iPad mini portrait — sidebar visible in Foundry, full nav in Web |
| 1024px | `lg` | iPad Pro / small laptop — full layout locked in, no anomalies |
| 1280px | `xl` | Desktop — wider content, no wasted space |
| 1440px+ | Above `xl` | Large desktop — verify content doesn't stretch absurdly |

### 7.2 RealmFoundry Checklist

For each page/route, test at 375px, 768px, 1024px, 1440px:

- [ ] `/` (Home) — feature cards reflow, no overflow
- [ ] `/login`, `/register`, `/forgot-password` — auth card centered, not cut off
- [ ] `/submissions` — table scrolls horizontally on mobile, no layout break
- [ ] `/submissions/new` — form fields full-width, no overflow
- [ ] `/submissions/{id}` — split-pane stacks on mobile, detail view accessible
- [ ] `/languages` — `.content-type-grid` reflows naturally
- [ ] `/profile` — layout adapts, sidebar toggle works

### 7.3 Veldrath.Web Checklist

For each page/route, test at 375px, 768px, 1024px, 1440px:

- [ ] `/` — hero scales down, feature cards reflow, nav collapses
- [ ] `/community` — `.community-links` grid reflows
- [ ] `/login`, `/register` — auth card fits, centered
- [ ] `/game/*` (future) — game layout adapts (when implemented)

### 7.4 Specific Risk Checks

| Risk | How to Test |
|------|-------------|
| Sidebar overlay not dismissible | On mobile, open sidebar → tap backdrop → verify sidebar closes |
| Sticky table headers broken | Scroll table on mobile → verify header stays visible |
| Split-pane double scrollbars | Scroll detail pane → verify only one vertical scrollbar |
| Nav links hidden behind browser chrome | Open nav on mobile → scroll to bottom → verify all links reachable |
| 100vh game layout on mobile | Open game page on mobile → verify no overlap with browser address bar (consider `dvh` units) |

### 7.5 Browser Compatibility

Test in:
- Chrome (latest) — primary dev browser
- Firefox (latest) — CSS Grid `:has()` support confirmed since FF 121
- Edge (latest) — Chromium-based, should match Chrome
- Safari (latest) — verify `:has()` support (Safari 15.4+), `dvh` support (Safari 15.4+)

> **Note:** [`RealmFoundry/wwwroot/app.css`](RealmFoundry/wwwroot/app.css) line 578 already uses `:has()` — `.page-body:has(.content-detail-shell)`. Safari 15.4+ supports this. No concern.

---

## Appendix A: Complete `:root` Block Reference

After all changes, both project `:root` blocks should include the breakpoint tokens. The complete token set:

```css
:root {
  /* ── Breakpoints ─────────────────────────────────── */
  --vds-bp-sm:  640px;
  --vds-bp-md:  768px;
  --vds-bp-lg: 1024px;
  --vds-bp-xl: 1280px;

  /* ... all existing --vds-* tokens unchanged ... */
}
```

## Appendix B: Mobile-First Pattern Cheat Sheet

```css
/* ❌ WRONG: desktop-first (subtractive) */
.element { display: flex; }
@media (max-width: 768px) { .element { display: none; } }

/* ✅ RIGHT: mobile-first (additive) */
.element { display: none; }
@media (min-width: 768px) { .element { display: flex; } }
```

---

*End of media breakpoint strategy.*
