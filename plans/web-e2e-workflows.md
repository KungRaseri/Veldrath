# Web e2e: user workflows, not just routes

The unit suites are strong — 1,463 web tests plus 6,089 .NET tests — but they cannot prove that a signed-in
person can actually finish a job in the browser. This plan closes that gap: a harness that runs the real app
against the real API, and specs that drive the journeys a player, an author and a reviewer take.

## 1. What e2e covered before this plan (verified)

| Spec | What it proves | Runs by default |
|---|---|---|
| [`public-site.e2e.ts`](../web/client/tests/public-site.e2e.ts) | Landing, lore index, patch-notes index, community, forgot-password render | yes |
| [`home.e2e.ts`](../web/client/tests/home.e2e.ts) | The landing page renders | yes |
| [`account-character.e2e.ts`](../web/client/tests/account-character.e2e.ts) | Auth pages render; character select and account require a session | yes |
| [`game-auth.e2e.ts`](../web/client/tests/game-auth.e2e.ts) | Play and legacy routes redirect; a bad character id is refused | yes |
| [`staff-guards.e2e.ts`](../web/client/tests/staff-guards.e2e.ts) | 13 staff routes turn an anonymous visitor away before rendering | yes |
| [`game-loop.e2e.ts`](../web/client/tests/game-loop.e2e.ts) | One full loop: register → roster → play → travel → inventory | `E2E_GAME=1` |
| [`foundry-maps.e2e.ts`](../web/client/tests/foundry-maps.e2e.ts) | **Before:** sign in, open a seeded graph, validate it — one test, gated on a hand-made staff account | `E2E_FOUNDRY=1` |

So the honest answer to "is every page covered e2e?" was **no**. Every *route* was referenced by a spec
([`parity/routes.test.ts`](../web/client/src/lib/parity/routes.test.ts) enforces that), but Foundry, admin,
moderation, editorial and the public detail pages were covered by *being mentioned* in
[`staff-guards.e2e.ts`](../web/client/tests/staff-guards.e2e.ts) — an anonymous redirect, not a workflow.

## 2. Why a browser mock cannot close the gap

Every Foundry page is server-rendered: `+page.server.ts` calls `gameApiFetch`, which runs on the SvelteKit
server and talks to `Veldrath.Server` (`INTERNAL_BRIDGE_URL ?? PUBLIC_API_URL`). The request never leaves the
browser, so `page.route()` sees nothing. The only honest e2e is the real stack:

```
Postgres  →  Veldrath.Server (:9000)  →  SvelteKit preview (:4173)  →  Playwright
```

## 3. The harness (shipped)

- [`scripts/e2e-stack.sh`](../scripts/e2e-stack.sh) — `start` brings up Postgres (dev compose, `:5433`), applies
  the Better Auth migrations, and starts the API on `:9000` with the JWT/bridge values read from
  `web/client/.env` (a mismatch is a 401 on every call). `stop`, `status`.
- [`web/client/tests/support/stack.ts`](../web/client/tests/support/stack.ts) — the specs' shared harness:
  - `stackEnabled` / `stackSkipReason`: every spec here skips with the command that would run it, so
    `npm run test` stays stack-free.
  - `makeAccount`, `register`, `signIn`: accounts are created through the real forms, so Better Auth owns the
    session exactly as it does for a player.
  - `signUpWithRoles`: register → visit `/foundry` (which makes the server provision the Identity row just in
    time) → `grantRoles` → reload.
  - `grantRoles`: writes the `AspNetUserRoles` join row. There is no bootstrap admin (`/api/admin/...` needs an
    admin), and the roles themselves are seeded with their permission claims at startup, so mapping the account
    to the role row is the whole job. It retries until the JIT-provisioned row exists.
- Run it:

  ```sh
  scripts/e2e-stack.sh start
  cd web && E2E_STACK=1 npm run test:e2e:stack
  scripts/e2e-stack.sh stop
  ```

- CI: the `e2e-game` job of [`ci-web.yml`](../.github/workflows/ci-web.yml) already boots Postgres + the API with
  every secret; it now also sets `E2E_STACK=1` and `Auth__AccessCacheSeconds=0` and runs the three
  authenticated specs.

## 4. What the new specs cover (shipped 2026-11-17)

[`foundry-content.e2e.ts`](../web/client/tests/foundry-content.e2e.ts) — one editor and one player:

