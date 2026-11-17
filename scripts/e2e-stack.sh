#!/usr/bin/env bash
# Start, stop or inspect the local stack the full-flow Playwright specs need.
#
# The SvelteKit server loaders call Veldrath.Server directly (`gameApiFetch` runs on the server, not
# in the browser), so a browser-level mock cannot stand in for the API: the workflow specs need
# Postgres, the real API and the real Better Auth tables. This script is the local equivalent of the
# "Playwright — authenticated" job in `.github/workflows/ci-web.yml`.
#
# Usage:
#   scripts/e2e-stack.sh start              # postgres (docker) + auth migrations + Veldrath.Server on :9000
#   scripts/e2e-stack.sh start --fresh      # drop the database first, so a run starts from the seed
#   scripts/e2e-stack.sh stop               # stop the API and the postgres container
#   scripts/e2e-stack.sh status             # exit 0 when both are reachable
#   scripts/e2e-stack.sh api-stop           # stop only the API (the Postgres data survives)
#   scripts/e2e-stack.sh api-start          # bring only the API back
#
# `--fresh` deletes the compose volume, so anything authored in the local dev database is gone. That is
# the point: a green run should mean "works against the seed", not "works against whatever this machine
# accumulated". `api-stop`/`api-start` exist for the upstream-failure spec, which takes the API away and
# checks that the app says so instead of rendering a blank page.
#
# Then, from web/:
#   npm run test:e2e:stack
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_FILE="$ROOT/docker-compose.dev.yml"
PID_FILE="$ROOT/.e2e-server.pid"
LOG_FILE="${E2E_SERVER_LOG:-/tmp/veldrath-e2e-server.log}"

API_PORT="${E2E_API_PORT:-9000}"
DB_PORT="${E2E_DB_PORT:-5433}"
DB_NAME="${E2E_DB_NAME:-veldrath}"
DB_USER="${E2E_DB_USER:-veldrath}"
DB_PASSWORD="${E2E_DB_PASSWORD:-veldrath_dev}"
AUTH_URL="${DATABASE_URL:-postgres://veldrath_auth:veldrath_auth_dev@localhost:${DB_PORT}/${DB_NAME}}"

# Must match web/client/.env exactly, or every API call answers 401 "The signature key was not found".
# The web app mints the game tokens with that file's values, so the API has to be started with them:
# prefer the caller's environment, then the checked-out `.env`, then a last-resort default.
ENV_FILE="$ROOT/web/client/.env"
read_env() {
  [ -f "$ENV_FILE" ] || return 0
  grep -E "^$1=" "$ENV_FILE" | head -n 1 | cut -d= -f2- | sed -e 's/^"//' -e 's/"$//'
}

JWT_KEY="${JWT_KEY:-$(read_env JWT_KEY)}"
JWT_KEY="${JWT_KEY:-dev-secret-key-change-in-production-must-be-32-chars}"
JWT_ISSUER="${JWT_ISSUER:-$(read_env JWT_ISSUER)}"
JWT_ISSUER="${JWT_ISSUER:-Veldrath.Server}"
JWT_AUDIENCE="${JWT_AUDIENCE:-$(read_env JWT_AUDIENCE)}"
JWT_AUDIENCE="${JWT_AUDIENCE:-Veldrath.Client}"
BRIDGE_TOKEN="${INTERNAL_BRIDGE_TOKEN:-$(read_env INTERNAL_BRIDGE_TOKEN)}"
BRIDGE_TOKEN="${BRIDGE_TOKEN:-change-me_shared_internal_bridge_token}"

log() { printf '[e2e-stack] %s\n' "$*"; }

wait_for() {
  local label="$1" url="$2" attempts="${3:-60}"
  for _ in $(seq 1 "$attempts"); do
    if curl -fsS "$url" >/dev/null 2>&1; then
      log "$label is up"
      return 0
    fi
    sleep 2
  done
  log "$label did not come up ($url)"
  return 1
}

# Kills whatever is running a Veldrath.Server build, whoever started it. The pattern is the built
# apphost path, so it cannot match this script or the shell that invoked it.
stop_stray_servers() {
  pkill -f "bin/.*/Veldrath\.Server" 2>/dev/null || true
  for _ in $(seq 1 20); do
    ss -ltn 2>/dev/null | grep -q ":$API_PORT " || return 0
    sleep 1
  done
  log "port $API_PORT is still held after stopping the stray server"
}

