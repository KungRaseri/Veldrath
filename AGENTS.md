# RealmEngine — Veldrath Project Instructions

> Primary agent instructions file for Zoo Code / Roo Code / Deepseek agents working on the RealmEngine monorepo.

---

## Session Start — Required Reading

**At the beginning of every session, read these files before doing anything else.**

### Memory Files (Canonical Project Context)

| # | File | Description |
|---|------|-------------|
| 1 | [`.github/agent-memory/engine-codebase.md`](.github/agent-memory/engine-codebase.md) | Key model facts, positional record constructors, known handler quirks, testing gotchas, known open P3/P4 items |
| 2 | [`.github/agent-memory/json-migration-status.md`](.github/agent-memory/json-migration-status.md) | JSON → DB migration history and completed work log |
| 3 |  [`.github/agent-memory/foundry-content-schema.md`](.github/agent-memory/foundry-content-schema.md) | Foundry content schema, write endpoints, integration test gotchas |
| 4 | [`.github/agent-memory/veldrath-memory.md`](.github/agent-memory/veldrath-memory.md) | Veldrath Server + web client hub architecture, blob schema, auth/env matrix, generated index of work-item notes |
| 5 | [`.github/agent-memory/gap-analysis-process.md`](.github/agent-memory/gap-analysis-process.md) | Process template for gap analysis at session start |
| 6 | [`.github/agent-memory/auth-and-character-creation-plan.md`](.github/agent-memory/auth-and-character-creation-plan.md) | Auth flow & character creation gap fix plan, implementation decisions |
| 7 | [`.github/agent-memory/combat-loop-plan.md`](.github/agent-memory/combat-loop-plan.md) | Combat loop + multiplayer architecture plan (actual turn-based implementation supersedes original tick-based design) |
| 8 | [`.github/agent-memory/world-lore-plan.md`](.github/agent-memory/world-lore-plan.md) | World lore plan, Calethic language reference, open items |

**Important:** Do **not** use `/memories/repo/` or any external memory store. Write new discoveries directly to the files above using file editing tools so they travel with the repo via git.

### Instruction Files (Consult When Working on Specific Areas)

| File | When to Consult |
|------|-----------------|
| [`.github/instructions/xml-documentation.md`](.github/instructions/xml-documentation.md) | When adding ANY public type/member |
| [`.github/instructions/ef-core-patterns.md`](.github/instructions/ef-core-patterns.md) | When working with databases, migrations, or DI |
| [`.github/instructions/hub-development.md`](.github/instructions/hub-development.md) | When adding or modifying SignalR hub methods |
| [`.github/instructions/testing.md`](.github/instructions/testing.md) | When writing tests of any kind |
| [`.github/instructions/engine-features.md`](.github/instructions/engine-features.md) | When creating new MediatR commands/queries |
| [`.github/instructions/build-strategy.md`](.github/instructions/build-strategy.md) | When deciding which solution to build after making changes |

---

## Project Overview

**RealmEngine** is a **framework-agnostic .NET 10 RPG game logic library**. It exposes all game operations as MediatR commands and queries — any .NET application (Godot via GDNative, Unity, ASP.NET Core, console, etc.) can consume it by calling `mediator.Send(command)`.

This repository also contains the **official game built on top of that engine**: **Veldrath**, a multiplayer RPG with an ASP.NET Core server and a SvelteKit web app. **Veldrath is web-first: the SvelteKit app is the only client and the only editing surface** (the Avalonia desktop client and the RealmForge desktop editor were retired). Additional projects include:

- **SvelteKit web app** (`web/`) — the single client and web frontend: public site, auth, game, **Foundry** (the content editor), editorial, admin, and moderation
- **Veldrath.Discord** — Discord bot integration
- **Veldrath.Contracts** — Shared API contracts and DTOs

---

## Project Structure

### Solution Files

| Solution | Contains | Use For |
|----------|----------|---------|
| [`RealmEngine.slnx`](RealmEngine.slnx) | Core + Data + Shared + tests | Engine-only development, CI |
| [`Veldrath.Server.slnx`](Veldrath.Server.slnx) | Server + engine + assets + contracts + tests | Game server development |
| [`Veldrath.Discord.slnx`](Veldrath.Discord.slnx) | Discord + engine + contracts + tests | Discord bot development |
| [`Veldrath.Libraries.slnx`](Veldrath.Libraries.slnx) | Assets + Contracts + engine + tests | Shared library development |
| [`Realm.Full.slnx`](Realm.Full.slnx) | Everything (.NET) | Full .NET local development |

