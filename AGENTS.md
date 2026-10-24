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
| 3 | [`.github/agent-memory/forge-foundry-sync.md`](.github/agent-memory/forge-foundry-sync.md) | Forge ↔ Foundry content schema, Foundry endpoint notes, integration test gotchas |
| 4 | [`.github/agent-memory/veldrath-memory.md`](.github/agent-memory/veldrath-memory.md) | Veldrath Server + Client hub architecture, blob schema, P3/P4 status, session log |
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

**RealmEngine** is a **framework-agnostic .NET 10 RPG game logic library**. It exposes all game operations as MediatR commands and queries — any .NET application (Avalonia, Godot via GDNative, Unity, ASP.NET Core, console, etc.) can consume it by calling `mediator.Send(command)`.

This repository also contains the **official game built on top of that engine**: **Veldrath**, a multiplayer RPG with an Avalonia desktop client, an ASP.NET Core server, and a SvelteKit web app. Additional tooling and portals include:

- **RealmForge** — Avalonia DB content editor for managing game entities in Postgres
- **SvelteKit web app** (`web/`) — the single web frontend: public site, auth, game client, Foundry, editorial, admin, and moderation
- **Veldrath.GameClient.Core** — Shared client-side game logic library (connection state, payloads, hub services)
- **Veldrath.Discord** — Discord bot integration
- **Veldrath.Auth** — Shared authentication libraries (OAuth, JWT)
- **Veldrath.Contracts** — Shared API contracts and DTOs
- **Veldrath.Assets** — Asset store and management
- **RealmUI.Fonts** — Custom font resources

---

## Project Structure

### Solution Files

| Solution | Contains | Use For |
|----------|----------|---------|
| [`RealmEngine.slnx`](RealmEngine.slnx) | Core + Data + Shared + GameClient.Core + tests | Engine-only development, CI |
| [`Veldrath.Client.slnx`](Veldrath.Client.slnx) | Client + engine + auth + assets + GameClient + fonts + tests | Desktop client development |
| [`Veldrath.Server.slnx`](Veldrath.Server.slnx) | Server + engine + assets + contracts + tests | Game server development |
| [`Veldrath.Discord.slnx`](Veldrath.Discord.slnx) | Discord + engine + contracts + tests | Discord bot development |
| [`Veldrath.Libraries.slnx`](Veldrath.Libraries.slnx) | Auth + Assets + Contracts + GameClient + fonts + engine + tests | Shared library development |
| [`Veldrath.slnx`](Veldrath.slnx) | Client + Server + GameClient.Core + tests | Multiplayer development |
| [`RealmForge.slnx`](RealmForge.slnx) | RealmForge + tests | Tooling development |
| [`Realm.Full.slnx`](Realm.Full.slnx) | Everything (.NET) | Full .NET local development |

The SvelteKit app in [`web/`](web/) is verified with npm scripts rather than a `.slnx`.

### Directory Layout

```
.
├── RealmEngine.Core/             # Game logic, MediatR handlers (zero UI dependencies)
├── RealmEngine.Shared/           # Models, interfaces, abstractions (zero UI dependencies)
├── RealmEngine.Data/             # EF Core persistence, repositories (zero UI dependencies)
├── Veldrath.Server/              # ASP.NET Core game server with SignalR hub
├── Veldrath.Client/              # Avalonia cross-platform desktop client (ReactiveUI + WebView2 Svelte shell)
├── Veldrath.GameClient.Core/     # Shared client-side logic: connection state, payloads, hub services
├── RealmForge/                   # Avalonia DB content editor (ReactiveUI)
├── Veldrath.Discord/             # Discord bot
├── Veldrath.Auth/                # Auth libraries (OAuth, JWT)
├── Veldrath.Contracts/           # Shared API contracts and DTOs
├── Veldrath.Assets/              # Asset store/management
├── RealmUI.Fonts/                # Custom fonts
├── web/                          # SvelteKit 3 / Svelte 5 web app (shared + client workspaces)
├── [Project].Tests/              # One test project per .NET library/application (10 total)
├── .github/
│   ├── agent-memory/             # Canonical memory store (8 files, see Required Reading)
│   ├── instructions/             # Detailed instruction files (7 files)
│   └── workflows/                # CI/CD workflows (per-component + deploy + docs + release)
├── .roo/skills/                  # Agent skills (build, test, run, web); catalog in .roo/skills/README.md
├── docs/                         # Documentation (DocFX → GitHub Pages)
├── wiki/                         # GitHub Wiki content (git submodule)
├── plans/                        # Live plans only; finished designs are folded into docs/
├── versions/                     # Component version props files
├── config/                       # Grafana dashboards, Prometheus config, etc.
├── scripts/                      # Build, asset, and release scripts
├── SECURITY.md                   # Security policy
└── THIRD-PARTY-NOTICES.md        # Third-party license notices
```

