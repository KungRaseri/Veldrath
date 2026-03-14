# RealmEngine — Copilot Instructions (.NET 10.0)

## What This Repository Is

**RealmEngine** is a **framework-agnostic .NET 10 RPG game logic library**. It exposes all game operations as MediatR commands and queries — any .NET application (Avalonia, Godot via GDNative, Unity, ASP.NET Core, console, etc.) can consume it by calling `mediator.Send(command)`.

This repository also contains the **official game built on top of that engine**: RealmUnbound, a multiplayer RPG with an Avalonia desktop client and an ASP.NET Core server. RealmForge, an Avalonia DB content editor for managing game entities in Postgres, is here too.

## Project Structure

```
RealmEngine/
├── RealmEngine.Core/          # Game logic, MediatR handlers (zero UI dependencies)
├── RealmEngine.Shared/        # Models, interfaces, abstractions (zero UI dependencies)
├── RealmEngine.Data/          # JSON loading, LiteDB persistence (zero UI dependencies)
├── RealmUnbound.Server/       # ASP.NET Core game server with SignalR hub
├── RealmUnbound.Client/       # Avalonia cross-platform desktop client (ReactiveUI)
├── RealmForge/                # Avalonia DB content editor for game entities (ReactiveUI)
├── [Project].Tests/           # One test project per library/application
├── .vscode/                   # Build tasks and debug launch configs
├── docs/                      # Documentation (MkDocs → GitHub Pages)
└── wiki/                      # GitHub Wiki content (git submodule)
```

## Solution Files

| Solution | Contains | Use For |
|----------|----------|---------|
| `RealmEngine.slnx` | Core + Data + Shared + tests | Engine-only development, CI |
| `RealmUnbound.slnx` | Client + Server + tests | Multiplayer development |
| `RealmForge.slnx` | RealmForge + tests | Tooling development |
| `Realm.Full.slnx` | Everything | Full-stack local development |

## Architecture

**CQRS with MediatR + Vertical Slice Feature Organization**

- All game operations are MediatR `IRequest<TResponse>` records (commands change state, queries read state)
- Features live in `RealmEngine.Core/Features/{FeatureName}/` — handler, command/query record, and response DTO together
- Pipeline behaviors automatically apply validation (FluentValidation) and structured logging (Serilog) to every operation
- Engine libraries (`Core`, `Shared`, `Data`) have **zero dependency on any UI framework** — this is a hard rule

**Engine Agnosticism**: The engine is UI-agnostic by design. Any .NET application can consume it via MediatR. The official clients in this repo are Avalonia-based (RealmUnbound.Client, RealmForge), but nothing in the engine assumes a particular UI layer.

### Integration Pattern

```csharp
// Any .NET consumer (Avalonia, Godot, Unity, console, ASP.NET Core...)
var result = await mediator.Send(new AttackEnemyCommand
{
    CharacterName = "Player1",
    Action = CombatActionType.Attack
});
// result is a typed DTO — Success, Damage, PlayerHealth, EnemyHealth, etc.
```

## Key Technologies

| Technology | Version | Purpose |
|---|---|---|
| .NET / C# | 10.0 | All projects |
| MediatR | 14.0.0 | CQRS command/query dispatch |
| FluentValidation | 12.1.1 | Input validation pipeline behavior |
| Serilog | 4.3.0 | Structured logging pipeline behavior |
| LiteDB | 5.0.21 | Save game persistence |
| Newtonsoft.Json | 13.0.4 | JSON game data loading |
| Bogus | 35.6.5 | Procedural content generation |
| Polly | 8.6.5 | Resilience patterns |
| Humanizer | 3.0.1 | Natural language formatting |
| Avalonia | 11.2.3 | UI framework (RealmUnbound.Client, RealmForge) |
| ReactiveUI | 20.1.1 | MVVM for Avalonia projects |
| ASP.NET Core + SignalR | .NET 10 | RealmUnbound.Server |
| xUnit + FluentAssertions | 2.9.3 + 8.8.0 | Testing |
| Avalonia.Headless.XUnit | 11.2.3 | Headless UI testing |
## Coding Conventions

- `nullable enable` and `ImplicitUsings enable` globally via `Directory.Build.props`
- C# 13 — primary constructors, collection expressions, and latest language features are in use
- Engine libraries (`Core`, `Shared`, `Data`) must never take a dependency on any UI framework
- New MediatR operations: `record CommandName : IRequest<ResponseDto>` + `class CommandNameHandler : IRequestHandler<CommandName, ResponseDto>` in the same file under the relevant `Features/` folder
- Avalonia ViewModels use ReactiveUI: `RaiseAndSetIfChanged` for bindable properties, `ReactiveCommand.CreateFromTask` for async operations, `WhenAnyValue` for derived state
- Avoid one-off abstractions, helpers, or comments explaining what the code does — only comment the *why* when it isn't obvious

## JSON Data Standards (v4.0 + v4.1 References)

**All game data files follow strict standards documented in `docs/standards/json/`:**

### JSON Reference System v4.1

**Purpose**: Unified system for linking game data across domains to eliminate duplication

