# Implementation Specification: `docker-compose.coolify.yml`

**Date:** 2026-07-02  
**Author:** Zoo (Architect mode)  
**Status:** Ready for Code mode implementation  
**Prerequisite Analysis:** [`plans/coolify-compatibility-analysis.md`](coolify-compatibility-analysis.md)

---

## Table of Contents

1. [File Summary & Merge Strategy](#1-file-summary--merge-strategy)
2. [Caddy Service Removal](#2-caddy-service-removal)
3. [Service Specifications](#3-service-specifications)
   - [3.1 postgres](#31-postgres)
   - [3.2 stalwart](#32-stalwart)
   - [3.3 server](#33-server)
   - [3.4 discord](#34-discord)
   - [3.5 foundry](#35-foundry)
   - [3.6 web](#36-web)
   - [3.7 seq](#37-seq)
   - [3.8 prometheus](#38-prometheus)
   - [3.9 grafana](#39-grafana)
4. [Network Configuration](#4-network-configuration)
5. [Volumes](#5-volumes)
6. [Environment Variables & Secrets Reference](#6-environment-variables--secrets-reference)
7. [Stalwart Production Review](#7-stalwart-production-review)
8. [Pre-Implementation Dependencies](#8-pre-implementation-dependencies)
9. [Complete Merged YAML (Reference)](#9-complete-merged-yaml-reference)

---

## 1. File Summary & Merge Strategy

### Sources

| File | Role | Disposition |
|------|------|-------------|
| [`docker-compose.yml`](../docker-compose.yml) | Base definitions (9 services, volumes, build contexts) | Fully merged |
| [`docker-compose.prod.yml`](../docker-compose.prod.yml) | Production overrides (GHCR images, env vars, restart policies, Caddy) | Fully merged; Caddy removed |
| [`Caddyfile`](../Caddyfile) | Caddy reverse-proxy routing rules | **Discarded** — routing logic translated to Traefik labels |

### Merge Rules Applied Globally

1. **Remove** every `build:` stanza; all .NET services use `image:` referencing GHCR
2. **Remove** host port mappings (`ports:` or individual `"HOST:CONTAINER"` mappings) from all services except where explicitly documented
3. **Add** `restart: unless-stopped` to every service
4. **Add** `ASPNETCORE_ENVIRONMENT=Production` (or `DOTNET_ENVIRONMENT=Production` for Discord) to all .NET services
5. **Remove** the `caddy` service, `caddy_data` and `caddy_config` named volumes, and the Caddyfile bind mount
6. **Add** `deploy.resources` blocks to every service
7. **Add** health checks to every service that lacks one
8. **Add** a top-level `networks:` block with `veldrath` (internal bridge) and `coolify` (external)
9. **Attach** every service to `veldrath`; additionally attach `server`, `foundry`, and `web` to `coolify`

---

## 2. Caddy Service Removal

### Service Deletion

The `caddy` service from [`docker-compose.prod.yml:13-29`](../docker-compose.prod.yml#L13) is **deleted entirely**. Its responsibilities — TLS termination, domain routing, and sticky-session enforcement — are assumed by Coolify's built-in Traefik instance configured via labels on the application services.

### Volume Deletion

Remove these named volumes from the top-level `volumes:` block:

```yaml
# REMOVED:
caddy_data:
caddy_config:
```

### Bind Mount Deletion

Remove the Caddyfile bind mount: `./Caddyfile:/etc/caddy/Caddyfile:ro`. The [`Caddyfile`](../Caddyfile) itself remains in the repository for reference but is not referenced by any service.

### Caddy `depends_on` Removal

The Caddy depends_on chain (lines 23–29 of docker-compose.prod.yml) is deleted along with the service. The application services' own depends_on chains remain intact.

---

## 3. Service Specifications

Each subsection provides the exact final merged YAML for that service. Every property is accounted for — nothing is omitted.

### 3.1 postgres

**Source merge:** Base [`docker-compose.yml:26-40`](../docker-compose.yml#L26-L40) + Prod [`docker-compose.prod.yml:92-95`](../docker-compose.prod.yml#L92-L95)

**Changes from base:**
- `POSTGRES_PASSWORD` changed from hardcoded `veldrath_dev` to `${POSTGRES_PASSWORD}` (Coolify-managed secret)
- `ports:` set to `[]` (no host exposure; internal Docker network only)
- `restart: unless-stopped` added (from prod overlay)
- `deploy.resources` added
- `networks:` added

**Final merged YAML:**

```yaml
  postgres:
    image: postgres:17-alpine
    ports: []
    environment:
      POSTGRES_DB: veldrath
      POSTGRES_USER: veldrath
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U veldrath"]
      interval: 5s
      timeout: 5s
      retries: 5
    restart: unless-stopped
    networks:
      - veldrath
    deploy:
      resources:
        limits:
          memory: 1G
          cpus: "1.0"
        reservations:
          memory: 256M
          cpus: "0.5"
```

### 3.2 stalwart

**Source merge:** Base [`docker-compose.yml:6-24`](../docker-compose.yml#L6-L24) + Prod [`docker-compose.prod.yml:97-98`](../docker-compose.prod.yml#L97-L98)

**Changes from base:**
- Host port mappings removed (`9004:8082` for admin UI and `5025:25` for SMTP): see "Port Exposure — SMTP Decision Point" below
- `restart: unless-stopped` added (from prod overlay)
- `deploy.resources` added
- `networks:` added

**Port Exposure — SMTP Decision Point:**

The `5025:25` SMTP port mapping is **removed by default**. The Stalwart configuration at [`config/stalwart/config.toml`](../config/stalwart/config.toml) is a dev-only relay that accepts all mail without authentication. Publishing port 25 to the host would create an open relay — a severe security risk. If production genuinely needs to receive inbound internet email through Stalwart, you must:

1. Harden [`config/stalwart/config.toml`](../config/stalwart/config.toml): disable `relay = true`, configure valid recipient domains, enable authentication
2. Re-add `"25:25"` to the `ports:` list
3. Ensure the host firewall allows inbound port 25

If production uses an external SMTP relay (SendGrid, Mailgun, etc.) for outbound only, Stalwart's SMTP listener is unnecessary and port 25 should remain unpublished.

**Final merged YAML:**

```yaml
  stalwart:
    image: stalwartlabs/stalwart:v0.15.5
    ports: []
    environment:
      - STALWART_PATH=/opt/stalwart
      - ADMIN_SECRET=${STALWART_ADMIN_SECRET}
    volumes:
      - stalwart_data:/opt/stalwart
      - ./config/stalwart:/opt/stalwart/etc:ro
    healthcheck:
      test: ["CMD-SHELL", "grep -q ':1F92 ' /proc/net/tcp6 || grep -q ':1F92 ' /proc/net/tcp"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 30s
    restart: unless-stopped
    networks:
      - veldrath
    deploy:
      resources:
        limits:
          memory: 512M
          cpus: "0.5"
        reservations:
          memory: 128M
          cpus: "0.25"
```

### 3.3 server

**Source merge:** Base [`docker-compose.yml:42-93`](../docker-compose.yml#L42-L93) + Prod [`docker-compose.prod.yml:31-55`](../docker-compose.prod.yml#L31-L55)

**Changes from base:**
- `build:` replaced with `image: ghcr.io/kungraseri/veldrath-server:latest`
- `ports:` set to `[]` (no host exposure; Traefik routes via coolify network)
- `ASPNETCORE_ENVIRONMENT` changed from `Development` to `Production`
- `ConnectionStrings__DefaultConnection` password changed from `veldrath_dev` to `${POSTGRES_PASSWORD}` — **critical**: connection string must use the same secret variable as postgres
- Email block replaced: `SmtpHost`/`SmtpPort`/`EnableTls`/`SenderAddress`/`SenderName` replaced with production SMTP relay variables from prod overlay
- `Email__User` and `Email__Password` added (from prod overlay)
- `Foundry__BaseUrl` changed from `http://localhost:8081` to `https://foundry.veldrath.com` (prod overlay)
- `Web__BaseUrl` changed from `http://localhost:8082` to `https://veldrath.com` (prod overlay)
- `Auth__CookieDomain=.veldrath.com` added (from prod overlay)
- `AllowedReturnUrlHosts` removed in production — the fixed HTTPS base URLs are sufficient; no dynamic Tailscale hostnames
- `restart: unless-stopped` added (from prod overlay)
- Traefik labels added
- `deploy.resources` added
- `networks:` added (`veldrath` + `coolify`)

**Traefik labels (exact):**

```yaml
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.server.rule=Host(`api.veldrath.com`)"
      - "traefik.http.routers.server.tls.certresolver=coolify"
      - "traefik.http.routers.server.entrypoints=websecure"
      - "traefik.http.services.server.loadbalancer.server.port=8080"
```

**Final merged YAML:**

```yaml
  server:
    image: ghcr.io/kungraseri/veldrath-server:latest
    ports: []
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
      - ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=veldrath;Username=veldrath;Password=${POSTGRES_PASSWORD}
      - Database__Provider=postgres
      - Jwt__Key=${JWT_KEY}
      - OAuth__Discord__ClientId=${OAUTH_DISCORD_CLIENT_ID}
      - OAuth__Discord__ClientSecret=${OAUTH_DISCORD_CLIENT_SECRET}
      - OAuth__Google__ClientId=${OAUTH_GOOGLE_CLIENT_ID}
      - OAuth__Google__ClientSecret=${OAUTH_GOOGLE_CLIENT_SECRET}
      - OAuth__Microsoft__ClientId=${OAUTH_MICROSOFT_CLIENT_ID}
      - OAuth__Microsoft__ClientSecret=${OAUTH_MICROSOFT_CLIENT_SECRET}
      - Email__SmtpHost=${EMAIL_SMTP_HOST}
      - Email__SmtpPort=${EMAIL_SMTP_PORT:-587}
      - Email__EnableTls=${EMAIL_ENABLE_TLS:-true}
      - Email__User=${EMAIL_USER}
      - Email__Password=${EMAIL_PASSWORD}
      - Email__SenderAddress=${EMAIL_SENDER_ADDRESS:-noreply@veldrath.com}
      - Email__SenderName=Veldrath
      - Foundry__BaseUrl=https://foundry.veldrath.com
      - Web__BaseUrl=https://veldrath.com
      - Auth__CookieDomain=.veldrath.com
      - Seq__ServerUrl=http://seq
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.server.rule=Host(`api.veldrath.com`)"
      - "traefik.http.routers.server.tls.certresolver=coolify"
      - "traefik.http.routers.server.entrypoints=websecure"
      - "traefik.http.services.server.loadbalancer.server.port=8080"
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:8080/health || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 15s
    depends_on:
      postgres:
        condition: service_healthy
      stalwart:
        condition: service_healthy
    volumes:
      - dataprotection_keys:/root/.aspnet/DataProtection-Keys
    restart: unless-stopped
    networks:
      - veldrath
      - coolify
    deploy:
      resources:
        limits:
          memory: 1G
          cpus: "2.0"
        reservations:
          memory: 512M
          cpus: "1.0"
```

### 3.4 discord

**Source merge:** Base [`docker-compose.yml:95-115`](../docker-compose.yml#L95-L115) + Prod [`docker-compose.prod.yml:85-90`](../docker-compose.prod.yml#L85-L90)

**Changes from base:**
- `build:` replaced with `image: ghcr.io/kungraseri/veldrath-discord:latest`
- `DOTNET_ENVIRONMENT` changed from `Development` to `Production` (prod overlay env var merge: base sets `DOTNET_ENVIRONMENT=Development`, prod overrides with `DOTNET_ENVIRONMENT=Production`)
- `ConnectionStrings__DefaultConnection` password changed from `veldrath_dev` to `${POSTGRES_PASSWORD}`
- `restart: unless-stopped` added (from prod overlay)
- Health check added — see "Discord Health Check" below
- `deploy.resources` added
- `networks:` added

**Discord Health Check:**

The Discord bot runs on `mcr.microsoft.com/dotnet/runtime:10.0` (not the ASP.NET image), so it has **no built-in HTTP server** and **no `curl`**. This creates two options:

| Option | Description | Effort |
|--------|-------------|--------|
| **A (Recommended)** | Switch Dockerfile to `aspnet:10.0`, add `app.MapHealthChecks("/health")` and `builder.Services.AddHealthChecks()` in `Program.cs`, add `curl` via apt-get in Dockerfile. Then use `curl -f http://localhost:8080/health`. | Medium |
| **B (Minimal)** | Use a process-liveness check: `pgrep -x dotnet || exit 1`. Works on the runtime image but only confirms the process exists, not that the bot has connected to Discord. | Low |

**Spec calls for Option A** (aligned with analysis Issue #8 recommendation). Until Option A is implemented, use Option B as a temporary health check.

**Final merged YAML (Option A — with HTTP health endpoint):**

```yaml
  discord:
    image: ghcr.io/kungraseri/veldrath-discord:latest
    ports: []
    environment:
      - DOTNET_ENVIRONMENT=Production
      - Discord__Token=${DISCORD_TOKEN}
      - Discord__DevGuildId=${DISCORD_DEV_GUILD_ID:-0}
      - Discord__ServerBaseUrl=http://server:8080
      - ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=veldrath;Username=veldrath;Password=${POSTGRES_PASSWORD}
    # healthcheck:  # Uncomment after Option A is implemented
    #   test: ["CMD-SHELL", "curl -f http://localhost:8080/health || exit 1"]
    #   interval: 15s
    #   timeout: 5s
    #   retries: 3
    #   start_period: 10s
    healthcheck:
      test: ["CMD-SHELL", "pgrep -x dotnet || exit 1"]
      interval: 15s
      timeout: 5s
      retries: 3
      start_period: 10s
    depends_on:
      postgres:
        condition: service_healthy
      server:
        condition: service_healthy
    restart: unless-stopped
    networks:
      - veldrath
    deploy:
      resources:
        limits:
          memory: 256M
          cpus: "0.5"
        reservations:
          memory: 128M
          cpus: "0.25"
```

### 3.5 foundry

**Source merge:** Base [`docker-compose.yml:117-135`](../docker-compose.yml#L117-L135) + Prod [`docker-compose.prod.yml:57-70`](../docker-compose.prod.yml#L57-L70)

**Changes from base:**
- `build:` replaced with `image: ghcr.io/kungraseri/realmfoundry:latest`
- `ports:` set to `[]` (no host exposure; Traefik routes via coolify network)
- `ASPNETCORE_ENVIRONMENT` changed from `Development` to `Production`
- `Veldrath__PublicServerUrl` changed from `${SERVER_PUBLIC_URL:-http://localhost:9000}` to `${FOUNDRY_PUBLIC_SERVER_URL:-https://api.veldrath.com}` (prod overlay)
- `Auth__CookieDomain=.veldrath.com` added (from prod overlay)
- `restart: unless-stopped` added (from prod overlay)
- Health check added (was missing in base + prod)
- Traefik labels with sticky-session cookie added
- `deploy.resources` added
- `networks:` added (`veldrath` + `coolify`)

**Traefik labels (exact):**

```yaml
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.foundry.rule=Host(`foundry.veldrath.com`)"
      - "traefik.http.routers.foundry.tls.certresolver=coolify"
      - "traefik.http.routers.foundry.entrypoints=websecure"
      - "traefik.http.services.foundry.loadbalancer.server.port=8081"
      - "traefik.http.services.foundry.loadbalancer.sticky.cookie.name=foundry_lb"
      - "traefik.http.services.foundry.loadbalancer.sticky.cookie.secure=true"
```

**Health check dependency:** The Foundry container runs on `mcr.microsoft.com/dotnet/aspnet:10.0` which does **not** include `curl`. The Foundry Dockerfile at [`RealmFoundry/Dockerfile`](../RealmFoundry/Dockerfile) must be updated to add `curl` (same approach as [`Veldrath.Server/Dockerfile:38-40`](../Veldrath.Server/Dockerfile#L38-L40)):

```dockerfile
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*
```

Additionally, [`RealmFoundry/Program.cs`](../RealmFoundry/Program.cs) must register the health check middleware:
```csharp
builder.Services.AddHealthChecks();
// ... later, after app build:
app.MapHealthChecks("/health");
```

**Final merged YAML:**

```yaml
  foundry:
    image: ghcr.io/kungraseri/realmfoundry:latest
    ports: []
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8081
      - Veldrath__ServerUrl=http://server:8080
      - Veldrath__PublicServerUrl=${FOUNDRY_PUBLIC_SERVER_URL:-https://api.veldrath.com}
      - Auth__CookieDomain=.veldrath.com
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.foundry.rule=Host(`foundry.veldrath.com`)"
      - "traefik.http.routers.foundry.tls.certresolver=coolify"
      - "traefik.http.routers.foundry.entrypoints=websecure"
      - "traefik.http.services.foundry.loadbalancer.server.port=8081"
      - "traefik.http.services.foundry.loadbalancer.sticky.cookie.name=foundry_lb"
      - "traefik.http.services.foundry.loadbalancer.sticky.cookie.secure=true"
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:8081/health || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 15s
    volumes:
      - foundry_dataprotection_keys:/root/.aspnet/DataProtection-Keys
    depends_on:
      server:
        condition: service_healthy
    restart: unless-stopped
    networks:
      - veldrath
      - coolify
    deploy:
      resources:
        limits:
          memory: 512M
          cpus: "1.0"
        reservations:
          memory: 256M
          cpus: "0.5"
```

### 3.6 web

**Source merge:** Base [`docker-compose.yml:175-192`](../docker-compose.yml#L175-L192) + Prod [`docker-compose.prod.yml:72-83`](../docker-compose.prod.yml#L72-L83)

**Changes from base:**
- `build:` replaced with `image: ghcr.io/kungraseri/veldrath-web:latest`
- `ports:` set to `[]` (no host exposure; Traefik routes via coolify network)
- `ASPNETCORE_ENVIRONMENT` changed from `Development` to `Production`
- `Veldrath__PublicServerUrl` changed from `${SERVER_PUBLIC_URL:-http://localhost:9000}` to `${WEB_PUBLIC_SERVER_URL:-https://api.veldrath.com}` (prod overlay)
- `Auth__CookieDomain=.veldrath.com` added (from prod overlay)
- `restart: unless-stopped` added (from prod overlay)
- `depends_on` with `condition: service_healthy` preserved (from prod overlay lines 81-83; the prod overlay re-declares this)
- Health check added (was missing in base + prod)
- Traefik labels with sticky-session cookie and www→apex redirect added
- `deploy.resources` added
- `networks:` added (`veldrath` + `coolify`)

**Traefik labels (exact):**

Two routers: main router for `veldrath.com` and a redirect router for `www.veldrath.com` → `veldrath.com`:

```yaml
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.web.rule=Host(`veldrath.com`)"
      - "traefik.http.routers.web.tls.certresolver=coolify"
      - "traefik.http.routers.web.entrypoints=websecure"
      - "traefik.http.services.web.loadbalancer.server.port=8082"
      - "traefik.http.services.web.loadbalancer.sticky.cookie.name=web_lb"
      - "traefik.http.services.web.loadbalancer.sticky.cookie.secure=true"
      - "traefik.http.routers.web-www.rule=Host(`www.veldrath.com`)"
      - "traefik.http.routers.web-www.tls.certresolver=coolify"
      - "traefik.http.routers.web-www.entrypoints=websecure"
      - "traefik.http.routers.web-www.middlewares=web-www-redirect"
      - "traefik.http.middlewares.web-www-redirect.redirectregex.regex=^https?://www\\.veldrath\\.com/(.*)"
      - "traefik.http.middlewares.web-www-redirect.redirectregex.replacement=https://veldrath.com/$${1}"
      - "traefik.http.middlewares.web-www-redirect.redirectregex.permanent=true"
```

**Note on the redirect regex:** The `$${1}` escaping is required because Docker Compose interprets `${1}` as a variable substitution. Double-dollar `$$` escapes to a literal `$` in the final Traefik config.

**Health check dependency:** Same as foundry — the [`Veldrath.Web/Dockerfile`](../Veldrath.Web/Dockerfile) must be updated to add `curl`. Additionally, [`Veldrath.Web/Program.cs`](../Veldrath.Web/Program.cs) must add health check middleware.

**Final merged YAML:**

```yaml
  web:
    image: ghcr.io/kungraseri/veldrath-web:latest
    ports: []
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8082
      - Veldrath__ServerUrl=http://server:8080
      - Veldrath__PublicServerUrl=${WEB_PUBLIC_SERVER_URL:-https://api.veldrath.com}
      - Auth__CookieDomain=.veldrath.com
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.web.rule=Host(`veldrath.com`)"
      - "traefik.http.routers.web.tls.certresolver=coolify"
      - "traefik.http.routers.web.entrypoints=websecure"
      - "traefik.http.services.web.loadbalancer.server.port=8082"
      - "traefik.http.services.web.loadbalancer.sticky.cookie.name=web_lb"
      - "traefik.http.services.web.loadbalancer.sticky.cookie.secure=true"
      - "traefik.http.routers.web-www.rule=Host(`www.veldrath.com`)"
      - "traefik.http.routers.web-www.tls.certresolver=coolify"
      - "traefik.http.routers.web-www.entrypoints=websecure"
      - "traefik.http.routers.web-www.middlewares=web-www-redirect"
      - "traefik.http.middlewares.web-www-redirect.redirectregex.regex=^https?://www\\.veldrath\\.com/(.*)"
      - "traefik.http.middlewares.web-www-redirect.redirectregex.replacement=https://veldrath.com/$${1}"
      - "traefik.http.middlewares.web-www-redirect.redirectregex.permanent=true"
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:8082/health || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 15s
    volumes:
      - web_dataprotection_keys:/root/.aspnet/DataProtection-Keys
    depends_on:
      server:
        condition: service_healthy
    restart: unless-stopped
    networks:
      - veldrath
      - coolify
    deploy:
      resources:
        limits:
          memory: 512M
          cpus: "1.0"
        reservations:
          memory: 256M
          cpus: "0.5"
```

### 3.7 seq

**Source merge:** Base [`docker-compose.yml:137-147`](../docker-compose.yml#L137-L147) + Prod [`docker-compose.prod.yml:100-101`](../docker-compose.prod.yml#L100-L101)

**Changes from base:**
- `ports:` set to `[]` (no host exposure; see "Optional Traefik Routing for Monitoring Services" below)
- `restart: unless-stopped` added (from prod overlay)
- Health check added (was missing in base + prod)
- `deploy.resources` added
- `networks:` added

**Health check:** Seq serves its UI on port 80 internally. A GET to `/` returns 200 when healthy.

**Optional Traefik Routing for Monitoring Services:**

If Seq should be accessible via a domain (e.g., `seq.veldrath.com`), add these labels:

```yaml
# OPTIONAL — uncomment to route Seq through Traefik with Coolify auth middleware:
#   labels:
#     - "traefik.enable=true"
#     - "traefik.http.routers.seq.rule=Host(`seq.veldrath.com`)"
#     - "traefik.http.routers.seq.tls.certresolver=coolify"
#     - "traefik.http.routers.seq.entrypoints=websecure"
#     - "traefik.http.routers.seq.middlewares=seq-auth"
#     - "traefik.http.services.seq.loadbalancer.server.port=80"
```

**Final merged YAML:**

```yaml
  seq:
    image: datalust/seq:latest
    ports: []
    environment:
      - ACCEPT_EULA=Y
      - SEQ_FIRSTRUN_ADMINPASSWORD=${SEQ_ADMIN_PASSWORD}
    volumes:
      - seq_data:/data
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:80 || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 10s
    restart: unless-stopped
    networks:
      - veldrath
    deploy:
      resources:
        limits:
          memory: 512M
          cpus: "0.5"
        reservations:
          memory: 256M
          cpus: "0.25"
```

### 3.8 prometheus

**Source merge:** Base [`docker-compose.yml:149-158`](../docker-compose.yml#L149-L158) + Prod [`docker-compose.prod.yml:103-104`](../docker-compose.prod.yml#L103-L104)

**Changes from base:**
- `ports:` set to `[]` (no host exposure)
- `restart: unless-stopped` added (from prod overlay)
- Health check added (was missing in base + prod)
- `deploy.resources` added
- `networks:` added

**Health check:** Prometheus exposes `/-/healthy` which returns 200 when the server is healthy.

**Optional Traefik Routing for Monitoring Services:**

If Prometheus should be accessible via a domain (e.g., `prometheus.veldrath.com`), add these labels:

```yaml
# OPTIONAL — uncomment to route Prometheus through Traefik with Coolify auth middleware:
#   labels:
#     - "traefik.enable=true"
#     - "traefik.http.routers.prometheus.rule=Host(`prometheus.veldrath.com`)"
#     - "traefik.http.routers.prometheus.tls.certresolver=coolify"
#     - "traefik.http.routers.prometheus.entrypoints=websecure"
#     - "traefik.http.routers.prometheus.middlewares=prometheus-auth"
#     - "traefik.http.services.prometheus.loadbalancer.server.port=9090"
```

**Final merged YAML:**

```yaml
  prometheus:
    image: prom/prometheus:latest
    ports: []
    volumes:
      - prometheus_data:/prometheus
      - ./config/prometheus/prometheus.yml:/etc/prometheus/prometheus.yml:ro
    healthcheck:
      test: ["CMD-SHELL", "wget --no-verbose --tries=1 --spider http://localhost:9090/-/healthy || exit 1"]
      interval: 15s
      timeout: 5s
      retries: 3
      start_period: 10s
    depends_on:
      server:
        condition: service_healthy
    restart: unless-stopped
    networks:
      - veldrath
    deploy:
      resources:
        limits:
          memory: 512M
          cpus: "0.5"
        reservations:
          memory: 256M
          cpus: "0.25"
```

> **Note on `wget`:** The `prom/prometheus` image is BusyBox-based and includes `wget` but not `curl`. Use `wget --spider` for the health check command.

### 3.9 grafana

**Source merge:** Base [`docker-compose.yml:160-173`](../docker-compose.yml#L160-L173) + Prod [`docker-compose.prod.yml:105-106`](../docker-compose.prod.yml#L105-L106)

**Changes from base:**
- `ports:` set to `[]` (no host exposure)
- `restart: unless-stopped` added (from prod overlay)
- Health check added (was missing in base + prod)
- `deploy.resources` added
- `networks:` added

**Health check:** Grafana exposes `/api/health` which returns 200 when healthy. The Grafana image includes `curl` or `wget`.

**Optional Traefik Routing for Monitoring Services:**

If Grafana should be accessible via a domain (e.g., `grafana.veldrath.com`), add these labels:

```yaml
# OPTIONAL — uncomment to route Grafana through Traefik with Coolify auth middleware:
#   labels:
#     - "traefik.enable=true"
#     - "traefik.http.routers.grafana.rule=Host(`grafana.veldrath.com`)"
#     - "traefik.http.routers.grafana.tls.certresolver=coolify"
#     - "traefik.http.routers.grafana.entrypoints=websecure"
#     - "traefik.http.routers.grafana.middlewares=grafana-auth"
#     - "traefik.http.services.grafana.loadbalancer.server.port=3000"
```

**Final merged YAML:**

```yaml
  grafana:
    image: grafana/grafana:11.6.1
    ports: []
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=${GF_ADMIN_PASSWORD}
    volumes:
      - grafana_data:/var/lib/grafana
      - ./config/grafana/provisioning:/etc/grafana/provisioning:ro
      - ./config/grafana/dashboards:/var/lib/grafana/dashboards:ro
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:3000/api/health || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 15s
    depends_on:
      - prometheus
    restart: unless-stopped
    networks:
      - veldrath
    deploy:
      resources:
        limits:
          memory: 256M
          cpus: "0.5"
        reservations:
          memory: 128M
          cpus: "0.25"
```

---

## 4. Network Configuration

### Top-Level `networks:` Block

```yaml
networks:
  veldrath:
    driver: bridge
  coolify:
    external: true
```

| Network | Driver | Scope | Purpose |
|---------|--------|-------|---------|
| `veldrath` | `bridge` | Internal | Service-to-service communication (postgres ↔ server, server ↔ foundry, prometheus → server, etc.) |
| `coolify` | `external: true` | Coolify-managed | Traefik reverse proxy → application containers. Must exist before deploy — created by Coolify automatically. |

### Network Attachment Matrix

| Service | `veldrath` | `coolify` | Reason for `coolify` |
|---------|-----------|-----------|----------------------|
| postgres | ✅ | — | Internal only |
| stalwart | ✅ | — | Internal only |
| server | ✅ | ✅ | Routed by Traefik (`api.veldrath.com`) |
| discord | ✅ | — | Internal only |
| foundry | ✅ | ✅ | Routed by Traefik (`foundry.veldrath.com`) |
| web | ✅ | ✅ | Routed by Traefik (`veldrath.com`) |
| seq | ✅ | — | Internal only (optionally add `coolify` if routed) |
| prometheus | ✅ | — | Internal only (optionally add `coolify` if routed) |
| grafana | ✅ | — | Internal only (optionally add `coolify` if routed) |

---

## 5. Volumes

### Named Volumes (Top-Level `volumes:` Block)

```yaml
volumes:
  postgres_data:
  dataprotection_keys:
  foundry_dataprotection_keys:
  web_dataprotection_keys:
  stalwart_data:
  seq_data:
  prometheus_data:
  grafana_data:
```

### Volume Changes from Base

| Volume | Status | Notes |
|--------|--------|-------|
| `postgres_data` | Kept | Persistent DB storage |
| `dataprotection_keys` | Kept | Server ASP.NET Data Protection key ring |
| `foundry_dataprotection_keys` | Kept | Foundry ASP.NET Data Protection key ring |
| `web_dataprotection_keys` | Kept | Web ASP.NET Data Protection key ring |
| `stalwart_data` | Kept | Stalwart RocksDB storage |
| `seq_data` | Kept | Seq log storage |
| `prometheus_data` | Kept | Prometheus TSDB |
| `grafana_data` | Kept | Grafana dashboards + config |
| `caddy_data` | **REMOVED** | Was Caddy TLS cert storage |
| `caddy_config` | **REMOVED** | Was Caddy runtime config |

### Data Protection Key Ring Consideration

The three Data Protection volumes (`dataprotection_keys`, `foundry_dataprotection_keys`, `web_dataprotection_keys`) are **separate per service**. This means `Auth__CookieDomain=.veldrath.com` (shared cookie domain) will NOT work for cross-service auth token validation — each service has its own key ring and cannot decrypt cookies issued by another service. See analysis Issue #14 for discussion. This is a pre-existing limitation carried forward; a shared Redis-based key ring is a future enhancement out of scope for this spec.

---

## 6. Environment Variables & Secrets Reference

This section documents every environment variable used across the merged compose file and how it should be provided to Coolify.

### Coolify Secrets to Create

These values must be created as secrets in the Coolify UI (or injected via Coolify's environment variable management). Variables in **bold** are critical security-sensitive values.

| # | Variable Name | Used By | Coolify Type | Notes |
|---|---------------|---------|-------------|-------|
| 1 | **`POSTGRES_PASSWORD`** | postgres, server, discord | Secret | Strong random password; used in `POSTGRES_PASSWORD` env var AND embedded in `ConnectionStrings__DefaultConnection` |
| 2 | **`JWT_KEY`** | server | Secret | 32+ character random string for JWT signing |
| 3 | **`DISCORD_TOKEN`** | discord | Secret | Discord bot token |
| 4 | `DISCORD_DEV_GUILD_ID` | discord | Variable | Set to `0` for production (global command registration) |
| 5 | **`OAUTH_DISCORD_CLIENT_ID`** | server | Secret | Discord OAuth application client ID |
| 6 | **`OAUTH_DISCORD_CLIENT_SECRET`** | server | Secret | Discord OAuth application client secret |
| 7 | **`OAUTH_GOOGLE_CLIENT_ID`** | server | Secret | Google OAuth client ID |
| 8 | **`OAUTH_GOOGLE_CLIENT_SECRET`** | server | Secret | Google OAuth client secret |
| 9 | **`OAUTH_MICROSOFT_CLIENT_ID`** | server | Secret | Microsoft OAuth client ID |
| 10 | **`OAUTH_MICROSOFT_CLIENT_SECRET`** | server | Secret | Microsoft OAuth client secret |
| 11 | `EMAIL_SMTP_HOST` | server | Variable | External SMTP relay hostname |
| 12 | `EMAIL_SMTP_PORT` | server | Variable | Defaults to `587` |
| 13 | `EMAIL_ENABLE_TLS` | server | Variable | Defaults to `true` |
| 14 | `EMAIL_USER` | server | Secret | SMTP auth username |
| 15 | **`EMAIL_PASSWORD`** | server | Secret | SMTP auth password |
| 16 | `EMAIL_SENDER_ADDRESS` | server | Variable | Defaults to `noreply@veldrath.com` |
| 17 | `FOUNDRY_PUBLIC_SERVER_URL` | foundry | Variable | Defaults to `https://api.veldrath.com` |
| 18 | `WEB_PUBLIC_SERVER_URL` | web | Variable | Defaults to `https://api.veldrath.com` |
| 19 | **`STALWART_ADMIN_SECRET`** | stalwart | Secret | Stalwart admin UI password |
| 20 | **`SEQ_ADMIN_PASSWORD`** | seq | Secret | Seq admin password (first-run only) |
| 21 | **`GF_ADMIN_PASSWORD`** | grafana | Secret | Grafana admin password |

### Variable Reference Pattern

All secrets use Docker Compose `${VARIABLE_NAME}` syntax (not Coolify-specific `${COOLIFY_SECRET_...}` notation). Coolify injects these at deploy time from its Secrets/Variables store. The standard Compose `${VAR}` syntax ensures the file remains portable across Docker environments.

### Variables NOT Carried Forward

| Variable | Source File | Reason for Removal |
|----------|-------------|-------------------|
| `SERVER_PUBLIC_URL` | `.env.example` | Replaced by service-specific `FOUNDRY_PUBLIC_SERVER_URL` and `WEB_PUBLIC_SERVER_URL` |
| `FOUNDRY_PUBLIC_URL` | `.env.example` | Was dev-only (Tailscale); replaced by hardcoded `https://foundry.veldrath.com` on server |
| `WEB_PUBLIC_URL` | `.env.example` | Was dev-only (Tailscale); replaced by hardcoded `https://veldrath.com` on server |
| `ALLOWED_RETURN_URL_HOSTS` | `.env.example` | Dev-only (Tailscale IPs/hostnames); not needed in production with fixed HTTPS domains |
| `CERT_PASSWORD` | `.env.example` | Dev-only (local HTTPS cert); not used in production |
| All Tailscale-related variables | `.env.example` | Dev-only |

---

## 7. Stalwart Production Review

### Current Configuration Assessment

The file [`config/stalwart/config.toml`](../config/stalwart/config.toml) is explicitly marked as "minimal dev configuration" and has the following production concerns:

| Setting | Current Value | Production Risk | Recommendation |
|---------|--------------|----------------|----------------|
| `[session.rcpt] relay = true` | Accept mail for any recipient | 🔴 **Critical**: Open relay — accepts mail for any domain without authentication | Change to `relay = false` and configure explicit recipient domains |
| `[server.listener."smtp"]` | No TLS, no auth, port 25 | 🟡 **High**: SMTP without authentication | If keeping Stalwart for inbound, enable `tls` and authentication settings |
| `[directory."internal"]` | In-memory directory | 🟠 **Medium**: No persistent user accounts | Configure directory with actual mail domains and accounts |
| `[tracer."stdout"]` level = "info" | Info-level logging | 🟢 **Low**: Acceptable for production | Consider `debug` for initial production run, then `info` |
| Outbound delivery | Disabled by default in v0.15 | 🟢 **Low**: Safe default; no open relay for outbound | Verify this remains disabled unless outbound relay is needed |

### Decision: Stalwart Purpose in Production

The production compose file (`docker-compose.prod.yml`) configures `server` to use an **external SMTP relay** (`Email__SmtpHost=${EMAIL_SMTP_HOST}`). This means the Stalwart container is **not used for outbound email delivery** in production. Stalwart's role in production is limited to:

1. **Inbound email reception** — only if `5025:25` port is published and DNS MX records point to the server
2. **Internal mail relay** — not applicable in current architecture

### Recommendation

**If production does not need inbound email reception**, the Stalwart service can remain in the compose stack for log-inspection/admin purposes but with port 25 unpublished (as spec'd above). The Stalwart config does not need changes since it's not internet-facing.

**If production needs inbound email**, the Stalwart configuration must be hardened before deploying to Coolify:
1. Set `relay = false` in `[session.rcpt]`
2. Configure `[server.listener."smtp"]` with TLS and authentication
3. Configure `[directory]` with actual domain and accounts
4. Re-add `"25:25"` to the `ports:` list in the compose file
5. Update DNS MX record for `veldrath.com` to point to the Coolify host

This hardening is **out of scope** for the docker-compose.coolify.yml file — it requires changes to [`config/stalwart/config.toml`](../config/stalwart/config.toml).

---

## 8. Pre-Implementation Dependencies

Before `docker-compose.coolify.yml` can work correctly, the following changes must be made to application code and Dockerfiles. These are **implementation prerequisites** for Code mode.

### 8.1 Dockerfile Updates (curl installation)

| Dockerfile | Change | Reason |
|-----------|--------|--------|
| [`RealmFoundry/Dockerfile`](../RealmFoundry/Dockerfile) | Add `apt-get install curl` in runtime stage (copy lines 38-40 from Server Dockerfile) | Health check requires `curl` |
| [`Veldrath.Web/Dockerfile`](../Veldrath.Web/Dockerfile) | Add `apt-get install curl` in runtime stage | Health check requires `curl` |
| [`Veldrath.Discord/Dockerfile`](../Veldrath.Discord/Dockerfile) | Switch from `runtime:10.0` to `aspnet:10.0`, add `curl`, add health endpoint | Discord needs HTTP health endpoint (Option A) |

### 8.2 Application Code Updates (health check endpoints)

| File | Change | Reason |
|------|--------|--------|
| [`RealmFoundry/Program.cs`](../RealmFoundry/Program.cs) | Add `builder.Services.AddHealthChecks()` and `app.MapHealthChecks("/health")` | Foundry health check endpoint |
| [`Veldrath.Web/Program.cs`](../Veldrath.Web/Program.cs) | Add `builder.Services.AddHealthChecks()` and `app.MapHealthChecks("/health")` | Web health check endpoint |
| [`Veldrath.Discord/Program.cs`](../Veldrath.Discord/Program.cs) | Add Kestrel listener on port 8080 with `app.MapHealthChecks("/health")` (Option A) | Discord health check endpoint |

### 8.3 Repository Structure

The `docker-compose.coolify.yml` file should be placed at the repository root: `c:/code/Veldrath/docker-compose.coolify.yml`

### 8.4 Coolify Configuration (Pre-Deploy)

Before first deploy on Coolify, these must be configured:

1. **Docker Registry**: Add GHCR credentials (GitHub PAT with `read:packages` scope)
2. **Secrets**: Create all 21 secrets listed in Section 6
3. **Domains**: Configure in Coolify UI:
   - `api.veldrath.com` → server
   - `foundry.veldrath.com` → foundry
   - `veldrath.com` + `www.veldrath.com` → web
4. **Network**: The `coolify` network is auto-created by Coolify; no manual action needed

---

## 9. Complete Merged YAML (Reference)

Below is the complete `docker-compose.coolify.yml` file that Code mode should produce. Every line is accounted for from the merge of base + prod, with additions and removals as specified above.

```yaml
# docker-compose.coolify.yml — Coolify-compatible single-file compose stack for veldrath.com
#
# Generated from: docker-compose.yml (base) + docker-compose.prod.yml (overlay)
# Caddy reverse proxy REMOVED — routing handled by Coolify's built-in Traefik.
# All .NET services use pre-built GHCR images (no local build contexts).
# All host port mappings removed — traffic routed via Traefik on the coolify network.
#
# Pre-deploy prerequisites:
#   1. Add curl to RealmFoundry/Dockerfile and Veldrath.Web/Dockerfile runtime stages
#   2. Add health check endpoints in Program.cs for foundry, web, and discord
#   3. Configure GHCR registry credentials in Coolify
#   4. Create Coolify secrets for all ${VARIABLE} references (see plans/docker-compose-coolify-spec.md)

services:
  # ── Infrastructure ──────────────────────────────────────────────────────────

  postgres:
    image: postgres:17-alpine
    ports: []
    environment:
      POSTGRES_DB: veldrath
      POSTGRES_USER: veldrath
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U veldrath"]
      interval: 5s
      timeout: 5s
      retries: 5
    restart: unless-stopped
    networks:
      - veldrath
    deploy:
      resources:
        limits:
          memory: 1G
          cpus: "1.0"
        reservations:
          memory: 256M
          cpus: "0.5"

  stalwart:
    image: stalwartlabs/stalwart:v0.15.5
    ports: []
    environment:
      - STALWART_PATH=/opt/stalwart
      - ADMIN_SECRET=${STALWART_ADMIN_SECRET}
    volumes:
      - stalwart_data:/opt/stalwart
      - ./config/stalwart:/opt/stalwart/etc:ro
    healthcheck:
      test: ["CMD-SHELL", "grep -q ':1F92 ' /proc/net/tcp6 || grep -q ':1F92 ' /proc/net/tcp"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 30s
    restart: unless-stopped
    networks:
      - veldrath
    deploy:
      resources:
        limits:
          memory: 512M
          cpus: "0.5"
        reservations:
          memory: 128M
          cpus: "0.25"

  # ── Application Services ────────────────────────────────────────────────────

  server:
    image: ghcr.io/kungraseri/veldrath-server:latest
    ports: []
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
      - ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=veldrath;Username=veldrath;Password=${POSTGRES_PASSWORD}
      - Database__Provider=postgres
      - Jwt__Key=${JWT_KEY}
      - OAuth__Discord__ClientId=${OAUTH_DISCORD_CLIENT_ID}
      - OAuth__Discord__ClientSecret=${OAUTH_DISCORD_CLIENT_SECRET}
      - OAuth__Google__ClientId=${OAUTH_GOOGLE_CLIENT_ID}
      - OAuth__Google__ClientSecret=${OAUTH_GOOGLE_CLIENT_SECRET}
      - OAuth__Microsoft__ClientId=${OAUTH_MICROSOFT_CLIENT_ID}
      - OAuth__Microsoft__ClientSecret=${OAUTH_MICROSOFT_CLIENT_SECRET}
      - Email__SmtpHost=${EMAIL_SMTP_HOST}
      - Email__SmtpPort=${EMAIL_SMTP_PORT:-587}
      - Email__EnableTls=${EMAIL_ENABLE_TLS:-true}
      - Email__User=${EMAIL_USER}
      - Email__Password=${EMAIL_PASSWORD}
      - Email__SenderAddress=${EMAIL_SENDER_ADDRESS:-noreply@veldrath.com}
      - Email__SenderName=Veldrath
      - Foundry__BaseUrl=https://foundry.veldrath.com
      - Web__BaseUrl=https://veldrath.com
      - Auth__CookieDomain=.veldrath.com
      - Seq__ServerUrl=http://seq
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.server.rule=Host(`api.veldrath.com`)"
      - "traefik.http.routers.server.tls.certresolver=coolify"
      - "traefik.http.routers.server.entrypoints=websecure"
      - "traefik.http.services.server.loadbalancer.server.port=8080"
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:8080/health || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 15s
    depends_on:
      postgres:
        condition: service_healthy
      stalwart:
        condition: service_healthy
    volumes:
      - dataprotection_keys:/root/.aspnet/DataProtection-Keys
    restart: unless-stopped
    networks:
      - veldrath
      - coolify
    deploy:
      resources:
        limits:
          memory: 1G
          cpus: "2.0"
        reservations:
          memory: 512M
          cpus: "1.0"

  discord:
    image: ghcr.io/kungraseri/veldrath-discord:latest
    ports: []
    environment:
      - DOTNET_ENVIRONMENT=Production
      - Discord__Token=${DISCORD_TOKEN}
      - Discord__DevGuildId=${DISCORD_DEV_GUILD_ID:-0}
      - Discord__ServerBaseUrl=http://server:8080
      - ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=veldrath;Username=veldrath;Password=${POSTGRES_PASSWORD}
    # TODO: Switch to HTTP health check after Discord Dockerfile moves to aspnet image
    # healthcheck:
    #   test: ["CMD-SHELL", "curl -f http://localhost:8080/health || exit 1"]
    #   interval: 15s
    #   timeout: 5s
    #   retries: 3
    #   start_period: 10s
    healthcheck:
      test: ["CMD-SHELL", "pgrep -x dotnet || exit 1"]
      interval: 15s
      timeout: 5s
      retries: 3
      start_period: 10s
    depends_on:
      postgres:
        condition: service_healthy
      server:
        condition: service_healthy
    restart: unless-stopped
    networks:
      - veldrath
    deploy:
      resources:
        limits:
          memory: 256M
          cpus: "0.5"
        reservations:
          memory: 128M
          cpus: "0.25"

  foundry:
    image: ghcr.io/kungraseri/realmfoundry:latest
    ports: []
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8081
      - Veldrath__ServerUrl=http://server:8080
      - Veldrath__PublicServerUrl=${FOUNDRY_PUBLIC_SERVER_URL:-https://api.veldrath.com}
      - Auth__CookieDomain=.veldrath.com
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.foundry.rule=Host(`foundry.veldrath.com`)"
      - "traefik.http.routers.foundry.tls.certresolver=coolify"
      - "traefik.http.routers.foundry.entrypoints=websecure"
      - "traefik.http.services.foundry.loadbalancer.server.port=8081"
      - "traefik.http.services.foundry.loadbalancer.sticky.cookie.name=foundry_lb"
      - "traefik.http.services.foundry.loadbalancer.sticky.cookie.secure=true"
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:8081/health || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 15s
    volumes:
      - foundry_dataprotection_keys:/root/.aspnet/DataProtection-Keys
    depends_on:
      server:
        condition: service_healthy
    restart: unless-stopped
    networks:
      - veldrath
      - coolify
    deploy:
      resources:
        limits:
          memory: 512M
          cpus: "1.0"
        reservations:
          memory: 256M
          cpus: "0.5"

  web:
    image: ghcr.io/kungraseri/veldrath-web:latest
    ports: []
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8082
      - Veldrath__ServerUrl=http://server:8080
      - Veldrath__PublicServerUrl=${WEB_PUBLIC_SERVER_URL:-https://api.veldrath.com}
      - Auth__CookieDomain=.veldrath.com
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.web.rule=Host(`veldrath.com`)"
      - "traefik.http.routers.web.tls.certresolver=coolify"
      - "traefik.http.routers.web.entrypoints=websecure"
      - "traefik.http.services.web.loadbalancer.server.port=8082"
      - "traefik.http.services.web.loadbalancer.sticky.cookie.name=web_lb"
      - "traefik.http.services.web.loadbalancer.sticky.cookie.secure=true"
      - "traefik.http.routers.web-www.rule=Host(`www.veldrath.com`)"
      - "traefik.http.routers.web-www.tls.certresolver=coolify"
      - "traefik.http.routers.web-www.entrypoints=websecure"
      - "traefik.http.routers.web-www.middlewares=web-www-redirect"
      - "traefik.http.middlewares.web-www-redirect.redirectregex.regex=^https?://www\\.veldrath\\.com/(.*)"
      - "traefik.http.middlewares.web-www-redirect.redirectregex.replacement=https://veldrath.com/$${1}"
      - "traefik.http.middlewares.web-www-redirect.redirectregex.permanent=true"
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:8082/health || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 15s
    volumes:
      - web_dataprotection_keys:/root/.aspnet/DataProtection-Keys
    depends_on:
      server:
        condition: service_healthy
    restart: unless-stopped
    networks:
      - veldrath
      - coolify
    deploy:
      resources:
        limits:
          memory: 512M
          cpus: "1.0"
        reservations:
          memory: 256M
          cpus: "0.5"

  # ── Monitoring ──────────────────────────────────────────────────────────────

  seq:
    image: datalust/seq:latest
    ports: []
    environment:
      - ACCEPT_EULA=Y
      - SEQ_FIRSTRUN_ADMINPASSWORD=${SEQ_ADMIN_PASSWORD}
    volumes:
      - seq_data:/data
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:80 || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 10s
    restart: unless-stopped
    networks:
      - veldrath
    deploy:
      resources:
        limits:
          memory: 512M
          cpus: "0.5"
        reservations:
          memory: 256M
          cpus: "0.25"

  prometheus:
    image: prom/prometheus:latest
    ports: []
    volumes:
      - prometheus_data:/prometheus
      - ./config/prometheus/prometheus.yml:/etc/prometheus/prometheus.yml:ro
    healthcheck:
      test: ["CMD-SHELL", "wget --no-verbose --tries=1 --spider http://localhost:9090/-/healthy || exit 1"]
      interval: 15s
      timeout: 5s
      retries: 3
      start_period: 10s
    depends_on:
      server:
        condition: service_healthy
    restart: unless-stopped
    networks:
      - veldrath
    deploy:
      resources:
        limits:
          memory: 512M
          cpus: "0.5"
        reservations:
          memory: 256M
          cpus: "0.25"

  grafana:
    image: grafana/grafana:11.6.1
    ports: []
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=${GF_ADMIN_PASSWORD}
    volumes:
      - grafana_data:/var/lib/grafana
      - ./config/grafana/provisioning:/etc/grafana/provisioning:ro
      - ./config/grafana/dashboards:/var/lib/grafana/dashboards:ro
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:3000/api/health || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 15s
    depends_on:
      - prometheus
    restart: unless-stopped
    networks:
      - veldrath
    deploy:
      resources:
        limits:
          memory: 256M
          cpus: "0.5"
        reservations:
          memory: 128M
          cpus: "0.25"

# ── Networks ──────────────────────────────────────────────────────────────────

networks:
  veldrath:
    driver: bridge
  coolify:
    external: true

# ── Volumes ───────────────────────────────────────────────────────────────────

volumes:
  postgres_data:
  dataprotection_keys:
  foundry_dataprotection_keys:
  web_dataprotection_keys:
  stalwart_data:
  seq_data:
  prometheus_data:
  grafana_data:
```

---

## Summary of Changes from Two-File Setup

| Change | Count | Details |
|--------|-------|---------|
| Services | 10 → 9 | Caddy removed |
| Named volumes | 10 → 8 | `caddy_data`, `caddy_config` removed |
| `build:` stanzas | 4 → 0 | All replaced with `image:` |
| Host port mappings | 9 → 0 | All `"HOST:CONTAINER"` removed |
| Health checks | 3 → 9 | Added to foundry, web, discord, seq, prometheus, grafana |
| Traefik labels | 0 → 3 services | server (5 labels), foundry (7 labels), web (14 labels incl. www redirect) |
| Networks | 0 → 2 | `veldrath` (bridge) + `coolify` (external) |
| `deploy.resources` blocks | 0 → 9 | Every service has limits + reservations |
| `restart: unless-stopped` | 4 → 9 | Added to postgres, stalwart, foundry, web, seq (was already on server/discord/prometheus/grafana) |
| Hardcoded secrets | 2 → 0 | `POSTGRES_PASSWORD` and connection string passwords now reference `${POSTGRES_PASSWORD}` |
| Env vars changed | 19 | Dev URLs → production HTTPS URLs, email relay config, cookie domain, environment name |
| Env vars removed | 5 | Tailscale URLs, `ALLOWED_RETURN_URL_HOSTS`, `CERT_PASSWORD` |