The SvelteKit app in [`web/`](web/) is verified with npm scripts rather than a `.slnx`.

### Directory Layout

```
.
├── RealmEngine.Core/             # Game logic, MediatR handlers (zero UI dependencies)
├── RealmEngine.Shared/           # Models, interfaces, abstractions (zero UI dependencies)
├── RealmEngine.Data/             # EF Core persistence, repositories (zero UI dependencies)
├── Veldrath.Server/              # ASP.NET Core game server with SignalR hub
├── Veldrath.Discord/             # Discord bot
├── Veldrath.Contracts/           # Shared API contracts and DTOs
├── web/                          # SvelteKit 3 / Svelte 5 app — the only client, plus Foundry (shared + client workspaces)
├── [Project].Tests/              # One test project per .NET library/application (5 total)
├── .github/
│   ├── agent-memory/             # Canonical memory store: 8 reference files + work-items/ (one per card) + areas/, see Required Reading
│   ├── instructions/             # Detailed instruction files (7 files)
│   └── workflows/                # CI/CD workflows (per-component + docs + hygiene + release)
├── .roo/skills/                  # Agent skills (build, test, run server, web); catalog in .roo/skills/README.md
├── docs/                         # Documentation (published by DocFX as pages of docs.veldrath.com)
├── wiki/                         # GitHub Wiki content (git submodule)
├── plans/                        # Live plans only; finished designs are folded into docs/
├── templates/veldrath/           # DocFX template override: site theme, fonts, brand assets
├── versions/                     # Component version props files
├── config/                       # Grafana dashboards, Prometheus config, etc.
├── scripts/                      # Build, asset, and release scripts
└── SECURITY.md                   # Security policy
```

### Component Versions

Version metadata lives in [`versions/`](versions/):

| Props File | Component |
|------------|-----------|
| [`versions/engine.props`](versions/engine.props) | RealmEngine.Core, Shared, Data |
| [`versions/server.props`](versions/server.props) | Veldrath.Server |
| [`versions/discord.props`](versions/discord.props) | Veldrath.Discord |

---

## Architecture Overview

### CQRS with MediatR + Vertical Slice Feature Organization

All game operations are MediatR `IRequest<TResponse>` records (commands change state, queries read state). Features live in [`RealmEngine.Core/Features/{FeatureName}/`](RealmEngine.Core/Features/) with handler, command/query record, and response DTO together in the same file. Pipeline behaviors automatically apply FluentValidation and Serilog logging.

> **See [`.github/instructions/engine-features.md`](.github/instructions/engine-features.md) for the full feature development conventions.**

### Hub → MediatR Bridge Pattern

SignalR hubs in [`Veldrath.Server`](Veldrath.Server/) are thin adapters: `SignalR Hub → MediatR.Send(command) → Handler → Response → Hub returns result to client`. This keeps the engine UI-agnostic.

> **See [`.github/instructions/hub-development.md`](.github/instructions/hub-development.md) for the full hub development conventions, including the single-DTO parameter rule and DI registration patterns.**

### Engine Agnosticism

The engine is UI-agnostic by design: any .NET consumer calls `await mediator.Send(new SomeGameCommand { ... })` and receives a typed DTO with the outcome.

### Client Architecture

The game client is the SvelteKit app. [`web/client/src/lib/game/`](web/client/src/lib/game/) holds the game shell, the `GameSession` runes store, the `CardinalGraphLayout` port, and the hub payload types. [`payload-contract.test.ts`](web/client/src/lib/game/payload-contract.test.ts) parses the payloads `Veldrath.Server/Hubs/GameHub.cs` broadcasts and fails when `types.ts` drifts.

### Web App (SvelteKit)

[`web/`](web/) is the single web frontend (public site, auth, game client, Foundry, editorial, admin, moderation). It is its own npm-workspaces monorepo (`shared`, `client`) with its own tooling — run all web commands **from `web/`**, never from `web/client/`.

