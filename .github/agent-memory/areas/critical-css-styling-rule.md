---
kind: area
date: undated
area: web
title: "CRITICAL — CSS/Styling Rule"
---

## CRITICAL — CSS/Styling Rule

**Before writing ANY CSS**, read [`docs/design-system.md`](../../../docs/design-system.md) and load the
`web-ui` skill. The web app is SvelteKit + Tailwind + Skeleton v5 and must use the Veldrath design
tokens in `web/client/src/routes/theme.css` and `brand.css` — no raw hex values, no ad-hoc component
stylesheets. The live token definitions are `theme.css` `:root` plus the `[data-theme='veldrath']`
Skeleton ramp block; the design document explains them and no longer carries any desktop-toolkit
material (Session-102 deleted it). `web/client/src/lib/parity/design-tokens.test.ts` fails when the
CSS, [`docs/design/tokens.json`](../../../docs/design/tokens.json) and the document disagree — add a
token in all three places.

**Applies to**: [`web/client`](../../../web/client) — the only client and the only UI surface (the
Avalonia `RealmForge` editor was deleted in Milestone B, so the rules that referred to it no longer
apply anywhere).

---
