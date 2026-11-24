---
kind: area
date: 2026-10-24
area: content
title: "Repository Config Consolidation (Session-64, 2026-10-24)"
---

## Repository Config Consolidation (Session-64, 2026-10-24)

**Removed nested config (single source of truth is now the repo root)**
- `web/.github/` (template `node.yml` + CI-CD-DASHBOARD.md) — superseded by `.github/workflows/ci-web.yml`.
- `web/.roo/skills/` — merged into root `.roo/skills/` as `web-development` and `web-ui`;
  catalog added at `.roo/skills/README.md`.
- `web/.gitignore` and `web/client/.gitignore` — patterns folded into the root `.gitignore`.
- `web/AGENTS.md` (stale Colyseus/template guidance) and `web/TEMPLATE-GUIDE.md` — content merged
  into the root `AGENTS.md` → **Web App (SvelteKit)** section.
- `web/scripts/ci-summary.mjs` — only used by the deleted template workflow.

**Workflow/config fixes**
- `.github/dependabot.yml` had an empty `package-ecosystem` (invalid); now configures nuget,
  npm (`/web`), github-actions, and docker with grouped weekly updates. *(File deleted — Forgejo
  runs no bot; the cadence now lives in [`docs/ci-forgejo.md`](../../../docs/ci-forgejo.md).)*
- `.github/workflows/sonarcloud.yml` was missing a checkout and used the deprecated
  `sonarcloud-github-action`; now checks out with `fetch-depth: 0` and uses
  `SonarSource/sonarqube-scan-action@v5`.
- `ci-web.yml` gained a `deploy` job calling `deploy.yml` with `service: web` (disabled via
  `if: false`, matching server/discord until the VPS exists).
- `docs.yml` API landing table no longer claims RealmEngine.Data uses JSON/LiteDB.

**Gotchas**
- Deleting `web/client/.gitignore` broke `includeIgnoreFile` in `web/client/eslint.config.js`;
  that config now declares generated-output ignores (`.svelte-kit`, `build`, `build-desktop`,
  coverage, Playwright artifacts) explicitly. Keep this in sync if more outputs appear.
- All web scripts run from `web/` (workspaces root); `npm run test` there runs 56 Vitest tests and
  22 Playwright tests, and the Playwright webServer performs the production `vite build`.
- `.NET` solution `Realm.Full.slnx` has 22 projects: 12 source + 10 test projects.

### Dependency consolidation (Session-64)

Dependabot PRs #9–#15 were consolidated into `main` instead of merged individually:

- **Applied:** the `actions` group (checkout v7, setup-node v7, setup-dotnet v6, upload-artifact v7,
  download-artifact v8, codecov v7, docker buildx v4 / build-push v7, pages v5, SonarQube scan v8),
  the `web-minor-patch` group, `vitest`/`@vitest/coverage-v8` **5.0.5**, `vite` **7.3.7** (security
  patch), and `@microsoft/signalr` **10.0.11**.
- **Deferred:** `@sveltejs/adapter-static` 4.0.0 (and `adapter-node` 6) require `@sveltejs/kit` **3**;
  Vite 8 requires `@sveltejs/vite-plugin-svelte` **7**. Both are Kit-3 migration work — do not bump
  them independently, and do not merge the `npm_and_yarn` PR that pairs Vite 8 with plugin-svelte 6.
- `svelte/no-navigation-without-resolve` is disabled in `web/client/eslint.config.js`: the app is
  served from the domain root (`paths.base` unset), so plain hrefs are correct. Re-enable and adopt
  `resolve()` from `$app/paths` before introducing a base path.
- After the bumps: `npm audit` reports 8 low/moderate advisories (SvelteKit 2.x URL/cache advisories,
  `runed`/`svelte-toolbelt`, and `@tailwindcss/typography` → `postcss-selector-parser`); all clear
  with a SvelteKit 3 / Tailwind typography refresh.

### SvelteKit 3 + Vite 8 migration (Session-64)

The web app now runs SvelteKit **3.1.0**, Vite **8.3.5**, `@sveltejs/vite-plugin-svelte` **7**,
`adapter-node` **6**, `adapter-static` **4**, and `#lib` subpath imports.

- `$lib` is removed in Kit 3. Imports now use `#lib/*`, backed by `web/client/package.json`
  `"imports": { "#lib/*": "./src/lib/*" }` plus `paths` in `web/client/tsconfig.json` for
  TypeScript (the package `imports` map alone is not picked up by TS here).
