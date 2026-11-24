---
kind: work-item
date: 2026-11-03
area: web
title: "Web coverage gate green: branch tail closed"
---

### Session-93 (2026-11-03) — Web coverage gate green: branch tail closed

`npm run coverage --workspace client` now **passes** (exit 0), which is the first time the gate the
user chose has held:

| Metric | Value |
|---|---|
| Lines | 99.19% (4702/4740) |
| Statements | 99.24% (7596/7654) |
| Functions | 98.83% (1614/1633) |
| Branches | 95.54% (2937/3074) |

80 test files, 1096 tests. Per-glob floors also clear: `src/lib/server` 99.67 lines / 100 branches,
`src/lib/game/**/*.ts` 99.73 / 99.47, `src/lib/foundry` 100 / 98.08, `src/routes/**/*.ts` 99.49 /
97.83, `src/**/*.svelte` 98.95 / 92.66. The `src/lib/game/**/*.ts` and `src/routes/**/*.ts` rows are
the globs the thresholds actually match; the directory totals that also include the `.svelte` files
beside them are lower (99.88 / 95.68 and 98.70 / 94.77) and are not the floored numbers. PR #13. The
path there (PRs 1–13 of the coverage pass) is
recorded in [`plans/web-feature-parity-and-coverage.md`](../../../plans/web-feature-parity-and-coverage.md),
which is the live document for this work — the earlier "still red at 76.33% / 58.05%" notes in this
file are historical.

What future coverage work on this app needs to know:

- **Svelte compiles `{expr}` to `expr ?? ''`.** Most "missing branches" in a `.svelte` file are that
  fallback, not logic: render the same page with the field nullish and it is covered.
- **`<select value={…}>` compiles to `if (select_value !== (select_value = …))`**, whose false side
  cannot run in a static render. Those branches are not reachable from a test; do not chase them and
  do not exclude the whole file to hide them.
- **Stateful components need a fresh render per case.** The overlays, the character sheet, and the
  creation wizard keep pending state; a shared `beforeEach` render leaks it, and two mounted panels
  make `getByRole` ambiguous.

**.NET still has no coverage gate.** (*Superseded on 2026-11-03: PR #21 landed the gate — see
Session-96 for the enforcing step, the merged floors and the settlement measurement.*)
[`coverage.runsettings`](../../../coverage.runsettings) collects
cobertura and every .NET workflow uploads a `CoverageReport/` artifact, but nothing compares it to a
floor, so `dotnet test` stays green at any coverage level. The SDK is present on kung-lab-hermes —
`~/.dotnet/dotnet`, version 10.0.403 — it is only missing from `PATH`, so
`export PATH="$HOME/.dotnet:$PATH"` gives a local number without waiting for CI. Tracked as its own
card (gate contract: which projects, which floors, cobertura-parse script vs
`coverlet.msbuild` threshold) — do not describe the 90% goal as met for .NET until that lands.
