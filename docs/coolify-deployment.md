# Coolify Deployment Reference — Veldrath

Environment variable reference for deploying all Veldrath services in Coolify as standalone Dockerfile-based resources.

---

## Service Overview

Coolify builds each service from its Dockerfile (one resource per service); the
compose files in this repository are for local development only.

| Service | Base dir / Dockerfile | Container Port | Coolify Resource Name | Persistent Volume |
|---|---|---|---|---|
| **Veldrath.Server** | `/` / [`Veldrath.Server/Dockerfile`](../Veldrath.Server/Dockerfile) | 8080 | `veldrath-server` | `dataprotection_keys` |
| **SvelteKit web app** | `/web` / [`web/client/Dockerfile`](../web/client/Dockerfile) | 8081 | `veldrath-web` | None needed |
| **Veldrath.Discord** | `/` / [`Veldrath.Discord/Dockerfile`](../Veldrath.Discord/Dockerfile) | 8083 | `veldrath-discord` | None needed |

> **Important:** In Coolify's standalone Dockerfile mode, container names are auto-generated UUIDs. For inter-service DNS to work, set a fixed **Container Name** in Coolify Advanced settings for each resource matching the names above. Containers can then resolve each other by that name on Coolify's internal Docker network.

---

## 1. Veldrath.Server (API)

### Environment Variables

```ini
# ── Database (use Coolify managed Postgres — injects ${POSTGRES_URL}) ──
ConnectionStrings__DefaultConnection=${POSTGRES_URL}

# ── OAuth Providers (set actual values directly) ──
OAuth__Discord__ClientId=your_discord_client_id
OAuth__Discord__ClientSecret=your_discord_client_secret
OAuth__Google__ClientId=your_google_client_id
OAuth__Google__ClientSecret=your_google_client_secret
OAuth__Microsoft__ClientId=your_microsoft_client_id
OAuth__Microsoft__ClientSecret=your_microsoft_client_secret

# ── JWT (256-bit+ signing key) ──
Jwt__Key=your-base64-or-random-string-at-least-32-chars

# ── Internal SvelteKit → Identity account bridge (desktop sign-in compatibility) ──
# Must match INTERNAL_BRIDGE_TOKEN on the SvelteKit web container.
InternalBridge__Token=your-shared-internal-bridge-secret

# ── Email (SMTP) ──
Email__SmtpHost=smtp.example.com
Email__SmtpPort=587
Email__User=noreply@veldrath.com
Email__Password=your_smtp_password
Email__SenderName=Veldrath

# ── Auth (REQUIRED in production — server throws at startup if missing) ──
Auth__CookieDomain=.veldrath.com

# ── Cross-service URLs ──
Seq__ServerUrl=http://veldrath-seq:5341

# ── ASP.NET Core ──
ASPNETCORE_URLS=http://+:8080
ASPNETCORE_ENVIRONMENT=Production
```

### Config Key Reference

