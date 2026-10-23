# Phase 0 Runbook — SvelteKit Foundation

This runbook completes the structural move and verifies the three hard parts:
Better Auth login, SvelteKit-issued JWT acceptance, and browser SignalR
connectivity.

## 1. Move and trim the workspace

Run from the repository root:

```bash
git mv Veldrath.Web.Sveltekit web

git rm -r web/server
git rm -r web/engine
git rm -r web/shared/src/schema
git rm web/shared/src/ticket.ts
git rm web/shared/src/ticket.test.ts
git rm -r web/llms/colyseus
```

`web/package.json` now lists only the `shared` and `client` workspaces. The
Colyseus and TypeScript engine packages are no longer installed or tested.

## 2. Install and configure

```bash
cd web
npm install --allow-git=all
cp client/.env.example client/.env
```

Edit `client/.env`:

- `DATABASE_URL` — use the repository root Postgres on port `5433`:
  `postgres://veldrath:veldrath_dev@localhost:5433/veldrath`
- `BETTER_AUTH_SECRET` — generate with `openssl rand -base64 32`
- `JWT_KEY` — the development value in
  `Veldrath.Server/appsettings.Development.json`
- `JWT_ISSUER` — `Veldrath.Server`
- `JWT_AUDIENCE` — `Veldrath.Client`
- `ORIGIN` and `BETTER_AUTH_URL` — `http://localhost:5173`
- `PUBLIC_API_URL` — `http://localhost:9000`
- `PUBLIC_HUB_URL` — `http://localhost:9000/hubs/game`

## 3. Prepare the auth schema

Start the root Postgres service and create the dedicated schema:

```bash
cd ..
docker compose up -d postgres

docker compose exec postgres psql -U veldrath -d veldrath \
  -c "CREATE SCHEMA IF NOT EXISTS auth;"

docker compose exec postgres psql -U veldrath -d veldrath \
  -c "ALTER DATABASE veldrath SET search_path TO auth, public;"
```

If the database already has active connections and the `ALTER DATABASE` fails,
use a dedicated auth role instead:

```sql
CREATE ROLE veldrath_auth LOGIN PASSWORD 'replace-me';
ALTER ROLE veldrath_auth SET search_path = auth, public;
GRANT USAGE, CREATE ON SCHEMA auth TO veldrath_auth;
```

Then point `DATABASE_URL` at that role.

## 4. Generate and apply migrations

```bash
cd web
npm run db:generate
npm run db:migrate
```

## 5. Verify the web build

```bash
npm run typecheck
npm run lint
npm run build
npm run test
```

The expected failures at this stage are Better Auth API type mismatches. Paste
the errors and they can be corrected before moving on.

## 6. Smoke test auth, JWT, and SignalR

Terminal 1 — game server:

```bash
dotnet run --project Veldrath.Server
```

Terminal 2 — SvelteKit:

```bash
cd web
npm run dev
```

Open `http://localhost:5173` and:

1. Create an account at `/register`.
2. Confirm the redirect to `/game`.
3. Click **Mint game token** and confirm a token prefix is shown.
4. Click **Connect to SignalR** and confirm the `ServerInfo` message appears.
5. Click **Sign out** and confirm the cookie session clears.

The .NET server's existing `AllowLocalClient` CORS policy already allows
`http://localhost:5173`, so the SignalR connection should work without a server
change. Production origins will be added in Phase 1.

## 7. Exit criteria

Phase 0 is complete when:

- `npm run typecheck`, `npm run lint`, and `npm run build` pass.
- A Better Auth user can register and sign in.
- `/api/game-token` returns a JWT that `Veldrath.Server` accepts.
- The browser can open `/hubs/game` with that token and receive `ServerInfo`.
- The Colyseus and TypeScript engine directories are gone from the workspace.
