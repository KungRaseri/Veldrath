#!/bin/sh
set -e

# server/docker-entrypoint.sh — runs Drizzle migrations (root migrate.mjs, no
# drizzle-kit needed at runtime), then boots the Colyseus game server with tsx.
# Idempotent: migrate() records applied migrations in __drizzle_migrations, so
# restarting the container is a no-op.

echo "[entrypoint] Running database migrations..."
node /app/migrate.mjs
echo "[entrypoint] Migrations complete. Starting application..."

exec npx tsx src/index.ts
