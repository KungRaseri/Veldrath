---
kind: work-item
date: 2026-11-02
area: web
title: "web parity matrix + coverage gate (PR 1 of the web pass)"
---

### Session-90 (2026-11-02) - web parity matrix + coverage gate (PR 1 of the web pass)

**Goal.** The user asked for a full feature-parity and coverage pass on the SvelteKit app:
90% unit coverage ("well above 85"), plus parity validation. Crafting, NPC dialogue, dungeons,
the skills UI, and every engine-only system (harvesting, enchanting, salvaging, socketing,
upgrading, reputation, achievements listing, NG+) are **deferred** with their own plans.
Plan: `plans/web-feature-parity-and-coverage.md`.

**Baseline measured before anything changed** (`npm run coverage --workspace client`, 11 files /
204 tests): statements 24.28%, branches 23.35%, functions 28.10%, lines 27.12% (1262/4652).
`src/lib/foundry` 100%, `src/lib/game` 75.9%, `src/lib/server` 8.3%, every route 0%.
4652 executable lines is the whole denominator — there is no path to 90 that skips the ~55 page
components, because `account/+page.svelte` (290 lines) and `characters/new/+page.svelte`
(280 lines) are the two largest untested files.

**Gate.** `web/client/vite.config.ts` coverage thresholds: 90 global (lines/statements/functions/
branches) plus glob floors — 92 for `src/lib/server/**`, `src/lib/game/**`, `src/lib/foundry/**`;
90 for `src/routes/**/*.ts`; 85/85/80/85 for `src/**/*.svelte`. Reporters now include
`text` + `json-summary`. Verify glob thresholds work: Vitest 5 supports them and glob files still
count toward the global aggregate, so floors raise the bar without exempting anything.
`web/shared` stays out of coverage on purpose (declaration-only; documented in its vitest config).

**Parity suites** (`web/client/src/lib/parity/`, 27 tests): hub methods (52 vs 41 invokes),
hub events (55 vs 39 subscriptions), REST routes (141 vs 67 client paths), content vocabulary
(21 registry keys vs browse/detail/write switches), route breadth (47 pages + 6 handlers vs
Playwright specs). Each has an allow-list of `Record<name, reason>`; stale or shadowed entries
fail the test, which is what stops the lists rotting.

**Two real defects the matrix found, both fixed:**
1. `ContentSchemaRegistry` advertises `weapon` and `armor`, but `BrowseAsync` and
   `BrowseDetailAsync` had no switch arms for them — the Foundry browser 404'd on both catalogs.
   Fixed with `db.Items.Where(x => x.ItemType == "weapon" | "armor")` (NOTE: the discriminator is
   `Item.ItemType`, not `TypeKey`).
2. 16 hub broadcasts had no subscriber. Nine are now handled in `session.svelte.ts`
   (`OnWarned`, `OnMuted`, `OnEmote`, `OnAnnouncement`, `OnTeleported`, `OnSummoned`,
   `OnItemReceived`, `CharacterIgnored`, `ZoneLeft`); seven stay documented (three deferred
   features, plus `EnemyEngaged`, `CharacterStatusChanged`, `ZoneLocationUnlocked`, `OnWhisper`).

