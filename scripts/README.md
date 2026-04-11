# Scripts

Utility scripts for development, versioning, releases, and data tooling.

---

## `download-assets.ps1`

Downloads the latest GameAssets release from the private `KungRaseri/Veldrath-Assets` GitHub repository and extracts it into `Veldrath.Assets/GameAssets/`. Run this after cloning instead of (or in addition to) `sync-assets.ps1` when you have the `ASSETS_TOKEN` PAT available.

Requires a fine-grained PAT with **Contents: Read** on `KungRaseri/Veldrath-Assets`. Set it as the `ASSETS_TOKEN` environment variable or pass it with `-Token`.

```powershell
.\scripts\download-assets.ps1                        # uses $env:ASSETS_TOKEN
.\scripts\download-assets.ps1 -Token ghp_xxxxx       # explicit token
.\scripts\download-assets.ps1 -Force                 # overwrite without prompting
```

Also available as the **download-assets** VS Code task (`Ctrl+Shift+B` → download-assets).

---

## `generate-version.ps1`

Reads the version from `versions/<component>.props` and the current git commit count to produce a deterministic version string.

```powershell
.\scripts\generate-version.ps1 -OutputFormat string         # e.g. 0.1.42-abc1234
.\scripts\generate-version.ps1 -OutputFormat msbuild        # MSBuild property args
.\scripts\generate-version.ps1 -PropsFile versions\engine.props -OutputFormat string
```

---

## `create-release-tag.ps1`

Creates and pushes a release tag for a specific component. Use this to manually trigger a release without waiting for CI.

```powershell
.\scripts\create-release-tag.ps1 -Component engine
.\scripts\create-release-tag.ps1 -Component tooling
.\scripts\create-release-tag.ps1 -Component server
.\scripts\create-release-tag.ps1 -Component client
.\scripts\create-release-tag.ps1 -Component engine -DryRun   # preview only
```

---

## `generate-coverage-report.ps1`

Merges coverage XML outputs from `coverage-results/` and generates an HTML report under `coverage-report/`. Requires `reportgenerator` to be installed (`dotnet tool install -g dotnet-reportgenerator-globaltool`).

```powershell
.\scripts\generate-coverage-report.ps1
```

Also wired to the **test-coverage** VS Code task (`Ctrl+Shift+B` → test-coverage).

---

## `format-json-files.py`

Re-formats all JSON files in a given directory to consistent 2-space indentation and UTF-8 without BOM. Pass the data directory as an argument. Run from the repo root.

```powershell
python scripts/format-json-files.py path/to/data
```


## Available Scripts

### `build-game-package.ps1`

**Purpose**: Builds and packages all game components for distribution or Godot integration.

**Usage**:
```powershell
.\build-game-package.ps1 [-Configuration Release|Debug]
```

**Output**: Creates `package/` folder in repo root with:
- `Libraries/` - RealmEngine.Core, RealmEngine.Shared, RealmEngine.Data DLLs
- `ContentBuilder/` - WPF JSON editor application
- `Data/` - game data files
- `package-manifest.json` - Build metadata

**Parameters**:
- `Configuration` - Build configuration (default: Release)

---

### `deploy-to-godot.ps1`

**Purpose**: Deploys packaged game files to a Godot project directory with automatic API changelog generation.

**Usage**:
```powershell
.\deploy-to-godot.ps1 -GodotProjectPath "C:\path\to\godot-project"
```

**What it does**:
1. **Analyzes XML documentation changes** (compares old vs new)
2. **Generates API changelog** (CHANGELOG_API.md) with:
   - Added methods/properties/types
   - Removed API members
   - Modified documentation
3. Validates package exists
4. Validates Godot project (checks for project.godot)
5. Copies Libraries/ to Godot project
6. Copies Data/ to Godot project
7. Optionally copies RealmForge
8. Creates deployment info file

**Automatic Changelog Features**:
- Detects new, removed, and modified XML documentation
- Parses API members (Types, Methods, Properties, Fields, Events)
- Prepends changes to existing CHANGELOG_API.md
- No manual tracking required!

**Parameters**:
- `GodotProjectPath` (required) - Path to Godot project root
- `PackagePath` (optional) - Path to package folder (default: ..\package)

**Example Output**:
```
Analyzing XML documentation changes...
  • Changes detected in RealmEngine.Core.xml
    → Added: 5 | Removed: 0 | Modified: 3
  • New file: RealmEngine.Shared.xml
✓ Changelog generated: CHANGELOG_API.md
  → 2 file(s) with changes documented
```

---

### `view-api-changes.ps1`

**Purpose**: View recent API changes from the auto-generated changelog.

**Usage**:
```powershell
.\view-api-changes.ps1 -GodotProjectPath "C:\path\to\godot-project" [-Entries 3]
```

**What it does**:
- Reads CHANGELOG_API.md from Godot project
- Shows most recent N deployment changes
- Color-coded output (Added=Green, Removed=Red, Modified=Cyan)
- Shows API member details (Methods, Properties, Types, etc.)

**Parameters**:
- `GodotProjectPath` (required) - Path to Godot project root
- `Entries` (optional) - Number of recent deployments to show (default: 3)

**Example Output**:
```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Deployment #1 - 2026-01-03 17:30:00
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

  📄 RealmEngine.Core.xml
    ➕ Added (5):
      [Method] GemGenerator.Generate
      [Method] EssenceGenerator.Generate
      [Property] SocketSlot.SocketType
    📝 Modified (3):
      [Method] ItemGenerator.Generate (documentation updated)
```

---

### `generate-coverage-report.ps1`

**Purpose**: Generates code coverage reports for test projects.

**Usage**:
```powershell
.\generate-coverage-report.ps1
```

**Requirements**:
- ReportGenerator tool (`dotnet tool install -g dotnet-reportgenerator-globaltool`)

**Output**: Creates `coverage-report/` with HTML coverage reports.

---

## Workflow

### Standard Development Build
```powershell
# 1. Build the package
.\build-game-package.ps1

# 2. Deploy to Godot project
.\deploy-to-godot.ps1 -GodotProjectPath "C:\path\to\godot-project"
```

### CI/CD Build

The GitHub Actions workflow (`.github/workflows/build-and-release.yml`) automatically:
1. Runs all tests
2. Builds package
3. Creates release artifacts on tagged commits

---

## Package Structure

After running `build-game-package.ps1`, the package structure is:

```
package/
├── Libraries/
│   ├── RealmEngine.Core/      [74 DLLs]
│   ├── RealmEngine.Shared/    [7 DLLs]
│   └── RealmEngine.Data/      [15 DLLs]
├── ContentBuilder/     [232 files - WPF app]
├── Data/
│   └── Json/           [186 JSON files]
└── package-manifest.json
```

---

## Notes

- Package output is gitignored (configured in `.gitignore`)
- ContentBuilder automatically uses package Data/ folder when deployed
- JSON data is deduplicated (not copied to ContentBuilder folder)
- All scripts require PowerShell 7+