- **Workspaces:** [`web/shared`](web/shared) (`@veldrath/shared` — Better Auth Drizzle schema and generated contracts) and [`web/client`](web/client) (`@veldrath/web` — SvelteKit app).
- **Stack:** SvelteKit 3, Svelte 5 runes, Vite 8, Tailwind CSS 4, Skeleton v5, Bits UI, Lucide, Better Auth, Drizzle ORM, adapter-node.
- **Auth:** **Better Auth is the single source of truth** for credentials and sessions — [`web/client/src/lib/server/auth.ts`](web/client/src/lib/server/auth.ts) (lazy `getAuth()`) and [`web/client/src/hooks.server.ts`](web/client/src/hooks.server.ts). SvelteKit mints short-lived game JWTs; `Veldrath.Server` validates them, provisions the account just in time, and adds RBAC claims. Nothing signs in through ASP.NET Core Identity passwords any more.
- **Email:** Better Auth relays verification/reset messages to the .NET server's SMTP transport through the internal bridge ([`email.ts`](web/client/src/lib/server/email.ts)), so there is one mail configuration.
- **Database:** schema in [`web/shared/src/db/schema.ts`](web/shared/src/db/schema.ts); migrations under `web/shared/db/migrations/`.
- **Theme/assets:** Veldrath Design System tokens in [`web/client/src/routes/theme.css`](web/client/src/routes/theme.css) and [`web/client/src/routes/brand.css`](web/client/src/routes/brand.css); self-hosted fonts in `web/client/static/fonts/`, imagery in `web/client/static/images/`.
- **Reference corpus:** third-party `llms.txt` docs live in [`web/llms/`](web/llms/).
- **Agent skills:** [`web-development`](.roo/skills/web-development/SKILL.md) (routes, auth, Drizzle, verification) and [`web-ui`](.roo/skills/web-ui/SKILL.md) (tokens, Skeleton, icons, fonts).

---

## Hard Rules

### Rule 1: XML Documentation (CS1591) — Compile-Time Error

Every publicly visible type and member must have an XML doc comment (`<summary>` at minimum). CS1591 is a compile-time error enforced in [`Directory.Build.targets`](Directory.Build.targets). Never suppress CS1591.

> **See [`.github/instructions/xml-documentation.md`](.github/instructions/xml-documentation.md) for full rules, doc comment templates for every member type, and prohibited patterns.**

### Rule 2: Engine Agnosticism

Engine libraries ([`RealmEngine.Core`](RealmEngine.Core/), [`RealmEngine.Shared`](RealmEngine.Shared/), [`RealmEngine.Data`](RealmEngine.Data/)) must **never** take a dependency on any UI framework (Avalonia, ASP.NET Core MVC/Razor, Blazor, etc.).

### Rule 3: No Mocking Frameworks by Default

