# Veldrath Web

The SvelteKit app for Veldrath — **the only game client** and the whole web frontend (public site,
auth, game, Foundry, editorial, admin, moderation). Better Auth and Drizzle own authentication;
`Veldrath.Server` remains the game API and SignalR authority.

## Workspaces

| Package | Role |
| --- | --- |
| [`shared/`](shared) — `@veldrath/shared` | Better Auth Drizzle schema and generated TypeScript contracts. |
| [`client/`](client) — `@veldrath/web` | SvelteKit app: public site, auth, game client, Foundry, admin, moderation, and editorial routes. |

## Commands

```bash
npm install
npm run dev
npm run typecheck
npm run lint
npm run test
npm run build
```

Database commands (from the repo root):

```bash
npm run db:generate
npm run db:migrate
npm run db:push
npm run db:studio
```

See [`shared/db/README.md`](shared/db/README.md) for the dedicated `auth`
schema and database role setup.

## Architecture

- **Better Auth is the single source of truth** for credentials and sessions.
- SvelteKit mints short-lived game JWTs; `Veldrath.Server` validates them, provisions the account
  just in time, and adds RBAC claims from Identity roles/permissions.
- Verification and password-reset mail is relayed to the .NET server's SMTP transport
  ([`client/src/lib/server/email.ts`](client/src/lib/server/email.ts)).
- Design decisions and history: [`docs/client-ui-architecture.md`](../docs/client-ui-architecture.md)
  and [`plans/sveltekit-migration.md`](../plans/sveltekit-migration.md).
