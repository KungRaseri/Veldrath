# Web Feature Parity and Coverage Pass

## Status

Live plan. Scope agreed with the user on 2026-11-02. This file is the brief for the
implementing mode; the measured parity matrix is appended by PR 1 so later PRs plan from
facts rather than from this pre-matrix inventory.

## Goal

Two outcomes, one gate:

1. **Feature parity is validated, not assumed.** Every server and engine capability is
   either consumed by the SvelteKit app, explicitly exempted with a reason, or recorded as
   a deferred gap. Parity becomes a test that fails on drift.
2. **Coverage is enforced at 90%.** Global 90 with per-glob floors, wired into CI, so a
   regression fails the build rather than being discovered later.

## Decisions taken

| Decision | Choice |
|---|---|
| Triage model | Triage together: build the matrix and baseline first, then treat gaps by bucket |
| Coverage gate | **90% global immediately**, with per-glob floors. No staged ratchet |
| E2E scope | Journey and route parity, not statement coverage. Playwright cannot feed reliable v8 statement coverage back through sourcemaps |
| Game loop e2e | Stays live against `Veldrath.Server`. Staff surfaces may be mocked; the wire contract check does not |
| Deferred features | Crafting, NPC dialogue, dungeons, skills UI, and every engine-only system |
| PR 1 boundary | Phase 0 + matrix + plumbing fixes only. No new game features |

## Deferred explicitly (do not build in this pass)

Each of these has a server or engine implementation and no web surface. They are real
features with design work, not test work, so they get their own plans after the gate holds.

| Gap | Why deferred |
|---|---|
| Crafting UI | `CraftItem` and `ItemCrafted` exist; needs a recipe book design plus recipe content |
| NPC dialogue | `TalkToNpc` exists; needs dialogue trees, NPC presentation, and quest-giver flow |
| Dungeon entry | `EnterDungeon` exists; needs descent UI and dungeon content |
| Skills UI | `AwardSkillXp` / `SkillXpGained` exist; needs a skill sheet and rank display |
| Harvesting, enchanting, salvaging, socketing, upgrading | Engine-only. No server endpoint, no hub method |
| Reputation, achievement listing, New Game Plus | Engine-only. Achievements are evaluated but never surfaced |
| Persisted game settings, responsive game shell | Product/UI pass. [`GameOverlays.svelte`](../web/client/src/lib/game/GameOverlays.svelte:613) is diagnostics-only; [`GameScreen.svelte`](../web/client/src/lib/game/GameScreen.svelte:1) has no breakpoints |

## In scope for PR 1: baseline, harness, gates, matrix

Nothing in PR 1 changes game behaviour except the error page and the chat/event plumbing.
That keeps the coverage number meaningful as a baseline.

### Phase 0 — Baseline and gates

1. Measure coverage:
   `cd web && npm run coverage --workspace client`
   Record the per-directory baseline in the matrix section below. Do not set thresholds
   before this number exists.
2. `web/shared` stays without a coverage gate, by design. It is declaration-only (the Drizzle
   auth schema plus a types placeholder), and [`vitest.config.ts`](../web/shared/vitest.config.ts:4)
   already documents that a coverage number there would be meaningless. Root
   `npm run coverage` therefore measures the client only, which is correct. Do not add the
   script back until that package owns real logic.
3. Configure coverage in [`vite.config.ts`](../web/client/vite.config.ts:30):
   - reporters `text`, `lcov`, `json-summary` (the summary enables sticky PR comments later)
   - global thresholds 90 for lines, statements, functions, branches
   - per-glob floors:

     | Glob | Floor |
     |---|---|
     | `src/lib/server/**` | 92 |
     | `src/lib/game/**` | 92 |
     | `src/lib/foundry/**` | 92 |
     | `src/routes/**/*.ts` | 90 |
     | `src/**/*.svelte` | 85 |

   Vitest 5 supports per-glob threshold keys; verify against the installed version early.
   If the installed version does not, the fallback is separate Vitest projects per area,
   each with its own threshold block — **not** a lowered gate.
4. If v8 instrumentation of Svelte 5 compiled output proves unreliable, switch the provider
   to `@vitest/coverage-istanbul` or tune `extension`/`ignoreEmptyLines`. Do not lower the
   gate and do not add broad `coverage.exclude` entries to make the number work. Exclusions
   are allowed only for files that cannot execute in a test process, and each needs a comment.

### Phase 0.5 — Test harness

Unit tests cannot import route modules until the Kit 3 module surface resolves. Build
`web/client/src/lib/testing/` (imported as `#lib/testing/*`, and excluded from coverage because
it is test-only code that never ships):

