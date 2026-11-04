# .NET coverage gate — the contract

## Status

> **Historical note (2026-11-04):** the `Veldrath.Assets` project and `Veldrath.Assets.Tests` were
> deleted, along with the `Veldrath.Assets` floor in [`scripts/coverage-floors.json`](../scripts/coverage-floors.json:1).
> Its rows below (measured 46.83% line / 21.74% branch, floor 45) and the "3,999 tests" figure are
> the numbers the decision was made on, kept as the record of the arithmetic. The live tree has five
> test projects; the gate itself is unchanged.

**Decided 2026-11-03, measured on `kung-lab-hermes`, not yet implemented.** This file holds
the decisions only; the implementing cards are listed at the end. It exists because the .NET
half of the 90% coverage goal has **no gate at all**: [`coverage.runsettings`](../coverage.runsettings:1)
configures a cobertura collector and each workflow uploads a `CoverageReport/` artifact, but
nothing compares the number to a threshold — `dotnet test` is green at any coverage level.

The web half already enforces 90% globally with per-glob floors
([`web-feature-parity-and-coverage.md`](web-feature-parity-and-coverage.md)). This file decides
the equivalent for .NET, with the numbers that decision depends on.

---

## Three corrections to the card this plan answers

The card's own premises were partly wrong. They are corrected here because two of them change
the shape of the answer.

### 1. `dotnet` was never missing from this host

The card said the measurement must wait for CI or a second host. The SDK was present at
`~/.dotnet/dotnet` (10.0.403), only absent from `PATH`. That is now corrected in
[`.github/agent-memory/veldrath-memory.md`](../.github/agent-memory/veldrath-memory.md) by
`f289ac37`. **Consequence:** every number in this plan is measured locally and reproduced
against the CI-pinned SDK — no "first number comes from CI" provisional step is needed, and
none is proposed.

### 2. There are three .NET workflows, not five — and one test project has never run in CI

`.github/workflows/` contains `ci-engine.yml`, `ci-server.yml`, `ci-discord.yml`, `ci-web.yml`
and `docs.yml`. `Veldrath.Assets.Tests` and `Veldrath.Libraries.slnx` appear in **no**
workflow (grep of `.github/workflows/` returns nothing for either), even though
[`build-strategy.md`](../.github/instructions/build-strategy.md:17) treats
`Veldrath.Assets/`, `Veldrath.Assets.Tests/` and `Veldrath.Contracts/` as a shippable
component. Those 10 tests have never gated anything. **Consequence:** the coverage gate cannot
be an addition to an existing workflow — see decision D2.

### 3. No workflow runs on a pull request into `development`

Every workflow filters `pull_request: branches: [ main ]` ([`ci-engine.yml`](../.github/workflows/ci-engine.yml:28),
[`ci-server.yml`](../.github/workflows/ci-server.yml:32), [`ci-discord.yml`](../.github/workflows/ci-discord.yml:32),
[`ci-web.yml`](../.github/workflows/ci-web.yml:23)), and `pull_request.branches` filters the
**base** branch. The PRs in this repository are opened against `development` — checked against the
API, not inferred: `GET /repos/Veldrath/Veldrath/pulls/12` returns
`base.ref = "development"`, `head.ref = "wt/t_a09b5eb6"`. `origin/development` carries the merge
commits for `#10`, `#11`, `#12` from `wt/*` branches.

So the gates do not run on the PRs that merge. What does run: `push` to `main` and `development`
([`ci-engine.yml`](../.github/workflows/ci-engine.yml:14)), and the rare PR whose **base** is
`main` — which is what the earlier CI audit's "58 pull_request runs" turned out to be (all
attributed to PR #3, `base: main, head: development`). **Every existing check, including the web
coverage gate, is post-merge only.** A coverage gate with the same trigger shape would be dead on
arrival: it would report a regression after the merge that caused it. The new workflow therefore
triggers on `pull_request: branches: [ main, development ]`, and aligning the other four is carded
separately (P7), because it changes behaviour for every PR and deserves its own review.

---

## How the baseline was measured

All six test projects, both SDKs, six independent runs, merged with the same tool CI installs.
Nothing here is estimated.

```bash
export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"   # or ~/.dotnet-402 for the CI pin
dotnet restore Realm.Full.slnx
dotnet build Realm.Full.slnx -c Release --no-restore
dotnet test Realm.Full.slnx -c Release --no-build \
  --collect "XPlat Code Coverage" --settings coverage.runsettings \
  --results-directory ./TestResults
dotnet tool install --global dotnet-reportgenerator-globaltool     # 5.5.12
reportgenerator "-reports:./TestResults/*/coverage.cobertura.xml" \
  "-targetdir:./CoverageReport" "-reporttypes:Cobertura;MarkdownSummaryGithub"
```

3,999 tests pass on both SDKs (793 Shared, 2,021 Core, 301 Data, 845 Server, 10 Assets,
29 Discord). Two cross-checks were made before trusting the numbers:

- **Merge correctness.** Cobertura `<source>` roots differ per test project — `RealmEngine.Shared.Tests`
  writes `filename="Models/Item.cs"` with `source=/…/RealmEngine.Shared/`, while
  `Veldrath.Server.Tests` writes `filename="RealmEngine.Shared/Models/Item.cs"` with
  `source=/…/`. Merging on the `filename` attribute double-counts every shared file (it inflated
  `RealmEngine.Shared` from 1,484 to 2,968 lines). ReportGenerator merges on the resolved path;
  a hand-written parser must do the same. ReportGenerator's merged total was then reproduced
  exactly by an independent parser (32,069 lines, 10,450 branches), which is why
  ReportGenerator is trusted as the gate's input.
