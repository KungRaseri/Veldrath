# Infrastructure Coolify Extraction Plan

## Overview

Move Postgres, Grafana, Seq, and Prometheus out of [docker-compose.coolify.yml](../docker-compose.coolify.yml) and manage them as independent Coolify resources/services. All services share the `veldrath` Docker network.

## Architecture

### Coolify-Managed (extracted)
| Service | Hostname | Type | Status |
|---------|----------|------|--------|
| PostgreSQL 17 | `veldrath-db` | Coolify Database Resource | ✅ Done |
| Grafana 11.6.1 | `veldrath-grafana` | Coolify Service | ✅ Done |
| Seq | `veldrath-seq` | Coolify Service (Docker image) | ⏳ To Do |
| Prometheus | `veldrath-prometheus` | Coolify Service (Docker image) | ⏳ To Do |

### Remaining in docker-compose.coolify.yml
| Service | Description | Depends On |
|---------|-------------|------------|
| stalwart | Mail server (SMTP) | — |
| server | Veldrath.Server ASP.NET Core | veldrath-db, stalwart, veldrath-seq |
| discord | Veldrath.Discord bot | veldrath-db, server |
| foundry | RealmFoundry Blazor portal | server |
| web | Veldrath.Web frontend | server |

## Connection Reference Changes

### docker-compose.coolify.yml Updates

| Service | Env Variable | Old Value | New Value |
|---------|-------------|-----------|-----------|
| server | `ConnectionStrings__DefaultConnection` | `Host=postgres;...` | `Host=veldrath-db;...` |
| server | `Seq__ServerUrl` | `http://seq` | `http://veldrath-seq` |
| discord | `ConnectionStrings__DefaultConnection` | `Host=postgres;...` | `Host=veldrath-db;...` |

### Grafana Provisioning (configure in Coolify Grafana UI)
- **Datasource**: Prometheus at `http://veldrath-prometheus:9090`
- **Dashboards**: Import from [`config/grafana/dashboards/`](../config/grafana/dashboards/):
  - `10915.json` — ASP.NET Core metrics
  - `19194.json` — .NET runtime
  - `23178.json` — Postgres
  - `23179.json` — Prometheus rate
- **Provisioning configs** (reference, no longer mounted):
  - [`config/grafana/provisioning/datasources/prometheus.yml`](../config/grafana/provisioning/datasources/prometheus.yml) — datasource definition
  - [`config/grafana/provisioning/dashboards/dashboards.yml`](../config/grafana/provisioning/dashboards/dashboards.yml) — dashboard provider

### Prometheus Config
- The scrape config [`config/prometheus/prometheus.yml`](../config/prometheus/prometheus.yml) targets `server:8080` — this still works since the server remains in the compose on the same `veldrath` network.
- Mount this file into the Coolify Prometheus service, or copy its contents into Coolify's config file editor.

## Seq Setup Instructions (Coolify Service)

| Setting | Value |
|---------|-------|
| **Image** | `datalust/seq:latest` |
| **Network** | `veldrath` |
| **Hostname** | `veldrath-seq` |
| **Ports** | `80` (internal) → Coolify auto-assigns external |
| **Volumes** | Persistent mount for `/data` (named volume or bind mount) |

### Environment Variables
```
ACCEPT_EULA=Y
SEQ_FIRSTRUN_ADMINPASSWORD=${SEQ_ADMIN_PASSWORD}
```

### Health Check (optional)
```
CMD-SHELL curl -f http://localhost:80 || exit 1
Interval: 10s, Retries: 5, StartPeriod: 10s
```

## Prometheus Setup Instructions (Coolify Service)

| Setting | Value |
|---------|-------|
| **Image** | `prom/prometheus:latest` |
| **Network** | `veldrath` |
| **Hostname** | `veldrath-prometheus` |
| **Ports** | `9090` (internal) → Coolify auto-assigns external |
| **Volumes** | Persistent mount for `/prometheus` |
| **Config** | Mount [`config/prometheus/prometheus.yml`](../config/prometheus/prometheus.yml) to `/etc/prometheus/prometheus.yml` (read-only) |

### Scrape Config (in prometheus.yml)
```yaml
global:
  scrape_interval:     15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: veldrath-server
    static_configs:
      - targets: ['server:8080']
    metrics_path: /metrics
```

The server is still on the `veldrath` network via docker-compose, so DNS resolution `server:8080` works fine from Prometheus.

### Health Check (optional)
```
CMD-SHELL wget --no-verbose --tries=1 --spider http://localhost:9090/-/healthy || exit 1
Interval: 15s, Retries: 3, StartPeriod: 10s
```

## Migration Steps

### Phase 1 — Prepare Coolify Services
1. Create Seq service in Coolify (image: `datalust/seq:latest`) with above config
2. Create Prometheus service in Coolify (image: `prom/prometheus:latest`) with above config
3. Verify both are healthy and on `veldrath` network

### Phase 2 — Update Grafana
1. In Coolify Grafana resource, add Prometheus datasource pointing to `http://veldrath-prometheus:9090`
2. Import dashboards from [`config/grafana/dashboards/`](../config/grafana/dashboards/)

### Phase 3 — Update docker-compose.coolify.yml
1. Remove `postgres`, `seq`, `prometheus`, `grafana` service definitions
2. Remove their volumes from the `volumes:` section
3. Update `server` connection string: `Host=postgres` → `Host=veldrath-db`
4. Update `server` Seq URL: `http://seq` → `http://veldrath-seq`
5. Update `discord` connection string: `Host=postgres` → `Host=veldrath-db`
6. Keep `veldrath` network definition (still needed for inter-service DNS)
7. Remove `coolify` network dependency from grafana/prometheus/seq/postgres (they're now Coolify resources)

### Phase 4 — Deploy
1. Deploy updated `docker-compose.coolify.yml` to Coolify
2. Verify all services can reach their dependencies by hostname
3. Check server logs for Seq connectivity
4. Verify Prometheus is scraping `server:8080/metrics`
5. Verify Grafana dashboards display data

## Rollback

If something goes wrong:
1. Revert docker-compose.coolify.yml to the previous version
2. Re-add the infrastructure services back into compose
3. The old compose file is preserved as `docker-compose.coolify.yml` in git history

## Files Changed

- [`docker-compose.coolify.yml`](../docker-compose.coolify.yml) — removed 4 services, updated 3 env vars
- [`plans/infrastructure-coolify-extraction-plan.md`](infrastructure-coolify-extraction-plan.md) — this plan (new file)