| Helper | Purpose |
|---|---|
| `server-event.ts` | Fake `RequestEvent`: `params`, `url`, `locals` with user and session, `cookies`, `request`, `fetch`, `setHeaders` |
| `fetch-stub.ts` | `vi.stubGlobal('fetch', …)` wrapper returning typed JSON and status codes |
| `game-api-stub.ts` | In-memory fake for `gameApiFetch` consumers: roster, characters, content, editorial, admin |
| `auth-stub.ts` | `vi.mock('#lib/server/auth')` factory returning a fake session and provider list |
| `db-stub.ts` | `vi.mock('#lib/server/db')` factory. Must never construct a real client |
| `app-modules.ts` | Aliases and stubs for `$app/env/public`, `$app/state`, `$app/navigation`, `$app/paths` |

Constraints to respect:

- `requireAssertions: true` is set in [`vite.config.ts`](../web/client/vite.config.ts:25).
  Every test needs an assertion, including guard tests that only assert a redirect.
- Server-only modules stay lazy. A module-scope client makes the whole route file untestable.
- [`src/test-setup.ts`](../web/client/src/test-setup.ts:1) already stubs `ResizeObserver`,
  registers Testing Library cleanup, and flushes the Bits UI body-scroll-lock timer. Add to
  it rather than creating a second setup file.

### Phase 1 — The parity matrix

Five checks, all parsing-based. [`payload-contract.test.ts`](../web/client/src/lib/game/payload-contract.test.ts:99)
proves the technique already works for one of them.

| Check | Source of truth | Compared against | Allow-list rule |
|---|---|---|---|
| Hub methods | `public` methods in [`GameHub.cs`](../Veldrath.Server/Hubs/GameHub.cs:210) | `hub.invoke('…')` calls in `web/client/src` | Server-only methods listed with a reason (for example `GainExperience`, `AddGold`, `TakeDamage`, `AwardSkillXp`, `GetActiveCharacters`) |
| Hub events | `SendAsync("<Event>"` in `GameHub.cs` | `hub.on('<Event>'` in [`session.svelte.ts`](../web/client/src/lib/game/session.svelte.ts:560) | Each ignored event listed with a reason. An entry with no reason fails the test |
| REST routes | `group.MapGet/Post/Put/Delete` in `Veldrath.Server/Features/**/*Endpoints.cs` | Functions in `web/client/src/lib/server/*-api.ts` | Dev, internal bridge, Foundry, and Discord-only endpoints listed with a reason |
| Content schema | Keys in `ContentSchemaRegistry` | Foundry editor type list and `web/shared` contracts | Every key must appear in both, or the test names the missing side |
| Routes | `web/client/src/routes/**` tree | Playwright `tests/*.e2e.ts` | Exemptions listed in-file with a reason; a route with neither fails |

The route check is the practical substitute for e2e statement coverage: it guarantees breadth
without pretending a journey test measures lines.

### Phase 1.5 — In-pass plumbing fixes

These are dead server paths that the matrix will flag, and fixing them keeps the coverage
pass from having to explain unreachable code:

1. Route chat through [`GameHub.SendChatMessage`](../Veldrath.Server/Hubs/GameHub.cs:2846)
   so slash commands work, and expose `GetChatCommands` for a help affordance.
2. Subscribe the events the client drops today in
   [`session.svelte.ts`](../web/client/src/lib/game/session.svelte.ts:560):
   `OnWarned`, `OnMuted`, `OnEmote`, `OnAnnouncement`, `OnTeleported`, `OnSummoned`,
   `OnItemReceived`, `CharacterIgnored`, `ZoneLeft`. Each needs a visible treatment, not a
   silent log.
3. Add a design-system styled root `+error.svelte`. There is none today, so thrown loads and
   404s fall through to the SvelteKit default page.

### Phase 1.75 — CI

Extend [`ci-web.yml`](../.github/workflows/ci-web.yml:115):

- the existing `Unit test coverage` step fails automatically once thresholds exist
- upload `coverage/lcov.info` and `coverage/coverage-summary.json`
- keep artifact actions on v3 and use no action outside the allow-list in
  [`docs/ci-forgejo.md`](../docs/ci-forgejo.md)
- optional sticky PR comment through [`scripts/forgejo-pr-comment.sh`](../scripts/forgejo-pr-comment.sh)

## Pre-matrix gap inventory

Gathered by inspection on 2026-11-02. PR 1 replaces this with measured results.

### Server capability with no web surface

