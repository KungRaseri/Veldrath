---
name: verification
description: Use before finishing ANY change to this repo — run typecheck, lint, tests, and build to confirm nothing is broken across the workspaces.
---

# Verification

## When to use

- Before declaring any task complete.
- After any change to `shared`, `engine`, `server`, or `client`.
- Whenever you are unsure whether a change compiled or broke tests.

## When NOT to use

- As a substitute for the workflow skills — verify at the END of a change session, after the actual change work (per `AGENTS.md` conventions, batch all edits first, then run these gates once).

## References

- [`llms/vitest/llms-full.txt`](../../../llms/vitest/llms-full.txt) — Vitest docs (in-repo reference):
  - `Getting Started`, `Configuring Vitest`, `Command Line Interface`
  - `Coverage` (per-workspace coverage runs)
  - `Mocking` (functions, modules, requests)
- [`llms/vite/llms-full.txt`](../../../llms/vite/llms-full.txt) — Vite docs (in-repo reference):
  - `Configuring Vite`, `Building for Production`, `Features` (HMR)
- [`AGENTS.md`](../../../AGENTS.md) → Commands, Conventions.
- Root [`package.json`](../../../package.json) — script definitions.
- [`client/vite.config.ts`](../../../client/vite.config.ts) — the client vitest settings (`jsdom`, `expect.requireAssertions`, setup files).

## Steps

Run from the repo root (each forwards `--workspaces --if-present`):

1. `npm run typecheck` — `tsc --noEmit` per workspace. For the client also run `npm run check --workspace client` (svelte-check).
2. `npm run lint` — ESLint per workspace (client also runs `prettier --check`).
3. `npm run test` — Vitest per workspace:
   - `engine` / `shared` / `server`: `vitest run` (unit + integration).
   - `client`: `vitest` with jsdom via `client/vite.config.ts`. `npm run test:unit --workspace client` runs them; `npm run test --workspace client` additionally runs Playwright e2e.
   - Caveat: Vitest does **not** enforce SvelteKit's server-only import rule (SvelteKit disables it under `process.env.TEST === 'true'`), so server-only leaks can pass unit tests.
4. `npm run build` — `tsc --noEmit` per workspace; client runs `vite build` (see `llms/vite/llms-full.txt` → `Building for Production`).
5. If formatting is affected: `npm run format:check` (or `npm run format` to fix), then re-check.

## Rules

- Fix all failures before finishing — do not leave a workspace red.
- If a change altered formatting, run `npm run format` (or `npm run format:check` in CI) so Prettier stays consistent.
- For DB-related changes, confirm migrations are generated/committed (see `database-workflow`).
