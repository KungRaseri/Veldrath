# Coolify Compatibility Analysis — docker-compose.prod.yml

**Date:** 2026-07-02
**Analyzed by:** Zoo (Architect mode)
**Scope:** [`docker-compose.yml`](../docker-compose.yml) + [`docker-compose.prod.yml`](../docker-compose.prod.yml) + [`Caddyfile`](../Caddyfile)
**Method:** Read-only analysis; no files modified.

---

## Executive Summary

### Overall Readiness: 🔴 NEEDS MAJOR REWORK

The current production compose stack is **not Coolify-compatible** in its current form. The architecture is built around a **Caddy-first** reverse proxy model that conflicts fundamentally with Coolify's **Traefik-first** model. Three critical issues require architectural decisions before any migration can begin:

1. **Caddy vs. Traefik**: Both want ports 80/443 and TLS termination — only one can own those responsibilities.
2. **Multi-file compose merge**: Coolify expects a single `docker-compose.yml`; the current stack requires two files merged at deploy time.
3. **No Traefik labels**: Zero services have Coolify-required routing labels; without them, Coolify cannot route traffic to any application service.

Beyond these, there are medium-severity gaps in health checks, environment variable management, networking, and missing `deploy.resources` constraints that should be addressed before production deployment.

---

## Issue Matrix

### 🔴 Critical Issues (3)

