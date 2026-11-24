---
kind: work-item
date: 2026-04-12
area: infra
title: "Monitoring Infrastructure"
---

### Session-32 (2026-04-12) — Monitoring Infrastructure
- Added Prometheus metrics integration: `prometheus-net`, `prometheus-net.DotNetRuntime`, `prometheus-net.AspNetCore`
- Added `UseHttpMetrics()`, `DotNetRuntimeStatsBuilder` to Program.cs (`Veldrath.Server/Program.cs:530`)
- Provisioned 4 Grafana dashboards: `10915.json` (.NET Runtime), `19194.json`, `23178.json`, `23179.json`
- Added Prometheus scrape config and Grafana provisioning configs
- Updated `docker-compose.yml` with Prometheus and Grafana services
- Pinned Grafana image to 11.6.1
