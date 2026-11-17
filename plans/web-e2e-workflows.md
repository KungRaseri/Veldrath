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

[`foundry-content.e2e.ts`](../web/client/tests/foundry-content.e2e.ts) — an editor and a player, one catalog:

- Editor: `/foundry` home with its capability groups, `/content` index, one type's browse list, the `/new`
  picker, the schema-driven create form, **create → detail → edit → Save → read the new value back**,
  `/schemas`, `/world-health` (200), then **Deactivate** and the entry is gone from the catalog (404).
- A failure path a person will hit: reusing a slug is refused and the form says so.
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

[`foundry-submissions.e2e.ts`](../web/client/tests/foundry-submissions.e2e.ts) — the community loop, which is
the reason the portal exists:

1. A player with **no roles** submits a power through `/new` → the schema form → `/submissions/{id}` (Pending).
2. `/submissions` defaults to the caller's own rows and offers no Everyone view.
3. `/review` is refused to that player (403).
4. A Curator finds the submission in the queue, approves it with notes; the decision is recorded on it.
5. Approving writes the payload: the entry is readable in the catalog, and the curator gets **no** edit link
   (decides, does not author).
6. The submitter's inbox carries the approval, Mark read clears it, and their view of the submission shows the
   decision and the notes.

[`staff-areas.e2e.ts`](../web/client/tests/staff-areas.e2e.ts) — editorial, admin and moderation with one Admin
account: `/editorial` and its three lists, `/admin` and its user list, `/admin/audit`, `/mod`, `/mod/users` with a
real filter round trip, and `/mod/reports?resolved=false`. Then a plain player is refused all three areas (403).

