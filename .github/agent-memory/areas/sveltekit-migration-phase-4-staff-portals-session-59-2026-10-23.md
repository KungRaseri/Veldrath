---
kind: area
date: 2026-10-23
area: engine
title: "SvelteKit Migration — Phase 4 Staff Portals (Session-59, 2026-10-23)"
---

## SvelteKit Migration — Phase 4 Staff Portals (Session-59, 2026-10-23)

**Route groups (complete)**
- `(foundry)`: `/submissions` (filter/search/paging), `/submissions/[id]` (`id='new'` = create;
  vote and Curator review actions), `/notifications`, `/content`, `/content/[type]`,
  `/content/[type]/[slug]`, `/languages`, `/languages/new`.
- `(editorial)`: `/editorial` dashboard plus lore, patch-notes, and announcements lists and
  `[id]` editors (`id='new'` = create; save/publish/delete actions).
- `(admin)`: `/admin`, `/admin/users`, `/admin/users/[id]` (roles, permissions, kick/warn/mute/
  ban/unban), `/admin/roles`, `/admin/audit`, `/admin/sessions`, `/admin/reports`,
  `/admin/status`, `/admin/server` (broadcast).
- `(mod)`: `/mod`, `/mod/users`, `/mod/users/[id]` (kick/warn/mute/unmute), `/mod/reports`.
- Public `/players/[id]` already covered the old Foundry player profile (no report form existed).

**Server API layer**
- `game-request.ts` mints a game token and calls `INTERNAL_BRIDGE_URL ?? PUBLIC_API_URL` with
  Authorization, query building, 8s timeout, 204 handling, and `{error}` extraction.
- `foundry-api.ts`, `content-api.ts`, `admin-api.ts` and editorial admin functions in
  `editorial-api.ts` are typed wrappers. Response shapes differ: Foundry/Editorial use
  `items/totalCount`, admin uses `items/total`.
- `access.ts` loads `GET /api/account/profile` (returns roles + permissions), exposes
  `Roles`/`Permissions` constants, and special-cases the Admin role in `hasPermission`.
- Root `+layout.server.ts` returns `{ user, access, staff }`; route-group layouts use
  `await parent()` and enforce `admin.users`, `content.manage`, or `players.view`. `/login`
  redirect for anonymous visitors; 403 for authenticated users without the permission.

**Gotchas**
- Foundry review uses the legacy role policy `RequireRole("Curator")`, not
  `content.review`; the UI gate must check `roles.includes('Curator')`. Admins cannot review
  unless they also hold Curator.
- `FoundryContentType` was missing `Language`; it is now appended (enum value 7) with a
  FoundryEndpointTests regression test. The old Blazor language builder would have received
  "Unknown content type: language".
- SvelteKit form actions return JSON (`{type:'redirect'|'failure',status,data}`) for non-browser
  POSTs (curl), not HTTP 303; browsers get normal navigation. `%{redirect_url}` is empty in curl.
- Foundry write rate limit is 5/min per IP (`foundry-writes`); rapid create+vote+review can
  return 429 in smoke tests.
- RBAC snapshots are cached 60s by `AccountProvisioningService`; role changes in the DB take up
  to a minute to appear on new tokens.
- Test-account pattern used locally: Better Auth sign-up → `/api/foundry-bridge/register` →
  insert `AspNetUserRoles` rows (Admin, Curator) via psql → cookie session drives SSR pages.

**Verification (local stack)**
- Every gated page returned 200 with an Admin+Curator session; anonymous Playwright guards
  redirect to `/login` (5 new tests, 15 total).
- Editorial: create lore → publish → public `/lore/{slug}` 200 → delete → public 404.
- Foundry: create item submission → upvote → Curator approval → status Approved with review
  notes → notification appears → admin audit shows the review.
- Language builder submitted a `language` payload accepted by the server and rendered on the
  submission detail page. Admin broadcast action succeeded.
- Typecheck/lint/format clean, production build, 31 Vitest, 15 Playwright, 30 Foundry server
  tests.
