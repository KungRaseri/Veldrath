# Deployment — Coolify + Traefik

Veldrath deploys as a Docker Compose stack managed by **Coolify**. Coolify's
built-in Traefik handles TLS termination, host routing, and forwarded headers.
There is **no Caddy** in the deployment path.

## Stack

| Service | Image / Dockerfile | Internal port | Public host |
|---|---|---|---|
| `server` | `ghcr.io/kungraseri/veldrath-server:latest` | 8080 | `api.veldrath.com` |
| `web` | `ghcr.io/kungraseri/veldrath-web:latest` (built from [`web/client/Dockerfile`](../web/client/Dockerfile)) | 3000 | `veldrath.com` |
| `foundry` | `ghcr.io/kungraseri/realmfoundry:latest` | 8081 | `foundry.veldrath.com` |
| `discord` | `ghcr.io/kungraseri/veldrath-discord:latest` | 8083 | none |
| `postgres` | Coolify-managed Postgres | 5432 | none |

The Coolify deployment file is [`docker-compose.coolify.yml`](../docker-compose.coolify.yml).
It is a single, self-contained compose file with Traefik labels and no host port
mappings.

## Required Coolify secrets

| Variable | Used by | Notes |
|---|---|---|
| `POSTGRES_HOST` / `POSTGRES_PORT` / `POSTGRES_DB` / `POSTGRES_USER` / `POSTGRES_PASSWORD` | server | Builds the .NET connection string. |
| `DATABASE_URL` | web | Better Auth/Drizzle connection. Must be able to create the `auth` schema on first boot. |
| `JWT_KEY` | server + web | Shared HMAC signing key, at least 32 chars. |
| `BETTER_AUTH_SECRET` | web | Better Auth session secret, at least 32 chars. |
| `DISCORD_TOKEN` | discord | Bot token. |
| `EMAIL_SMTP_HOST` / `EMAIL_USER` / `EMAIL_PASSWORD` | server | Outbound email. |
| `SEQ_ADMIN_PASSWORD` / `GF_ADMIN_PASSWORD` | seq / grafana | Admin passwords. |

Optional OAuth provider credentials:

```ini
OAUTH_DISCORD_CLIENT_ID
OAUTH_DISCORD_CLIENT_SECRET
OAUTH_GOOGLE_CLIENT_ID
OAUTH_GOOGLE_CLIENT_SECRET
OAUTH_MICROSOFT_CLIENT_ID
OAUTH_MICROSOFT_CLIENT_SECRET
```

## First boot and migrations

The two applications own different schemas:

- **Veldrath.Server** applies its EF Core migrations to the `public` schema at
  startup.
- **Veldrath.Web** runs Drizzle auth migrations before adapter-node starts.

The web entrypoint in [`web/client/docker-entrypoint.sh`](../web/client/docker-entrypoint.sh):

1. Waits for the auth database to accept connections.
2. Runs [`web/migrate.mjs`](../web/migrate.mjs), which creates the `auth` schema
   if needed and applies every pending Drizzle migration.
3. Starts the SvelteKit server.

Drizzle and Better Auth tables are schema-qualified (`auth.user`,
`auth.session`, `auth.account`, `auth.verification`). The migration bookkeeping
table is `auth.__drizzle_migrations`. Runtime and `drizzle-kit` use the same
table, so repeated container restarts are idempotent.

For a Coolify-managed Postgres, the `DATABASE_URL` user must have `CREATE` on
the database. The owner user created by Coolify normally has this. If you use a
separate least-privilege role, create it and grant:

```sql
CREATE SCHEMA IF NOT EXISTS auth;
GRANT CONNECT ON DATABASE veldrath TO veldrath_auth;
GRANT CREATE ON DATABASE veldrath TO veldrath_auth;
GRANT USAGE, CREATE ON SCHEMA auth TO veldrath_auth;
ALTER ROLE veldrath_auth SET search_path = auth;
```

The local compose stack automates this with
[`config/postgres/init-auth-schema.sh`](../config/postgres/init-auth-schema.sh),
which Postgres runs on a fresh volume. Existing volumes need the SQL run once.

## Deploying

1. Push the SvelteKit web image to GHCR. The [`ci-web.yml`](../.github/workflows/ci-web.yml)
   workflow builds `web/client/Dockerfile` from the `web/` context and pushes
   `ghcr.io/kungraseri/veldrath-web:latest`.
2. In Coolify, create or update the Docker Compose application pointing at
   [`docker-compose.coolify.yml`](../docker-compose.coolify.yml).
3. Set the required secrets listed above.
4. Deploy. Traefik routes:
   - `veldrath.com` → `web:3000`
   - `api.veldrath.com` → `server:8080`
   - `foundry.veldrath.com` → `foundry:8081`
5. Confirm health:
   - `https://veldrath.com/`
   - `https://api.veldrath.com/health`
   - `https://foundry.veldrath.com/health`

## Reverse proxy

No Caddy. The SvelteKit container trusts Coolify's Traefik forwarding headers:

```ini
PROTOCOL_HEADER=x-forwarded-proto
HOST_HEADER=x-forwarded-host
ADDRESS_HEADER=x-forwarded-for
XFF_DEPTH=1
```

Set `ORIGIN` and `BETTER_AUTH_URL` to the public HTTPS origin. The .NET server
also trusts forwarded headers through `UseForwardedHeaders`.

## Local deployment

- Local development: [`docker-compose.yml`](../docker-compose.yml) with
  `docker compose up -d --build`.
- Production-like local: [`docker-compose.prod.yml`](../docker-compose.prod.yml)
  as an overlay, with an external reverse proxy on the Docker network.
- Coolify: [`docker-compose.coolify.yml`](../docker-compose.coolify.yml) only.

The old Blazor `Veldrath.Web` container and Caddy configuration are retired.
