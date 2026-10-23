# Monorepo Template — Guide

> This document describes the generic template in this directory: what it contains, what to
> customize before you build on it, and how to lift it into a fresh repository.

## 1. Overview

This is a **TypeScript npm-workspaces monorepo template** (root `your-project`, workspaces
`shared`, `engine`, `server`, `client`) that wires up the full tooling so you can start a fresh
project without re-doing boilerplate:

| Layer | Technology |
| --- | --- |
| Language & module system | TypeScript, **strict**, **ESM everywhere** (`"type": "module"`), `engines.node >= 20` |
| Workspace orchestration | **npm workspaces** (`shared`, `engine`, `server`, `client`) + `concurrently`/`wait-on` |
| Client | **SvelteKit** (Svelte 5 runes, Vite, Tailwind 4, `@sveltejs/adapter-node`) |
| Client UI | **Skeleton v5** design system (custom `app` theme), **Bits UI** headless primitives, **Lucide** + **Simple Icons** |
| Server | **Colyseus** (`@colyseus/core` + `@colyseus/ws-transport` + `@colyseus/schema`) hosting authoritative sessions |
| Database | **Drizzle ORM** + **drizzle-kit**; single driver-agnostic schema in `shared`; Postgres |
| Auth | **Better Auth** (client) + stateless HMAC ws tickets |
| Testing | **Vitest** (engine/shared/server + client unit), **Playwright** (client e2e) |
| Lint/format | **ESLint** (flat config, `typescript-eslint`) + **Prettier** |
| Deployment | **Docker** (multi-stage `node:22-alpine`) + `docker-compose.yml` |
| CI | **GitHub Actions** (`node.yml`) with a coverage + artifact summary |
| Agent tooling | `.roo/skills/` agent skills + `llms/` third-party reference corpus |

## 2. What's inside

### Workspaces

- [`shared/`](shared) — `@your-scope/shared`: shared contracts entry + the **single Drizzle
  schema**. Subpath exports: `@your-scope/shared/db` (schema) and `@your-scope/shared/schema`
  (Colyseus room schemas).
- [`engine/`](engine) — `@your-scope/engine`: pure, deterministic logic. No I/O, no network.
- [`server/`](server) — `@your-scope/server`: Colyseus server + pg/Drizzle persistence.
- [`client/`](client) — SvelteKit app + Skeleton theme + Better Auth/DB scaffolding.

### Root tooling

[`tsconfig.base.json`](tsconfig.base.json), [`eslint.config.js`](eslint.config.js),
[`.prettierrc.json`](.prettierrc.json), [`.editorconfig`](.editorconfig),
[`.gitattributes`](.gitattributes), [`.gitignore`](.gitignore),
[`.prettierignore`](.prettierignore), [`.dockerignore`](.dockerignore),
[`migrate.mjs`](migrate.mjs), [`docker-compose.yml`](docker-compose.yml),
[`docker-compose.env.example`](docker-compose.env.example), [`AGENTS.md`](AGENTS.md) (agent
reference), [`README.md`](README.md).

### CI, Docker, scripts

[`.github/workflows/node.yml`](.github/workflows/node.yml) (build/typecheck/lint/migrate/test/e2e/
coverage + artifacts + step summary), [`scripts/ci-summary.mjs`](scripts/ci-summary.mjs),
[`scripts/curate-icons.mjs`](scripts/curate-icons.mjs), [`server/Dockerfile`](server/Dockerfile),
[`client/Dockerfile`](client/Dockerfile), [`server/docker-entrypoint.sh`](server/docker-entrypoint.sh).

### Reference & skills

[`llms/`](llms) (official docs corpus: sveltekit, vite, vitest, drizzle, betterauth, colyseus,
tailwindcss, zod, skeletondev, bitsui, lucide) and [`.roo/skills/`](.roo/skills) (`repo-context`,
`database-workflow`, `auth-setup`, `sveltekit-development`, `ui-development`, `verification`,
`new-feature`, `_template`).

## 3. Customize before you build (placeholder checklist)

Search the template for `your-project`, `your-scope`, and `app` (theme) and replace:

