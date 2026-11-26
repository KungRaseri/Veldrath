---
kind: work-item
date: 2026-11-20
area: web
title: "The coverage reporter now reads the root counters (v1.0.4)"
---

### Session-106 (2026-11-20) — The coverage reporter now reads the root counters (v1.0.4)

The residual Session-104 recorded is closed upstream, not in this repository.

- **Upstream.** `codecov/codecov-report` PR #1 merged as `f6a5681` and tagged **`v1.0.4`**: the reader
  takes the root `<coverage branches-valid=… branches-covered=…>` attributes when a file carries them
  and falls back to the per-line `condition-coverage` sum for coverlet (whose per-line conditions are
  complete). The Total row therefore reports the gate's branch pair; only the formatting differs —
  one decimal (`90.3%`) against the gate's two (`90.33%`).
- **Pinned in both on-demand paths.** `scripts/coverage-report.sh` (`COVERAGE_REPORT_REF` default) and
  `scripts/generate-coverage-report.ps1` both default to `v1.0.4`. The tool clones into
  `.coverage-report/v1.0.4/` (gitignored) on first use.
- **The contrast paragraph reads the root counters.** `scripts/web-coverage-gate.mjs` has three
  mutually exclusive states: root counters equal to the gate → "the two accountings agree on this run"
  plus the per-directory caveat (those rows are still istanbul's line conditions and do not sum to the
  Total); root counters present but different → "a Cobertura file that does not describe this run";
  no root counters → the previous per-line paragraph verbatim. The `Warning — accounting mismatch`
  note still fires in the stale-file case. Tests:
  `node --test scripts/tests/web-coverage-gate.test.mjs` (9 cases).
- **Verified against real artifacts, not only fixtures.** Replaying the PR #88 Cobertura file through
  the bumped pin printed `Fetching coverage reporter v1.0.4` and `Branch coverage: 90.3% (4,428 of
  4,902)`; the gate paragraph on the same file says the two accountings agree.
- **Not a defect to "fix".** The `90.33` vs `90.3` precision difference is deliberate (the tool
  formats percentages with one decimal); the contract is that the gate row and the reporter's Total
  row carry the same two counts. `scripts/coverage-floors.json`, `coverage.runsettings` and
  `web/client/vite.config.ts` thresholds did not move.