Prefer `FakeXxx` stub classes in `Infrastructure/` directories over mocking frameworks. [`Moq`](https://github.com/moq/moq4) and [`NSubstitute`](https://nsubstitute.github.io/) are available only when stubs aren't practical.

---

## Key Technologies & Versions

All package versions are centrally managed in [`Directory.Packages.props`](Directory.Packages.props) — that file is **authoritative** for exact versions.

| Technology | Version | Purpose |
|---|---|---|
| .NET / C# | 10.0 | All projects |
| MediatR | 12.5.0 | CQRS command/query dispatch |
| FluentValidation | 12.1.1 | Input validation pipeline behavior |
| Serilog | 4.4.0 | Structured logging pipeline behavior |
| SvelteKit / Svelte | 3.x / 5 | Web app framework with runes (`web/`, Vite 8) |
| Tailwind CSS + Skeleton | 4.x / 5 | Styling and UI utilities (`web/`) |
| ASP.NET Core + SignalR | .NET 10 | Veldrath.Server API and hub |
| Entity Framework Core | .NET 10 | ORM (Postgres production, SQLite/InMemory for tests) |
| xUnit | 2.9.3 | Testing framework |
| FluentAssertions | 8.10.0 | Assertion library |

---

## Build & Test Commands

### Smart Build (Recommended During Development)

Build only what changed. The rule is simple: **build the `.slnx` file matching the directory you changed.**

> **Full decision logic: [`.github/instructions/build-strategy.md`](.github/instructions/build-strategy.md)**

| Changed Path | Build Command |
|---|---|
| `RealmEngine.*/**` | `dotnet build RealmEngine.slnx` |
| `Veldrath.Server/**` | `dotnet build Veldrath.Server.slnx` |
| `Veldrath.Discord/**` | `dotnet build Veldrath.Discord.slnx` |
| `Veldrath.Contracts/**` | `dotnet build Veldrath.Libraries.slnx` |
| `web/**` | `cd web && npm run typecheck && npm run lint && npm run test && npm run build` |
| Cross-component / `Directory.Build.*` | `dotnet build Realm.Full.slnx` |

> **When NOT to build:** The Vite dev server hot-reloads Svelte and CSS changes. Skip full rebuilds for `.css`, `.js`, `.md`, and non-embedded `.json` changes.

### Web parity and coverage gates

- [`web/client/src/lib/parity/`](web/client/src/lib/parity/) holds five source-reading suites (hub methods, hub events, REST routes, content types, route breadth). They fail when a server capability has no web consumer or a page route has no Playwright reference — add the entry to the suite's allow-list **with a reason**, never delete the assertion.
- `cd web && npm run coverage --workspace client` enforces **90% global** with per-glob floors (92 for `src/lib/server`, `src/lib/game`, `src/lib/foundry`; 90 for `src/routes/**/*.ts`; 85/80 for `.svelte`). The gate is expected to be red until the coverage pass lands — do not lower it to make CI green.
- Unit-test helpers live in [`web/client/src/lib/testing/`](web/client/src/lib/testing/) (imported as `#lib/testing/*`) and are excluded from coverage; Playwright helpers belong in `web/client/tests/`.
- `web/shared` is intentionally excluded from coverage: it is declaration-only.
- **E2E reporting and the spec manifest.** [`web/client/tests/e2e-manifest.json`](web/client/tests/e2e-manifest.json) is the only list of which spec runs in which run; `web/client/package.json` and [`ci-web.yml`](.github/workflows/ci-web.yml) both go through [`tests/run-e2e.mjs`](web/client/tests/run-e2e.mjs). **A new `*.e2e.ts` file must be added to one run there** — a spec no run claims fails `scripts/check-e2e-manifest.mjs`, and that check is why `tests/encounters.e2e.ts` had silently stopped running in CI. Playwright writes HTML/JSON/JUnit per run, [`scripts/e2e-summary.mjs`](scripts/e2e-summary.mjs) renders `E2EReport/Summary.md`, and both web jobs post it to the PR's own `<!-- ci-e2e -->` comment (separate from coverage). The gate enforces integrity only; pass/fail stays the job's exit code and flaky is reported, never gated. Tooling tests: `node --test scripts/tests/e2e-reporting.test.mjs`. Plan: [`plans/e2e-reporting.md`](plans/e2e-reporting.md).
- Plan, baseline numbers, and deferred gaps: [`plans/web-feature-parity-and-coverage.md`](plans/web-feature-parity-and-coverage.md).

### Full Build (Final Verification)

```powershell
dotnet build Realm.Full.slnx    # Full build (all .NET projects)
dotnet test Realm.Full.slnx     # All tests across the 5 .NET test projects (with coverage)
dotnet format Realm.Full.slnx   # Apply the .editorconfig formatting rules
```

> **Formatting is enforced.** Each .NET component workflow has a `Verify formatting` step running
> `dotnet format <slnx> --verify-no-changes` ([`ci-engine.yml`](.github/workflows/ci-engine.yml),
> [`ci-server.yml`](.github/workflows/ci-server.yml), [`ci-discord.yml`](.github/workflows/ci-discord.yml)).
> Run the format command above (or enable format-on-save) before committing, otherwise the gate fails.

### Running Projects

```powershell
dotnet run --project Veldrath.Server
cd web && npm run dev            # SvelteKit web app → http://localhost:5173
```

### Workflow e2e — isolated stack, never the dev ports

The journey specs need a real server and database, so they get their own compose project instead of
borrowing the development ones. **Postgres 55432, API 9100, preview 4273**; the script refuses a port
it did not take and never kills a server it did not start.

```powershell
./scripts/e2e-stack.sh start --fresh            # empty volume → a green run means "works against the seed"
cd web && npm run test:e2e:stack --workspace client
./scripts/e2e-stack.sh stop
```

`npm run test:e2e:stack` runs the parallel specs and then `upstream-failure.e2e.ts` alone on one worker:
that spec stops the API, so it can never share a run. Playwright starts the preview with `ORIGIN`,
`BETTER_AUTH_URL`, `PUBLIC_API_URL`, `PUBLIC_HUB_URL`, `DATABASE_URL` (the auth role) and
`AUTH_RATE_LIMIT_DISABLED` — do not export those by hand; the `PUBLIC_*` pair is inlined at build time and
a hand-set value is ignored.

---

## CI / Releases (Forgejo)

CI runs on **Forgejo Actions** at `https://git.kungraseri.cloud/Veldrath/Veldrath`. There is no Codecov, SonarCloud, GitHub Pages or GHCR dependency: coverage and docs are workflow artifacts, and images and releases are published through the Forgejo API. **Deployment is not done by Actions** — Coolify pulls the published images.

> **Read [`docs/ci-forgejo.md`](docs/ci-forgejo.md) before adding an action.** The runner only resolves actions mirrored on `data.forgejo.org`; anything else makes the job die in under a second with no useful log. That file carries the verified allow-list and the replacement for every action that is not available.

| Workflow | Component | Publishes |
|---|---|---|
| [`ci-engine.yml`](.github/workflows/ci-engine.yml) | Core + Shared + Data | `engine/v*` release + libraries zip |
| [`ci-coverage.yml`](.github/workflows/ci-coverage.yml) | .NET coverage floors across all five test projects | nothing (status only) |
| [`ci-server.yml`](.github/workflows/ci-server.yml) | Veldrath.Server | `server/v*` release + zip + `veldrath-server` image |
| [`ci-discord.yml`](.github/workflows/ci-discord.yml) | Veldrath.Discord | `discord/v*` release + zip + `veldrath-discord` image |
| [`ci-web.yml`](.github/workflows/ci-web.yml) | SvelteKit web | `veldrath-web` image + coverage/Playwright artifacts |
| [`docs.yml`](.github/workflows/docs.yml) | DocFX build + markdown relative-link check | `docs-site` artifact; Coolify deploy from `main` |
| [`merge-gate.yml`](.github/workflows/merge-gate.yml) | PR gate: workflow lint, runs on every pull request | nothing (status only) |

Releases are created inside the package job by [`scripts/forgejo-release.sh`](scripts/forgejo-release.sh); there is no separate release workflow, because fetching an artifact cross-run is a GitHub-API dependency. Images go to `git.kungraseri.cloud/veldrath/*`, which is what [`docker-compose.coolify.yml`](docker-compose.coolify.yml) pulls.

That is the whole list, deliberately. Four checks live inside those workflows instead of having a file each:

| Check | Lives in | Was |
|---|---|---|
| `dotnet format --verify-no-changes` | the `Verify formatting` step of each .NET component workflow | `format.yml` (deleted) |
| Markdown relative-link check | a step in `docs.yml` | `repo-hygiene.yml` (deleted) |
| Content validation over the seeded catalog | `SeededContentValidationTests`, already run by `ci-server.yml` | a planned `content-guard.yml` (never kept) |
| Calethic name-collision check | a step in `docs.yml` ([`scripts/check-content-names.mjs`](scripts/check-content-names.mjs)) | never existed — D5 found `Orneld`/`Orveld` and `Gaelraeth`/`Kael-Raeth` by hand |

Coverage exclusions are configured in [`coverage.runsettings`](coverage.runsettings). Each .NET job uploads a `CoverageReport/` artifact and posts the summary as a sticky PR comment via [`scripts/forgejo-pr-comment.sh`](scripts/forgejo-pr-comment.sh).

---

## Documentation

- **DocFX reference docs:** generated from XML doc comments across all projects by `docs.yml`;
  test projects are excluded from the reference on purpose. Published at
  <https://docs.veldrath.com> (Coolify builds [`docs/Dockerfile`](docs/Dockerfile) from `main`;
  the workflow also uploads a `docs-site` artifact).
