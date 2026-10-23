---
name: database-workflow
description: Use when changing the Drizzle schema, generating or applying migrations, regenerating auth tables, or debugging DB/env issues across shared, client, and server.
---

# Database Workflow

## When to use

- Adding, altering, or removing tables/columns in the shared schema.
- Generating or applying migrations.
- Debugging `DATABASE_URL` / `.env` problems.
- Regenerating the Better Auth tables after a Better Auth upgrade.

## When NOT to use

- UI or auth feature work that does not touch the schema — pick the matching skill instead.
- Server transport/room logic that does not touch the schema — that is `sveltekit-development`/app-specific work.

## References

- [`llms/drizzle/llms.txt`](../../../llms/drizzle/llms.txt) — Drizzle ORM + drizzle-kit docs (in-repo reference):
  - Get started: `Get started with Drizzle and PostgreSQL` (relevant only; the rest of the file covers other DBs).
  - Schema declaration: `pg/sql-schema-declaration` (what `shared/src/db/schema.ts` uses).
  - Relations: `pg/relations-schema-declaration` (used in `shared/src/db/auth.schema.ts`).
  - Queries + CRUD: `Drizzle Queries + CRUD`.
  - Migrations: `Migrations` → `drizzle-kit generate` / `migrate` / `push` / `pull`, `Drizzle Kit configuration file`.
  - Validation: `Validations` → zod (only when schema validation is added).
- [`AGENTS.md`](../../../AGENTS.md) → Database workflow, Environment & setup.
- [`shared/src/db/schema.ts`](../../../shared/src/db/schema.ts) — single source of truth (driver-agnostic `drizzle-orm/pg-core`).
- [`shared/src/db/auth.schema.ts`](../../../shared/src/db/auth.schema.ts) — Better Auth tables (`user`, `session`, `account`, `verification`).
- [`shared/src/db/index.ts`](../../../shared/src/db/index.ts) — `@your-scope/shared/db` subpath export.
- [`shared/drizzle.config.ts`](../../../shared/drizzle.config.ts) — loads `../server/.env` (fallback `../client/.env`) via `loadEnvFile`; dialect `postgresql`, out `shared/db/migrations`.
- [`client/src/lib/server/db/index.ts`](../../../client/src/lib/server/db/index.ts) — lazy `getAuthDb()` (postgres.js driver).

## Steps

1. Edit the schema in `shared/src/db/schema.ts` (which re-exports `auth.schema.ts` and your own tables). Consult `llms/drizzle/llms.txt` → `pg/sql-schema-declaration` for table/column syntax.
2. For relations, follow `pg/relations-schema-declaration` (the pattern already used in `auth.schema.ts`).
3. From the repo root: `npm run db:generate` → creates a migration under `shared/db/migrations/`.
4. Apply it: `npm run db:migrate` (or `npm run db:push` for dev-time schema sync).
5. After upgrading Better Auth, regenerate its tables: `npm run auth:schema --workspace client`.
6. If `DATABASE_URL` errors: confirm it is set in `server/.env` and `client/.env` (both git-ignored) — the value must match in both files.

## Rules

- The schema stays **driver-agnostic** — never import `pg`/`postgres` into `shared/src/db/*`.
- Workspaces import the schema from the `@your-scope/shared/db` subpath, never the package root, so protocol types stay free of Drizzle dependencies.
- Client uses postgres.js; server uses `pg`; table definitions must never drift (both build from the shared schema).
- Keep `getAuthDb()` lazy — do not construct the DB client at module scope (build must stay DB-free).
- `DATABASE_URL` lives in both `client/.env` (auth connection) and `server/.env` (app DB); `shared/drizzle.config.ts` and server `main()` load them automatically.