| Env Var | Config Key | Read at | Purpose |
|---|---|---|---|
| `ConnectionStrings__DefaultConnection` | `ConnectionStrings:DefaultConnection` | [`Program.cs:101`](../Veldrath.Server/Program.cs:101) | Postgres connection string. **Required.** |
| `OAuth__Discord__ClientId` | `OAuth:Discord:ClientId` | [`Program.cs:249`](../Veldrath.Server/Program.cs:249) | Discord OAuth app client ID. |
| `OAuth__Discord__ClientSecret` | `OAuth:Discord:ClientSecret` | [`Program.cs:250`](../Veldrath.Server/Program.cs:250) | Discord OAuth app client secret. |
| `OAuth__Google__ClientId` | `OAuth:Google:ClientId` | [`Program.cs:265`](../Veldrath.Server/Program.cs:265) | Google OAuth app client ID. |
| `OAuth__Google__ClientSecret` | `OAuth:Google:ClientSecret` | [`Program.cs:266`](../Veldrath.Server/Program.cs:266) | Google OAuth app client secret. |
| `OAuth__Microsoft__ClientId` | `OAuth:Microsoft:ClientId` | [`Program.cs:280`](../Veldrath.Server/Program.cs:280) | Microsoft OAuth app client ID. |
| `OAuth__Microsoft__ClientSecret` | `OAuth:Microsoft:ClientSecret` | [`Program.cs:281`](../Veldrath.Server/Program.cs:281) | Microsoft OAuth app client secret. |
| `Jwt__Key` | `Jwt:Key` | [`Program.cs:141`](../Veldrath.Server/Program.cs:141) | HMAC-SHA JWT signing key. **Required.** |
| `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, … | `Cors:AllowedOrigins` | [`Program.cs:95`](../Veldrath.Server/Program.cs:95) | Optional extra browser origins for SignalR CORS. Defaults to local dev plus `Web:BaseUrl`. |
| `InternalBridge__Token` | `InternalBridge:Token` | [`InternalAccountBridgeEndpoints.cs`](../Veldrath.Server/Features/Auth/InternalAccountBridgeEndpoints.cs) | Shared secret for the temporary SvelteKit → Identity account bridge. Unset disables the endpoint (503). |
| `Email__SmtpHost` | `Email:SmtpHost` | [`Program.cs:308`](../Veldrath.Server/Program.cs:308) | SMTP relay hostname. If empty, uses `NullEmailSender` (no emails sent). |
| `Email__SmtpPort` | `Email:SmtpPort` | [`appsettings.json:42`](../Veldrath.Server/appsettings.json:42) | SMTP port (default: 587). |
| `Email__User` | `Email:User` | [`appsettings.json:44`](../Veldrath.Server/appsettings.json:44) | SMTP auth username. |
| `Email__Password` | `Email:Password` | [`appsettings.json:45`](../Veldrath.Server/appsettings.json:45) | SMTP auth password. |
| `Auth__CookieDomain` | `Auth:CookieDomain` | [`Program.cs:584`](../Veldrath.Server/Program.cs:584) | Cookie domain for cross-subdomain session sharing. **Required in production** — server throws if missing. |
| `Seq__ServerUrl` | `Seq:ServerUrl` | [`Program.cs:56`](../Veldrath.Server/Program.cs:56) | Seq server URL for structured log shipping. |

### Persistent Volume

| Volume Name (source) | Mount Path (destination) |
|---|---|
| `dataprotection_keys` | `/root/.aspnet/DataProtection-Keys` |

Data Protection keys are persisted at [`Program.cs:402`](../Veldrath.Server/Program.cs:402) with application name `Veldrath.Server`. Required for OAuth correlation cookies to survive container restarts.

### Health Check

Point Coolify's health check to `http://localhost:8080/health` (exposed at [`Program.cs:553`](../Veldrath.Server/Program.cs:553)).

---

## 2. SvelteKit web app

### Coolify resource settings

| Field | Value |
|---|---|
| **Base Directory** | `/web` |
| **Dockerfile Location** | `/client/Dockerfile` |
| **Port** | `8081` |
| **Health Check** | `http://localhost:8081/` |
| **Build args** | `PUBLIC_API_URL=https://api.veldrath.com`, `PUBLIC_HUB_URL=https://api.veldrath.com/hubs/game` |

> SvelteKit 3 resolves public variables in **two** places: they are inlined into
> the browser bundle at **build** time and validated from `process.env` when
> `node build` starts. The Dockerfile re-declares the build args as runtime `ENV`
> defaults, so setting them as Coolify **build arguments** is enough for the
> container to boot. The runtime entries below are optional; if you set them they
> must match the build args exactly, otherwise SSR and the browser bundle point
> at different hosts.

### Environment Variables

