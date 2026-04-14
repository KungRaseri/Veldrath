# Deployment Guide — Veldrath on Hetzner

This document covers everything needed to prepare, execute, and verify a production deployment of the Veldrath stack on a Hetzner VPS using Docker Compose and Caddy.

---

## Architecture Overview

```
Internet
   │
   ▼
Caddy (80/443)          ← TLS termination, auto Let's Encrypt
   ├── api.veldrath.com     → server:8080  (Veldrath.Server + SignalR)
   └── foundry.veldrath.com → foundry:8081 (RealmFoundry Blazor portal)

Internal (not publicly exposed):
   ├── postgres:5432
   ├── stalwart:25 (SMTP relay — port 25 open on firewall)
   ├── seq:80
   ├── prometheus:9090
   └── grafana:3000

No public port exposure:
   └── discord  (outbound-only bot worker)
```

Deploys are per-service. GitHub Actions builds and pushes a Docker image to GHCR on every merge to `main`. A manual approval gate then triggers SSH onto the VPS to pull `:latest` and restart only the changed container.

---

## One-Time VPS Setup

### 1. Provision the server

- Provider: Hetzner Cloud  
- Recommended size: **CX32** (4 vCPU, 8 GB RAM) or **CX22** (2 vCPU, 4 GB) for lower load  
- OS: **Ubuntu 24.04 LTS**  
- Enable Hetzner Cloud Firewall (see step 4)

### 2. Install Docker

```bash
curl -fsSL https://get.docker.com | sh
# Docker Compose plugin is included — verify with:
docker compose version
```

### 3. Create a dedicated deploy user

```bash
adduser deploy
usermod -aG docker deploy
# Switch to the deploy user to set up SSH
su - deploy
mkdir -p ~/.ssh
chmod 700 ~/.ssh
```

Paste the **ed25519 public key** (generated in the GitHub secrets step below) into:

```bash
~/.ssh/authorized_keys
chmod 600 ~/.ssh/authorized_keys
```

### 4. Configure Hetzner Cloud Firewall

Allow inbound on:

| Port | Protocol | Purpose |
|------|----------|---------|
| 22   | TCP      | SSH |
| 80   | TCP      | HTTP (Caddy ACME challenge + redirect) |
| 443  | TCP      | HTTPS |
| 25   | TCP      | SMTP inbound to Stalwart |

All other inbound traffic should be blocked. This keeps Postgres, Seq, Grafana, and Stalwart's admin UI off the public internet.

### 5. Create the work directory

```bash
mkdir -p /opt/veldrath
chown deploy:deploy /opt/veldrath
```

### 6. Place the `.env` file on the VPS

Copy `.env.example` from the repo to `/opt/veldrath/.env` and fill in every value:

```bash
scp .env.example deploy@<VPS_IP>:/opt/veldrath/.env
ssh deploy@<VPS_IP>
nano /opt/veldrath/.env
```

Required values to set:

| Key | Notes |
|-----|-------|
| `JWT_KEY` | 32+ random characters — generate with `openssl rand -hex 32` |
| `DISCORD_TOKEN` | Bot token from Discord Developer Portal |
| `DISCORD_DEV_GUILD_ID` | Set to `0` for global command registration in production |
| `OAUTH_DISCORD_CLIENT_ID/SECRET` | From Discord Developer Portal → OAuth2 |
| `OAUTH_GOOGLE_CLIENT_ID/SECRET` | From Google Cloud Console → Credentials |
| `OAUTH_MICROSOFT_CLIENT_ID/SECRET` | From Azure Portal → App registrations |
| `STALWART_ADMIN_SECRET` | Admin password for Stalwart web UI |
| `SEQ_ADMIN_PASSWORD` | Admin password for Seq log viewer |
| `GF_ADMIN_PASSWORD` | Admin password for Grafana |
| `FOUNDRY_PUBLIC_SERVER_URL` | `https://api.veldrath.com` |
| `EMAIL_SMTP_HOST` | SMTP relay host (or leave blank to use NullEmailSender/Stalwart) |
| `EMAIL_USER` / `EMAIL_PASSWORD` | SMTP credentials if using an external relay |
| `EMAIL_SENDER_ADDRESS` | `noreply@veldrath.com` |

### 7. Initial stack deployment

The first deployment must be done manually to initialize the database and pull all images:

```bash
ssh deploy@<VPS_IP>
cd /opt/veldrath
# Copy compose files and Caddyfile from the repo first (scp or git clone)
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

Wait for all services to become healthy:

```bash
docker compose -f docker-compose.yml -f docker-compose.prod.yml ps
```

---

## GitHub Setup (One-Time)

### 1. Generate an SSH key pair for GitHub Actions

Run this locally (or on any machine — the private key is only needed to add to GitHub):

```bash
ssh-keygen -t ed25519 -C "github-actions-deploy" -f ~/.ssh/veldrath_deploy -N ""
```

- **Public key** → paste into `/home/deploy/.ssh/authorized_keys` on the VPS (step 3 above)
- **Private key** → add to GitHub as a secret (step 2 below)

### 2. Add secrets to the `production` environment

In the repository: **Settings → Environments → New environment → name it `production`**

- Add yourself (or a team) as a **required reviewer**
- Optionally restrict deployments to the `main` branch only

Under the `production` environment, add these secrets:

| Secret | Value |
|--------|-------|
| `HETZNER_HOST` | VPS IP address or hostname |
| `HETZNER_SSH_USER` | `deploy` |
| `HETZNER_SSH_KEY` | Contents of `~/.ssh/veldrath_deploy` (the **private** key) |

### 3. Update OAuth redirect URIs

In each OAuth provider's developer console, add the production callback URLs:

| Provider | Callback URL |
|----------|-------------|
| Discord | `https://api.veldrath.com/api/auth/oauth/discord/callback` |
| Google | `https://api.veldrath.com/api/auth/oauth/google/callback` |
| Microsoft | `https://api.veldrath.com/api/auth/oauth/microsoft/callback` |

### 4. Configure DNS

Add A records pointing to the VPS IP:

| Hostname | Type | Value |
|----------|------|-------|
| `api.veldrath.com` | A | `<VPS IP>` |
| `foundry.veldrath.com` | A | `<VPS IP>` |

Caddy will automatically provision Let's Encrypt certificates once DNS resolves.

---

## Deployment Flow (Ongoing)

Once setup is complete, every merge to `main` follows this flow automatically:

```
1. Developer merges a PR to main
2. CI workflow runs (build, test, publish Docker image to GHCR)
3. Deploy job appears as "waiting for review" in GitHub Actions
4. Reviewer approves in the Actions UI
5. GitHub Actions SSHes into the VPS:
   a. Syncs compose files and Caddyfile
   b. docker compose pull <service>
   c. docker compose up -d --no-deps --wait <service>
   d. caddy reload (server/foundry only)
6. Only the changed container restarts (~5-15 seconds)
```

### Which CI triggers which deploy

| Change path | CI workflow | Container restarted |
|-------------|-------------|---------------------|
| `Veldrath.Server/**`, `RealmEngine.Core/**`, etc. | `ci-server.yml` | `server` |
| `RealmFoundry/**`, `Veldrath.Contracts/**` | `ci-foundry.yml` | `foundry` |
| `Veldrath.Discord/**` | `ci-discord.yml` | `discord` |

Postgres, Caddy, Stalwart, Seq, Prometheus, and Grafana are **never** restarted by CI deploys.

---

## Deployment Verification

After each deploy, verify with:

### Server

```bash
curl -s https://api.veldrath.com/health | jq .
```

Expected response:

```json
{
  "status": "Healthy",
  "checks": {
    "database": { "status": "Healthy" },
    "game-engine": { "status": "Healthy" }
  }
}
```

### Foundry

```bash
curl -I https://foundry.veldrath.com
```

Expected: `HTTP/2 200`

### Docker image digest

On the VPS, confirm the new image digest was pulled:

```bash
docker inspect ghcr.io/kungraseri/veldrath-server:latest --format '{{.RepoDigests}}'
```

Compare against the digest shown in the GitHub Actions build log.

### Caddy TLS

```bash
curl -v https://api.veldrath.com/health 2>&1 | grep "subject\|issuer\|expire"
```

Certificate should be issued by Let's Encrypt and valid.

### SignalR (Veldrath.Client)

Launch the desktop client pointed at `https://api.veldrath.com`. Hub negotiation should complete and the client should reach the character select screen without errors.

---

## Restarting a Single Service Manually

If you need to restart a service outside of CI (e.g. after editing `.env`):

```bash
ssh deploy@<VPS_IP>
cd /opt/veldrath
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --no-deps --wait <service>
```

Replace `<service>` with: `server`, `foundry`, `discord`, `postgres`, `stalwart`, `seq`, `prometheus`, or `grafana`.

## Viewing Logs

```bash
# Tail a service
docker compose -f docker-compose.yml -f docker-compose.prod.yml logs -f server

# Or use Seq at http://<VPS_IP>:8083 (firewall-internal only — access via SSH tunnel)
ssh -L 8083:localhost:8083 deploy@<VPS_IP>
# Then open http://localhost:8083 in your browser
```
