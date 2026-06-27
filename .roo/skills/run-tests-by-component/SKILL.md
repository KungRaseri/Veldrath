---
name: run-tests-by-component
description: Run tests for a specific RealmEngine/Veldrath component (engine, server, client, forge, foundry, discord, web, auth, assets, shared, data) with optional coverage collection. Use when you need to test only one component instead of the full suite.
---

# Skill: run-tests-by-component

Run tests for a single component of the RealmEngine/Veldrath monorepo, with optional XPlat Code Coverage collection.

## Usage

Invoke this skill when you need to:
- Test only the component you're working on (faster than the full suite)
- Iterate quickly on a specific area of the codebase
- Generate coverage for a single component

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) installed
- PowerShell terminal
- Working directory: `g:/code/Veldrath`

## Parameters

| Parameter | Values | Description |
|-----------|--------|-------------|
| `component` (required) | `engine`, `server`, `client`, `forge`, `foundry`, `discord`, `web`, `auth`, `assets`, `shared`, `data` | The component to test |
| `coverage` (optional) | `true`, `false` | Collect XPlat Code Coverage (default: `false`) |

## Component Mapping

| Component | Command | Test Project | Notes |
|-----------|---------|-------------|-------|
| `engine` | `dotnet test RealmEngine.slnx` | Core + Shared + Data tests | Uses the engine-only solution; 3 test projects |
| `server` | `dotnet test Veldrath.Server.Tests` | Server integration tests | Uses SQLite for SQL-semantics tests |
| `client` | `dotnet test Veldrath.Client.Tests --filter "Category!=UI"` | Client Avalonia tests | Skips UI tests needing a display |
| `forge` | `dotnet test RealmForge.Tests --filter "Category!=UI"` | Forge editor tests | Skips UI tests needing a display |
| `foundry` | `dotnet test RealmFoundry.Tests` | Foundry portal tests | Blazor Server unit tests |
| `discord` | `dotnet test Veldrath.Discord.Tests` | Discord bot tests | — |
| `web` | `dotnet test Veldrath.Web.Tests` | Web portal tests | — |
| `auth` | `dotnet test Veldrath.Auth.Tests` | Auth library tests | — |
| `assets` | `dotnet test Veldrath.Assets.Tests` | Asset management tests | — |
| `shared` | `dotnet test RealmEngine.Shared.Tests` | Shared model/utility tests | — |
| `data` | `dotnet test RealmEngine.Data.Tests` | Repository/persistence tests | Uses EF Core InMemory provider |

## Steps

1. **Build the component** (tests won't run if compilation fails):

   ```powershell
   # For engine component:
   dotnet build RealmEngine.slnx

   # For any other component, build the relevant test project:
   dotnet build Veldrath.Server.Tests
   ```

2. **Run tests for the selected component without coverage:**

   ```powershell
   dotnet test Veldrath.Server.Tests
   ```

3. **Run tests with coverage collection:**

   ```powershell
   dotnet test Veldrath.Server.Tests --collect "XPlat Code Coverage" --settings coverage.runsettings
   ```

4. **Run with a specific test filter** (e.g., by category):

   ```powershell
   dotnet test Veldrath.Server.Tests --filter "Category=Integration"
   ```

## Notes

- The `engine` component uses [`RealmEngine.slnx`](../../RealmEngine.slnx) instead of `Realm.Full.slnx` for a faster, engine-only build-test loop.
- `client` and `forge` components exclude `Category=UI` tests by default. Remove the `--filter` argument if you have a display available and want to run UI tests.
- Coverage results are written to `TestResults/` under the test project directory.
- If using `--no-build`, ensure the project is already built to avoid stale binary issues.

## See Also

- [run-all-tests](../run-all-tests/SKILL.md) — Run the full test suite
- [build-full](../build-full/SKILL.md) — Build the full solution
- [`coverage.runsettings`](../../coverage.runsettings) — Coverage configuration
- [`.github/agent-memory/engine-codebase.md`](../../.github/agent-memory/engine-codebase.md) — Testing gotchas and known quirks