- **SDK skew.** CI pins 10.0.402 ([`ci-engine.yml`](../.github/workflows/ci-engine.yml:45)) and
  this host has 10.0.403. Both were measured: identical line and branch counts, and every assembly landed within
  0.17 points (`RealmEngine.Core` 70.73 line on 10.0.403, 70.90 on 10.0.402). The floors below
  use the **10.0.402** numbers because that is what CI runs.

### Coverage is not deterministic — this is a finding, not noise

Six runs (three solution-level, three per-project, all on 10.0.402) do not agree:

| Assembly | min line % | max line % | min branch % | max branch % | spread |
|---|---|---|---|---|---|
| `RealmEngine.Core` | 70.49 | 70.90 | 55.51 | 55.76 | 0.41 pp |
| `Veldrath.Server` | 72.42 | 72.79 | 52.88 | 53.04 | 0.38 pp |
| `RealmEngine.Data` | 92.56 | 92.56 | 67.10 | 67.10 | 0 |
| `RealmEngine.Shared` | 56.33 | 56.33 | 53.61 | 53.61 | 0 |
| `Veldrath.Contracts` | 90.83 | 90.83 | 100.00 | 100.00 | 0 |
| `Veldrath.Assets` | 46.83 | 46.83 | 21.74 | 21.74 | 0 |
| `Veldrath.Discord` | 32.74 | 32.74 | 40.79 | 40.79 | 0 |
| **TOTAL** | **75.01** | **75.28** | **55.12** | **55.27** | 0.27 pp |

Diffing two identical runs line-by-line localises it exactly: 59 lines change covered/not-covered
state, in `Veldrath.Server/Features/Characters/Combat/FleeFromCombatHubCommand.cs` (34),
`RealmEngine.Core/Features/Combat/Services/CombatService.cs` (14),
`RealmEngine.Core/Features/Combat/Commands/ApplyStatusEffectHandler.cs` (9) and
`RealmEngine.Core/Features/Exploration/Services/DungeonGeneratorService.cs` (2).

The cause is structural: **13 files call `Random.Shared` directly** and the "dice" abstraction is
barely used —

- [`DiceRoller.cs`](../RealmEngine.Core/Services/DiceRoller.cs:1) is a **static** class over
  `Random.Shared` with no injection point (`RollDiceString`, `Roll`, …), and `DiceRoller.*` is
  called from exactly one file;
- [`FleeFromCombatHubCommand.cs`](../Veldrath.Server/Features/Characters/Combat/FleeFromCombatHubCommand.cs:100)
  rolls inline: `if (Random.Shared.Next(100) < FleeSuccessChance)`.

A branch behind an unseeded RNG cannot be reached deterministically, so tests assert one arm per
run and coverage moves. That surface is 2,930 lines at 52.8% — and it is the **first** piece of
work in the plan, because it converts unreachable limbs into assertable ones.

Two consequences for the contract: floors need real headroom (a floor at `trunc(measured)` has
0.04 points of margin on `Veldrath.Server` and would fail about half of all runs), and the
invocation style does not matter — the spread is the same for `dotnet test Realm.Full.slnx` and
for six sequential `dotnet test <project>` calls, so the simpler form is specified.

---

## Decisions

### D1 — Is 90% the right target? Yes, for line coverage of every shipping assembly, but it is a destination, not a day-one gate

The card's hypothesis was that "handler/validation arms are cheap to cover, EF interceptors and
DI bootstrap are not". The measurement mostly contradicts it: the uncovered mass **is** the cheap
kind. The largest gaps are ordinary untested handler paths, not framework glue —

| File | Covered / lines | |
|---|---|---|
| [`GameHub.cs`](../Veldrath.Server/Hubs/GameHub.cs:1) | 891 / 1,990 | 44.8% — the largest file in the repo |
| [`CombatService.cs`](../RealmEngine.Core/Features/Combat/Services/CombatService.cs:1) | 243 / 629 | 38.6% |
| [`AdminEndpoints.cs`](../Veldrath.Server/Features/Admin/AdminEndpoints.cs:1) | 90 / 334 | 26.9% |
| `QuestMutationHubCommands.cs` | 0 / 176 | 0.0% |
| [`BudgetItemGenerationService.cs`](../RealmEngine.Core/Services/Budget/BudgetItemGenerationService.cs:1) | 15 / 206 | 7.2% |
| `TraitApplicator.cs` | 0 / 127 | 0.0% |
| `EfCoreInventoryService.cs` | 0 / 85 | 0.0% |

Only a minority of the gap is genuinely low-value: DI/bootstrap
(`ServiceCollectionExtensions` 57.8% line / 13.6% branch, `DotEnvConfiguration` 12.8%,
`SmtpEmailSender` 0%), data-only asset manifests (`Veldrath.Assets/Manifest/*`, 0%),
positional DTOs (`Veldrath.Contracts/Admin/AdminContracts.cs`, 58 lines, 0%) and the Godot
logging sinks (48 lines, 0%, for a client that no longer exists).

So 90% line is right for the engine and the server, and reachable. What is **not** right is
enforcing it today:

| Assembly | Lines | Covered | Line % | At 90% needs | Shortfall |
|---|---|---|---|---|---|
| `RealmEngine.Core` | 13,424 | 9,464 | 70.5% | 12,082 | **2,618** |
| `Veldrath.Server` | 9,016 | 6,529 | 72.4% | 8,115 | **1,586** |
| `RealmEngine.Shared` | 1,484 | 836 | 56.3% | 1,336 | **500** |
| `Veldrath.Discord` | 394 | 129 | 32.7% | 355 | **226** |
| `Veldrath.Assets` | 126 | 59 | 46.8% | 114 | **55** |
| `Veldrath.Contracts` | 1,145 | 1,040 | 90.8% | 1,031 | 0 (met) |
| `RealmEngine.Data` | 6,480 | 5,998 | 92.6% | 5,832 | 0 (met) |
| **TOTAL** | **32,069** | **24,055** | **75.0%** | **28,863** | **4,808** |

