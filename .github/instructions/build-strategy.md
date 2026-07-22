# Build Strategy

## Core Principle

**Build the smallest solution that fully verifies your changes.** Never default to `Realm.Full.slnx` unless changes span multiple components or touch shared build infrastructure.

## Decision Flowchart

1. How many unique top-level project directories did you change?
   - **1 directory** → Go to step 2
   - **2+ directories** → Go to step 3

2. Which directory?
   - `RealmEngine.Core/`, `RealmEngine.Shared/`, `RealmEngine.Data/`, `RealmEngine.*.Tests/`, `Veldrath.GameClient.Core/` → Build `RealmEngine.slnx`
   - `RealmForge/`, `RealmForge.Tests/` → Build `RealmForge.slnx`
   - `RealmFoundry/`, `RealmFoundry.Tests/` → Build `RealmFoundry.slnx`
   - `Veldrath.Web/`, `Veldrath.Web.Tests/` → Build `Veldrath.Web.slnx`
   - `Veldrath.Client/`, `Veldrath.Client.Tests/` → Build `Veldrath.Client.slnx`
   - `Veldrath.Server/`, `Veldrath.Server.Tests/` → Build `Veldrath.Server.slnx`
   - `Veldrath.Discord/`, `Veldrath.Discord.Tests/` → Build `Veldrath.Discord.slnx`
   - `Veldrath.Auth/`, `Veldrath.Auth.Blazor/`, `Veldrath.Auth.Tests/`, `Veldrath.Assets/`, `Veldrath.Assets.Tests/`, `Veldrath.Contracts/` → Build `Veldrath.Libraries.slnx`
   - `Veldrath.GameClient.Components/`, `Veldrath.GameClient.Components.Tests/` → Build `RealmForge.slnx` (smallest solution containing it)
   - `RealmUI.Fonts/` → Build `RealmForge.slnx` (smallest solution containing it)

3. Are all changed directories covered by a SINGLE existing sub-solution?
   - **Yes** → Build that sub-solution
   - **No** → Changes are cross-cutting → Build `Realm.Full.slnx`

4. **ALWAYS build `Realm.Full.slnx` for:**
   - Changes to `Directory.Build.props`
   - Changes to `Directory.Build.targets`
   - Changes to `Directory.Packages.props`
   - Changes to `coverage.runsettings`
   - Changes spanning 2+ sub-solutions that don't share a parent solution

## When NOT to Build

| File Type | Action | Reason |
|---|---|---|
| `.razor` files when `dotnet watch` is running | Skip build | Blazor hot reload handles recompilation |
| `.css` files | Skip build | Static content |
| `.js` files (unless part of a build pipeline) | Skip build | Static content |
| `.md` files | Skip build | Documentation only |
| `.json` config files (non-embedded) | Skip build | No compilation impact |
| `.cs` files | **Always build** | Requires compilation |
| `.csproj` files | **Always build** | May change dependencies |
| `.props` / `.targets` files | **Always build full** | Affects every project |

## Quick Reference

| Changed Path | Build Command |
|---|---|
| `RealmEngine.*/**` | `dotnet build RealmEngine.slnx` |
| `RealmForge/**` or `RealmForge.Tests/**` | `dotnet build RealmForge.slnx` |
| `RealmFoundry/**` or `RealmFoundry.Tests/**` | `dotnet build RealmFoundry.slnx` |
| `Veldrath.Web/**` or `Veldrath.Web.Tests/**` | `dotnet build Veldrath.Web.slnx` |
| `Veldrath.Client/**` or `Veldrath.Client.Tests/**` | `dotnet build Veldrath.Client.slnx` |
| `Veldrath.Server/**` or `Veldrath.Server.Tests/**` | `dotnet build Veldrath.Server.slnx` |
| `Veldrath.Discord/**` or `Veldrath.Discord.Tests/**` | `dotnet build Veldrath.Discord.slnx` |
| `Veldrath.Auth/**`, `Veldrath.Assets/**`, `Veldrath.Contracts/**` | `dotnet build Veldrath.Libraries.slnx` |
| `Veldrath.GameClient.Components/**` | `dotnet build RealmForge.slnx` |
| `RealmUI.Fonts/**` | `dotnet build RealmForge.slnx` |
| Cross-component changes | `dotnet build Realm.Full.slnx` |
| `Directory.Build.*` / `Directory.Packages.props` | `dotnet build Realm.Full.slnx` |

## Verification Before Commit

Before committing or opening a PR, always run the full build as a final verification:
```powershell
dotnet build Realm.Full.slnx
```
