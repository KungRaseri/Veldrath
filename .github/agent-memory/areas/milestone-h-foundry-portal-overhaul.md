---
kind: area
date: undated
area: web
title: "Milestone H — Foundry portal overhaul"
---

## Milestone H — Foundry portal overhaul

Plan: [`plans/foundry-overhaul.md`](../../../plans/foundry-overhaul.md). **H0 is shipped** (shell, home,
grouped sidebar, group error page, `/languages` redirect); H1–H6 follow.

**The portal home is `/foundry`, not `/`.** `web/client/src/routes/+page.svelte` is the public site's landing
page, and a `(foundry)/+page.svelte` would resolve to the same URL — a SvelteKit route conflict, not
something to work around. `FOUNDRY_HOME_HREF` in `web/client/src/lib/foundry/nav.ts` is the single place the
home route is spelled.

**Access split.** Root `+layout.server.ts`: `staff.foundry = hasPermission(content.view)` and
`staff.submit = Boolean(user)`. `SiteHeader` renders "Foundry" → `/foundry` for the former and
"Submit content" → `/submissions/new` for the latter, never both. The Foundry home and every content loader
call `requirePermission(access, Permissions.ViewContent)`; `/submissions*` and `/notifications` stay
session-only.

**`canReview` is the Curator role, not the `content.review` permission.** The decision endpoints are
`.RequireAuthorization("Curator")`, so a hand-granted permission would render a Review entry that answers
403. Note `hasPermission` returns `true` for `Admin`, which does **not** hold the Curator role — the role
check is deliberate, and reconciling the two models is H5 work.

**Unread badge.** `GET /api/foundry/notifications/unread-count` → `FoundryUnreadNotificationCountDto(int
Count)`, served by `FoundryService.CountUnreadAsync` with the same predicate as `GetNotificationsAsync`. The
group layout fetches it once and degrades a failure to `0` rather than taking the whole group down.

**Shell components** live in `web/client/src/lib/components/foundry/`: `FoundrySidebar`,
`FoundryPageHeader`, `FoundryBreadcrumbs`, `FoundryEmptyState`, `FoundrySection`. `(foundry)/+layout.svelte`
owns the frame (`max-w-6xl`, one padding scale, sticky sidebar that becomes a chip row below `lg`); no page
may reintroduce its own `mx-auto max-w-*`.

**Gotchas:**
- `resolve()` cannot target a dynamic route with a literal — `resolve('/(foundry)/submissions/new')` is a
  typecheck error. Use the plain path string for `/submissions/new`.
- `/languages/+page.server.ts` is a `redirect(308, '/content/language')` that preserves the query; its
  `+page.svelte` is deleted. `/languages/new` stays until H4.
- `(foundry)/+error.svelte` renders inside the shell, so a 403 names the missing capability without dropping
  the reader out of the portal. It does not cover a failure in the group layout's own `load` — that still
  falls through to the root boundary, which is why the unread count degrades instead of throwing.
- The legacy pages (`content/*`, `submissions/*`, `notifications`, `languages/new`) lost their frames in H0
  but keep their own `<header>` blocks; they adopt `FoundryPageHeader`/`FoundryBreadcrumbs` as each is
  rewritten in H1+.
- Adding a route means adding it to the Playwright guard list (`tests/staff-guards.e2e.ts`) or the parity
  suite fails: `/foundry` and `/review` were added there, and the old `/languages` exemption is gone.

**Counts after H0:** 1,319 web unit tests, 26 Playwright tests (2 skipped), coverage gate green.

### H1 — the content editor became a form

`web/client/src/lib/foundry/content-editor.ts` gained five pure helpers, all unit tested: `slugify`,
`anchorId`, `isDirty`, `missingRequired`, `parseMarkdown`.

- **Derived slug** behind a "Derive from display name" checkbox. Locked by default on an existing entity,
  because the slug is what other content links to.
- **`parseMarkdown` returns blocks and spans, never HTML.** The page renders them with a `{#snippet inline}`
  and Svelte markup, so there is no `{@html}` and `svelte/no-at-html-tags` stays satisfied. Do not
  "simplify" it back to an HTML string: the write tooling decodes HTML entities in file content, which is
  how the first escaping renderer ended up with a no-op `.replace(/&/g, '&')`.
- **Required blocking happens client-side too**: `missingRequired` disables Save and labels the field. A
  boolean is never blank; an empty number is blank rather than `0`.
- **Sticky bar** Save / Validate / Discard, with `isDirty` driving a `beforeunload` guard and an
  "Unsaved changes" note. Discard reseeds from the loaded payload; it is not a form action.
- The page uses `FoundryPageHeader` and `FoundryBreadcrumbs`, and the required asterisk is `aria-hidden`
  so a field's accessible label stays exactly its name.
- A sparse record with neither display name nor slug used to render `Edit null` — a template literal
  stringifies `null`, unlike Svelte's `{x}` interpolation. The heading is now built conditionally.
- `(foundry)/+error.svelte` had zero coverage until H1's coverage pass; it now has three render tests
  (403 naming the capability, 500 with a retry, 404 without).

**Counts after H1:** 1,350 web unit tests, coverage 90.04% branches against the 90 global gate,
typecheck/lint/build clean.

### H2 — collections are first-class in the schema

`ContentFieldType` gained `StringList`, `KeyValue`, `ObjectList` and `Reference`; `ContentFieldDescriptor`
gained `Items` (the row shape), `RefType` and `Multiple`. `quest.objectives`, `quest.rewards`,
`recipe.ingredients` and `loottable.entries` are now `ObjectList`s, and recipe's `outputItemSlug` and
`craftingSkill` are `Reference`s. **The stored JSON did not change** — only how it is authored.

- **Form state is a union now**: `FieldValue = string | boolean | string[] | ContentRow[]` and
  `FieldValues = Record<string, FieldValue>`. `isDirty` compares collections with `JSON.stringify`
  because the editors rebuild arrays on every edit; a reference comparison would mark the form dirty on
  first render.
- `coerceFieldValue` passes collections through, except `KeyValue`, which is edited as rows and stored as
  an object via `pairsToObject`.
- `Recipe`/`LootTable`/`Quest` row fields stay `Text` for slugs *inside* rows: `writePath` addresses
  dot-paths, not array indices, so a `Reference` inside a row only gets a datalist, not a dot-path value.
  Top-level Reference fields are the ones the loader preloads options for.
- The edit loader now calls `browseContent(refType, null, 1, 50)` per distinct reference type, including
  ones nested in `Items`; a failure yields an empty option list rather than a broken form.
- `src/lib/parity/field-kinds.test.ts` reads `ContentFieldType` out of `Veldrath.Contracts` and compares
  it with `CONTENT_FIELD_KINDS`, so a kind added on one side fails a test.
- **`write_to_file`/`apply_diff` decode HTML entities in file content.** That silently turned an
  `&` escape into a no-op in H1 and it is why the markdown preview now returns spans, not HTML. Do not
  write entity text into source through these tools.