The per-assembly shortfalls add up to 4,985 rather than 4,808 because `RealmEngine.Data`
(+166) and `Veldrath.Contracts` (+9) are already above 90 and their surplus counts toward the
global number. A global floor alone can therefore be satisfied by one assembly's surplus
masking another's deficit — which is why D4 gates per assembly as well.

Enforcing 90% on the first day is not a gate, it is a freeze: no .NET change could merge until
~4,800 lines of tests exist. The web decision ("90% global immediately, no staged ratchet",
[`web-feature-parity-and-coverage.md`](web-feature-parity-and-coverage.md)) was affordable only
because the web started at 95.54% branches / 99.19% lines — **the gate was a ratchet on coverage
already achieved.** The same principle applied to a 75.0% / 55.1% starting point gives a
different entry condition, not a different target:

> **Enforce a no-regression ratchet at the measured baseline now, with 90% line as the recorded
> target for every assembly. Raise the floors as the tests land.**

Both metrics are gated — line is the headline, branch is the anti-gaming ratchet — but branch
gets **no separate target**. Chasing a branch number in code full of `?.` and compiler-generated
null checks invites tests written for the metric rather than for behaviour; branch only has to
rise as line rises.

### D2 — Scope: all seven shipping assemblies are gated; nothing is exempt from the line floor

| Assembly | Gated (line) | Gated (branch) | Why |
|---|---|---|---|
| `RealmEngine.Core` | yes | yes | domain logic, 5,475 branch sites |
| `RealmEngine.Data` | yes | yes | repositories + validators, 848 sites |
| `RealmEngine.Shared` | yes | yes | models + utilities, 761 sites |
| `Veldrath.Server` | yes | yes | hub, features, stores, 3,143 sites |
| `Veldrath.Contracts` | yes | **no** | 2 branch sites — a rate over 2 sites is not a signal |
| `Veldrath.Assets` | yes | **no** | 69 branch sites, under the 100-site minimum |
| `Veldrath.Discord` | yes | yes | 152 sites |

Exempt because they are already excluded in [`coverage.runsettings`](../coverage.runsettings:9),
unchanged by this plan: test assemblies (`[*.Tests]*`), `Obsolete`/`GeneratedCode`/`CompilerGenerated`
attributes, EF migrations (`**/Migrations/**/*.cs`), and entry points (`**/Program.cs`,
`**/App.axaml*`). **No assembly is exempt**: exempting the awkward ones is how a coverage gate
becomes theatre, and every assembly here is mostly testable.

The branch gate is applied per assembly only where there are **at least 100 branch sites**
(`branchFloorMinSites` in the floors file). That is a rule, not three special cases.

### D3 — Enforcement: a Node script over ReportGenerator's **merged** Cobertura, run in a new workflow

**Chosen: `scripts/check-dotnet-coverage.mjs` + `scripts/coverage-floors.json`, invoked by a new
job in `.github/workflows/ci-coverage.yml`, step `Enforce the coverage contract`.**

*Why not `coverlet.msbuild` `Threshold`/`ThresholdType`.* It enforces **one** number for the test
run that carries it, so it cannot express per-assembly floors, and it cannot see a merge. Both
matter here, measurably:

| Assembly | merged (truth) | as seen by the engine workflow | as seen by the server workflow |
|---|---|---|---|
| `RealmEngine.Data` | 92.56% | **46.48%** | 75.57% |
| `RealmEngine.Core` | 70.90% | 70.52% | **6.06%** |
| `RealmEngine.Shared` | 56.33% | 54.72% | **9.16%** |
| `Veldrath.Assets` | 46.83% | — | **0.00%** |

A threshold inside `ci-engine` would have to be 45 for `RealmEngine.Data` — hiding 47 points of
real coverage — while `ci-server`'s own report shows `RealmEngine.Core` at 6%. `ReportGenerator`
has no threshold facility at all. **Coverage is a property of an assembly, not of the job that
ran a test against it, so the gate must read the merged report and must therefore be its own
job.**

*Why a new workflow rather than a step in an existing one.* [`AGENTS.md`](../AGENTS.md:245) says
the five workflows are "the whole list, deliberately". That list is about checks that can live
inside the workflow of the component they protect; this check spans all four .NET components and
one component (`Veldrath.Assets`) that has no workflow at all (correction 2). Putting it in
`ci-engine` would either miss Discord/Assets changes or require widening `ci-engine`'s path
filter to every .NET path — which is a separate job in everything but name. The new workflow uses
**no action that is not already mirrored** (`actions/checkout@v7`, `actions/setup-node@v7`,
`actions/upload-artifact@v3`), so the allow-list in
[`docs/ci-forgejo.md`](../docs/ci-forgejo.md:56) is untouched. Cost, measured: 24 s build +
25 s tests + 2 s report = **~51 s** of runner time.

*Why Node and not a shell script.* `node scripts/check-markdown-links.mjs` already runs in this
pipeline after an explicit `actions/setup-node@v7` step ([`docs.yml`](../.github/workflows/docs.yml:88)),
so Node is proven present; `python3` is used by no workflow, and the floors table needs JSON
parsing, a numeric comparison and a formatted table on failure. `.mjs` with no dependencies means
no `npm ci` in the job.

### D4 — Floors: `trunc(lowest observed) − 1`, per assembly plus a global floor

Measured on 10.0.402 over six runs; the lowest observation is what the gate must survive.

