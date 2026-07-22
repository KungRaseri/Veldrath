# EF Core Patterns

Scope: All projects that use Entity Framework Core — [`RealmEngine.Data`](../../RealmEngine.Data/), [`Veldrath.Server`](../../Veldrath.Server/), [`Veldrath.Web`](../../Veldrath.Web/). Repository abstractions live in [`RealmEngine.Shared`](../../RealmEngine.Shared/).

---

## DbContext Separation

The project uses **four separate DbContexts** sharing the same Postgres database but owning distinct table sets. Getting the wrong entity in the wrong context causes runtime DI failures or data corruption.

### Context Reference

| Context | Namespace | Tables | Purpose |
|---|---|---|---|
| [`ContentDbContext`](../../RealmEngine.Data/Persistence/ContentDbContext.cs) | `RealmEngine.Data.Persistence` | Weapons, Armors, Skills, Powers, Recipes, Items, Materials, LootTables, Enemies/NPCs (ActorArchetypes), Quests, Species, ActorClasses, ZoneLocations, Dialogue, Languages, Organizations, Enchantments, Backgrounds, NamePatterns, GameConfig | **Static reference data.** Immutable game content catalog seeded at startup. Read-heavy, write-rare (only during content updates via RealmForge). |
| [`GameDbContext`](../../RealmEngine.Data/Persistence/GameDbContext.cs) | `RealmEngine.Data.Persistence` | SaveGameRecords, HallOfFameEntries, InventoryRecords, HarvestableNodeRecords | **Per-player game state.** Save games, inventories, hall of fame. Changes during gameplay. Portable across clients (used by both server and standalone clients). |
| [`ApplicationDbContext`](../../Veldrath.Server/Data/ApplicationDbContext.cs) | `Veldrath.Server.Data` | Identity (AspNetUsers, AspNetRoles, etc.), Characters, RefreshTokens, Worlds, Regions, Zones, PlayerSessions, FoundrySubmissions, Announcements, GlobalStats, AdminAuditEntries, PlayerReports, PendingLinkTokens | **Server operational data.** ASP.NET Identity, server auth, server-side character records, zone/region geography, sessions, moderation. |
| [`EditorialDbContext`](../../Veldrath.Server/Data/EditorialDbContext.cs) | `Veldrath.Server.Data` | PatchNotes, LoreArticles, EditorialAnnouncements | **Editorial content.** Patch notes, lore articles, site announcements. Separated so editorial schema can be migrated independently. |

### Decision Table: Where Does My Entity Go?

| If the entity stores… | Put it in… |
|---|---|
| Game reference data that doesn't change per player (items, enemies, quests, skills, recipes, etc.) | [`ContentDbContext`](../../RealmEngine.Data/Persistence/ContentDbContext.cs) |
| Player-specific save data (save games, inventory, hall of fame) | [`GameDbContext`](../../RealmEngine.Data/Persistence/GameDbContext.cs) |
| User accounts, roles, login info | [`ApplicationDbContext`](../../Veldrath.Server/Data/ApplicationDbContext.cs) |
| Server-side character records (name, slot, attributes blob, equipment blob) | [`ApplicationDbContext`](../../Veldrath.Server/Data/ApplicationDbContext.cs) |
| Zone sessions, player positions, active connections | [`ApplicationDbContext`](../../Veldrath.Server/Data/ApplicationDbContext.cs) |
| World geography (worlds, regions, zones) | [`ApplicationDbContext`](../../Veldrath.Server/Data/ApplicationDbContext.cs) |
| Foundry community content submissions and votes | [`ApplicationDbContext`](../../Veldrath.Server/Data/ApplicationDbContext.cs) |
| Patch notes, lore articles, editorial announcements | [`EditorialDbContext`](../../Veldrath.Server/Data/EditorialDbContext.cs) |

---

## Entity Placement Rules

