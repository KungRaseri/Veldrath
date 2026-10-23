---
name: new-feature
description: Use for an end-to-end feature that may span multiple workspaces — from orientation and design through engine, shared contract, server, client UI, and final verification.
---

# New Feature

## When to use

- A feature touches more than one workspace (e.g., a new domain mechanic surfaced in the UI, a new API contract, or new persistence).
- You want a structured end-to-end workflow instead of jumping straight into code.

## When NOT to use

- A single-workspace change — use the matching skill directly (`sveltekit-development`, `ui-development`, `database-workflow`, `auth-setup`) rather than this orchestration flow.

## References

- [`repo-context`](../repo-context/SKILL.md) — orientation.
- [`database-workflow`](../database-workflow/SKILL.md) — if the schema changes.
- [`auth-setup`](../auth-setup/SKILL.md) — if authentication is involved.
- [`sveltekit-development`](../sveltekit-development/SKILL.md) — if the client changes.
- [`ui-development`](../ui-development/SKILL.md) — if the UI changes.
- [`verification`](../verification/SKILL.md) — always at the end.
- [`AGENTS.md`](../../../AGENTS.md) — the authoritative reference throughout.

## Steps

1. **Orient** — read `repo-context`, then `AGENTS.md`. Identify which workspaces the feature spans.
2. **Design** — decide where each piece belongs: logic → `engine/`, contracts/schema → `shared/`, transport/persistence → `server/`, UI → `client/`. Check `docs/` for architecture constraints.
3. **Engine** (if domain logic) — implement deterministically; add tests.
4. **Shared** (if contracts) — extend protocol/DTO/enums/constants; for schema changes use the `@your-scope/shared/db` subpath and `database-workflow`.
5. **Server** (if transport/persistence) — update WebSocket protocol handling or the persistence layer to expose the feature.
6. **Client** (if UI) — build routes/components following SvelteKit patterns (`sveltekit-development`); keep auth/DB code server-side.
7. **Verify** — run `verification`: typecheck, lint, tests, build across all affected workspaces.
8. **Docs** — update `docs/` if the architecture changed.

## Rules

- Keep dependency direction clean: `shared` ← `engine` ← `server`; client consumes `shared` via its package exports.
- Keep DB/auth construction lazy so builds stay DB-free.
- Keep the engine pure and deterministic (seeded RNG, in-memory state, no I/O/network); inject any non-deterministic behavior (e.g. server-side scheduling) outside it.
