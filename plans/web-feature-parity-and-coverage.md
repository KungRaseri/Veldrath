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
| Deferred features | Crafting, NPC dialogue, dungeons, skills UI, and every engine-only system — all four features have since shipped; only the engine-only systems are still deferred (see "Closed since this list was written") |
| PR 1 boundary | Phase 0 + matrix + plumbing fixes only. No new game features |
| Coverage comment accounting | **The comment quotes the gate.** [`scripts/web-coverage-gate.mjs`](../scripts/web-coverage-gate.mjs) heads the sticky comment with the totals the `Coverage gate` step printed; the reporter's per-directory table is labelled as the other accounting. Decided 2026-11-20, see "Coverage comment accounting" below |

## Deferred explicitly (do not build in this pass)

Each of these has a server or engine implementation and no web surface. They are real
features with design work, not test work, so they get their own plans after the gate holds.

| Gap | Why deferred |
|---|---|
| Enchanting, salvaging, socketing, upgrading | Engine-only. No server endpoint, no hub method |
| Reputation, achievement listing, New Game Plus | Engine-only. Achievements are evaluated but never surfaced |
| Persisted game settings, responsive game shell | Product/UI pass. [`GameOverlays.svelte`](../web/client/src/lib/game/GameOverlays.svelte:596) is diagnostics-only; [`GameScreen.svelte`](../web/client/src/lib/game/GameScreen.svelte:1) has no breakpoints |

**Closed since this list was written:** every feature this table used to hold has shipped except the
engine-only systems above.