1. **An entity belongs to exactly ONE DbContext.** Never add the same `DbSet<T>` to multiple contexts.
2. **Content entities** (immutable reference data) → [`ContentDbContext`](../../RealmEngine.Data/Persistence/ContentDbContext.cs). Entities live in [`RealmEngine.Data/Entities/Content/`](../../RealmEngine.Data/Entities/Content/).
3. **Game state entities** (player-specific, changes during gameplay) → [`GameDbContext`](../../RealmEngine.Data/Persistence/GameDbContext.cs).
4. **Identity entities** → [`ApplicationDbContext`](../../Veldrath.Server/Data/ApplicationDbContext.cs) (inherits from `IdentityDbContext`).
5. **Editorial entities** → [`EditorialDbContext`](../../Veldrath.Server/Data/EditorialDbContext.cs).
6. **Never cross contexts** — a repository that injects `ContentDbContext` must not access `GameDbContext` tables, and vice versa.
7. **`ApplicationDbContext` reuses content schema** — it calls [`ContentModelConfiguration.Configure(builder)`](../../RealmEngine.Data/Persistence/ContentModelConfiguration.cs) in `OnModelCreating` so server-side code can query content entities via `ApplicationDbContext` when needed. The canonical content schema is defined once in `ContentModelConfiguration`.

---

## Migration Workflow

### Which Context Gets Migrations?

Each context has its own migration directory:

| Context | Migration Directory | Design-Time Factory |
|---|---|---|
| [`ApplicationDbContext`](../../Veldrath.Server/Data/ApplicationDbContext.cs) | [`Veldrath.Server/Migrations/`](../../Veldrath.Server/Migrations/) | [`ApplicationDbContextFactory`](../../Veldrath.Server/Data/ApplicationDbContextFactory.cs) |
| [`GameDbContext`](../../RealmEngine.Data/Persistence/GameDbContext.cs) | [`RealmEngine.Data/Migrations/GameDb/`](../../RealmEngine.Data/Migrations/GameDb/) | [`GameDbContextFactory`](../../RealmEngine.Data/GameDbContextFactory.cs) |
| [`ContentDbContext`](../../RealmEngine.Data/Persistence/ContentDbContext.cs) | [`RealmEngine.Data/Migrations/`](../../RealmEngine.Data/Migrations/) | [`ContentDbContextFactory`](../../RealmEngine.Data/ContentDbContextFactory.cs) |
| [`EditorialDbContext`](../../Veldrath.Server/Data/EditorialDbContext.cs) | [`Veldrath.Server/Migrations/Editorial/`](../../Veldrath.Server/Migrations/Editorial/) | *Shares ApplicationDbContextFactory pattern* |

### Creating a Migration

```powershell
# Content catalog changes
dotnet ef migrations add MigrationName --context ContentDbContext --project RealmEngine.Data --startup-project RealmEngine.Data

# Game state changes
dotnet ef migrations add MigrationName --context GameDbContext --project RealmEngine.Data --startup-project RealmEngine.Data

# Server operational changes
dotnet ef migrations add MigrationName --context ApplicationDbContext --project Veldrath.Server --startup-project Veldrath.Server

# Editorial changes
dotnet ef migrations add MigrationName --context EditorialDbContext --project Veldrath.Server --startup-project Veldrath.Server
```

### Migration Conventions

- **Naming:** PascalCase, descriptive of the change. Examples: `InitialCreate`, `AddZoneLocationConnections`, `AddLinkedAtToUserLogins`.
- **When to migrate:** After adding/modifying any entity class, `DbSet<T>`, or Fluent API configuration. Always check ALL contexts — an entity change in Content may need a `ContentDbContext` migration, but if `ApplicationDbContext` also maps that entity (via `ContentModelConfiguration`), you may need migrations for both.
- **Startup:** All three contexts (Application, Game, Content) are migrated at server startup in [`Program.cs`](../../Veldrath.Server/Program.cs) using a shared `allKnown` set to avoid `RepairStaleMigrationsAsync` false-positives.

---

## DI Registration — The `UseExternal` Pattern

### What It Is

The call `AddRealmEngineCore(p => p.UseExternal())` in [`Program.cs`](../../Veldrath.Server/Program.cs) tells the Core library to **skip all persistence-layer registrations**. The server must manually register every `IXxxRepository` and `IXxxService` it needs.

