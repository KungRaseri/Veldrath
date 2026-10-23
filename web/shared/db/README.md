# Auth database

Better Auth tables live in a dedicated Postgres schema so they cannot collide
with the EF Core tables in the `public` schema.

## One-time database setup

Run as a Postgres administrator:

```sql
CREATE SCHEMA IF NOT EXISTS auth;

CREATE ROLE veldrath_auth LOGIN PASSWORD 'replace-me';
ALTER ROLE veldrath_auth SET search_path = auth, public;

GRANT USAGE, CREATE ON SCHEMA auth TO veldrath_auth;
```

Set `DATABASE_URL` in `client/.env` to use that role. The repository root
compose maps Postgres to `localhost:5433`; the web-only compose maps it to
`localhost:5432`:

```text
DATABASE_URL=postgres://veldrath_auth:replace-me@localhost:5433/veldrath
```

Because the role's `search_path` starts with `auth`, the generated Better Auth
tables (`user`, `session`, `account`, `verification`) and Drizzle's migration
table are created in the `auth` schema.

## Migrations

```bash
npm run db:generate
npm run db:migrate
```

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
