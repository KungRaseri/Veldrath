#!/usr/bin/env bash
# Start, stop or inspect the isolated stack the full-flow Playwright specs need.
#
# The SvelteKit server loaders call Veldrath.Server directly (`gameApiFetch` runs on the server, not in
# the browser), so a browser-level mock cannot stand in for the API: the workflow specs need Postgres,
# the real API and the real Better Auth tables. This script is the local equivalent of the
# "Playwright — authenticated" job in `.github/workflows/ci-web.yml`.
#
# Isolation, because a test run must never be why a developer loses their world:
#
#   compose project   veldrath-e2e   (own network, own volume, own container name)
#   postgres          55432          (PostgreSQL default is 5432, the dev compose is 5433)
#   API               9100           (dev `dotnet run` uses 5000, the dev container 9000)
#   web preview       4273           (Vite preview default is 4173)
#
# The script refuses to start when one of its ports is already taken, and it never kills a server it
# did not start: an earlier version pkill'd any Veldrath.Server build, which meant starting the e2e
# stack silently killed a developer's own `dotnet run`. Now the occupying process is reported instead.
#
# Usage:
#   scripts/e2e-stack.sh start              # postgres + auth migrations + Veldrath.Server on :9100
#   scripts/e2e-stack.sh start --fresh      # drop the e2e database first, so a run starts from the seed
#   scripts/e2e-stack.sh stop               # stop the API and the e2e postgres container
#   scripts/e2e-stack.sh status             # exit 0 when both are reachable
#   scripts/e2e-stack.sh api-stop           # stop only the API (the Postgres data survives)
#   scripts/e2e-stack.sh api-start          # bring only the API back
#
# `--fresh` deletes the e2e compose volume, so a green run means "works against the seed", not "works
# against whatever this machine accumulated". `api-stop`/`api-start` exist for the upstream-failure
# spec, which takes the API away and checks that the app says so instead of rendering a blank page.
#
# The specs themselves expect the matching environment, which `npm run test:e2e:stack` sets:
#   E2E_STACK=1 E2E_API_PORT=9100 E2E_DB_PORT=55432 E2E_WEB_PORT=4273
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_PROJECT="veldrath-e2e"
# The e2e file is standalone, not an override: compose merges `ports`, so an override would publish the
# dev port too and the two databases would collide.
COMPOSE=(docker compose -p "$COMPOSE_PROJECT" -f "$ROOT/docker-compose.e2e.yml")
PID_FILE="$ROOT/.e2e-server.pid"
LOG_FILE="${E2E_SERVER_LOG:-/tmp/veldrath-e2e-server.log}"

API_PORT="${E2E_API_PORT:-9100}"
DB_PORT="${E2E_DB_PORT:-55432}"
WEB_PORT="${E2E_WEB_PORT:-4273}"
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

# Refuses to take a port somebody else is already using, and names the owner so the fix is obvious.
require_free_port() {
  local port="$1" purpose="$2"
  if ! ss -ltn 2>/dev/null | grep -q ":$port "; then
    return 0
  fi

  local owner
  owner="$(ss -ltnp 2>/dev/null | grep ":$port " | sed -n 's/.*users:((\"\([^\"]*\)\",pid=\([0-9]*\).*/\1 (pid \2)/p' | head -n 1)"
  log "cannot start $purpose: port $port is already in use${owner:+ by $owner}"
  log "the e2e stack is isolated on :$DB_PORT (postgres), :$API_PORT (api) and :$WEB_PORT (preview);"
  log "stop whatever holds $port — it is not part of this stack, and this script will not kill it."
  exit 1
}

# Only ever stops the server this script started.
stop_own_api() {
  if [ -f "$PID_FILE" ]; then
    local pid
    pid="$(cat "$PID_FILE")"
    kill "$pid" 2>/dev/null || true
    # `dotnet run` spawns the app as a child, so the wrapper exiting is not the port being free.
    for _ in $(seq 1 20); do
      kill -0 "$pid" 2>/dev/null || break
      sleep 1
    done
    rm -f "$PID_FILE"
  fi
}

# Starts only the API, against whatever database Postgres currently holds.
launch_api() {
  if [ -f "$PID_FILE" ] && kill -0 "$(cat "$PID_FILE")" 2>/dev/null; then
    log "Veldrath.Server already running (pid $(cat "$PID_FILE"))"
    return 0
  fi

  require_free_port "$API_PORT" "the API"
  log "starting Veldrath.Server on :$API_PORT (log: $LOG_FILE)"
  # Email__SmtpHost is deliberately empty: every registration asks the bridge to send a verification
  # mail, the development settings point SMTP at a dev mail container this stack does not run, and the
  # connect hangs until the web app's 10s timeout expires — paid on every single sign-up. No host
  # selects NullEmailSender, which answers immediately.
  (
    cd "$ROOT/Veldrath.Server"
    env \
      ASPNETCORE_ENVIRONMENT=Development \
      ASPNETCORE_URLS="http://127.0.0.1:$API_PORT" \
      ConnectionStrings__DefaultConnection="Host=localhost;Port=$DB_PORT;Database=$DB_NAME;Username=$DB_USER;Password=$DB_PASSWORD" \
      Jwt__Key="$JWT_KEY" \
      Jwt__Issuer="$JWT_ISSUER" \
      Jwt__Audience="$JWT_AUDIENCE" \
      Web__BaseUrl="http://localhost:$WEB_PORT" \
      Cors__AllowedOrigins__0="http://localhost:$WEB_PORT" \
      InternalBridge__Token="$BRIDGE_TOKEN" \
      Email__SmtpHost="" \
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
  log "starting the e2e postgres on :$DB_PORT (project $COMPOSE_PROJECT)"
  "${COMPOSE[@]}" up -d postgres >/dev/null

  for _ in $(seq 1 30); do
    if "${COMPOSE[@]}" exec -T postgres pg_isready -U "$DB_USER" -d "$DB_NAME" >/dev/null 2>&1; then
      return 0
    fi
    sleep 2
  done

  log "postgres did not become ready"
  return 1
}

# Deletes the e2e database and lets the container's init script recreate the auth role, schema and grants.
fresh_database() {
  log "--fresh: stopping the API and dropping the e2e volume"
  api_stop
  "${COMPOSE[@]}" down -v postgres >/dev/null 2>&1 || true
}

start() {
  local fresh="${1:-}"
  require_free_port "$WEB_PORT" "the preview server"
  if [ "$fresh" = "--fresh" ]; then
    fresh_database
  fi

  start_postgres

  log "applying Better Auth migrations against :$DB_PORT"
  (cd "$ROOT/web" && DATABASE_URL="$AUTH_URL" npm run db:migrate >/dev/null)

  # The server migrates and seeds its own databases at startup, so a fresh volume needs nothing else.
  launch_api

  log "ready: api http://127.0.0.1:$API_PORT, preview http://localhost:$WEB_PORT, database :$DB_PORT"
  log "run the specs with: cd web && npm run test:e2e:stack"
}

stop() {
  api_stop
  "${COMPOSE[@]}" stop postgres >/dev/null 2>&1 || true
  log "stopped"
}

api_stop() {
  stop_own_api
  log "api stopped"
}

api_start() {
  launch_api
}

status() {
  local ok=1
  curl -fsS "http://127.0.0.1:$API_PORT/health" >/dev/null 2>&1 && log "api up (:$API_PORT)" || { log "api down"; ok=0; }
  "${COMPOSE[@]}" ps --status running postgres 2>/dev/null | grep -q postgres &&
    log "postgres up (:$DB_PORT)" || { log "postgres down"; ok=0; }
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