- **Site theme:** [`templates/veldrath/`](templates/veldrath/) — a docfx custom template that maps
  the client design system onto the modern template. `public/veldrath.css` imports the client's
  `theme.css` (copied verbatim by `docfx.json`'s `resource` section) and the client's self-hosted
  fonts, so palette and type cannot drift from the game. `layout/_master.tmpl` is a copy of docfx
  2.81.0's modern master with exactly two changes: an extra `<link>` and a dark default theme —
  bump the pinned `DOCFX_VERSION` in `docs.yml` and `docs/Dockerfile` together, and re-sync it.
- **The published site is an allowlist, not `docs/`.** `docfx.json` publishes
  [`docs/client-overview.md`](docs/client-overview.md), [`docs/lore/`](docs/lore/), the design set
  ([`docs/design-system.md`](docs/design-system.md),
  [`docs/client-ui-architecture.md`](docs/client-ui-architecture.md),
  [`docs/design/`](docs/design/)) and [`docs/design/tokens.json`](docs/design/tokens.json) as a
  downloadable resource. The design set is published on purpose: the creative team works outside the
  repository, and a brand specification nobody can read is a specification nobody follows. Everything
  else in `docs/` (`deployment.md`, `coolify-deployment.md`, `ci-forgejo.md`) stays repo-only —
  operational runbooks and migration history are read in the repo and do not belong on a site read by
  players and API consumers.
- **Adding a published page:** write it, add it to [`docs/toc.yml`](docs/toc.yml) (sidebar) and, if
  it deserves a top-level tab, [`toc.yml`](toc.yml), then add its path to the `content` allowlist in
  `docfx.json` — a file outside the allowlist builds in the repo but never appears on the site.
  Links to anything outside the published set must be absolute
  `https://git.kungraseri.cloud/...` URLs, or they 404.
- **Wiki** ([`wiki/`](wiki/) submodule): Getting Started, Contributing, FAQ, Roadmap Summary
- **Key docs:**
  - [`docs/ci-forgejo.md`](docs/ci-forgejo.md) — Forgejo Actions allow-list, runner setup, registry and releases
  - [`docs/deployment.md`](docs/deployment.md) — deployment overview
  - [`docs/coolify-deployment.md`](docs/coolify-deployment.md) — the authoritative Coolify/env reference
  - [`docs/design-system.md`](docs/design-system.md) — tokens, typography, component primitives, decision log
  - [`docs/client-ui-architecture.md`](docs/client-ui-architecture.md) — the shell, every surface, the dialog set, the DB map graph
  - [`docs/design/brand-guide.md`](docs/design/brand-guide.md) — the mark, lockups, clear space, art direction
  - [`docs/design/asset-specs.md`](docs/design/asset-specs.md) — canvas sizes, formats, weight budgets, naming
  - [`docs/design/asset-inventory.md`](docs/design/asset-inventory.md) — every image file, its consumer and its verdict
  - [`docs/lore/calethic-language.md`](docs/lore/calethic-language.md) — Calethic language reference
- **Design docs are generated-adjacent.** [`docs/design/tokens.json`](docs/design/tokens.json) mirrors
  `theme.css` `:root`, and `web/client/src/lib/parity/design-tokens.test.ts` fails when the CSS, the
  export and `docs/design-system.md` disagree. Add a token in all three places.

---

## Memory & Reference Architecture

### Canonical Memory Store

The directory [`.github/agent-memory/`](.github/agent-memory/) is the **canonical memory store** for this project. Memory files are:

- **Committed to the repo** and shared across all machines via git
- **Read at session start** (see Required Reading above)
- **Written to directly** using file editing tools when new discoveries are made

### Memory File Reference

| File | Purpose |
|------|---------|
| [`.github/agent-memory/engine-codebase.md`](.github/agent-memory/engine-codebase.md) | Codebase notes: model facts, constructor patterns, handler quirks, testing gotchas, open items |
| [`.github/agent-memory/json-migration-status.md`](.github/agent-memory/json-migration-status.md) | JSON → DB migration log, completed work, deleted files |
| [`.github/agent-memory/foundry-content-schema.md`](.github/agent-memory/foundry-content-schema.md) | Foundry content schema, write endpoints, integration test gotchas |
| [`.github/agent-memory/veldrath-memory.md`](.github/agent-memory/veldrath-memory.md) | Server + web client hub architecture, blob schema, auth/env matrix, and where the notes live; the page itself is generated from a template |
| [`.github/agent-memory/gap-analysis-process.md`](.github/agent-memory/gap-analysis-process.md) | Process template for gap analysis at session start (prompt templates) |
| [`.github/agent-memory/auth-and-character-creation-plan.md`](.github/agent-memory/auth-and-character-creation-plan.md) | Auth flow & character creation gap fix plan, decisions made, next steps |
| [`.github/agent-memory/combat-loop-plan.md`](.github/agent-memory/combat-loop-plan.md) | Combat loop architecture plan (actual turn-based implementation supersedes original design) |
| [`.github/agent-memory/world-lore-plan.md`](.github/agent-memory/world-lore-plan.md) | World lore plan, Calethic language reference, open items |
| [`.github/agent-memory/work-items/`](.github/agent-memory/work-items/) | One file per work item — the note a card or session leaves behind; these folders are the index, and `node scripts/memory/memory-index.mjs --list` prints them |
| [`.github/agent-memory/areas/`](.github/agent-memory/areas/) | A topic that outgrew a session log: long-lived reference, edited in place like the files above |

### Writing to Memory Files

When you discover something new about the codebase that future sessions should know:

1. **Read the relevant memory file** first to understand current state
2. **Edit it directly** with the new information
3. **Commit the change** so it travels with the repo

**A note that belongs to one work item goes in its own file.** Create
`.github/agent-memory/work-items/<yyyy-mm-dd>-<card>-<slug>.md` and write only that file; front matter is
`kind: work-item`, `date`, `area`, `card`, `pr`, `title`. `area` is one of `content`, `engine`, `web`,
`map`, `auth` or `infra`; `card` (`t_xxxxxxxx`) and `pr` are optional; the date in the file name and in
`date:` agree, or both say `undated`.

**Never edit another work item's note.** Record a correction in the correcting card's own note,
link to the original, and update the relevant stable reference if the correction changes a long-lived fact.

Never hand-edit [`.github/agent-memory/veldrath-memory.md`](.github/agent-memory/veldrath-memory.md) —
`node scripts/memory/memory-index.mjs` generates it from `scripts/memory/veldrath-memory.template.md`,
and CI checks the result. Adding a note touches **one new path and nothing else**: nothing is regenerated,
so CI checks the note's front matter instead (`node scripts/memory/memory-index.mjs --check`, which names
the file and the field when it fails).

