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
| 4 | [`.github/agent-memory/unbound-memory.md`](.github/agent-memory/unbound-memory.md) | Veldrath Server + Client hub architecture, blob schema, P3/P4 status, session log |
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
| [`.github/instructions/blazor-component-development.md`](.github/instructions/blazor-component-development.md) | When building Blazor UI components |
| [`.github/instructions/styling-and-css.md`](.github/instructions/styling-and-css.md) | When writing CSS or styling Blazor UI |

---

## Project Overview

**RealmEngine** is a **framework-agnostic .NET 10 RPG game logic library**. It exposes all game operations as MediatR commands and queries — any .NET application (Avalonia, Godot via GDNative, Unity, ASP.NET Core, console, etc.) can consume it by calling `mediator.Send(command)`.

This repository also contains the **official game built on top of that engine**: **Veldrath**, a multiplayer RPG with an Avalonia desktop client and an ASP.NET Core server. Additional tooling and portals include:

- **RealmForge** — Avalonia DB content editor for managing game entities in Postgres
- **RealmFoundry** — Blazor Server web app for community content submission and curation
- **Veldrath.Web** — Blazor Server (InteractiveServer) public website and game portal; uses MudBlazor for UI
- **Veldrath.GameClient.Core** — Shared client-side game logic library (connection state, payloads, hub services)
- **Veldrath.GameClient.Components** — Shared Razor Class Library with reusable Blazor UI components
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
| [`RealmEngine.slnx`](RealmEngine.slnx) | Core + Data + Shared + tests | Engine-only development, CI |
| [`Veldrath.slnx`](Veldrath.slnx) | Client + Server + GameClient.* + tests | Multiplayer development |
| [`RealmForge.slnx`](RealmForge.slnx) | RealmForge + tests | Tooling development |
| [`RealmFoundry.slnx`](RealmFoundry.slnx) | RealmFoundry + tests | Community portal development |
| [`Realm.Full.slnx`](Realm.Full.slnx) | Everything | Full-stack local development |

### Directory Layout

```
.
├── RealmEngine.Core/             # Game logic, MediatR handlers (zero UI dependencies)
├── RealmEngine.Shared/           # Models, interfaces, abstractions (zero UI dependencies)
├── RealmEngine.Data/             # EF Core persistence, repositories (zero UI dependencies)
├── Veldrath.Server/              # ASP.NET Core game server with SignalR hub
├── Veldrath.Client/              # Avalonia cross-platform desktop client (ReactiveUI)
├── Veldrath.Web/                 # Blazor Server (InteractiveServer) public website with MudBlazor
├── Veldrath.GameClient.Core/     # Shared client-side logic: connection state, payloads, hub services
├── Veldrath.GameClient.Components/ # Razor Class Library: reusable Blazor UI components
├── RealmForge/                   # Avalonia DB content editor (ReactiveUI)
├── RealmFoundry/                 # Blazor Server community content portal
├── Veldrath.Discord/             # Discord bot
├── Veldrath.Auth/                # Auth libraries (OAuth, JWT, Blazor auth state)
├── Veldrath.Contracts/           # Shared API contracts and DTOs
├── Veldrath.Assets/              # Asset store/management
├── RealmUI.Fonts/                # Custom fonts
├── [Project].Tests/              # One test project per library/application (13 total)
├── .github/
│   ├── agent-memory/             # Canonical memory store (8 files, see Required Reading)
│   ├── instructions/             # Detailed instruction files (7 files)
│   └── workflows/                # CI/CD workflows (per-component + deploy + docs + release)
├── .roo/skills/                  # Agent skills for common tasks (build, test, run, etc.)
├── docs/                         # Documentation (DocFX → GitHub Pages)
├── wiki/                         # GitHub Wiki content (git submodule)
├── plans/                        # Architecture and implementation plans
├── versions/                     # Component version props files
├── config/                       # Grafana dashboards, Prometheus config, etc.
├── scripts/                      # Build, asset, and release scripts
├── llms.txt                      # MudBlazor comprehensive reference for LLMs
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
| [`versions/web.props`](versions/web.props) | Veldrath.Web |
| [`versions/foundry.props`](versions/foundry.props) | RealmFoundry |
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

- [`Veldrath.GameClient.Core/`](Veldrath.GameClient.Core/) — Framework-agnostic client logic: `IGameHubConnectionService`, `IGameStateService`, connection state model, and typed payloads
- [`Veldrath.GameClient.Components/`](Veldrath.GameClient.Components/) — Razor Class Library with reusable Blazor components that consume GameClient.Core services

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
| MudBlazor | 9.7.0 | Material Design component library (Veldrath.Web) |
| ASP.NET Core + SignalR | .NET 10 | Veldrath.Server, Veldrath.Web |
| Entity Framework Core | .NET 10 | ORM (Postgres production, SQLite/InMemory for tests) |
| xUnit | 2.9.3 | Testing framework |
| FluentAssertions | 8.10.0 | Assertion library |
| bunit | 2.6.2 | Blazor component testing |

For a comprehensive MudBlazor reference, see [`llms.txt`](llms.txt).

---

## Build & Test Commands

```powershell
dotnet build Realm.Full.slnx    # Full build
dotnet test Realm.Full.slnx     # All 4,700+ tests (with coverage)
```

Individual project runs: `dotnet run --project Veldrath.Server`, `Veldrath.Client`, `Veldrath.Web`, `RealmForge`, `RealmFoundry`.

> **See [`.roo/skills/`](.roo/skills/) for agent skills** that automate build, test, run, and dev-stack workflows. Use `Ctrl+Shift+B` in VS Code for the default build task, or `F5` to debug.

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
| [`ci-foundry.yml`](.github/workflows/ci-foundry.yml) | RealmFoundry | `foundry` |
| [`ci-web.yml`](.github/workflows/ci-web.yml) | Veldrath.Web | `web` |
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
- **MudBlazor reference:** [`llms.txt`](llms.txt) — Comprehensive component and API reference for LLM context

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
| [`.github/agent-memory/unbound-memory.md`](.github/agent-memory/unbound-memory.md) | Server + Client hub architecture, blob schema, OAuth flow, session log |
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
| Desktop UI | Avalonia + ReactiveUI (MVVM) |
| Web UI | Blazor Server + MudBlazor (9.7.0) |
| Client Core | Veldrath.GameClient.Core (framework-agnostic payloads and services) |
| Shared Components | Veldrath.GameClient.Components (Razor Class Library) |
| Testing | xUnit + FluentAssertions |
| Blazor Testing | bunit (v2.6.2) |
| Mocks | Prefer `FakeXxx` stubs; Moq/NSubstitute as fallback |
| Database | EF Core (Postgres production, SQLite/InMemory for tests) |
| Documentation | XML doc comments (CS1591 = error), DocFX → GitHub Pages |
| Agent Skills | [`.roo/skills/`](.roo/skills/) for common dev workflows |
