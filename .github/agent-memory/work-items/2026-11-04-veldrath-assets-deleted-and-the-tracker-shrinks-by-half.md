---
kind: work-item
date: 2026-11-04
area: web
title: "`Veldrath.Assets` deleted, and the tracker shrinks by half"
---

### Session-97 (2026-11-04) — `Veldrath.Assets` deleted, and the tracker shrinks by half

The project and its test project are gone. It was **dead, not merely unused**: no static files were
ever served from it, and the directory held 1,837 art files (98 MB) that no code path read.

What actually proved it dead, in order of strength:

- `Veldrath.Server.csproj` carried a `<ProjectReference>` to it, but **no** `.cs` file under
  `Veldrath.Server/` referenced any type from the `Veldrath.Assets` namespace — a reference with no
  importer compiles and does nothing.
- `AddAssetStore` (the DI extension) was never called from any composition root, so `AssetStore` was
  never constructed at runtime.
- No `UseStaticFiles`/`MapStaticAssets` mount pointed at `GameAssets/`, so the tree was not reachable
  over HTTP either.
- No CI workflow referenced it, so nothing was even compiling it in the gate `Realm.Full.slnx` covered.

The nuance worth keeping: `AssetStore` was a generic content-addressed store, and the **desktop client
never used it either**. It was not killed by the web-first pivot — it was never wired up. Do not read
the deletion as "the pivot removed an asset pipeline"; there was no pipeline.

**Removed:** `Veldrath.Assets/` (1,837 files, 98 MB) and `Veldrath.Assets.Tests/`;
`versions/assets.props`; `scripts/download-assets.ps1`, `scripts/sync-assets.ps1`,
`scripts/package-assets.ps1`.

**References swept** (the first pass missed several; a wide `grep -ri` is mandatory for a project
removal): the three `.slnx` files that listed it (`Realm.Full`, `Veldrath.Libraries`,
`Veldrath.Server`), `Veldrath.Server/Veldrath.Server.csproj`, `Directory.Build.props`,
`Veldrath.Server/Dockerfile` and `Dockerfile.dev`, `docker-compose.dev.yml` (the source mount),
`scripts/coverage-floors.json` (its floor was line 45), the `GameAssets` binary-ignore block in
`.gitignore`, three tasks in `.vscode/tasks.json` (`build-veldrath-assets`, `test-veldrath-assets`,
`download-assets`), `.github/instructions/{testing,xml-documentation,build-strategy}.md`,
`.roo/skills/{run-all-tests,run-tests-by-component}/SKILL.md`, `scripts/generate-version.{sh,ps1}`,
`AGENTS.md`, `README.md`, and both path lists in `ci-coverage.yml`.

**Verification:** `dotnet build Realm.Full.slnx` → 0 warnings, 0 errors.
`dotnet test Realm.Full.slnx --no-build` → **4,011 passed, 0 failed** across five projects:
Core 2,040, Shared 795, Data 301, Server 846, Discord 29. The test-project count everywhere reads
**five**, not six.

**Inventory after the deletion** (`bash scripts/ci-merge-gate.sh --uncovered` on this tree): tracked
files **1,618** (was 3,472) and uncovered paths **36** (was 39); the "scripts no workflow names" class
fell 10 → 7 because the three asset scripts left with the project. `docs/ci-forgejo.md` records the new
numbers with the date, so anyone re-running `--uncovered` sees the same counts or knows the tree moved.

**Recovery:** the art tree still exists in history — `git log --diff-filter=D -- Veldrath.Assets/`
finds the deletion commit, and `git checkout <parent> -- Veldrath.Assets/GameAssets` restores any
subtree. Nothing needs re-downloading; the old download script pointed at third-party packs, not a
canonical source. `world-lore-plan.md` marks the TMX maps the same way.
