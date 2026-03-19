# RealmEngine — Copilot Instructions (.NET 10.0)

## Session Start — Required Reading

**At the beginning of every session, read these files before doing anything else:**

- [`.github/copilot-memory/engine-codebase.md`](.github/copilot-memory/engine-codebase.md) — key model facts, positional record constructors, known handler quirks
- [`.github/copilot-memory/json-migration-status.md`](.github/copilot-memory/json-migration-status.md) — migration history and completed work log

These files are committed to the repo and shared across all machines. **Do not use `/memories/repo/` for codebase notes** — write new discoveries directly to the files above using the file editing tools so they travel with the repo via git.

---

## What This Repository Is

**RealmEngine** is a **framework-agnostic .NET 10 RPG game logic library**. It exposes all game operations as MediatR commands and queries — any .NET application (Avalonia, Godot via GDNative, Unity, ASP.NET Core, console, etc.) can consume it by calling `mediator.Send(command)`.

This repository also contains the **official game built on top of that engine**: RealmUnbound, a multiplayer RPG with an Avalonia desktop client and an ASP.NET Core server. RealmForge, an Avalonia DB content editor for managing game entities in Postgres, is here too. RealmFoundry is a Blazor Server web app for community content submission and curation.

## Project Structure

```
RealmEngine/
├── RealmEngine.Core/          # Game logic, MediatR handlers (zero UI dependencies)
├── RealmEngine.Shared/        # Models, interfaces, abstractions (zero UI dependencies)
├── RealmEngine.Data/          # EF Core persistence, repositories (zero UI dependencies)
├── RealmUnbound.Server/       # ASP.NET Core game server with SignalR hub
├── RealmUnbound.Client/       # Avalonia cross-platform desktop client (ReactiveUI)
├── RealmForge/                # Avalonia DB content editor for game entities (ReactiveUI)
├── RealmFoundry/              # Blazor Server web app — community content submission portal
├── [Project].Tests/           # One test project per library/application
├── .vscode/                   # Build tasks and debug launch configs
├── docs/                      # Documentation (DocFX → GitHub Pages)
└── wiki/                      # GitHub Wiki content (git submodule)
```

## Solution Files

| Solution | Contains | Use For |
|----------|----------|---------|
| `RealmEngine.slnx` | Core + Data + Shared + tests | Engine-only development, CI |
| `RealmUnbound.slnx` | Client + Server + tests | Multiplayer development |
| `RealmForge.slnx` | RealmForge + tests | Tooling development |
| `RealmFoundry.slnx` | RealmFoundry + Server + tests | Community portal development |
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
var result = await mediator.Send(new SomeGameCommand { ... });
// result is a typed DTO with the outcome of the operation
```

## Key Technologies

| Technology | Version | Purpose |
|---|---|---|
| .NET / C# | 10.0 | All projects |
| MediatR | 12.4.1 | CQRS command/query dispatch |
| FluentValidation | 12.1.1 | Input validation pipeline behavior |
| Serilog | 4.3.0 | Structured logging pipeline behavior |
| Bogus | 35.6.5 | Procedural content generation |
| Polly | 8.6.5 | Resilience patterns |
| Humanizer | 3.0.1 | Natural language formatting |
| Avalonia | 11.2.3 | UI framework (RealmUnbound.Client, RealmForge) |
| ReactiveUI | 20.1.1 | MVVM for Avalonia projects |
| ASP.NET Core + SignalR | .NET 10 | RealmUnbound.Server |
| xUnit + FluentAssertions | 2.9.3 + 8.8.0 | Testing |
| Avalonia.Headless.XUnit | 11.2.3 | Headless UI testing |
## XML Documentation (CS1591) — Hard Rule

**Every publicly visible type and member in every non-test project must have an XML doc comment (`<summary>` at minimum). This is a compile-time error, not a warning.**

### How enforcement works

`Directory.Build.targets` sets `<GenerateDocumentationFile>true</GenerateDocumentationFile>` and `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` for all projects where `$(IsTestProject) != 'true'`. This makes CS1591 (missing XML comment) a hard build error across the entire solution.

### Rules that must never be broken

- **Never add `<NoWarn>CS1591</NoWarn>` (or any CS1591 suppression) to any `.csproj` file.** Doing so silently hides undocumented public API and defeats the enforcement entirely.
- **Never add `<NoWarn>` entries to `Directory.Build.props` or `Directory.Build.targets` that suppress CS1591.**
- If you add or modify a public type, method, property, constructor, or interface member, you **must** add the corresponding XML doc comment in the same change — do not defer it.
- If the build fails with CS1591 errors, fix the missing doc comments; do not suppress the error.

### Where doc enforcement lives

The **only** correct place for doc enforcement is `Directory.Build.targets` (post-project evaluation, so `$(IsTestProject)` is already set). Do not add enforcement logic to `Directory.Build.props` (pre-project, `IsTestProject` is not yet populated).

### Doc comment format

```csharp
/// <summary>Brief description of what this does.</summary>
/// <param name="foo">What foo represents.</param>
/// <returns>What the return value means.</returns>
public SomeType DoSomething(string foo) { ... }
```

Constructors require at minimum a `<summary>` line: `/// <summary>Initializes a new instance of <see cref="ClassName"/>.</summary>`