### Why It Exists

Core defines repository **interfaces** (`IQuestRepository`) in [`RealmEngine.Shared/Abstractions/`](../../RealmEngine.Shared/Abstractions/). The EF Core **implementations** (`EfCoreQuestRepository`) live in [`RealmEngine.Data/Repositories/`](../../RealmEngine.Data/Repositories/). Core doesn't know about EF Core — the consuming application wires them together.

### The Crash-If-Missing Rule

If a new Core handler injects an interface not already registered in `Program.cs`, the server will crash at startup with:

```
Unable to resolve service for type 'IFoo' while attempting to activate 'SomeHandler'.
```

### Build-Check Pattern

After adding a new handler that depends on a new repository:

```powershell
dotnet build Veldrath.Server
```

This catches missing registrations at compile/build time rather than at Docker startup.

### Where Registrations Go

In [`Veldrath.Server/Program.cs`](../../Veldrath.Server/Program.cs), grouped in two sections:

1. **Server-local repositories** (backed by `ApplicationDbContext`):
```csharp
builder.Services.AddScoped<ICharacterRepository, CharacterRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IZoneRepository, ZoneRepository>();
// ... etc.
```

2. **Content catalog repositories** (backed by `ContentDbContext`):
```csharp
builder.Services.AddScoped<IBackgroundRepository, EfCoreBackgroundRepository>();
builder.Services.AddScoped<IQuestRepository, EfCoreQuestRepository>();
builder.Services.AddScoped<IRecipeRepository, EfCoreRecipeRepository>();
// ... etc.
```

Add new registrations to the appropriate block. Use `AddScoped` for repository implementations (they depend on scoped `DbContext`).

---

## Repository Pattern

### Three-Layer Structure

| Layer | Location | Example |
|---|---|---|
| **Interface** | [`RealmEngine.Shared/Abstractions/`](../../RealmEngine.Shared/Abstractions/) | [`IQuestRepository.cs`](../../RealmEngine.Shared/Abstractions/IQuestRepository.cs) |
| **EF Core Implementation** | [`RealmEngine.Data/Repositories/`](../../RealmEngine.Data/Repositories/) | [`EfCoreQuestRepository.cs`](../../RealmEngine.Data/Repositories/EfCoreQuestRepository.cs) |
| **InMemory Implementation** | [`RealmEngine.Data/Repositories/`](../../RealmEngine.Data/Repositories/) | *(InMemory variants used for tests and standalone clients)* |

### Interface Pattern

```csharp
namespace RealmEngine.Shared.Abstractions;

/// <summary>Repository interface for reading quest catalog data.</summary>
public interface IQuestRepository
{
    /// <summary>Returns all active quests.</summary>
    Task<List<Quest>> GetAllAsync();

    /// <summary>Returns a single quest by slug.</summary>
    /// <param name="slug">The unique quest identifier.</param>
    /// <returns>The matching quest, or <see langword="null"/> if not found.</returns>
    Task<Quest?> GetBySlugAsync(string slug);
}
```

### EF Core Implementation Pattern

```csharp
namespace RealmEngine.Data.Repositories;

/// <summary>EF Core-backed repository for quest catalog data.</summary>
public class EfCoreQuestRepository(ContentDbContext db, ILogger<EfCoreQuestRepository> logger)
    : IQuestRepository
{
    /// <inheritdoc />
    public async Task<List<Quest>> GetAllAsync()
    {
        var entities = await db.Quests.AsNoTracking()
            .Where(q => q.IsActive)
            .ToListAsync();

        logger.LogDebug("Loaded {Count} quests from database", entities.Count);
        return entities.Select(MapToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<Quest?> GetBySlugAsync(string slug)
    {
        var entity = await db.Quests.AsNoTracking()
            .Where(q => q.IsActive && q.Slug == slug)
            .FirstOrDefaultAsync();

        return entity is null ? null : MapToModel(entity);
    }

    private static Quest MapToModel(Entities.Quest e) => new()
    {
        Id           = e.Slug,
        Slug         = e.Slug,
        Title        = e.DisplayName ?? e.Slug,
        DisplayName  = e.DisplayName ?? e.Slug,
        RarityWeight = e.RarityWeight,
    };
}
```