- Editor: `/foundry` home with its capability groups, `/content` index, one type's browse list, one entry, the
  edit form (preloaded `displayName` and `typeKey`), the `/new` picker, the schema-driven create form, `/schemas`,
  `/world-health` (200).
- Player: the same catalog with **no** New entry action, **no** edit link, the create form explaining the
  permission instead of 403ing, and `/world-health` refused with 403.

[`foundry-maps.e2e.ts`](../web/client/tests/foundry-maps.e2e.ts) — the whole map lifecycle, two accounts:

1. An author creates a draft through `/maps/new` (kind → owner → name).
2. The editor lands with the canvas, the details, the draft checklist and the zone-locations panel.
3. Validate runs the world rules without writing.
4. Submit lets a warning through — a half-made map is how a zone gets built.
5. The reviewer's `/review` row already says **"Approval would be refused · the map has no exit"**.
6. Approving is refused and the page says why ("…only leave it by dying"); the state stays *In review*.
7. Rejecting with a note is accepted, and the **author's inbox** carries the note and links to the map.

## 5. What writing these found

Four defects, all real, none of which the unit suites could have caught because each one only appears when the
real web app meets the real API:

1. **`GET /api/account/profile` reported no permissions for a role holder.** It read only the user's own
   permission claims; a role carries its permissions as *role* claims. Every ContentEditor and Curator therefore
   looked permissionless to the web app, which hid the authoring surface and the review queue from exactly the
   accounts trusted with them. Fixed in [`AccountService`](../Veldrath.Server/Features/Account/AccountService.cs)
   (now resolves through `RolePermissionResolver`, like the JWT enrichment) with two endpoint tests.
2. **A granted role did not apply until the 60s access cache expired.** Fixed by invalidating the account's
   snapshot from the four admin role/permission endpoints, and by making the cache window configurable
   (`Auth:AccessCacheSeconds`, `0` disables it — what the e2e stack sets).
3. **A refused map approval reached the browser as "Request failed (400)."** [`game-request.ts`](../web/client/src/lib/server/game-request.ts)
   only read RFC 7807 fields; Veldrath's own results carry `message`. Now it does too — which is how step 6 above
   can assert the reason.
4. **The content schemas asked for the wrong fields.** `ContentBase.TypeKey` is required by
   `ContentShapeRules.ValidateShared` and no schema exposed it, so *every* entry created in Foundry failed with
   `type_key_missing`; and the ability form wrote `abilityType` while the model and the validator read
   `powerType`, so edits never satisfied it either. [`ContentSchema.cs`](../Veldrath.Contracts/Content/ContentSchema.cs)
   now authors `typeKey` in the shared Identity group and `powerType` (including `innate`) for abilities and spells.

## 6. Still open

- **Content writes through Foundry return "The request body is not a valid content payload."**
  `ContentEntityTypes.Deserialize` returns null for the nested payload the schema-driven form builds (a flat
  payload with the same values creates the entity fine — verified with `typeKey` + `powerType` → 201). Until that
  is fixed the content spec asserts the read-only split and the form rendering rather than the write. This is the
  next thing to fix, and the spec is ready to grow a create/edit/save half when it is.
- **The review queue validates every submitted map on load** (two API calls per row). Fine for a page of maps;
  worth moving onto `MapSummaryDto` if the queue ever pages.

## 7. Phases

| Phase | Scope | Status |
|---|---|---|
| E0 | The stack script, the account/role harness, CI wiring for the authenticated specs | **shipped 2026-11-17** |
| E1 | Public + auth smoke, Foundry content reads, the full map lifecycle | **shipped 2026-11-17** |
| E2 | Content writes (create → edit → save → deactivate) once the payload binding is fixed | blocked on §6 |
| E3 | Submissions end to end: a player submits through the schema form, a Curator approves, the published entity appears in the catalog; `?scope=all` vs mine; `/notifications` mark-read | next |
| E4 | Editorial, admin and moderation happy paths (announcements, patch notes, user list → kick/warn/mute, reports) | pending |
| E5 | Public detail pages with seeded content (`/lore/[slug]`, `/patch-notes/[slug]`, `/players/[id]`, `/leaderboard`), and retiring the `routes.test.ts` exemptions one by one as their spec lands | pending |

E3–E5 are recorded here rather than half-built: the harness is what makes them cheap, and each one retires an
exemption in [`parity/routes.test.ts`](../web/client/src/lib/parity/routes.test.ts) rather than adding a new
allow-list entry.