```ini
# ── SvelteKit / adapter-node ──
NODE_ENV=production
ORIGIN=https://veldrath.com
BETTER_AUTH_URL=https://veldrath.com
BETTER_AUTH_SECRET=your-better-auth-secret-at-least-32-chars

# ── Auth database (Coolify Postgres; the container applies Drizzle migrations) ──
DATABASE_URL=${POSTGRES_URL}

# ── JWT shared with Veldrath.Server ──
JWT_KEY=your-jwt-signing-key-at-least-32-chars
JWT_ISSUER=Veldrath.Server
JWT_AUDIENCE=Veldrath.Client

# ── Browser-reachable .NET endpoints ──
PUBLIC_API_URL=https://api.veldrath.com
PUBLIC_HUB_URL=https://api.veldrath.com/hubs/game

# ── Temporary SvelteKit → Identity account bridge (server-side only) ──
INTERNAL_BRIDGE_URL=http://server:8080
INTERNAL_BRIDGE_TOKEN=your-shared-internal-bridge-secret

# ── Better Auth social providers (same OAuth apps as the .NET server) ──
DISCORD_CLIENT_ID=${OAUTH_DISCORD_CLIENT_ID}
DISCORD_CLIENT_SECRET=${OAUTH_DISCORD_CLIENT_SECRET}
GOOGLE_CLIENT_ID=${OAUTH_GOOGLE_CLIENT_ID}
GOOGLE_CLIENT_SECRET=${OAUTH_GOOGLE_CLIENT_SECRET}
MICROSOFT_CLIENT_ID=${OAUTH_MICROSOFT_CLIENT_ID}
MICROSOFT_CLIENT_SECRET=${OAUTH_MICROSOFT_CLIENT_SECRET}
MICROSOFT_TENANT_ID=${OAUTH_MICROSOFT_TENANT_ID}

# ── Traefik forwarded headers ──
PROTOCOL_HEADER=x-forwarded-proto
HOST_HEADER=x-forwarded-host
ADDRESS_HEADER=x-forwarded-for
XFF_DEPTH=1
```

### Config Key Reference

| Env Var | Read by | Purpose |
|---|---|---|
| `ORIGIN` | adapter-node | Public origin used for redirects and URL construction. |
| `BETTER_AUTH_URL` | Better Auth | Public base URL for auth callbacks and cookies. |
| `BETTER_AUTH_SECRET` | Better Auth | Session signing secret. **Required.** |
| `DATABASE_URL` | Drizzle / Better Auth | Postgres connection for the `auth` schema. The user must be able to create the schema on first boot. |
| `JWT_KEY` / `JWT_ISSUER` / `JWT_AUDIENCE` | game token minting | Must match `Veldrath.Server` JWT validation. |
| `PUBLIC_API_URL` | browser SSR/client | Public .NET API origin. |
| `PUBLIC_HUB_URL` | browser SignalR | Public SignalR hub URL. |
| `INTERNAL_BRIDGE_URL` | `foundry-bridge.ts`, `email.ts`, `character-api.ts` | Internal .NET base URL (`http://server:8080`). Never exposed to the browser. |
| `INTERNAL_BRIDGE_TOKEN` | `foundry-bridge.ts`, `email.ts` | Shared secret matching `InternalBridge__Token`. |
| `PROTOCOL_HEADER` / `HOST_HEADER` / `ADDRESS_HEADER` / `XFF_DEPTH` | adapter-node | Trust Traefik's forwarded headers. |

### Internal Identity bridge (desktop compatibility)

The Avalonia desktop client's native sign-in still authenticates through the .NET
`/api/auth/*` endpoints, so SvelteKit mirrors web accounts into ASP.NET Core Identity:

- Registration posts the plaintext password once to `/api/foundry-bridge/register`,
  which calls the internal .NET endpoint and stores an Identity password hash.
  SvelteKit never persists the password.
- [`hooks.server.ts`](../web/client/src/hooks.server.ts) synchronizes the account whenever
  the username, email, or verification state changes, and links Discord/Google/Microsoft
  provider accounts. The sync mirrors username, email, verification, and display name onto
  the existing Identity row.
- The internal endpoint requires the `X-Internal-Bridge-Token` header and returns
  `503` when `InternalBridge__Token` is not configured.
- Keep the bridge while the desktop client relies on .NET Identity sign-in.

### Transactional email

Better Auth password-reset and email-verification messages are delivered by the .NET
server's `IEmailSender` (SMTP in production, `NullEmailSender` locally) through
`/api/internal/email/password-reset` and `/api/internal/email/verification`. Configure the
`EMAIL_*` variables on the **server** service and the internal bridge URL/token on the
**web** service. The web app rebuilds links to its own `/reset-password` and
`/verify-email` pages, so `BETTER_AUTH_URL`/`ORIGIN` must be the public site origin.

### Migrations

The container entrypoint waits for Postgres, creates the `auth` schema if it
does not exist, applies Drizzle migrations from
[`web/shared/db/migrations`](../web/shared/db/migrations), then starts
adapter-node. Drizzle and Better Auth tables are schema-qualified, so the
connection does not need a special `search_path`.

