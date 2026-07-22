# RealmEngine — Veldrath Project Instructions

> Primary agent instructions file for Zoo Code / Roo Code / Deepseek agents working on the RealmEngine monorepo.

---

## Session Start — Required Reading

**At the beginning of every session, read these memory files before doing anything else.** They contain canonical project context, known quirks, status updates, and process templates that are committed to the repo and shared across all machines.

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

**Important:** Do **not** use `/memories/repo/` or any external memory store for codebase notes. Write new discoveries directly to the files above using file editing tools so they travel with the repo via git.

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

> **Note:** The directory [`.github/agent-memory/`](.github/agent-memory/) is referenced throughout this document with clickable links.

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

## Architecture

### CQRS with MediatR + Vertical Slice Feature Organization

- All game operations are MediatR [`IRequest<TResponse>`](https://github.com/jbogard/MediatR) records (commands change state, queries read state)
- Features live in [`RealmEngine.Core/Features/{FeatureName}/`](RealmEngine.Core/Features/) — handler, command/query record, and response DTO together in the same file
- Pipeline behaviors automatically apply validation ([FluentValidation](https://docs.fluentvalidation.net/)) and structured logging ([Serilog](https://serilog.net/)) to every operation
- Engine libraries (`Core`, `Shared`, `Data`) have **zero dependency on any UI framework** — this is a hard rule

### Hub → MediatR Bridge Pattern

SignalR hubs in [`Veldrath.Server`](Veldrath.Server/) never call Core handlers directly. Instead:

```
SignalR Hub → MediatR.Send(command) → Handler → Response → Hub returns result to client
```

This keeps the engine truly UI-agnostic and allows any consumer (Avalonia client, Godot, Unity, console, Discord bot, Blazor web app) to reuse the same game operations.

### Engine Agnosticism

The engine is UI-agnostic by design:

```csharp
// Any .NET consumer (Avalonia, Godot, Unity, console, ASP.NET Core...)
var result = await mediator.Send(new SomeGameCommand { ... });
// result is a typed DTO with the outcome of the operation
```

### Shared Client Architecture

The client-side code is split into two libraries to enable reuse across the Avalonia desktop client and the Blazor web portal:

- [`Veldrath.GameClient.Core/`](Veldrath.GameClient.Core/) — Framework-agnostic client logic: `IGameHubConnectionService`, `IGameStateService`, connection state model, and typed payloads (Chat, Combat, Dungeon, Economy, Entity, Inventory, Progression, Quest, Shop, Zone)
- [`Veldrath.GameClient.Components/`](Veldrath.GameClient.Components/) — Razor Class Library with reusable Blazor components that consume GameClient.Core services

---

## Key Technologies & Versions

All package versions are centrally managed in [`Directory.Packages.props`](Directory.Packages.props).

| Technology | Version | Purpose |
|---|---|---|
| .NET / C# | 10.0 | All projects |
| MediatR | 12.5.0 | CQRS command/query dispatch |
| FluentValidation | 12.1.1 | Input validation pipeline behavior |
| Serilog | 4.4.0 | Structured logging pipeline behavior |
| Bogus | 35.6.5 | Procedural content generation |
| Polly | 8.7.0 | Resilience patterns |
| Humanizer | 3.0.10 | Natural language formatting |
| Avalonia | 11.2.3 | UI framework (Veldrath.Client, RealmForge) |
| Avalonia.Headless.XUnit | 11.2.3 | Headless UI testing |
| ReactiveUI | 20.1.1 | MVVM for Avalonia projects |
| MudBlazor | 9.7.0 | Material Design component library (Veldrath.Web) |
| ASP.NET Core + SignalR | .NET 10 | Veldrath.Server, Veldrath.Web |
| Entity Framework Core | .NET 10 | ORM (Postgres production, SQLite/InMemory for tests) |
| xUnit | 2.9.3 | Testing framework |
| FluentAssertions | 8.10.0 | Assertion library |
| bunit | 2.6.2 | Blazor component testing (Veldrath.Web.Tests, Veldrath.GameClient.Components.Tests) |
| coverlet.collector | 10.0.1 | Code coverage collection |
| Discord.Net | 3.20.1 | Discord bot SDK |

For a comprehensive MudBlazor reference used in this project, see [`llms.txt`](llms.txt).

---

## Hard Rules

### Rule 1: XML Documentation (CS1591) — Compile-Time Error

**Every publicly visible type and member in every non-test project must have an XML doc comment (`<summary>` at minimum). This is a compile-time error, not a warning.**

Enforcement is in [`Directory.Build.targets`](Directory.Build.targets) (post-project evaluation):

```xml
<PropertyGroup Condition="'$(IsTestProject)' != 'true'">
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <WarningsAsErrors>CS1591</WarningsAsErrors>
</PropertyGroup>
```

**Rules that must never be broken:**

- **Never add `<NoWarn>CS1591</NoWarn>`** (or any CS1591 suppression) to any `.csproj` file.
- **Never add `<NoWarn>` entries** to [`Directory.Build.props`](Directory.Build.props) or [`Directory.Build.targets`](Directory.Build.targets) that suppress CS1591.
- If you add or modify a public type, method, property, constructor, or interface member, you **must** add the corresponding XML doc comment in the same change — do not defer it.
- If the build fails with CS1591 errors, fix the missing doc comments; do not suppress the error.

**Doc comment format:**

```csharp
/// <summary>Brief description of what this does.</summary>
/// <param name="foo">What foo represents.</param>
/// <returns>What the return value means.</returns>
public SomeType DoSomething(string foo) { ... }
```

Constructors require at minimum a `<summary>` line: `/// <summary>Initializes a new instance of <see cref="ClassName"/>.</summary>`

### Rule 2: Engine Agnosticism

Engine libraries ([`RealmEngine.Core`](RealmEngine.Core/), [`RealmEngine.Shared`](RealmEngine.Shared/), [`RealmEngine.Data`](RealmEngine.Data/)) must **never** take a dependency on any UI framework (Avalonia, ASP.NET Core MVC/Razor, Blazor, etc.).

### Rule 3: No Mocking Frameworks by Default

Prefer `FakeXxx` stub classes in `Infrastructure/` directories over mocking frameworks. [`Moq`](https://github.com/moq/moq4) and [`NSubstitute`](https://nsubstitute.github.io/) are available only when stubs aren't practical.

---

## Coding Conventions

- `nullable enable` and `ImplicitUsings enable` globally via [`Directory.Build.props`](Directory.Build.props)
- C# latest — primary constructors, collection expressions, and latest language features are in use
- New MediatR operations: `record CommandName : IRequest<ResponseDto>` + `class CommandNameHandler : IRequestHandler<CommandName, ResponseDto>` in the same file under the relevant [`Features/`](RealmEngine.Core/Features/) folder
- Avalonia ViewModels use ReactiveUI patterns:
  - `RaiseAndSetIfChanged` for bindable properties
  - `ReactiveCommand.CreateFromTask` for async operations
  - `WhenAnyValue` for derived state
- Blazor components follow detailed conventions in [`.github/instructions/blazor-component-development.md`](.github/instructions/blazor-component-development.md):
  - MudBlazor-first: always check [`llms.txt`](llms.txt) for an existing MudBlazor component before writing custom UI
  - Code-behind partial class pattern (`Component.razor` + `Component.razor.cs`)
  - Components reused across multiple pages go in `Components/Shared/`; single-use UI stays in the page
  - `[Parameter]` for data in, `EventCallback<T>` for events out, `@bind-{Property}` for two-way binding
- Styling follows the priority hierarchy in [`.github/instructions/styling-and-css.md`](.github/instructions/styling-and-css.md):
  - 1) MudBlazor CSS utilities (`Class="mt-4 d-flex gap-2"`) — always first
  - 2) MudBlazor theme system ([`VeldrathTheme.cs`](Veldrath.Web/Themes/VeldrathTheme.cs))
  - 3) Custom CSS with VDS design tokens (kebab-case, BEM-like naming)
  - 4) Inline `style=""` attributes — absolute last resort, only for truly dynamic values
