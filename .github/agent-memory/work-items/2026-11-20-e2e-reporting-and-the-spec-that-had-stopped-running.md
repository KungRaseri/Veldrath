---
kind: work-item
date: 2026-11-20
area: web
title: "E2E reporting, and the spec that had stopped running"
---

### Session-103 (2026-11-20) — E2E reporting, and the spec that had stopped running

Asked for "reporting for e2e like we do for coverage". Investigating it turned up two defects worse than the
missing report.

- **No Playwright reporter was ever configured.** `playwright.config.ts` set `webServer`, `retries`, `use` and
  `testMatch` and no `reporter`, so the default was console `list` and `web/client/playwright-report/` **never
  existed**. Both web jobs upload that path with `if-no-files-found: ignore`, so the step was green and empty.
  There was no JSON or JUnit either: no history, no flaky count, no way to notice a spec that stopped running.
- **`tests/encounters.e2e.ts` did not run in CI.** The spec list existed twice — `web/client/package.json` (15
  specs) and `.github/workflows/ci-web.yml` (14) — and the workflow's copy was missing the combat and death
  journey that the content pass had just unblocked. Two hand-maintained lists, one short.

What landed (detail and decisions in [`plans/e2e-reporting.md`](../../../plans/e2e-reporting.md)):

- **One manifest, one runner.** `web/client/tests/e2e-manifest.json` names every spec once across the runs
  `stack-free`, `stack` and `outage`; `web/client/tests/run-e2e.mjs` expands a run into the Playwright
  invocation, applies the run's env (manifest values are **defaults** — a variable already in the environment
  wins, which is how CI keeps its own ports), and points the reporters at per-run files. `--list`/`--dry-run`
  work without `npm ci`; a run with an unmet gate refuses to start instead of producing an all-skipped report.
- **Reports per run**, because one job runs Playwright twice and a fixed output file would be truncated by the
  second: `html` (never opens), `json`, `junit`, and `outputDir` for traces, all from `PW_*` variables.
- **`scripts/e2e-summary.mjs`** renders `E2EReport/Summary.md` from one or more JSON reports: totals, per-spec
  table slowest-first, failures with their first error line and trace path, skips with their reason, plus the
  commit in the header (a reused preview is `reuseExistingServer` outside CI, so provenance matters).
- **Flaky means a *failed* attempt, not merely a second attempt.** A test is flaky only when at least one
  attempt is `failed`/`timedOut` and the last one passed. A `skipped` attempt is not a failure: in a serial group
  a timed-out test marks the rest of the group `skipped` on attempt 1, and the group retry passes them all — so
  `[skipped, passed]` is **passed**, which is what Playwright's own tally says. Counting it flaky is why comment
  1086 reported 2 flaky / 19 passed for run 2194 while Playwright's console said 1 flaky / 20 passed
  (`encounters.e2e.ts:134`). `Duration` stays the sum of attempts across workers, not wall clock, and the
  summary says so under the totals table.
- **`scripts/check-e2e-manifest.mjs`** is the gate, and it is deliberately **not** a pass/fail gate: it fails
  when a spec on disk is claimed by no run, when a claimed spec produced no result (a suite with zero specs is
  collected from `suite.file` for exactly this), when a spec ran that the run does not claim, when a skip has no
  allow-listed reason, or when a run falls below its floor in `web/client/tests/e2e-baseline.json` (empty today
  on purpose — `--suggest` prints the number to commit). Flaky is counted and named, never gated.
- **A separate `<!-- ci-e2e -->` comment**, not a section of the coverage comment: `merge-coverage-sections.awk`
  hardcoded `## Coverage summary`, and test counts under a coverage heading are the wrong document. The script
  now takes `--marker` **with** `--section`/`--title` and a `--heading`, and the awk takes its marker and heading
  as parameters — coverage's output stays byte-identical, which its 102-check test pins.
- **Two skips are allow-listed with reasons**: `harvest.e2e.ts` (chests already emptied on a non-fresh stack) and
  `upstream-failure.e2e.ts` (it needs an API started by `scripts/e2e-stack.sh`; CI starts `dotnet run` itself, so
  the spec self-skips there and runs locally).
- **Tests**: `node --test scripts/tests/e2e-reporting.test.mjs` (22 tests: summary rendering, flaky (including
  `[skipped, passed]` → passed and `[failed, passed]` → flaky), merged runs, missing report, and five negative
  controls for the gate) and the extended
  [`scripts/tests/forgejo-pr-comment.test.sh`](../../../scripts/tests/forgejo-pr-comment.test.sh) (case I covers the
  e2e marker, its heading, the kept sibling section, and that the coverage comment is neither patched nor
  deleted).
- **Worth knowing when touching this**: `require.resolve('playwright/cli.js')` throws
  `ERR_PACKAGE_PATH_NOT_EXPORTED` — resolve the package entry and take `cli.js` beside it. And the checker's
  default manifest path is resolved **from the script**, not the working directory, because the same command has
  to work from the repo root, `web/` and `web/client/`.

Still open after this pass: the baseline floors are empty until a green CI run records them; the outage spec
only executes locally (CI has no API for it to stop); and the e2e comment has no artifact deep-link, because the
artifact URL shape is instance-specific.
