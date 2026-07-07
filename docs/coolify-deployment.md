# Coolify Deployment Reference — Veldrath

Environment variable reference for deploying all Veldrath services in Coolify as standalone Dockerfile-based resources.

---

## Service Overview

| Service | Dockerfile | Container Port | Coolify Resource Name | Data Protection Volume |
|---|---|---|---|---|
| **Veldrath.Server** | [`Veldrath.Server/Dockerfile`](../Veldrath.Server/Dockerfile) | 8080 | `veldrath-server` | `dataprotection_keys` |
| **Veldrath.Web** | [`Veldrath.Web/Dockerfile`](../Veldrath.Web/Dockerfile) | 8082 | `veldrath-web` | `web_dataprotection_keys` |
| **RealmFoundry** | [`RealmFoundry/Dockerfile`](../RealmFoundry/Dockerfile) | 8081 | `realmfoundry` | `foundry_dataprotection_keys` |
| **Veldrath.Discord** | [`Veldrath.Discord/Dockerfile`](../Veldrath.Discord/Dockerfile) | 8080 | `veldrath-discord` | None needed |

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
| `Jwt__Key` | `Jwt:Key` | [`Program.cs:136`](../Veldrath.Server/Program.cs:136) | HMAC-SHA JWT signing key. **Required.** |
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

## 2. Veldrath.Web

### Environment Variables

```ini
# ── API Server URL (REQUIRED — app crashes without this) ──
Veldrath__ServerUrl=http://veldrath-server:8080

# ── Public API Server URL (optional, falls back to ServerUrl) ──
Veldrath__PublicServerUrl=https://api.veldrath.com

# ── Auth Cookie Domain (REQUIRED in production) ──
Auth__CookieDomain=.veldrath.com

# ── ASP.NET Core ──
ASPNETCORE_URLS=http://+:8082
ASPNETCORE_ENVIRONMENT=Production
```

### Config Key Reference

| Env Var | Config Key | Read at | Purpose |
|---|---|---|---|
| `Veldrath__ServerUrl` | `Veldrath:ServerUrl` | [`Program.cs:37`](../Veldrath.Web/Program.cs:37) | HTTP client base address for all API calls. **Required** — throws if empty. |
| `Veldrath__PublicServerUrl` | `Veldrath:PublicServerUrl` | [`appsettings.json:14`](../Veldrath.Web/appsettings.json:14) | Public origin for OAuth redirect URLs. |
| `Auth__CookieDomain` | `Auth:CookieDomain` | [`Program.cs:139`](../Veldrath.Web/Program.cs:139) | Domain for the `rt` refresh token cookie on sign-out. Must match server's `Auth__CookieDomain`. |
| `ASPNETCORE_URLS` | — | Kestrel | Binds to `http://+:8082` ([`Dockerfile:42`](../Veldrath.Web/Dockerfile:42)). |
| `ASPNETCORE_ENVIRONMENT` | — | ASP.NET Core | Sets `Production` mode. |

### Persistent Volume

| Volume Name (source) | Mount Path (destination) |
|---|---|
| `web_dataprotection_keys` | `/root/.aspnet/DataProtection-Keys` |

Data Protection keys at [`Program.cs:77`](../Veldrath.Web/Program.cs:77) with application name `Veldrath.Web`. Required for antiforgery token stability.

### Health Check

Point Coolify's health check to `http://localhost:8082/health`.

---

## 3. RealmFoundry

### Environment Variables

```ini
# ── API Server URL (REQUIRED — app crashes without this) ──
Veldrath__ServerUrl=http://veldrath-server:8080

# ── Public API Server URL (optional, for OAuth redirect URLs) ──
Veldrath__PublicServerUrl=https://api.veldrath.com

# ── Auth Cookie Domain (REQUIRED in production) ──
Auth__CookieDomain=.veldrath.com

# ── ASP.NET Core ──
ASPNETCORE_URLS=http://+:8081
ASPNETCORE_ENVIRONMENT=Production
```

### Config Key Reference