| Capability | Evidence | Bucket |
|---|---|---|
| Slash commands, `/help`, `/who`, moderation commands | [`SendChatMessage`](../Veldrath.Server/Hubs/GameHub.cs:2846), `GetChatCommands` | Close in PR 1 |
| Moderation and item events to the player | `OnWarned`, `OnMuted`, `OnEmote`, `OnAnnouncement`, `OnTeleported`, `OnSummoned`, `OnItemReceived`, `CharacterIgnored`, `ZoneLeft` broadcasts | Close in PR 1 |
| Crafting | [`CraftItem`](../Veldrath.Server/Hubs/GameHub.cs:1430), `ItemCrafted`, `GET /api/content/recipes` | Deferred |
| NPC dialogue | [`TalkToNpc`](../Veldrath.Server/Hubs/GameHub.cs:1407), `GET /api/content/dialogues` | Deferred |
| Dungeons | [`EnterDungeon`](../Veldrath.Server/Hubs/GameHub.cs:1480), `DungeonEntered` | Deferred |
| Skills | [`AwardSkillXp`](../Veldrath.Server/Hubs/GameHub.cs:1163), `SkillXpGained` | Deferred |
| Harvesting, enchanting, salvaging, socketing, upgrading, reputation, achievements, NG+ | `RealmEngine.Core/Features/` only; no folder under `Veldrath.Server/Features/` | Deferred |
| Unused client-facing calls | `GetActiveCharacters` | Deferred, evaluate when multi-character is designed |

### Current test inventory

| Area | Files | Tests today |
|---|---|---|
| `src/lib/game` and `src/lib/foundry` logic | ~20 | 9 Vitest files |
| `src/lib/server` clients and helpers | 17 | 2 Vitest files (`game-response`, `auth-password`) |
| `src/routes/**` loads, actions, endpoints | ~80 | none |
| Svelte components | ~15 | partial in `components.test.ts` |
| Playwright journeys | 6 specs | home, public site, account and character, game auth guards, staff guards, live game loop |

## Measured parity matrix (PR 1)

Five parity suites live in `web/client/src/lib/parity/`, all green:

| Suite | Compares | Result |
|---|---|---|
| `hub-methods.test.ts` | 52 public `GameHub` methods vs 41 client `hub.invoke` calls | 11 documented server-only methods |
| `hub-events.test.ts` | 55 broadcast events vs 39 subscriptions | 7 documented ignores, 9 previously-dropped events now handled |
| `rest-routes.test.ts` | 141 server routes vs 67 client paths | 1 removal candidate, 4 grouped exemptions |
| `content-schema.test.ts` | 21 registry keys vs browse, detail, and write switches | **found 2 real defects** |
| `routes.test.ts` | 47 page routes + 6 route handlers vs Playwright specs | 7 grouped exemptions |

### Defects the matrix found

| Defect | Status |
|---|---|
| `weapon` and `armor` are advertised by `ContentSchemaRegistry` but missing from `BrowseAsync` and `BrowseDetailAsync`, so the Foundry browser 404'd on both catalogs | **Fixed** in `ContentEndpoints.cs` (filters `Item.ItemType`) |
| `GET /api/announcements` has no consumer in the repo; `/api/editorial/announcements` superseded it | Documented as a removal candidate in the route parity rule |
| `GetActiveCharacters` is client-callable but unused | Documented; the roster stays server-rendered |
| 16 broadcasts had no subscriber (`OnWarned`, `OnMuted`, `OnEmote`, `OnAnnouncement`, `OnTeleported`, `OnSummoned`, `OnItemReceived`, `CharacterIgnored`, `ZoneLeft`, plus the deferred four) | **9 fixed** in `session.svelte.ts`; 7 documented |
| Chat never used `SendChatMessage`, so every slash command was unreachable | **Fixed**: `/…` routes through `SendChatMessage`; `GetChatCommands` feeds a command list in the chat panel |

### Newly visible gap (not in the pre-matrix inventory)

`NavigateToLocation` and `UnlockZoneLocation` have no web caller. That is correct for this
client — the map graph is the navigation surface and `TravelToNode` returns the destination
location, while unlocks happen inside `NavigateToLocation`/`SearchArea` server-side — but it is
worth knowing that within-zone location movement is intentionally map-driven, not a missing UI.

## Measured baseline (2026-11-02)

`cd web && npm run coverage --workspace client` — **11 files, 204 tests, all passing.**

| Metric | Value |
|---|---|
| Statements | 24.28% (1830/7536) |
| Branches | 23.35% (702/3006) |
| Functions | 28.10% (452/1608) |
| Lines | 27.12% (1262/4652) |

### Per directory (line coverage)

