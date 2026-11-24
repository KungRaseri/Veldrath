---
kind: work-item
date: 2026-11-03
area: web
title: "The .NET coverage gate landed, and settling it moved no floor"
---

### Session-96 (2026-11-03) — The .NET coverage gate landed, and settling it moved no floor

PR #21 landed `scripts/coverage-floors.json`, `scripts/check-dotnet-coverage.mjs` and
`ci-coverage.yml` on `development` (merge `ab5dfe72`, after #20 `7d81b2ae` and #22 `4e713b08`). The
"no coverage gate" note in Session-93 is therefore superseded — but the enforcer is **not**
`dotnet test`. Plain `dotnet test`, even with `--collect "XPlat Code Coverage"`, only produces the
Cobertura report and stays green at any coverage level. The comparison is a separate step:
`ci-coverage.yml`'s `coverage-gate` job ("Enforce .NET Coverage Floors") runs `Test with coverage`,
then `Generate coverage report` (ReportGenerator merge), and only then `Enforce the coverage
contract`, which is

```bash
node scripts/check-dotnet-coverage.mjs \
  --report ./CoverageReport/Cobertura.xml \
  --floors scripts/coverage-floors.json \
  --suggest
```

That node script is what fails on a per-assembly or global regression, on an assembly missing from
the merged report, and on a shipping assembly carrying no floor. Reproduce the gate locally as
those three steps in that order — not by reading the exit code of `dotnet test`.

**Settling the floors on the merged base was a no-op, by the contract's own arithmetic.** Three full
runs on `0efb7c7c` with the CI-pinned SDK 10.0.402, each merged with ReportGenerator, are identical at
every package on line *and* branch: total 75.3806% / 55.5100% — **24,213 of 32,121 covered lines and
5,823 of 10,490 covered branches** — with 4,019 tests passing in every run.
`check-dotnet-coverage.mjs --suggest` names nothing on any of the three reports, because the largest
lift over a floor is +2.22 pp (Core branch) against D5 rule 2's 3-point trigger. (The "24,183 covered
lines" written in PR #22's body was a prose slip; ReportGenerator's summary says 24213 of 32121.)

**Two traps for whoever touches this next:**

- Do **not** "fix" the floors to the deterministic three-run rates. `measured` in
  `coverage-floors.json` is the six-run *lowest* observation that produced each floor (D4: floor =
  `trunc(lowest) − 1`), which is why Core's floor is 69 against a 70.49 low. Setting Core to 71
  because three clean runs said 71.12 puts the floor above the lowest observation and makes the gate
  fail intermittently — the exact failure the one-point subtraction exists to prevent.
- The `measured` block is the baseline, not a live field: no D5 rule refreshes it, so leave it alone
  when no floor moves. Live counts have drifted with the code since the baseline (+52 coverable lines:
  Core 13,459, Shared 1,488, Server 9,029), which makes its `lines` values read slightly low —
  informational, never a gate input.
