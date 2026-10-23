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

Start the root Postgres service and create a dedicated auth role and schema:

```bash
cd ..
docker compose up -d postgres

docker compose exec postgres psql -U veldrath -d veldrath <<'SQL'
CREATE SCHEMA IF NOT EXISTS auth;

CREATE ROLE veldrath_auth LOGIN PASSWORD 'veldrath_auth_dev';
GRANT CONNECT ON DATABASE veldrath TO veldrath_auth;
-- Drizzle runs CREATE SCHEMA IF NOT EXISTS auth before migrating; that
-- statement requires CREATE on the database even if the schema exists.
GRANT CREATE ON DATABASE veldrath TO veldrath_auth;
GRANT USAGE, CREATE ON SCHEMA auth TO veldrath_auth;

-- Only this role gets the auth search_path. Do not change the database-wide
-- search_path or the shared `veldrath` role: EF Core still owns `public`.
ALTER ROLE veldrath_auth SET search_path = auth;
SQL
```

Then point `DATABASE_URL` in `web/client/.env` at that role:

```text
postgres://veldrath_auth:veldrath_auth_dev@localhost:5433/veldrath
```

This is safe on the existing Blazor database because the auth role cannot see
or modify the `public` tables that EF Core owns. Take a normal database backup
before running migrations anyway.

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

## 7. Docker first-boot verification

The production image builds from [`web/client/Dockerfile`](../web/client/Dockerfile)
with the `web/` build context. The runtime entrypoint:

1. Waits for Postgres.
2. Runs [`web/migrate.mjs`](../web/migrate.mjs), which creates the `auth` schema if needed.
3. Applies Drizzle migrations.
4. Starts adapter-node.

Verified with a fresh Postgres 17 container and the Postgres init script:

- The init script created the `veldrath_auth` role and the `auth` schema.
- The web entrypoint applied both migrations.
- The resulting tables were `auth.user`, `auth.session`, `auth.account`,
  `auth.verification`, and `auth.__drizzle_migrations`.
- `GET /` returned `200`.
- Restarting the web container re-ran the entrypoint and was idempotent.
- Running the same image against the existing database was a no-op because
  `auth.__drizzle_migrations` already existed.

The Coolify deployment file is [`docker-compose.coolify.yml`](../docker-compose.coolify.yml).
It has no Caddy service; Traefik labels route `veldrath.com` to `web:3000`,
`api.veldrath.com` to `server:8080`, and `foundry.veldrath.com` to
`foundry:8081`. The web container receives Traefik forwarded headers through
`PROTOCOL_HEADER`, `HOST_HEADER`, `ADDRESS_HEADER`, and `XFF_DEPTH`.

## 8. Verified results

The runbook was executed successfully on the existing Blazor database:

- Better Auth sign-up returned a GUID user ID and set the session cookie.
- `/api/game-token` returned a JWT with `sub`, `unique_name`, `iss`,
  `aud`, and `exp` matching the .NET server configuration.
- `GET /api/characters` with the token returned `200` and an empty list for
  the fresh user.
- `POST /hubs/game/negotiate` returned `200`.
- A real WebSocket connection received `ServerInfo` with the server version.
- The `auth` schema contains `user`, `session`, `account`, `verification`, and
  `__drizzle_migrations`; the 66 public EF Core tables were untouched.

Re-validation after the Docker, social-auth, and Caddy-removal changes:

- `npm run typecheck`, `npm run lint`, `npm run build`, and `npm run test`
  (Playwright) pass with zero errors.
- `npm run db:generate` reports no schema drift and
  `git diff --exit-code -- shared/db/migrations` is clean.
- `docker compose config --quiet` passes for the base, dev, prod overlay, and
  Coolify compose files.
- Discord OAuth login completes and returns a Better Auth session.
- Live runtime smoke test against `Veldrath.Server` (`localhost:9000`) and the
  SvelteKit dev server (`localhost:5173`):
  - `/api/game-token` minted a 356-character HS256 JWT from the session cookie.
  - `GET /api/characters` returned `200`.
  - `POST /hubs/game/negotiate` returned `200` with WebSockets offered.
  - `@microsoft/signalr` connected over WebSockets (`101`) and received
    `ServerInfo` with `serverVersion` `0.1`.
- Cookie note: Better Auth scopes the session cookie to the exact host used at
  login. Use `localhost` consistently, not `127.0.0.1`.

> **Additional verified result:** a fresh Postgres volume created the
> `veldrath_auth` role and `auth` schema through the init script, the web
> entrypoint applied Drizzle migrations on first boot, and a container restart
> was a no-op.

One important local-development detail: the root `.env` `JWT_KEY` overrides
`appsettings.Development.json` because `Veldrath.Server` loads the root `.env`
in Development. Copy that same value into `web/client/.env`. The checked-in
example uses the appsettings fallback and will not match if a different root
`.env` key is present.

## 9. Social auth

Better Auth is configured for Discord, Google, and Microsoft. The login and
register pages render a **Continue with Discord / Google / Microsoft** button
group.

Server env vars:

```ini
DISCORD_CLIENT_ID
DISCORD_CLIENT_SECRET
GOOGLE_CLIENT_ID
GOOGLE_CLIENT_SECRET
MICROSOFT_CLIENT_ID
MICROSOFT_CLIENT_SECRET
MICROSOFT_TENANT_ID   # optional; empty uses the common/consumers endpoint
```

The compose files map the existing `OAUTH_*` variables from the root `.env`
into these names for the web container. No duplicate OAuth app configuration
is required.

Callback URLs to register with each provider:

- Discord: `http://localhost:5173/api/auth/callback/discord`
- Google: `http://localhost:5173/api/auth/callback/google`
- Microsoft: `http://localhost:5173/api/auth/callback/microsoft`
- Production: `https://veldrath.com/api/auth/callback/{provider}`

Verified redirect targets:

- Discord: `discord.com/api/oauth2/authorize`
- Google: `accounts.google.com/o/oauth2/v2/auth`
- Microsoft: `login.microsoftonline.com/common/oauth2/v2.0/authorize`

All three received the correct `redirect_uri` and a non-empty `client_id`.
A real provider login still requires the callback URL to be registered in the
provider portal and a valid account.

## 10. Exit criteria

Phase 0 is complete when:

- `npm run typecheck`, `npm run lint`, and `npm run build` pass.
- A Better Auth user can register and sign in.
- `/api/game-token` returns a JWT that `Veldrath.Server` accepts.
- The browser can open `/hubs/game` with that token and receive `ServerInfo`.
- The Colyseus and TypeScript engine directories are gone from the workspace.