| Env Var | Config Key | Read at | Purpose |
|---|---|---|---|
| `Veldrath__ServerUrl` | `Veldrath:ServerUrl` | [`Program.cs:36-37`](../RealmFoundry/Program.cs:36) | HTTP client base address. **Required** — throws if empty. |
| `Veldrath__PublicServerUrl` | `Veldrath:PublicServerUrl` | [`appsettings.json:14`](../RealmFoundry/appsettings.json:14) | Public origin for OAuth redirects. |
| `Auth__CookieDomain` | `Auth:CookieDomain` | [`Program.cs:157`](../RealmFoundry/Program.cs:157) | Cookie domain for `rt` cookie on sign-out. Must match server's `Auth__CookieDomain`. |
| `ASPNETCORE_URLS` | — | Kestrel | Binds to `http://+:8081` ([`Dockerfile:31`](../RealmFoundry/Dockerfile:31)). |
| `ASPNETCORE_ENVIRONMENT` | — | ASP.NET Core | Sets `Production` mode. |

### Persistent Volume

| Volume Name (source) | Mount Path (destination) |
|---|---|
| `foundry_dataprotection_keys` | `/root/.aspnet/DataProtection-Keys` |

Data Protection keys at [`Program.cs:79`](../RealmFoundry/Program.cs:79) with application name `RealmFoundry`. Required for antiforgery token stability.

### Health Check

Point Coolify's health check to `http://localhost:8081/health`. Includes a [`ServerConnectivityHealthCheck`](../RealmFoundry/Health/ServerConnectivityHealthCheck.cs) that pings the server — reports Degraded (not Unhealthy) if server is unreachable.

---

## 4. Veldrath.Discord

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
ASPNETCORE_URLS=http://+:8080
DOTNET_ENVIRONMENT=Production
```

### Config Key Reference

| Env Var | Config Key | Read at | Purpose |
|---|---|---|---|
| `Discord__Token` | `Discord:Token` | [`DiscordSettings.cs:9`](../Veldrath.Discord/Settings/DiscordSettings.cs:9) | Discord bot token from Developer Portal. **Required.** |
| `Discord__DevGuildId` | `Discord:DevGuildId` | [`DiscordSettings.cs:17`](../Veldrath.Discord/Settings/DiscordSettings.cs:17) | Guild ID for instant slash-command registration. `0` = global. |
| `Discord__ServerBaseUrl` | `Discord:ServerBaseUrl` | [`Program.cs:46`](../Veldrath.Discord/Program.cs:46) | Base URL of the game server API. |
| `ConnectionStrings__DefaultConnection` | `ConnectionStrings:DefaultConnection` | [`Program.cs:37-38`](../Veldrath.Discord/Program.cs:37) | Postgres connection string. **Required** — throws if missing. |
| `ASPNETCORE_URLS` | — | Kestrel | Binds to `http://+:8080` ([`Dockerfile:36`](../Veldrath.Discord/Dockerfile:36)). |
| `DOTNET_ENVIRONMENT` | — | ASP.NET Core | Hardcoded to `Production` in Dockerfile (line 42). |

### Notes

- **No Data Protection volume needed** — The Discord bot doesn't issue cookies or antiforgery tokens.
- **Health check** at `http://localhost:8080/health` ([`Program.cs:69`](../Veldrath.Discord/Program.cs:69)). Curl is already installed in the Dockerfile (line 33).

---

## 5. Supporting Infrastructure

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

## 6. Grafana (Already Deployed)

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
│  │ Web/FW   │  │ Prometheus│  │  Discord   │ │
│  │ :8082/81 │  │  :9090   │  │   :8080    │ │
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
5. **Veldrath.Web** — waits for server's `/health`
6. **RealmFoundry** — waits for server's `/health`
7. **Veldrath.Discord** — waits for server's `/health`
8. **Grafana** — waits for Prometheus (already deployed)

In Coolify standalone mode, set up health check endpoints so Coolify knows when each service is ready before routing traffic.