- Avoid one-off abstractions, helpers, or comments explaining what the code does — **only comment the *why* when it isn't obvious**

---

## Testing Conventions

- **13 test projects** (mirroring each source library/application), all in the solution — see [`.github/agent-memory/engine-codebase.md`](.github/agent-memory/engine-codebase.md) for current test counts
- **8,500+ tests** — all must pass; `dotnet test Realm.Full.slnx` runs the full suite
- Test files mirror source structure: [`RealmEngine.Core/Features/Combat/`](RealmEngine.Core/Features/Combat/) → [`RealmEngine.Core.Tests/Features/Combat/`](RealmEngine.Core.Tests/Features/Combat/)
- Use `[Fact]` / `[Theory]` with FluentAssertions; standard **Arrange / Act / Assert** layout
- Prefer `FakeXxx` stub classes in `Infrastructure/` directories over mocking frameworks
- EF Core `InMemory` provider is the default for most data tests; SQLite is used for [`Veldrath.Server.Tests`](Veldrath.Server.Tests/) integration tests where SQL semantics matter
- Avalonia headless tests use `[AvaloniaFact]` from [`Avalonia.Headless.XUnit`](https://github.com/AvaloniaUI/Avalonia.Headless.XUnit); add `[Trait("Category", "UI")]` only when a real display is required
- Blazor component tests use [`bunit`](https://bunit.dev/) (v2.6.2) — render components with `RenderComponent<T>()`, assert with `Find()`, `MarkupMatches()`, `FindComponent<T>()`
- Apply `[ExcludeFromCodeCoverage]` to: entry points (`Program`), app bootstrappers (`App`), compiled XAML resources, thin infrastructure wrappers (e.g. `HubConnectionFactory`, `HubConnectionWrapper` in [`Veldrath.Client`](Veldrath.Client/))
- Every test project's [`Properties/AssemblyInfo.cs`](RealmEngine.Core.Tests/Properties/AssemblyInfo.cs) has `[assembly: ExcludeFromCodeCoverage]`

---

## Build & Test Commands

```powershell
# Full build
dotnet build Realm.Full.slnx

# All 8,500+ tests (with coverage)
dotnet test Realm.Full.slnx

# Start the game server (ASP.NET Core + SignalR)
dotnet run --project Veldrath.Server

# Start the desktop game client (Avalonia)
dotnet run --project Veldrath.Client

# Start the web portal (Blazor Server + MudBlazor)
dotnet run --project Veldrath.Web

# Launch the DB content editor
dotnet run --project RealmForge

# Launch the community content portal
dotnet run --project RealmFoundry
```

Use `Ctrl+Shift+B` in VS Code to trigger the default build task, or `F5` to debug.

---

## Agent Skills

The [`.roo/skills/`](.roo/skills/) directory contains agent skills for common development workflows:

| Skill | Purpose |
|-------|---------|
| [`build-full`](.roo/skills/build-full/SKILL.md) | Build the complete Realm.Full.slnx solution |
| [`create-new-feature`](.roo/skills/create-new-feature/SKILL.md) | Scaffold a new MediatR vertical-slice feature |
| [`run-all-tests`](.roo/skills/run-all-tests/SKILL.md) | Run the complete test suite with coverage |
| [`run-client`](.roo/skills/run-client/SKILL.md) | Start the Veldrath Avalonia desktop client |
| [`run-realmforge`](.roo/skills/run-realmforge/SKILL.md) | Start the RealmForge DB content editor |
| [`run-realmfoundry`](.roo/skills/run-realmfoundry/SKILL.md) | Start the RealmFoundry community portal |
| [`run-server`](.roo/skills/run-server/SKILL.md) | Start the Veldrath ASP.NET Core game server |
| [`run-tests-by-component`](.roo/skills/run-tests-by-component/SKILL.md) | Run tests for a single component |
| [`start-dev-stack`](.roo/skills/start-dev-stack/SKILL.md) | Start the full local dev environment (DB + server + client) |

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
  - [`wiki/Engine-GDD.md`](wiki/Engine-GDD.md) — Game Design Document
  - [`wiki/Engine-Combat.md`](wiki/Engine-Combat.md) — Combat system design
  - [`wiki/Engine-Character-Creation.md`](wiki/Engine-Character-Creation.md) — Character creation design
  - [`wiki/Engine-Implementation-Status.md`](wiki/Engine-Implementation-Status.md) — Implementation status tracker
  - [`plans/`](plans/) — Architecture and implementation plans (game client unification, web architecture, rendering extraction, etc.)
- **DocFX config:** [`docfx.json`](docfx.json) — sources the full solution (`Realm.Full.slnx`) and outputs to `_site/`
- **MudBlazor reference:** [`llms.txt`](llms.txt) — Comprehensive MudBlazor component and API reference for LLM context

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