### Component Versions

Version metadata lives in [`versions/`](versions/):

| Props File | Component |
|------------|-----------|
| [`versions/engine.props`](versions/engine.props) | RealmEngine.Core, Shared, Data |
| [`versions/tooling.props`](versions/tooling.props) | RealmForge |
| [`versions/server.props`](versions/server.props) | Veldrath.Server |
| [`versions/client.props`](versions/client.props) | Veldrath.Client |
| [`versions/discord.props`](versions/discord.props) | Veldrath.Discord |
| [`versions/assets.props`](versions/assets.props) | Veldrath.Assets |

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

### Shared Client Architecture

- [`Veldrath.GameClient.Core/`](Veldrath.GameClient.Core/) — Framework-agnostic .NET client logic: `IGameHubConnectionService`, `IGameStateService`, connection state model, and typed payloads
- [`web/client/src/lib/game/`](web/client/src/lib/game/) — Svelte game shell, `GameSession` runes store, `CardinalGraphLayout` port, and hub payload types

### Web App (SvelteKit)

[`web/`](web/) is the single web frontend (public site, auth, game client, Foundry, editorial, admin, moderation). It is its own npm-workspaces monorepo (`shared`, `client`) with its own tooling — run all web commands **from `web/`**, never from `web/client/`.

- **Workspaces:** [`web/shared`](web/shared) (`@veldrath/shared` — Better Auth Drizzle schema and generated contracts) and [`web/client`](web/client) (`@veldrath/web` — SvelteKit app).
- **Stack:** SvelteKit 3, Svelte 5 runes, Vite 8, Tailwind CSS 4, Skeleton v5, Bits UI, Lucide, Better Auth, Drizzle ORM, adapter-node (web) plus adapter-static (desktop bundle).
- **Auth:** Better Auth owns web credentials and sessions via [`web/client/src/lib/server/auth.ts`](web/client/src/lib/server/auth.ts) (lazy `getAuth()`) and [`web/client/src/hooks.server.ts`](web/client/src/hooks.server.ts). SvelteKit mints short-lived JWTs; `Veldrath.Server` remains the game authority.
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
| Avalonia | 11.2.3 | UI framework (Veldrath.Client, RealmForge) |
| ReactiveUI | 20.1.1 | MVVM for Avalonia projects |
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
| `RealmForge/**` | `dotnet build RealmForge.slnx` |
| `Veldrath.Client/**` | `dotnet build Veldrath.Client.slnx` (run `cd web && npm run build:desktop` first to bundle the Svelte UI) |
| `Veldrath.Server/**` | `dotnet build Veldrath.Server.slnx` |
| `Veldrath.Discord/**` | `dotnet build Veldrath.Discord.slnx` |
| `Veldrath.Auth/**`, `Veldrath.Assets/**`, `Veldrath.Contracts/**` | `dotnet build Veldrath.Libraries.slnx` |
| `web/**` | `cd web && npm run typecheck && npm run lint && npm run test && npm run build` (add `npm run build:desktop` for the desktop WebView shell) |
| Cross-component / `Directory.Build.*` | `dotnet build Realm.Full.slnx` |

> **When NOT to build:** The Vite dev server hot-reloads Svelte and CSS changes. Skip full rebuilds for `.css`, `.js`, `.md`, and non-embedded `.json` changes.

### Full Build (Final Verification)

```powershell
dotnet build Realm.Full.slnx    # Full build (all .NET projects)
dotnet test Realm.Full.slnx     # All tests across the 10 .NET test projects (with coverage)
```

### Running Projects

```powershell
dotnet run --project Veldrath.Server
dotnet run --project Veldrath.Client
dotnet run --project RealmForge
cd web && npm run dev            # SvelteKit web app → http://localhost:5173
```