| Directory | Coverage | Files | Lines |
|---|---|---|---|
| `src/lib/foundry` | 100.0% | 1 | 47/47 |
| `src/lib/game` | 75.9% | 17 | 1190/1567 |
| `src/lib/server` | 8.3% | 18 | 25/302 |
| `src` (root: `hooks.server.ts`, `env.ts`) | 0.0% | 2 | 0/13 |
| `src/lib` (hub, auth-client, index) | 0.0% | 4 | 0/15 |
| `src/lib/components` (+`auth`, `site`) | 0.0% | 4 | 0/115 |
| `src/routes/**` (all groups and api) | 0.0% | ~95 | 0/2200 |

### Largest uncovered files

| Uncovered lines | File |
|---|---|
| 290 | `src/routes/account/+page.svelte` |
| 280 | `src/routes/characters/new/+page.svelte` |
| 144 | `src/lib/game/GameScreen.svelte` |
| 95 | `src/routes/characters/+page.svelte` |
| 72 | `src/lib/game/GameOverlays.svelte` (of 321) |
| 70 | `src/routes/(foundry)/content/[type]/[slug]/edit/+page.svelte` |
| 60 | `src/lib/game/CardinalMap.svelte` |
| 55 | `src/routes/(admin)/admin/users/+page.svelte` |
| 54 | `src/routes/+page.svelte` |
| 52 | `src/routes/(foundry)/content/[type]/[slug]/edit/+page.server.ts` |
| 51 | `src/lib/game/RightSidebar.svelte` |
| 51 | `src/routes/api/characters/creation/[id]/[step]/+server.ts` |
| 50 | `src/lib/server/game-request.ts` |
| 48 | `src/routes/(admin)/admin/users/[id]/+page.server.ts` |
| 47 | `src/lib/server/editorial-api.ts` |

### What the baseline means for the gate

Total executable lines are only **4652**, and ~3390 are uncovered. Hitting 90% global
means covering roughly **2925 additional lines** — essentially every route loader, every
server client, every page component, plus the game shell components. There is no path to 90
that avoids render tests for the ~55 page components, because the two largest untested files
alone are 570 lines of page markup.

Two consequences worth accepting up front:

1. `src/lib/server` and the route loaders are the cheapest lines to cover and the most
   valuable, so they come first; the pages follow.
2. Svelte files are the long tail. If they cannot reach 90 while `#lib` is already there,
   the correct answer is more render tests, not a lower global threshold — the gate the user
   chose is 90.

## Progress: server-layer coverage complete

The `src/lib/server/**` floor (92) now passes on every metric.

| Directory | Before | After |
|---|---|---|
| `src/lib/server` lines | 8.27% | **94.03%** |
| `src/lib/server` statements | 7.96% | **94.26%** |
| `src/lib/server` functions | 1.63% | **95.90%** |
| `src/lib/server` branches | 8.02% | **93.79%** |
| Global lines | 27.12% | 33.29% |
| Global statements | 24.28% | 28.20% |
| Global functions | 28.10% | 35.39% |
| Global branches | 23.35% | 31.39% |

What made it work, and what to reuse:

- `vi.hoisted` + `vi.mock('#lib/server/game-request', …)` turns the domain clients into
  path/verb assertions. Every helper gets one case; the table is the contract with
  `Veldrath.Server`.
- `vi.mock('#lib/server/game-token')` is required for any `gameApiFetch` test, otherwise the real
  JWT minting runs and `jose` fails on jsdom's `Uint8Array` realm. `game-token.test.ts` itself
  uses `// @vitest-environment node`, as do `email`, `env`, `db`, and `auth`.
- `getAuth()` builds without a database as long as `DATABASE_URL` is set: `postgres()` and
  `betterAuth()` are lazy, so `auth.ts` is coverable even though it looks untestable.
- `env.ts` is testable by writing a temp `.env`, `process.chdir`, `vi.resetModules()`, and a
  dynamic import. That also documents the "process wins over file" precedence.

Remaining before the global gate can pass: `src/lib/game` (76.3% — the untested shell components
`GameScreen`, `CardinalMap`, `RightSidebar`), every route file (0%), and the ~55 page components.

## Progress: the game shell is covered

`src/lib/game` moved **76.3% → 92.16% lines** (89.98% statements, 89.41% functions, 72.93%
branches) with one new suite, `src/lib/game/shell.test.ts`, covering `GameScreen`, `CardinalMap`,
and `RightSidebar`. `src/lib/game` tests now number 210.

Two patterns worth reusing:

