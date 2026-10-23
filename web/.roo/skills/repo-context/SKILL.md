---
name: repo-context
description: Use when starting any work in this repository or when you need to orient yourself — project structure, workspaces, commands, and conventions.
---

# Repo Context

## When to use

- Beginning a new task in the repository.
- Unsure which workspace holds a given piece of code.
- Need the correct command to build, test, typecheck, or lint a change.
- Reading unfamiliar code and want the architecture in one place.

## When NOT to use

- Task-specific workflows — pick the matching skill (`database-workflow`, `auth-setup`, `sveltekit-development`, `ui-development`, `new-feature`) once you know which workspace the work touches.

## References

- [`AGENTS.md`](../../../AGENTS.md) — authoritative reference: repo layout, commands, env, DB/auth workflow, conventions.
- [`README.md`](../../../README.md) — project overview.
- [`docs/`](../../../docs) — architecture and design documentation.
- [`package.json`](../../../package.json) (root) — workspaces list and root scripts.
- [`shared/package.json`](../../../shared/package.json), [`engine/package.json`](../../../engine/package.json), [`server/package.json`](../../../server/package.json), [`client/package.json`](../../../client/package.json) — per-workspace scripts and deps.

## Key facts

- **npm-workspaces monorepo** (root `your-project`): workspaces are `shared`, `engine`, `server`, `client`. Install once at root with `npm install`.
- `shared/` — shared contracts (protocol types, DTOs, enums, constants) **and the single Drizzle schema**. Subpath exports: `@your-scope/shared/db`, `@your-scope/shared/schema`.
- `engine/` — pure deterministic TypeScript logic engine; **no I/O, no network**; depends only on `@your-scope/shared`.
- `server/` — Colyseus server hosting authoritative WebSocket sessions in rooms; Postgres persistence via `pg`.
- `client/` — SvelteKit app (Svelte 5 runes, Tailwind 4, adapter-node, Better Auth).
- `docs/` — architecture and design documentation; keep in sync when the architecture changes.
- Env: per-process `.env` files — `client/.env` and `server/.env` (git-ignored; templates `client/.env.example` and `server/.env.example`); shared values (`DATABASE_URL`, `WS_AUTH_SECRET`) match in both.

## Steps

1. Read `AGENTS.md` in full — it is the authoritative reference for the repo.
2. Identify the workspace(s) involved in the task.
3. Use root scripts for cross-workspace actions (`npm run build/typecheck/test/lint`), or `npm run <script> --workspace <name>` for a single workspace.
4. Follow the **Conventions** section of `AGENTS.md` for every change (strict TS + ESM, run typecheck/lint/tests before finishing).

## Rules

- Each workspace owns its env file (`client/.env`, `server/.env`); keep shared values (`DATABASE_URL`, `WS_AUTH_SECRET`) in sync across them.
- Keep DB/auth construction lazy (`getAuthDb()`/`getAuth()`) so builds stay DB-free.
