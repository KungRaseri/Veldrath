---
name: run-server
description: Start the Veldrath ASP.NET Core game server with SignalR hub. Use when you need to launch the backend for local multiplayer development or testing.
---

# Skill: run-server

Start the Veldrath ASP.NET Core game server, which hosts the SignalR hub for real-time multiplayer communication.

## Usage

Invoke this skill when you need to:
- Launch the backend server for local development
- Test server-side features (auth, game state, SignalR endpoints)
- Run the server alongside the client for local multiplayer testing

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) installed
- **PostgreSQL must be running** — start via Docker (see Steps below)
- PowerShell terminal
- Working directory: `g:/code/Veldrath`

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

3. **Start the server:**

   ```powershell
   dotnet run --project Veldrath.Server
   ```

4. **(Optional) Specify the ASP.NET environment:**

   ```powershell
   dotnet run --project Veldrath.Server -- --environment Development
   ```

   Valid environments: `Development` (default), `Staging`, `Production`.

## Expected Output

Once the server starts successfully, you'll see output similar to:

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:5001
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

## Stopping the Server

Press `Ctrl+C` in the terminal where the server is running, or close the terminal.

For clean shutdown of the database:

```powershell
docker compose down
```

## Notes

- The server uses **SignalR** for real-time communication. Clients connect to the SignalR hub at `/hub/game` (or the path configured in `Veldrath.Server`).
- **Hot reload** is supported: use `dotnet watch run --project Veldrath.Server` for automatic recompilation on file changes.
- Configuration is loaded from `Veldrath.Server/appsettings*.json`, environment variables, and user secrets.
- The server references engine libraries via project references — no separate NuGet feed is needed.
- For production deployments, see [`docs/deployment.md`](../../docs/deployment.md).

## See Also

- [run-client](../run-client/SKILL.md) — Start the Avalonia game client
- [start-dev-stack](../start-dev-stack/SKILL.md) — Start full environment (DB + server + clients)
- [`docker-compose.yml`](../../docker-compose.yml) — Docker Compose configuration
- [`.github/agent-memory/unbound-memory.md`](../../.github/agent-memory/unbound-memory.md) — Server hub architecture and session notes
