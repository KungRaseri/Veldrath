# Scripts

Utility scripts for development, versioning, releases, and data tooling.

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

Merges coverage XML outputs from `coverage-results/` and generates an HTML report under `coverage-report/`. It runs the [coverage reporter](https://git.kungraseri.cloud/codecov/codecov-report) pinned by `scripts/coverage-report.sh`; no global tool or license is required, only the .NET SDK.

```powershell
.\scripts\generate-coverage-report.ps1
```

Also wired to the **test-coverage** VS Code task (`Ctrl+Shift+B` → test-coverage).

---

## `coverage-report.sh`

Runs the coverage reporter without keeping a copy in this repository. It clones the pinned
`codecov/codecov-report` tag into `.coverage-report/<ref>/` (gitignored) and runs it with
the .NET SDK; `COVERAGE_REPORT_REPO`, `COVERAGE_REPORT_REF` and `COVERAGE_REPORT_DIR`
override the defaults. Arguments are ReportGenerator's, plus `-minimumcoverage:` for a gate.
It reads coverlet and vitest/istanbul Cobertura alike; the web job points it at
`web/client/coverage/cobertura-coverage.xml`.

```bash
bash scripts/coverage-report.sh \
  "-reports:./TestResults/*/coverage.cobertura.xml" \
  "-targetdir:./CoverageReport" \
  "-reporttypes:Html;Cobertura;MarkdownSummaryGithub;TextSummary"
```

---

## `format-json-files.py`

Re-formats all JSON files in a given directory to consistent 2-space indentation and UTF-8 without BOM. Pass the data directory as an argument. Run from the repo root.

```powershell
python scripts/format-json-files.py path/to/data
```

---

## `apply-branch-protection.sh`

Applies the merge gate for `development` and `main`: status checks required, reviews off, and
`apply_to_admins: false` — the operator, an administrator, keeps the break-glass override, and
the doc states what that costs. `development` takes pull requests only; `main` additionally
accepts a direct push from exactly one account (`PUSH_BYPASS_USERS`, default `kungraseri`), with
no team and no deploy key allowed to inherit the bypass, so a deploy-blocking commit can be
repaired without waiting on the checks. It is the executable copy of the decision in
[`docs/ci-forgejo.md`](../docs/ci-forgejo.md#branch-protection-what-can-block-a-merge) — read that
section before changing any of the values.

```bash
set -a; . ~/.config/zoo-agent/forgejo.env; set +a
export FORGEJO_SERVER_URL="$FORGEJO_URL"       # FORGEJO_TOKEN comes from the env file
scripts/apply-branch-protection.sh             # create or edit, then read the rule back and verify
scripts/apply-branch-protection.sh --check     # verify only; exits 1 when the stored rule has drifted
scripts/tests/apply-branch-protection.test.sh  # offline regression tests: no token, no network
```

Idempotent, and it fails rather than leaving a rule that differs from the one requested. `BRANCHES`,
`STATUS_CONTEXTS` and `PUSH_BYPASS_USERS` override the defaults. The offline tests stub the API
response and run in the [docs workflow](../.github/workflows/docs.yml).

---

## `forgejo-pr-comment.sh`

Posts or updates the sticky comment on a pull request — the replacement for
`marocchino/sticky-pull-request-comment`, which only speaks the GitHub API. Two modes:

- `--section <name> --title <heading>` (what CI uses): one shared coverage comment holding
  named sections. Each job replaces only its own section, deletes the superseded
  one-comment-per-job comments, and re-reads the result so two jobs finishing together cannot
  drop each other's numbers.
- `--marker '<html comment>'`: one comment per marker, replaced in place.

```bash
export FORGEJO_TOKEN=… FORGEJO_REPOSITORY=Veldrath/Veldrath
bash scripts/forgejo-pr-comment.sh --pr 42 --section dotnet \
  --title '.NET (all test projects)' --body-file CoverageReport/SummaryGithub.md
bash scripts/tests/forgejo-pr-comment.test.sh  # offline regression tests: no token, no network
```

The comment lookup must return **one integer id**. It used to print the id twice when another
comment followed the match — `awk` ran its `END` rule after `exit()` — which put a newline in
`COMMENT_ID` and made the PATCH URL `…/comments/746<newline>746`; curl answered
`(3) bad/illegal URL` and the coverage job failed on its last step. The offline tests (run in the
[docs workflow](../.github/workflows/docs.yml)) drive the real script against a fake `curl` on
PATH, so the lookup, both marker paths, the create-on-miss path and the malformed-id guard are
covered without touching Forgejo.

---

## `check-dotnet-coverage.mjs` + `coverage-floors.json`

Enforces the .NET coverage contract from the **merged** Cobertura report. Coverage is a property
of an assembly, not of the workflow that happened to run a test against it, so the input is
the merge of all six test projects' Cobertura output produced by `scripts/coverage-report.sh`
(`codecov/codecov-report`) — not any single component workflow's `CoverageReport/`. Run by [`.github/workflows/ci-coverage.yml`](../.github/workflows/ci-coverage.yml)
on every PR and push that touches .NET code.

It fails, with a non-zero exit, on:

- an assembly whose line or branch rate is below its floor in `coverage-floors.json`;
- the repo-wide total below the global floor;
- an assembly named in the floors file that is **missing** from the report — a test project that
  stopped running, so the assembly was not measured at all;
- an assembly **in** the report that has no floor — a new shipped project has to get a floor in
  the same change that adds it.

```bash
node scripts/check-dotnet-coverage.mjs \
  --report ./CoverageReport/Cobertura.xml \
  --floors scripts/coverage-floors.json \
  --suggest
```

`--floors` defaults to `scripts/coverage-floors.json` next to the script. Branch coverage is gated
only where an assembly has at least `branchFloorMinSites` branch sites, so assemblies with a
handful of branches do not get a meaningless percentage floor.

**The floors are a ratchet: they may only rise.** Never lower one to make the gate pass — a lower
floor is either a real coverage regression (fix it with tests) or a deliberate scope change, which
needs a reviewed note alongside the new number. `--suggest` names every floor that has gone stale
(the measured value is 3+ points above the floor) with the value to raise it to, so the nudge
appears in the log of the PR that caused it. The recorded target is 90% line per assembly; the
floors currently hold the measured baseline and climb towards that target as tests land.

---

## `ci-merge-gate.sh`

The whole body of [`merge-gate.yml`](../.github/workflows/merge-gate.yml), the one `pull_request`
workflow that declares **no `paths` filter**. Branch protection requires
`status_check_contexts: ["*"]`, and an empty status list reads as `pending`, so a pull request that
matches no component workflow's paths (a root dotfile, a `.slnx`, `docker-compose.dev.yml`) has to be
given at least one status. The measurement and the decision are in
[`docs/ci-forgejo.md`](../docs/ci-forgejo.md), "The always-on check that keeps `*` satisfiable".

It lints the workflow definitions and builds nothing, so it must stay offline and dependency-free:
`bash`, `git` and `awk`/`sed`/`grep` only.

```bash
bash scripts/ci-merge-gate.sh --lint         # exit 0 clean, 1 violation, 2 usage error
bash scripts/ci-merge-gate.sh --uncovered    # tracked paths no pull_request.paths pattern matches
```

`--lint` enforces L1–L4 from the docs: every workflow declares a top-level `name:`, every real
`uses:` names one of the six actions mirrored on this instance, every `pull_request` workflow
triggers on both `main` and `development`, and at least one declares neither `paths` nor
`paths-ignore`. `--uncovered` regenerates the "Files that no component workflow builds" table in
`docs/ci-forgejo.md`, using the runner's own glob semantics.

---

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
7. Creates deployment info file

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
- .NET SDK (nothing else — the reporter is fetched from its own repository)

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