| Assembly | Lines | Lowest line % | Floor | Lowest branch % | Floor | Target |
|---|---|---|---|---|---|---|
| `RealmEngine.Core` | 13,424 | 70.49 | **69** | 55.51 | **54** | 90 |
| `RealmEngine.Data` | 6,480 | 92.56 | **91** | 67.10 | **66** | 90 (met) |
| `RealmEngine.Shared` | 1,484 | 56.33 | **55** | 53.61 | **52** | 90 |
| `Veldrath.Server` | 9,016 | 72.42 | **71** | 52.88 | **51** | 90 |
| `Veldrath.Contracts` | 1,145 | 90.83 | **89** | — | n/a | 90 (met) |
| `Veldrath.Assets` | 126 | 46.83 | **45** | — | n/a | 90 |
| `Veldrath.Discord` | 394 | 32.74 | **31** | 40.79 | **39** | 90 |
| **TOTAL** | **32,069** | **75.01** | **74** | **55.12** | **54** | **90** |

The one-point subtraction is not caution, it is the correction for a measured ±0.4 pp spread
(table above). Every floor was checked against all six runs, not against one.

There is **no per-glob floor below the assembly level**, unlike web. .NET assemblies are already
cohesive (helpers, DTOs and manifest data are split by assembly here, not by directory), and
ReportGenerator's per-assembly rate is the finest number the report gives without a
file-selection layer that would itself need maintaining. Where web needed globs to stop
`src/routes/**` from sliding on the strength of `src/lib/game/**`, the .NET equivalent is already
the assembly boundary.

The **global floor is kept** as a second assertion because it is the number the PR comment shows
and because a per-assembly table alone cannot notice an assembly *appearing*.

### D5 — The ratchet protocol (this is what turns the floor table into a march to 90)

1. **Floors are never lowered.** A change that needs a lower floor is either a real coverage
   regression (fix it) or a deliberate scope change (a reviewer-approved note in this file and an
   updated floor in the same PR).
2. **A PR that lifts an assembly's measured coverage 3+ points above its floor raises that floor
   in the same PR.** `--suggest` prints exactly which floors are stale, so the nudge appears in
   the job log of the PR that caused it.
3. **A new shipping assembly must arrive with a floor.** The script fails when a package in the
   report has no entry in the floors file — otherwise a new project silently escapes the gate.
4. **An assembly missing from the report fails the gate.** A test project that stops running
   would otherwise pass by measuring nothing. This is the check that would have caught
   `Veldrath.Assets.Tests` never running (correction 2).

### D6 — Where each check lives after this plan

| Check | Enforced in | Blocks a merge? |
|---|---|---|
| `dotnet format --verify-no-changes` | each component workflow | post-merge only (correction 3) |
| Unit tests | each component workflow | post-merge only |
| **.NET coverage floors** | **`ci-coverage.yml`** | **PR and post-merge** |
| Web coverage floors | `ci-web.yml` (`npm run coverage`) | post-merge only |

The component workflows keep their `CoverageReport/` HTML artifacts and sticky PR comments —
they are human-facing, and this plan does not turn them into gates.

---

## The gate, as specified

### `scripts/coverage-floors.json`

```json
{
  "$comment": [
    "The .NET coverage contract. Floors are a no-regression ratchet, measured 2026-11-03 with",
    "the CI-pinned SDK 10.0.402, merged from all six test projects by ReportGenerator.",
    "Coverage is not deterministic: 13 files call Random.Shared directly, so four combat files",
    "oscillate. Six runs (three solution-level, three per-project) spanned 0.41 points on",
    "RealmEngine.Core and 0.38 on Veldrath.Server. A floor is therefore trunc(lowest observed)",
    "minus 1 point, not trunc(measured): a floor with sub-1-point headroom fails intermittently.",
    "Never lower a floor. Raise it in the PR that lifts the measured value 3+ points above it",
    "(scripts/check-dotnet-coverage.mjs --suggest names them). Target is 90% line everywhere;",
    "branch coverage carries no separate target and is gated only against regression."
  ],
  "branchFloorMinSites": 100,
  "global": { "line": 74, "branch": 54 },
  "assemblies": {
    "RealmEngine.Core":     { "line": 69, "branch": 54, "measured": { "line": 70.49, "branch": 55.51, "lines": 13424 } },
    "RealmEngine.Data":     { "line": 91, "branch": 66, "measured": { "line": 92.56, "branch": 67.10, "lines": 6480 } },
    "RealmEngine.Shared":   { "line": 55, "branch": 52, "measured": { "line": 56.33, "branch": 53.61, "lines": 1484 } },
    "Veldrath.Server":      { "line": 71, "branch": 51, "measured": { "line": 72.42, "branch": 52.88, "lines": 9016 } },
    "Veldrath.Contracts":   { "line": 89, "branch": null, "measured": { "line": 90.83, "branch": 100.0, "lines": 1145 } },
    "Veldrath.Assets":      { "line": 45, "branch": null, "measured": { "line": 46.83, "branch": 21.74, "lines": 126 } },
    "Veldrath.Discord":     { "line": 31, "branch": 39, "measured": { "line": 32.74, "branch": 40.79, "lines": 394 } }
  }
}
```

### `.github/workflows/ci-coverage.yml`

