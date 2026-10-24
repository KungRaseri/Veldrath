---
name: web-ui
description: Use when building or restyling the SvelteKit UI — Veldrath design tokens, Skeleton v5 components, Bits UI primitives, Lucide icons, self-hosted fonts, or brand imagery in web/client.
---

# Skill: web-ui

Apply the Veldrath Design System (VDS) to the SvelteKit web app so it matches the Avalonia client and the former Blazor UI.

## When to use

- Building or restyling pages, panels, auth cards, the game shell, or site chrome.
- Choosing colours, typography, radii, or spacing for a web component.
- Adding Skeleton v5 components, Bits UI primitives, or Lucide icons.
- Adding or replacing fonts, logos, crests, or favicons under `web/client/static/`.

## When not to use

- Route/load/action logic, runes state, auth, or build config — use [`web-development`](../web-development/SKILL.md).
- Avalonia (`Veldrath.Client`, `RealmForge`) styling — those use the ResourceDictionary token map in [`docs/design-system.md`](../../../docs/design-system.md).

## References

- [`docs/design-system.md`](../../../docs/design-system.md) — authoritative palette, type scale, spacing, component guidance, decision log.
- [`web/client/src/routes/theme.css`](../../../web/client/src/routes/theme.css) — VDS → Skeleton token mapping for `[data-theme='veldrath']`.
- [`web/client/src/routes/brand.css`](../../../web/client/src/routes/brand.css) — `@font-face`, base typography, site header/footer, `.hero`, `.feature-card`, `.game-shell`, `.game-panel`, `.auth-card`, `.map-node-cell`, rarity/status classes.
- [`web/client/src/routes/layout.css`](../../../web/client/src/routes/layout.css) — Tailwind/Skeleton entry point plus the `.markdown` prose styles.
- [`web/client/src/app.html`](../../../web/client/src/app.html) — `data-theme="veldrath" data-mode="dark"`, font preloads, favicons, manifest.
- [`web/client/src/lib/components/site/`](../../../web/client/src/lib/components/site) — `SiteHeader` / `SiteFooter`.
- [`web/client/src/lib/game/`](../../../web/client/src/lib/game) — game shell, map, sidebars, combat panel, overlays.
- [`web/client/src/lib/brand-icons.ts`](../../../web/client/src/lib/brand-icons.ts) — vendored GitHub/Discord/Google glyph paths.
- [`web/llms/skeletondev/llms-full.txt`](../../../web/llms/skeletondev/llms-full.txt), [`web/llms/bitsui/llms-full.txt`](../../../web/llms/bitsui/llms-full.txt), [`web/llms/tailwindcss/llms.txt`](../../../web/llms/tailwindcss/llms.txt), [`web/llms/lucide/llms.txt`](../../../web/llms/lucide/llms.txt).

## VDS tokens (web)

| Role | Token | Value |
| --- | --- | --- |
| Primary — Crimson Seal | `--vds-crimson` / `primary-500` | `#C0392B` |
| Primary hover/active | `primary-400` / `primary-700` | `#E05545` / `#8C2018` |
| Secondary — Gold | `--vds-gold` / `secondary-500` | `#C9A84C` |
| Tertiary — Emberfall | `--vds-emberfall` / `tertiary-500` | `#CC4125` |
| Background base | `surface-950` | `#0C0D13` |
| Background layers | `surface-900…800` | `#14151D`, `#1B1D28`, `#232533`, `#2E2F42` |
| Body text | `--vds-text` | `#E8E6E3` |
| Muted text | `--vds-text-muted` | `#9A96A0` |
| Borders | `--vds-border` | `#2E2F42` |
| Radii | `--vds-radius-*` | 4px / 6px / 8px |

Typography: headings **Cinzel** (display: Cinzel Decorative), body **Lora**, UI **Inter** (Google Fonts + `system-ui` fallback), code **JetBrains Mono**. All but Inter are self-hosted in `web/client/static/fonts/`.

## Key facts

- **Theme activation:** `data-theme="veldrath"` + `data-mode="dark"` on `<html>` in `app.html`; tokens are mapped in `theme.css`. Never hardcode hex values in components.
- **Skeleton tokens first:** `bg-surface-900`, `text-primary-400`, `border-surface-700`, `preset-filled-primary-500`, `size-elem-*`. Use `@skeletonlabs/skeleton-svelte` components before hand-rolling chrome.
- **Bits UI** is headless — supply classes yourself; `onValueChange` emits the raw value, not an event.
- **Icons:** `@lucide/svelte/icons/<kebab-name>` per-icon imports. Brand marks come from `brand-icons.ts` (do not import the whole `simple-icons` package — it blows up SSR transforms).
- **Fonts:** unlayered `h1,h2,h3,.h1,.h2,.h3` rules in `brand.css` set the heading font and text colour. Skeleton utility classes reset `font-family`, so new headings need those classes or the rule.
- **Game shell:** `/play/*` and `/desktop` suppress the public header/footer via `isFullScreen` in [`+layout.svelte`](../../../web/client/src/routes/+layout.svelte) and use `.game-shell` / `.game-panel*`.
- **Imagery:** logos, herald crests, and favicons live in `web/client/static/images/`; keep `static/images/favicon/site.webmanifest` in sync.

## Steps

1. Read the relevant section of `docs/design-system.md` and check `theme.css` for an existing token before adding one.
2. Reuse an existing primitive (`.game-panel`, `.auth-card`, `.hero`, `.feature-card`, `preset-*`) where possible.
3. Add components in the right place: site chrome → `src/lib/components/site/`, shared atoms → `src/lib/components/ui/`, feature UI → `src/lib/features/*` or `src/lib/game/*`.
4. Use Lucide icons at `size-elem-*` sizing; no emoji as UI icons.
5. Verify visually at a desktop and a narrow viewport, then run from `web/`:
   ```sh
   npm run typecheck
   npm run lint
   npm run test
   npm run build
   ```

## Rules

- No hardcoded colours, radii, or fonts in components — use VDS/Skeleton tokens.
- Avoid inline `style=` attributes and `<style>` blocks in `.svelte` files; extend `brand.css`/`theme.css` for genuine primitives and document exceptions.
- Do not add a second theme; `veldrath` is the only web theme.
- Keep focus rings, contrast, and keyboard operability intact (WCAG AA per `docs/design-system.md`).
- Svelte 5 runes only; no legacy reactivity syntax.
