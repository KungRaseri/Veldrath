---
name: auth-setup
description: Use when working with authentication in this repo — Better Auth configuration, sessions, protected routes, auth UI, or regenerating the shared auth schema.
---

# Auth Setup

## When to use

- Adding or editing the Better Auth configuration or plugins.
- Protecting routes or reading the session server-side.
- Building auth UI (login/signup) or demo routes.
- Regenerating the shared auth schema after a Better Auth upgrade.

## When NOT to use

- Schema changes unrelated to the auth tables — use `database-workflow`.

## References

- [`llms/betterauth/llms.txt`](../../../llms/betterauth/llms.txt) — Better Auth docs (in-repo reference):
  - Setup: `Installation`, `Basic Usage`.
  - Adapter: `Adapters` → `Drizzle ORM Adapter`, `PostgreSQL` (this repo uses `drizzleAdapter(..., { provider: 'pg' })`).
  - Concepts: `Database` (core schema `user`/`session`/`account`/`verification`), `Session Management`, `CLI` (`auth generate`/`migrate`), `Cookies`, `API`, `Hooks`, `Plugins`, `TypeScript`.
  - Integration: `Integrations` → `SvelteKit Integration`, `Examples` → `SvelteKit Example`.
  - Options: `Reference` → `Options`.
- [`AGENTS.md`](../../../AGENTS.md) → Auth architecture, Database workflow.
- [`client/src/lib/server/auth.ts`](../../../client/src/lib/server/auth.ts) — lazy `getAuth()`; drizzle adapter `provider: 'pg'`, email/password enabled, `sveltekitCookies` plugin.
- [`client/src/hooks.server.ts`](../../../client/src/hooks.server.ts) — `handle` → `svelteKitHandler`; sets `event.locals.user` / `event.locals.session`.
- [`client/src/app.d.ts`](../../../client/src/app.d.ts) — `App.Locals.user?: User`, `session?: Session` types.
- [`client/src/routes/(public)/login/`](../../../client/src/routes/(public)/login/) + `(public)/register/` — working auth wiring (server + page).
- [`shared/src/db/auth.schema.ts`](../../../shared/src/db/auth.schema.ts) — `user`, `session`, `account`, `verification` tables.

## Key facts (grounded in `llms/betterauth/llms.txt`)

- **Core schema** = `user`, `session`, `account`, `verification` — exactly the tables in `shared/src/db/auth.schema.ts`.
- **Drizzle adapter**: `drizzleAdapter(db, { provider: 'pg' })` over the shared-schema Drizzle client (`getAuthDb()`).
- **CLI**: `auth generate` / `auth migrate`; this repo's `auth:schema` script regenerates the shared auth schema (`npm run auth:schema --workspace client`).
- **SvelteKit integration**: `svelteKitHandler` in hooks + the `sveltekitCookies` plugin (must remain the last plugin).
- **Server-side session**: `auth.api.getSession(...)` in hooks populates `event.locals.session` / `event.locals.user`.

## Steps

1. Read `AGENTS.md` → Auth architecture before changing anything; consult the relevant `llms/betterauth/llms.txt` section for the feature you're adding.
2. Server-side session access: read `event.locals.session` / `event.locals.user` (typed in `app.d.ts`); set by `hooks.server.ts`.
3. Use `client/src/routes/(public)/login/` + `(public)/register/` as the reference pattern for login pages and session reads.
4. To change auth schema after a Better Auth upgrade: `npm run auth:schema --workspace client`, then `npm run db:generate` and `npm run db:migrate` from the root.
5. Verify with `npm run check --workspace client` and `npm run build --workspace client`.

## Rules

- Keep `getAuth()` lazy — never construct at module scope (the build must stay DB-free).
- `sveltekitCookies` must stay the **last** plugin in the plugins array.
- Auth tables live in the **shared** schema, not in client — edit/regenerate there.
- Server-only code stays in `client/src/lib/server`.