[`staff-actions.e2e.ts`](../web/client/tests/staff-actions.e2e.ts) — every moderation verb on a **real registered
player** found through the user search: Issue warning → the page shows "1 warnings"; Mute for 30 minutes → the
"Muted" chip appears; Unmute → it clears. And a second test puts a player *in the world* (character created
through the API, `/play/{id}` waiting for the hub's snapshot), then kicks them from the user page and asserts the
player's own screen shows the reason the moderator typed — the hub's `OnKicked` path, end to end.

[`editorial-publishing.e2e.ts`](../web/client/tests/editorial-publishing.e2e.ts) — writing for the public site:
create an announcement → Publish → it appears on `/community`; create a patch note → Publish → its public article
renders and the public index links it; create a lore article → Publish → its public page renders; and a missing
body is refused on the page. Note the create form lives in `[id]` with `id === 'new'`, which is why
`/editorial/.../new` works with no `new/` directory.

[`admin-console.e2e.ts`](../web/client/tests/admin-console.e2e.ts) — the thing the console exists to do: an Admin
grants Curator through `/admin/users/[id]`, the **player's own session** (a different context, no re-login) sees
the review queue and gets 200 on `/review`, revoking takes it back to 403. Then `/admin`, `/admin/roles`,
`/admin/sessions`, `/admin/server`, `/admin/status` and `/admin/reports` all render. This is the spec that would
catch a regression in the access-snapshot invalidation.

`staff-actions.e2e.ts` also closes the report loop: a second player files a report through
`POST /api/reports` with their own minted game token (the game client's path; no page files one), the moderator's
unresolved queue shows it, and resolving it leaves the row with the **Resolved** status.

[`public-details.e2e.ts`](../web/client/tests/public-details.e2e.ts) — the reading surfaces against seeded data:
`/lore`, `/patch-notes`, `/leaderboard`, `/community`, plus the failure paths (an unknown lore slug, patch note or
player id is a 404 rather than a blank page).

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
5. **The field-type enum crossed the wire as a number.** The web switches on `'Integer'`, `'Boolean'`,
   `'EnumString'`; the API serialized `ContentFieldType` as `3`, `5`, `6`, so every comparison missed and the form
   posted `"50"` where an integer belonged — which the write path then refused as "not a valid content payload".
   The same bug made `ContentIssueSeverity` arrive as `1`/`0`, so the world-health counts and every issue badge
   read the wrong severity. Both enums now carry `[JsonConverter(typeof(JsonStringEnumConverter))]`.
6. **A checked checkbox was read as false.** An `<input type="checkbox">` with no `value` submits `on`, and
   `coerceFieldValue` only accepted `'true'` — so every save wrote `isActive: false`, the catalog hid the entry,
   and the page the save redirected to answered 404. Fixed in [`coerceFieldValue`](../web/client/src/lib/foundry/content-editor.ts:146)
   (and the edit form now sets `value="true"`), with a unit test for `on`.
7. **The create form ignored the boolean default**, so a new entry was born inactive even before (6) — the
   checkbox did not honour `isActive: "true"` from the schema. Fixed in the create page, with a component test.
8. **A refused page still called the API.** SvelteKit runs `+page.server.ts` and `+layout.server.ts` loads
   concurrently, so a player opening `/mod/users` made one API call that the API refused with 403 before the
   layout's redirect won — a wasted round trip per blocked page, and an alarming line in the server log. The 11
   admin/mod page loaders now `await parent()` first, and a full authenticated run logs zero refused
   `/api/mod…`/`/api/admin…` calls.

## 6. Still open

Every page route is driven by a spec; `parity/routes.test.ts` is down to two exemptions — `/api/**` (route
handlers, covered by unit tests) and `/languages/**` (a redirect onto the content type). The two depth items this
section used to carry — the disconnected screen and the review queue's N+1 — are closed in §9. What is left:

- **Combat and death are not driven** (E13c/E13d). `CombatPanel` mounts only once a fight is active, and starting
  one needs a known enemy at a known node; the honest next step is a fixture (a GM spawn, or a seeded encounter at
  a named node) rather than a spec that loops over nodes hoping one has a monster.

## 7. Phases

| Phase | Scope | Status |
|---|---|---|
| E0 | The stack script, the account/role harness, CI wiring for the authenticated specs | **shipped 2026-11-17** |
| E1 | Public + auth smoke, Foundry content reads, the full map lifecycle | **shipped 2026-11-17** |
| E2 | Content writes: create → edit → save → deactivate, plus a refused duplicate slug | **shipped 2026-11-17** |
| E3 | Submissions end to end: a player submits, a Curator approves, the catalog gains the entry, the inbox reports it and Mark read clears it; own rows vs Everyone | **shipped 2026-11-17** |
| E4 | Editorial, admin and moderation: the areas, their lists, a real user filter, and a player refused all three | **shipped 2026-11-17** (publishing and kick/warn/mute pending, see §6) |
| E5 | Public reading surfaces (`/lore`, `/patch-notes`, `/leaderboard`, `/community`) and their 404 paths | **shipped 2026-11-17** |
| E6 | Moderation verbs (warn, mute, unmute) on a real player, and publishing an announcement and a patch note through to the public site | **shipped 2026-11-17** |
| E7 | Role grant → live for the player → revoke, the admin sub-pages, `editorial/lore/[id]`, and resolving a report end to end; the admin/mod/editorial/players/leaderboard exemptions retired | **shipped 2026-11-17** |
| E8 | Guarding each gated page load on `parent()` so a refused page makes no API call (11 admin/mod loaders; the run now logs zero refused `/api/mod…`/`/api/admin…` calls) | **shipped 2026-11-17** |
| E9 | Kick, with the player in the world: the moderator's reason reaches the client's own screen | **shipped 2026-11-17** |
| E10 | Depth, not breadth (§6): a diff before promotion on the review queue, the disconnected-session screen, a `--fresh` stack | superseded by E11–E16 |
| E11 | `start --fresh`: drop the compose volume so a run starts from the seed (`api-stop`/`api-start` came with it) | **shipped 2026-11-17** |
| E12 | The session state in between: cleared cookies → `/login?redirectTo=<nested path with query>` → back to it; a play URL that is not a character id → roster | **shipped 2026-11-17** |
| E13a/b | The seven-step creation wizard → straight into the world; the sheet is usable (bag rows, an action on every row); a neighbour is walkable; the roster remembers the character | **shipped 2026-11-17** |
| E13c/d | Combat and death | blocked on a known encounter (see §6) |
| E14 | A rename diffed before approval, approved, and read back from the catalog — which also fixed promotion's create-only refusal (`PromoteAsync`) | **shipped 2026-11-17** |
| E15 | The API taken away: both a Foundry page and a public page say "Could not reach the game server." and recover after `api-start` | **shipped 2026-11-17** |
| E16 | CI instrumentation: `retries: 1`, `trace: on-first-retry`, `video: retain-on-failure`, `expect.timeout: 10s` | **shipped 2026-11-17** |

E3–E5 are recorded here rather than half-built: the harness is what makes them cheap, and each one retires an
exemption in [`parity/routes.test.ts`](../web/client/src/lib/parity/routes.test.ts) rather than adding a new
allow-list entry.

## 8. Trust, the states people hit, and the game (E11–E16, shipped 2026-11-17)

Breadth is done — every page route is driven. What follows is what makes the suite *trustworthy*, then what covers
the largest untested surface.

**E11 — a resettable stack (`start --fresh`).** The stack reused whatever was in the `veldrath` database, so a green
run only proved "still works on this machine's history" and stale rows from earlier runs were a debugging tax.
`--fresh` drops the compose volume, recreates Postgres (the init script recreates the `auth` role/schema/grants), then
runs the existing migrate-and-start steps; the server seeds on boot, so the catalog is reproducible. CI's service
container is fresh by construction, so the flag is for local runs — and for debugging one that smells like data.
The same pass added `api-stop`/`api-start`, which only manage the API process.

**E12 — the states a real session reaches.** Everything was tested signed-in or anonymous; the interesting state is
in between. `session-states.e2e.ts` clears the cookies mid-flow, asserts the guard sends the visitor to
`/login?redirectTo=` carrying the **whole** nested target (`/content/ability?page=1`, query included), signs in, and
lands back there — the open-redirect guard in [`navigation.ts`](../web/client/src/lib/navigation.ts:1) is what makes
the round trip safe. It also asserts `/play/not-a-character-id` sends a player to their roster.

**E13 — the game, not just its first paint.** `game-loop` proved register → travel → inventory heading.
`game-journeys.e2e.ts` now drives the seven-step creation wizard (`/characters/new`: name availability, the
`aria-pressed` class/species/background options, the Review step, "Create character"), which drops the player
straight into `/play/{id}`; it then asserts the sheet is *usable* — every bag row offers an action, which is the
invariant that catches a dead bag without pinning a class kit — and that a neighbouring node is walkable. Creating a
character goes to the world, not the roster, so the roster is asserted at the end. Combat and death stay open
(§6): `CombatPanel` mounts only during a fight and starting one needs a known encounter.

**E14 — the diff approval writes.** The submission page already renders `ContentDiff`; what is untested is the
reviewer's real decision. A player submits a *change* to a seeded entry (same slug, different display name), and the
Curator sees the field-level diff, approves, and reads the new value in the catalog. That is the promotion path,
end to end. **Writing it found that approval could not do what the diff promised:** promotion called a create-only
path and refused every submission whose slug already existed, so a community could propose a correction nobody
could accept. `ContentWriteService.PromoteAsync` now upserts (id survives, version bumps), with a server test that
asserts the replace.

**E15 — upstream failure rendering.** We fixed a 429 that read as "not found"; without a spec it regresses.
`upstream-failure.e2e.ts` stops the API through the stack script, asserts a **Foundry** page and a **public** page
each say "Could not reach the game server." while keeping their own chrome, brings the API back in a `finally`, and
asserts a reload recovers. It self-skips when the API was not started by the script (CI runs `dotnet run` against a
service container), so it only runs where it can actually stop something.

**E16 — CI instrumentation.** `retries: 1` on CI and `trace: 'on-first-retry'`, `video: 'retain-on-failure'` in
[`playwright.config.ts`](../web/client/playwright.config.ts:3), plus a 10s expect budget for server-rendered pages.
A retry without a trace is a flake nobody can diagnose — the trace is the point, not the retry. The authenticated
job stays serial: these specs share one database and one API.

**Result:** twelve stack specs, fourteen tests, ~4.5 minutes together; thirty stack-free specs in ~18 seconds. The
suite now fails for one of two reasons only: the code broke, or the environment was not the one the spec needs —
and both say which.

## 9. The two §6 leftovers (2026-11-17)

**A kick has its own screen.** `session.phase` became `disconnected` when a moderator kicked a player and nothing
consumed it: the map, the travel buttons and the bag stayed on screen, each of them firing a command into a closed
connection. [`GameScreen.svelte`](../web/client/src/lib/game/GameScreen.svelte:58) now renders a `Disconnected`
region carrying the moderator's reason and one action, "Back to characters", and the game shell and the dialog
overlays are dropped while that phase holds — a dead session is not a banner you dismiss. `staff-actions.e2e.ts`
asserts the region, the reason, that no map button survives, and that the button lands on `/characters`; two
component tests in [`shell.test.ts`](../web/client/src/lib/game/shell.test.ts:204) cover the reason and the wording
when the server gives none. The new `.svelte` branches took the coverage gate to 89.95%, which is what the component
tests are for.

**The review queue no longer reads every map itself.** Each row cost two API calls (the graph, then a validate pass)
and, on the server, two full world loads — so the queue got slower with every draft awaiting a decision. The list
endpoint now answers the question once: `MapEditorService.ListAsync` computes the verdict for the maps actually in
the queue from a single world snapshot and ships it on `MapSummaryDto.Review`, using the same error rule and the
same `map_without_exit` refusal the decision applies (`BlocksPublication`), so a row cannot promise an approval the
decision would then refuse. `ValidateWorldAsync` was split into `LoadWorldAsync` plus a static
`ValidateWorld(world, …)` so one snapshot can answer many maps. The web loader just reads `map.review`, and a map
the server could not judge still reads `unknown` rather than `ready`. A server test asserts that a submitted draft
is `blocked` with **zero** errors (the missing exit is a warning) and that a draft nobody submitted carries no
verdict at all.