```yaml
name: CI - .NET Coverage Gate

# Forgejo-native. See the header of ci-engine.yml and docs/ci-forgejo.md before changing any
# 'uses:' line. No action here is outside the mirrored allow-list (checkout v7, setup-node v7,
# upload-artifact v3), so this workflow needs no import on the instance.
#
# This job exists because coverage is a property of an assembly, not of the job that ran a test
# against it: the engine workflow's own report shows RealmEngine.Data at 46.48% and the server
# workflow's shows RealmEngine.Core at 6.06%, while the merged number is 92.56% and 70.90%.
# Only a job that runs all six test projects can assert the real floors. It is also the only
# .NET workflow that runs Veldrath.Assets.Tests at all.
#
# pull_request includes development on purpose: the other workflows filter [main] only, so every
# existing check is post-merge. A coverage gate is worthless after the merge it should have blocked.

concurrency:
  group: ci-coverage-${{ github.ref }}
  cancel-in-progress: true

on:
  push:
    branches: [ main, development ]
    paths:
      - 'RealmEngine.Core/**'
      - 'RealmEngine.Shared/**'
      - 'RealmEngine.Data/**'
      - 'RealmEngine.Core.Tests/**'
      - 'RealmEngine.Shared.Tests/**'
      - 'RealmEngine.Data.Tests/**'
      - 'Veldrath.Server/**'
      - 'Veldrath.Server.Tests/**'
      - 'Veldrath.Contracts/**'
      - 'Veldrath.Assets/**'
      - 'Veldrath.Assets.Tests/**'
      - 'Veldrath.Discord/**'
      - 'Veldrath.Discord.Tests/**'
      - 'Realm.Full.slnx'
      - 'global.json'
      - 'Directory.Build.props'
      - 'Directory.Build.targets'
      - 'Directory.Packages.props'
      - 'coverage.runsettings'
      - 'scripts/ci-toolchain.sh'
      - 'scripts/check-dotnet-coverage.mjs'
      - 'scripts/coverage-floors.json'
      - 'scripts/forgejo-pr-comment.sh'
      - '.github/workflows/ci-coverage.yml'
  pull_request:
    branches: [ main, development ]
    paths:
      - 'RealmEngine.Core/**'
      - 'RealmEngine.Shared/**'
      - 'RealmEngine.Data/**'
      - 'RealmEngine.Core.Tests/**'
      - 'RealmEngine.Shared.Tests/**'
      - 'RealmEngine.Data.Tests/**'
      - 'Veldrath.Server/**'
      - 'Veldrath.Server.Tests/**'
      - 'Veldrath.Contracts/**'
      - 'Veldrath.Assets/**'
      - 'Veldrath.Assets.Tests/**'
      - 'Veldrath.Discord/**'
      - 'Veldrath.Discord.Tests/**'
      - 'Realm.Full.slnx'
      - 'global.json'
      - 'Directory.Build.props'
      - 'Directory.Build.targets'
      - 'Directory.Packages.props'
      - 'coverage.runsettings'
      - 'scripts/ci-toolchain.sh'
      - 'scripts/check-dotnet-coverage.mjs'
      - 'scripts/coverage-floors.json'
      - 'scripts/forgejo-pr-comment.sh'
      - '.github/workflows/ci-coverage.yml'
  workflow_dispatch:

env:
  DOTNET_VERSION: '10.0.402'
  CONFIGURATION: 'Release'

jobs:
  coverage-gate:
    name: Enforce .NET Coverage Floors
    runs-on: ubuntu-latest

    steps:
    - name: Checkout code
      uses: actions/checkout@v7
      with:
        fetch-depth: 0
        submodules: false

    - name: Install the .NET SDK
      run: bash scripts/ci-toolchain.sh

    - name: Restore dependencies
      run: dotnet restore Realm.Full.slnx

    - name: Build
      run: dotnet build Realm.Full.slnx --configuration "${CONFIGURATION}" --no-restore

    # One invocation, all six test projects: the merged report is the gate's input. Verified at
    # 3,999 tests / 25 s on this solution.
    - name: Test with coverage
      run: |
        dotnet test Realm.Full.slnx \
          --configuration "${CONFIGURATION}" --no-build --verbosity normal \
          --collect "XPlat Code Coverage" --settings coverage.runsettings \
          --results-directory ./TestResults

    - name: Install ReportGenerator
      run: bash scripts/ci-toolchain.sh --reportgenerator

    - name: Generate coverage report
      run: |
        reportgenerator "-reports:./TestResults/*/coverage.cobertura.xml" \
          "-targetdir:./CoverageReport" "-reporttypes:Html;Cobertura;MarkdownSummaryGithub"

    # Node is not guaranteed on the runner image; docs.yml provisions it the same way.
    - name: Setup Node
      uses: actions/setup-node@v7
      with:
        node-version: 22

    # The gate. Fails on a per-assembly or global regression, on an assembly missing from the
    # report (a dead test project), and on an assembly with no floor (a new project).
    - name: Enforce the coverage contract
      run: |
        node scripts/check-dotnet-coverage.mjs \
          --report ./CoverageReport/Cobertura.xml \
          --floors scripts/coverage-floors.json \
          --suggest

    - name: Upload coverage report
      uses: actions/upload-artifact@v3
      if: always()
      with:
        name: coverage-dotnet-gate
        path: CoverageReport/
        retention-days: 14

    - name: Upload test results
      uses: actions/upload-artifact@v3
      if: always()
      with:
        name: test-results-dotnet-gate
        path: TestResults/
        retention-days: 14

    - name: Comment the coverage summary on the pull request
      if: github.event_name == 'pull_request'
      env:
        FORGEJO_TOKEN: ${{ secrets.GITEA_TOKEN }}
        FORGEJO_REPOSITORY: ${{ github.repository }}
        FORGEJO_SERVER_URL: ${{ github.server_url }}
      run: |
        bash scripts/forgejo-pr-comment.sh \
          --pr "${{ github.event.pull_request.number }}" \
          --body-file CoverageReport/SummaryGithub.md \
          --marker '<!-- ci-coverage-gate -->'
```

