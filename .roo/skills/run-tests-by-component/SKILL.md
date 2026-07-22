---
name: run-tests-by-component
description: Run tests for a specific RealmEngine/Veldrath component (engine, server, client, forge, foundry, discord, web, auth, assets, libraries) with optional coverage collection. Use when you need to test only one component instead of the full suite. Build the matching .slnx first per the build strategy in .github/instructions/build-strategy.md.
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
- Working directory: `c:/code/Veldrath`

## Parameters

| Parameter | Values | Description |
|-----------|--------|-------------|
| `component` (required) | `engine`, `server`, `client`, `forge`, `foundry`, `discord`, `web`, `auth`, `assets`, `libraries` | The component to test |
| `coverage` (optional) | `true`, `false` | Collect XPlat Code Coverage (default: `false`) |

## Component Mapping

Build the matching solution first, then run the tests. See [`.github/instructions/build-strategy.md`](../../.github/instructions/build-strategy.md) for the full decision logic.

| Component | Build Command | Test Command | Notes |
|-----------|--------------|-------------|-------|
| `engine` | `dotnet build RealmEngine.slnx` | `dotnet test RealmEngine.slnx` | Core + Shared + Data + GameClient.Core tests; 4 test projects |
| `server` | `dotnet build Veldrath.Server.slnx` | `dotnet test Veldrath.Server.slnx` | Server + engine + assets tests |
| `client` | `dotnet build Veldrath.Client.slnx` | `dotnet test Veldrath.Client.slnx --filter "Category!=UI"` | Client + engine + auth + assets + GameClient tests; skips UI tests needing a display |
| `forge` | `dotnet build RealmForge.slnx` | `dotnet test RealmForge.slnx --filter "Category!=UI"` | Forge editor tests; skips UI tests needing a display |
| `foundry` | `dotnet build RealmFoundry.slnx` | `dotnet test RealmFoundry.slnx` | Foundry portal tests; Blazor Server unit tests |
| `discord` | `dotnet build Veldrath.Discord.slnx` | `dotnet test Veldrath.Discord.slnx` | Discord bot + engine tests |
| `web` | `dotnet build Veldrath.Web.slnx` | `dotnet test Veldrath.Web.slnx` | Web portal + Server + engine + auth + assets + GameClient tests |
| `auth` | `dotnet build Veldrath.Libraries.slnx` | `dotnet test Veldrath.Libraries.slnx --filter "FullyQualifiedName~Veldrath.Auth"` | Auth library tests only |
| `assets` | `dotnet build Veldrath.Libraries.slnx` | `dotnet test Veldrath.Libraries.slnx --filter "FullyQualifiedName~Veldrath.Assets"` | Asset management tests only |
| `libraries` | `dotnet build Veldrath.Libraries.slnx` | `dotnet test Veldrath.Libraries.slnx` | Auth + Assets + GameClient.Core + GameClient.Components + engine tests |

## Steps

1. **Build the component** using the matching `.slnx` file (tests won't run if compilation fails):

   ```powershell
   # Build the smallest solution covering your component:
   dotnet build Veldrath.Server.slnx
   ```

2. **Run tests for the selected component without coverage** (using the matching `.slnx`):

   ```powershell
   dotnet test Veldrath.Server.slnx
   ```

3. **Run tests with coverage collection:**

   ```powershell
   dotnet test Veldrath.Server.slnx --collect "XPlat Code Coverage" --settings coverage.runsettings
   ```

4. **Run with a specific test filter** (e.g., by category):

   ```powershell
   dotnet test Veldrath.Server.slnx --filter "Category=Integration"
   ```

## Notes

- Each component now has a dedicated `.slnx` file that includes the source project, its test project, and all transitive dependencies. See [`.github/instructions/build-strategy.md`](../../.github/instructions/build-strategy.md) for the full mapping.
- `client` and `forge` components exclude `Category=UI` tests by default. Remove the `--filter` argument if you have a display available and want to run UI tests.
- Coverage results are written to `TestResults/` under the test project directory.
- If using `--no-build`, ensure the project is already built to avoid stale binary issues.

## See Also

- [run-all-tests](../run-all-tests/SKILL.md) — Run the full test suite
- [build-full](../build-full/SKILL.md) — Build the full solution (final verification)
- [`.github/instructions/build-strategy.md`](../../.github/instructions/build-strategy.md) — Smart build strategy
- [`coverage.runsettings`](../../coverage.runsettings) — Coverage configuration
- [`.github/agent-memory/engine-codebase.md`](../../.github/agent-memory/engine-codebase.md) — Testing gotchas and known quirks
