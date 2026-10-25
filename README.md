# RealmEngine

[![.NET Version](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Documentation](https://img.shields.io/badge/docs-GitHub%20Pages-blue)](https://kungraseri.github.io/RealmEngine/)

## CI Status

| Component | Build | Coverage |
|-----------|-------|----------|
| Engine Libraries | [![CI - Engine](https://github.com/KungRaseri/RealmEngine/actions/workflows/ci-engine.yml/badge.svg)](https://github.com/KungRaseri/RealmEngine/actions/workflows/ci-engine.yml) | [![codecov (engine)](https://codecov.io/gh/KungRaseri/RealmEngine/branch/main/graph/badge.svg?flag=engine)](https://codecov.io/gh/KungRaseri/RealmEngine?flag=engine) |
| Veldrath Server | [![CI - Server](https://github.com/KungRaseri/RealmEngine/actions/workflows/ci-server.yml/badge.svg)](https://github.com/KungRaseri/RealmEngine/actions/workflows/ci-server.yml) | [![codecov (server)](https://codecov.io/gh/KungRaseri/RealmEngine/branch/main/graph/badge.svg?flag=server)](https://codecov.io/gh/KungRaseri/RealmEngine?flag=server) |
| Veldrath Web (client) | [![CI - Web](https://github.com/KungRaseri/RealmEngine/actions/workflows/ci-web.yml/badge.svg)](https://github.com/KungRaseri/RealmEngine/actions/workflows/ci-web.yml) | [![codecov (web)](https://codecov.io/gh/KungRaseri/RealmEngine/branch/main/graph/badge.svg?flag=web)](https://codecov.io/gh/KungRaseri/RealmEngine?flag=web) |
| Discord Bot | [![CI - Discord](https://github.com/KungRaseri/RealmEngine/actions/workflows/ci-discord.yml/badge.svg)](https://github.com/KungRaseri/RealmEngine/actions/workflows/ci-discord.yml) | [![codecov (discord)](https://codecov.io/gh/KungRaseri/RealmEngine/branch/main/graph/badge.svg?flag=discord)](https://codecov.io/gh/KungRaseri/RealmEngine?flag=discord) |

## Latest Releases

| Component | Latest |
|-----------|--------|
| Engine Libraries | [![Engine release](https://img.shields.io/github/v/release/KungRaseri/RealmEngine?filter=engine%2Fv*&label=engine&color=brightgreen)](https://github.com/KungRaseri/RealmEngine/releases?q=engine&expanded=true) |
| Veldrath Server | [![Server release](https://img.shields.io/github/v/release/KungRaseri/RealmEngine?filter=server%2Fv*&label=server&color=brightgreen)](https://github.com/KungRaseri/RealmEngine/releases?q=server&expanded=true) |
| Discord Bot | [![Discord release](https://img.shields.io/github/v/release/KungRaseri/RealmEngine?filter=discord%2Fv*&label=discord&color=brightgreen)](https://github.com/KungRaseri/RealmEngine/releases?q=discord&expanded=true) |

RPG backend engine implementing CQRS with MediatR for clean command/query separation. It powers **Veldrath** — an ASP.NET Core game server plus a SvelteKit app that is the game's only client and its content editor (**Foundry**).

## Architecture

**Vertical Slice + CQRS with MediatR**

### Core Libraries (`RealmEngine.slnx`)
- **RealmEngine.Core** — Game logic (combat, inventory, crafting, quests)
- **RealmEngine.Data** — EF Core persistence and content repositories (PostgreSQL; SQLite for tests)
- **RealmEngine.Shared** — Models, utilities, abstractions

### Applications
- **Veldrath.Server** — ASP.NET Core game server with SignalR hub
- **web/** — SvelteKit 3 app, the **only client and the only editing surface**: public site, auth, game, Foundry content editor, editorial, admin, and moderation

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

# Build everything (13 .NET projects)
dotnet build Realm.Full.slnx

# Run all tests
dotnet test Realm.Full.slnx

# Run engine tests only
dotnet test RealmEngine.slnx

# Run the game client (SvelteKit)
cd web; npm install; npm run dev   # → http://localhost:5173
```

The server expects Postgres on `localhost:5433` (`docker compose up postgres -d`) and the web app
reads its configuration from `web/client/.env` — see [`web/README.md`](web/README.md) and
[`docs/coolify-deployment.md`](docs/coolify-deployment.md).

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
- EF Core content catalog backed by PostgreSQL (SQLite for tests)
- Seeded content for items, enemies, abilities, quests, zones, and other content types
- Four DbContexts: application auth/ops, game state, content catalog, and editorial content
- Budget-based item costing using `rarityWeight` inverse formula

**Integration**
- MediatR commands/queries for all game operations
- Event-driven architecture for game state changes
- Structured logging (Serilog) with exception tracking
- Validation pipeline (FluentValidation) for all inputs

## Testing

Comprehensive test suite across 6 .NET test projects plus the SvelteKit web suite:

| Project | Scope |
|---------|-------|
| `RealmEngine.Core.Tests` | Combat, crafting, inventory, quests, character progression |
| `RealmEngine.Data.Tests` | Repository/persistence and content validation |
| `RealmEngine.Shared.Tests` | Models, utilities, services |
| `Veldrath.Server.Tests` | Server endpoints, game hub, services |
| `Veldrath.Discord.Tests` | Discord bot modules and embeds |
| `Veldrath.Assets.Tests` | Asset store and manifests |

The SvelteKit app is tested separately with Vitest and Playwright from `web/`.

```powershell
# Run all non-UI tests with coverage
dotnet test Realm.Full.slnx --filter "Category!=UI" `
  --collect "XPlat Code Coverage" --settings coverage.runsettings
```

## Key Dependencies

**Core Libraries**
- MediatR 12.5.x — CQRS command/query pattern
- SQLite — Embedded database for testing
- PostgreSQL — Database for local development and production
- System.Text.Json — Serialization (JSON→DB migration complete; Newtonsoft remains only in legacy server paths)
- FluentValidation 12.1.x — Input validation
- Serilog 4.4.x — Structured logging
- Polly 8.7.x — Resilience patterns

**Server / Client**
- ASP.NET Core — Server framework
- SignalR — Real-time client/server communication
- SvelteKit 3 / Svelte 5 — The game client, Foundry content editor, and web frontend (`web/`)
- Better Auth + Drizzle — Credentials, sessions, and the auth database schema

**Testing**
- xUnit 2.9.x+ — Test framework
- FluentAssertions 8.10.x — Test assertions
- Vitest + Playwright — Web unit and end-to-end tests

## Project Structure

```
RealmEngine/
├── RealmEngine.Core/                 # Game logic and MediatR handlers
│   └── Features/                     # Vertical slices (Combat, Crafting, Inventory, etc.)
├── RealmEngine.Data/                 # EF Core persistence and content repositories
├── RealmEngine.Shared/               # Shared models and utilities
├── Veldrath.Server/                  # ASP.NET Core game server
├── Veldrath.Discord/                 # Discord bot
├── Veldrath.Contracts/               # Shared DTOs and contracts
├── Veldrath.Assets/                  # Asset store and manifests
├── web/                              # SvelteKit app — the only client, plus the Foundry editor
└── [Project].Tests/                  # 6 .NET test projects (+ web Vitest/Playwright suites)
```

## Solution Files

| Solution | Projects Included | Use For |
|----------|-------------------|---------|
| `RealmEngine.slnx` | Core libraries + tests | Engine development, CI |
| `Veldrath.Server.slnx` | Server + engine + contracts + tests | Server development |
| `Veldrath.Libraries.slnx` | Assets + Contracts + engine + tests | Shared library development |
| `Veldrath.Discord.slnx` | Discord bot + engine + contracts + tests | Bot development |
| `Realm.Full.slnx` | Everything | Local full-stack development |

## Downloads

Latest releases are published automatically on each merge to `main` and are available on the [GitHub Releases page](https://github.com/KungRaseri/RealmEngine/releases).

| Component | Tag Prefix | Artifacts |
|-----------|------------|-----------|
| **Engine Libraries** | `engine/v*` | `RealmEngine-Libraries-{version}.zip` |
| **Veldrath Server** | `server/v*` | `Veldrath-Server-{version}.zip` |
| **Veldrath Discord Bot** | `discord/v*` | `Veldrath-Discord-{version}.zip` |

## Documentation

Full documentation is deployed to **[GitHub Pages](https://kungraseri.github.io/RealmEngine/)** via the `docs.yml` workflow on every push to `main`.

- [Game Design Document](https://kungraseri.github.io/RealmEngine/GDD-Main/) — Complete game specification
- [Commands and Queries Index](https://kungraseri.github.io/RealmEngine/COMMANDS_AND_QUERIES_INDEX/) — All MediatR commands/queries
- [API Specification](https://kungraseri.github.io/RealmEngine/API_SPECIFICATION/) — Server and engine API reference
- [JSON Standards](https://kungraseri.github.io/RealmEngine/standards/json/) — JSON data file standards
- [Implementation Status](https://kungraseri.github.io/RealmEngine/IMPLEMENTATION_STATUS/) — Feature completion tracking
- [Roadmap](https://kungraseri.github.io/RealmEngine/ROADMAP/) — Planned features and milestones

---

**Platform**: .NET 10.0 | **Pattern**: CQRS with MediatR | **Client & editor**: SvelteKit (web-first) | **Data**: PostgreSQL