### Key Conventions

- **Use `AsNoTracking()`** for all read-only queries. Content repositories are read-only in normal operation.
- **Map EF entities to domain models** in a private `MapToModel` method. The EF entity (e.g., `RealmEngine.Data.Entities.Quest`) is NOT the domain model (e.g., `RealmEngine.Shared.Models.Quest`).
- **Inject `ILogger<T>`** and log at `Debug` level for repository operations.
- **Use primary constructors** for DI (the `class EfCoreQuestRepository(ContentDbContext db, ILogger<...> logger)` syntax).

### The `FakeXxx` / InMemory Pattern for Tests

For tests that don't need a real database, use InMemory implementations:

```csharp
// RealmEngine.Data/Repositories/InMemoryQuestRepository.cs
public class InMemoryQuestRepository : IQuestRepository
{
    private readonly List<Quest> _quests;

    public InMemoryQuestRepository(List<Quest>? seed = null)
        => _quests = seed ?? [];

    public Task<List<Quest>> GetAllAsync()
        => Task.FromResult(_quests.ToList());

    public Task<Quest?> GetBySlugAsync(string slug)
        => Task.FromResult(_quests.FirstOrDefault(q => q.Slug == slug));
}
```

Prefer `FakeXxx` / `InMemoryXxx` over mocking frameworks (Moq/NSubstitute) per the project's "no mocking frameworks by default" rule.

---

## Seed Data Conventions

### Structure

Seeders live in [`RealmEngine.Data/Seeders/`](../../RealmEngine.Data/Seeders/). Each seeder is a `static class` with a `SeedAsync` method:

```csharp
namespace RealmEngine.Data.Seeders;

/// <summary>Seeds baseline <see cref="Quest"/> rows into <see cref="ContentDbContext"/>.</summary>
public static class QuestsSeeder
{
    /// <summary>Seeds all quest rows (idempotent).</summary>
    public static async Task SeedAsync(ContentDbContext db)
    {
        var existing = await db.Quests.AsNoTracking().Select(x => x.Slug).ToHashSetAsync();
        var missing = GetAllQuests().Where(x => !existing.Contains(x.Slug)).ToList();
        if (missing.Count == 0) return;
        db.Quests.AddRange(missing);
        await db.SaveChangesAsync();
    }

    private static Quest[] GetAllQuests() => [ /* ... */ ];
}
```

### Rules

1. **Idempotent seeding** — always check if data exists before inserting. Use `AsNoTracking()` for the existence check.
2. **Use Bogus** for procedural generation where appropriate (names, descriptions, randomized stats).
3. **Seeding order matters** — entities with foreign key dependencies must be seeded before their dependents. Content seeders are called in dependency order during startup.
4. **One seeder per entity type** — don't mix Item and Quest seeding in the same file.
5. **Seeders are called during server startup** via [`CatalogInitializationService`](../../Veldrath.Server/Services/CatalogInitializationService.cs).

---

## Testing with EF Core

### Provider Selection

| Provider | When to Use | FK Enforcement | Transaction Support |
|---|---|---|---|
| **EF Core InMemory** | Most unit tests (fast, no real DB) | ❌ No | ❌ No |
| **SQLite in-memory** | Tests where SQL semantics matter (FK constraints, unique indexes, complex queries) | ✅ Yes | ✅ Yes |
| **Postgres** | Integration tests, deployment verification | ✅ Yes | ✅ Yes |

### Test DbContext Factories

Server tests use SQLite-backed factories in [`Veldrath.Server.Tests/Infrastructure/`](../../Veldrath.Server.Tests/Infrastructure/):

