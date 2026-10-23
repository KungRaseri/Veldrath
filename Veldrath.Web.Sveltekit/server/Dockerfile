# server/Dockerfile — @your-scope/server (Colyseus WebSocket server)
#
# This file lives in server/, but the build context MUST be the repo root so
# the npm workspaces resolve. docker-compose.yml (and Coolify) build with:
#   context: .          # repo root — required for the npm workspace layout
#   dockerfile: server/Dockerfile
#
# Notes / assumptions:
#   - The server imports @your-scope/shared and @your-scope/engine as TypeScript
#     source (workspace `main`/`exports` -> `src/index.ts`) and runs them through
#     tsx, so the runtime image keeps the shared/ + engine/ sources and the full
#     node_modules (tsx lives in devDependencies, hence `npm ci` with dev deps).
#   - Base image node:22-alpine ships npm 10, which allows the lockfile's git
#     dependency (uWebSockets.js, an unused peer) — avoiding the npm >= 12
#     EALLOWGIT failure. Do not bump to node:24+ without --allow-git=all.
#   - The server reads server/.env via dotenv (quiet). Compose provides env vars,
#     and server/.env.example is copied in as a harmless fallback.

# ---- Stage 1: install the whole workspace + validate (typecheck) ----
FROM node:22-alpine AS build

WORKDIR /app

# Install first so layer caching works when only sources change.
COPY package.json package-lock.json ./
COPY tsconfig.base.json ./
COPY shared/package.json shared/package.json
COPY engine/package.json engine/package.json
COPY server/package.json server/package.json
COPY client/package.json client/package.json

RUN npm ci

# Copy the workspace sources the server needs (build + runtime).
COPY shared ./shared
COPY engine ./engine
COPY server ./server

# Runtime migration script + migrations (root migrate.mjs runs them before
# boot; it prefers shared/db/migrations and also finds ./migrations).
COPY migrate.mjs ./migrate.mjs
COPY shared/db/migrations ./migrations

# Validate the server workspace typechecks (build = tsc --noEmit). The engine is
# pure and never runs standalone in deploy.
RUN npm run build --workspace server

# ---- Stage 2: runtime ----
FROM node:22-alpine AS runtime

WORKDIR /app

ENV NODE_ENV=production
ENV PORT=3000
ENV HOST=0.0.0.0

# Full node_modules (includes dev deps: tsx + the workspace TS sources via
# symlinks into /app/shared + /app/engine).
COPY --from=build /app/node_modules ./node_modules
COPY --from=build /app/shared ./shared
COPY --from=build /app/engine ./engine
COPY --from=build /app/server ./server

# Root tsconfig.base.json — server/tsconfig.json (and shared/engine) extend it.
# tsx/esbuild resolves it at runtime; without it experimentalDecorators falls
# back to false and the Colyseus @type() decorators break with a
# "Cannot read properties of undefined (reading 'constructor')" crash.
COPY --from=build /app/tsconfig.base.json ./tsconfig.base.json

# Runtime migration script + migrations (root migrate.mjs — postgres.js driver,
# no drizzle-kit needed at runtime). Entrypoint runs them before boot.
COPY --from=build /app/migrate.mjs ./migrate.mjs
COPY --from=build /app/migrations ./migrations

# Entrypoint: apply Drizzle migrations (idempotent), then boot the server.
COPY --from=build /app/server/docker-entrypoint.sh /app/docker-entrypoint.sh
RUN chmod +x /app/docker-entrypoint.sh

# dotenv fallback (quiet; existing env vars always win).
COPY --from=build /app/server/.env.example /app/server/.env.example

WORKDIR /app/server

EXPOSE 3000

ENTRYPOINT ["/app/docker-entrypoint.sh"]