- The global branch gate sits right on 90, so every new branch counts: H2 needed explicit tests for an
  enum option labelled `(empty)`, a Reference with no `refType`, a `KeyValue` holding a scalar, and a
  schema with a slug but no display name before it went green again (90.02%).

**Counts after H2:** 1,382 web unit tests, 6,060 .NET tests, coverage 90.02% branches, typecheck/lint/build
clean.

### H3 — the read paths render fields, not JSON

`web/client/src/lib/components/foundry/ContentRenderer.svelte` renders a payload through the schema: groups
as cards, a `<dl>` of label → value, booleans as Yes/No, empties as a dash, `StringList` as chips, `KeyValue`
as a small table, `ObjectList` as a table whose columns are the row descriptors, and a `Reference` as a link
to `/content/{refType}/{slug}`. Keys the schema does not describe are listed under "Other fields"; the raw
JSON lives behind an "Advanced — raw JSON" disclosure. **That disclosure is now the only `<pre>` under
`(foundry)`** — the H3 done-when.

- The content detail loader now loads the schema too (`getContentTypeSchema`), best-effort: a missing schema
  is not fatal, the renderer just lists every key.
- Submission create is schema-driven. The type picker is a **GET** (`/submissions/new?type=quest`) because
  the server renders the chosen schema; the POST action rebuilds the payload with `buildPayload` and refuses
  with `Fill in: <fields>` when a required field is blank. **Collections are not authorable there yet** — the
  form names them and points at the content editor; H4 renders the editors in the create form.
- The submission view parses the stored payload and renders it with the same component. A payload that is
  not valid JSON is shown verbatim with a warning rather than swallowed.
- `ContentTypeInfo` is `{ contentType, displayLabel, description }` — **not** `{ key, label }`. Several older
  Foundry test mocks used `key`/`label`; the real fields are the longer ones.
- Watch for duplicate accessible labels: the submission form has its own "Description" textarea, so a schema
  field also labelled "Description" makes `getByLabelText` ambiguous.

**Counts after H3:** 1,397 web unit tests, coverage 90.01% branches, typecheck/lint/build clean.

### H4 — create for every content type

`/content/[type]/new` renders the type's own schema, so a new content type needs no new page. It is
deliberately a **plain form with no client-side state**: it creates an entity from its scalar fields and then
sends the author to the editor, which owns collections. Demanding collections at create time would have made
every collection-bearing type uncreatable, because a stateless form has no list/row editors.

- `referenceOptions`/`referenceTypes` moved to `#lib/server/content-refs.ts` (the edit and create loaders
  both need them). `isScalarKind(field)` in `content-editor.ts` is the one definition of "a field a plain
  control can author" — do not re-derive `fieldType !== 'StringList' && …` locally.
- The browse page's loader gained `canEdit`, and the page shows a "New entry" link only for an author.
- Submission create still cannot author collections; that is a deliberate deferral, not an oversight.
- **A malformed `apply_diff` with a SEARCH but no `=======` silently deletes the matched text.** One such
  block removed a plan bullet during H4; it was caught by reading the region afterwards, not by the tool.

**Counts after H4:** 1,412 web unit tests, coverage 90.01% branches, typecheck/lint/build clean.

### H5 — an approval lands content, and the reviewer sees the diff

`ContentWriteService` (server) is the one write path onto the catalog, used by Foundry's review gate. It runs
the same primitives as `ContentWriteEndpoints.CreateAsync` — `ContentEntityTypes.Deserialize`,
`ContentReferenceCatalog`, `ValidatorRegistry` — so a payload the editor refuses is refused here for the same
reason; only the orchestration is repeated. `ReviewAsync` promotes **before** flipping the status, so a failed
write leaves the submission `Pending` with the reason. `FoundrySubmission.PromotedSlug` records where it
landed (migration `AddSubmissionPromotion`), the DTO carries it, and the approval notification names it.

- **A content payload needs `typeKey`.** "Item has no type key, so content may not load it" is the error a
  payload gets without it; a minimal valid item is
  `{"slug","displayName","typeKey":"material","itemType":"material","rarityWeight":50,"isActive":true}`.
  `FoundryEndpointTests.PromotableItem` is the fixture.
- **Approval is stricter on purpose**: every existing approve test had to gain a promotable payload, because
  `{}` cannot be written. Do not "fix" that by skipping promotion when the payload is empty.
- The web diff is `diffFields(schema, previous, next)` in `content-editor.ts` plus `ContentDiff.svelte`. The
  submission loader fetches `getContentDetail(type.toLowerCase(), slug)` **only while the decision is open**;
  after that the diff is history.
- The same value appears in both the payload renderer and the diff table, so page tests must scope with
  `within(screen.getByRole('table'))` instead of `screen.getByText`.

**Counts after H5:** 6,062 .NET tests, 1,421 web unit tests, coverage 90.06% branches, lint/typecheck/build and
dotnet format clean.

### H6 — world health, schemas, and the defect the audit endpoint exposed

- `GET /api/content/audit` runs `ContentAuditService.RunAsync` and returns `ContentAuditIssueDto` rows; it
  requires `content.view`. The service is registered as **both** a singleton and a hosted service
  (`AddHostedService(sp => sp.GetRequiredService<ContentAuditService>())`) so startup and the endpoint share
  one instance — `AddHostedService<T>` alone registers only `IHostedService`, so injecting the concrete type
  would fail.
- **The audit never ran on SQLite.** It projected nested collections (zone actor pools, quest objectives, map
  nodes), which needs SQL `APPLY`; SQLite cannot translate it, the startup run swallows exceptions, and
  `ContentAuditServiceTests` asserted that translation failure as expected behaviour. It now materialises and
  flattens in memory. If you add a rule that projects a collection, keep it out of the query.
- `/world-health` and `/schemas` require `content.view`, are in the sidebar's Reference group, and are in
  `tests/staff-guards.e2e.ts` (that is what satisfies the route-parity suite — no exemption needed).
- **`ContentIssue` has no content type**, only `Identifier` (a slug), so `/world-health` cannot deep-link a
  finding. Adding `ContentType` to the record is the follow-up.
- Cross-type Foundry search is **not** built: `browseContent` searches one type, and a global search needs an
  endpoint over every resolver.

**Counts after H6:** 6,062 .NET tests, 1,431 web unit tests, coverage 90.01% branches, 28 Playwright tests,
lint/typecheck/build and dotnet format clean.

### I — the access model, and navigation v2 (2026-11-16)

The H0 decision (§5.4 of `plans/foundry-overhaul.md`) made the portal staff-only, and the first real account to
use it got 403s on the home and every sidebar item. The replacement model is §8 of that plan; this is the list
of traps.

- **`AddPolicy("Curator", …)` is a role policy and it was on three decision endpoints.** `Permissions.All` is
  granted to Admin as permission *claims*, so an Admin passed `RequireAuthorization(permission)` but failed
  `RequireAuthorization("Curator")`. It now uses `Permissions.ReviewSubmissions` on submission review, map
  review and map deactivate. Any new "who may decide" check goes through the permission.