#### Issue #1: Reverse Proxy Collision — Caddy vs. Coolify Traefik
| Field | Detail |
|-------|--------|
| **Severity** | 🔴 Critical |
| **Category** | Reverse Proxy |
| **Source** | [`docker-compose.prod.yml:13-29`](../docker-compose.prod.yml#L13) — `caddy` service definition; [`Caddyfile`](../Caddyfile) — domain routing rules |
| **Description** | The `caddy` service binds host ports `80:80` and `443:443`, performs TLS termination via Let's Encrypt, and routes to `server:8080`, `foundry:8081`, and `web:8082`. Coolify's built-in Traefik instance also binds 80/443 on the host and performs the same functions. Deploying both simultaneously causes a port conflict — only one process can bind 80/443. |
| **Coolify Impact** | **Port binding failure** on deploy. Even if Caddy's port mappings are removed, the Caddy container itself still manages TLS (Let's Encrypt via ACME) and domain routing via the [`Caddyfile`](../Caddyfile). Coolify has no visibility into Caddy's routing; it cannot inject its own domains, apply middleware, or manage certificates. The Caddy service effectively operates as an opaque black box to Coolify, breaking the core value proposition of the platform. |
| **Recommendation** | Remove the `caddy` service entirely. Migrate all routing, TLS, and sticky-session logic to Coolify-managed Traefik labels on each application service. See [Architecture Decision: Caddy vs. Traefik](#architecture-decision-caddy-vs-traefik) below. |

#### Issue #2: Multi-File Compose Merge
| Field | Detail |
|-------|--------|
| **Severity** | 🔴 Critical |
| **Category** | Compose Structure |
| **Source** | [`docker-compose.prod.yml:3`](../docker-compose.prod.yml#L3) — `docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d` |
| **Description** | The production stack is deployed by merging two files: [`docker-compose.yml`](../docker-compose.yml) (base) + [`docker-compose.prod.yml`](../docker-compose.prod.yml) (overrides). Coolify expects a **single** `docker-compose.yml` file. The merge is done at the CLI level with `-f` flags, which Coolify cannot replicate. |
| **Coolify Impact** | **Deployment failure.** Coolify will look for exactly one `docker-compose.yml` in the configured repository path. It cannot merge multiple files. If only the base file is used, the `caddy` service is missing, services use `build:` instead of GHCR images, and host ports are exposed on all services. If only the prod file is used, it's an overlay that lacks complete service definitions (e.g., [`foundry`](../docker-compose.prod.yml#L57-L70), [`web`](../docker-compose.prod.yml#L72-L83), and [`discord`](../docker-compose.prod.yml#L85-L90) don't define `volumes`, `depends_on`, or base `environment` — those come from the base file). |
| **Recommendation** | Create a single, self-contained `docker-compose.coolify.yml` that merges all definitions from both files. Every service must have its complete configuration in one file. Remove all `build:` stanzas in favor of `image:` references to GHCR. |

#### Issue #3: Postgres Password Hardcoded in Base, Not Overridden in Prod
| Field | Detail |
|-------|--------|
| **Severity** | 🔴 Critical |
| **Category** | Config / Security |
| **Source** | [`docker-compose.yml:31`](../docker-compose.yml#L31) — `POSTGRES_PASSWORD: veldrath_dev`; also embedded in connection strings at lines 52, 109 |
| **Description** | The Postgres superuser password is hardcoded as `veldrath_dev` in the base compose file. The prod overlay does **not** override `POSTGRES_PASSWORD` or the connection strings with a production-grade secret. The connection string on [`server`](../docker-compose.yml#L52) uses `Password=veldrath_dev` and [`discord`](../docker-compose.yml#L109) uses the same. Even though Postgres ports are removed in prod, any internal container can connect with this well-known password. Additionally, the connection string is not updated in prod — [`docker-compose.prod.yml`](../docker-compose.prod.yml) does not set `ConnectionStrings__DefaultConnection` on any service, so the dev password propagates to production. |
| **Coolify Impact** | **Security vulnerability.** Coolify manages secrets through its own Secrets system. Hardcoded passwords bypass this and cannot be rotated or managed through the Coolify UI. The `veldrath_dev` password would be visible in the Coolify service configuration UI. |
| **Recommendation** | Move `POSTGRES_PASSWORD` and all connection string passwords to Coolify-managed secrets (referenced via `${COOLIFY_SECRET_POSTGRES_PASSWORD}` or Coolify's variable injection). Generate a strong random password for production. Override `ConnectionStrings__DefaultConnection` on `server`, `discord`, and any other service that connects to Postgres. |

---

### 🟡 High Issues (4)

#### Issue #4: No Traefik Routing Labels on Any Service
| Field | Detail |
|-------|--------|
| **Severity** | 🟡 High |
| **Category** | Reverse Proxy |
| **Source** | Entire [`docker-compose.prod.yml`](../docker-compose.prod.yml) — absence of `labels:` sections with Traefik configuration |
| **Description** | Coolify uses Traefik labels to discover services and route traffic. The standard labels include `traefik.enable=true`, `traefik.http.routers.<name>.rule=Host(...)`, `traefik.http.services.<name>.loadbalancer.server.port=...`, and TLS configuration. No service in the current compose has any Traefik labels. |
| **Coolify Impact** | **No traffic routing.** Coolify cannot route HTTP/HTTPS traffic to `server`, `foundry`, or `web`. Users would see a 404 or connection refused even if the containers are running. Domain management through the Coolify UI would be non-functional. |
| **Recommendation** | Add Traefik labels to `server`, `foundry`, and `web` services. Include: `traefik.enable=true`, router rule with `Host()` matching the domain, service port declaration, TLS resolver reference (`coolify` or `letsencrypt`), and sticky-session middleware for [`foundry`](../Caddyfile#L16-L19) and [`web`](../Caddyfile#L27-L32) (Blazor Server). See [Migration Path](#migration-path) for label templates. |

#### Issue #5: Blazor Server Sticky Sessions — Caddy Cookie LB Must Be Replicated
| Field | Detail |
|-------|--------|
| **Severity** | 🟡 High |
| **Category** | Reverse Proxy |
| **Source** | [`Caddyfile:16-19`](../Caddyfile#L16-L19) — `lb_policy cookie` for foundry; [`Caddyfile:27-32`](../Caddyfile#L27-L32) — `lb_policy cookie` for web |
| **Description** | Both [`RealmFoundry`](../RealmFoundry/) and [`Veldrath.Web`](../Veldrath.Web/) are Blazor Server applications. Blazor Server maintains a persistent SignalR circuit between the browser and the server. If a request is routed to a different container instance mid-session, the circuit breaks and the user gets a "Connection disconnected" error. Caddy handles this with `lb_policy cookie { name foundry_lb }` and `lb_policy cookie { name web_lb }`. |
| **Coolify Impact** | **Broken Blazor Server sessions.** Without sticky sessions configured in Traefik, users of [`foundry.veldrath.com`](../Caddyfile#L15) and [`veldrath.com`](../Caddyfile#L27) will experience random disconnections and state loss. Even with a single replica, the sticky-session configuration is still needed for correct SignalR circuit negotiation. |
| **Recommendation** | Configure sticky sessions in Traefik via a cookie-based load balancer sticky configuration. Add a Traefik sticky cookie middleware to `foundry` and `web` service labels: `traefik.http.services.foundry.loadbalancer.sticky.cookie.name=foundry_lb` and `traefik.http.services.foundry.loadbalancer.sticky.cookie.secure=true`. |

#### Issue #6: No Custom Docker Network
| Field | Detail |
|-------|--------|
| **Severity** | 🟡 High |
| **Category** | Networking |
| **Source** | Both compose files — absence of `networks:` top-level key |
| **Description** | The compose stack does not define a custom Docker network. All services communicate via the default Compose network (auto-created per-project). Coolify places its managed services on its own `coolify` network. If the Veldrath services are on the default network and Coolify's Traefik is on the `coolify` network, they cannot communicate. |
| **Coolify Impact** | **Traefik cannot reach application services.** The Coolify Traefik instance runs on the `coolify` network. If Veldrath services are on a different network, routing fails with `502 Bad Gateway`. Coolify typically expects services to either be on the `coolify` network or have the network explicitly attached. |
| **Recommendation** | Define a custom network (e.g., `veldrath`) and attach all services to it. Or attach all routable services to the `coolify` network. The safest approach: define a `veldrath` internal network for service-to-service communication, and explicitly connect only the services that need to be routed by Traefik (`server`, `foundry`, `web`) to the `coolify` network. |

#### Issue #7: GHCR Registry Authentication
| Field | Detail |
|-------|--------|
| **Severity** | 🟡 High |
| **Category** | Compose Structure |
| **Source** | [`docker-compose.prod.yml:33`](../docker-compose.prod.yml#L33), [`:59`](../docker-compose.prod.yml#L59), [`:73`](../docker-compose.prod.yml#L73), [`:87`](../docker-compose.prod.yml#L87) — `ghcr.io/kungraseri/...` image references |
| **Description** | All production services pull from `ghcr.io/kungraseri/*` (GitHub Container Registry). GHCR requires authentication even for public images when pulling at high rates or from non-GitHub infrastructure. Coolify needs registry credentials configured to pull these images. |
| **Coolify Impact** | **Image pull failures.** Without registry credentials, `docker pull` from GHCR will fail with `unauthorized: authentication required` or hit rate limits quickly. This is especially problematic during initial deploy or when pulling multiple images simultaneously. |
| **Recommendation** | Configure GHCR credentials in Coolify's Docker Registry settings. Use a GitHub Personal Access Token (PAT) with `read:packages` scope. Alternatively, if images are made truly public (no auth needed for pull), document that explicitly — but even then, rate limits apply (100 pulls/6hr unauthenticated vs 5000/hr authenticated). |

---

### 🟠 Medium Issues (7)

#### Issue #8: Only 3/10 Services Have Health Checks
| Field | Detail |
|-------|--------|
| **Severity** | 🟠 Medium |
| **Category** | Health |
| **Source** | [`docker-compose.yml:17-24`](../docker-compose.yml#L17-L24) (stalwart), [`:36-40`](../docker-compose.yml#L36-L40) (postgres), [`:81-86`](../docker-compose.yml#L81-L86) (server) |
| **Description** | Only `stalwart`, `postgres`, and `server` define health checks. The remaining 7 services (`discord`, `foundry`, `web`, `seq`, `prometheus`, `grafana`, `caddy`) have no health checks. Coolify uses health checks for zero-downtime deploys and service status monitoring — without them, Coolify cannot determine when a service is ready to receive traffic. |
| **Coolify Impact** | **No zero-downtime deploys.** Coolify cannot do rolling updates or wait-for-healthy deployments. It falls back to a basic "container started" check, which means traffic may be routed to a container before the application is ready to serve requests. For Blazor Server services (`foundry`, `web`), this could cause initial connection failures. |
| **Recommendation** | Add health checks to all routable services: `discord` (can probe the bot's internal health endpoint or just check the process), `foundry` (HTTP GET `/health`), `web` (HTTP GET `/health`), `seq` (HTTP GET `/`), `prometheus` (HTTP GET `/-/healthy`), `grafana` (HTTP GET `/api/health`). For `discord`, a simple process check or a minimal HTTP health endpoint exposed internally would suffice. |

#### Issue #9: Caddy depends_on Without Health Checks
| Field | Detail |
|-------|--------|
| **Severity** | 🟠 Medium |
| **Category** | Health / Operations |
| **Source** | [`docker-compose.prod.yml:23-29`](../docker-compose.prod.yml#L23-L29) — Caddy `depends_on` |
| **Description** | Caddy's depends_on uses mixed conditions: `server` with `service_healthy`, but `foundry` and `web` with only `service_started`. This means Caddy starts and begins routing traffic to `foundry` and `web` before those services are actually ready to serve HTTP requests. If the compose file changes to remove `caddy` (which is required for Coolify), this issue goes away for that service — but the pattern highlights that `foundry` and `web` lack health checks entirely. |
| **Coolify Impact** | **Partial.** The `caddy` service will be removed, so this specific depends_on gap disappears. However, the underlying problem — `foundry` and `web` having no health checks — means Coolify cannot guarantee they're ready before routing traffic. |
| **Recommendation** | Add health checks to `foundry` and `web` (see Issue #8). Coolify's built-in Traefik will use these health checks for upstream routing decisions. |

#### Issue #10: Monitoring/Infra Ports Published in Prod
| Field | Detail |
|-------|--------|
| **Severity** | 🟠 Medium |
| **Category** | Networking |
| **Source** | [`docker-compose.yml:139-140`](../docker-compose.yml#L139-L140) — seq `9001:80`; [`:151-152`](../docker-compose.yml#L151-L152) — prometheus `9003:9090`; [`:162-163`](../docker-compose.yml#L162-L163) — grafana `9002:3000`; [`:8-10`](../docker-compose.yml#L8-L10) — stalwart `9004:8082` and `5025:25` |
| **Description** | While `server`, `foundry`, `web`, and `postgres` correctly remove host ports in prod, the monitoring and infrastructure services (seq, prometheus, grafana, stalwart) still publish ports to the Docker host. In Coolify, this is unnecessary and potentially a security concern — Coolify can route to these services through Traefik with authentication middleware. Published ports bypass Coolify's access control. |
| **Coolify Impact** | **Security bypass.** Ports 9001-9004 on the host are directly accessible, bypassing Coolify's Traefik, authentication middleware, and access logs. Anyone who can reach the host IP on those ports can access Seq, Prometheus, Grafana, and Stalwart admin UIs without authentication (unless the service itself enforces it). Coolify's dashboard won't show these as routed services. |
| **Recommendation** | Remove host port mappings from `seq`, `prometheus`, `grafana`, and `stalwart`. Optionally add Traefik labels with `traefik.http.routers.seq.middlewares=auth` and Coolify's built-in basic auth or forward-auth middleware. For `stalwart` SMTP port 25 — this is a special case since SMTP is not HTTP and Traefik cannot route it; SMTP should remain published only if the service genuinely needs to receive email from the internet, otherwise keep it internal. |

#### Issue #11: `build:` Contexts in Base Compose
| Field | Detail |
|-------|--------|
| **Severity** | 🟠 Medium |
| **Category** | Compose Structure |
| **Source** | [`docker-compose.yml:43-45`](../docker-compose.yml#L43-L45) — server build; [`:96-98`](../docker-compose.yml#L96-L98) — discord build; [`:118-120`](../docker-compose.yml#L118-L120) — foundry build; [`:176-178`](../docker-compose.yml#L176-L178) — web build |
| **Description** | The base compose defines `build:` contexts for `server`, `discord`, `foundry`, and `web`. These require the full source tree and .NET SDK to be present on the deploy target. While Coolify *can* build Docker images, doing so for 4 .NET 10 projects (each with 5-10 project references) on a production server is slow and resource-intensive. The prod overlay correctly replaces these with `image:` references to GHCR. |
| **Coolify Impact** | **Minor.** If the merged Coolify compose uses only `image:` references (recommended), this is resolved. If `build:` stanzas leak into the Coolify compose, it triggers a full .NET SDK build on every deploy, requiring the entire source repo and .NET SDK on the Coolify host, consuming significant CPU/memory during builds. |
| **Recommendation** | Ensure the single Coolify compose file uses `image:` for all .NET services (as the prod overlay already does). Remove all `build:` stanzas. |

#### Issue #12: Bind-Mounted Config Files in Prod
| Field | Detail |
|-------|--------|
| **Severity** | 🟠 Medium |
| **Category** | Config / Volumes |
| **Source** | [`docker-compose.yml:16`](../docker-compose.yml#L16) — `./config/stalwart:/opt/stalwart/etc:ro`; [`:155`](../docker-compose.yml#L155) — `./config/prometheus/prometheus.yml:/etc/prometheus/prometheus.yml:ro`; [`:170-171`](../docker-compose.yml#L170-L171) — grafana provisioning and dashboards; [`docker-compose.prod.yml:20`](../docker-compose.prod.yml#L20) — `./Caddyfile:/etc/caddy/Caddyfile:ro` |
| **Description** | Several services use bind-mounted config files from the host filesystem. Coolify deploys from a Git repository — these config files must be present in the repo at the expected paths. The [`Caddyfile`](../Caddyfile) is at the repo root (works), [`config/stalwart/config.toml`](../config/stalwart/config.toml) is in the repo (works), and [`config/prometheus/prometheus.yml`](../config/prometheus/prometheus.yml) is in the repo (works). However, Coolify's deployment model clones the repo to a temporary working directory — the bind mount paths must be relative and within the repo tree. |
| **Coolify Impact** | **Works with caveats.** Relative bind mounts from the repo root are fine as long as the deploy context matches. The [`Caddyfile`](../Caddyfile) bind mount becomes irrelevant once the `caddy` service is removed. However, the [`config/stalwart/config.toml`](../config/stalwart/config.toml) contains `veldrath_dev`-style configuration (dev SMTP relay mode) that may not be suitable for production — it should be reviewed. |
| **Recommendation** | Keep bind mounts for config files that genuinely need version control. For secrets embedded in configs (like the Stalwart admin password via env var `ADMIN_SECRET`), ensure they use environment variable interpolation (already the case). Remove the Caddyfile bind mount when removing the `caddy` service. |

#### Issue #13: Missing `deploy.resources` Constraints
| Field | Detail |
|-------|--------|
| **Severity** | 🟠 Medium |
| **Category** | Operations |
| **Source** | All services in both compose files — absence of `deploy:` sections |
| **Description** | No service defines `deploy.resources.limits` or `deploy.resources.reservations` for CPU and memory. Coolify supports setting these per-service through its UI, but having them in the compose file provides sensible defaults. Without limits, a memory leak or runaway process in one service can starve others. |
| **Coolify Impact** | **Operational risk.** Coolify can still manage resources via its UI overrides, but the compose file provides the baseline. Without any limits, Coolify's container monitoring has no reference point for alerts. |
| **Recommendation** | Add `deploy.resources` blocks to production services. Suggested baselines: `server` (512MB-1GB memory, 1-2 CPU), `foundry` (256MB-512MB, 0.5-1 CPU), `web` (256MB-512MB, 0.5-1 CPU), `discord` (128MB-256MB, 0.25-0.5 CPU), `postgres` (256MB-1GB, 0.5-1 CPU), monitoring services (128MB-256MB each). |

#### Issue #14: DataProtection Key Volumes — No Backup Strategy
| Field | Detail |
|-------|--------|
| **Severity** | 🟠 Medium |
| **Category** | Config / Volumes |
| **Source** | [`docker-compose.yml:93`](../docker-compose.yml#L93) — `dataprotection_keys`; [`:132`](../docker-compose.yml#L132) — `foundry_dataprotection_keys`; [`:189`](../docker-compose.yml#L189) — `web_dataprotection_keys` |
| **Description** | ASP.NET Core Data Protection keys are stored in named Docker volumes. If these volumes are lost or corrupted, all existing auth cookies, antiforgery tokens, and session data become invalid — users are force-logged out. The keys are also not shared between `foundry` and `web` (they use separate volumes), meaning cookies issued by one service cannot be validated by the other, despite sharing `Auth__CookieDomain=.veldrath.com`. |
| **Coolify Impact** | **Data loss risk.** Named Docker volumes are managed by Docker, not by Coolify directly. Coolify's backup mechanisms may not cover these volumes unless explicitly configured. The separate key rings also mean cross-service auth validation doesn't actually work — each Blazor Server app has its own key ring. |
| **Recommendation** | Consider sharing a single DataProtection key volume (or a Redis-based key ring) between `foundry` and `web` so `Auth__CookieDomain=.veldrath.com` actually works for cross-subdomain auth. Ensure Coolify's volume backup strategy covers DataProtection keys. Document the backup requirement. |

---

### 🟢 Low Issues (3)

#### Issue #15: `restart: unless-stopped` Already Set in Prod — Good
| Field | Detail |
|-------|--------|
| **Severity** | 🟢 Low (informational) |
| **Category** | Operations |
| **Source** | [`docker-compose.prod.yml`](../docker-compose.prod.yml) — all services have `restart: unless-stopped` |
| **Description** | All 10 services have `restart: unless-stopped` in the prod overlay. This is the correct policy for Coolify — containers restart on crash but can be deliberately stopped via Coolify's UI. |
| **Coolify Impact** | **None — compatible.** Coolify expects `unless-stopped` or `always`. |
| **Recommendation** | No change needed. Keep `restart: unless-stopped` on all services. |

#### Issue #16: `depends_on` Chains Are Sound
| Field | Detail |
|-------|--------|
| **Severity** | 🟢 Low (informational) |
| **Category** | Operations |
| **Source** | [`docker-compose.yml:87-91`](../docker-compose.yml#L87-L91) — server depends_on postgres+stalwart; [`:110-114`](../docker-compose.yml#L110-L114) — discord depends_on postgres+server; etc. |
| **Description** | The startup order dependency chain is well-structured: `postgres` + `stalwart` → `server` → `foundry`/`web`/`discord`/`prometheus` → `grafana`. The only gap is `caddy` → `foundry`/`web` using `service_started` instead of `service_healthy`, but that's resolved when `caddy` is removed. |
| **Coolify Impact** | **Minimal.** Coolify respects `depends_on` with `condition: service_healthy` (Docker Compose v3.8+). |
| **Recommendation** | When adding health checks to `foundry` and `web` (Issue #8), consider adding internal depends_on between them and other services if there are hidden dependencies. |

#### Issue #17: Seq, Prometheus, Grafana — Internal Tooling Exposure
| Field | Detail |
|-------|--------|
| **Severity** | 🟢 Low |
| **Category** | Networking |
| **Source** | [`docker-compose.yml:137-173`](../docker-compose.yml#L137-L173) — seq, prometheus, grafana definitions |
| **Description** | These monitoring services are developer tooling. In a Coolify deployment, they consume resources and expose additional attack surface. Coolify has its own metrics/logs dashboard, so these services may be partially redundant. |
| **Coolify Impact** | **Minor.** They work fine but add operational overhead (backups, updates, resource consumption). Coolify can route them through Traefik with authentication, which is an improvement over the current setup. |
| **Recommendation** | Keep them if the team relies on them, but route them through Traefik (see Issue #10). Consider whether Coolify's built-in monitoring can replace Prometheus+Grafana, and whether Coolify's own log viewer can replace Seq. |

---

## Architecture Decision: Caddy vs. Traefik

This is the **core fork in the road**. Every other decision flows from this one. There are two viable paths:

### Path A: Replace Caddy with Coolify's Traefik (Recommended)

**What changes:**
- Remove the `caddy` service entirely
- Remove [`Caddyfile`](../Caddyfile) bind mount
- Replace Caddyfile routing rules with Traefik labels on each application service
- Coolify manages TLS certificates, domain routing, and middleware

**Sample Traefik labels for `server`:**
```yaml
server:
  labels:
    - "traefik.enable=true"
    - "traefik.http.routers.server.rule=Host(`api.veldrath.com`)"
    - "traefik.http.routers.server.tls.certresolver=coolify"
    - "traefik.http.routers.server.entrypoints=websecure"
    - "traefik.http.services.server.loadbalancer.server.port=8080"
```

**Sample Traefik labels for `foundry` (with sticky sessions):**
```yaml
foundry:
  labels:
    - "traefik.enable=true"
    - "traefik.http.routers.foundry.rule=Host(`foundry.veldrath.com`)"
    - "traefik.http.routers.foundry.tls.certresolver=coolify"
    - "traefik.http.routers.foundry.entrypoints=websecure"
    - "traefik.http.services.foundry.loadbalancer.server.port=8081"
    - "traefik.http.services.foundry.loadbalancer.sticky.cookie.name=foundry_lb"
    - "traefik.http.services.foundry.loadbalancer.sticky.cookie.secure=true"
```

**Pros:**
- Full Coolify integration: domain management, TLS, middleware all work through the UI
- Single TLS termination point (Traefik) — no cert duplication
- Coolify health-check-aware routing (Traefik only routes to healthy instances)
- Zero-downtime deploys via Coolify's rolling update mechanism
- Coolify middleware (rate limiting, IP whitelist, basic auth) works out of the box
- One less container to manage, patch, and monitor

**Cons:**
- Must port Caddyfile logic to Traefik labels (one-time migration)
- Team must learn Traefik label syntax (well-documented, straightforward)
- Loses Caddy's `lb_try_duration 30s` retry window — Traefik has its own retry middleware capabilities
- Any custom Caddy modules or advanced Caddyfile features need Traefik equivalents

### Path B: Keep Caddy Alongside Coolify's Traefik (Not Recommended)

**What changes:**
- Remove Caddy's port 80/443 mappings to avoid collision
- Place Caddy behind Coolify's Traefik (Traefik → Caddy → app services)
- Or: Run Caddy on non-standard ports and configure Coolify to route to Caddy

**Pros:**
- No Caddyfile changes needed
- Preserves existing Caddy logic and retry behavior
- Team keeps familiar tooling

**Cons:**
- **Double proxy hop**: Traefik → Caddy → app service adds latency and complexity
- **TLS duplication**: Both Traefik and Caddy would try to manage TLS unless one is explicitly disabled
- **Coolify UI blind spot**: Coolify cannot see through Caddy to the real services; health status and routing are opaque
- **Sticky session complexity**: Must ensure cookie stickiness survives the Traefik→Caddy→app hop
- **Defeats the purpose of Coolify**: You lose domain management, middleware, and routing visibility
- **Higher resource usage**: Running two reverse proxies

**Verdict: Path A (replace Caddy) is the only practical option for meaningful Coolify integration.**

---

## Migration Path

Below is the recommended ordered sequence for creating a Coolify-compatible deployment. Each step builds on the previous ones.

### Phase 1: Prepare the Codebase (Pre-Coolify)

1. **Add health check endpoints to `foundry` and `web`**
   - Both are ASP.NET Core apps — a simple `/health` endpoint returning 200
   - Add the health check middleware in `Program.cs`: `app.MapHealthChecks("/health")`
   - Ensure the Docker image includes `curl` or use the ASP.NET Core health check middleware (no curl needed)

2. **Add health check to `discord`**
   - Expose a minimal HTTP health endpoint (port 8080 or similar) with a Kestrel listener
   - Or: validate that the process check alone is sufficient for Coolify's deploy strategy

3. **Generate a strong Postgres production password**
   - Use `openssl rand -hex 32` or similar
   - Document it as a Coolify-managed secret

4. **Review [`config/stalwart/config.toml`](../config/stalwart/config.toml) for production suitability**
   - Current config says "minimal dev configuration" and "safe here: port 25 is only reachable from within the Docker network"
   - If production uses an external SMTP relay, the Stalwart container may not be needed at all
   - If production uses Stalwart as an actual mail server, review relay settings

### Phase 2: Create the Single Coolify Compose File

5. **Create `docker-compose.coolify.yml`**
   - Merge all services from [`docker-compose.yml`](../docker-compose.yml) and [`docker-compose.prod.yml`](../docker-compose.prod.yml) into one file
   - Remove the `caddy` service entirely
   - Replace all `build:` stanzas with `image:` references to GHCR
   - Set `ASPNETCORE_ENVIRONMENT=Production` on all .NET services
   - Add `restart: unless-stopped` to all services
   - Set `ports: []` (or remove ports) on services that should only be reachable via Traefik: `server`, `foundry`, `web`, `postgres`
   - Remove host port mappings from `seq`, `prometheus`, `grafana`, `stalwart` (admin UI)
   - Keep `5025:25` for Stalwart SMTP only if inbound email is needed; otherwise remove

6. **Add Traefik labels to routable services**
   - `server`: router + TLS for `api.veldrath.com`, port 8080
   - `foundry`: router + TLS for `foundry.veldrath.com`, port 8081, sticky cookie `foundry_lb`
   - `web`: router + TLS for `veldrath.com` (optionally `www.veldrath.com` redirect), port 8082, sticky cookie `web_lb`
   - Optionally: `seq`, `prometheus`, `grafana` with auth middleware

7. **Add health checks to all services**
   - Port existing health checks from base compose (`stalwart`, `postgres`, `server`)
   - Add new health checks for `discord`, `foundry`, `web`, `seq`, `prometheus`, `grafana`

8. **Define a custom Docker network**
   - `networks.veldrath:` (internal, driver: bridge)
   - Attach all Veldrath services to `veldrath` network
   - Attach `server`, `foundry`, `web` additionally to the `coolify` network (external)

9. **Add `deploy.resources` blocks**
   - Memory limits and CPU reservations for each service
   - Use conservative defaults; Coolify admins can override via UI

### Phase 3: Configure Coolify

10. **Set up GHCR registry credentials in Coolify**
    - Docker Registry → Add → GHCR with PAT (read:packages)

11. **Create Coolify Secrets for sensitive values**
    - `POSTGRES_PASSWORD` (strong random value)
    - `JWT_KEY` (32+ chars)
    - `DISCORD_TOKEN`
    - All OAuth client IDs and secrets
    - `STALWART_ADMIN_SECRET`
    - `SEQ_ADMIN_PASSWORD`
    - `GF_ADMIN_PASSWORD`
    - `EMAIL_PASSWORD` (if using external SMTP)

12. **Configure domains in Coolify UI**
    - `api.veldrath.com` → `server` service
    - `foundry.veldrath.com` → `foundry` service
    - `veldrath.com` → `web` service

### Phase 4: Deploy and Validate

13. **Initial deploy on Coolify**
    - Pull images, start all services, verify health checks pass
    - Check Traefik dashboard for correct routing

14. **Validate Blazor Server sticky sessions**
    - Open `foundry.veldrath.com` and `veldrath.com`
    - Verify SignalR circuit stays connected
    - Check browser cookies for `foundry_lb` and `web_lb`

15. **Validate OAuth flows**
    - Test Discord, Google, Microsoft login through `foundry`
    - Verify redirect URIs match Coolify's domain configuration

16. **Validate monitoring**
    - Confirm Seq, Prometheus, Grafana are reachable (via Traefik or Coolify's internal network)
    - Verify Prometheus is scraping the server metrics endpoint

### Phase 5: Cleanup

17. **Archive old Hetzner deployment artifacts**
    - [`Caddyfile`](../Caddyfile) — no longer needed (keep for reference)
    - [`docker-compose.prod.yml`](../docker-compose.prod.yml) — superseded by `docker-compose.coolify.yml`
    - CI deploy workflows — may need updating for Coolify deploy triggers

---

## Summary of Required Changes

| # | Change | Type | Effort |
|---|--------|------|--------|
| 1 | Remove `caddy` service | Delete | Low |
| 2 | Merge base + prod into single `docker-compose.coolify.yml` | Create | Medium |
| 3 | Add Traefik labels (3 services) | Add | Low |
| 4 | Add sticky-session Traefik middleware (2 services) | Add | Low |
| 5 | Add health check to `foundry` | Add | Low |
| 6 | Add health check to `web` | Add | Low |
| 7 | Add health check to `discord` | Add | Medium |
| 8 | Add health checks to `seq`, `prometheus`, `grafana` | Add | Low |
| 9 | Replace hardcoded Postgres password with secret reference | Change | Low |
| 10 | Override connection strings in prod | Change | Low |
| 11 | Define custom Docker network | Add | Low |
| 12 | Add `deploy.resources` blocks (10 services) | Add | Medium |
| 13 | Remove published ports from monitoring services | Remove | Low |
| 14 | Configure GHCR registry auth in Coolify | Config | Low |
| 15 | Create Coolify secrets | Config | Medium |
| 16 | Review Stalwart production config | Review | Low |

**Total new file:** 1 (`docker-compose.coolify.yml`)
**Total modified files:** 2-3 (health check additions in `foundry`/`web`/`discord` `Program.cs`)
**Total deleted/replaced files:** 0 (existing files kept; `docker-compose.prod.yml` and `Caddyfile` archived for reference)

---

## Appendix: Key File Reference

| File | Role | Coolify Fate |
|------|------|-------------|
| [`docker-compose.yml`](../docker-compose.yml) | Base service definitions | Merged into `docker-compose.coolify.yml` |
| [`docker-compose.prod.yml`](../docker-compose.prod.yml) | Production overrides | Merged into `docker-compose.coolify.yml`; archived |
| [`docker-compose.dev.yml`](../docker-compose.dev.yml) | Dev hot-reload compose | **Unchanged** — not used for Coolify |
| [`Caddyfile`](../Caddyfile) | TLS + domain routing | **Removed** — replaced by Traefik labels |
| [`.env.example`](../.env.example) | Environment variable template | Replaced by Coolify Secrets UI |
| [`.dockerignore`](../.dockerignore) | Build context exclusions | **Unchanged** |
| [`config/stalwart/config.toml`](../config/stalwart/config.toml) | Stalwart mail server config | Reviewed for production suitability |
| [`config/prometheus/prometheus.yml`](../config/prometheus/prometheus.yml) | Prometheus scrape config | **Unchanged** |
| [`config/grafana/provisioning/`](../config/grafana/provisioning/) | Grafana dashboards/datasources | **Unchanged** |
| [`Veldrath.Server/Dockerfile`](../Veldrath.Server/Dockerfile) | Server image build | **Unchanged** (CI builds; Coolify pulls image) |
| [`RealmFoundry/Dockerfile`](../RealmFoundry/Dockerfile) | Foundry image build | **Unchanged** |
| [`Veldrath.Web/Dockerfile`](../Veldrath.Web/Dockerfile) | Web image build | **Unchanged** |
| [`Veldrath.Discord/Dockerfile`](../Veldrath.Discord/Dockerfile) | Discord image build | **Unchanged** |
| [`docs/deployment.md`](../docs/deployment.md) | Deployment documentation | Requires update for Coolify workflow |
