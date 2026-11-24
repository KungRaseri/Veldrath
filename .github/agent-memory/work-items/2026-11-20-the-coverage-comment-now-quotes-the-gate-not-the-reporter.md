---
kind: work-item
date: 2026-11-20
area: web
title: "The coverage comment now quotes the gate, not the reporter"
---

### Session-104 (2026-11-20) — The coverage comment now quotes the gate, not the reporter

The web coverage **check** and the web coverage **comment** have never been the same measurement, and
on PR #88 the difference surfaced as a contradiction: the gate logged `Branches : 90.33% ( 4428/4902 )`
while comment 1063 carried `Branch coverage | 89.8% (3,752 of 4,180)`, which reads as a gate failure
beside a green check.

- **Mechanism, nailed down at the source.** `codecov/codecov-report`'s `CoberturaReader.ReadLines`
  (`src/CoverageReport/CoberturaReader.cs:119-160`) sums the per-line `condition-coverage="x% (c/t)"`
  attributes and never reads the root `<coverage branches-valid=… branches-covered=…>`. istanbul
  writes a line-level condition only for a branch site it can attach to a source line, so that sum is
  a *subset* of v8's branch map — 4,180 of 4,902 sites on that run. The root attributes **do** agree
  with v8 exactly (`branches-valid="4902" branches-covered="4383"` on `development@44422ac2`), so the
  lossy step is the per-line conversion, not the file.
- **Decision: the comment quotes the gate** (option 1 of three; the plan records why 2 and 3 lost).
  `scripts/web-coverage-gate.mjs` heads the `web` section with the totals parsed from the coverage
  step's log — that is also the only place vitest's `ERROR: Coverage for …` threshold lines exist, so
  per-glob floor breaches now reach the comment — and labels the reporter's per-directory table as
  Cobertura accounting with both numbers for the run. Tests:
  `node --test scripts/tests/web-coverage-gate.test.mjs` (8 cases).
- **Two reporting defects fixed alongside it.** `coverage.reportOnFailure` was off, so a red gate wrote
  no Cobertura at all (vitest's `text` lines went to the log, nothing else) and the comment kept the
  previous head's numbers; and the comment step had no `if: always()`, so a red job never posted. Both
  are on now. Verified locally: a failing gate at 89.41% still writes `coverage-summary.json` and
  `cobertura-coverage.xml`.
- **Do not "fix" this by reconciliation.** The gate is v8's branch *sites*; the table is istanbul's
  line conditions. Lowering the gate to 4,180 or subtracting the 722 conditionless sites from the
  numerator would each be a real weakening. The remaining fix belongs upstream (read the root
  attributes when present, fall back to the per-line sum for coverlet) plus a `COVERAGE_REPORT_REF`
  bump — recorded as residual in the plan.
- **Gotcha for local reproduction.** `npm run coverage --workspace client` needs `PUBLIC_API_URL` and
  `PUBLIC_HUB_URL` set or SvelteKit aborts with `env_invalid`; `src/lib/testing/harness.test.ts:84`
  asserts the checkout path contains `Veldrath`, so a worktree named otherwise fails one test (and,
  with `reportOnFailure` now on, still writes the report). The pinned reporter needs
  `PATH="$HOME/.dotnet:$PATH"` if the SDK is not installed system-wide.
