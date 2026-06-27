---
name: start-dev-stack
description: Start the full Veldrath development environment including Postgres database via Docker, ASP.NET Core game server, and optionally the Avalonia client. Use when you need to spin up the entire local development stack.
---

# Skill: start-dev-stack

Start the complete local development environment: Postgres database (Docker), the ASP.NET Core game server, and optionally one or more game clients.

## Usage

Invoke this skill when you need to:
- Spin up the entire local development stack from scratch
- Start everything needed for full-stack testing (DB + server + client)
- Quickly set up a complete working environment after a fresh clone

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) installed
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running
- PowerShell terminal
- Working directory: `g:/code/Veldrath`

## Steps

### 1. Start PostgreSQL via Docker

```powershell
docker compose up postgres -d
```

This starts the Postgres container defined in [`docker-compose.yml`](../../docker-compose.yml) in detached mode.

### 2. Wait for database health check

```powershell
# Poll until Postgres reports healthy
Write-Host "Waiting for Postgres...";
do {
    $status = docker compose ps postgres --format json | ConvertFrom-Json;
    Start-Sleep -Seconds 2;
} while ($status.Health -ne "healthy");
Write-Host "Postgres is healthy!";
```

Alternatively, check manually:

```powershell
docker compose ps
```

Look for `postgres` with `Status: Up` or `Status: healthy`.

### 3. Start the game server

Open a **new terminal** and run:

```powershell
dotnet run --project Veldrath.Server -- --environment Development
```

Wait until the console shows the server is listening (expected URLs: `https://localhost:5001`, `http://localhost:5000`).

### 4. (Optional) Start a game client

Open another **new terminal** and run:

```powershell
dotnet run --project Veldrath.Client
```

The client connects to the server and opens the game window.

### 5. (Optional) Start RealmFoundry

Open another **new terminal** and run:

```powershell
dotnet run --project RealmFoundry
```

The portal web app starts and is available at the URL shown in the console output.

### 6. Verify the stack

| Service | Expected URL / Status | How to Verify |
|---------|----------------------|---------------|
| PostgreSQL | `localhost:5432` | `docker compose ps` shows `healthy` |
| Game Server | `https://localhost:5001` | `curl -k https://localhost:5001/health` or check terminal |
| Game Client | GUI window | Window opens on screen |
| RealmFoundry | `https://localhost:5002` | Open in browser |

## Stopping the Stack

1. Press `Ctrl+C` in each terminal running a .NET process.
2. Stop the database:

   ```powershell
   docker compose down
   ```

   To also remove volumes (reset database):

   ```powershell
   docker compose down -v
   ```

## Notes

- **Order matters:** Postgres must be healthy before the server starts. The server will fail to connect if the database isn't ready.
- The server and client run in **separate terminal windows** — use `Ctrl+Shift+5` in VS Code to split terminals.
- For **hot reload** with auto-restart, use `dotnet watch run` instead of `dotnet run` in Steps 3–5.
- If you get port conflicts, check for other processes using ports 5432, 5000, 5001, or 5002.
- The `docker compose up` command uses [`docker-compose.yml`](../../docker-compose.yml). For production, use `docker-compose.prod.yml` (see [`docs/deployment.md`](../../docs/deployment.md)).
- Realm engine libraries (`RealmEngine.Core`, `RealmEngine.Shared`, `RealmEngine.Data`) are project-referenced — no NuGet packages needed.

## See Also

- [run-server](../run-server/SKILL.md) — Start server only
- [run-client](../run-client/SKILL.md) — Start client only
- [run-realmforge](../run-realmforge/SKILL.md) — Start content editor
- [run-realmfoundry](../run-realmfoundry/SKILL.md) — Start community portal
- [build-full](../build-full/SKILL.md) — Build the solution
- [`docker-compose.yml`](../../docker-compose.yml) — Docker Compose configuration
- [`docs/deployment.md`](../../docs/deployment.md) — Production deployment guide