- `resolve()` is now typed against the generated `RouteId` union, which **includes route groups**
  (e.g. `/(mod)/mod`). Dynamic routes use `resolve('/play/[id]', { id })`.
- `$app/environment` is now `$app/env`; `Handle` moved to `@sveltejs/kit/hooks`.
- `web/client/tsconfig.json` extends **`$app/tsconfig`** (generated in
  `node_modules/$app/tsconfig.json`) with an explicit `include` (`src`, `tests`, config files) and
  the `#lib` paths mapping. Extending `./.svelte-kit/tsconfig.json` still works but Kit warns
  `tsconfig_extends_missing` on every dev start.
- Public env vars are declared in `web/client/src/env.ts` via `defineEnvVars` and imported by name
  from `$app/env/public` (`PUBLIC_API_URL`, `PUBLIC_HUB_URL`). `$env/dynamic/*` is legacy in Kit 3.
- **Public env vars are needed twice in containers:** inlined into the browser bundle at build time
  (`--build-arg`) *and* validated from `process.env` when `node build` starts. An image built with
  the build args but started without runtime env vars originally died with
  `SvelteKit error: env_invalid … PUBLIC_API_URL Value is missing`; fixed by re-declaring the ARGs in
  the runtime stage as `ENV` defaults in `web/client/Dockerfile` (build args alone now boot fine).
  Optional runtime overrides must match the build values or the browser bundle and SSR disagree.
- `+layout.server.ts` / `+page.server.ts` load functions now annotate `LayoutServerLoad` /
  `PageServerLoad` from `./$types`; raw `tsc` (not just svelte-check) needs those annotations.
- `$env/dynamic/private` is snapshotted by the Kit 3 + Vite 8 (Rolldown) build and gets
  constant-folded, so server secrets must come from `process.env`. `#lib/server/env.ts` reads
  `process.env` and loads `.env` (trimming quotes) for local dev/preview; all server modules import
  `env` from there instead of `$env/dynamic/private`.
- The root `hooks.server.ts` returns `resolve(event)` early while `building` so prerendering needs
  no auth secret.
- The `cookie` override is now `^1.1.1` (adapter-node 6 needs `parseCookie`); the `vite` override
  is `^8.3.5`.
- Web e2e (`vite preview`) relies on `#lib/server/env.ts` loading `.env`; do not parse `.env` in
  `playwright.config.ts` — quoted values like `ORIGIN="http://…"` break `new URL()`.
