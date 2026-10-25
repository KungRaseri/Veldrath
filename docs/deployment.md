# Deployment — Coolify + Traefik

Veldrath deploys as a Docker Compose stack managed by **Coolify**. Coolify's
built-in Traefik handles TLS termination, host routing, and forwarded headers.
There is **no Caddy** in the deployment path.

## Stack

Coolify runs **one Dockerfile-based resource per service**; it builds each image
from this repository. Compose files are for local development only.

| Service | Coolify base / Dockerfile | Internal port | Public host |
|---|---|---|---|
| `server` | `/` / [`Veldrath.Server/Dockerfile`](../Veldrath.Server/Dockerfile) | 8080 | `api.veldrath.com` |
| `web` | `/web` / [`web/client/Dockerfile`](../web/client/Dockerfile) | 8081 | `veldrath.com` |
| `discord` | `/` / [`Veldrath.Discord/Dockerfile`](../Veldrath.Discord/Dockerfile) | 8083 | none |
| `postgres` | Coolify-managed Postgres | 5432 | none |

For the web resource set the Coolify fields to:

- **Base Directory:** `/web`
- **Dockerfile Location:** `/client/Dockerfile`
- **Port:** `8081`
- **Health check:** `http://localhost:8081/`
- **Build args:** `PUBLIC_API_URL=https://api.veldrath.com`,
  `PUBLIC_HUB_URL=https://api.veldrath.com/hubs/game`

SvelteKit resolves public env vars in two places: it inlines them into the browser
bundle at build time, and it validates them from `process.env` when `node build`
starts. [`web/client/Dockerfile`](../web/client/Dockerfile) handles both — the
runtime stage re-declares the build args as `ENV` defaults, so passing only the
build args above is enough to boot. If you also set `PUBLIC_API_URL` /
`PUBLIC_HUB_URL` as runtime environment variables (optional), they must match the
build args value-for-value; a mismatch leaves SSR pointing at a different host
than the browser bundle.

## Required Coolify secrets

| Variable | Used by | Notes |
|---|---|---|
| `POSTGRES_HOST` / `POSTGRES_PORT` / `POSTGRES_DB` / `POSTGRES_USER` / `POSTGRES_PASSWORD` | server | Builds the .NET connection string. |
| `DATABASE_URL` | web | Better Auth/Drizzle connection. Must be able to create the `auth` schema on first boot. |
| `Web__BaseUrl` | server | Public site origin (`https://veldrath.com`); OAuth return URLs, email links, default SignalR CORS origin. Must equal the web's `ORIGIN` / `BETTER_AUTH_URL`. |
| `Auth__CookieDomain` | server | `.veldrath.com` — cross-subdomain session cookie. **Required**: the server throws at startup when `ASPNETCORE_ENVIRONMENT=Production` and this is missing. |
| `JWT_KEY` | server + web | Shared HMAC signing key, at least 32 chars. Must be **byte-identical** on both resources — the web mints game tokens with it and the server validates them, so any drift surfaces as `401` on `/api/characters` (see [the troubleshooting steps](coolify-deployment.md#troubleshooting-401-from-the-game-api)). |
| `BETTER_AUTH_SECRET` | web | Better Auth session secret, at least 32 chars. |
| `INTERNAL_BRIDGE_TOKEN` | server + web | Shared secret for the internal SvelteKit → server transactional-email relay (the server owns SMTP). |
| `DISCORD_TOKEN` | discord | Bot token. |
| `EMAIL_SMTP_HOST` / `EMAIL_USER` / `EMAIL_PASSWORD` | server | Outbound email. |
| `SEQ_ADMIN_PASSWORD` / `GF_ADMIN_PASSWORD` | seq / grafana | Admin passwords. |

Optional OAuth provider credentials, shared by the .NET server and Better Auth:

```ini
OAUTH_DISCORD_CLIENT_ID
OAUTH_DISCORD_CLIENT_SECRET
OAUTH_GOOGLE_CLIENT_ID
OAUTH_GOOGLE_CLIENT_SECRET
OAUTH_MICROSOFT_CLIENT_ID
OAUTH_MICROSOFT_CLIENT_SECRET
OAUTH_MICROSOFT_TENANT_ID
```

Register these redirect URIs with each provider:

- Discord: `https://veldrath.com/api/auth/callback/discord`
- Google: `https://veldrath.com/api/auth/callback/google`
- Microsoft: `https://veldrath.com/api/auth/callback/microsoft`

For local development use `http://localhost:5173/api/auth/callback/{provider}`.
The compose files map the `OAUTH_*` variables into the web container's
`DISCORD_CLIENT_ID`, `GOOGLE_CLIENT_ID`, and `MICROSOFT_CLIENT_ID` variables.

## First boot and migrations

The two applications own different schemas:

- **Veldrath.Server** applies its EF Core migrations to the `public` schema at
  startup.
- **The SvelteKit web app** runs Drizzle auth migrations before adapter-node starts.

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

## Migrating existing accounts

Copy existing ASP.NET Core Identity accounts into Better Auth before cutover:

```bash
MIGRATION_DATABASE_URL="postgres://user:pass@host:5432/veldrath" \
  node web/scripts/migrate-identity-accounts.mjs --dry-run

# Re-run without --dry-run to apply
```

The script:

- Copies `AspNetUsers` → `auth.user` using the same GUID.
- Copies `AspNetUsers.PasswordHash` → `auth.account`
  (`provider_id = 'credential'`, `issuer = 'local:credential'`).
- Copies `AspNetUserLogins` → `auth.account` with
  `issuer = 'local:oauth:{provider}'`.
- Is idempotent and leaves existing Better Auth users untouched.

Existing Identity PBKDF2 password hashes keep working because Better Auth uses the
dual-format verifier in
[`web/client/src/lib/server/auth-password.ts`](../web/client/src/lib/server/auth-password.ts).
A password change writes a Better Auth scrypt hash.

The migration role must be able to read `public."AspNetUsers"` and write `auth.*`;
the restricted `veldrath_auth` role cannot, which is why `MIGRATION_DATABASE_URL`
uses the owner/admin connection. Keep `AspNetUsers` while the desktop client's
native sign-in and the internal identity bridge still use ASP.NET Core Identity.

## Deploying

1. Create one Coolify **Dockerfile** resource per service (see the table above).
2. Point each resource at this repository and set its base directory, Dockerfile
   location, container port, health check, and environment variables.
3. For the web resource set `PUBLIC_API_URL` and `PUBLIC_HUB_URL` as build args.
   The image already carries them as runtime `ENV` defaults (SvelteKit validates
   public vars at startup), so no runtime duplicates are required — only add them
   at runtime if you must and keep the values identical.
4. Set the required secrets listed above and deploy. Traefik routes:
   - `veldrath.com` → `web:8081`
   - `api.veldrath.com` → `server:8080`
5. Confirm health:
   - `https://veldrath.com/`
   - `https://api.veldrath.com/health`

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
  `docker compose up -d --build` (web on `http://localhost:8081`).
- Hot-reload web development: `docker compose -f docker-compose.dev.yml up web`
  (Vite on `http://localhost:8081`) or `cd web && npm run dev` (port 5173).
- Production-like local: [`docker-compose.prod.yml`](../docker-compose.prod.yml)
  as an overlay, with an external reverse proxy on the Docker network.
- Coolify uses Dockerfile resources, not a compose file.

The old Blazor `Veldrath.Web` and `RealmFoundry` containers and the Caddy
configuration are retired.
