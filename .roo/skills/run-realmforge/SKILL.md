---
name: run-realmforge
description: Start the RealmForge Avalonia DB content editor (ReactiveUI MVVM). Use when you need to manage game entities (actors, items, quests, zones, etc.) directly in the Postgres database.
---

# Skill: run-realmforge

Start the RealmForge DB content editor — an Avalonia desktop application built with ReactiveUI (MVVM) for managing game entities in Postgres.

## Usage

Invoke this skill when you need to:
- Edit game entities (actors, items, quests, zones, materials, recipes, etc.) in the Postgres content database
- Create or modify seed data for the game
- Preview content changes before they appear in the game
- Work with the content database directly (via EF Core migrations)

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) installed
- **PostgreSQL must be running** — start via Docker (see Steps below)
- PowerShell terminal
- Working directory: `g:/code/Veldrath`
- A display (desktop environment) is required

## Steps

1. **Ensure PostgreSQL is running:**

   ```powershell
   docker compose up postgres -d
   ```

   This starts the Postgres container defined in [`docker-compose.yml`](../../docker-compose.yml) in detached mode.

2. **(Optional) Verify database is healthy:**

   ```powershell
   docker compose ps
   ```

   Look for `postgres` with status `Up` or `healthy`.

3. **Start RealmForge:**

   ```powershell
   dotnet run --project RealmForge
   ```

## Expected Output

The RealmForge window opens on screen. The tool connects to the Postgres content database and loads available entities for editing.

## Notes

- RealmForge uses the **ReactiveUI (MVVM)** pattern, consistent with the Avalonia client. See coding conventions in [`AGENTS.md`](../../AGENTS.md) for MVVM patterns.
- The content database schema is defined in [`RealmEngine.Data`](../../RealmEngine.Data/) — see `Entities/ContentBase.cs` and related entity classes for field definitions.
- **Hot reload** is supported: use `dotnet watch run --project RealmForge` for automatic recompilation on file changes.
- RealmForge tests use **Avalonia.Headless.XUnit**. Tests tagged `[Trait("Category", "UI")]` require a display and are excluded from CI.
- The content database is separate from the game "world" database (used at runtime by `Veldrath.Server`).
- For database migrations, use the `dotnet ef` CLI tools targeting `RealmEngine.Data`.

## See Also

- [run-server](../run-server/SKILL.md) — Start the game server
- [start-dev-stack](../start-dev-stack/SKILL.md) — Start full environment
- [`docker-compose.yml`](../../docker-compose.yml) — Docker Compose configuration
- [`RealmEngine.Data/Entities/`](../../RealmEngine.Data/Entities/) — Entity model definitions
- [`.github/agent-memory/forge-foundry-sync.md`](../../.github/agent-memory/forge-foundry-sync.md) — Forge ↔ Foundry content sync notes