- **The submission feed had no authorization.** `GET /api/foundry/submissions` and `/{id}` were anonymous,
  payloads included. They now require a token, and `FoundrySubmissionScope` (`Everything`, `Own`,
  `OwnPlusApproved`) decides what is visible; `mine=true` narrows for everyone including a reviewer, and a
  non-reviewer's default feed is `OwnPlusApproved`, not `Approved` — the first version dropped the caller's own
  rows and a test caught it.
- **`GET /api/content/maps*` now requires a session, not `content.view`.** The graph carries hidden nodes and
  unlock keys; that spoiler cost is accepted and the mitigation, if needed, is rendering hidden nodes
  distinctly rather than closing the editor.
- **The `(foundry)` group still requires a user** (`requireUser`), so "public" means "signed in". The redirect
  carries `?redirectTo=` and [`web/client/src/lib/navigation.ts`](../../../web/client/src/lib/navigation.ts)
  rejects anything but a local path — do not inline that check, it is the open-redirect guard.
- **Layout flags are `canManageContent`, `canReview` (permission), `canAudit` (`content.view`).** There is no
  "can read" flag any more; if you find yourself adding one, the page probably should not be gated.
- **Nav v2:** one primary action (`/new`, the type picker) then Explore / Contribute / Curate / Maintain, with
  Notifications in a separate `account` list below a rule. `FOUNDRY_PATH_ROOTS` + `isFoundryPath` exist because
  the group has no URL prefix — the site header marks "Foundry" from any portal page through them.
- **`FoundrySidebar`'s icon map is exhaustive by construction and has no fallback icon.** A nav entry without an
  icon fails the sidebar tests rather than rendering a wrong glyph. That also removes an uncoverable branch.
- **Test gotchas:** `parentData` fixtures for the home loader must include `canReview` (a missing key is
  `undefined`, not false, and `toMatchObject({ canReview: false })` fails); the submissions page needs
  `scope` (`'mine' | 'all'`) and `canReview` in its fixtures, because the empty-state copy changed with the
  scope.
- **Coverage lands exactly on the gate.** Removing two branches from the sidebar left 4,249/4,721 = 90.00%. Any
  new branch in a Foundry file needs a test with it.

Deferred, recorded in the plan: `/submissions/new` is still `params.id === 'new'` inside `/[id]` (works, links
resolve, refactor is separate), and the type picker has no search.

**Counts after I:** 1,666 server tests, 1,442 web unit tests, coverage 90.00% branches, lint/typecheck/build
clean. Playwright was not run locally this pass; `tests/staff-guards.e2e.ts` gained `/new`.

### J — community map authoring (2026-11-17)

Plan: [`plans/community-map-authoring.md`](../../../plans/community-map-authoring.md). Branch
`feat/milestone-j-community-map-authoring`, cut from `development` at `836198b5` (PR #73, which is where
milestone I actually shipped — there is no `feat/milestone-i-*` branch).

- **Map writes are decided per map, not per endpoint.** `MapCaller(AccountId, CanManageAny, CanReview)` — with
  `SeesEveryDraft` and `MayEdit(createdByAccountId)` — is passed into `ListAsync`, `GetAsync`, `SaveAsync`,
  `SubmitAsync`, `DeleteDraftAsync`. An endpoint-level `content.manage` made every map an editor's map and left
  an author unable to write their own draft; do not put a permission back on those routes. `validate` is a
  session-only read.
- **A stranger's draft reads as 404, not 403.** `GetAsync(mapId, caller)` returns null for a draft the caller
  cannot see, so the endpoint's existing 404 covers it. A 403 would confirm the map exists.
- **`canEdit` rides on `MapSummaryDto` and `MapGraphDto`** and the editor disables its controls from it. Do not
  re-derive it from `content.manage` in the page — that is the bug the flag exists to prevent.
- **`map_without_exit` is a warning on save and a refusal at approval.** `ReviewAsync` calls
  `BlocksPublication(issues)` (the one rule that refuses publication). A test that expects a *save* to return 400
  for a map with no exit is wrong — it returns 200 and persists. This cost a test rewrite during J0.
- **`ValidateWorld` grew two inputs:** `locationZones` (slug → zone) and `liveMapIds`. New codes:
  `node_reference_zone_mismatch` (error), `location_missing_node` and `map_unreachable` (warnings). A `Location`
  node's `refId` is a **zone-location** slug, so a test helper wanting "a way out" must use `RegionExit` with a
  **region** slug (`cinderplain`) — a `ZoneExit` with a location slug fails `node_reference_unknown`.
- **Authored coordinates are ranks, not distances.** `MapEditorService.ToDto` densifies each axis
  (`DenseRanks`) so `x=2` beside `x=34` renders adjacent, and the canvas fixed the cell at
  `CARD = 80` + `GAP = 24`. The spread-out-map complaint was authored slack, not a CSS bug; do not "fix" it in
  `map-canvas.ts`.
- **The review queue validates each submitted map before a reviewer opens it** (`getMap` + `validateMap`), and a
  failed check reports `unknown`, not `blocked` — an unreachable validator must never read as "this map is
  broken". `reviewVerdict` / `reviewVerdictLabel` in `map-editor.ts` own that wording, including "refused · the
  map has no exit" for a blocked verdict with zero errors.
- **A deactivation notification needs a second read.** `MapLifecycleResponse` carries neither the author nor the
  name, so `MapEditorService.AuthorOfAsync` returns `MapAuthor(AccountId, Name)` (submitter, else creator) and
  `DeactivateMapAsync` titles the inbox row with the map's name. A rejection already notifies through
  `MapReviewOutcome`.
- **Test gotchas:** `getByRole('link' | 'button', { name: /^Create/ })` matches more than one control on the
  maps pages — index into `getAllByRole(...)`; `parentData` fixtures must carry every flag the layout returns
  (a missing key is `undefined`, not `false`); the `src/lib/server/**` function floor (92) fails when a loader
  test mocks `content-api`, so a new API function needs a case in `content-api.test.ts` as well.

**Counts after J:** 6,089 .NET tests (1,677 server, 2,744 core, 510 data, 1,158 shared), 1,463 web unit tests,
30 Playwright tests (2 skipped), coverage 90.03% branches, lint/typecheck/build and
`dotnet format --verify-no-changes` clean.

### K — web e2e: user workflows (2026-11-17)

Plan: [`plans/web-e2e-workflows.md`](../../../plans/web-e2e-workflows.md). The ask was blunt: "I can't always spend
time manually testing the UI." Before this, e2e proved *routes* (a spec mentioned every `+page.svelte`) and one
gated map smoke test — not one Foundry journey.

- **A browser mock cannot test these pages.** Every Foundry loader calls `gameApiFetch` on the SvelteKit server,
  so `page.route()` never sees the request. The specs need Postgres + `Veldrath.Server` + the preview: see
  [`scripts/e2e-stack.sh`](../../../scripts/e2e-stack.sh) (`start|stop|status`) and
  [`web/client/tests/support/stack.ts`](../../../web/client/tests/support/stack.ts).
