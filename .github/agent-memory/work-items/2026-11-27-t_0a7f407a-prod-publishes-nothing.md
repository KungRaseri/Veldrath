---
kind: work-item
date: 2026-11-27
area: infra
card: t_0a7f407a
title: "Production publishes nothing: `ports: []` is not a reset, and ufw does not cover Docker"
---

### Session t_0a7f407a (2026-11-27) — the prod overlay published six host ports

Security epic PR 5, `security-remediation.md` §6. The card body was wrong and the operator's comment on
the card was right: the six ports were not in `docker-compose.prod.yml`, they were **inherited** from
`docker-compose.yml`. `server`, `web` and `postgres` already said `ports: []` in the overlay, and Compose
merges `ports` lists across `-f` files — an empty list contributes nothing, it does not clear the base
list. Rendered with Compose v5.8.0, the pair published `5433` (postgres), `8081` (web), `9000` (server),
`9001` (seq), `9002` (grafana) and `9003` (prometheus); `seq`, `prometheus` and `grafana` had no `ports`
key in the overlay at all.

**Fix:** `ports: !reset []` on all six services in `docker-compose.prod.yml` (`!reset` needs Compose
≥ 2.24). Base file untouched. `docker compose -f docker-compose.yml -f docker-compose.prod.yml config`
now renders **zero** `published:` lines and still lists all seven services. Note that a generic YAML
language server flags `!reset` as an unresolved tag — that is the editor's schema, not Compose, which
accepts it.

### The gate

`scripts/check-prod-ports.mjs` (static mode) computes Compose's documented `ports` merge and fails on any
publication outside an `ALLOWLIST` (empty). It also reads `docker-compose.coolify.yml` as a second
production stack (it publishes nothing; `expose` only) and refuses to pass if a service disappears from
the merge. `--render <file>` checks `docker compose … config --format json` instead, applies the same
publication allowlist **and the same expected-service check**, and is how the static merge is
parity-tested: `scripts/tests/prod-ports.test.mjs` renders the checked-in overlay (zero publications)
**and** a mutation with `!reset` reverted to `ports: []`, then asserts both the render and the static
merge name the same six ports. The tests remove their fixture directories on exit.

**CI cannot run `docker compose`**: the `forgejo-job` image ships the docker CLI but no compose plugin,
and the runner's `valid_volumes` allows only `/var/run/docker.sock`, so the host's
`/usr/libexec/docker/cli-plugins` cannot be mounted into a job. That is why the gate parses the files
instead of rendering them, and why the parity test exists. `.github/workflows/ci-prod-ports.yml` runs the
unit tests and the check on every PR that touches a compose file.

### Firewall facts (measured 2026-11-27, they are what `docs/deployment.md` now states)

- The Veldrath resources run on `kung-lab-2`; the Coolify Traefik front is `kung-lab-hub` (its access log
  routes `veldrath@file` on to `https://192.168.50.100:443`).
- `kung-lab-2` ufw: `Default: deny (incoming)`, inbound rules only `22/tcp` from the LAN and the tailnet.
  `kung-lab-hub` ufw: `80/tcp` and `443/tcp` from anywhere, `41641/udp`, all inbound from
  `192.168.50.0/24` / `100.64.0.0/10` / `10.0.0.0/8`, plus SSH from LAN/tailnet.
- **ufw does not cover published container ports.** Both hosts are `default deny incoming` and
  `docker-proxy` still holds `0.0.0.0:8080` for `coolify-proxy` itself, plus `:5173`, `:3000` etc. on
  `kung-lab-2` for other Coolify stacks, because Docker writes its own `DOCKER`/`FORWARD` rules. The
  publication is the real control. Among the ports in the deployment table, the only `0.0.0.0` listeners
  on either host are `22` (sshd) and `80`/`443`/`8080` (`coolify-proxy`); `veldrath-server` publishes
  nothing (`8080/tcp` container-only).
- External TCP checks from check-host.net nodes: `80` and `443` connect; `22`, `3306`, `5432`, `6379`,
  `4222`, `25565`, `7777`, `8080` all time out. `199.36.89.7` is the lab's own egress IP, i.e. the
  router's WAN address, so veldrath.com's A record is not a third-party relay.
- `docker-compose.prod.yml`'s header said "veldrath.com on Hetzner"; the deployment is Coolify on
  `kung-lab-2`. Fixed in that header (the string did not appear in `docs/deployment.md`).
- Left alone as out of scope: `docker-compose.prod.yml` still pulls `ghcr.io/kungraseri/*` images, which
  no CI job publishes any more (images go to `git.kungraseri.cloud/veldrath/*`).
