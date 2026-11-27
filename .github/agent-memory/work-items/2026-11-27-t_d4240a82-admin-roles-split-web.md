---
kind: work-item
date: 2026-11-27
area: web
card: t_d4240a82
title: "The web app expresses the admin.users / admin.roles split"
---

### Session 2026-11-27 — the client half of the `admin.roles` split

PR #334 made `admin.roles` the only permission that admits the four role/permission mutations, but the web
app still treated the whole `(admin)` group as `admin.users`. Two defects followed: a role manager (holding
only `admin.roles`) was refused by the group layout before reaching `/admin/roles` or an account's role
controls, and an account administrator saw forms whose submits the API now answers with 403.

The client shape, which the tests pin from the outside:

- **The layout decides entry, the page decides its API.** `(admin)/+layout.server.ts` now admits
  `admin.users` **or** `admin.roles` (`plans/sveltekit-migration.md:317`). Every admin loader awaits
  `parent()` and then requires the permission its own call needs — so a page the account may open never
  calls an endpoint that would refuse it. The dashboard is the exception that proves the rule: it branches,
  calling `/api/admin/status` only for `admin.users` and otherwise rendering the link grid alone.
- **One page, two read paths.** `/admin/users/[id]` reads `GET /api/admin/users/{id}` for `admin.users` and
  the new `GET /api/admin/roles/users/{id}` for `admin.roles`. The second returns an `AccountAuthorityDto`
  (id, username, roles, direct grants, effective permissions) and deliberately omits email, ban/mute state
  and warnings: those stay behind account administration. The page renders role/permission forms only for
  `admin.roles` and the kick/warn/mute/ban forms only for `admin.users`.
- **Form actions re-read the profile.** SvelteKit runs actions without layout data, so each action calls
  `requireActionPermission(locals, permission, message)` (in `#lib/server/access.ts`) before touching the
  API. Without it an account administrator's submit reached the endpoint and came back a 403 — the visible
  symptom this card exists to remove.
- **A role manager needs a way to find an account.** The account list is `admin.users`, so the role page
  carries a username lookup (`GET /api/admin/roles/users/by-username/{name}` → redirect to the account
  page). Usernames are matched through Identity's `FindByNameAsync`, so the lookup is case-insensitive.

Pitfalls worth remembering:

- **`parity/rest-routes.test.ts` scans comments too.** It greps `/api/…` across `web/client/src` without
  stripping comments, so prose like "makes no `/api/admin…` call" registers as a client route and fails the
  orphan check with a truncated path. Write the phrase without the literal.
- **`makeServerEvent` hands `parent()` `{}` by default.** Every updated admin loader test now passes
  `parentData: { access }`, and every gated action test mocks `#lib/server/game-request`'s `gameApiFetch`
  (with `importOriginal`, because the editorial detail pages import `upstreamStatus` from it).
- The authenticated e2e job (`ci-web.yml:331`) does not run for PRs into `development`; `admin-console.e2e.ts`
  drives the Admin path only, and the roles-only path is covered by `staff-admin-role-split.test.ts` plus the
  server integration tests.