`actions/setup-node@v7` (`node-version: 22`) is in the block above, immediately before the gate
step and matching [`docs.yml`](../.github/workflows/docs.yml:88) — Node is not guaranteed on the
runner image. The script itself has no dependencies to install, so there is no `npm ci` step.

### `scripts/check-dotnet-coverage.mjs`

Verified against real and doctored reports (evidence in the next section); ship it as-is.

```js
#!/usr/bin/env node
/**
 * Enforce the .NET coverage contract from a merged Cobertura report.
 *
 * Coverage is a property of an assembly, not of the job that happened to run a test
 * against it, so the only honest number is the merged one: ReportGenerator over the six
 * test projects' cobertura output (`-reporttypes:Cobertura`). This script compares that
 * with `coverage-floors.json` and exits non-zero on a regression.
 *
 * Two integrity checks beyond the floors, both there to stop the gate passing by
 * measuring nothing:
 *   - an assembly named in the floor table must appear in the report (a dead test project
 *     would otherwise just remove it from the report);
 *   - an assembly in the report with no floor must fail (a new shipped project has to get
 *     a floor in the same change that adds it).
 *
 * Usage: node scripts/check-dotnet-coverage.mjs --report <Cobertura.xml> [--floors <json>] [--suggest]
 */
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));

function arg(name, fallback) {
  const i = process.argv.indexOf(name);
  return i === -1 ? fallback : process.argv[i + 1];
}

const reportPath = resolve(arg('--report', 'CoverageReport/Cobertura.xml'));
const floorsPath = resolve(arg('--floors', resolve(here, 'coverage-floors.json')));
const suggest = process.argv.includes('--suggest');

const floors = JSON.parse(readFileSync(floorsPath, 'utf8'));
const xml = readFileSync(reportPath, 'utf8');
const minBranchSites = floors.branchFloorMinSites ?? 100;

/**
 * Rates come from the <package> attributes; counts are re-derived from the class-level
 * <lines> blocks. Each line appears twice in the XML — once under <methods>/<method> and
 * again in the class-level <lines> — so they are deduplicated by line number per class.
 */
function parsePackages(source) {
  const packages = new Map();
  for (const chunk of source.split(/<package\s+/).slice(1)) {
    const name = /^name="([^"]+)"/.exec(chunk)?.[1];
    if (!name) continue;
    let lineSites = 0;
    let covered = 0;
    let branchSites = 0;
    for (const classMatch of chunk.matchAll(/<class\s[^>]*>([\s\S]*?)<\/class>/g)) {
      const seen = new Map();
      for (const [, attrs] of classMatch[1].matchAll(/<line\s([^>]*)\/?>/g)) {
        const number = /number="(\d+)"/.exec(attrs)?.[1];
        if (number == null) continue;
        const hits = Number(/hits="(\d+)"/.exec(attrs)?.[1] ?? '0');
        const cc = /condition-coverage="[\d.]+%\s*\((\d+)\/(\d+)\)"/.exec(attrs);
        const previous = seen.get(number);
        if (!previous || hits > previous.hits) seen.set(number, { hits, cc });
        else if (previous && !previous.cc && cc) previous.cc = cc;
      }
      for (const { hits, cc } of seen.values()) {
        lineSites += 1;
        if (hits > 0) covered += 1;
        if (cc) branchSites += Number(cc[2]);
      }
    }
    packages.set(name, {
      lineRate: Number(/line-rate="([\d.]+)"/.exec(chunk)?.[1]),
      branchRate: Number(/branch-rate="([\d.]+)"/.exec(chunk)?.[1]),
      lineSites,
      covered,
      branchSites,
    });
  }
  return packages;
}

const rootAttrs = /<coverage\s([^>]*)>/.exec(xml)?.[1] ?? '';
const rootNum = (name) => Number(new RegExp(`${name}="([\\d.]+)"`).exec(rootAttrs)?.[1]);
const totals = { lineRate: rootNum('line-rate'), branchRate: rootNum('branch-rate') };

const packages = parsePackages(xml);
const failures = [];
const notes = [];
const rows = [];
const pct = (rate) => `${(rate * 100).toFixed(1)}%`;
const pad = (v, w) => String(v).padStart(w);

if (packages.size === 0) {
  console.error(`error: no <package> elements found in ${reportPath} — the report is empty or not Cobertura`);
  process.exit(1);
}

for (const [assembly, floor] of Object.entries(floors.assemblies)) {
  const pkg = packages.get(assembly);
  if (!pkg) {
    failures.push(`${assembly}: missing from the report — a test project did not run, so this assembly is not measured`);
    rows.push([assembly, '—', floor.line, '—', floor.branch ?? '—', 'MISSING']);
    continue;
  }
  const linePct = pkg.lineRate * 100;
  const branchPct = pkg.branchRate * 100;
  const branchGated = floor.branch != null && pkg.branchSites >= minBranchSites;

  if (linePct + 1e-9 < floor.line) {
    failures.push(`${assembly}: line ${pct(pkg.lineRate)} is below its floor of ${floor.line}% (regression: ${(floor.line - linePct).toFixed(2)} points)`);
  }
  if (branchGated && branchPct + 1e-9 < floor.branch) {
    failures.push(`${assembly}: branch ${pct(pkg.branchRate)} is below its floor of ${floor.branch}% (regression: ${(floor.branch - branchPct).toFixed(2)} points)`);
  }
  if (floor.branch != null && !branchGated) {
    notes.push(`${assembly}: branch floor not applied — ${pkg.branchSites} branch sites is under the ${minBranchSites} minimum for a meaningful branch rate (measured ${pct(pkg.branchRate)})`);
  }
  if (suggest) {
    if (linePct >= floor.line + 3) notes.push(`${assembly}: line is ${(linePct - floor.line).toFixed(1)} points above its floor — raise it to ${Math.floor(linePct)}`);
    if (branchGated && branchPct >= floor.branch + 3) notes.push(`${assembly}: branch is ${(branchPct - floor.branch).toFixed(1)} points above its floor — raise it to ${Math.floor(branchPct)}`);
  }
  rows.push([assembly, pct(pkg.lineRate), floor.line, branchGated ? pct(pkg.branchRate) : 'n/a', branchGated ? floor.branch : 'n/a', '']);
}

for (const assembly of packages.keys()) {
  if (!(assembly in floors.assemblies)) {
    failures.push(`${assembly}: in the report but has no floor — add it to coverage-floors.json in this change`);
  }
}

if (totals.lineRate * 100 + 1e-9 < floors.global.line) {
  failures.push(`TOTAL: line ${pct(totals.lineRate)} is below the global floor of ${floors.global.line}%`);
}
if (totals.branchRate * 100 + 1e-9 < floors.global.branch) {
  failures.push(`TOTAL: branch ${pct(totals.branchRate)} is below the global floor of ${floors.global.branch}%`);
}

const width = Math.max(...Object.keys(floors.assemblies).map((n) => n.length), 16);
console.log(`${'assembly'.padEnd(width)}  measured line / floor   measured branch / floor`);
for (const [name, line, lineFloor, branch, branchFloor, flag] of rows) {
  console.log(`${name.padEnd(width)}  ${pad(line, 8)} / ${pad(lineFloor, 5)}   ${pad(branch, 8)} / ${pad(branchFloor, 5)}  ${flag}`);
}
console.log(`${'TOTAL'.padEnd(width)}  ${pad(pct(totals.lineRate), 8)} / ${pad(floors.global.line, 5)}   ${pad(pct(totals.branchRate), 8)} / ${pad(floors.global.branch, 5)}`);
console.log(`\n(${[...packages.values()].reduce((a, p) => a + p.lineSites, 0)} lines, ${[...packages.values()].reduce((a, p) => a + p.branchSites, 0)} branches, ${packages.size} assemblies)`);

for (const note of notes) console.log(`note: ${note}`);

if (failures.length > 0) {
  console.error(`\nCoverage contract failed (${failures.length}):`);
  for (const f of failures) console.error(`  - ${f}`);
  console.error('\nFloors live in scripts/coverage-floors.json. Never lower one to make this pass — add tests.');
  process.exit(1);
}
console.log('\nCoverage contract satisfied.');
```