- **Accounts are real, roles are written directly.** There is no bootstrap admin, so `grantRoles` inserts the
  `AspNetUserRoles` row (`AspNetUsers`/`AspNetRoles`, PascalCase quoted identifiers, no snake_case) after the
  first `/foundry` visit provisions the Identity row. Roles already carry their permission claims from startup
  seeding. Accounts are created through `/register`, so Better Auth owns the session.
- **JWT key mismatch is the #1 first-run failure** (`401 The signature key was not found`): the stack script
  reads `JWT_KEY`/`JWT_ISSUER`/`JWT_AUDIENCE`/`INTERNAL_BRIDGE_TOKEN` from `web/client/.env`. Do not hardcode them.
- **The API port is not 9000 by default** in Development: `appsettings.Development.json` says `:5000`, so the
  script pins `ASPNETCORE_URLS=http://127.0.0.1:9000` to match `PUBLIC_API_URL`.
- **Gating:** `E2E_STACK=1` enables the specs; `Auth__AccessCacheSeconds=0` is required (see below).
  `npm run test:e2e:stack` runs them; plain `npm run test` stays stack-free.
- **What CI does now:** the `e2e-game` job of `ci-web.yml` sets `E2E_STACK=1` and runs
  `game-loop` + `foundry-content` + `foundry-maps`.

Four defects these specs found (each only visible when the real app meets the real API):

1. `GET /api/account/profile` reported **no permissions** for a role holder — it read only per-user claims, while
   a role carries its permissions as *role* claims. Every ContentEditor and Curator looked permissionless, so the
   web app hid the authoring surface and the review queue from them. Fixed via `RolePermissionResolver` in
   `AccountService`, with two `AccountEndpointTests`.
2. A granted role did not apply for up to 60s: `AccountProvisioningService` caches the access snapshot. The four
   admin role/permission endpoints now call `AccountProvisioningService.Invalidate(accountId)`, and the TTL is
   `Auth:AccessCacheSeconds` (0 disables the cache).
3. A refused map approval surfaced as **"Request failed (400)."** — `game-request.ts#extractError` only read
   `error|detail|title`. It now reads `message` too, which is the shape every Veldrath operation result uses.
4. **Foundry could not create any content**: `ContentShapeRules.ValidateShared` requires `ContentBase.TypeKey` and
   no schema exposed it (`type_key_missing`); abilities also had the field named `abilityType` while the model and
   validator read `powerType`. `ContentSchema.cs` now authors `typeKey` in the shared Identity group and
   `powerType` (including `innate`) for abilities and spells.

Second pass (same day) closed the write half and added three more journeys. Five stack specs now:
`foundry-content` (create → edit → save → duplicate refused → deactivate → 404), `foundry-maps` (the map
lifecycle), `foundry-submissions` (a role-less player submits, a Curator approves, the catalog gains the entry,
the inbox reports it, Mark read clears it), `staff-areas` (editorial/admin/mod lists, a real user filter, and a
player refused all three) and `public-details` (`/lore`, `/patch-notes`, `/leaderboard`, `/community`, plus the
404 paths). Three more defects they found:

5. `ContentFieldType` and `ContentIssueSeverity` were serialized as **numbers** while the web switches on
   `'Integer'`/`'Boolean'`/`'Error'`. Every coercion missed, so the form posted `"50"` where an integer belonged and
   the write refused the body; the audit's severity badges were wrong too. Both enums now carry
   `[JsonConverter(typeof(JsonStringEnumConverter))]` — keep it if either enum moves.
6. A checkbox with no `value` submits `on`, and `coerceFieldValue` only accepted `'true'`: every save wrote
   `isActive: false`, the catalog hid the entry and the redirect target 404'd. Fixed in `coerceFieldValue`
   (`'on'` is true) and the edit form now sets `value="true"`.
7. The create form ignored the schema's boolean default, so new entries were born inactive. Fixed with
   `checked={field.default === 'true'}` on the create page.

**Trap worth remembering:** Playwright's `webServer` has `reuseExistingServer: !process.env.CI`, so a local rerun
silently reuses the previous `vite preview` build and your Svelte changes never reach the browser — the symptom
is a fixed bug that keeps failing. Run stack specs with `CI=true`. Likewise `scripts/e2e-stack.sh` kills a stray
`bin/**/Veldrath.Server` process before starting; without that, an old build keeps the port and every later run
tests it.

Third pass added the staff verbs and publishing. **Seven stack specs** now: `foundry-content`, `foundry-maps`,
`foundry-submissions`, `staff-areas`, `staff-actions` (warn → "1 warnings", mute 30 min → "Muted" chip, unmute →
cleared, on a real registered player), `editorial-publishing` (announcement → Publish → `/community`; patch note →
Publish → its public article and index link; empty body refused) and `public-details`.

- **The editorial create routes DO exist** — as `[id]` with `id === 'new'`, the same deferral as
  `/submissions/new`. `/editorial/announcements/new` is served by `[id]/+page.server.ts`, which switches on that
  id. Do not add a `new/` directory; you would shadow it.
- **The admin-action rate limit throttled the suite** (20/min, `RateLimit:AdminActionsPerMinute`). Loading an
  editorial page costs several admin calls, so `scripts/e2e-stack.sh` now sets
  `RateLimit__AdminActionsPerMinute=600` and `RateLimit__FoundryWritesPerMinute=600`. If a spec starts seeing
  429s, that is the knob.
- **A 429 used to read as "not found".** The three editorial `[id]` loaders did `error(404, result.error)`
  whenever data was missing, so a throttled or forbidden admin call sent the editor hunting for a record that
  exists. They now propagate the upstream status (`result.status === 0 ? 502 : result.status`). The same pattern
  is worth checking in any new admin loader.

Fourth pass closed E7. **Eight stack specs**; the newest is `admin-console` — an Admin grants Curator through
`/admin/users/[id]`, the *player's own context* (no re-login) sees the review queue and gets 200 on `/review`,
revoking returns 403 — the regression test for the snapshot-invalidation fix. `staff-actions` now also closes the
report loop (a second player files one via `POST /api/reports` with its own minted token, the moderator sees it
unresolved and resolves it), and `editorial-publishing` publishes a lore article too.

- **`parity/routes.test.ts` is down to two exemptions**: `/api/**` and `/languages/**`. The admin, mod, editorial,
  foundry, players and leaderboard exemptions are gone because a spec now drives each page.
- **The parity suite reads spec *source*, so a dynamic route needs its literal in the spec.** For
  `/admin/users/[id]` and `/mod/users/[id]` the specs record the real URL and re-`goto` it (a genuine
  deep-linkable assertion) with the route pattern named in a comment; do that rather than re-adding an exemption.