# Starts only the API, against whatever database Postgres currently holds.
launch_api() {
  if [ -f "$PID_FILE" ] && kill -0 "$(cat "$PID_FILE")" 2>/dev/null; then
    log "Veldrath.Server already running (pid $(cat "$PID_FILE"))"
    return 0
  fi

  # `dotnet run` spawns the app as a child whose name is the apphost, not the .dll, so a stop that only
  # killed the wrapper can leave an OLD build holding the port — and every later run silently tests it.
  stop_stray_servers
  log "starting Veldrath.Server on :$API_PORT (log: $LOG_FILE)"
  (
    cd "$ROOT/Veldrath.Server"
    env \
      ASPNETCORE_ENVIRONMENT=Development \
      ASPNETCORE_URLS="http://127.0.0.1:$API_PORT" \
      ConnectionStrings__DefaultConnection="Host=localhost;Port=$DB_PORT;Database=$DB_NAME;Username=$DB_USER;Password=$DB_PASSWORD" \
      Jwt__Key="$JWT_KEY" \
      Jwt__Issuer="$JWT_ISSUER" \
      Jwt__Audience="$JWT_AUDIENCE" \
      Web__BaseUrl="http://localhost:4173" \
      InternalBridge__Token="$BRIDGE_TOKEN" \
      Auth__AccessCacheSeconds=0 \
      RateLimit__AdminActionsPerMinute=600 \
      RateLimit__FoundryWritesPerMinute=600 \
      nohup dotnet run --no-launch-profile >"$LOG_FILE" 2>&1 &
    echo $! >"$PID_FILE"
  )

  wait_for "Veldrath.Server" "http://127.0.0.1:$API_PORT/health" 90 || {
    tail -n 60 "$LOG_FILE" >&2
    exit 1
  }
}

start_postgres() {
  log "starting postgres on :$DB_PORT"
  docker compose -f "$COMPOSE_FILE" up -d postgres >/dev/null

  for _ in $(seq 1 30); do
    if docker compose -f "$COMPOSE_FILE" exec -T postgres \
      pg_isready -U "$DB_USER" -d "$DB_NAME" >/dev/null 2>&1; then
      return 0
    fi
    sleep 2
  done

  log "postgres did not become ready"
  return 1
}

# Deletes the database and lets the container's init script recreate the auth role, schema and grants.
fresh_database() {
  log "--fresh: stopping the API and dropping the compose volume"
  api_stop
  docker compose -f "$COMPOSE_FILE" down -v postgres >/dev/null 2>&1 || true
}

start() {
  local fresh="${1:-}"
  if [ "$fresh" = "--fresh" ]; then
    fresh_database
  fi

  start_postgres

  log "applying Better Auth migrations"
  (cd "$ROOT/web" && DATABASE_URL="$AUTH_URL" npm run db:migrate >/dev/null)

  # The server migrates and seeds its own databases at startup, so a fresh volume needs nothing else.
  launch_api
}

stop() {
  api_stop
  docker compose -f "$COMPOSE_FILE" stop postgres >/dev/null 2>&1 || true
  log "stopped"
}

api_stop() {
  if [ -f "$PID_FILE" ]; then
    kill "$(cat "$PID_FILE")" 2>/dev/null || true
    rm -f "$PID_FILE"
  fi
  stop_stray_servers
  log "api stopped"
}

api_start() {
  launch_api
}

status() {
  local ok=1
  curl -fsS "http://127.0.0.1:$API_PORT/health" >/dev/null 2>&1 && log "api up" || { log "api down"; ok=0; }
  docker compose -f "$COMPOSE_FILE" ps --status running postgres 2>/dev/null | grep -q postgres &&
    log "postgres up" || { log "postgres down"; ok=0; }
  return $((1 - ok))
}

case "${1:-start}" in
  start) start "${2:-}" ;;
  stop) stop ;;
  status) status ;;
  api-stop) api_stop ;;
  api-start) api_start ;;
  *) echo "usage: $0 {start [--fresh]|stop|status|api-stop|api-start}" >&2; exit 2 ;;
esac
