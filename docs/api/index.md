# API Reference

Full API reference documentation is generated from C# XML `<summary>` doc comments and published here automatically on every push to `main`.

The three engine libraries are documented separately:

| Assembly | Description |
|----------|-------------|
| [RealmEngine.Core](RealmEngine.Core.md) | Game logic, MediatR commands/queries, combat, crafting, inventory, quest, spell, and save/load handlers |
| [RealmEngine.Shared](RealmEngine.Shared.md) | Shared models, domain entities, interfaces, and utilities consumed by both Core and Data |
| [RealmEngine.Data](RealmEngine.Data.md) | JSON data loading, LiteDB persistence, repositories, and data seeding services |
| [RealmForge](RealmForge.md) | Avalonia UI JSON data editor — ViewModels, services, and tree/editor components |
| [RealmUnbound.Client](RealmUnbound.Client.md) | Avalonia desktop client — ViewModels, navigation, SignalR connection, and authentication services |
| [RealmUnbound.Server](RealmUnbound.Server.md) | ASP.NET Core game server — endpoints, SignalR hub, EF Core identity, and game session services |

## Navigation

Each assembly page lists all public namespaces. Drill into a namespace to see the types it contains, with links back to the source on GitHub.

Use the **search bar** (top-right) to find a specific class, interface, or method by name.

## Adding Documentation

Public types and members without a `<summary>` comment produce a compiler warning (`CS1591`). Add XML doc comments to any public API:

```csharp
/// <summary>
/// Executes a single combat round between the player and the current enemy.
/// </summary>
/// <param name="action">The action the player is taking this turn.</param>
/// <returns>A <see cref="CombatResult"/> with damage dealt, health changes, and turn outcome.</returns>
public async Task<CombatResult> Handle(AttackEnemyCommand command, CancellationToken ct)
```

Documentation pages are rebuilt automatically whenever source files in `RealmEngine.Core/`, `RealmEngine.Shared/`, or `RealmEngine.Data/` change on `main`.