The bookkeeping table is `auth.__drizzle_migrations`. Runtime and
`drizzle-kit` use the same table.

### Persistent Volume

None. The SvelteKit web container is stateless; sessions live in the Better
Auth `auth.session` table.

### Health Check

Point Coolify's health check to `http://localhost:8081/`.

---

## 3. Veldrath.Discord

### Environment Variables

```ini
# ── Discord Bot Token (REQUIRED — bot won't start without this) ──
Discord__Token=${DISCORD_BOT_TOKEN}

# ── Dev Guild ID (0 = global command registration in production) ──
Discord__DevGuildId=0

# ── API Server URL (for /server status command) ──
Discord__ServerBaseUrl=http://veldrath-server:8080

# ── Database (same Postgres as the server — bot connects directly) ──
ConnectionStrings__DefaultConnection=${POSTGRES_URL}

# ── ASP.NET Core ──
ASPNETCORE_URLS=http://+:8083
DOTNET_ENVIRONMENT=Production
```

### Config Key Reference

| Env Var | Config Key | Read at | Purpose |
|---|---|---|---|
| `Discord__Token` | `Discord:Token` | [`DiscordSettings.cs:9`](../Veldrath.Discord/Settings/DiscordSettings.cs:9) | Discord bot token from Developer Portal. **Required.** |
| `Discord__DevGuildId` | `Discord:DevGuildId` | [`DiscordSettings.cs:17`](../Veldrath.Discord/Settings/DiscordSettings.cs:17) | Guild ID for instant slash-command registration. `0` = global. |
| `Discord__ServerBaseUrl` | `Discord:ServerBaseUrl` | [`Program.cs:46`](../Veldrath.Discord/Program.cs:46) | Base URL of the game server API. |
| `ConnectionStrings__DefaultConnection` | `ConnectionStrings:DefaultConnection` | [`Program.cs:37-38`](../Veldrath.Discord/Program.cs:37) | Postgres connection string. **Required** — throws if missing. |
| `ASPNETCORE_URLS` | — | Kestrel | Binds to `http://+:8083` ([`Dockerfile:36`](../Veldrath.Discord/Dockerfile:36)). |
| `DOTNET_ENVIRONMENT` | — | ASP.NET Core | Hardcoded to `Production` in Dockerfile (line 42). |

### Notes

- **No Data Protection volume needed** — The Discord bot doesn't issue cookies or antiforgery tokens.
- **Health check** at `http://localhost:8083/health` ([`Program.cs:69`](../Veldrath.Discord/Program.cs:69)). Curl is already installed in the Dockerfile (line 33).

---

## 4. Supporting Infrastructure

### Postgres (if not using Coolify's managed Postgres)

| Field | Value |
|---|---|
| **Image** | `postgres:17-alpine` |
| **Port** | `5432` |

**Environment Variables:**

```ini
POSTGRES_DB=veldrath
POSTGRES_USER=veldrath
POSTGRES_PASSWORD=your_production_password
```

**Volumes:**

| Volume Name (source) | Mount Path (destination) |
|---|---|
| `postgres_data` | `/var/lib/postgresql/data` |

> **Recommendation:** Use Coolify's managed Postgres database service instead. It auto-generates `${POSTGRES_URL}` which all app services reference via `ConnectionStrings__DefaultConnection`.

---

### Seq — Structured Log Viewer

| Field | Value |
|---|---|
| **Image** | `datalust/seq:latest` |
| **Port** | `80` (web UI) |
| **Port** | `5341` (ingestion — used by Serilog sink) |

**Environment Variables:**

```ini
ACCEPT_EULA=Y
SEQ_FIRSTRUN_ADMINPASSWORD=${SEQ_ADMIN_PASSWORD}
```

**Volumes:**

| Volume Name (source) | Mount Path (destination) |
|---|---|
| `seq_data` | `/data` |

**Notes:**
- The `SEQ_FIRSTRUN_ADMINPASSWORD` is only read on first launch.
- App services send logs to `Seq__ServerUrl=http://veldrath-seq:5341`.
- In Coolify, expose port `80` for the UI. The ingestion endpoint `5341` is reached via the same hostname on a different port — Coolify standalone only exposes one port, so expose `80` and use `http://veldrath-seq:80` for ingestion (Seq accepts ingestion on both ports).

---