---

## CI / Coverage

CI workflows live in [`.github/workflows/`](.github/workflows/). Coverage is uploaded to Codecov with per-component flags.

| Workflow | Component | Coverage Flag |
|---|---|---|
| [`ci-engine.yml`](.github/workflows/ci-engine.yml) | Core + Shared + Data | `engine` |
| [`ci-client.yml`](.github/workflows/ci-client.yml) | Veldrath.Client | `client` |
| [`ci-server.yml`](.github/workflows/ci-server.yml) | Veldrath.Server | `server` |
| [`ci-tooling.yml`](.github/workflows/ci-tooling.yml) | RealmForge | `forge` |
| [`ci-discord.yml`](.github/workflows/ci-discord.yml) | Veldrath.Discord | `discord` |
| [`ci-web.yml`](.github/workflows/ci-web.yml) | SvelteKit web | `web` |
| [`deploy.yml`](.github/workflows/deploy.yml) | Deployment pipeline | — |
| [`docs.yml`](.github/workflows/docs.yml) | DocFX documentation build & deploy | — |
| [`release.yml`](.github/workflows/release.yml) | Release tagging and publishing | — |
| [`sonarcloud.yml`](.github/workflows/sonarcloud.yml) | SonarCloud static analysis | — |

Coverage exclusions are configured in [`coverage.runsettings`](coverage.runsettings).

---

## Documentation

- **GitHub Pages (DocFX):** `https://kungraseri.github.io/RealmEngine/` — generated from XML doc comments across all projects, deployed by `docs.yml`
- **GitHub Wiki** ([`wiki/`](wiki/) submodule): Getting Started, Contributing, FAQ, Roadmap Summary
- **Key docs:**
  - [`docs/deployment.md`](docs/deployment.md) — Deployment guide
  - [`docs/design-system.md`](docs/design-system.md) — UI design system reference
  - [`docs/lore/calethic-language.md`](docs/lore/calethic-language.md) — Calethic language reference

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
| [`.github/agent-memory/forge-foundry-sync.md`](.github/agent-memory/forge-foundry-sync.md) | Forge ↔ Foundry content schema, endpoint notes, integration test gotchas |
| [`.github/agent-memory/veldrath-memory.md`](.github/agent-memory/veldrath-memory.md) | Server + Client hub architecture, blob schema, OAuth flow, session log |
| [`.github/agent-memory/gap-analysis-process.md`](.github/agent-memory/gap-analysis-process.md) | Process template for gap analysis at session start (prompt templates) |
| [`.github/agent-memory/auth-and-character-creation-plan.md`](.github/agent-memory/auth-and-character-creation-plan.md) | Auth flow & character creation gap fix plan, decisions made, next steps |
| [`.github/agent-memory/combat-loop-plan.md`](.github/agent-memory/combat-loop-plan.md) | Combat loop architecture plan (actual turn-based implementation supersedes original design) |
| [`.github/agent-memory/world-lore-plan.md`](.github/agent-memory/world-lore-plan.md) | World lore plan, Calethic language reference, open items |

### Writing to Memory Files

When you discover something new about the codebase that future sessions should know:

1. **Read the relevant memory file** first to understand current state
2. **Edit it directly** with the new information
3. **Commit the change** so it travels with the repo

Do **not** create new memory files unless the existing 8 files are insufficient. Keep information organized by topic.

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
| Desktop UI | Avalonia + ReactiveUI (MVVM), WebView2 hosts the Svelte desktop build |
| Web UI | SvelteKit 3 + Svelte 5 runes + Tailwind/Skeleton (Veldrath tokens only) |
| Web auth/DB | Better Auth + Drizzle (schema in `web/shared`, migrations committed) |
| Client Core | Veldrath.GameClient.Core (framework-agnostic .NET payloads and services) |
| Testing | xUnit + FluentAssertions (server/desktop); Vitest + Playwright (web, run from `web/`) |
| Mocks | Prefer `FakeXxx` stubs; Moq/NSubstitute as fallback |
| Database | EF Core (Postgres production, SQLite/InMemory for tests) |
| Documentation | XML doc comments (CS1591 = error), DocFX → GitHub Pages |
| Agent Skills | [`.roo/skills/`](.roo/skills/) for common dev workflows |
