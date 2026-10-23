# Veldrath Web

SvelteKit frontend for Veldrath, using Better Auth and Drizzle for web
authentication. The .NET game server remains the API and SignalR authority.

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

- Better Auth owns web credentials and sessions.
- SvelteKit mints short-lived JWTs for `Veldrath.Server`.
- `Veldrath.Server` remains the source of truth for game accounts, roles, and
  permissions during the migration.
- The full migration plan is in
  [`plans/sveltekit-migration.md`](../plans/sveltekit-migration.md).