**Stable reference keeps its file.** The other seven are edited in place; a topic that outgrows a session
log becomes `.github/agent-memory/areas/<topic>.md`. Create a new reference file only when none of them
fits. A stable reference update alone does not require a work-item note.

One file per work item is what keeps two cards from colliding: a new path cannot conflict, and adding a
note regenerates nothing, so two branches that each add one merge without a conflict at all.

---

## Additional Notes

### Solution-Wide Settings

- [`Directory.Build.props`](Directory.Build.props) — Shared build configuration (target framework, nullable, implicit usings, version fallbacks)
- [`Directory.Build.targets`](Directory.Build.targets) — Per-project-type settings (doc enforcement, test project defaults)
- [`Directory.Packages.props`](Directory.Packages.props) — Central package version management

### Key Conventions Summary

| Aspect | Convention |
|--------|-----------|
| Language | C# latest (primary constructors, collection expressions) |
| Nullability | `nullable enable` globally |
| Imports | `ImplicitUsings enable` globally |
| CQRS | MediatR `IRequest<TResponse>` records |
| Validation | FluentValidation pipeline behavior |
| Logging | Serilog pipeline behavior |
| Content editor | Foundry, inside the SvelteKit app (no desktop tooling) |
| Web UI | SvelteKit 3 + Svelte 5 runes + Tailwind/Skeleton (Veldrath tokens only) |
| Web auth/DB | Better Auth owns credentials/sessions; Drizzle schema in `web/shared`, migrations committed |
| Game client | The SvelteKit app only (web-first) |
| Testing | xUnit + FluentAssertions (.NET); Vitest + Playwright (web, run from `web/`) |
| Mocks | Prefer `FakeXxx` stubs; Moq/NSubstitute as fallback |
| Database | EF Core (Postgres production, SQLite/InMemory for tests) |
| Documentation | XML doc comments (CS1591 = error), DocFX → docs.veldrath.com |
| CI | Forgejo Actions, mirrored actions only (see [`docs/ci-forgejo.md`](docs/ci-forgejo.md)) |
| Agent Skills | [`.roo/skills/`](.roo/skills/) for common dev workflows |
