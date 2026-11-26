# E2E reporting — the Playwright half of the CI summary

> Opened 2026-11-20. Coverage has a report, a gate, an artifact and a PR comment. The
> Playwright suites have console output and a trace directory nobody is told about.

## 1. What was actually broken

Two defects, both measured in [`ci-web.yml`](../.github/workflows/ci-web.yml) and
[`playwright.config.ts`](../web/client/playwright.config.ts).

**No reporter was ever configured.** [`playwright.config.ts`](../web/client/playwright.config.ts:27)
sets `webServer`, `retries`, `use` and `testMatch` — and no `reporter`. The default is `list`:
console only. Consequences:

- `web/client/playwright-report` is uploaded by two steps
  ([`ci-web.yml`](../.github/workflows/ci-web.yml:183), [`ci-web.yml`](../.github/workflows/ci-web.yml:332))
  and the path **never exists**, because the `html` reporter was never enabled. Both uploads
  are `if-no-files-found: ignore`, so the step is green and empty forever.
- Only `test-results/` (traces, videos) lands as an artifact, and nothing on the pull request
  says it is there.
- There is no JSON or JUnit output, so there is no history, no flaky count, no duration
  trend, and no mechanical way to notice that a spec stopped running.

**`encounters.e2e.ts` does not run in CI.** The spec list in
[`ci-web.yml`](../.github/workflows/ci-web.yml:306) is hard-coded and names 14 specs; the local
list in [`web/client/package.json`](../web/client/package.json:24) names 15. The extra one is
`tests/encounters.e2e.ts` — the combat and death specs that the content pass worked to make
reachable. Two hand-maintained lists, one of them short.

## 2. Decisions

| # | Decision | Choice |
|---|---|---|
| **R1** | Where the report goes | Its **own sticky comment** (`<!-- ci-e2e -->`), not a section of the coverage comment. The merge script hardcodes a `## Coverage summary` heading and a `dotnet, web` ordering, and test counts under a coverage heading is the wrong document. |
| **R2** | Reuse, do not duplicate | The e2e comment uses the **same** section-merge machinery as coverage: [`scripts/forgejo-pr-comment.sh`](../scripts/forgejo-pr-comment.sh) gains the ability to combine `--marker` with `--section`/`--title`, and [`merge-coverage-sections.awk`](../scripts/lib/merge-coverage-sections.awk) takes its marker and heading as parameters. Coverage's defaults do not change, so its output stays byte-identical. |
| **R3** | One section per job | `ci-web` has two independent Playwright executions (the stack-free run inside `build-test`, and `e2e-game`). They write sections `e2e` and `e2e-stack` into the one comment, so they never clobber each other. |
| **R4** | What gates | **Integrity only.** A spec named in the manifest that produced no result fails the check, as does an unexpected spec file, as does a `test.skip` without an allow-listed reason. Pass/fail stays the specs' exit code. Coverage gates on a number because coverage can silently drop; e2e does not need a second truth. |
| **R5** | Flaky | **Reported, never gated.** `retries: 1` on CI already hides one flake; the useful signal is the name in the comment. A budget would go red on the first day and be deleted. |
| **R6** | Single source of truth for spec sets | [`web/client/tests/e2e-manifest.json`](../web/client/tests/e2e-manifest.json) lists every spec exactly once, grouped into the runs that execute it. npm scripts, CI and the integrity check all read it. This is what makes R4 possible and what puts `encounters.e2e.ts` back into CI. |
| **R7** | Reports are always produced | Report generation and upload run with `if: always()`. A report that only exists on a green run is missing exactly when it is read. |
| **R8** | Provenance | The summary header records the run label, the git SHA and the branch, so a stale artifact cannot be mistaken for the current push. [`playwright.config.ts`](../web/client/playwright.config.ts:53) disables server reuse under `E2E_STACK`, so a report from a reused preview would be wrong in a way nothing else would catch. |
| **R9** | Artifacts | `html` (openable, with traces), `json` (machine-readable history), `junit` (the format the .NET jobs already upload as `.trx`), `test-results/` (traces and video), and the generated `E2EReport/`. Retention 14 days, matching every other artifact here. |

## 3. How it works

```text
web/client/tests/e2e-manifest.json
        │  run name → { env, specs[], workers }
        ▼
web/client/tests/run-e2e.mjs  ──►  playwright test <specs…>  (env applied)
        │                                   │
        │                                   ├─► playwright-report/<run>/   (html)
        │                                   ├─► test-results/e2e-<run>.json
        │                                   ├─► test-results/e2e-<run>.xml   (junit)
        │                                   └─► test-results/<run>/          (traces, video)
        ▼
scripts/check-e2e-manifest.mjs  →  integrity gate (R4), reads the JSON outputs
scripts/e2e-summary.mjs         →  E2EReport/Summary.md
        ▼
scripts/forgejo-pr-comment.sh --marker '<!-- ci-e2e -->' --section e2e --title 'E2E (Playwright)'
```

Divergence is impossible by construction: the runner refuses a run name it does not know, and
the integrity check fails when a spec on disk belongs to no run.

## 4. Phases

| Phase | Work | Done when |
|---|---|---|
| **P1** | Manifest + runner | `npm run test:e2e:stack` and the CI job invoke the same list; every `*.e2e.ts` file on disk is claimed by exactly one run; a new spec that is claimed by no run fails the check |
| **P2** | Reporters | A CI run writes `playwright-report/<run>/index.html`, the JSON and the JUnit file; opening the HTML report from the artifact shows the failing test with its trace |
| **P3** | Summary + integrity | `E2EReport/Summary.md` lists totals, per-spec rows slowest-first and every failure with its error headline; the integrity check fails on a missing spec, an unexpected spec and an unjustified skip |
| **P4** | Comment | A pull request shows one `ci-e2e` comment with a section per job, updated in place, present on red runs too |
| **P5** | Docs + memory | [`docs/ci-forgejo.md`](../docs/ci-forgejo.md) describes the report and the new troubleshooting rows; [`web/client/README.md`](../web/client/README.md) gains the local recipe; the session note records both defects |

## 5. Verification

```bash
# Local, against the isolated stack
./scripts/e2e-stack.sh start --fresh
cd web && npm run test:e2e:stack --workspace client
node ../scripts/check-e2e-manifest.mjs --run stack --results client/test-results/e2e-stack.json
node ../scripts/e2e-summary.mjs --results client/test-results/e2e-stack.json \
  --out client/E2EReport/Summary.md
```

The negative controls are the point, and each one is run before the check is trusted:

- delete a spec file's entry from the manifest → the integrity check fails naming the file;
- point a manifest entry at a spec that does not exist → fails naming the entry;
- add `test.skip(true, 'temporarily')` to a spec → fails until the skip is allow-listed with a reason;
- break one assertion in a spec → the comment shows the failing test, its error headline and the
  trace artifact path, and the job is red.

## 6. What this deliberately does not do

- **No Playwright coverage.** v8 statement coverage through Playwright's sourcemaps is not
  reliable enough to gate on; the parity suites and Vitest already cover the client.
- **No flaky budget** (R5).
- **No publishing of the HTML report to a site.** It is an artifact, like `CoverageReport/`.
- **No change to what the specs assert.** This pass makes the existing runs legible; it does not
  add a journey.
