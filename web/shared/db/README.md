# Auth database

Better Auth tables live in a dedicated Postgres schema so they cannot collide
with the EF Core tables in the `public` schema.

## Recommended: dedicated role

Run as a Postgres administrator (the `veldrath` user created by the root
`docker-compose.yml` is a superuser):

```sql
CREATE SCHEMA IF NOT EXISTS auth;

CREATE ROLE veldrath_auth LOGIN PASSWORD 'replace-me';
GRANT CONNECT ON DATABASE veldrath TO veldrath_auth;
-- Drizzle runs CREATE SCHEMA IF NOT EXISTS auth before migrating; that
-- statement requires CREATE on the database even if the schema exists.
GRANT CREATE ON DATABASE veldrath TO veldrath_auth;
GRANT USAGE, CREATE ON SCHEMA auth TO veldrath_auth;

-- Only this role gets the auth search_path. Do not change the database-wide
-- search_path or the shared `veldrath` role: EF Core and the existing game
-- tables remain in `public`.
ALTER ROLE veldrath_auth SET search_path = auth;
```

Set `DATABASE_URL` in `client/.env` to use that role. The repository root
compose maps Postgres to `localhost:5433`; the web-only compose maps it to
`localhost:5432`:

```text
DATABASE_URL=postgres://veldrath_auth:replace-me@localhost:5433/veldrath
```

Because the role's `search_path` is `auth`, Better Auth tables and Drizzle's
migration table are created in `auth`.

## Why this is safe on the existing Blazor database

- Better Auth uses `user`, `session`, `account`, and `verification`; those
  names are isolated in the `auth` schema. The Blazor setup uses
  `AspNetUsers`, `AspNetRoles`, characters, and the other EF Core tables in
  `public`.
- The auth role has no privileges on `public`, so a bad migration cannot drop
  or alter game tables.
- The SvelteKit process never issues raw SQL against `public`.
- The .NET server continues to use the `veldrath` connection string unchanged.

## Migrations

```bash
npm run db:generate
npm run db:migrate
```

Use `generate` + `migrate`, not `db:push`, against the shared database.
`drizzle-kit push` is convenient for throwaway databases, but it is the wrong
tool when the database also contains EF Core-owned tables.

`drizzle-kit` loads `client/.env` from the `shared` workspace, so the root
commands work without exporting `DATABASE_URL`.

## Regenerating the Better Auth schema

After upgrading Better Auth:

```bash
npm run auth:schema --workspace client
npm run db:generate
npm run db:migrate
```

The regeneration writes `shared/src/db/auth.schema.ts`. Do not hand-edit
generated columns beyond app-specific additions unless you also update the
Better Auth configuration.

The checked-in schema intentionally wraps the generated tables in
`pgSchema("auth")`. The `auth generate` CLI emits `pgTable` definitions without
schema qualification, so after regenerating, re-apply the `pgSchema("auth")`
wrapper and the `username`/`display_username` additions before generating the
next Drizzle migration.