- **`/mod/reports`'s resolve action redirects to the *unfiltered* queue**, so a resolved row does not disappear
  from the page — assert its status chip flips to "Resolved", not that the row vanishes.
- **SvelteKit runs `+page.server.ts` and `+layout.server.ts` concurrently**, so a page load used to call the API
  before its layout's guard refused the request — a 403 line in the log and a wasted round trip per blocked
  page. Fixed: the 11 admin/mod page loaders `await parent()` first, and a full authenticated run now logs zero
  refused `/api/mod…`/`/api/admin…` calls. **Do this in any new gated page load**; the layout stays the one place
  that decides *entry to the group*.
- **A group that admits two permissions decides neither in the layout.** Since `t_d4240a82` the `(admin)` layout
  accepts `admin.users` **or** `admin.roles` (`plans/sveltekit-migration.md:317`), because the server split them:
  account administration and handing out authority are separate grants. Each admin page therefore awaits
  `parent()` and then requires the permission *its own API call* demands — user list, audit, sessions and status
  `admin.users`; reports `players.view`; server commands `admin.announce`; the dashboard branches (a role manager
  gets the link grid and no status call). The account page (`/admin/users/[id]`) is the only one that serves both:
  it reads `GET /api/admin/users/{id}` for `admin.users` and `GET /api/admin/roles/users/{id}` for `admin.roles`
  (authority fields only — no email, ban or warning state), and renders the role/permission forms only for the
  latter. Form actions run without layout data, so each one re-reads the profile through
  `requireActionPermission(locals, …)` before calling the API: an action for a permission the session lacks must
  never reach `/api/admin…`. The role page opens an account by username
  (`GET /api/admin/roles/users/by-username/{name}`) because the account list itself is `admin.users`.

Fifth pass closed E9: **kick is covered end to end.** `staff-actions` puts a player in the world (character via
`POST /api/characters` with a minted token, then `/play/{id}` waiting for `.map-node-cell button` — the hub's first
snapshot), the moderator kicks with a reason from `/mod/users/{id}`, and the player's own screen shows that reason
in the `session.error` banner (`GameScreen.svelte`). The seventh pass below gave `phase === 'disconnected'` its own
screen; the spec now asserts that instead of the banner.

Sixth pass (E11–E16) made the suite trustworthy rather than merely broad. **Twelve stack specs, fourteen tests,
~4.5 minutes**, plus thirty stack-free specs in ~18s.

- **`scripts/e2e-stack.sh start --fresh`** drops the compose volume so a run starts from the seed (the server
  migrates and seeds on boot). Use it when a failure smells like stale data. The same pass added
  `api-stop` / `api-start`, which manage **only** the API process — `upstream-failure.e2e.ts` needs them, and it
  self-skips when `.e2e-server.pid` is absent (CI runs `dotnet run` itself, so there is nothing to stop).
- **`session-states.e2e.ts`** is the session truth: clearing cookies mid-flow sends the visitor to
  `/login?redirectTo=` with the **whole** nested target (`/content/ability?page=1`), and signing in lands back there.
  If you touch the guard, `navigation.ts#localPathFrom` or the login action, that spec is the one to watch.
- **`game-journeys.e2e.ts`** drives the seven-step wizard (`Continue` until the last step, where the button reads
  `Create character`). Two traps: the wizard accepts **letters and spaces only** in a name, so a digit stamp is
  rejected before the availability check; and creating a character navigates to `/play/{id}`, **not** the roster.
- **`review-diff.e2e.ts` found a real defect:** the review page's diff says "this would replace the stored entry",
  but promotion called a create-only path and refused any submission whose slug existed — so no reviewer could
  accept a correction. `ContentWriteService.PromoteAsync` now upserts (row id survives, version bumps), with a
  server test that asserts the replace. Keep that in mind when reading `FoundryService.ReviewAsync`: approval is an
  upsert, not a create.
- **`playwright.config.ts`** now sets `retries: 1` on CI with `trace: 'on-first-retry'` and
  `video: 'retain-on-failure'`, and a 10s expect budget. A retry without a trace is a flake nobody can diagnose.

Seventh pass closed the two §6 leftovers.

