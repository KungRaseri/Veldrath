---
kind: work-item
date: 2026-11-20
area: web
title: "The authenticated e2e job was red: one failure, three traps"
---

### Session-108 (2026-11-20) — The authenticated e2e job was red: one failure, three traps

CI reported `CI - Web (SvelteKit) / Authenticated e2e (real server)` failing on `development` after ~3 minutes.
Reproduced with the isolated stack. Four defects: one was the failure, three were the reasons local reproduction
kept lying about it.

- **The failure: a spec that assumes a character arrives at a location.** `dialogue.e2e.ts` expected `Wayfarer Elder`
  the moment the wizard lands on `/play/{id}`. Every zone map chains `entry → first location → …`, so the square is a
  hop away and the People block lists nobody. The spec now walks there with `walkTo`, which moved to
  [`web/client/tests/support/map.ts`](../../../web/client/tests/support/map.ts) and is shared with `harvest.e2e.ts`.
  This is the trap the content pass already recorded for the Ashlen Wood; it is now a helper, not folklore.
- **`scripts/e2e-stack.sh` was committed without the executable bit** (mode 100644) while the docs and
  `upstream-failure.e2e.ts` both *execute* it. `execFileSync` therefore died with `EACCES` in 535 ms — and because that
  spec stops the API, the **next** run failed in a dozen unrelated places. Fixed with
  `git update-index --chmod=+x`. If a spec fails instantly with a spawn error, check the mode before the code.
- **The runner's gate read `process.env` rather than the manifest's defaults**, so `npm run test:e2e:stack` — the
  documented local command — refused to start, and CI (which exports both gates at job level) was the only place it
  worked. The gate now evaluates the *effective* env; asking for the refusal is an explicitly empty `E2E_STACK=`,
  which is what the tooling test now does.
- **`harvest.e2e.ts` asserted "no Open button remains"** after opening a chest. A location can hold a reopened
  authored chest beside a roaming one the sweeper placed, so the assertion was false on any database used twice — it
  passes on a fresh stack and fails on the second run, which is the worst shape a test defect can have. It now asserts
  what the comment always claimed: the list shrank.

Verified: the full `npm run test:e2e:stack` green twice in a row (22 specs + the outage leg), 23 tooling tests, web
lint clean. In CI the outage leg still self-skips — it needs an API the stack script started, and the job starts its
own — so the mode bug could never have shown up there.
- **`node --test` passing says nothing about `svelte-check`.** `web/client/tests/run-e2e.mjs` is type-checked (`checkJs`
  is on for the client tsconfig), so `env[gate]` on an object literal is a TS7053 implicit-any index error and the
  `Build and Test SvelteKit Web` job died in `typecheck` — while the 23 tooling tests that *execute* the same file were
  green. The env object is annotated `@type {Record<string, string | undefined>}` now. Two habits follow: run
  `npm run typecheck` after touching anything under `web/client/tests/`, and treat the tooling tests as a runtime
  check, never a compile check.