**Reference Syntax**: `@domain/path/category:item-name[filters]?.property.nested`

**Common Reference Patterns**:
- Abilities: `@abilities/active/offensive:basic-attack`
- Classes: `@classes/warriors:fighter`
- Items: `@items/weapons/swords:iron-longsword`
- Enemies: `@enemies/humanoid:goblin-warrior`
- NPCs: `@npcs/merchants:blacksmith`
- Quests: `@quests/main-story:chapter-1`

**Features**:
- Direct references: Link to specific items
- Property access: Use dot notation (`.property.nested`)
- Wildcard selection: `:*` for random item respecting rarityWeight
- Optional references: `?` suffix returns null instead of error
- Filtering: Support for operators (=, !=, <, <=, >, >=, EXISTS, NOT EXISTS, MATCHES)

**Documentation**: `docs/standards/json/JSON_REFERENCE_STANDARDS.md`

### names.json Standard (Pattern Generation)

**Required Fields:**
- `version`: "4.0"
- `type`: "pattern_generation"
- `supportsTraits`: true or false
- `lastUpdated`: ISO date string
- `description`: Purpose of the file
- `patterns[]`: Array with `rarityWeight` (NOT "weight")
- `components{}`: Component arrays (prefix, suffix, etc.)

**Pattern Syntax:**
- Component tokens: `{base}`, `{prefix}`, `{suffix}`, `{quality}`
- External references: Use v4.1 syntax `@items/materials/metals` instead of old `[@materialRef/weapon]`
- NO "example" fields allowed

### catalog.json Standard (Item/Enemy Definitions)

**Required Metadata:**
- `description`, `version`, `lastUpdated`, `type` (ends with "_catalog")

**Structure:**
- All items MUST have `name` and `rarityWeight`
- Physical "weight" allowed (item weight in lbs)
- Use references instead of hardcoded names (e.g., `@abilities/...` not "Basic Attack")

### .cbconfig.json Standard (ContentBuilder UI)

**Required Fields:**
- `icon`: MaterialDesign icon name (NOT emojis)
- `sortOrder`: Integer for tree position

**Standards Documentation**: `docs/standards/json/` — covers the reference system, names.json, catalog.json, and .cbconfig.json formats in full detail.

### Budget System Field Standardization

All game data uses `rarityWeight` exclusively for both selection probability and cost calculation. Costs are calculated dynamically — never stored.

- **Materials**: `cost = (6000 / rarityWeight) × costScale`
- **Components**: `cost = 100 / rarityWeight`
- **Enchantments**: `cost = 130 / rarityWeight`

**Rarity tiers** are also derived on-demand from `rarityWeight` (Common 50–100 → Legendary 1–4).

**Material pools** are dictionary-keyed (not arrays) in `material-pools.json`.

## Testing Conventions

- Six test projects (one per source library/application), all in the solution
- **8,500+ tests** — all must pass; `dotnet test Realm.Full.slnx` runs the full suite
- Test files mirror source structure: `RealmEngine.Core/Features/Combat/` → `RealmEngine.Core.Tests/Features/Combat/`
- Use `[Fact]` / `[Theory]` with FluentAssertions; standard `Arrange / Act / Assert` layout
- Prefer `FakeXxx` stub classes in `Infrastructure/` directories over mocking frameworks
- Avalonia headless tests use `[AvaloniaFact]` from `Avalonia.Headless.XUnit`; add `[Trait("Category", "UI")]` only when a real display is required
- Apply `[ExcludeFromCodeCoverage]` to: entry points (`Program`), app bootstrappers (`App`), compiled XAML resources, thin infrastructure wrappers (`HubConnectionFactory`, `HubConnectionWrapper`)
- Every test project's `Properties/AssemblyInfo.cs` has `[assembly: ExcludeFromCodeCoverage]`

## How to Build and Test

```powershell
dotnet build Realm.Full.slnx              # Full build
dotnet test Realm.Full.slnx               # All 8,500+ tests
dotnet run --project RealmForge           # Launch the DB content editor
dotnet run --project RealmUnbound.Server  # Start the game server
dotnet run --project RealmUnbound.Client  # Start the game client
```

Use `Ctrl+Shift+B` in VS Code to trigger the default build task, or `F5` to debug.

## CI / Coverage

Four CI workflows, one per component:

| Workflow | Component | Coverage flag |
|---|---|---|
| `ci-engine.yml` | Core + Shared + Data | `engine` |
| `ci-client.yml` | RealmUnbound.Client | `client` |
| `ci-server.yml` | RealmUnbound.Server | `server` |
| `ci-tooling.yml` | RealmForge | `forge` |

Coverage is uploaded to Codecov with per-component flags. Exclusions are configured in `coverage.runsettings`.

## Documentation

- **GitHub Pages** (MkDocs Material): `https://kungraseri.github.io/RealmEngine/`
- **GitHub Wiki** (`wiki/` submodule): Getting Started, Contributing, FAQ, Roadmap Summary
- Key docs: `docs/GDD-Main.md` (game design), `docs/ROADMAP.md`, `docs/IMPLEMENTATION_STATUS.md`