---

## Verification already performed on this specification

Both artefacts were run against real reports before being written down here; the coder
implementing them starts from something known to work rather than from a design.

**Pass/fail behaviour**, against a real merged report and three doctored ones:

| Case | Input | Result |
|---|---|---|
| Real 10.0.402 report | 7 packages, 32,069 lines | `exit 0` — "Coverage contract satisfied" |
| A test project did not run | `Veldrath.Discord` package removed | `exit 1` — "missing from the report — a test project did not run" |
| Real regression | `RealmEngine.Core` forced to 50.1% line / 40.0% branch | `exit 1` — both line and branch failures named with the point deficit |
| New assembly with no floor | package `Veldrath.NewThing` injected | `exit 1` — "in the report but has no floor" |
| Stale floors | Core line floor lowered to 60, Server branch to 45 | `exit 0` + "raise it to 70" / "raise it to 53" |

**Floor stability** — the gate was run against *all six* measured reports, including the
worst-case ones (Core 70.49, Server 72.42): `exit 0` six times. This is the check that a floor
set from a single measurement would fail.

**Counting agreement** — the script's derived totals (32,069 lines, 10,450 branches) match
ReportGenerator's `<coverage>` attributes exactly, so the branch-site eligibility rule and the
printed table agree with the report humans read.

---

## Implementation pieces

Branch-sized, in order. Each names its verification gate.

### P1 — Ship the gate, green on day one (no thresholds change)

Files: `scripts/check-dotnet-coverage.mjs` (new), `scripts/coverage-floors.json` (new),
`.github/workflows/ci-coverage.yml` (new), `scripts/README.md` (document the pair).

**Verification gate:** reproduce the four numbers locally with the CI-pinned SDK —

```bash
dotnet test Realm.Full.slnx -c Release --collect "XPlat Code Coverage" \
  --settings coverage.runsettings --results-directory ./TestResults
reportgenerator "-reports:./TestResults/*/coverage.cobertura.xml" \
  "-targetdir:./CoverageReport" "-reporttypes:Cobertura"
node scripts/check-dotnet-coverage.mjs --report ./CoverageReport/Cobertura.xml --suggest   # exit 0
```

then prove it fails by temporarily setting `RealmEngine.Core.line` to 99 (expect exit 1) and
restoring it. The floors are the measured ones, so no threshold in the repo is *changed* by this
piece — they are introduced.

### P2 — Make the RNG injectable, then the numbers stop moving

This is the enabling change and it should land early; without it, every later test PR makes the
gate jitter, and the combat/crafting surface can only ever be half-covered.