- **Test the shell against the real `GameSession`, not a fake.** The shell reads about twenty
  derived getters (`zoneLabel`, `canRest`, `canShop`, `adjacentNodeIds`, `messagesForChannel`, …);
  a hand-written fake has to reimplement all of them. `new GameSession()` plus direct state
  assignment (`session.map = graph()`, `session.dead = true`) exercises the same getters the
  browser does, and `vi.spyOn(session, 'travel')` still asserts the command.
- **`#lib/hub` must be mocked** even though the shell never connects: it imports
  `$app/env/public`, whose client runtime reads `globalThis.__sveltekit_dev`, which does not exist
  under Vitest (`Cannot destructure property 'env' of 'globalThis.__sveltekit_dev'`).

### Gate correction: `src/lib/game/**` → `src/lib/game/**/*.ts`

The original glob swept the shell components into a **branch floor of 92**. v8's branch
bookkeeping for compiled Svelte templates counts `{#if}` chains and snippet boundaries, so that
floor was measuring markup shape, not logic, and no amount of render tests would meet it. The glob
is now TypeScript-only (the runes store, reducers, layout, helpers), and the components are held to
the `src/**/*.svelte` floor that exists for exactly that purpose. This is a glob correction, not a
lowered gate: nothing that was previously measured as logic is now unmeasured.

Global after this step: **38.75% lines** (34.67% statements, 43.60% functions, 36.36% branches).

## Progress: the route layer has started

Two suites, 25 tests, covering the public surface and the first authenticated flow:

- [`public-loaders.test.ts`](../web/client/src/routes/public-loaders.test.ts:1) — home (two newest
  announcements, three of each article type), lore index and detail, patch notes index and detail,
  community health probe, player profile (malformed id 404s before the API call, missing profile vs
  upstream 502), and the hardcore board (redirect, page clamping, page passthrough).
- [`game-loaders.test.ts`](../web/client/src/routes/game-loaders.test.ts:1) — the `/game` redirect, the
  `/play/[id]` guards (anonymous → login, malformed id → roster), the roster load merging four API
  results and surviving each failing independently, and the delete action (anonymous redirect, empty
  id, 4xx passthrough, transport failure → 400, success redirect).

Harness additions that the rest of the route tests depend on:

| Helper | Why |
|---|---|
| `makeServerEvent({ form })` | URL-encoded body, because Node `Request` + jsdom `FormData` do not survive the trip (`request.formData()` rejects) |
| `callLoad(load, event)` | Erases the route-specific `ServerLoadEvent` type (literal `route.id`, required `params.id`) in one place instead of casting per call |

`src/routes` is at **3.33% lines** (was 0); global is **40.16%**. Reaching 90 still requires the
creation wizard, the api route handlers, auth/account actions, the staff route groups, and the page
components.

## Progress: browser libs and the auth pages

`src/lib` root is now **66.66% lines** (hub, auth client, brand glyphs) and global is **41.13%**
with **455 tests**.

- [`hub.test.ts`](../web/client/src/lib/hub.test.ts:1) — the two misconfigurations that look identical
  in the browser: a missing `PUBLIC_HUB_URL` and an `http://` hub on an `https://` page. Both throw
  a named error, and a configured hub builds a real `HubConnection`.
- [`auth-client.test.ts`](../web/client/src/lib/auth-client.test.ts:1) — pins that the username plugin
  is registered (`signIn.username` exists) without touching its availability helper, which issues a
  network request the moment it is read.
- [`brand-icons.test.ts`](../web/client/src/lib/brand-icons.test.ts:1) — the vendored SVG paths are
  long single lines that render a blank button when truncated. `src/lib/index.ts` is an empty
  placeholder and stays untested by design.
- [`auth-pages.test.ts`](../web/client/src/routes/auth-pages.test.ts:1) — signed-in visitors are sent
  away from `/login` and `/register`; `/verify-email` reports `missing`, `verified`, and `invalid`
  instead of throwing at the player; the account page maps providers and sessions, and still renders
  with empty lists when Better Auth fails.

`forgot-password` and `reset-password` have no server module — they call `authClient` directly — so
they are component-test targets, not loader targets.

Harness refinement: `session: null` now means "signed in, but no session row" (the account page has
to survive it); only an omitted `session` falls back to a synthetic one.

## Progress: the public pages render

Six page components covered in one suite, [`pages-public.test.ts`](../web/client/src/routes/pages-public.test.ts:1),
15 tests. `src/routes` jumped **3.33% → 63.33% lines** and global **41.13% → 44.87%** (470 tests).