**Also fixed in the same PR:** chat never called `SendChatMessage`, so every slash command was
unreachable. `/…` now routes through `SendChatMessage` and `GetChatCommands` feeds a command list
in `RightSidebar`. Added a root `+error.svelte` (there was none, so 404s and thrown loads fell
through to SvelteKit's unstyled page).

**Recorded, not fixed:** `GET /api/announcements` has zero consumers (superseded by
`/api/editorial/announcements`) — removal candidate, and the route-parity rule names it.
`NavigateToLocation`/`UnlockZoneLocation` have no web caller by design: the map graph is the
navigation surface and unlocks happen server-side.

**Test harness** (`web/client/src/lib/testing/`, `#lib/testing/*`, excluded from coverage):
`makeServerEvent`/`fakeUser`/`fakeSession`, a recording `installFetch` + response factories,
`withEnv` (works because `#lib/server/env` exports `process.env` itself), and `walkFiles`/
`readRepoFile`/`stripComments`/`extractBracedBlock` for source-reading tests.

**Traps found:** `walkFiles` needs `Dirent.parentPath` (Node ≥20.12); scanning client paths must
stop only at quotes/backticks or `${encodeURIComponent(x)}` gets truncated; normalize `${x}`
**before** `{x}` or a stray `$` survives; `session.test.ts`'s `FakeHub.invoke` now returns
`results.get(method)` so `GetChatCommands` can be exercised; `connect()` records `GetChatCommands`,
so the connect-lifecycle assertion expects two invocations.

**Verification:** web typecheck 0/0, lint clean, **249 Vitest tests** (was 204), production build
OK, `dotnet build Veldrath.Server.slnx` 0 warnings / 0 errors, 129 content server tests green.
The coverage step is deliberately red until the coverage pass lands; total 27.12% is the number
PR 2 starts from.

**PR 2 - the whole `src/lib/server` layer is now covered (90 -> 94%).** 13 new test files, 390
Vitest tests total. `src/lib/server`: lines 8.27% -> **94.03%**, statements **94.26%**, functions
**95.90%**, branches **93.79%** — the 92 floor passes on every metric. Global: 27.12% -> **33.29%**
lines.

How the layer is tested, because the patterns repeat:
- `vi.hoisted` + `vi.mock('#lib/server/game-request')` turns `character-api`, `content-api`,
  `editorial-api`, `foundry-api`, `admin-api`, `game-api`, and `access` into path/verb/query
  assertions. One case per helper; the expectations are the contract with
  `Veldrath.Server/Features/**`.
- `game-request` is tested against a real `fetch` stub (`installFetch`) so the JSON parsing, 204,
  invalid-JSON, 400/409/422 bodies, 401 challenge logging, 5xx body logging, and transport failure
  paths all execute. `vi.mock('#lib/server/game-token')` is mandatory there; otherwise the real
  minting runs and `jose` throws "Key for HS256 must be ... Received an instance of Uint8Array"
  under jsdom (cross-realm `Uint8Array`).
- `// @vitest-environment node` is required for `game-token`, `email`, `env`, `db`, and `auth`
  for the same reason. `email.test.ts` also mocks `$app/env` with `dev: false` to cover the
  production branch that must not log reset links.
- `getAuth()` is coverable without a database: `postgres()` and `betterAuth()` are lazy, so a
  fake `DATABASE_URL` is enough to execute the whole config (including all three OAuth provider
  branches).
- `env.ts` is covered by writing a temp `.env`, `process.chdir`, `vi.resetModules()`, then a
  dynamic import. That pins the precedence rule: a process value is never overridden by the file.

Traps worth remembering: `gameApiFetch(identity, path)` records only two args, so a mock-call
assertion must pad to three or compare `slice(0, 2)`; `listLoreArticles` takes `category` first,
not `page`; `CreationEquipmentPreferences` is `preferredWeaponType` / `preferredArmorType` /
`includeShield`; `form.Field` values need `new File([...], 'x')` in `FormData` to reach the
non-string branches.

Next blocker for the global gate: `src/lib/game` sits at 76.3% because the shell components
(`GameScreen` 0%, `CardinalMap` 0%, `RightSidebar` 0%) have no render tests, then every route
file at 0% and the ~55 page components.

**PR 3 - the game shell is covered, `src/lib/game` 76.3% -> 92.16% lines.** One new suite
(`src/lib/game/shell.test.ts`, 20 tests) covers `GameScreen`, `CardinalMap`, and `RightSidebar`;
`src/lib/game` tests now number 210 and the whole web suite 410. Global lines 33.29% -> **38.75%**.

Two things that made it work:
- **Drive the real `GameSession`, not a fake.** The shell reads ~20 derived getters
  (`zoneLabel`, `canRest`, `canShop`, `adjacentNodeIds`, `messagesForChannel`), so a fake would be
  a parallel model. `new GameSession()` + direct state assignment reproduces the browser, and
  `vi.spyOn(session, 'travel')` still asserts the command. This also avoids extending the 170-line
  `fakeSession` in `components.test.ts`.
- **Mock `#lib/hub` in any component test that imports the session class.** `hub.ts` imports
  `$app/env/public`, whose client runtime does `const { env } = globalThis.__sveltekit_dev` and
  throws under Vitest. `vi.mock('#lib/hub', () => ({ createGameHub: () => null }))` is enough
  because the session only creates a hub inside `connect()`.

**Gate correction worth remembering:** the `src/lib/game/**` glob included the `.svelte` shell, so
its 92 *branch* floor was counting v8 branches from compiled templates (`{#if}` chains, snippets)
and was unreachable by design. The glob is now `src/lib/game/**/*.ts`; components are governed by
the `src/**/*.svelte` floor. Same rule as before: fix the glob, never lower the number.

Remaining: `GameOverlays` dialog branches, `hub.ts`/`auth-client`, every route file at 0%, and the
~55 page components.

**PR 4 - the route layer has started (public loaders + roster/play guards).** 25 tests in
`src/routes/public-loaders.test.ts` and `src/routes/game-loaders.test.ts`; web suite 435 tests.
`src/routes` 0% -> **3.33%** lines, global 38.75% -> **40.16%**.

Two harness additions the rest of the route tests need:
- **`makeServerEvent({ form })` builds a urlencoded body, not `FormData`.** Node's `Request` and
  jsdom's `FormData` are different realms, so the multipart body does not survive and
  `request.formData()` rejects with "Content-Type was not one of multipart/form-data". Tests that
  exercise a form action must use `form: { id }`.
- **`callLoad(load, event)`** is the one place the route-specific `ServerLoadEvent` cast lives.
  Every generated load expects a literal `route.id` and a required `params.id`, so a shared fake
  event never typechecks on its own. Do not add per-route aliases; call `callLoad`.

Two behaviours worth remembering: guards in non-async loads (`/game`) throw synchronously, so they
are asserted with `expect(fn).toThrow()` and not `.rejects`; async guards (`/play/[id]`,
`/characters`) reject, and a `fail(...)` result is a value, not a rejection.

Still zero coverage: the creation wizard (`/characters/new`), the `api/**/+server.ts` handlers,
auth/account actions, the four staff route groups, and the ~55 page components.

**PR 5 - browser libs and the auth route loads.** `src/lib` root 0% -> **66.66%** lines, global
40.16% -> **41.13%**, web suite **455 tests** in 40 files.

- `hub.test.ts` pins the two connection misconfigurations (missing `PUBLIC_HUB_URL`, `http://` hub
  on an `https://` page) plus a real `HubConnection` build. It mocks `$app/env/public` with a
  getter-backed object so each case can change the URL.
- `auth-client.test.ts` asserts `signIn.username` exists — the username plugin's own availability
  helper must NOT be read in a test: reading it fires a network request (the first attempt produced
  `ECONNREFUSED 127.0.0.1:3000` as an unhandled error).
- `brand-icons.test.ts` guards the vendored SVG paths against truncation.
- `auth-pages.test.ts` covers `/login` and `/register` (signed-in redirect, anonymous allowed),
  `/verify-email` (missing token short-circuits before Better Auth; verified; invalid logs and
  returns a status instead of throwing), and `/account` (provider/session mapping, all three OAuth
  providers linkable, and the degraded path where both lookups fail).
- `forgot-password` and `reset-password` have no `+page.server.ts`; they are client-side
  `authClient` calls, so they belong to the component tests.

Harness change: `session: null` now means "signed in with no session row" — previously `??` treated
null as absent and injected a synthetic session, which hid the account page's null-session path.

**PR 6 - the public pages render, and `src/routes` went from 3.33% to 63.33% lines.** One suite
(`src/routes/pages-public.test.ts`, 15 tests) renders home, lore index/detail, patch-notes index,
player profile, and the hardcore board. Global 41.13% -> **44.87%** lines (470 tests, 41 files).

- These pages take only `data` and import `resolve()` from `$app/paths`; both work under Vitest with
  no stubbing. That is why they came before the auth pages, which read `$app/state` and
  `$app/navigation`.
- Cast the fixture once at the render call (`props: { data: fixture as never }`). Each route has its
  own generated `PageData`; importing six of them into one test is noise, and the loaders already
  enforce the shapes.
- Cover the empty states deliberately — a page with no rows is where these ship broken, and each
  one has its own copy branch.
- `{@html}` content is asserted through `container.querySelector('.markdown h1')` rather than
  `getByText`, because the injected markup is the thing under test.

**PR 7 - the auth pages render, and the `$app/*` harness landed.** Global 44.87% -> **56.24%** lines
(2666/4740), 51.67% statements, 46.94% branches, 58.72% functions; web suite **532 tests in 43
files**. `.svelte` lines 37.31% -> **49.58%**.

The stubbing, which every page-level test now depends on:
- `web/client/vite.config.ts` has a `test.alias` (not a build alias) mapping `$app/state` ->
  `src/lib/testing/app-state.ts` and `$app/navigation` -> `src/lib/testing/app-navigation.ts`. Both
  live under `src/lib/testing/**`, which is excluded from coverage.
- `app-state.ts` exports a mutable `page` and `setPage(overrides)`. **Call `setPage` before render**:
  `$state(page.url.searchParams.get('token') ?? '')` runs when the instance is created, so a later
  call changes nothing.
- `app-navigation.ts` exports spies. Declare them as `vi.fn<(args) => result>(async () => {})` - the
  generic carries the caller-visible signature and the empty body avoids ESLint's `no-unused-vars`,
  which rejects `_`-prefixed parameters in this repo. `resetNavigation()` clears them all through a
  `{ mockClear(): void }[]` so the differing mock types do not fight.
- `$app/paths` needs no stub; `resolve()` works under Vitest as-is.

Suites: `src/routes/pages-auth.test.ts` (29 tests - login, register, verify-email, forgot-password,
reset-password) and `src/routes/pages-account.test.ts` (33 tests). `SocialLoginButtons` is covered
through the login page instead of in isolation.

Test-writing details that cost time:
- `getByText('Unknown IP')` fails when the string is an interpolation sharing a paragraph with other
  text nodes; use `/Unknown IP/`. testing-library's `getNodeText` only concatenates *direct* text
  children, which is also why the same call succeeds for a plain `<span>`.
- jsdom does not implement `window.confirm`, so the unlink prompt is `vi.spyOn(window, 'confirm')`.
- jsdom only allows same-document navigation, so the "follow the returned provider URL" case feeds a
  `#linked` fragment and asserts `window.location.hash`; the `href` assignment under test is real.
- `reset-password` schedules `goto` with `setTimeout(..., 1500)`; assert the message first, then
  `await vi.advanceTimersByTimeAsync(1500)` under `vi.useFakeTimers()` (restored in `afterEach`).
- The account page renders three forms in order (profile, email, password) and has no form ids, so
  `forms(container)[n]` is the selector; the password form's three inputs are ordered
  current/new/confirm.
- The login "username path" test must not assert `goto` was not called - it navigates to `/characters`
  on success. Assert `signInEmail` was not called instead.

**PR 8 - the `api/**` route handlers.** One suite (`src/routes/api-handlers.test.ts`, 25 tests) covers
`api/game-token`, `api/characters/creation`, `creation/[id]`, `creation/check-name`,
`creation/[id]/[step]`, and `auth/[...all]`. Global 56.24% -> **57.74%** lines; web suite **557 tests
in 44 files**; `src/routes/**/*.ts` 17.57% -> **29.69%** lines.

- Call the exported `GET`/`POST`/`PATCH`/`DELETE` directly with `makeServerEvent`. Only
  `#lib/server/character-api`, `#lib/server/game-token`, and `#lib/server/auth` are mocked;
  `gameResultResponse` and `requireUser` run for real, so the status mapping and the guard are
  covered by the same assertions.
- `requireUser` throws inside an `async` handler, so an anonymous case is
  `await expect(call(GET, event)).rejects.toThrow()` - never `expect(fn).toThrow()`, which would pass
  for the wrong reason or miss a rejection entirely.
- `makeServerEvent({ params, body, method: 'PATCH' })` already sets `content-type: application/json`
  for a body, so `request.json()` works; an array body is what exercises the `readJson` rejection
  branch (the route rejects non-objects).
- The `[step]` route is worth one case per arm: each choice step has its own field name and its own
  `400` message, and `attributes` filters non-integer values before calling the API, so a
  `{ strength: 2, luck: 'lots', fate: 1.5 }` body must arrive as `{ strength: 2 }`.

**PR 9 - the character creation wizard.** One suite (`src/routes/pages-creation.test.ts`, 21 tests)
drives `characters/new/+page.svelte` (638 lines, the largest component in the app). Global 57.74% ->
**63.18%** lines; web suite **578 tests in 45 files**; `.svelte` 49.58% -> **58.05%**.

- Stub `fetch` with `installFetch` and answer by `` `${method} ${pathname}` ``; make the handler
  **throw** on an unknown route so a page calling something unexpected fails loudly instead of
  returning a confusing parse error.
- The step header (`Step 1 of 7 · Name`) renders even while the session is still starting, so waiting
  for it proves nothing and the form is not there yet. Wait for the **Cancel** button, which only
  exists inside the started branch. This cost a full round of 16 false failures.
- There is a 350 ms debounce before the name availability check. Keep real timers and use
  `findByText` (1 s default) rather than fake timers: `waitFor`/`findBy` both need real timers, and
  the whole suite still runs in about a second.
- `getByText('9')` is ambiguous on the attributes step because the remaining-points counter also
  reads 9 after one increase; assert the allocation from the PATCH body instead.
- `aria-pressed` is an attribute, not a property: use `.getAttribute('aria-pressed')`, not
  `toHaveProperty`.
- `validateCharacterName` returns the full sentence ("Name may only contain letters and spaces."),
  so match the real string rather than a fragment.

**PR 10 - the staff group guards and the moderation pages.** One suite
(`src/routes/staff-loaders.test.ts`, 23 tests) covers the four group layouts (admin, editorial,
mod, foundry) and the four moderation route modules. Global 63.18% -> **64.49%** lines; web suite
**601 tests in 46 files**; `src/routes/**/*.ts` 29.69% -> **40.27%**.

- `error(...)` has an **empty** `message` - the text is in `body.message` - so `toThrow('...')` cannot
  assert a 403/404 message. A small `thrownMessage()` helper that reads `body.message` is needed, and
  `expect.requireAssertions` means the helper must throw if nothing was thrown at all.
- A layout that is **not `async`** (`(foundry)/+layout.server.ts`) throws synchronously, so it needs
  `expect(() => ...).toThrow()`; every other layout rejects. Same rule already known from `/game`.
- The layouts read `access` from `parent()`, so the fake event's `parent` is replaced per test:
  `event.parent = async () => ({ access })`. `requirePermission` and `hasPermission` run for real, so
  `roles: ['Admin']` works with an empty permission list while a plain account is refused.
- Declare the access fixtures with an explicit interface. `{ permissions: [] }` infers `never[]`, and
  spreading a `['content.manage']` variant into it fails typecheck.

**PR 11 - the admin and editorial page modules, and the `docs/coolify-site` merge.** One suite
(`src/routes/staff-admin-editorial.test.ts`, 31 tests) covers the eight admin page modules and the
seven editorial ones. Web suite **632 tests in 47 files**; typecheck 0/0, lint clean.

- The admin user detail page has ten form actions; they all follow the same shape (`stringField` ->
  API -> `fail(400, { error })` or `back()`), so a table of assertions per action is enough and the
  two validation guards (warn and ban require a reason) are the only branches worth their own cases.
- The role catalogue page is static data. Assert that the Admin row's permissions **equal**
  `allPermissions`: if someone adds a permission and forgets the Admin row, that is a real bug.
- Editorial `publish`/`delete` mocks must be seeded in `beforeEach` like the others. A missing mock
  returns `undefined`, and `result.error` then throws a `TypeError` that reads like an assertion
  failure.

**Merge fixes (five incoming commits, `docs/coolify-site`).** The merge was clean; the incoming work
had two defects that had to be repaired:

1. **A workflow step swallowed `retention-days`.** The new `Queue the docs deployment` step was
   inserted between `path:` and `retention-days: 30` of the `Upload the generated site` step. The
   artifact lost its retention, and `retention-days: 30` became a line of the deploy step's `run:`
   script. Fixed by moving it back under the upload step's `with:`; verify workflow edits by parsing
   the YAML (`python3 -c "import yaml; yaml.safe_load(...)"`) and printing each step's keys, not by
   eyeballing indentation.
2. **36 broken relative links in `plans/web-feature-parity-and-coverage.md`.** From `plans/` the
   correct prefix is a single `../`; the file had `../../web/...` (resolves above the repo root) and
   root-relative `web/...`. `node scripts/check-markdown-links.mjs` is a gate inside `docs.yml` and
   was failing on all of them. It now prints *Checked 43 Markdown files: no broken relative links.*
   Run that script after touching any Markdown that links into the tree.

**PR 12 - the Foundry page modules.** One suite (`src/routes/foundry-loaders.test.ts`, 30 tests)
covers the content browser, the editor (validate/save/deactivate), the language fork, the
notification inbox, and the submission queue. Global 64.49% -> **71.35%** lines; web suite **662
tests in 48 files**.

- Mock `#lib/server/access` with `importOriginal` and override only `loadAccountAccess`: the routes
  need the real `Permissions`, `hasPermission`, `requireUser`, and `toGameIdentity`, and the
  important assertion is that `canEdit` follows `content.manage`.
- The editor passes `detail.data.summary.updatedAt` as the fifth argument to `updateContent`. That is
  optimistic-concurrency plumbing; a test that only checks four arguments would not notice it going
  missing.
- Form field names and payload keys differ in the language fork: `initialClusters` ->
  `allowedInitialClusters`, `syllablePatterns` -> `allowedSyllablePatterns`. Assert the payload key.
- `listSubmissions` uses `totalCount` (Foundry API) while the admin API uses `total`. Both appear in
  this codebase; check which shape a loader reads before writing the fixture.
- `vi.mock` is hoisted above imports, so static imports of the route modules are enough - no
  top-level `await import(...)` dance needed.

`src/routes/**/*.ts` now clears its lines/functions/statements floor; only its **branch** coverage
is red at 76.33%. Still red: `src/**/*.svelte` 58.05% lines (staff pages, layout chrome, site
components) and `src/lib/game/**/*.ts` branches 82.74 (real `session.svelte.ts` guard branches, not a
glob problem; belongs with the `GameOverlays` work).
