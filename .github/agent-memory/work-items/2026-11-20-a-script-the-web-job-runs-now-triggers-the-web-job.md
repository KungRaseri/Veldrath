---
kind: work-item
date: 2026-11-20
area: web
title: "A script the web job runs now triggers the web job"
---

### Session-105 (2026-11-20) — A script the web job runs now triggers the web job

`scripts/check-e2e-manifest.mjs` and `scripts/e2e-summary.mjs` were executed by `ci-web.yml` but named
by neither `paths` list, so a PR that edited one without its test ran no web CI (PR #92, head
`968f0cf6`: merge gate and Build Docs only). Both are now named in the `push` and `pull_request` lists,
next to `scripts/web-coverage-gate.mjs` and `scripts/tests/**` — the rule is "a script the job executes
is in the job's paths". `scripts/e2e-stack.sh` stays unnamed on purpose: it is the local equivalent of
the authenticated e2e job, and CI starts its own stack. `bash scripts/ci-merge-gate.sh --uncovered` now
reports 37 paths, 8 in the scripts class.
