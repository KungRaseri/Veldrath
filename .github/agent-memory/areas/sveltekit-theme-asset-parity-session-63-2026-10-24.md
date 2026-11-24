---
kind: area
date: 2026-10-24
area: web
title: "SvelteKit Theme + Asset Parity (Session-63, 2026-10-24)"
---

## SvelteKit Theme + Asset Parity (Session-63, 2026-10-24)

**Theme**
- `web/client/src/routes/theme.css` now maps Skeleton v5 tokens to the VDS palette from
  `docs/design-system.md` and the deleted `VeldrathTheme.cs`: Crimson Seal primary, Gold
  secondary, Emberfall tertiary, warm charcoal surface scale, 4/6px radii.
- `web/client/src/routes/brand.css` adds self-hosted `@font-face` rules, VDS typography
  utilities, base page chrome, site header/footer styles, auth cards, game shell/panels, map
  connector lines, rarity colours, and OAuth button variants.
- `web/client/src/app.html` forces `data-mode="dark"`, adds the favicon/manifest set, preloads
  Cinzel and Lora, and loads Inter from Google Fonts with a system fallback.
- `src/layout.css` was previously unused; `routes/layout.css` now imports it, so rendered
  Markdown (lore/patch notes) finally gets themed headings, code/table/blockquote styles.

**Assets**
- Restored all `Veldrath.Web/wwwroot/images/**` files (logos, crests, herald emblems,
  favicons, `site.webmanifest`) into `web/client/static/images/**`; the manifest now points at
  `/images/favicon/...` and uses `#0C0D13`.
- Copied `RealmUI.Fonts/Assets/Fonts/*.ttf` (Cinzel, Cinzel Decorative, Lora, Lora Italic,
  JetBrains Mono) to `web/client/static/fonts/`.
- `web/client/src/lib/brand-icons.ts` vendors the Discord/Google/GitHub SVG paths. Do **not**
  import from `simple-icons` root in SvelteKit — it pulls the entire icon set through the SSR
  transform (thousands of extra modules).

**UI parity**
- New `src/lib/components/site/SiteHeader.svelte` and `SiteFooter.svelte` restore the brand
  logo, primary nav, staff links, account/Play actions, and the fat footer. Both are hidden on
  `/play/*` and `/desktop`, which use the full-screen game shell.
- Game components (`GameScreen`, `GameHeader`, `GameFooter`, `LeftSidebar`, `RightSidebar`,
  `CombatPanel`, `GameOverlays`, `CardinalMap`) now use Lucide icons, VDS panels, map
  connectors, rarity classes, and a compact 20px character heading.
- Auth pages use the crest, `auth-card`, icon inputs, and brand-coloured OAuth buttons; account,
  roster, wizard, lore, patch notes, and community pages use crests/section icons and Cinzel
  headings.

**Gotchas**
- Importing `Map` from `@lucide/svelte` shadows the global `Map` constructor — import it as
  `Map as MapIcon` (hit in `CardinalMap.svelte`).
- Skeleton's `.h1/.h2/.h3` utilities reset the family; unlayered heading rules at the end of
  `brand.css` restore Cinzel **and** `--vds-text` colour (otherwise linked headings inherit the
  seal-red link colour).
- Keep game-shell pages out of the public layout; otherwise the footer renders below the
  full-screen game and the public header consumes vertical space.

**Verification**
- Playwright visual smoke at 1600×1000: home, login, lore, roster, account, wizard, and live
  game. Computed body background `rgb(12,13,19)`, Cinzel headings, loaded logos/crests,
  8-node map with 11 connector lines, 10 header icons, 7 sidebar icons, 3 footer icons, and no
  site header/footer on `/play`.
