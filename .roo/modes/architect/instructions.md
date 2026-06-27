# RealmEngine — Architect Mode Instructions

## Architecture Overview

### CQRS with MediatR + Vertical Slices
- All game operations are `IRequest<TResponse>` records
- Features organized in `RealmEngine.Core/Features/{FeatureName}/`
- Each feature contains: Commands (state changes), Queries (read operations), Services (domain logic)
- Pipeline behaviors: Validation (FluentValidation) → Logging (Serilog) → Performance

### Hub → MediatR Bridge
SignalR hubs NEVER call Core handlers directly:
```
SignalR Hub → MediatR.Send(command) → Handler → Response → Hub returns result to client
```
This keeps engine UI-agnostic. Any consumer (Avalonia, Godot, Unity, console, ASP.NET Core) reuses same operations.

### Engine Agnosticism (Hard Rule)
`RealmEngine.Core`, `RealmEngine.Shared`, `RealmEngine.Data` must NEVER depend on any UI framework.

### Solution Architecture
| Solution | Purpose |
|----------|---------|
| `RealmEngine.slnx` | Engine-only (Core + Shared + Data + tests) |
| `Veldrath.slnx` | Multiplayer (Client + Server + all) |
| `RealmForge.slnx` | Tooling (RealmForge + tests) |
| `RealmFoundry.slnx` | Community portal |
| `Realm.Full.slnx` | Everything |

## Project Structure Key Points
- 16 source projects + 11 test projects = 27 total
- Version metadata in `versions/*.props` (one per component)
- Test files mirror source structure path-for-path
- All NuGet versions centrally managed in `Directory.Packages.props`

## Key Design Decisions
- No Newtonsoft.Json in engine (migrated to System.Text.Json)
- EF Core with Postgres (production), SQLite (server tests), InMemory (data tests)
- Discord bot uses minimal MediatR pipeline (no validation, generation handlers only)
- Character attributes stored as JSON blob (`Dictionary<string, int>`)
- Equipment stored as JSON blob (`Dictionary<string, string>`)
