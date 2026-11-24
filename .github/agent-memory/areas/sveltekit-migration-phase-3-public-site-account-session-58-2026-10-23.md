---
kind: area
date: 2026-10-23
area: engine
title: "SvelteKit Migration — Phase 3 Public Site + Account (Session-58, 2026-10-23)"
---

## SvelteKit Migration — Phase 3 Public Site + Account (Session-58, 2026-10-23)

**Public site (complete)**
- SSR routes: `/` (announcements + latest lore + patch notes + server status), `/lore`, `/lore/[slug]`,
  `/patch-notes`, `/patch-notes/[slug]`, `/community`, `/players/[id]`.
- `editorial-api.ts` server client calls `/api/editorial/*` and `/api/players/*` using
  `INTERNAL_BRIDGE_URL ?? PUBLIC_API_URL`. The editorial endpoints return the .NET
  `PagedResult<T>` serialized as `{ items, totalCount, page, pageSize }` — use `totalCount`, not
  `total`. Announcements are paged too (not a bare array); the home loader slices
  `announcements.page.items`.
- Markdown: `marked` + `sanitize-html` in `$lib/server/markdown.ts`; anchors get
  `rel="noopener noreferrer"`. Detail pages use `{@html}` with a targeted
  `svelte/no-at-html-tags` disable because the server sanitizes. `<script>` payloads are stripped.
- Account provider query moved to `$lib/server/account.ts`; routes must not import
  `@veldrath/shared/db` directly (eslint `no-restricted-imports` DB boundary).
- Blazor public routes intentionally remain until Phase 6; SvelteKit is the canonical site.

**Live verification (local stack)**
- Seeded `public."LoreArticles"`, `"PatchNotes"`, `"EditorialAnnouncements"` with `Status=1`
  (Published). Home/lore/patch-notes/community render the seeded titles; detail pages render the
  sanitized markdown; `/players/<kungraseri guid>` renders 200 with username.
- Account flow with a fresh Better Auth user: sign-up 200 → `/account` shows username +
  `Email & password` provider chip → change password 200 → `/api/foundry-bridge/register`
  `{"synced":true}` → sign-out 200 → Better Auth old password 401 / new 200 → .NET
  `/api/auth/login` old 401 / new 200.
- Gotcha: Better Auth rejects cookie-authenticated POSTs without an `Origin` header (403 CSRF).
  Browsers always send it; curl smoke scripts must pass `-H "Origin: http://localhost:5173"`.
- `npm run format`, `typecheck`, `lint`, `build` all pass; Vitest 31; Playwright 10 (home,
  lore, patch notes, community, forgot password, account guard, game guards).
- Temporary test accounts `phase3-*`, `phase3b-*` and the editorial seed rows exist only in the
  local dev DB (not committed).