| Factory | Creates | Used By |
|---|---|---|
| [`TestDbContextFactory`](../../Veldrath.Server.Tests/Infrastructure/TestDbContextFactory.cs) | `ApplicationDbContext` (SQLite) | Zone, Auth, Character, GameHub tests |
| [`TestGameDbContextFactory`](../../Veldrath.Server.Tests/Infrastructure/TestGameDbContextFactory.cs) | `GameDbContext` (SQLite) | `ServerSaveGameRepositoryTests`, `ServerHallOfFameRepositoryTests` |

### Pattern

```csharp
public sealed class TestDbContextFactory : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDbContextFactory()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
    }

    public ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    public void Dispose() => _connection.Dispose();
}
```

### Critical Gotchas

- **InMemory provider does NOT enforce FK constraints.** Tests that rely on cascade deletes or FK violations MUST use SQLite.
- **Use `EnsureCreated()` not `Migrate()`** in test factories — it creates the schema from the current model without migration history.
- **SQLite cannot ORDER BY `DateTimeOffset`** columns natively. The `ApplicationDbContext` and `EditorialDbContext` automatically apply a `DateTimeOffsetToStringConverter` when running under SQLite. This is handled in `OnModelCreating`.
- **Share the SQLite connection** across contexts created by the same factory. Keep the connection open for the test's lifetime; closing it destroys the in-memory database.

---

## Entity Configuration

### Fluent API Configuration

Complex entity configuration lives in [`ContentModelConfiguration.cs`](../../RealmEngine.Data/Persistence/ContentModelConfiguration.cs). This static class is called from both `ContentDbContext` and `ApplicationDbContext` so the content schema is defined exactly once:

```csharp
public static class ContentModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<Power>(e =>
        {
            ConfigureContent(e);  // shared base configuration
            e.Property(x => x.PowerType).HasMaxLength(32).IsRequired();
            e.Property(x => x.School).HasMaxLength(32);
            e.OwnsOne(x => x.Stats, o => o.ToJson());
            e.OwnsOne(x => x.Effects, o => o.ToJson());
            e.OwnsOne(x => x.Traits, o => o.ToJson());
        });
        // ... more entities
    }
}
```

### Convention

| Scenario | Approach |
|---|---|
| Simple column constraints (max length, required) | Data annotations on entity properties |
| Complex relationships, composite keys, indexes | Fluent API in `OnModelCreating` or `ContentModelConfiguration` |
| Owned entity / JSON column | Fluent API: `e.OwnsOne(x => x.ComplexProp, o => o.ToJson())` |
| Entity-wide configuration shared across contexts | Static configuration class (`ContentModelConfiguration`) |

### JSON Column Pattern

Complex nested data that doesn't need to be queried directly is stored as JSON columns:

```csharp
e.OwnsOne(x => x.Stats, o => o.ToJson());     // Serialized as JSONB in Postgres
e.OwnsOne(x => x.Traits, o => o.ToJson());    // Serialized as JSONB in Postgres
```

This avoids explosion of join tables for properties that are always loaded/saved together. Use for: entity stats blocks, trait bags, effects lists, reward arrays.

---

## See Also

- [`Directory.Build.props`](../../Directory.Build.props) — Global usings and framework settings
- [`RealmEngine.Data/Persistence/ContentDbContext.cs`](../../RealmEngine.Data/Persistence/ContentDbContext.cs) — Content catalog context
- [`RealmEngine.Data/Persistence/GameDbContext.cs`](../../RealmEngine.Data/Persistence/GameDbContext.cs) — Game state context
- [`Veldrath.Server/Data/ApplicationDbContext.cs`](../../Veldrath.Server/Data/ApplicationDbContext.cs) — Server operational context
- [`Veldrath.Server/Data/EditorialDbContext.cs`](../../Veldrath.Server/Data/EditorialDbContext.cs) — Editorial content context
- [`Veldrath.Server/Program.cs`](../../Veldrath.Server/Program.cs) — DI registrations and startup migration
- [`.github/agent-memory/unbound-memory.md`](../agent-memory/unbound-memory.md) — Session log with DbContext separation history
- [`.github/agent-memory/engine-codebase.md`](../agent-memory/engine-codebase.md) — Testing gotchas and EF Core quirks
