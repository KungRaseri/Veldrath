# Your Project

<!-- Replace the badge URL with your repository owner/name. -->
[![Node CI](https://github.com/<owner>/<repo>/actions/workflows/node.yml/badge.svg)](https://github.com/<owner>/<repo>/actions/workflows/node.yml)

A starter built as a **TypeScript npm-workspaces monorepo** (`shared/`, `engine/`, `server/`,
`client/`): a pure logic engine, a **Colyseus** server hosting authoritative WebSocket sessions,
and a SvelteKit client with Better Auth.

## 📦 Workspaces

| Package | Role |
| --- | --- |
| [`shared/`](shared) — `@your-scope/shared` | Shared contracts: protocol types, DTOs, enums, constants, the single Drizzle schema + DB config, and Colyseus room schemas under `@your-scope/shared/schema` (subpath exports: `./db`, `./schema`). |
| [`engine/`](engine) — `@your-scope/engine` | Pure TypeScript logic engine (domain, systems, persistence). No I/O, no network. Depends only on `@your-scope/shared`. |
| [`server/`](server) — `@your-scope/server` | **Colyseus** server hosting authoritative sessions in rooms; Postgres persistence via Drizzle/`pg`. |
| [`client/`](client) | SvelteKit app (Svelte 5 runes, Vite, Tailwind 4, adapter-node) using the `@colyseus/sdk` client and Better Auth. |

See [`docs/`](docs) for your architecture documentation.

## 🧪 Testing & quality

- **Vitest** per workspace (`engine` / `shared` / `server`: `vitest run`; `client`: node + browser
  projects + Playwright e2e).
- **Coverage gate** — per-workspace thresholds in the workspace vitest configs. Start with a simple
  global gate and raise it as your suites grow (the copied configs include an example gate).
- **ESLint** (flat config, `typescript-eslint`) + **Prettier**.

## 🛠️ Getting Started

Prerequisites: **Node.js >= 20** and **PostgreSQL** (for DB-backed features).

```bash
# 1. Install (once, at the repo root — installs all workspaces)
npm install
```

> **npm 12 note:** if install fails with `EALLOWGIT`, run `npm install --allow-git=all` (the
> lockfile may resolve an unused git dependency such as `uWebSockets.js`).

```bash
# 2. Configure env (Windows: `copy`, elsewhere: `cp`)
copy client\.env.example client\.env
copy server\.env.example server\.env
#   DATABASE_URL and WS_AUTH_SECRET must match across both files.
```

```bash
# 3. Run everything (--workspaces --if-present)
npm run build       # tsc --noEmit per workspace (client: vite build)
npm run typecheck   # tsc --noEmit per workspace
npm run lint        # eslint per workspace (client also runs prettier --check)
npm run test        # vitest run per workspace (+ Playwright in client)
npm run format      # prettier --write .
```

```bash
# 4. Dev servers
npm run dev         # server (ws://localhost:3000) then client (http://localhost:5173)
```

```bash
# 5. Database (Drizzle — forwarded to the shared workspace)
npm run db:generate   # generate migrations
npm run db:migrate    # apply migrations
npm run db:push       # dev-time schema sync
npm run db:studio     # Drizzle Studio
```

## 🚀 Deployment (docker-compose)

```bash
# Copy the compose env template and adjust, then start the stack
copy docker-compose.env.example .env
docker compose up -d --build
```

The stack is `postgres → server → client`; the build context must stay the repo root so the npm
workspaces resolve (see the `Dockerfile`s). See [`docker-compose.yml`](docker-compose.yml) for the
service/env details.
