---
name: run-all-tests
description: Run the complete RealmEngine/Veldrath test suite (~8,500+ tests across 11 test projects) with XPlat Code Coverage collection. Use when you need to verify all tests pass before committing or merging.
---

# Skill: run-all-tests

Run all tests across the entire RealmEngine/Veldrath monorepo (~8,500+ tests in 11 test projects) with code coverage collection.

## Usage

Invoke this skill when you need to:
- Verify all tests pass before committing, pushing, or merging
- Generate code coverage reports for CI/CD or local analysis
- Validate that recent changes don't break existing functionality

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) installed
- PowerShell terminal
- Working directory: `g:/code/Veldrath`
- Solution must build successfully first (see [build-full](../build-full/SKILL.md))

## Steps

1. **Ensure the solution builds** (tests won't run if compilation fails):

   ```powershell
   dotnet build Realm.Full.slnx
   ```

2. **Run all tests with coverage collection:**

   ```powershell
   dotnet test Realm.Full.slnx --collect "XPlat Code Coverage" --settings coverage.runsettings
   ```

   This runs tests from all 11 test projects and collects coverage data per component.

3. **Review results:**
   - Test results stream to the console in real time.
   - A summary line at the end shows: `Passed! - Failed: 0, Passed: ~8500, Skipped: 0, Total: ~8500, Duration: ~Xm Ys`
   - Coverage results are written to `TestResults/` directories under each test project. The `coverage.runsettings` file controls which assemblies and methods are included/excluded.

## Test Project Breakdown

| Test Project | Tests | Coverage Flag |
|---|---|---|
| `RealmEngine.Core.Tests` | Feature/unit tests for engine core | `engine` |
| `RealmEngine.Shared.Tests` | Shared model/utility tests | `engine` |
| `RealmEngine.Data.Tests` | Repository/persistence tests | `engine` |
| `Veldrath.Server.Tests` | Server integration tests | `server` |
| `Veldrath.Client.Tests` | Client UI tests (Avalonia headless) | `client` |
| `RealmForge.Tests` | Forge editor tests | `forge` |
| `RealmFoundry.Tests` | Foundry portal tests | `foundry` |
| `Veldrath.Discord.Tests` | Discord bot tests | `discord` |
| `Veldrath.Web.Tests` | Web portal tests | `web` |
| `Veldrath.Auth.Tests` | Auth library tests | — |
| `Veldrath.Assets.Tests` | Asset management tests | — |

## Notes

- **Client and Forge UI tests** marked with `Category=UI` require a real display and are skipped in headless CI. This is configured per-project in `.csproj` files.
- **`ExcludeFromCodeCoverage`** is applied at the assembly level in each test project's `Properties/AssemblyInfo.cs` — test projects themselves are excluded from coverage.
- **Coverage runsettings** are configured in [`coverage.runsettings`](../../coverage.runsettings) — modify this file to adjust exclusion rules or threshold requirements.
- **EF Core test projects** use the `InMemory` provider by default. `Veldrath.Server.Tests` uses SQLite for integration tests where SQL semantics matter.
- If you encounter `dotnet test` hanging, add `--no-build` if the build hasn't changed, or use `-v n` (normal verbosity) for more detail.

## See Also

- [build-full](../build-full/SKILL.md) — Build the solution before running tests
- [run-tests-by-component](../run-tests-by-component/SKILL.md) — Run tests for a single component
- [`coverage.runsettings`](../../coverage.runsettings) — Coverage configuration
- [`.github/agent-memory/engine-codebase.md`](../../.github/agent-memory/engine-codebase.md) — Codebase notes with testing gotchas