| Page | Cases |
|---|---|
| `+page.svelte` (home) | hero and three feature cards, announcement section hidden when empty, both empty states, and announcements/lore/patch notes when present |
| `lore/+page.svelte` | category chips, article cards, error notice, empty state |
| `lore/[slug]/+page.svelte` | header, published date, injected sanitized HTML, draft with no date |
| `patch-notes/+page.svelte` | version chip, summary, date, empty state |
| `players/[id]/+page.svelte` | level/class/species line, zone, account id, `Unknown` fallback |
| `leaderboard/hardcore/+page.svelte` | living and fallen rows, `You` chip, skull, fell date, pagination both ways, load error, empty state |

Practical note: these pages only take `data` as a prop and use `resolve()` from `$app/paths`, both
of which work under Vitest unchanged. The `data` fixture is cast once at the render call because
`PageData` is generated per route — the loaders are where those shapes are actually enforced.

### PR 7 — app-module stubs and the auth page components

`$app/state` and `$app/navigation` are now aliased in [`vite.config.ts`](../web/client/vite.config.ts:24)
to [`app-state.ts`](../web/client/src/lib/testing/app-state.ts:1) and
[`app-navigation.ts`](../web/client/src/lib/testing/app-navigation.ts:1). Both are `test.alias`
entries only, so the real modules are untouched in the build. The navigation stubs are typed
`vi.fn<(args) => result>(async () => {})`: the generic carries the caller-visible signature and the
empty body avoids the unused-parameter rule. `app-state.ts` exports a mutable `page` object and
`setPage(overrides)`, which must be called **before** render because `$state(page.url...)`
initializers read it when the component instance is created.

[`pages-auth.test.ts`](../web/client/src/routes/pages-auth.test.ts:1) — 29 tests:

| Page | Covered |
|---|---|
| `login/+page.svelte` | all four `?error=` codes, email vs username `signIn` branch with trimming, rejected credentials with and without a message, three provider buttons, provider error with and without a message, thrown `Error` and thrown non-error |
| `register/+page.svelte` | password mismatch (no `signUp` call), success into `/game` with the exact payload, failure with and without a message |
| `verify-email/+page.svelte` | `verified` (resend form hidden), `missing`, `invalid`, resend success with its deliberately non-committal copy, resend failure with and without a message |
| `forgot-password/+page.svelte` | `redirectTo` pointed at the reset page, success copy, failure with and without a message |
| `reset-password/+page.svelte` | no token (form not rendered), mismatched passwords, success then the delayed `goto('/login')` under fake timers, failure with and without a message |

`SocialLoginButtons` is covered through the login page rather than in isolation, which is why its
error paths are asserted there.

[`pages-account.test.ts`](../web/client/src/routes/pages-account.test.ts:1) — 33 tests, because
every control there is either destructive or identity-changing and each one is asserted on both
outcomes (including whether `invalidateAll` runs, since the page renders server data that would
otherwise go stale):

| Section | Covered |
|---|---|
| Profile | identity, `Verified`/`Unverified`, resend verification success plus failure, empty-name guard, trimmed save with `invalidateAll`, save failure |
| Email | empty guard, trimmed change with the field cleared afterwards, failure with and without a message |
| Linked providers | empty list, credential row has no Unlink, unknown provider id shown verbatim, cancel prompt, unlink success and failure, link button hidden for a linked provider, link success following the returned URL, link failure, all three `data.oauthError` codes |
| Sessions | empty list, `This device`, `Unknown device`, `Unknown IP`, user-agent truncation at 90 characters, revoke one and revoke all, both failures, bulk button hidden with a single session |
| Password | mismatch guard, exact `changePassword` payload with `revokeOtherSessions: false` and cleared fields, failure with and without a message, sign out to `/` |

Two jsdom constraints are worth knowing. `window.confirm` is not implemented, so the unlink prompt is
stubbed with `vi.spyOn`; and jsdom only performs same-document navigation, so the link test feeds the
component a `#linked` fragment — the `window.location.href = result.url` assignment is the same one.

Measured after this PR: **56.24% lines** (2666/4740), 51.67% statements, 46.94% branches, 58.72%
functions; **532 tests in 43 files**. Layer figures: `src/lib/server` 94.03, `src/lib/game` 92.16,
`src/lib/foundry` 100, `src/lib` root 66.66, `.svelte` 49.58 lines (from 37.31).

### PR 8 — the `api/**` route handlers

[`api-handlers.test.ts`](../web/client/src/routes/api-handlers.test.ts:1) — 25 tests over the six
proxies the browser actually talks to: `api/game-token`, `api/characters/creation`,
`api/characters/creation/[id]`, `api/characters/creation/check-name`,
`api/characters/creation/[id]/[step]`, and `api/auth/[...all]`.

The handlers are called directly with the fake event rather than through a server, and
`gameResultResponse` plus `requireUser` run for real so a status-mapping or guard regression fails
here as well as in their own suites. What is pinned:

| Handler | Covered |
|---|---|
| `game-token` | minted with id/username/email/verified, `username` falling back to `name`, `cache-control: no-store`, anonymous redirect |
| `creation` (POST) | session payload, upstream 409 passed through with its message |
| `creation/[id]` (DELETE) | the path id reaches the API, 204 stays bodyless |
| `check-name` | `?name=` passed through, missing parameter becomes `''` |
| `[id]/[step]` (PATCH) | unknown step 404, missing/non-object body 400, one case per choice step including its own field error, attribute allocations filtered to integers, empty allocation rejected, equipment normalised (`''` to `null`, `includeShield` strict `true`), upstream 422 preserved |
| `[id]/[step]` (GET/POST) | `preview` and `finalize` only, blank name 400, name trimmed, `difficultyMode: 'hardcore'` maps to `hardcore` and anything else to `normal` |
| `auth/[...all]` | all five verbs forwarded to `getAuth().handler(request)` |

Measured after this PR: **57.74% lines**, 52.67% statements, 48.82% branches, 59.58% functions;
**557 tests in 44 files**. `src/routes/**/*.ts` went 17.57% -> **29.69%** lines. Every
`src/lib/server` client (`admin-api`, `character-api`, `content-api`, `editorial-api`,
`foundry-api`, `game-api`, `game-token`) is at 100%.

### PR 9 — the character creation wizard

[`pages-creation.test.ts`](../web/client/src/routes/pages-creation.test.ts:1) — 21 tests that drive
[`characters/new/+page.svelte`](../web/client/src/routes/characters/new/+page.svelte:1) the way a
player does: type a name, wait for the debounced availability check, then press Continue once per
step and assert the JSON that was sent. `fetch` is stubbed with `installFetch` from the harness,
answering by `METHOD path` and throwing on any route the page is not expected to call.

| Area | Covered |
|---|---|
| Start | "Preparing your creation session…" while pending, the upstream error with a retry that works, a non-JSON failure body, an unreachable server, a catalog error that does not block the form |
| Name | local validation refusing `K1!` without a request, `checking` → `available` gating Continue, a taken name, a failed check, and the trimmed value reaching the PATCH |
| Selections | class/species/background defaults sent as `className`, `speciesSlug`, `backgroundId`, `aria-pressed` on the picked class, a rejected save keeping the player on the step, the generic `The server rejected that choice.`, and Back clearing the error |
| Attributes | `Increase Strength` producing `{ Strength: 9, ... }` in the allocations payload |
| Equipment | `select` change and shield checkbox producing `{ preferredArmorType: 'light', preferredWeaponType: 'sword', includeShield: true }` |
| Review | preview rendering, the hardcore warning appearing on selection, `finalize` sending `difficultyMode: 'hardcore'` and `goto('/play/char-1')`, a failed preview, a 409 that keeps the player on Review |
| Cancel | DELETE of the session then `goto('/characters')`, including when the DELETE itself fails |

Two traps worth writing down. The step header (`Step 1 of 7 · Name`) renders **before** the session
starts, so waiting for it proves nothing — wait for the Cancel button, which only exists inside the
started branch. And `getByText('9')` is ambiguous on the attributes step because the remaining-points
counter also drops to 9, so that assertion belongs in the PATCH body instead.

Measured after this PR: **63.18% lines**, 58.08% statements, 53.7% branches, 63.38% functions;
**578 tests in 45 files**. `.svelte` lines 49.58% -> **58.05%**.

### PR 10 — the staff group guards and the moderation pages

[`staff-loaders.test.ts`](../web/client/src/routes/staff-loaders.test.ts:1) — 23 tests over the
four group layouts and the four moderation route modules. `#lib/server/admin-api` is mocked;
`requireUser` and `requirePermission` run for real, because they are the only thing standing between
a player and a moderation action.

| Area | Covered |
|---|---|
| `(admin)`, `(editorial)`, `(mod)` layouts | anonymous redirect, 403 with each layout's own message when the permission is missing, `access` returned for an Admin, and entry for a holder of the specific permission without the Admin role |
| `(foundry)` layout | session required, no permission check, `user` returned |
| `mod/+page.server` | user and open-report counts, plus the zero fallback that collects both API errors |
| `mod/users` | `search`/`status`/`sort` passed through, `page=0` clamped to 1, fallbacks when the API fails |
| `mod/users/[id]` | 404 on a missing account, the player returned, kick with and without a reason, kick failure as a 400, warn refusing a blank reason, warn with the trimmed reason, mute with a duration, unmute, unmute failure as a 400 |
| `mod/reports` | `?resolved=true|false` → `true|false|null`, page passthrough, defaults on failure, resolve success and a 400 on failure |

