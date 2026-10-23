# CI/CD Dashboard

The primary CI/CD dashboard is the **live per-run summary** rendered on every GitHub Actions
workflow run (the job's **Step summary** — see [`.github/workflows/node.yml`](workflows/node.yml)).
It shows build/typecheck/lint/test/e2e/coverage status, a per-workspace coverage table, and test
counts, and publishes downloadable artifacts (coverage HTML reports, the client build, the Playwright
report, and CI logs). This document is the static companion describing the pipeline.

## 🚀 Quick Status Overview

| Component | Status | Details |
|-----------|--------|---------|
| 🏗️ Build | [![Build](https://github.com/<owner>/<repo>/actions/workflows/node.yml/badge.svg)](https://github.com/<owner>/<repo>/actions/workflows/node.yml) | TypeScript monorepo — `tsc --noEmit` per workspace + client `vite build` |
| 🧪 Tests | — | Vitest suites (shared/engine/server/client) + Playwright e2e smoke |
| 📊 Coverage | — | Per-file/global thresholds in the workspace vitest configs |

> Replace `<owner>/<repo>` with the actual GitHub repository to enable the workflow status badge.
> Fill in the current test/coverage numbers once your suites exist.

## 🔄 CI/CD Pipeline (Node CI)

**Triggers:** Push to `main`, Pull Requests to `main`.
**Location:** `.github/workflows/node.yml`.
**Runner:** `ubuntu-latest` + a `postgres:16-alpine` service (db `yourproject`, port 5432) with the
job `env` (`DATABASE_URL`, `BETTER_AUTH_SECRET`, `ORIGIN`, `WS_AUTH_SECRET`, `WS_URL`) so the DB-gated
server tests and the client Playwright webServer can run in CI.

Steps (each check uses `continue-on-error` so every result is collected; the **Summarize** step marks
the run red if any gate failed):
1. 🛒 Checkout → 🔧 Setup Node (22, npm cache) → 📦 `npm ci` → 🗂️ `mkdir -p ci-results`
2. 🏗️ **Build** → 🏗️ **Typecheck** (incl. svelte-check) → 🔎 **Lint** (ESLint + Prettier)
3. 🗄️ **DB migrations** (`npm run db:migrate`)
4. 🧪 **Tests** per workspace (Vitest JSON results into `ci-results/*.json`): shared → engine →
   server → client (unit)
5. 🌐 **Playwright browsers** (`install --with-deps chromium`) → 🧪 **e2e** (`playwright test`,
   `github,html` reporters → `client/playwright-report/`)
6. 📊 **Coverage** (`npm run coverage`; text + HTML per workspace)

The per-step outcomes are passed as `name=value` args to
[`scripts/ci-summary.mjs`](../scripts/ci-summary.mjs), which renders the markdown summary
(`$GITHUB_STEP_SUMMARY`) and exits non-zero if any gated step failed.

## 📦 Artifacts

Every run uploads whatever exists:
- `coverage-reports` — HTML coverage reports (shared/engine/server)
- `client-build` — the SvelteKit build output
- `playwright-report` — the e2e HTML report
- `ci-results` — the raw per-workspace JSON/log results