| Find | Replace with |
| --- | --- |
| `your-project` (root package name, docker DB/volume names) | your project name |
| `@your-scope/*` (workspace package names, cross-workspace deps, imports, `no-restricted-imports` rule) | your npm scope |
| `app` theme (`data-theme="app"` in [`app.html`](client/src/app.html), `[data-theme='app']` in [`theme.css`](client/src/routes/theme.css), `@import './theme'` in [`layout.css`](client/src/routes/layout.css)) | your theme name |
| DB name `yourproject` in `DATABASE_URL` (env examples + compose) and CI (`POSTGRES_DB`, `pg_isready`) | your db name |
| `.env.example` secrets/comments | your values |
| GitHub badge URLs in [`README.md`](README.md) | your repo |
| [`docs/`](docs) | your docs (currently empty) |

## 4. Adopt into a fresh repo (step-by-step)

1. Copy the contents of this directory into the new repo (or fork it).
2. Apply the placeholder replacements above (`your-project`, `your-scope`, theme, db name).
3. Create `.env` files from the templates:

   ```bash
   copy client\.env.example client\.env   # Windows (cp client/.env.example client/.env elsewhere)
   copy server\.env.example server\.env
   ```

   `DATABASE_URL` and `WS_AUTH_SECRET` must match across both files.
4. Install once at the root: `npm install`
   (if it fails with `EALLOWGIT`, run `npm install --allow-git=all` — an unused git dependency
   such as `uWebSockets.js` may appear in the lockfile).
5. Generate + apply the initial migrations: `npm run db:generate && npm run db:migrate`
   (`shared/db/migrations` ships empty on purpose — the schema is regenerated).
6. Fill in the [`engine/src/index.ts`](engine/src/index.ts) and [`server/src/index.ts`](server/src/index.ts)
   stubs; add your domain logic, server rooms, and client routes.
7. Verify: `npm run typecheck && npm run lint && npm run test && npm run build`.
8. Commit. Keep [`AGENTS.md`](AGENTS.md) updated as the architecture evolves.

## 5. Gotchas & conventions

- **Shared env values must match** across [`client/.env`](client/.env.example) and
  [`server/.env`](server/.env.example) (`DATABASE_URL`, `WS_AUTH_SECRET`).
- **Install once at the root** — npm workspaces install everything.
- **Keep DB/auth construction lazy** (`getAuthDb()`/`getAuth()`) so `vite build` stays DB-free.
- **Colyseus isolation**: keep Colyseus out of `engine/` and out of the shared main entry (only the
  `@your-scope/shared/schema` subpath). Import `@colyseus/core` / `@colyseus/ws-transport` /
  `@colyseus/schema` directly — never the `colyseus` umbrella, never `@colyseus/auth` (Better Auth +
  stateless HMAC tickets instead).
- **`tsconfig.base.json` must exist at the repo root and be copied into the server image** —
  tsx/esbuild resolves `experimentalDecorators` from it at runtime (Colyseus `@type()` decorators).
- **Server Vitest needs the Colyseus inlining + `@pm2/io` stub** (already configured in
  [`server/vitest.config.ts`](server/vitest.config.ts) + [`server/tests/stubs/pm2-io.ts`](server/tests/stubs/pm2-io.ts)) —
  don't remove them.
- **Drizzle config loads `../server/.env` then `../client/.env`** so root `npm run db:*` works
  without exporting `DATABASE_URL` — preserve those path assumptions.
- **Prettier**: root `.prettierrc.json` (spaces/120/double) vs `client/prettier.config.js`
  (tabs/100/single) differ — pick one convention.
- **Docker build context = repo root** (`context: .`, `dockerfile: server/Dockerfile` /
  `client/Dockerfile`) so the npm workspaces resolve. Keep base images at `node:22-alpine`
  (npm 10) or add `--allow-git=all`.
- **DB authority split** (as scaffolded): the client process owns Better Auth/auth tables; the
  server owns app tables. Keep it consistent as you add tables.
- **Do not copy `package-lock.json` between projects** — regenerate per repo.
- **Skills embed paths** — if you rename workspaces or the `llms/` corpus, update the affected
  skills (`repo-context`, `database-workflow`, `auth-setup`, `sveltekit-development`,
  `ui-development`, `verification`, `new-feature`).

## 6. Intentionally excluded

This template carries **no application-specific code**: no domain logic, no server rooms, no client
routes/components, no product documentation, and no migrations. It ships the plumbing (config,
tooling, CI, Docker, env scaffolding, agent skills) so you can fill in your product.