- **`phase === 'disconnected'` has its own screen.** A kick used to leave the map, the travel buttons and the bag on
  screen, each firing a command into a closed connection. `GameScreen.svelte` now renders a `Disconnected` region
  (the moderator's reason plus "Back to characters") and drops the game shell and `<GameOverlays>` while that phase
  holds. Component tests are in `shell.test.ts` (`playing({ phase: 'disconnected' })`); the e2e kick test asserts the
  region, the reason, zero `.map-node-cell button`, and the landing on `/characters`. **Watch the coverage gate when
  adding `.svelte` branches**: this screen put branches at 89.95% until its component tests landed.
- **The review queue's N+1 is gone.** "Would approval accept this map?" is now answered server-side in
  `MapEditorService.ListAsync` from **one** world snapshot and returned on `MapSummaryDto.Review`
  (`MapReviewVerdictDto`), using the same `BlocksPublication`/error rule the decision applies, so a row cannot
  promise an approval the decision would refuse. `ValidateWorldAsync` became `LoadWorldAsync` plus a static
  `ValidateWorld(world, …)`. The web loader reads `map.review` and words a missing verdict as `unknown`, never
  `ready`. Do not reintroduce a `getMap`/`validateMap` pair per row on `/review`.

- **Running the stack specs locally needs the preview origin too.** `E2E_STACK=1` alone was not enough: Better Auth
  rejected the sign-up with `Invalid origin: http://localhost:4173`, because `web/client/.env` sets
  `ORIGIN=http://localhost:5173` (the dev server). That is now handled for you — `playwright.config.ts` starts the
  preview with `ORIGIN`/`BETTER_AUTH_URL`/`PUBLIC_*`/`DATABASE_URL` set, so
  `./scripts/e2e-stack.sh start --fresh` + `npm run test:e2e:stack --workspace client` is the whole recipe.
  **Never export those by hand**: a hand-set `PUBLIC_*` on the command line is ignored (they are inlined at build
  time), which is how the dev hub URL leaked into a "stack" run.

Eighth pass — **the .NET coverage gate was red, and it was `Veldrath.Server` branches**: 80.6% against a floor of 82
(measured 2026-11-17 with the pinned SDK 10.0.402, `dotnet test Realm.Full.slnx -c Release --collect "XPlat Code
Coverage"` → `scripts/coverage-report.sh` → `scripts/check-dotnet-coverage.mjs`). Driver: whole features landed with
no tests, and the gate only sees a regression once the branch count grows. Closed to **82.5% branch / 95.2% line**
(total 82.7% / 95.1%) with two suites:

- [`GetCraftableRecipesQueryTests`](../../../Veldrath.Server.Tests/Features/GetCraftableRecipesQueryTests.cs) — the
  craft panel's entire data source had **zero** tests, so `GetCraftableRecipesQuery.cs` sat at 0%. Eleven tests now
  cover the three gates in order (level → skill rank → materials), the joined shortfall sentence, ranked attribute
  parsing (`Skill_{id}_Rank`, unreadable JSON, missing rank = 0), the informational normalisation and the panel's
  ordering.
- [`DotEnvConfigurationTests`](../../../Veldrath.Server.Tests/Infrastructure/DotEnvConfigurationTests.cs) — the
  `.env` loader was at 5.9%. It is `internal`, so `Veldrath.Server.csproj` now has
  `<InternalsVisibleTo Include="Veldrath.Server.Tests" />`. **The class is in a `DisableParallelization`
  collection** because it writes process environment variables; do the same for any future env-mutating test.

The remaining large gaps are `CharacterCreationSessionEndpoints.cs` (65%), `GetInventoryHubCommand` (67%) and the
`GameHub` partials — worth ~2 more points if the floor ever needs margin. The floors themselves were not touched.
**Still open — and it is a game defect, not only a test gap:** combat and death (E13c/d) need a known encounter,
and there is none. **No seeded zone location has an `ActorPool`** (verified with
`jsonb_array_length("ActorPool")` over `ZoneLocations`: every row is 0), and `ActorPoolResolver.SpawnRosterAsync`
returns an empty roster for an empty pool *by design* — the old `"common"` fallback was removed so a missing pool
could not be silently faked. So nothing spawns anywhere, the HUD's "Local" panel can never list a hostile, and
`CombatPanel` never mounts for a real player. The content audit only **warns** on an empty pool ("safe locations
are legitimate"), so a wholly empty world passes. Also: only **4 actor archetypes** exist (two of them NPCs —
`tavern-keeper`, `village-blacksmith`), the 4 loot tables are referenced by **no** archetype, and two of them name
content that does not exist (`wolf-common-drops` has no wolf, `bandit-boss-drops` has no boss).

**Measurement trap, and a correction.** I first reported that no location had `Traits.SafeRespawn = true` and that
every death logged `Zone {ZoneId} has no location flagged SafeRespawn`. That was **my query, not the code**:
`ContentModelConfiguration` stores traits with `ToJson()`, so the JSON keys are the property names —
`"Traits"->>'SafeRespawn'`, `IsDungeon`, `IsTown` — and I had used camelCase (`'safeRespawn'`), which matches
nothing. The seeders do set it (8 towns/waypoints plus one `SafeRespawn`/`IsDungeon = false` lobby in every dungeon
zone), so respawn and the lobby lookup work. **Always query these columns in PascalCase.**

Everything is written up in [`plans/content-seeding-pass.md`](../../../plans/content-seeding-pass.md): measured
counts, P0/P1/P2 priorities, the guardrail test to add to `SeededContentValidationTests`, and what each item
unblocks. **Return here after the content pass**, then write E13c (engage → victory) and E13d (die → respawn).

Ninth pass — **the workflow specs run against their own isolated stack, and getting that green exposed four
cross-wirings**. `scripts/e2e-stack.sh` is a standalone compose project
([`docker-compose.e2e.yml`](../../../docker-compose.e2e.yml), `veldrath-e2e`, own volume, own container name) on
**Postgres 55432 / API 9100 / preview 4273**. Nothing is shared with a development environment, `start --fresh`
drops the volume first (a green run then means "works against the seed"), a port it did not take is reported
instead of stolen, and it never `pkill`s — an earlier version killed a developer's own `dotnet run` on :9000.

Each failure below was a **configuration** defect that a single-spec run hid:

1. **`PUBLIC_HUB_URL` was never injected.** `PUBLIC_*` are inlined at `vite build`; setting only
   `PUBLIC_API_URL` left the browser's SignalR connection pointed at `.env`'s dev hub on :9000, so
   `.map-node-cell` never rendered and encounters/game-journeys/staff-actions all failed looking like client
   bugs. `playwright.config.ts`'s `webServer.env` now sets `PUBLIC_API_URL` **and** `PUBLIC_HUB_URL`, `ORIGIN`,
   `BETTER_AUTH_URL`, `DATABASE_URL` (the auth role — `.env` points at the dev DB on 5433) and
   `AUTH_RATE_LIMIT_DISABLED`.
2. **Better Auth rate-limited the run.** ~40 sign-ups a minute from loopback answered
   `Too many requests. Please try again later.` on `/register`: the client address cannot be resolved, so one
   bucket is shared per path. [`auth.ts`](../../../web/client/src/lib/server/auth.ts) reads
   `AUTH_RATE_LIMIT_DISABLED` (test stacks only, never the image) **and** now sets
   `advanced.ipAddress.ipAddressHeaders = ['x-forwarded-for']` — that second part is the production fix behind
   Coolify/Traefik, where without it every player shares a single bucket per path.
3. **`upstream-failure.e2e.ts` stops the API**, so it cannot share a run with anything. `test:e2e:stack` is now
   `:specs` (parallel) then `:outage` (`--workers=1`), and CI mirrors the split in two steps. The spec's own doc
   comment already said "run with one worker"; the script was what ignored it.
4. **Every sign-up paid a 10s SMTP timeout.** Development settings point `Email:SmtpHost` at a mail container
   this stack does not run, so the bridge call hung until the web app's `AbortSignal.timeout(10_000)` fired.
   The e2e API now starts with `Email__SmtpHost=""` → `NullEmailSender` → immediate answer.

Also: `tests/game-loop.e2e.ts` stopped hard-coding `http://localhost:9000` and imports `apiBase`/`gameToken`
from [`tests/support/stack.ts`](../../../web/client/tests/support/stack.ts) like every other spec, so `E2E_API_PORT`
reaches it too.

Proof: `scripts/e2e-stack.sh start --fresh` then `npm run test:e2e:stack --workspace client` → **17 passed in
about a minute** (16 in parallel, then the outage spec), with 1,470 web unit tests and a clean typecheck/prettier.
Documented for humans in [`web/client/README.md`](../../../web/client/README.md).

Tenth pass — **two defects found by a person using the wizard, and the layout harness that now catches the class.**

- **`.btn` is a single-line control.** Skeleton sets `white-space: nowrap` on it, and a grid item defaults to
  `min-width: auto`. Put a sentence in a `.btn` inside a grid and the row becomes as wide as the *unwrapped*
  sentence: measured on the creation wizard, a **1290px row in a 574px track**, document 1643px in a 1280px
  viewport, so every page gained a horizontal scrollbar. Fixing it needs **both** `white-space: normal` and
  `min-width: 0` (wrapping alone still cannot shrink below the longest word). The primitive is
  `.choice-option` in [`brand.css`](../../../web/client/src/routes/brand.css) — imported after Skeleton, so it wins
  on order, no `!important`, no layer fighting. **Do not put a description inside a `.btn`.**
- **The point-buy budget is 27, stats 8–15, cost `{8:0, 9:1, 10:2, 11:3, 12:4, 13:5, 14:7, 15:9}`** — so
  15/15/15 is exactly 27 and 15/14/12/11/10/10 is exactly 27. `AllocateCreationAttributesHandler` used to accept
  any allocation *under* budget and store it, and `FinalizeCreationSessionCommand` passed it to
  `CreateCharacterCommand`, so the API could create a minimum-stats character with 27 points unused. Both refuse
  now (missing / partial / out-of-range), and the wizard disables Continue with a hint while points remain.
  **Tests that walk the wizard must spend the budget**: `spendAttributePoints` in
  [`tests/support/creation.ts`](../../../web/client/tests/support/creation.ts).
- **Layout is asserted, not photographed.** [`tests/support/layout.ts`](../../../web/client/tests/support/layout.ts)
  `expectNoHorizontalOverflow` checks `document.scrollingElement.scrollWidth` and every descendant rect against its
  container, skipping descendants inside an `overflow-x` clipper (the map cells truncate deliberately).
  [`tests/layout.e2e.ts`](../../../web/client/tests/layout.e2e.ts) drives the seven steps at 1280/768/390 plus
  `/`, `/lore`, `/patch-notes`, `/community`, `/register`, and signed in `/characters` and `/account`. **Negative
  control run before keeping it**: removing `choice-option` from the species row fails it with "the page scrolls
  sideways by 949px". jsdom has no layout, so a Vitest test can never catch this class.
- **Sweep of the other 196 `.btn` sites**: the wizard was the only sentence-in-a-button, and the only one in a
  single-column grid track. Account/mod/admin labels are two words in `flex flex-wrap` rows (they wrap instead of
  overflowing); the game panels keep dynamic text in a `<span>` beside short verbs. `/account` is the riskiest
  variable-content surface and is in the harness.

Eleventh pass — **the forest can now be foraged from, and the engine's harvesting command cannot be reused.**

- `LootTablesSeeder` gained `forest-forage` (`TypeKey = "harvesting"`, `IsHarvesting = true`, guaranteed
  `glowcap-mushroom` 1–2). Until this there was **no harvesting loot table at all**, so a node could be
  harvested for nothing — and `gather-mushrooms` had no possible source.
- [`HarvestableNodesSeeder`](../../../Veldrath.Server/Data/Seeders/HarvestableNodesSeeder.cs) authors five nodes in
  `wood-road` and `old-clearing` (the Ashlen Wood), seeded from `Program.cs` against `GameDbContext`, idempotent
  by `NodeId` so **a restart never restores a depleted node**. `LootTableRef` is the engine's reference form:
  `@loot-tables/harvesting/forest:forest-forage` (the roll uses the part after the last colon).
- **`HarvestNodeCommand` is single-player and must not be wired to the hub.** It reads
  `ISaveGameService.GetCurrentSave()` (one global "current save") and writes the bag through the save-game
  store, not the character blob the client reads. The multiplayer path composes the *stateless* pieces —
  `HarvestCalculatorService`, `CriticalHarvestService`, `ToolValidationService`,
  `LootTableService.RollHarvestingDrops(node.LootTableRef, yield, isCritical)` (returns `ItemDrop` with an
  `@items/general:<slug>` ref) — plus `IInventoryService.AddItemAsync(name, itemRef, qty)`, exactly as combat
  loot does. All four services are registered by `AddRealmEngineCore`.
- `HarvestableNodes` lives in **`GameDbContext`** (live state), while `DatabaseSeeder.SeedAsync` is content-only
  and is reused by `SeededContentProbe` with just a `ContentDbContext` — so node seeding belongs in `Program.cs`,
  never inside `SeedAsync`.
- Design decisions for the hub slice (recorded in [`plans/content-seeding-pass.md`](../../../plans/content-seeding-pass.md)):
  authored nodes, `node.LocationId == character.CurrentZoneLocationSlug` as the guard, MinToolTier 0 only,
  skill rank 0 and no skill XP yet, `collect` advanced with both slug and ref, `LocationNodes` pushed on
  `EnterLocation` and refreshed after failures, and the web consumer in the same commit (parity suites).
- **The hub slice is shipped and the loop is green** (`GetLocationNodes`/`HarvestNode`, `LocationNodes`,
  `Harvested`, a Gatherable panel section). Getting the spec green exposed four things worth remembering:
  - **`IInventoryService` (`EfCoreInventoryService`) writes save-game records, not `Characters.InventoryBlob`** —
    the bag the client reads. Both harvest and combat loot now write the blob through `CharacterBag`
    (twelfth pass below).
  - **Region → zone travel was broken**: region `Zone` nodes had no `TargetMapId`, and
    `TravelToNodeHubCommand` only switches maps when a node names one. `MapGraphSeeder.LinkRegionZonesToZoneMaps`
    now sets it, guarded by a seeder test. Needs `--fresh` because the seeder skips an existing graph.
  - **Zone maps chain `entry → first location → second …`**, so the second location is two hops from arrival.
  - **Nodes never regenerate** (`NodeSpawnerService.RespawnNode` has no caller), and `session.zone` goes stale on
    a map travel that changes zone (banner shows the old zone with the new location). Both closed in the twelfth
    pass below.

Twelfth pass — **the three follow-ups the harvest loop left behind are closed.**

- **One home for the bag.** [`CharacterBag`](../../../Veldrath.Server/Features/Characters/CharacterBag.cs) owns
  `Read`/`Add`/`Serialize` over the `[{ "ItemRef", "Quantity" }]` JSON. `AttackEnemyHubCommand.RollLootAsync` now
  takes the `Character` entity, writes the blob and saves the row — `IInventoryService` is gone from the handler,
  and the test asserts the stored blob rather than a fake inventory. The harvest handler dropped its private
  `ReadBag`/`AddToBag` for the same helper.
- **Lazy node regrowth, no scheduler.** [`HarvestingOptions`](../../../Veldrath.Server/Settings/HarvestingOptions.cs)
  (`Harvesting:RegrowMinutes`, default 30, `[Range(1, 1440)]`, `ValidateOnStart`) is read by
  `GetLocationNodesHubCommandHandler`: a node that cannot be harvested and whose `LastHarvestedAt` is older than
  the cutoff is reset to full health with `TimesHarvested = 0` and saved on read. Guarded by
  `LocationNodesRegrowthTests` (comes back after the window, stays empty inside it).
- **A walk is an arrival.** `NodeTraveled` carries `ZoneId` and nothing else — no name, inn/merchant flags or
  occupants — so a cross-zone walk left the banner reading the old zone. `GameHub.TravelToNode` now calls
  `SyncArrivedZoneAsync`, which updates `Context.Items` (`CurrentZoneId`/`CurrentMapId`/`CurrentNodeId`) and sends
  the same `ZoneEntered` snapshot `EnterZone` sends; the client clears its zone when an arrival reports none
  (`handleNodeTraveled`). Three hub tests (`GameHubZoneTests`) cover crossing, staying and leaving for the region
  map; two session tests cover the client half.
- **Known gap this leaves:** a walk out of a zone changes the session row's zone but never sends `PlayerLeft` to
  the zone it left, so peers in the old zone can keep showing the walker until they refresh. Travel also never
  sets `Context.Items["CurrentZoneId"]` before this pass, which is now fixed for the arrival's zone.

Thirteenth pass — **chests exist, and they do not stand still.**

- **Where the row lives.** [`LootChest`](../../../Veldrath.Server/Data/Entities/LootChest.cs) is in
  `ApplicationDbContext` (not `GameDbContext` like `HarvestableNodes`) because nothing in the engine consumes it —
  only `LootTableService.RollChestDrops` — so it needs no `INodeRepository`-style abstraction, and it belongs
  beside `GroundItems`/`ZoneInstances`, which are the other per-location live rows. It has a nullable
  `ZoneInstanceId` with cascade delete, so a delve's chests die with the run. Migration:
  `Veldrath.Server/Migrations/*_AddLootChests.cs` (deploy needs it; tests use `EnsureCreated`).
- **Authored vs roaming, one table.** `Kind` discriminates them. `LootChestsSeeder.Authors()` seeds two authored
  chests in the Ashlen Wood (`old-clearing-strongbox`, `wood-road-cache`), idempotent by `ChestId` and preserving
  opened state. Roaming rows are inserted by `ChestService` from the zone's `ZoneLocation` rows: not hidden, not
  safe respawn, not instanced, `MinLevel >= Chests:MinSiteDangerLevel`, location not already occupied, zone is
  not a town. Site and loot table are both drawn by `RarityWeight`.
- **Swept, not timed.** `ChestService : BackgroundService` mirrors `EnemyRespawnService`. `ReopenAt` drives the
  reopen window (`Chests:ReopenAfterMinutes`, 180); a zone gains a roaming chest when its newest roaming
  `SpawnedAt`/`OpenedAt` is older than `Chests:SpawnEveryMinutes` (90) and it holds fewer than
  `Chests:MaxRoamingPerZone` (1). Opened roaming rows are tombstones on purpose: they are the clock. Every write
  is conditional (`ExecuteUpdateAsync`), so two sweeper instances cannot double-reopen a row.
- **Node regrowth moved onto the same sweep.** The lazy rule I shipped hours earlier is gone;
  `GetLocationNodesHubCommand` is a pure read again, and `ChestService.RegrowNodesAsync` resets depleted nodes
  whose `LastHarvestedAt` is older than `HarvestingOptions.RegrowMinutes`. `LocationNodesRegrowthTests` was
  replaced by `ChestSweeperTests` (reopen window, roaming site/town/cap/cadence filters, node regrowth).
- **First open wins.** `OpenChestHubCommandHandler` guards position, instance and `IsOpened`, rolls, then claims
  with `WHERE IsOpened = false`; a rows-affected of zero answers "someone else got there first" instead of paying
  twice. Items go into `Characters.InventoryBlob` through `CharacterBag`; `roll.Gold` is routed through
  `AddGoldHubCommand` so the attributes gold key keeps one writer.
- **Transport + web.** `GetLocationChests`/`OpenChest` on the hub, `LocationChests` on pull and on `EnterLocation`,
  `ChestOpened` to the current zone group (a shared chest disappears for the party, not just the opener). Web:
  `LocationChest`/`LocationChestsPayload`/`ChestOpenedPayload`, `session.chests`, a **Chests** section in the Local
  panel, pulls on `LocationEntered` and on `NodeTraveled`. `harvest.e2e.ts` now opens the strongbox and asserts the
  guaranteed potion lands in the bag.
- **Gaps worth remembering:** the sweeper does not broadcast (a chest appearing under a player's feet only shows
  on their next arrival — the difficulty-scoped group name lives per connection, not on the row); the seeded
  `forest-chest-loot` has no `gold` entry, so chests pay items only; the e2e stack exports
  `Chests__ReopenAfterMinutes=1` + `Chests__SweepIntervalSeconds=5` so an isolated re-run recovers the authored
  chests, and the spec walks on to the road's cache and then skips with that reason if both were emptied.

Fourteenth pass — **people exist, and the world cannot be fooled into handing them a knife.**

- **Placement is a row, not a pool entry.** [`LocationNpc`](../../../Veldrath.Server/Data/Entities/LocationNpc.cs) in
  `ApplicationDbContext` (`NpcId`, `ZoneId`, `LocationSlug`, `ArchetypeSlug`, optional `DisplayName`, `SortOrder`),
  seeded by [`LocationNpcsSeeder`](../../../Veldrath.Server/Data/Seeders/LocationNpcsSeeder.cs): blacksmith at
  `aurelian-market`, tavern keeper at `hearthbound-inn`, elder at `crestfall-square`. Migration
  `*_AddLocationNpcs.cs`. A pool entry means "may spawn here, weighted" and every pool entry becomes an attackable
  `SpawnedEnemy`, which is exactly why the seeder comment had kept NPCs out of pools — and why placement needed to
  be explicit.
- **The bug that made this urgent:** the main-story quest in `QuestsSeeder` has `talk:village-blacksmith` and no
  blacksmith existed anywhere, so the quest was unfinishable — the same shape as `gather-mushrooms` without nodes.
- **Read + talk.** `GetLocationNpcs` → `LocationNpcs` on location entry (pushed next to nodes and chests). The DTO
  carries the archetype slug, name, occupation and role flags read from `ContentDbContext.ActorArchetypes`, because
  the shared `NPC` projection has no roles. `TalkToNpc(slug)` verifies the placement is at the character's location,
  answers with a line from `IDialogueRepository.GetBySpeakerAsync(slug)` filtered to greetings (neutral fallback),
  sends `NpcTalked`, then advances `talk:<slug>` — the archetype slug is the speaker key and the objective key.
- **Hostile filter.** `EfCoreEnemyRepository.MapToModel` now projects `traits["hostile"] =
  e.Traits.Hostile ?? true`, and `ActorPoolResolver.SpawnRosterAsync` skips a non-hostile entry with a warning, so
  content that puts a shopkeeper in a pool cannot turn them into a target. `ResolveArchetypeAsync` is untouched, so
  rebuilding existing enemy rows still works.
- **Client-side gap this exposed:** location lists were only pulled on a *move*, so a resumed character saw no
  nodes, chests or people until they walked. `handleCharacterSelected` now pulls ground items, nodes, chests and
  people once after `EnterZone`. Session tests assert those four invocations.
- **Web:** `LocationNpc`/`LocationNpcsPayload`/`NpcTalkedPayload`, `session.npcs`, a **People** section with a Talk
  button (shows `· quests` / `· trades` from the flags), the line logged as `Name: “line”`. The `TalkToNpc` entry was
  removed from the hub-methods parity allow-list because it now has a real caller.
- **Tests:** `NpcPlacementTests` (seed guards, listing with roles, talk line, refusals, hostile/non-hostile roster),
  four reworked `TalkToNpc` hub tests (`NpcTalked` goes to `Clients.Caller`; the journal push goes to the connection
  id, so assertions must use the right proxy), two session tests, and the elder-then-wood walk in `harvest.e2e.ts`.