---

## Coding Conventions

- `nullable enable` and `ImplicitUsings enable` globally via `Directory.Build.props`
- C# latest — primary constructors, collection expressions, and latest language features are in use
- Engine libraries (`Core`, `Shared`, `Data`) must never take a dependency on any UI framework
- New MediatR operations: `record CommandName : IRequest<ResponseDto>` + `class CommandNameHandler : IRequestHandler<CommandName, ResponseDto>` in the same file under the relevant `Features/` folder
- Avalonia ViewModels use ReactiveUI: `RaiseAndSetIfChanged` for bindable properties, `ReactiveCommand.CreateFromTask` for async operations, `WhenAnyValue` for derived state
- Avoid one-off abstractions, helpers, or comments explaining what the code does — only comment the *why* when it isn't obvious

## Testing Conventions

- Seven test projects (one per source library/application), all in the solution
- **8,500+ tests** — all must pass; `dotnet test Realm.Full.slnx` runs the full suite
- Test files mirror source structure: `RealmEngine.Core/Features/Combat/` → `RealmEngine.Core.Tests/Features/Combat/`
- Use `[Fact]` / `[Theory]` with FluentAssertions; standard `Arrange / Act / Assert` layout
- Prefer `FakeXxx` stub classes in `Infrastructure/` directories over mocking frameworks; `Moq` and `NSubstitute` are available when stubs aren't practical
- EF Core `InMemory` provider is the default for most data tests; SQLite is used for `RealmUnbound.Server.Tests` integration tests where SQL semantics matter
- Avalonia headless tests use `[AvaloniaFact]` from `Avalonia.Headless.XUnit`; add `[Trait("Category", "UI")]` only when a real display is required
- Apply `[ExcludeFromCodeCoverage]` to: entry points (`Program`), app bootstrappers (`App`), compiled XAML resources, thin infrastructure wrappers (e.g. `HubConnectionFactory`, `HubConnectionWrapper` in RealmUnbound.Client)
- Every test project's `Properties/AssemblyInfo.cs` has `[assembly: ExcludeFromCodeCoverage]`

## How to Build and Test

```powershell
dotnet build Realm.Full.slnx              # Full build
dotnet test Realm.Full.slnx               # All 8,500+ tests
dotnet run --project RealmForge           # Launch the DB content editor
dotnet run --project RealmFoundry         # Launch the community content portal
dotnet run --project RealmUnbound.Server  # Start the game server
dotnet run --project RealmUnbound.Client  # Start the game client
```

Use `Ctrl+Shift+B` in VS Code to trigger the default build task, or `F5` to debug.

## CI / Coverage

| Workflow | Component | Coverage flag |
|---|---|---|
| `ci-engine.yml` | Core + Shared + Data | `engine` |
| `ci-client.yml` | RealmUnbound.Client | `client` |
| `ci-server.yml` | RealmUnbound.Server | `server` |
| `ci-tooling.yml` | RealmForge | `forge` |
| `ci-discord.yml` | RealmUnbound.Discord | `discord` |

Coverage is uploaded to Codecov with per-component flags. Exclusions are configured in `coverage.runsettings`.

## Documentation

- **GitHub Pages** (DocFX): `https://kungraseri.github.io/RealmEngine/` — generated from XML doc comments across all projects, deployed by `docs.yml`
- **GitHub Wiki** (`wiki/` submodule): Getting Started, Contributing, FAQ, Roadmap Summary
- Key docs: `docs/GDD-Main.md` (game design), `docs/ROADMAP.md`, `docs/IMPLEMENTATION_STATUS.md`
- DocFX config: `docfx.json` — sources the full solution (`Realm.Full.slnx`) and outputs to `_site/`
