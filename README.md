# RealmEngine

[![.NET Version](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Documentation](https://img.shields.io/badge/docs-GitHub%20Pages-blue)](https://kungraseri.github.io/RealmEngine/)

## CI Status

| Component | Build | Coverage |
|-----------|-------|----------|
| Engine Libraries | [![CI - Engine](https://github.com/KungRaseri/RealmEngine/actions/workflows/ci-engine.yml/badge.svg)](https://github.com/KungRaseri/RealmEngine/actions/workflows/ci-engine.yml) | [![codecov (engine)](https://codecov.io/gh/KungRaseri/RealmEngine/branch/main/graph/badge.svg?flag=engine)](https://codecov.io/gh/KungRaseri/RealmEngine?flag=engine) |
| RealmForge Tooling | [![CI - Tooling](https://github.com/KungRaseri/RealmEngine/actions/workflows/ci-tooling.yml/badge.svg)](https://github.com/KungRaseri/RealmEngine/actions/workflows/ci-tooling.yml) | [![codecov (forge)](https://codecov.io/gh/KungRaseri/RealmEngine/branch/main/graph/badge.svg?flag=forge)](https://codecov.io/gh/KungRaseri/RealmEngine?flag=forge) |
| RealmUnbound Client | [![CI - Client](https://github.com/KungRaseri/RealmEngine/actions/workflows/ci-client.yml/badge.svg)](https://github.com/KungRaseri/RealmEngine/actions/workflows/ci-client.yml) | [![codecov (client)](https://codecov.io/gh/KungRaseri/RealmEngine/branch/main/graph/badge.svg?flag=client)](https://codecov.io/gh/KungRaseri/RealmEngine?flag=client) |
| RealmUnbound Server | [![CI - Server](https://github.com/KungRaseri/RealmEngine/actions/workflows/ci-server.yml/badge.svg)](https://github.com/KungRaseri/RealmEngine/actions/workflows/ci-server.yml) | [![codecov (server)](https://codecov.io/gh/KungRaseri/RealmEngine/branch/main/graph/badge.svg?flag=server)](https://codecov.io/gh/KungRaseri/RealmEngine?flag=server) |

## Latest Releases

| Component | Latest |
|-----------|--------|
| Engine Libraries | [![Engine release](https://img.shields.io/github/v/release/KungRaseri/RealmEngine?filter=engine%2Fv*&label=engine&color=brightgreen)](https://github.com/KungRaseri/RealmEngine/releases?q=engine&expanded=true) |
| RealmForge Tooling | [![Forge release](https://img.shields.io/github/v/release/KungRaseri/RealmEngine?filter=tooling%2Fv*&label=forge&color=brightgreen)](https://github.com/KungRaseri/RealmEngine/releases?q=tooling&expanded=true) |
| RealmUnbound Client | [![Client release](https://img.shields.io/github/v/release/KungRaseri/RealmEngine?filter=client%2Fv*&label=client&color=brightgreen)](https://github.com/KungRaseri/RealmEngine/releases?q=client&expanded=true) |
| RealmUnbound Server | [![Server release](https://img.shields.io/github/v/release/KungRaseri/RealmEngine?filter=server%2Fv*&label=server&color=brightgreen)](https://github.com/KungRaseri/RealmEngine/releases?q=server&expanded=true) |

RPG backend engine implementing CQRS with MediatR for clean command/query separation. Includes a multiplayer client/server (RealmUnbound) and a JSON data editor (RealmForge).

## Architecture

**Vertical Slice + CQRS with MediatR**

### Core Libraries (`RealmEngine.slnx`)
- **RealmEngine.Core** — Game logic (combat, inventory, crafting, quests)
- **RealmEngine.Data** — JSON data loading, persistence (SQLite / PostgreSQL)
- **RealmEngine.Shared** — Models, utilities, abstractions

### Applications
- **RealmUnbound.Server** — ASP.NET Core game server with SignalR hub
- **RealmUnbound.Client** — Avalonia UI desktop client with ReactiveUI
- **RealmForge** — Avalonia UI tool for editing JSON game data

Code is organized by business feature (Features/Combat, Features/Inventory, etc.) with automatic validation and logging via MediatR pipeline behaviors.

## Usage

Dispatch commands and queries via MediatR to interact with the engine from any .NET application:

```csharp
var result = await mediator.Send(new AttackEnemyCommand
{
    CharacterName = "Player1",
    Action = CombatActionType.Attack
});

if (result.Success) {
    Console.WriteLine($"Hit for {result.Damage} damage! Enemy HP: {result.EnemyHealth}");
}
```

All operations return structured DTOs and pass through FluentValidation and Serilog pipeline behaviors automatically.

## Quick Start

```powershell
# Build engine libraries only
dotnet build RealmEngine.slnx

# Build everything (requires Avalonia workloads)
dotnet build Realm.Full.slnx

# Run all tests
dotnet test Realm.Full.slnx --filter "Category!=UI"

# Run engine tests only
dotnet test RealmEngine.slnx

# VS Code debug — press F5
```

## Core Systems

**Game Mechanics**
- D20 system with 6 attributes (STR/DEX/CON/INT/WIS/CHA) and derived stats
- 6 character classes with unique bonuses and progression paths
- Turn-based combat with dodge/crit/block mechanics
- Interactive level-up with attribute allocation
- Skill system enhancing combat and abilities
- Status effects (poison, stun, burning, shield, regeneration)

**Content Systems**
- Item generation with budget-fitting algorithm across multiple rarity tiers
- Tiered crafting system with quality bonuses and failure mechanics
- Material reference system for data reuse
- Quest system with objectives and rewards
- Spell system with mana management
- Shop/economy with dynamic pricing
- Procedural enemy generation with difficulty scaling

**Data & Persistence**
- Extensible JSON data files (enemies, items, abilities, spells, materials) with customizable content
- Cross-reference system for linking data across files (`@domain/path:item`)
- SQLite for testing, PostgreSQL for local development and production
- Budget-based item costing using `rarityWeight` inverse formula

**Integration**
- MediatR commands/queries for all game operations
- Event-driven architecture for game state changes
- Structured logging (Serilog) with exception tracking
- Validation pipeline (FluentValidation) for all inputs

## Testing

Comprehensive test suite across six test projects:

| Project | Scope |
|---------|-------|
| `RealmEngine.Core.Tests` | Combat, crafting, inventory, quests, character progression |
| `RealmEngine.Data.Tests` | JSON compliance, reference integrity, data validation |
| `RealmEngine.Shared.Tests` | Models, utilities, services |
| `RealmForge.Tests` | RealmForge UI and service logic |
| `RealmUnbound.Client.Tests` | Client ViewModels, services, navigation |
| `RealmUnbound.Server.Tests` | Server endpoints, game hub, services |

```powershell
# Run all non-UI tests with coverage
dotnet test Realm.Full.slnx --filter "Category!=UI" `
  --collect "XPlat Code Coverage" --settings coverage.runsettings
```

## Key Dependencies

**Core Libraries**
- MediatR 14.x+ — CQRS command/query pattern
- SQLite — Embedded database for testing
- PostgreSQL — Database for local development and production
- Newtonsoft.Json 13.0.x+ — JSON data loading
- FluentValidation 12.1.x+ — Input validation
- Serilog 4.3.x+ — Structured logging
- Polly 8.6.x+ — Resilience patterns

**Client / Server**
- Avalonia 11.2.x+ — Cross-platform UI (Client, RealmForge)
- ReactiveUI 20.1.x+ — MVVM framework
- ASP.NET Core — Server framework
- SignalR — Real-time client/server communication

**Testing**
- xUnit 2.9.x+ — Test framework
- FluentAssertions 8.8.x+ — Test assertions
- Avalonia.Headless.XUnit — Headless UI testing

## Project Structure

```
RealmEngine/
├── RealmEngine.Core/           # Game logic and MediatR handlers
│   └── Features/               # Vertical slices (Combat, Crafting, Inventory, etc.)
├── RealmEngine.Data/           # Data access and JSON loading
│   └── Data/Json/              # JSON game data files
├── RealmEngine.Shared/         # Shared models and utilities
├── RealmUnbound.Server/        # ASP.NET Core game server
├── RealmUnbound.Client/        # Avalonia desktop client
├── RealmForge/                 # JSON data editor tool
└── [Project].Tests/            # Test projects (one per library/app)
```

## Solution Files

| Solution | Projects Included | Use For |
|----------|-------------------|---------|
| `RealmEngine.slnx` | Core libraries + tests | Engine development, CI |
| `RealmUnbound.slnx` | Client + Server + tests | Multiplayer development |
| `RealmForge.slnx` | RealmForge + tests | Tooling development |
| `Realm.Full.slnx` | Everything | Local full-stack development |

## Downloads

Latest releases are published automatically on each merge to `main` and are available on the [GitHub Releases page](https://github.com/KungRaseri/RealmEngine/releases).

| Component | Tag Prefix | Artifacts |
|-----------|------------|-----------|
| **Engine Libraries** | `engine/v*` | `RealmEngine-Libraries-{version}.zip` · `RealmEngine-Data-{version}.zip` |
| **RealmForge** | `tooling/v*` | `RealmForge-Windows-{version}.zip` |
| **RealmUnbound Server** | `server/v*` | `RealmUnbound-Server-{version}.zip` |
| **RealmUnbound Client** | `client/v*` | `RealmUnbound-Client-Windows-{version}.zip` · `RealmUnbound-Client-Linux-{version}.zip` · `RealmUnbound-Client-macOS-x64-{version}.zip` · `RealmUnbound-Client-macOS-arm64-{version}.zip` |

## Documentation

Full documentation is deployed to **[GitHub Pages](https://kungraseri.github.io/RealmEngine/)** via the `docs.yml` workflow on every push to `main`.

- [Game Design Document](docs/GDD-Main.md) — Complete game specification
- [Commands and Queries Index](docs/COMMANDS_AND_QUERIES_INDEX.md) — All MediatR commands/queries
- [API Specification](docs/API_SPECIFICATION.md) — Server and engine API reference
- [JSON Standards](docs/standards/json/README.md) — JSON v5.1 data file standards
- [Implementation Status](docs/IMPLEMENTATION_STATUS.md) — Feature completion tracking
- [Roadmap](docs/ROADMAP.md) — Planned features and milestones

---

**Platform**: .NET 10.0 | **Pattern**: CQRS with MediatR | **UI**: Avalonia | **Data**: PostgreSQL + JSON