Files: `RealmEngine.Shared/` (new `IRandomSource` + a default `SystemRandomSource` wrapping
`Random.Shared`), plus the 13 call sites of `Random.Shared` — `RealmEngine.Core/Services/DiceRoller.cs`,
`RealmEngine.Shared/Utilities/WeightedSelector.cs`, `RealmEngine.Core/Features/Combat/Commands/ApplyStatusEffectHandler.cs`,
`RealmEngine.Core/Features/Combat/Commands/FleeFromCombat/FleeFromCombatHandler.cs`,
`RealmEngine.Core/Features/Exploration/{Services/ExplorationService,Commands/ExploreLocationCommandHandler,Commands/GenerateEnemyForLocationHandler}.cs`,
`RealmEngine.Core/Features/Progression/Services/SpellCastingService.cs`,
`RealmEngine.Core/Services/Budget/MaterialPoolService.cs`,
`Veldrath.Server/Features/Characters/Combat/{FleeFromCombatHubCommand,CombatHelpers}.cs`,
`Veldrath.Server/Features/Zones/SearchAreaHubCommand.cs`, `Veldrath.Server/Hubs/GameHub.cs`.
Keep `DiceRoller`'s public surface for compatibility; add a seeded `FakeRandomSource` in the test
Infrastructure per the project's `FakeXxx` rule.

**Verification gate:** run the six suites three times and diff the per-assembly rates — the four
oscillating files must produce identical numbers; then re-measure and raise any floor the change
moved (D5 rule 2). `RealmEngine.Shared`'s public API change needs XML docs (CS1591) and a note in
`.github/agent-memory/engine-codebase.md`.

### P3..P6 — The 4,808 lines, one assembly per PR

Ordered by shortfall: `RealmEngine.Core` (2,618), `Veldrath.Server` (1,586),
`RealmEngine.Shared` (500), `Veldrath.Discord` (226), `Veldrath.Assets` (55). Each PR raises that
assembly's floors and leaves the others alone.

**Verification gate for each:** the assembly's floor rises by the measured amount, the gate is
green, and the total moves toward 74 → 90. `Veldrath.Assets` is a single afternoon (5 files,
`AssetStore` already at 98%); `RealmEngine.Data` and `Veldrath.Contracts` need no work to reach
90 and are simply held by their floors.

**Note for the `Veldrath.Server` piece:** `GameHub.cs` is 1,990 lines (22% of the assembly) and
1,099 of them are uncovered — 44% of the server's 2,487 uncovered lines, and 23% of the
repo-wide shortfall to 90%, in one file. Before writing 1,000
lines of hub tests, split it by feature slice to match `Features/`, as its sibling handlers
already are; otherwise every future feature PR collides with the coverage work in the same file.
This plan does not decompose it — that is design work with its own review.

### P7 — Trigger scope for the existing workflows (separate PR, separate decision)

Add `development` to `pull_request.branches` in `ci-engine.yml`, `ci-server.yml`,
`ci-discord.yml` and `ci-web.yml` (correction 3). One line each, but it changes what runs on
every PR, so it is reviewed on its own — carded rather than bundled here.

### P8 — Documentation

- [`.github/instructions/testing.md`](../.github/instructions/testing.md:272) — the `## Code Coverage`
  section describes collection and exclusion only. Add the contract, the floors table, the ratchet
  protocol, and fix the stale version claim there (`coverlet.collector` v10.0.1; it is 10.1.0 in
  [`Directory.Packages.props`](../Directory.Packages.props:75)).
- [`AGENTS.md`](../AGENTS.md:237) — add `ci-coverage.yml` to the workflow table and adjust the
  "That is the whole list, deliberately" paragraph at [`AGENTS.md:245`](../AGENTS.md:245) to say
  why this one cannot live inside a component workflow (D3).
- [`.roo/skills/run-all-tests/SKILL.md`](../.roo/skills/run-all-tests/SKILL.md:8) — it says "7 test
  projects" and "~3,870 tests"; it is 6 projects and 3,999 tests, and it should mention that
  `dotnet test Realm.Full.slnx --collect` is now the gate's input.
- This file is deleted when P1–P6 land, per [`plans/README.md`](README.md) — the durable parts
  fold into `testing.md` (P8) and `docs/ci-forgejo.md`.

---

## Explicitly out of scope

- **Raising any floor to 90 in this plan.** 90 is the recorded target; the floors are the
  measured baseline. P3–P6 raise them.
- **Excluding "hard" code to make 90 cheaper.** No new `ExcludeByFile` / `ExcludeByAttribute`
  entries. Exclusions stay at [`coverage.runsettings`](../coverage.runsettings:9-12): tests,
  generated, migrations, entry points. `ServiceCollectionExtensions`, `DotEnvConfiguration`,
  `SmtpEmailSender` and the asset manifests are all executable in a test process, so the web
  rule ("exclude only what cannot execute") keeps them in scope.
- **A coverage gate for `web/`.** Already enforced; untouched.
- **Per-file or per-directory floors.** Rejected in D4 with a reason.

## Open questions for a human

1. **Ratchet vs freeze — the one decision worth overriding.** This plan enforces the measured
   baseline now and climbs to 90 by assembly (D1/D5), because enforcing 90 immediately blocks
   every .NET PR until ~4,800 lines of tests exist. If the intent is that 90 must be *enforced*
   from day one — accepting the freeze — say so before P1 lands: the same script then ships with
   the target numbers as floors, and `ci-coverage.yml` is red until P3–P6 are done. Nothing else
   in this plan changes.
2. **`Veldrath.Discord` at 90%.** `BotWorker` and `InteractionHandlingService` are Discord-API
   adapter code (0% today). Reaching 90 means a fake `DiscordSocketClient` harness; it may be
   better to accept 75–80 for the bot and record that as a deliberate exception. The plan
   currently holds it to 90 like everything else.
3. **Whether `GameHub` gets split before or during P4.** The split is design work; doing it first
   makes the tests cheaper and the file smaller, but delays the first coverage PR on the server.
4. **Whether the `development` trigger fix (P7) should block P1.** The gate is useful without it
   (it still reports post-merge and on PRs into `main`), but not merge-blocking. I would land P7
   first because it is four one-line changes that make every existing check meaningful.
