# client/Dockerfile — SvelteKit client (@sveltejs/adapter-node)
#
# Build context MUST be web/ so the npm workspaces resolve.
# Coolify (one Dockerfile-based resource per service) uses:
#   Base directory: /web
#   Dockerfile:     /client/Dockerfile
# docker-compose.yml/dev/prod build with context ./web and the same file.
#
# Notes / assumptions:
#   - The client imports @veldrath/shared as TypeScript source (workspace
#     `main`/`exports` -> `src/index.ts`), so shared/ must be present at build
#     and runtime (vite + the adapter-node output resolve it via node_modules).
#   - adapter-node emits client/build/; the runtime runs `node build` with the
#     prod deps present. We copy the full root node_modules (which contains the
#     client's prod `dependencies` that SvelteKit externalises) — correct, if
#     slightly larger than a `--omit=dev` prune.
#   - The entrypoint waits for Postgres, applies Drizzle auth migrations, then
#     starts SvelteKit on PORT (default 8081).
#   - PUBLIC_API_URL / PUBLIC_HUB_URL are PUBLIC env vars declared in
#     client/src/env.ts. SvelteKit 3 inlines them into the browser bundle at
#     build time AND validates them from process.env when `node build` starts,
#     so they must be supplied BOTH as build args and as runtime env vars.
#     The runtime defaults below match the build-arg defaults; an orchestrator
#     (Coolify/compose) can override them at runtime but must keep them equal to
#     the build values, otherwise SSR and the browser bundle disagree.
#   - Base image node:22-alpine meets SvelteKit 3's Node >= 22.17 requirement.

# ---- Stage 1: install the whole workspace + build the client ----
FROM node:22-alpine AS build

WORKDIR /app

# Public values are baked into the client/server bundle by SvelteKit.
ARG PUBLIC_API_URL=http://localhost:9000
ARG PUBLIC_HUB_URL=http://localhost:9000/hubs/game
ENV PUBLIC_API_URL=$PUBLIC_API_URL
ENV PUBLIC_HUB_URL=$PUBLIC_HUB_URL

# Install first so layer caching works when only sources change.
COPY package.json package-lock.json ./
COPY tsconfig.base.json ./
COPY shared/package.json shared/package.json
COPY client/package.json client/package.json

RUN npm ci

# Copy the workspace sources the client build needs (shared is consumed as TS
# source by the SvelteKit server).
COPY shared ./shared
COPY client ./client

# Ensure .svelte-kit types exist (npm ci runs the client `prepare` script, which
# already swallows errors — this makes the sync explicit), then vite build.
RUN npm run prepare --workspace client
RUN npm run build --workspace client

# ---- Stage 2: runtime ----
FROM node:22-alpine AS runtime

WORKDIR /app

ENV NODE_ENV=production
# adapter-node listens on PORT; 8081 is the port the retired Blazor web deployment used.
ENV PORT=8081
ENV HOST=0.0.0.0

# Public values are validated by SvelteKit at startup (`node build`), so the
# image carries the same defaults it was built with. Override them at runtime
# only if the values match the ones used for the build args above.
ARG PUBLIC_API_URL=http://localhost:9000
ARG PUBLIC_HUB_URL=http://localhost:9000/hubs/game
ENV PUBLIC_API_URL=$PUBLIC_API_URL
ENV PUBLIC_HUB_URL=$PUBLIC_HUB_URL

# Prod deps: copy the full node_modules (contains the client's `dependencies`
# that adapter-node externalises) + the shared sources behind the workspace
# symlinks.
COPY --from=build /app/node_modules ./node_modules
COPY --from=build /app/shared ./shared

# Auth migrations + startup entrypoint. migrate.mjs resolves the migrations
# from /app/shared/db/migrations; the entrypoint waits for Postgres, applies
# them, then starts adapter-node.
COPY migrate.mjs ./migrate.mjs
COPY client/docker-entrypoint.sh ./docker-entrypoint.sh
RUN chmod +x ./docker-entrypoint.sh

# The built SvelteKit app (adapter-node output).
COPY --from=build /app/client/package.json ./client/package.json
COPY --from=build /app/client/build ./client/build

WORKDIR /app/client

EXPOSE 8081

ENTRYPOINT ["/app/docker-entrypoint.sh"]