- The skills UI shipped in milestone E (PR #66) —
  [`SkillsOverlay.svelte`](../web/client/src/lib/game/SkillsOverlay.svelte) over
  [`skills.ts`](../web/client/src/lib/game/skills.ts), with the session folding `SkillXpGained`; the
  client-callable `AwardSkillXp` bridge was retired in PR #269.
- Crafting shipped in the same milestone (PR #69). The recipe book that row asked for is
  [`CraftingOverlay.svelte`](../web/client/src/lib/game/CraftingOverlay.svelte):
  `GetCraftableRecipes` fills it with the recipes resolved against the bag, `ItemCrafted` is the event
  that drives it, and `craftBlockedReason` ([`crafting.ts`](../web/client/src/lib/game/crafting.ts:58))
  is the per-recipe disabled reason. Harvesting shipped beside it — `HarvestNode`, the `Harvested`
  broadcast and a Gather button per node.
- NPC dialogue shipped as E4 (PR #98) —
  [`DialogueOverlay.svelte`](../web/client/src/lib/game/DialogueOverlay.svelte) over `TalkToNpc` and
  `ChooseNpcOption`, with authored greetings and a requirement and consequence per option.
- Dungeons shipped as F4 of [`dungeons.md`](dungeons.md) —
  [`DungeonPanel.svelte`](../web/client/src/lib/game/DungeonPanel.svelte) for the room list, the rooms
  already cleared, progress and an always-available exit. The `EnterDungeon` hub method is still
  uncalled there: a dungeon is entered on foot through the map graph, which is the model that plan
  chose.

They are recorded here rather than in the table because the table lists only what still has no web
surface.

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
3. Configure coverage in [`vite.config.ts`](../web/client/vite.config.ts:57):
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

- `requireAssertions: true` is set in [`vite.config.ts`](../web/client/vite.config.ts:38).
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
| Hub methods | `public` methods in [`GameHub.cs`](../Veldrath.Server/Hubs/GameHub.cs:50) | `hub.invoke('…')` calls in `web/client/src` | Server-only methods listed with a reason (for example `GetActiveCharacters`, `Ping`, `NavigateToLocation`) |
| Hub events | `SendAsync("<Event>"` in `GameHub.cs` | `hub.on('<Event>'` in [`session.svelte.ts`](../web/client/src/lib/game/session.svelte.ts:758) | Each ignored event listed with a reason. An entry with no reason fails the test |
| REST routes | `group.MapGet/Post/Put/Delete` in `Veldrath.Server/Features/**/*Endpoints.cs` | Functions in `web/client/src/lib/server/*-api.ts` | Dev, internal bridge, Foundry, and Discord-only endpoints listed with a reason |
| Content schema | Keys in `ContentSchemaRegistry` | Foundry editor type list and `web/shared` contracts | Every key must appear in both, or the test names the missing side |
| Routes | `web/client/src/routes/**` tree | Playwright `tests/*.e2e.ts` | Exemptions listed in-file with a reason; a route with neither fails |

The route check is the practical substitute for e2e statement coverage: it guarantees breadth
without pretending a journey test measures lines.

### Phase 1.5 — In-pass plumbing fixes

These are dead server paths that the matrix will flag, and fixing them keeps the coverage
pass from having to explain unreachable code:

1. Route chat through [`GameHub.SendChatMessage`](../Veldrath.Server/Hubs/GameHub.cs:3548)
   so slash commands work, and expose `GetChatCommands` for a help affordance.
2. Subscribe the events the client drops today in
   [`session.svelte.ts`](../web/client/src/lib/game/session.svelte.ts:758):
   `OnWarned`, `OnMuted`, `OnEmote`, `OnAnnouncement`, `OnTeleported`, `OnSummoned`,
   `OnItemReceived`, `CharacterIgnored`, `ZoneLeft`. Each needs a visible treatment, not a
   silent log.
3. Add a design-system styled root `+error.svelte`. There is none today, so thrown loads and
   404s fall through to the SvelteKit default page.

### Phase 1.75 — CI

Extend [`ci-web.yml`](../.github/workflows/ci-web.yml:178):

- the existing `Unit test coverage` step fails automatically once thresholds exist
- upload `coverage/lcov.info` and `coverage/coverage-summary.json`
- keep artifact actions on v3 and use no action outside the allow-list in
  [`docs/ci-forgejo.md`](../docs/ci-forgejo.md)
- optional sticky PR comment through [`scripts/forgejo-pr-comment.sh`](../scripts/forgejo-pr-comment.sh)

## Pre-matrix gap inventory

Gathered by inspection on 2026-11-02. PR 1 replaces this with measured results. The Bucket column
records what has since happened to each row, so `Close in PR 1` and `Closed` stay listed as the gap
this plan measured rather than as open work.

### Server capability with no web surface (as measured 2026-11-02)

| Capability | Evidence | Bucket |
|---|---|---|
| Slash commands, `/help`, `/who`, moderation commands | [`SendChatMessage`](../Veldrath.Server/Hubs/GameHub.cs:3548), `GetChatCommands` | Close in PR 1 |
| Moderation and item events to the player | `OnWarned`, `OnMuted`, `OnEmote`, `OnAnnouncement`, `OnTeleported`, `OnSummoned`, `OnItemReceived`, `CharacterIgnored`, `ZoneLeft` broadcasts | Close in PR 1 |
| Crafting | [`CraftItem`](../Veldrath.Server/Hubs/GameHub.cs:1632), `ItemCrafted`, `GET /api/content/recipes` | Closed — crafting overlay shipped in milestone E |
| NPC dialogue | [`TalkToNpc`](../Veldrath.Server/Hubs/GameHub.cs:1442), `GET /api/content/dialogues` | Closed — dialogue overlay shipped as E4 |
| Dungeons | [`EnterDungeon`](../Veldrath.Server/Hubs/GameHub.cs:1937), `DungeonEntered` | Closed — dungeon panel shipped as F4; the entry method is still uncalled because dungeons are entered on foot |
| Skills | `SkillXpGained`, broadcast from [`CraftItem`](../Veldrath.Server/Hubs/GameHub.cs:1632); no client-callable award method — the old `AwardSkillXp` bridge was retired in PR #269 | Closed — skill sheet shipped in milestone E |
| Harvesting | `HarvestNode`, the `Harvested` broadcast, a Gather button per node in `GameScreen.svelte` | Closed — shipped with crafting |
| Enchanting, salvaging, socketing, upgrading, reputation, achievements, NG+ | `RealmEngine.Core/Features/` only; no folder under `Veldrath.Server/Features/` | Deferred |
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

`$app/state` and `$app/navigation` are now aliased in [`vite.config.ts`](../web/client/vite.config.ts:43)
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

### PR 12 — the Foundry page modules

[`foundry-loaders.test.ts`](../web/client/src/routes/foundry-loaders.test.ts:1) — 30 tests over the
eight Foundry page modules. `content-api` and `foundry-api` are mocked; `buildPayload`, `hasPermission`
and `toGameIdentity` run for real, so the editor's coercion and the `canEdit` / `canReview`
derivations are what is asserted.

| Area | Covered |
|---|---|
| Content index + type browse | type list with an empty fallback, `search`/page passthrough (`page=0` clamped), browse fallbacks |
| Content detail | `canEdit` true with `content.manage` and false without, 404 message |
| Content editor load | 404 for a missing entry **and** a distinct 404 for a missing schema, then schema/summary/payload |
| Validate action | unsaved form values coerced and sent, issues returned, upstream `422` passed through |
| Save action | the row version travels as the fifth argument, clean save returns `{ action: 'save', saved: true }`, `isValid: false` becomes a `400` with the issues, failed save keeps its status |
| Deactivate | redirect to `/content/{type}`, failure status preserved |
| Language fork | browse uses the `language` type, empty create form, display-name/slug guards, slug charset guard, the submitted payload's `phonology` lists, rejected submission as `400` |
| Notifications | inbox list and error, mark-read refusing a missing id, success `{ ok: true }`, failure as `400` |
| Submission queue | filters with `pageSize: 20`, fallbacks, create mode, `canReview` from the Curator role, view mode with its error, create/vote/review actions including the `+1`/`-1` guard and the `approved: 'yes'` mapping |

The form-to-payload rename is worth knowing: the language form field is `initialClusters`, but the
payload key is `allowedInitialClusters` (likewise `syllablePatterns` →
`allowedSyllablePatterns`). Assert the payload key, not the input name.

Measured after this PR: **71.35% lines**, 63.37% statements, 62.39% branches, 68.15% functions;
**662 tests in 48 files**. `src/routes/**/*.ts` now clears lines, functions, and statements — its
**branch** coverage is the last red item at 76.33%.

Still red: the `src/**/*.svelte` floor (58.05% lines — staff pages, layout chrome, site components),
`src/routes/**/*.ts` branches at 76.33%, and `src/lib/game/**/*.ts` branches at 82.74 (`session.svelte.ts`
guards, tracked with the `GameOverlays` work).

> **Closed by PR 13** (see below). Kept as the record of where this PR left the gate. The final web
> measurement is **99.19% lines / 99.24% statements / 98.83% functions / 95.54% branches** with
> **1,096 tests in 80 files** and every floor green — re-measured 2026-11-04 with
> `cd web && npm run coverage --workspace client`.

### PR 13 — the staff surfaces, the overlays, and the branch tail

Six new suites plus two for the request hook, 153 tests — the first commit of this PR. Starting from
PR 12's **71.35% lines / 63.37% statements / 68.15% functions / 62.39% branches** it reaches **96.85 /
96.85 / 96.44 / 81.71** (815 tests in 56 files). Lines, statements and functions now clear the 90 gate
and every per-glob line/statement/function floor. **Branch coverage was the remaining red gate** at
that point, and it was the only thing standing between this pass and the 90% the user chose — the
second commit below closes it.

| Suite | Covers | Tests |
|---|---|---|
| `src/routes/pages-staff.test.ts` | the nine admin pages and the four moderation pages | 28 |
| `src/routes/pages-editorial.test.ts` | the editorial overview, three lists, three editors | 19 |
| `src/routes/pages-foundry.test.ts` | content index/browse/detail/editor, languages, notifications, submissions | 25 |
| `src/routes/pages-site.test.ts` | root layout, header, footer, pager, `+error`, community, roster, patch-note article | 33 |
| `src/lib/game/overlays.test.ts` | quest log, shop, abilities, attributes, item details, party, settings, toasts | 23 |
| `src/lib/game/widgets.test.ts` | item tooltip, game header, combat panel, quest tracker | 14 |
| `src/hooks.server.test.ts` + `src/hooks.server.build.test.ts` | session copy onto `locals`, prerender bypass, `handleError` | 5 |
| `src/routes/foundry-loaders.test.ts` (extended) | the `?? '…'` / `status \|\| 502` fallbacks | +6 |

Traps worth recording:

- **A bits-ui dialog is named by its title, not its `aria-label`.** `getByRole('dialog', { name:
  'Item details' })` fails; the accessible name is the `Dialog.Title` content, so it is
  `{ name: 'Ember blade' }` there and `{ name: /Shop/ }` for a titled shop.
- **`anchor` is a testing-library mount option**, so passing it as a plain prop makes `render()`
  throw `UnknownSvelteOptionsError`. Pass `{ props: { …, anchor } }` instead.
- **Every repeated string needs counting, not matching.** Staff pages put the same word in a filter
  `<option>` and a status chip (`Open`, `Banned`, `Published`), so `getByText` throws
  "found multiple"; the assertions use `getAllByText(…).toHaveLength(2)` and say why.
- **Identity lines are concatenated nodes**, so an email assertion goes through
  `document.body.textContent` rather than `getByText`.
- **`$app/env` has to be mocked twice.** `building` is a compile-time flag; a single file cannot see
  both sides of it, hence `hooks.server.build.test.ts` beside `hooks.server.test.ts` — branch
  coverage merges across files, a mock does not merge across imports.
- **The roster's `lastSession` lookup is a two-step guard**: `data.lastSession` truthy and a
  matching character in the list. Covering only the matched case leaves the `?? null` side dark.
- Local runs need `web/client/.env` (gitignored) mirroring the `ci-web.yml` job env; without
  `PUBLIC_API_URL` / `PUBLIC_HUB_URL` SvelteKit refuses to start under Vitest with `env_invalid`.

#### Branch coverage closed (second commit of PR 13)

The gate is green. 24 branch-focused suites, 281 tests, moving the measured figures from the
staff-surface commit's 96.85 / 96.85 / 96.44 / 81.71 to:

| Metric | Before (first commit) | After (branch-tail commit) |
|---|---|---|
| Lines | 96.85% | **99.19%** (4702/4740) |
| Statements | 96.85% | **99.24%** (7596/7654) |
| Functions | 96.44% | **98.83%** (1614/1633) |
| Branches | 81.71% | **95.54%** (2937/3074) |

Every floor now clears, not just the global gate:

| Glob | Floor | Measured (lines / statements / branches / functions) |
|---|---|---|
| global | 90 | 99.19 / 99.24 / 95.54 / 98.83 |
| `src/lib/server/**` | 92 | 99.67 / 99.68 / 100 / 100 |
| `src/lib/game/**/*.ts` | 92 | 99.73 / 99.54 / 99.47 / 100 |
| `src/lib/foundry/**` | 92 | 100 / 100 / 98.08 / 100 |
| `src/routes/**/*.ts` | 90 | 99.49 / 99.53 / 97.83 / 100 |
| `src/**/*.svelte` | 85 / 85 / 80 / 85 | 98.95 / 99.13 / 92.66 / 98.38 |

Each row is measured over exactly the files its glob selects. Vitest resolves these threshold globs
with `pm(glob)` against root-relative paths, so `src/lib/game/**/*.ts` and `src/routes/**/*.ts` never
match the `.svelte` files beside them; the whole-directory aggregates that do include those components
are lower (99.88 / 99.49 / 95.68 / 99.08 and 98.70 / 99.04 / 94.77 / 98.34) and are not the floored
numbers.

`cd web && npm run coverage --workspace client` — **80 files, 1096 tests, all passing, exit 0**, so
the thresholds are enforced rather than merely reported.

The suites are companions, not replacements: each one targets only the arms its predecessor left
dark, which keeps a failure pointing at a specific behaviour instead of at "a branch". The shape is
consistent enough to reuse:

| Group | Suites | What they add |
|---|---|---|
| Game shell and overlays | `game-screen-arms`, `game-overlays-arms`, `left-sidebar-branches`, `shell-branches`, `widgets-branches`, `item-icons-branches` | the sparse-value side of every interpolated label, the untaken `switch` default, pending-discard and tooltip state |
| Pure game logic | `session-events`, `reducers-branches`, `cardinal-layout-branches`, `point-buy-branches`, `types-branches` | optional-field event arms, name-check race guards, the direction switches in the map layout, unpriced cost entries |
| Routes | `pages-creation-branches`, `pages-staff-branches`, `pages-foundry-branches`, `pages-editorial-branches`, `pages-public-branches`, `pages-roster`, `pages-account-branches` | a deliberately sparse payload per page (null counters, absent display names, missing dates) and the `{#if record}` fallback in edit mode |
| Server modules | `staff-server-branches`, `public-server-branches`, `auth-branches`, `hooks.server-branch` | every upstream rejection as `fail(400)` carrying the API message, list fallbacks, and the lazy Better Auth singleton's half-configured providers |
| Shared chrome | `hub-branches`, `component-branches` | the non-2xx game-token fetch, and each of the four OAuth click outcomes |

What made the tail tractable:

- **Svelte compiles `{expr}` to `expr ?? ''`.** Nearly every missing branch in a template is that
  fallback, so one render with a nullish field covers it — no new harness, and the assertion stays
  on the rendered text, which proves the arm actually ran.
- **`<select value={…}>` compiles to `if (select_value !== (select_value = …))`.** The false side
  and the inner `?? ''` cannot execute during a static render, so they are not reachable from a
  test at all. That is the bulk of the 137 branches still uncovered below.
- **Shared state needs a fresh render per case.** The overlays, the character sheet, and the wizard
  each own pending state, so a shared `beforeEach` render leaks it between assertions; `getByRole`
  also starts matching a neighbouring panel if two are mounted.

Residual uncovered branches: **137 of 3074**, none of them a gate. 46 files hold at least one; the
largest are `characters/new/+page.svelte` (17), `GameOverlays.svelte` (14),
`(foundry)/…/edit/+page.server.ts` (10), `LeftSidebar.svelte` (8), and `GameScreen.svelte` (6).
They are compiled-guard artefacts and defensive arms for states the API cannot currently produce,
so they are recorded rather than chased — a branch number is not worth a test that asserts nothing.

There is a small residual on lines too, so 99.19% should not be read as full coverage:
`src/routes/game/+page.svelte` (the client-side redirect fallback, 5 lines) and
`src/routes/play/[id]/+page.svelte` (mounts `GameScreen` and calls `session.connect`/`disconnect`,
8 lines) report **0%** and account for **13 of the 38** uncovered lines app-wide. Neither body runs
without a live server, hub and character: the e2e suite (`tests/game-auth.e2e.ts`) only asserts their
unauthenticated redirects, so today they are covered by nothing. That is 13 lines of wiring rather
than a gate, but it is why the line number is 99.19% and not 100%.

## Coverage comment accounting (decided 2026-11-20)

The web coverage **gate** and the web coverage **comment** disagreed on branch coverage for the same
run, and the comment read as a gate failure: `Branches : 90.33% ( 4428/4902 )` in the
`Build and Test SvelteKit Web` log beside `Branch coverage | 89.8% (3,752 of 4,180)` in the sticky
comment (PR #88, head `3a128de0`, run 2171). Neither number was wrong; only one of them was the
number being enforced, and the comment did not say which.

### Why the two differ

- The gate is vitest/v8: `coverage.thresholds` in [`web/client/vite.config.ts`](../web/client/vite.config.ts:78)
  counts branch **sites** — 4,902 of them on that run, 4,428 covered.
- The comment's table is `scripts/coverage-report.sh` → `codecov/codecov-report` (pinned
  `v1.0.3`) fed `web/client/coverage/cobertura-coverage.xml`. `CoberturaReader.ReadLines`
  (`src/CoverageReport/CoberturaReader.cs:119-160` in that repository) sums the per-line
  `condition-coverage="x% (c/t)"` attributes and never reads the root
  `<coverage branches-valid=… branches-covered=…>` attributes.
- istanbul writes a line-level condition only for a branch site it can attach to a source line, so
  that sum is a **subset** of v8's branch map: 4,180 of 4,902 sites on the same run. The gap is not
  a coverage regression and not a broken report — it is two different branch models.
- The Cobertura root attributes are istanbul's branch-map totals and therefore agree with v8
  exactly. Reproduced on `development@44422ac2` with `reportOnFailure` on:
  gate `Branches : 89.41% ( 4383/4902 )`, reporter `88.8% (3,713 of 4,180)`, XML root
  `branches-valid="4902" branches-covered="4383"`. So the lossy step is the per-line conversion,
  not the file as a whole.

### The decision, and the two options not taken

**Option 1 — the comment quotes the gate.** The sticky comment's `web` section now opens with the
totals the `Coverage gate` step printed, and the reporter's per-directory table follows, explicitly
labelled as Cobertura accounting with both numbers for the run spelled out. A reviewer scanning the
PR cannot read a percentage that contradicts the check next to it.

- *Option 2 alone* (keep the reporter's number, label it "not the enforced gate") was rejected as
  insufficient: the number a reviewer reads first would still be the one nothing enforces, and the
  difference would have to be re-derived by hand on every run. The labelling survives, but beneath
  the gate block rather than instead of it.
- *Option 3* (declare the Cobertura branch count authoritative and move the gate onto it) was
  rejected as the most expensive and the least defensible: it would loosen the gate by 722 branch
  sites, need the per-glob floors rebuilt on a branch model that silently drops sites, and pin the
  gate to a third-party tool's line-level conversion.

### What implements it

- [`scripts/web-coverage-gate.mjs`](../scripts/web-coverage-gate.mjs) parses the last
  `Statements/Branches/Functions/Lines` block from the coverage step's log (ANSI-stripped) and every
  `ERROR: Coverage for …` threshold line, so **per-glob floor breaches appear in the comment too** —
  no machine-readable artifact carries them. It cross-checks the log against
  `coverage-summary.json` (v8's own totals) and against the Cobertura root attributes, and prints a
  warning instead of silently preferring one when they drift. Fixture tests:
  [`scripts/tests/web-coverage-gate.test.mjs`](../scripts/tests/web-coverage-gate.test.mjs).
- [`ci-web.yml`](../.github/workflows/ci-web.yml:182) tees the coverage step to
  `$RUNNER_TEMP/web-coverage.log`, keeps npm's exit status via `pipefail`, renders the block and
  concatenates it ahead of `SummaryGithub.md` into `CoverageReport/WebSection.md`, which is what the
  commenter posts.
- `coverage.reportOnFailure: true` ([`vite.config.ts`](../web/client/vite.config.ts:67)) so a failed
  gate still writes the report — previously a red run produced no Cobertura at all, so the comment
  kept the *previous* head's numbers beside a red check. The comment step now also runs with
  `if: always()`.
- Recorded in [`scripts/README.md`](../scripts/README.md) and [`docs/ci-forgejo.md`](../docs/ci-forgejo.md)
  ("Coverage reports").

### Verified on a run, not just locally

The reporting path was exercised end to end by CI on a throwaway branch that stacked this change on
PR #88's head (whose branch fixes the five `web/client/tests/run-e2e.mjs` typecheck errors that stop
`build-test` before the coverage step on `development@44422ac2`): run 2176, `Build and Test SvelteKit
Web`, **successful in 11m51s**. From that one run:

```text
Statements   : 96.77% ( 10901/11264 )
Branches     : 90.33% ( 4428/4902 )        <- the gate
...
Line 96.7% · branch 89.8% · function 95.1% · full function 95.1%   <- the reporter, unchanged
```

and the comment it posted (PR #90, comment 1084):

```markdown
| Statements | 96.77% | 10,901 / 11,264 |
| Branches | 90.33% | 4,428 / 4,902 |
```

with the contrast paragraph reading *"It counts 3,752 of 4,180 branch conditions against the gate's
4,428 of 4,902 branch sites: 722 sites carry no line-level condition"*. The gate line and the
comment's branch row are the same number, which is the property the old comment lacked.

### Fixed upstream, adopted here

The residual was the reporter's own headline, and it belongs to
[`codecov/codecov-report`](https://git.kungraseri.cloud/codecov/codecov-report), not to this
repository. It was fixed there (PR #1, merged as `f6a5681`) and tagged **`v1.0.4`**: the reader now
takes the root `branches-valid`/`branches-covered` attributes when a file provides them and falls
back to the per-line sum for coverlet, whose per-line conditions are complete. Both on-demand paths
pin that tag — [`scripts/coverage-report.sh`](../scripts/coverage-report.sh) and
[`generate-coverage-report.ps1`](../scripts/generate-coverage-report.ps1) — so on the PR #88 file
the reporter's Total row reads `4,428 of 4,902`: the gate's pair, rendered with one decimal
(`90.3%`) instead of two (`90.33%`). The counts are the contract; the precision difference is not a
defect.

What remains is only the **per-directory table**: its rows are still built on istanbul's per-line
conditions (`3,752 of 4,180` on that run), so they do not sum to the Total.
[`scripts/web-coverage-gate.mjs`](../scripts/web-coverage-gate.mjs) says exactly that in the
comment, and switches to a "this file is not from this run" paragraph when the root counters
disagree with the gate — the gate block itself is unchanged.

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
