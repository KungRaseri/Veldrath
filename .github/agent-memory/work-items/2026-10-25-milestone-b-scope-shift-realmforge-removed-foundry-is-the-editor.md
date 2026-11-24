---
kind: work-item
date: 2026-10-25
area: web
title: "Milestone B scope shift: RealmForge removed, Foundry is the editor"
---

### Session-90 (2026-10-25) — Milestone B scope shift: RealmForge removed, Foundry is the editor

**Decision (user):** "pull RealmForge out of the project entirely. Focus on Foundry (in svelte app)."
RealmForge was the last remaining Avalonia surface, so this also finishes the web-first pivot.

**Deleted:** `RealmForge/`, `RealmForge.Tests/` (27 tests), `RealmUI.Fonts/`, `RealmForge.slnx`,
`versions/tooling.props`, `.github/workflows/ci-tooling.yml`, `.roo/skills/run-realmforge/`,
`THIRD-PARTY-NOTICES.md` (its only content was the RealmForge game-icons attribution).

**Config cleaned:** `Realm.Full.slnx` (13 projects left) and **`Veldrath.Libraries.slnx`, which still
listed `RealmUI.Fonts/RealmUI.Fonts.csproj` and would no longer build**; `Directory.Packages.props`
(Avalonia + ReactiveUI ItemGroups); `Directory.Build.props`; `codecov.yml` (`forge` flag);
`coverage.runsettings`; `.dockerignore`; `.github/dependabot.yml` (Avalonia/ReactiveUI ignore rules);
`.github/workflows/release.yml` (`tooling/v*` trigger, tooling tag branch, `tooling)` release body);
`.vscode/tasks.json` + `launch.json` (6 dead tasks/configs); `scripts/generate-version.ps1`;
`scripts/README.md`.

**Docs updated:** `AGENTS.md`, `README.md`, `SECURITY.md`, `docs/design-system.md` (all Avalonia/AXAML
guidance is now explicitly historical, matching the earlier Blazor treatment), `docs/client-ui-architecture.md`,
`.github/instructions/{build-strategy,testing,xml-documentation,ef-core-patterns}.md`, `.roo/skills/*`,
`.roo/modes/*/instructions.md`, `web/client/README.md`, `web/client/vite.config.ts`, the Foundry
content page copy.

**Memory:** `forge-foundry-sync.md` → `foundry-content-schema.md` (the name referenced a deleted app).

**Milestone B re-scoped:** Foundry was read-only, so B2 is no longer "validate saves in a desktop app" —
it now owns the server write path: `GET /api/content/{type}/{slug}/validate`,
`POST /api/content/{type}`, `PUT /api/content/{type}/{slug}`, `DELETE /api/content/{type}/{slug}`,
RBAC-gated while reads stay anonymous, an `UpdatedAt` precondition for concurrency, and a
schema-driven edit form in Foundry. See `plans/content-pipeline.md` §2.2.

**Verification:** `dotnet build Realm.Full.slnx` + `Veldrath.Libraries.slnx` 0 warnings 0 errors,
`dotnet format --verify-no-changes` clean, **3,925 tests** across 6 projects all green
(Core 2,021 / Shared 793 / Data 265 / Server 807 / Discord 29 / Assets 10), web typecheck 0 errors,
lint clean (`prettier --check` needs the Foundry page reformatted after edits), Vitest 10 files,
Playwright 22 passed + 1 skipped, production build OK.