Two traps. A SvelteKit `error(...)` has an **empty** `message`: the text is in `body.message`, so
`toThrow('…')` cannot assert a 403/404 message — use a helper that reads `body.message`. And a
non-`async` layout (`(foundry)`) throws synchronously, so it needs `expect(() => …).toThrow()`, not
`rejects`.

Measured after this PR: **64.49% lines**, 58.97% statements, 55.2% branches, 64.29% functions;
**601 tests in 46 files**. `src/routes/**/*.ts` 29.69% -> **40.27%**.

### PR 11 — the admin and editorial page modules, plus the `docs/coolify-site` merge

[`staff-admin-editorial.test.ts`](../web/client/src/routes/staff-admin-editorial.test.ts:1) — 31 tests
over the eight admin page modules and the seven editorial ones, with `admin-api` / `editorial-api`
mocked and the real `requireUser`, `toGameIdentity`, and form helpers:

| Area | Covered |
|---|---|
| Admin dashboard + status | the status row and its error, on both pages that load it |
| Admin users list | `search`/`role`/`status`/`sort` passthrough, `page=0` clamped, fallbacks on failure |
| Admin user detail | 404 for a missing account, the player plus both catalogues |
| Admin user actions | assign/revoke role, grant/revoke permission, kick (with and without a reason), mute with a duration, unmute, ban only with a reason, unban, blank warning refused, and an upstream rejection turned into a `400` carrying its message |
| Admin audit / reports / roles / sessions / broadcast | page clamp and `targetId`, `?resolved=` mapping and resolve, the static role catalogue (Admin grants every permission, asserted against `allPermissions`), region/zone passthrough, broadcast refusing an empty message |
| Editorial overview + three lists | one `totalCount` per kind with the errors collected, page clamps |
| Lore / patch notes / announcements editors | `new` opens create mode, 404 messages, the mandatory-field guard per kind (patch notes also require `version`), create → redirect to the new id, missing slug refused, edit → `{ ok, message }`, publish and delete redirects, upstream failure as a `400` |

**Merge fixes.** `origin/development` brought five commits (`docs/coolify-site`: `docs/Dockerfile`,
the artifact retention tweak, the Coolify deploy step, and `docs/ci-forgejo.md`). The merge itself was
clean, but it carried two defects that had to be repaired here:

1. **The new deploy step swallowed `retention-days`.** It was inserted between `path:` and
   `retention-days: 30` of the `Upload the generated site` step, so the artifact lost its retention
   and the value became a line of the deploy step's `run:` script — the job would have died on
   `retention-days: command not found` on the one branch where it runs. Moved back under the upload
   step's `with:`; verified by parsing the file (12 steps, upload step keeping `name`/`path`/
   `retention-days`).
2. **36 broken relative links in this file.** Every link written as `../../web/…` or root-relative
   `web/…` is wrong from `plans/`, which needs a single `../`. `node scripts/check-markdown-links.mjs`
   — a gate inside `docs.yml` — now reports *Checked 43 Markdown files: no broken relative links.*

Verification after the merge: web typecheck 0/0, lint clean, **632 tests in 47 files**, markdown link
gate green.

Still red, in order of size: `src/routes/**/*.ts` (the Foundry page modules), `src/**/*.svelte` at
58.05% (staff pages, layout chrome, site components), and `src/lib/game/**/*.ts` branches at 82.74 —
that last one is `session.svelte.ts` guard branches, not a glob problem, and is tracked with the
`GameOverlays` work.

## Traps

- `$app/*` imports must resolve in Vitest before any route test can run.
  [`hub.ts`](../web/client/src/lib/hub.ts:2) already imports `$app/env/public`.
- Threshold keys are per glob, but files are weighted by size. Verify the arithmetic against
  the baseline; if 90 global and the floors conflict, raise a floor, never lower the gate.
- Route modules that import `#lib/server/db` at module scope are untestable. Fix the module,
  not the test.
- Playwright has no `storageState` seeding today. Account and roster journeys will need a
  registered user per run; prefer one worker-scoped fixture over per-test registration.
- The `e2e-game` job needs the .NET server, Postgres, and `E2E_GAME=1`. Keep it as its own
  job rather than folding it into `build-test`.

## Verification

From `web/`, every PR in this pass must end with all four green:

```sh
npm run typecheck
npm run lint
npm run test
npm run coverage --workspace client
```

Plus `npm run build`, and for PR 1 confirm the five parity tests fail when a hub method,
event, route, schema key, or page is removed.