- Remaining blocked majors are ignored in `.github/dependabot.yml`: Avalonia 12,
  FluentAvaloniaUI 3, ReactiveUI 26 / Microsoft.Reactive.Testing 7 (RxVoid), MediatR 14
  (commercial licence), TypeScript 7 (native port; tooling not ready). *(File deleted — the holds
  now live in [`docs/ci-forgejo.md`](../../../docs/ci-forgejo.md); MediatR's current hold is below 13.)*

### Diagnosing `401` from the game API (Session-65)

`/characters` (and every other authenticated page) mints a game JWT in
`#lib/server/game-token.ts` (`HS256`, `iss`, `aud`, `sub` = Better Auth user id, which is a GUID
because `generateId: () => crypto.randomUUID()`), and Veldrath.Server validates it against
`Jwt:Key` / `Jwt:Issuer` / `Jwt:Audience` before `JwtClaimsEnrichment` JIT-provisions the Identity
account. A `401` therefore means the token was rejected — never "no session" (that redirects to
`/login`).

- Verified signatures (local repro, server on `:9099`, dev key from `.env`):
  - correct key → `200` in ~220 ms, body `[]`, log `JIT provisioned game account <guid> as 'probe'`;
  - wrong key → `401` in ~1 ms with
    `WWW-Authenticate: Bearer error="invalid_token", error_description="The signature key was not found"`
    and server log `JWT authentication failed: IDX10517: Signature validation failed…`.
  A sub-2 ms `401` in production is a **key/issuer/audience mismatch**, not an account problem.
- Instrumentation added: the server logs `JWT configuration: issuer=… audience=… keyFingerprint=…`
  at startup (SHA-256 prefix) plus an `OnAuthenticationFailed` warning; the web logs
  `[game-token] issuer=… audience=… keyFingerprint=…` on the first mint and
  `[game-request] <METHOD> <path> responded 401: <reason>` (reason parsed from
  `WWW-Authenticate` by `describeAuthFailure`). Compare the two fingerprints first.
- In Development the server key comes from the repo `.env` via `DotEnvConfiguration`
  (`JWT_KEY` → `Jwt__Key`), which overrides `appsettings.Development.json`; the appsettings dev
  key is ignored whenever `.env` defines `JWT_KEY`.
- `fetchCharactersForUser` now delegates to `gameApiFetch` (shared token + diagnostics) instead of
  its own fetch loop.
- `[access] could not load the account profile. An unexpected error occurred.` is the .NET 500
  ProblemDetails title (from `UseExceptionHandler` in `Program.cs`): the JWT validated, but the
  endpoint — or provisioning inside `OnTokenValidated` — threw. Root cause found: a fresh login makes
  SvelteKit fire four concurrent SSR calls (profile, characters, slots, last-session) with the same
  brand-new subject; each raced to insert the same Identity primary key and the losers rethrew
  `DbUpdateException` → 500. `AccountProvisioningService` now serializes provisioning per account
  (`ProvisionLocks` semaphore, cache re-check inside the lock) and polls up to ~200 ms for the
  winning row; `JwtClaimsEnrichment` catches provisioning exceptions, logs them, and rejects the
  token instead of surfacing a 500.
- SvelteKit gained `handleError` in `hooks.server.ts` (Kit 3 signature: `{ kind, error, event }`,
  returns void — do not destructure `status`/`message`) so server exceptions print a stack trace in
  the container log instead of vanishing into an "Internal Error" page.
- Coolify internal hostname is `veldrath-server` (set as the resource's Container Name), so
  `INTERNAL_BRIDGE_URL` must be `http://veldrath-server:8080` — `http://server:8080` is the
  docker-compose service name and does not resolve inside Coolify. Without it the account bridge is
  skipped (`[foundry-bridge] skipped`) and Identity rows only appear via JIT provisioning.
- **SignalR `Failed to complete negotiation … NetworkError`** is a browser-side CORS/transport
  problem, not a hub bug. The policy `AllowLocalClient` allows `Web__BaseUrl` (trimmed) plus
  localhost unless `Cors__AllowedOrigins` is set (indexed `__0` or one comma-separated value).
  `Program.cs` now logs `CORS allowed origins: …` at startup and **throws in Production** when the
  list contains only localhost — a silent misconfiguration otherwise. The other cause is an image
  built with the default `PUBLIC_HUB_URL=http://localhost:9000/hubs/game`, which an `https://` page
  blocks; `createGameHub` throws a descriptive error for that case.

**Security note:** a GitHub PAT was briefly committed inside `.roo/mcp.json` (`6648b6c5`) and the
file was deleted in `85bb8492`. Never commit MCP tokens — pass them via environment variables, and
treat any token that landed in git history as compromised (rotate it).

### Environment variable audit (Session-66)

Authoritative inventory — anything not listed is not read by code.

- **Veldrath.Server** (`Configuration[...]`): `ConnectionStrings:DefaultConnection`,
  `Jwt:Key` / `Jwt:Issuer` / `Jwt:Audience` / `Jwt:AccessTokenExpiryMinutes` /
  `Jwt:RefreshTokenExpiryDays`, `OAuth:{Discord,Google,Microsoft}:ClientId/ClientSecret`,
  `Email:{SmtpHost,SmtpPort,EnableTls,User,Password,SenderAddress,SenderName}`,
  `Web:BaseUrl`, `Foundry:BaseUrl`, `AllowedReturnUrlHosts`, `Auth:CookieDomain`,
  `Auth:PendingLinkExpiryMinutes`, `Seq:ServerUrl`, `DataProtection:KeysPath`,
  `Cors:AllowedOrigins`, plus the `Moderation` / `VersionCompatibility` /
  `ActiveCharacterTracker` / `CharacterCreation` sections. In Development
  `DotEnvConfiguration` maps `.env` names onto these (`JWT_KEY` → `Jwt__Key`, etc.).
- **SvelteKit web**: `DATABASE_URL` (read by `web/migrate.mjs` and by the
  Drizzle/Better Auth client), `ORIGIN`, `BETTER_AUTH_URL`,
  `BETTER_AUTH_SECRET`, `JWT_KEY` / `JWT_ISSUER` / `JWT_AUDIENCE`,
  `PUBLIC_API_URL` / `PUBLIC_HUB_URL` (inlined at build + validated at runtime),
  `INTERNAL_BRIDGE_URL` (falls back to `PUBLIC_API_URL`), `INTERNAL_BRIDGE_TOKEN`,
  `{DISCORD,GOOGLE,MICROSOFT}_CLIENT_ID` / `_SECRET`, `MICROSOFT_TENANT_ID`,
  and adapter-node's `PORT` / `HOST` / `PROTOCOL_HEADER` / `HOST_HEADER` /
  `ADDRESS_HEADER` / `XFF_DEPTH`.
- **Removed as dead (Session-66):** `Database__Provider` / `Database:Provider` (never
  read — every DbContext is `UseNpgsql`), `CERT_PASSWORD` (no consumer anywhere),
  `FOUNDRY_PUBLIC_SERVER_URL` (the `.env` alias is `FOUNDRY_PUBLIC_URL`), stale
  `Web__BaseUrl` defaults of `:8080` / `:8082` in the compose files (web is `:8081`),
  and the `http://localhost:8080` default CORS origin in `Program.cs` (now `:8081`).
- **Must be identical on server + web:** `JWT_KEY` ↔ `Jwt__Key`,
  `JWT_ISSUER`/`JWT_AUDIENCE` ↔ `Jwt__Issuer`/`Jwt__Audience`,
  `INTERNAL_BRIDGE_TOKEN` ↔ `InternalBridge__Token`, and the OAuth client
  id/secret pairs. Web-only: `MICROSOFT_TENANT_ID`, `BETTER_AUTH_SECRET`,
  `INTERNAL_BRIDGE_URL`, proxy headers. Server-only: `Auth__CookieDomain`,
  `Email__*`, `Seq__ServerUrl`, `Web__BaseUrl`, `AllowedReturnUrlHosts`.
- `docs/coolify-deployment.md` §0 carries the same matrix for humans.

### Standing rule — tunables are configuration, not constants (2026-11-13)

Written down in [`plans/dungeons.md` §9](../../../plans/dungeons.md) and
[`docs/coolify-deployment.md`](../../../docs/coolify-deployment.md) (server env reference). Short version:

- A number a designer can argue about is either **content** (a row in the content DB, edited in Foundry)
  or **configuration** (a host option). Never a `const` in a handler, never a value that needs a rebuild
  to change.
- `EnemyWorldOptions` (`Settings/`, section `Enemies`) is the current host surface: `RespawnDelaySeconds`
  60, `SweepIntervalSeconds` 5, `RespawnBatchSize` 50, `SessionGraceSeconds` 60,
  `ClearedRoomRespawnMultiplier` 3. `[Range]` + `ValidateDataAnnotations().ValidateOnStart()` at
  [`Program.cs:174`](../../../Veldrath.Server/Program.cs). No `Enemies` section exists in
  `appsettings.json`, so the class defaults are what actually runs — a bad env var stops the host rather
  than being overridden.
- **`GameConfig` (`RealmEngine.Data/Entities/Config/GameConfig.cs`) is the intended runtime home** —
  `ConfigKey` + JSONB `Data` + `Version` + `UpdatedAt`, one row per config file, already seeded by
  migration — but **nothing reads it at runtime today**; a grep only finds migrations and snapshots.
- When that pass happens: options class keeps its shape and validation (it becomes the typed view of the
  row), the DB row overrides appsettings and appsettings stays the fallback so an empty DB still boots,
  one `ITunableSource` reads it (never per-attack), and Foundry is the editing surface, not a DB console.
- Known placeholders to fix in that pass: the cleared-room rule is one global multiplier (should be
  per-zone or per-difficulty) and instance expiry reuses `SessionGraceSeconds` (should be its own key).
  Level gates (`Zone.MinLevel`, `ZoneLocation.Stats.MinLevel`) stay content and must never become
  appsettings.

### Milestone G — the Foundry map editor (2026-11-11/12, PR #71)

Plan: [`plans/foundry-map-editor.md`](../../../plans/foundry-map-editor.md). Durable description:
[`docs/client-ui-architecture.md` §5](../../../docs/client-ui-architecture.md). Branch
`feat/milestone-g-foundry-map-editor`; commits `7bd1a220` … `6e8565c6`.

**What it is.** `/maps` lists graphs with a state badge and a create form; `/maps/[mapId]` is a canvas,
a metadata panel, a node inspector and an edge list. The route group strips from the URL, so links are
`/maps`, `/content`, `/submissions` — **never** `/foundry/...` (three of those were 404s until G0).

**The rules live in TypeScript, not markup** (`web/client/src/lib/foundry/`):
`map-canvas.ts` (positions, unplaced lane, collision, tidy), `map-edges.ts` (direction inference,
self-reference/duplicate-direction refusals, reciprocal edges), `map-nodes.ts` (suggested ids, defaults,
delete refusals), `map-editor.ts` (`issuesByNode`, `mapState`, `parseDraftGraph`, `normalizeDraft`).
Components render and dispatch.

**One draft, one writer.** The page holds a `MapGraphDto` in state; the hidden `graph` field posts it
whole. The old write-through `save` action, `applyNodeEdits`, `readNodeEdit` and `NodeEdit` are **deleted** —
two writers silently discard canvas gestures.

**Lifecycle (the gate).** `IsActive` + `ApprovedVersion` on `Map` (content DB; migrations
`AddMapReviewState`, `AddMapReviewOutcome`). `CreateDraftAsync` → inactive, one entry node, no fabricated
exit; `SubmitAsync` → refuses a graph with errors; `ReviewAsync` (`Curator` policy) → approval needs a
submission **and** a clean validate, sets `IsActive` + `ApprovedVersion`; rejection clears the submission
and keeps `LastReviewNotes`. `DeactivateAsync`/`DeleteDraftAsync` refuse while another map's node still
targets the map; a live map is retired, never deleted. **`Apply` never writes `IsActive`** — a crafted save
payload must not activate a draft. Every lifecycle change writes an `AdminAuditEntry`.

**Access matrix.** GET `/api/content/maps*` → `content.view` (the graph leaks hidden nodes and unlock
keys); PUT/POST authoring → `content.manage`; review/deactivate → the `Curator` **role** (a permission is
not enough). The endpoints used to require `ManageContent` group-wide, which made read-only unreachable.

**Notifications.** `FoundryNotification` gained `Kind` (`Submission`/`MapReview`), an optional
`SubmissionId`, a `MapId` (plain string — maps are in the content DB, so no cross-context FK) and a
`Title` snapshot. `FoundryNotificationWriter` is the only place a row is built. Storing the title removed a
**required** join that was silently hiding rows. Migration `AllowMapReviewNotifications`.

**Validator.** `MapGraphValidator.ValidateWorld` gained `target_node_unknown` (an exit must name a node the
target map has). The entry rules (`entry_missing`, `entry_not_found`, `entry_flag_mismatch`) already
existed — do not "add" them again.

**Gotchas worth remembering:**
- A local variable named `state` in a `.svelte` that uses `$state` breaks rune lexing with ~24 confusing
  errors ("`$state` used before its declaration"). The `MapHeader` prop is therefore `status`.
- `WebAppFactory` content DB starts empty: seed with `MapGraphSeeder`/`ZoneLocationsSeeder` before an HTTP
  test that needs a graph (`MapEditorEndpointTests.EnsureAMapAsync`).
- `MapEditorService` only has `ContentDbContext`; audit and notifications need `ApplicationDbContext`, so
  the **endpoint** writes them. `ReviewAsync` returns `MapReviewOutcome` (response + author + map name)
  for exactly that reason.
- Client DTOs keep the review fields **optional** (`isActive?`, `approvedVersion?`) because fixtures
  predate the gate; `mapState` treats `undefined` as live.
- Coverage: `vite.config.ts` now gates `src/lib/foundry/**/*.ts` (rules) at 92 and leaves the `.svelte`
  components to the 85/80 `.svelte` floor — the same reasoning already applied to `src/lib/game/**/*.ts`.
  Global branches sit at exactly 90 after G; adding component branches will push it under again.
- The Foundry authoring e2e (`tests/foundry-maps.e2e.ts`) is opt-in (`E2E_FOUNDRY=1` + staff credentials):
  a test cannot grant itself `content.manage` or `Curator`.

**Counts at the end of G:** 6,058 .NET tests, 1,303 web unit tests, coverage green globally (90 branches).