### Prometheus — Metrics Scraper

| Field | Value |
|---|---|
| **Image** | `prom/prometheus:latest` |
| **Port** | `9090` (web UI) |

**Environment Variables:** None required.

**Volumes:**

| Volume Name (source) | Mount Path (destination) |
|---|---|
| `prometheus_data` | `/prometheus` |
| Config file mount | `/etc/prometheus/prometheus.yml:ro` |

**Config file:**

The Prometheus scrape config is at [`config/prometheus/prometheus.yml`](../config/prometheus/prometheus.yml). In Coolify standalone mode, use one of these approaches to get it into the container:

**Option A — Fork the image:**
Create `Dockerfile.prometheus`:
```dockerfile
FROM prom/prometheus:latest
COPY config/prometheus/prometheus.yml /etc/prometheus/prometheus.yml
```

**Option B — Mount the file in Coolify:**
Mount the repo's `config/prometheus/prometheus.yml` to `/etc/prometheus/prometheus.yml`. Update the `targets` line to use your server's Coolify hostname:

```yaml
scrape_configs:
  - job_name: veldrath-server
    static_configs:
      - targets: ['veldrath-server:8080']
    metrics_path: /metrics
```

**Notes:**
- The server exposes `/metrics` at [`Program.cs:576`](../Veldrath.Server/Program.cs:576) via the Prometheus .NET client library (`prometheus-net` and `prometheus-net.DotNetRuntime`).
- Grafana needs Prometheus as a data source — configure it with `http://veldrath-prometheus:9090`.

---

## 5. Grafana (Already Deployed)

### Provisioning Config Mounts

If you want auto-provisioned dashboards and datasources, mount these from the repo:

| Host file/dir | Container path |
|---|---|
| [`config/grafana/provisioning/datasources/`](../config/grafana/provisioning/datasources/) | `/etc/grafana/provisioning/datasources/` |
| [`config/grafana/provisioning/dashboards/`](../config/grafana/provisioning/dashboards/) | `/etc/grafana/provisioning/dashboards/` |
| [`config/grafana/dashboards/`](../config/grafana/dashboards/) | `/var/lib/grafana/dashboards/` |

### Available Dashboards

| File | Description |
|---|---|
| [`10915.json`](../config/grafana/dashboards/10915.json) | ASP.NET Core metrics |
| [`19194.json`](../config/grafana/dashboards/19194.json) | .NET runtime (GC, JIT, thread pool) |
| [`23178.json`](../config/grafana/dashboards/23178.json) | Prometheus metrics |
| [`23179.json`](../config/grafana/dashboards/23179.json) | Node exporter (if running) |

---

## Network Architecture

```
┌─────────────────────────────────────────────┐
│            Coolify Docker Network             │
│                                              │
│  ┌──────────┐       ┌──────────────┐        │
│  │ Postgres │       │     Seq      │         │
│  │  :5432   │       │  :5341 / :80 │         │
│  └────┬─────┘       └──────┬───────┘         │
│       │                    │                  │
│  ┌────▼────────────────────▼──────────────┐  │
│  │         Veldrath.Server                │  │
│  │     :8080  /health  /metrics           │  │
│  └────┬───────────────────────────────────┘  │
│       │                                      │
│  ┌────▼─────┐  ┌──────────┐  ┌────────────┐ │
│  │   Web    │  │ Prometheus│  │  Discord   │ │
│  │  :8081   │  │  :9090   │  │   :8080    │ │
│  └──────────┘  └────┬─────┘  └────────────┘ │
│                     │                        │
│              ┌──────▼─────┐                  │
│              │   Grafana  │                  │
│              │   :3000    │                  │
│              └────────────┘                  │
└─────────────────────────────────────────────┘
```

---

## Deployment Order

Services with health check dependencies should be deployed in this order:

1. **Postgres** — database must be ready first
2. **Seq** — log sink (optional, no dependency)
3. **Veldrath.Server** — waits for Postgres, auto-migrates on startup
4. **Prometheus** — waits for server's `/metrics`
5. **SvelteKit web app** — waits for server's `/health`
6. **Veldrath.Discord** — waits for server's `/health`
7. **Grafana** — waits for Prometheus (already deployed)

In Coolify standalone mode, set up health check endpoints so Coolify knows when each service is ready before routing traffic.
