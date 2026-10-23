# client/Dockerfile — SvelteKit client (@sveltejs/adapter-node)
#
# This file lives in client/, but the build context MUST be the repo root so
# the npm workspaces resolve. docker-compose.yml (and Coolify) build with:
#   context: .          # repo root — required for the npm workspace layout
#   dockerfile: client/Dockerfile
#
# Notes / assumptions:
#   - The client imports @your-scope/shared as TypeScript source (workspace
#     `main`/`exports` -> `src/index.ts`), so shared/ must be present at build
#     and runtime (vite + the adapter-node output resolve it via node_modules).
#   - adapter-node emits client/build/; the runtime runs `node build` with the
#     prod deps present. We copy the full root node_modules (which contains the
#     client's prod `dependencies` that SvelteKit externalises) — correct, if
#     slightly larger than a `--omit=dev` prune.
#   - ORIGIN / WS_URL / secrets are supplied by docker-compose at runtime.
#   - Base image node:22-alpine (npm 10) avoids the npm >= 12 EALLOWGIT issue
#     with the lockfile's git dependency (uWebSockets.js).

# ---- Stage 1: install the whole workspace + build the client ----
FROM node:22-alpine AS build

WORKDIR /app

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

# Prod deps: copy the full node_modules (contains the client's `dependencies`
# that adapter-node externalises) + the shared sources behind the workspace
# symlinks.
COPY --from=build /app/node_modules ./node_modules
COPY --from=build /app/shared ./shared

# The built SvelteKit app (adapter-node output).
COPY --from=build /app/client/package.json ./client/package.json
COPY --from=build /app/client/build ./client/build

WORKDIR /app/client

EXPOSE 3000

CMD ["node", "build"]
