---
kind: work-item
date: 2026-11-19
area: web
title: "Design documentation restructured for a creative team"
---

### Session-102 (2026-11-19) — Design documentation restructured for a creative team

Started as "clean up the stale Avalonia references", ended as a rebuild of the design set plus a guard
against the drift that caused them.

- **The document was not merely stale, it was wrong.** `docs/design-system.md` documented a component class
  vocabulary (`.btn-primary`, `.btn-ghost`, `.badge-accent`, `.health`, `.mana`) that has never existed in the
  codebase — buttons are Skeleton `btn preset-filled-primary-500` / `preset-tonal-surface` / `preset-tonal-error`
  and badges are `badge preset-tonal-*`. It also claimed `.card` was Layer 2 with 24px padding (it is
  `--vds-bg-1` with no padding), claimed the app does not use `--vds-space-*` (it does, throughout `brand.css`),
  and documented the heading scale as `--vds-text-*` when headings are `.h1`/`.h2`/`.h3` in `rem`.
- **`--vds-gold*` was undocumented.** The metal the whole identity is built from — logo, panel-header icons,
  nav glyphs, hero kicker, equipped marks, prose emphasis — appeared in `theme.css` and 18 call sites in
  `brand.css`/`layout.css` but in no token table. Documented now, as a first-class accent with its own role:
  gold is the metal, Seal is the action.
- **Contrast table was numerically wrong**, not just dated. Recomputed with the WCAG formula: text primary
  18→16.6, seal-light 5.8→5.1, success 5.4→7.0, warning 4.6→7.6, info 5.2→6.7, danger 4.9→5.1. The old numbers
  were inflated for accents and deflated for greys, which is why seal-light looked safe for body text when it is
  not.
- **New set:** `docs/design/brand-guide.md` (the mark described for the first time, file-to-surface map, clear
  space, minimum sizes, raster-logo-vs-Cinzel rule, art direction), `docs/design/asset-specs.md` (canvas sizes,
  SVG-first, weight budgets, naming, icon grid, item-art contract), `docs/design/asset-inventory.md` (every file
  with its consumer and a proposed verdict), `docs/design/tokens.json` (machine-readable export).
- **`docs/client-ui-architecture.md`** rewritten around the surface inventory: every panel, block, dialog and
  HUD widget, what opens it, and the four states each must define. The two structural decisions and the Foundry
  review gate survive; the retired-surface framing is one line.
- **Guard:** `web/client/src/lib/parity/design-tokens.test.ts` — `theme.css` `:root` ↔ `tokens.json` ↔
  `docs/design-system.md`, four assertions (missing, extra, value mismatch, undocumented). It passes; it would
  have caught the gold omission.
- **Published.** `docfx.json` now includes the design set in `content` and maps `docs/design/images` (optimised
  2× previews only — the live 1–2 MB originals must never be copied) plus `docs/design/tokens.json` as a
  resource. `docs/toc.yml` gains a **Design** section. AGENTS.md's allowlist paragraph, the `web-ui` skill and
  `web/README.md` point at the new set.
- **`web-ui` skill had drifted too:** it named `--vds-crimson` and `--vds-emberfall` (both nonexistent; the real
  tokens are `--vds-seal` and `--vds-ember`), listed background layers as `#14151D/#1B1D28/#232533`, body text as
  `#E8E6E3`, muted as `#9A96A0`, border as `#2E2F42`, and called Lora the body face. Corrected against `theme.css`.
- **Dead tokens found:** `--vds-bg-deeper`, `--vds-accent`, `--vds-indicator-player|occupant|enemy` and
  `--vds-game-footer-h` are declared and never consumed. Listed in design-system §8.3 — give each a job or delete
  it, but do not document it as live.
- **Asset reality, measured:** 17 PNGs, ≈31.8 MB, of which only 3 are referenced anywhere. The crest is 1.61 MB
  rendered at 76px (and 44–48px elsewhere), the header wordmark 1.01 MB rendered at 30px tall, the footer lockup
  1.81 MB at 64px, the 512 install icon 583 KB. Fourteen files (≈27.2 MB) are referenced by nothing: ten alternate
  lockups and V studies, plus four herald emblems at 1254–1669px with no consumer. **No vector or layered master
  is committed** — that, not the file sizes, is the blocking gap.
- **Decision on the 14: left in place (2026-11-19).** No move, no deletion. They were reviewed visually
  and are not duplicates of the live marks — they are an unassigned lockup family, and two of them fill
  roles the docs already assume exist: `veldrath-v-clean-…` is the V-only monogram the brand guide names
  for anything under 48px, and `veldrath-icon-big-…` is the rounded-square install tile that the
  180/192/512 icons have no master for. Both stay unassigned and unwired. The four `herald/*` emblems
  point at nothing: no seeded organisation, no faction record in content, no emblem slot in the UI.
  Recorded in `asset-inventory.md` §4 with a role-by-role table; the re-export work is unaffected.
- **Docs link gate was already red** on `plans/content-seeding-pass.md` (8 links: `../../Veldrath.Server/…` from
  `plans/`, and `Features/Shops/` where the directory is `Features/Shop/`). Fixed, because it is the same CI job
  that builds the new pages. `node scripts/check-markdown-links.mjs` now reports 57 files clean.
