---
name: run-realmfoundry
description: Start the RealmFoundry Blazor Server community content portal. Use when you need to test community content submission, curation, and review workflows alongside the game server.
---

# Skill: run-realmfoundry

Start the RealmFoundry Blazor Server web application — a community content portal for submitting, curating, and reviewing game content.

## Usage

Invoke this skill when you need to:
- Test community content submission workflows
- Review or manage user-submitted content (actors, items, quests, etc.)
- Develop or debug Blazor Server features (SignalR interactions, auth state)
- Validate Foundry ↔ Forge content sync

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) installed
- **PostgreSQL must be running** — start via Docker (see Steps below)
- **Veldrath Server should be running** — Foundry depends on server-side APIs
- PowerShell terminal
- Working directory: `g:/code/Veldrath`

## Steps

1. **Ensure PostgreSQL is running:**

   ```powershell
   docker compose up postgres -d
   ```

2. **Start the game server** (in a separate terminal):

   ```powershell
   dotnet run --project Veldrath.Server
   ```

3. **Start RealmFoundry** (in a new terminal):

   ```powershell
   dotnet run --project RealmFoundry
   ```

## Expected Output

Once started, the console shows:

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:5002
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

Open `https://localhost:5002` (or the URL shown) in a web browser.

## Notes

- RealmFoundry is a **Blazor Server** application, meaning SignalR maintains a persistent connection between browser and server.
- The application requires the **game server** (`Veldrath.Server`) to be running because it communicates with server-side APIs for content operations.
- **Hot reload** is supported: use `dotnet watch run --project RealmFoundry` for automatic recompilation on file changes.
- Auth is handled via `Veldrath.Auth` / `Veldrath.Auth.Blazor` libraries — see [`Veldrath.Auth`](../../Veldrath.Auth/) for authentication configuration.
- Content synchronization between Foundry and the RealmForge database is documented in [`.github/agent-memory/forge-foundry-sync.md`](../../.github/agent-memory/forge-foundry-sync.md).
- For production deployments, see [`docs/deployment.md`](../../docs/deployment.md).

## See Also

- [run-realmforge](../run-realmforge/SKILL.md) — Start the DB content editor
- [run-server](../run-server/SKILL.md) — Start the game server
- [start-dev-stack](../start-dev-stack/SKILL.md) — Start full environment
- [`.github/agent-memory/forge-foundry-sync.md`](../../.github/agent-memory/forge-foundry-sync.md) — Forge ↔ Foundry sync details
