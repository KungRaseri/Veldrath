---
_layout: landing
---

# RealmEngine

RPG backend engine implementing CQRS with MediatR for clean command/query separation. Includes a multiplayer client/server (RealmUnbound) and a JSON data editor (RealmForge).

## Projects

| Project | Description |
|---------|-------------|
| **RealmEngine.Core** | Game logic — combat, crafting, inventory, quests, spells |
| **RealmEngine.Shared** | Shared models, domain entities, interfaces |
| **RealmEngine.Data** | JSON data loading, LiteDB persistence, repositories |
| **RealmUnbound.Server** | ASP.NET Core game server with SignalR hub |
| **RealmUnbound.Client** | Avalonia desktop client with ReactiveUI |
| **RealmForge** | Avalonia UI JSON game data editor |

## Quick Links

- [Game Design Document](~/docs/GDD-Main.md)
- [Architecture & CQRS](~/docs/CQRS_IMPLEMENTATION_STATUS.md)
- [Commands & Queries Index](~/docs/COMMANDS_AND_QUERIES_INDEX.md)
- [Implementation Status](~/docs/IMPLEMENTATION_STATUS.md)
- [Roadmap](~/docs/ROADMAP.md)
- [API Reference](~/api/index.md)
