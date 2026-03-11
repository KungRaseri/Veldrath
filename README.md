# RealmEngine

## CI Status

| Component | Build | Coverage |
|-----------|-------|----------|
| Engine Libraries | [![CI - Engine](https://github.com/KungRaseri/RealmEngine/actions/workflows/ci-engine.yml/badge.svg)](https://github.com/KungRaseri/RealmEngine/actions/workflows/ci-engine.yml) | [![codecov (engine)](https://codecov.io/gh/KungRaseri/RealmEngine/branch/main/graph/badge.svg?flag=engine)](https://codecov.io/gh/KungRaseri/RealmEngine?flag=engine) |
| RealmForge Tooling | [![CI - Tooling](https://github.com/KungRaseri/RealmEngine/actions/workflows/ci-tooling.yml/badge.svg)](https://github.com/KungRaseri/RealmEngine/actions/workflows/ci-tooling.yml) | [![codecov (forge)](https://codecov.io/gh/KungRaseri/RealmEngine/branch/main/graph/badge.svg?flag=forge)](https://codecov.io/gh/KungRaseri/RealmEngine?flag=forge) |
| RealmUnbound Client | [![CI - Client](https://github.com/KungRaseri/RealmEngine/actions/workflows/ci-client.yml/badge.svg)](https://github.com/KungRaseri/RealmEngine/actions/workflows/ci-client.yml) | [![codecov (client)](https://codecov.io/gh/KungRaseri/RealmEngine/branch/main/graph/badge.svg?flag=client)](https://codecov.io/gh/KungRaseri/RealmEngine?flag=client) |
| RealmUnbound Server | [![CI - Server](https://github.com/KungRaseri/RealmEngine/actions/workflows/ci-server.yml/badge.svg)](https://github.com/KungRaseri/RealmEngine/actions/workflows/ci-server.yml) | [![codecov (server)](https://codecov.io/gh/KungRaseri/RealmEngine/branch/main/graph/badge.svg?flag=server)](https://codecov.io/gh/KungRaseri/RealmEngine?flag=server) |

[![.NET Version](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Documentation](https://img.shields.io/badge/docs-GitHub%20Pages-blue)](https://kungraseri.github.io/RealmEngine/)

RPG backend engine designed as a game logic API for Godot UI integration. Implements CQRS pattern with MediatR for clean command/query separation. Includes a multiplayer client/server (RealmUnbound) and a JSON data editor (RealmForge).

## Architecture

**Vertical Slice + CQRS with MediatR**

### Core Libraries (`RealmEngine.slnx`)
- **RealmEngine.Core** — Game logic (combat, inventory, crafting, quests)
- **RealmEngine.Data** — JSON data loading, persistence (LiteDB)
- **RealmEngine.Shared** — Models, utilities, abstractions

### Applications
- **RealmUnbound.Server** — ASP.NET Core game server with SignalR hub
- **RealmUnbound.Client** — Avalonia UI desktop client with ReactiveUI
- **RealmForge** — Avalonia UI tool for editing JSON game data

Code is organized by business feature (Features/Combat, Features/Inventory, etc.) with automatic validation and logging via MediatR pipeline behaviors.

## Integration Pattern

**Backend API for Godot UI** — This repository contains zero production UI code.

```csharp
// Godot calls backend via MediatR
var result = await mediator.Send(new AttackEnemyCommand
{
    CharacterName = "Player1",
    Action = CombatActionType.Attack
});

// Godot receives DTO and updates UI
if (result.Success) {
    UpdateHealthBar(result.PlayerHealth);
    ShowDamageNumber(result.Damage);
}
```

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
- Skill system (8 skills enhancing combat/abilities)
- Status effects (poison, stun, burning, shield, regeneration)

**Content Systems**
- Item generation with budget-fitting algorithm (5 rarity tiers)
- Tiered crafting system with quality bonuses and failure mechanics
- Material reference system (JSON v5.1) for data reuse
- Quest system with objectives and rewards
- Spell system with mana management
- Shop/economy with dynamic pricing
- Procedural enemy generation with difficulty scaling

**Data & Persistence**
- 211 JSON data files (enemies, items, abilities, spells, materials)
- JSON v5.1 standards with cross-reference system (`@domain/path:item`)
- LiteDB save/load with auto-save support
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
- MediatR 14.0.0 — CQRS command/query pattern
- LiteDB 5.0.21 — NoSQL persistence
- Newtonsoft.Json 13.0.4 — JSON data loading
- FluentValidation 12.1.1 — Input validation
- Serilog 4.3.0 — Structured logging
- Polly 8.6.5 — Resilience patterns

**Client / Server**
- Avalonia 11.2.3 — Cross-platform UI (Client, RealmForge)
- ReactiveUI 20.1.1 — MVVM framework
- ASP.NET Core — Server framework
- SignalR — Real-time client/server communication

**Testing**
- xUnit 2.9.3 — Test framework
- FluentAssertions 8.8.0 — Test assertions
- Avalonia.Headless.XUnit — Headless UI testing

## Project Structure

```
RealmEngine/
├── RealmEngine.Core/           # Game logic and MediatR handlers
│   └── Features/               # Vertical slices (Combat, Crafting, Inventory, etc.)
├── RealmEngine.Data/           # Data access and JSON loading
│   └── Data/Json/              # 211 game data files
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

## Documentation

Full documentation is deployed to **[GitHub Pages](https://kungraseri.github.io/RealmEngine/)**.

- [Game Design Document](docs/GDD-Main.md) — Complete game specification
- [Commands and Queries Index](docs/COMMANDS_AND_QUERIES_INDEX.md) — All MediatR commands/queries
- [API Specification](docs/API_SPECIFICATION.md) — Backend API for Godot integration
- [JSON Standards](docs/standards/json/README.md) — JSON v5.1 data file standards
- [Implementation Status](docs/IMPLEMENTATION_STATUS.md) — Feature completion tracking
- [Roadmap](docs/ROADMAP.md) — Planned features and milestones

---

**Platform**: .NET 10.0 | **Pattern**: CQRS with MediatR | **UI**: Godot (separate) + Avalonia | **Data**: LiteDB + JSON


RPG backend engine designed as a game logic API for Godot UI integration. Implements CQRS pattern with MediatR for clean command/query separation.

## Architecture

**Vertical Slice + CQRS with MediatR**

Three core libraries:
- **RealmEngine.Core** - Game logic (combat, inventory, crafting, quests)
- **RealmEngine.Data** - JSON data loading, persistence (LiteDB)
- **RealmEngine.Shared** - Models, utilities, abstractions

**RealmForge** - Separate MAUI tool for editing JSON game data (on hold).

Code organized by business feature (Features/Combat, Features/Inventory, etc.) with automatic validation and logging via MediatR pipeline behaviors.

## Integration Pattern

**Backend API for Godot UI** - This repository contains zero production UI code.

```csharp
// Godot calls backend via MediatR
var result = await mediator.Send(new AttackEnemyCommand 
{ 
    CharacterName = "Player1",
    Action = CombatActionType.Attack 
});

// Godot receives DTO and updates UI
if (result.Success) {
    UpdateHealthBar(result.PlayerHealth);
    ShowDamageNumber(result.Damage);
}
```

## Quick Start

```powershell
# Build engine (no MAUI required)
dotnet build RealmEngine.sln

# Run test suite
dotnet test RealmEngine.sln

# VS Code debug
Press F5
```

## Core Systems

**Game Mechanics**
- D20 system with 6 attributes (STR/DEX/CON/INT/WIS/CHA) and derived stats
- 6 character classes with unique bonuses and progression paths
- Turn-based combat with dodge/crit/block mechanics
- Interactive level-up with attribute allocation
- Skill system (8 skills enhancing combat/abilities)
- Status effects (poison, stun, burning, shield, regeneration)

**Content Systems**
- Item generation with budget-fitting algorithm (5 rarity tiers)
- Tiered crafting system with quality bonuses and failure mechanics
- Material reference system (JSON v4.1) for data reuse
- Quest system with objectives and rewards
- Spell system with mana management
- Shop/economy with dynamic pricing
- Procedural enemy generation with difficulty scaling

**Data & Persistence**
- 211 JSON data files (enemies, items, abilities, spells, materials)
- JSON v5.1 standards with cross-reference system (`@domain/path:item`)
- LiteDB save/load with auto-save support
- Budget-based item costing using `rarityWeight` inverse formula

**Integration**
- MediatR commands/queries for all game operations
- Event-driven architecture for game state changes
- Structured logging (Serilog) with exception tracking
- Validation pipeline (FluentValidation) for all inputs

## Testing

Comprehensive test suite across three test projects:

- **RealmEngine.Data.Tests** - JSON compliance, reference integrity, data validation
- **RealmEngine.Core.Tests** - Combat, crafting, inventory, quests, character progression
- **RealmEngine.Shared.Tests** - Models, utilities, services

```powershell
# Run all tests
dotnet test RealmEngine.sln

# Run specific test projects
dotnet test RealmEngine.Data.Tests
dotnet test RealmEngine.Core.Tests
dotnet test RealmEngine.Shared.Tests
```

## Key Dependencies

**Core Libraries**
- MediatR 14.0.0 - CQRS command/query pattern
- LiteDB 5.0.21 - NoSQL persistence
- Newtonsoft.Json 13.0.4 - JSON data loading
- FluentValidation 12.1.1 - Input validation
- Serilog 4.3.0 - Structured logging
- Polly 8.6.5 - Resilience patterns

**Utilities**
- Bogus 35.6.5 - Procedural content generation
- Humanizer 3.0.1 - Natural language formatting

**Testing**
- xUnit 2.9.3 - Test framework
- FluentAssertions 8.8.0 - Test assertions

## Project Structure

```
RealmEngine/
├── RealmEngine.Core/           # Game logic and MediatR handlers
│   ├── Features/               # Vertical slices (Combat, Crafting, Inventory, etc.)
│   ├── Generators/             # Procedural content generation
│   └── Services/               # Core game services
├── RealmEngine.Data/           # Data access and JSON loading
│   ├── Data/Json/              # Game data files
│   └── Services/               # Data services and repositories
├── RealmEngine.Shared/         # Shared models and utilities
│   ├── Models/                 # Domain models
│   └── Abstractions/           # Interfaces and base classes
└── [Project].Tests/            # Test projects
```

## Solution Files

- **RealmEngine.sln** - Core engine only (no MAUI dependencies, CI/CD builds)
- **RealmForge.sln** - MAUI data editor tool (requires MAUI workloads)
- **RealmEngine.Full.sln** - All projects for local development

## Documentation

- [Game Design Document](docs/GDD-Main.md) - Complete game specification
- [Commands and Queries Index](docs/COMMANDS_AND_QUERIES_INDEX.md) - All MediatR commands/queries
- [API Specification](docs/API_SPECIFICATION.md) - Backend API for Godot integration
- [JSON Standards](docs/standards/json/README.md) - JSON v5.1 data file standards
- [Implementation Status](docs/IMPLEMENTATION_STATUS.md) - Feature completion tracking
- [Documentation Index](docs/README.md) - Complete documentation list

---

**Platform**: .NET 9.0 | **Pattern**: CQRS with MediatR | **UI**: Godot (separate) | **Data**: LiteDB + JSON
