---
name: build-full
description: Build the complete RealmEngine/Veldrath solution (Realm.Full.slnx) covering all 30 projects including engine, server, client, tools, and tests. Use for final verification before commit/PR. For development iteration, prefer the smart build strategy in `.github/instructions/build-strategy.md`.
---

# Skill: build-full

Build the complete RealmEngine/Veldrath monorepo by compiling `Realm.Full.slnx`, which includes all 30 projects (engine, data, shared, server, client, tools, auth, contracts, assets, fonts, and all 13 test projects).

## Usage

Invoke this skill when you need to:
- Verify the entire codebase compiles without errors (final verification before commit/PR)
- Check for CS1591 XML doc comment violations across all projects
- Validate build configuration changes (Directory.Build.props, Directory.Build.targets)
- Prepare for running the full test suite

> **For development iteration**, use targeted builds instead. See [`.github/instructions/build-strategy.md`](../../.github/instructions/build-strategy.md) for the decision flowchart and quick reference table.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) installed
- PowerShell terminal
- Working directory: `g:/code/Veldrath`

## Steps

1. **Restore NuGet packages** (if packages are missing or `Directory.Packages.props` has changed):

   ```powershell
   dotnet restore Realm.Full.slnx
   ```

2. **Build the full solution:**

   ```powershell
   dotnet build Realm.Full.slnx
   ```

   This compiles all projects in parallel where possible. A successful build exits with code 0 and shows `Build succeeded.` with zero warnings and zero errors.

## Common Build Errors

| Error | Cause | Fix |
|-------|-------|-----|
| `CS1591: Missing XML comment for publicly visible type or member` | XML doc comments are enforced as compile-time errors per [`Directory.Build.targets`](../../Directory.Build.targets). | Add the missing `<summary>` (and `<param>`/`<returns>` where applicable) doc comment. The error message includes the file path and line number. |
| `NU1101: Unable to find package` | NuGet feed unreachable or package version mismatch. | Run `dotnet restore Realm.Full.slnx` first. If the issue persists, check [`Directory.Packages.props`](../../Directory.Packages.props) for version correctness. |
| `NETSDK1136: Target framework not available` | .NET 10 SDK not installed or not the active SDK. | Verify with `dotnet --version` (must show 10.x). Install from [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0). |
| `CS0118: Type expected` / `CS0246: Type or namespace not found` | A recently added project or file may be missing from the solution file or have incorrect `using`/`global using`. | Check the solution file for inclusion and verify implicit usings in [`Directory.Build.props`](../../Directory.Build.props). |

## Notes

- **CS1591 is a compile-time error, not a warning.** Never add `<NoWarn>CS1591</NoWarn>` to any `.csproj` file or to [`Directory.Build.props`](../../Directory.Build.props)/[`Directory.Build.targets`](../../Directory.Build.targets).
- The build uses central package management defined in [`Directory.Packages.props`](../../Directory.Packages.props) — all project-specific NuGet versions are ignored.
- Engine libraries (`RealmEngine.Core`, `RealmEngine.Shared`, `RealmEngine.Data`) must never take dependencies on UI frameworks (Avalonia, ASP.NET Core MVC/Razor, Blazor, etc.).
- This skill builds **all 30 projects**. For faster iteration, use the smart build strategy: build the `.slnx` matching the directory you changed (see [`.github/instructions/build-strategy.md`](../../.github/instructions/build-strategy.md)).
- Test projects have `ExcludeFromCodeCoverage` applied at the assembly level (see `Properties/AssemblyInfo.cs` in each test project).

## See Also

- [run-all-tests](../run-all-tests/SKILL.md) — Run the full test suite after a successful build
- [run-tests-by-component](../run-tests-by-component/SKILL.md) — Run tests for a specific component
- [`README.md`](../../README.md) — Project overview
- [`.github/agent-memory/engine-codebase.md`](../../.github/agent-memory/engine-codebase.md) — Codebase notes and known quirks
