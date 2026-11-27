#!/usr/bin/env bash
# Prove the BackfillSeededMapDoorsAndArrivalHops upgrade on disposable PostgreSQL 17.
#
# Why this script exists
# ----------------------
# The migration is data-only, and the test factories use EnsureCreated(), so no unit test can prove it: the only
# honest proof is an old server seeding an old database, the new server starting on that same database, and the
# resulting rows being read back. This script does exactly that, twice — once from the commit just before #270
# (38fe4b0f^1, the state that has neither the twelve doors nor the two Arrival hops) and once from the actual
# predecessor on main (origin/main) — and then adds the awkward cases:
#
#   fresh        an empty database; the migration must return and the seeder must create the doors
#   equivalent   an existing door node carrying custom text/layout, and the Arrival hop already present with
#                another uuid and a label; the patch must keep both and add only what is missing
#   legacy       a seed older than the derived walk and the region-marker link: no marketplace walk to re-home and
#                no marker targets at all. This is the shape production was in; the patch must converge, not abort
#   conflict     a stale edge occupying a door's natural slot, and a same-id node with different topology; the
#                patch must abort with no partial rows and no migration-history row
#
# Isolation, because this must never be why somebody loses a world:
#   * its own container (unique name, no volume, removed when stopped) on a localhost-only ephemeral port;
#   * its own databases, cloned from the old server's seed;
#   * only API processes this script started, stopped by pid and by the port they took;
#   * old revisions run from throwaway git worktrees, removed on exit.
#
# Usage:
#   bash scripts/verify-map-graph-upgrade.sh            # run everything, clean up
#   bash scripts/verify-map-graph-upgrade.sh --keep     # leave the temp dir, databases and worktrees for poking
#
# Requirements: docker (postgres:17-alpine), dotnet SDK 10 (local tool manifest), curl, python3, git.
# Do not run this against a database you care about: every database it touches is created by it.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export PATH="${HOME}/.dotnet:${PATH}"

KEEP=0
if [ "${1:-}" = "--keep" ]; then KEEP=1; fi

DOTNET="${DOTNET:-dotnet}"
DOCKER="${DOCKER:-docker}"
PG_IMAGE="${PG_IMAGE:-postgres:17-alpine}"
DB_USER="veldrath"
DB_PASSWORD="veldrath_dev"
MIGRATION_NAME="BackfillSeededMapDoorsAndArrivalHops"
OLD_DOORS_SHA="${OLD_DOORS_SHA:-38fe4b0f^1}"

pass() { printf '  ok   %s\n' "$*"; }
fail() { printf '  FAIL %s\n' "$*" >&2; exit 1; }
log() { printf '[verify-map-graph] %s\n' "$*"; }

SUFFIX="$$-$(python3 -c 'import secrets; print(secrets.token_hex(3))')"
WORK="$(mktemp -d "${TMPDIR:-/tmp}/mapgraph-upgrade.XXXXXX")"
CONTAINER="mapgraph-upgrade-${SUFFIX}"
PGPORT="$(python3 -c 'import socket; s=socket.socket(); s.bind(("127.0.0.1",0)); print(s.getsockname()[1]); s.close()')"

declare -a API_PIDS=()
declare -a API_PORTS=()
declare -a WORKTREES=()
declare -a RESULTS=()
API_LAST_PID=""
API_LAST_PORT=""

cleanup() {
  local rc=$?
  local i
  for i in "${!API_PIDS[@]}"; do
    stop_api "${API_PIDS[$i]}" "${API_PORTS[$i]}" >/dev/null 2>&1 || true
  done
  local wt
  for wt in "${WORKTREES[@]:-}"; do
    [ -n "$wt" ] || continue
    git -C "$ROOT" worktree remove --force "$wt" >/dev/null 2>&1 || true
  done
  if [ "$KEEP" -eq 1 ]; then
    log "keeping work dir $WORK, container $CONTAINER (postgres 127.0.0.1:$PGPORT) and its databases"
    log "stop it with: $DOCKER stop $CONTAINER"
  else
    # `docker stop` is the obvious cleanup, but this also works where the caller may not use it: the postgres
    # postmaster is pid 1 inside the container, so a TERM to it shuts the container down and --rm removes it.
    "$DOCKER" exec "$CONTAINER" sh -c 'kill -TERM 1' >/dev/null 2>&1 \
      || "$DOCKER" stop "$CONTAINER" >/dev/null 2>&1 || true
    for i in $(seq 1 20); do
      "$DOCKER" ps --format '{{.Names}}' | grep -qx "$CONTAINER" || break
      sleep 1
    done
    rm -rf "$WORK"
  fi
  exit "$rc"
}
trap cleanup EXIT INT TERM

# --- infrastructure ---------------------------------------------------------------------------------------------

free_port() {
  python3 -c 'import socket; s=socket.socket(); s.bind(("127.0.0.1",0)); print(s.getsockname()[1]); s.close()'
}

start_postgres() {
  log "postgres 17 on 127.0.0.1:$PGPORT (container $CONTAINER, no volume)"
  "$DOCKER" run -d --rm --name "$CONTAINER" -p "127.0.0.1:${PGPORT}:5432" \
    -e "POSTGRES_DB=veldrath" -e "POSTGRES_USER=$DB_USER" -e "POSTGRES_PASSWORD=$DB_PASSWORD" \
    "$PG_IMAGE" >/dev/null
  local i
  for i in $(seq 1 60); do
    if "$DOCKER" exec "$CONTAINER" pg_isready -U "$DB_USER" -d veldrath >/dev/null 2>&1; then
      return 0
    fi
    sleep 1
  done
  fail "postgres did not become ready"
}

db_sql() {
  local db=$1
  "$DOCKER" exec -i -e PGTZ=UTC "$CONTAINER" psql -U "$DB_USER" -d "$db" -v ON_ERROR_STOP=1 -tA
}

db_query() {
  local db=$1 query=$2
  "$DOCKER" exec -i -e PGTZ=UTC "$CONTAINER" psql -U "$DB_USER" -d "$db" -v ON_ERROR_STOP=1 -tA -c "$query"
}

create_db() {
  db_query postgres "CREATE DATABASE \"$1\";" >/dev/null
}

clone_db() {
  local target=$1 template=$2 try
  for try in $(seq 1 30); do
    if db_query postgres "CREATE DATABASE \"$target\" TEMPLATE \"$template\";" >/dev/null 2>&1; then
      return 0
    fi
    sleep 1
  done
  fail "could not clone $template into $target (a connection to the template may still be open)"
}

add_worktree() {
  local sha=$1 name=$2 path="$WORK/$2"
  git -C "$ROOT" worktree add --detach "$path" "$sha" >/dev/null 2>&1 || fail "could not create a worktree at $sha"
  WORKTREES+=("$path")
  WT_LAST="$path"
}

build_server() {
  local dir=$1 label=$2
  if ! ( cd "$dir" && "$DOTNET" build Veldrath.Server/Veldrath.Server.csproj --nologo -v q > "$WORK/build-$label.log" 2>&1 ); then
    tail -n 30 "$WORK/build-$label.log" >&2
    fail "build failed for $label"
  fi
  pass "built $label"
}

start_api() {
  local dir=$1 db=$2 port=$3 logfile=$4
  (
    cd "$dir/Veldrath.Server" || exit 1
    exec env \
      ASPNETCORE_ENVIRONMENT=Development \
      ASPNETCORE_URLS="http://127.0.0.1:$port" \
      ConnectionStrings__DefaultConnection="Host=127.0.0.1;Port=$PGPORT;Database=$db;Username=$DB_USER;Password=$DB_PASSWORD" \
      Jwt__Key="dev-secret-key-change-in-production-must-be-32-chars" \
      Jwt__Issuer="Veldrath.Server" \
      Jwt__Audience="Veldrath.Client" \
      InternalBridge__Token="change-me_shared_internal_bridge_token" \
      Email__SmtpHost="" \
      Auth__AccessCacheSeconds=0 \
      RateLimit__AdminActionsPerMinute=600 \
      RateLimit__FoundryWritesPerMinute=600 \
      Chests__SweepIntervalSeconds=5 \
      Chests__ReopenAfterMinutes=1 \
      "$DOTNET" run --project . --no-build --no-launch-profile
  ) >"$logfile" 2>&1 &
  local pid=$!
  API_PIDS+=("$pid")
  API_PORTS+=("$port")
}

stop_api() {
  local pid=$1 port=$2 child i
  kill "$pid" 2>/dev/null || true
  for i in $(seq 1 20); do
    kill -0 "$pid" 2>/dev/null || break
    sleep 1
  done
  child="$(ss -ltnp 2>/dev/null | sed -n "s/.*:${port} .*pid=\([0-9]*\).*/\1/p" | head -n 1)"
  if [ -n "${child:-}" ]; then
    kill "$child" 2>/dev/null || true
    for i in $(seq 1 20); do
      kill -0 "$child" 2>/dev/null || break
      sleep 1
    done
  fi
}

wait_listening() {
  local pid=$1 port=$2 timeout=${3:-180} i
  for i in $(seq 1 "$timeout"); do
    if curl -s -o /dev/null "http://127.0.0.1:${port}/health" 2>/dev/null; then
      return 0
    fi
    if ! kill -0 "$pid" 2>/dev/null; then
      return 1
    fi
    sleep 1
  done
  return 1
}

wait_exit_without_listening() {
  local pid=$1 port=$2 timeout=${3:-180} i
  for i in $(seq 1 "$timeout"); do
    if curl -s -o /dev/null "http://127.0.0.1:${port}/health" 2>/dev/null; then
      return 1
    fi
    if ! kill -0 "$pid" 2>/dev/null; then
      return 0
    fi
    sleep 1
  done
  return 2
}

wait_seeded() {
  local db=$1 pid=$2 timeout=${3:-240} i count
  for i in $(seq 1 "$timeout"); do
    count="$(db_query "$db" 'SELECT count(*) FROM "Maps";' 2>/dev/null || echo 0)"
    if [ "${count:-0}" -gt 0 ]; then
      return 0
    fi
    if ! kill -0 "$pid" 2>/dev/null; then
      return 1
    fi
    sleep 1
  done
  return 1
}

# Starts the API and waits for it to listen. Sets API_LAST_PID / API_LAST_PORT (a subshell cannot update the
# cleanup arrays, so the caller must not capture this function's output).
start_and_wait() {
  local dir=$1 db=$2 logfile=$3 label=$4 port pid
  port="$(free_port)"
  start_api "$dir" "$db" "$port" "$logfile"
  pid="${API_PIDS[${#API_PIDS[@]}-1]}"
  if ! wait_listening "$pid" "$port" 240; then
    tail -n 40 "$logfile" >&2 || true
    fail "$label did not become ready"
  fi
  API_LAST_PID="$pid"
  API_LAST_PORT="$port"
}

# Runs the exact Up body inside a transaction that is rolled back: the preflight report the migration raises, on a
# copy of the database, before the server is allowed to apply it for real.
preflight_db() {
  local db=$1 label=$2 out
  out="$WORK/preflight-$db.log"
  if { echo 'BEGIN;'; cat "$PATCH_SQL"; echo 'ROLLBACK;'; } | db_sql "$db" >"$out" 2>&1; then
    grep -E 'NOTICE:.*BackfillSeededMapDoorsAndArrivalHops' "$out" | sed 's/.*NOTICE:  /  notice: /' | sort -u
    pass "$label: preflight on the restored copy reported its plan (rolled back)"
  else
    cat "$out" >&2
    fail "$label: preflight aborted; see the error above"
  fi
}

assert_eq() {
  local label=$1 actual=$2 expected=$3
  if [ "$actual" = "$expected" ]; then
    pass "$label"
  else
    fail "$label: expected [$expected], got [$actual]"
  fi
}

assert_contains() {
  local label=$1 file=$2 needle=$3
  if grep -qF "$needle" "$file"; then
    pass "$label"
  else
    tail -n 30 "$file" >&2 || true
    fail "$label: expected to find [$needle]"
  fi
}

graph_counts() {
  local db=$1
  db_query "$db" 'SELECT format($q$maps=%s nodes=%s edges=%s$q$, (SELECT count(*) FROM "Maps"), (SELECT count(*) FROM "MapNodes"), (SELECT count(*) FROM "MapEdges"));'
}

graph_checksum() {
  local db=$1
  db_sql "$db" <<'SQL'
SELECT md5(string_agg(line, E'\n' ORDER BY line)) FROM (
  SELECT 'm|' || "Id" || '|' || "MapType" || '|' || "OwnerId" || '|' || "Name" || '|' || "IsActive" || '|' || "Version" || '|' || "UpdatedAt" AS line FROM "Maps"
  UNION ALL
  SELECT 'n|' || "Id" || '|' || "MapId" || '|' || "NodeType" || '|' || COALESCE("RefId",'-') || '|' || "DisplayName" || '|' || "IsExitPoint" || '|' || COALESCE("TargetMapId",'-') || '|' || COALESCE("TargetNodeId",'-') || '|' || "IsHidden" || '|' || "SortOrder" FROM "MapNodes"
  UNION ALL
  SELECT 'e|' || "Id"::text || '|' || "MapId" || '|' || "FromNodeId" || '|' || "ToNodeId" || '|' || "Direction" || '|' || "EdgeType" || '|' || COALESCE("Label",'-') || '|' || "IsTraversable" || '|' || "IsHidden" FROM "MapEdges"
) t;
SQL
}

history_count() {
  local db=$1
  db_query "$db" "SELECT count(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" LIKE '%${MIGRATION_NAME}';"
}

install_expectations() {
  local db=$1
  db_sql "$db" >/dev/null <<'SQL'
SET client_min_messages = warning;
DROP TABLE IF EXISTS _verify_nodes;
CREATE TABLE _verify_nodes (
    "Id" text PRIMARY KEY, "MapId" text NOT NULL, "RefId" text NOT NULL, "DisplayName" text NOT NULL,
    "TargetMapId" text NOT NULL, "TargetNodeId" text NOT NULL,
    "LayoutX" integer NOT NULL, "LayoutY" integer NOT NULL, "SortOrder" integer NOT NULL);
INSERT INTO _verify_nodes VALUES
 ('zone:aldenmere:exit:zone:barrow-deeps','zone:aldenmere','barrow-deeps','Descent into the Barrow Deeps','zone:barrow-deeps','zone:barrow-deeps:entry',10,15,50),
 ('zone:aldenmere:exit:zone:pale-moor','zone:aldenmere','pale-moor','Road to the Pale Moor','zone:pale-moor','zone:pale-moor:entry',48,10,60),
 ('zone:ashfields:exit:zone:skarhold','zone:ashfields','skarhold','Road to Skarhold','zone:skarhold','zone:skarhold:entry',20,24,50),
 ('zone:ashfields:exit:zone:smoldering-reach','zone:ashfields','smoldering-reach','Track to the Smoldering Reach','zone:smoldering-reach','zone:smoldering-reach:entry',45,47,60),
 ('zone:pale-moor:exit:zone:aldenmere','zone:pale-moor','aldenmere','Road to Aldenmere','zone:aldenmere','zone:aldenmere:entry',20,24,50),
 ('zone:pale-moor:exit:zone:soddenfen','zone:pale-moor','soddenfen','Track to Soddenfen','zone:soddenfen','zone:soddenfen:entry',45,47,60),
 ('zone:saltcliff-heights:exit:zone:tolvaren','zone:saltcliff-heights','tolvaren','Road to Tolvaren','zone:tolvaren','zone:tolvaren:entry',24,32,50),
 ('zone:skarhold:exit:zone:ashfields','zone:skarhold','ashfields','Road to the Ashfields','zone:ashfields','zone:ashfields:entry',24,8,50),
 ('zone:smoldering-reach:exit:zone:kaldrek-maw','zone:smoldering-reach','kaldrek-maw','Descent into the Maw','zone:kaldrek-maw','zone:kaldrek-maw:entry',38,10,50),
 ('zone:tolvaren:exit:zone:saltcliff-heights','zone:tolvaren','saltcliff-heights','Climb to the Saltcliff Heights','zone:saltcliff-heights','zone:saltcliff-heights:entry',24,8,50),
 ('zone:tolvaren:exit:zone:sunken-name','zone:tolvaren','sunken-name','Descent into the Sunken Name','zone:sunken-name','zone:sunken-name:entry',10,19,60),
 ('zone:tolvaren:exit:zone:tidewrack-flats','zone:tolvaren','tidewrack-flats','Track to the Tidewrack Flats','zone:tidewrack-flats','zone:tidewrack-flats:entry',35,22,70);

DROP TABLE IF EXISTS _verify_edges;
CREATE TABLE _verify_edges (
    "Id" uuid PRIMARY KEY, "MapId" text NOT NULL, "FromNodeId" text NOT NULL, "ToNodeId" text NOT NULL,
    "Direction" text NOT NULL, "EdgeType" text NOT NULL, "Label" text);
INSERT INTO _verify_edges VALUES
 ('27b8b645-fbf3-46dc-b7f0-7bbde33cd1b3','zone:aldenmere','zone:aldenmere:location:ironhollow-keep','zone:aldenmere:exit:zone:barrow-deeps','South','ExitToZone','barrow-stair'),
 ('2d43bf13-803c-43e2-8c95-82e967ee47a8','zone:aldenmere','zone:aldenmere:exit:zone:barrow-deeps','zone:aldenmere:location:ironhollow-keep','North','ExitToZone','barrow-stair'),
 ('ccfc4ff2-6fe6-42f1-bb30-9024de679f51','zone:aldenmere','zone:aldenmere:location:aldenmere-marketplace','zone:aldenmere:exit:zone:pale-moor','East','ExitToZone','moor-road'),
 ('fa16fa6d-c1de-4da4-a137-1e270e1ba8d4','zone:aldenmere','zone:aldenmere:exit:zone:pale-moor','zone:aldenmere:location:aldenmere-marketplace','West','ExitToZone','moor-road'),
 ('5a7d1e2e-6a01-4a21-a9ee-2081b888bd17','zone:ashfields','zone:ashfields:entry','zone:ashfields:exit:zone:skarhold','West','ExitToZone','ash-road'),
 ('c8c9455d-8565-4075-a2e1-2c18c783b37a','zone:ashfields','zone:ashfields:exit:zone:skarhold','zone:ashfields:entry','East','ExitToZone','ash-road'),
 ('c549a9ed-de23-4b88-bfc3-c201f213b106','zone:ashfields','zone:ashfields:location:scorched-battlefield','zone:ashfields:exit:zone:smoldering-reach','South','ExitToZone','lava-track'),
 ('3d97744b-383a-45a1-bb9b-dfc6629df4aa','zone:ashfields','zone:ashfields:exit:zone:smoldering-reach','zone:ashfields:location:scorched-battlefield','North','ExitToZone','lava-track'),
 ('0044c3f3-4efa-4af6-b173-4f0ed103c0bd','zone:pale-moor','zone:pale-moor:entry','zone:pale-moor:exit:zone:aldenmere','West','ExitToZone','waytown-road'),
 ('6d93b407-2edd-4d16-9976-02e3938690bf','zone:pale-moor','zone:pale-moor:exit:zone:aldenmere','zone:pale-moor:entry','East','ExitToZone','waytown-road'),
 ('e9151578-f5b8-44cc-99c1-15d6e7e011ee','zone:pale-moor','zone:pale-moor:location:moorstone-cairns','zone:pale-moor:exit:zone:soddenfen','South','ExitToZone','fen-track'),
 ('7f07a552-17d9-4971-aed2-50593c8036d9','zone:pale-moor','zone:pale-moor:exit:zone:soddenfen','zone:pale-moor:location:moorstone-cairns','North','ExitToZone','fen-track'),
 ('8d69e45a-18f8-42ca-8f73-8631f94842a8','zone:saltcliff-heights','zone:saltcliff-heights:entry','zone:saltcliff-heights:exit:zone:tolvaren','South','ExitToZone','cliff-road'),
 ('a7cb64b6-1572-47a7-a671-373fa2098fb6','zone:saltcliff-heights','zone:saltcliff-heights:exit:zone:tolvaren','zone:saltcliff-heights:entry','North','ExitToZone','cliff-road'),
 ('6a4f967b-60f6-4830-9b31-f7f0a06665a6','zone:skarhold','zone:skarhold:entry','zone:skarhold:exit:zone:ashfields','North','ExitToZone','caldera-gate'),
 ('8173294a-e2b7-4557-b193-2cc472845bf4','zone:skarhold','zone:skarhold:exit:zone:ashfields','zone:skarhold:entry','South','ExitToZone','caldera-gate'),
 ('c8e866e2-3144-49d0-9eb7-0567d06be0d8','zone:smoldering-reach','zone:smoldering-reach:location:vent-fields','zone:smoldering-reach:exit:zone:kaldrek-maw','East','ExitToZone','vent-descent'),
 ('b62af2e4-fddd-4aa0-ae35-e887233b810a','zone:smoldering-reach','zone:smoldering-reach:exit:zone:kaldrek-maw','zone:smoldering-reach:location:vent-fields','West','ExitToZone','vent-descent'),
 ('cf9516dd-7c7a-4da5-b430-c3d1ee67de99','zone:tolvaren','zone:tolvaren:entry','zone:tolvaren:exit:zone:saltcliff-heights','North','ExitToZone','cliff-stair'),
 ('3e1a9d26-74ce-4546-a8d6-fdd5ae8cae94','zone:tolvaren','zone:tolvaren:exit:zone:saltcliff-heights','zone:tolvaren:entry','South','ExitToZone','cliff-stair'),
 ('27d355b2-5092-45a5-9452-4efa6ee45d31','zone:tolvaren','zone:tolvaren:location:tolvaren-harbour','zone:tolvaren:exit:zone:sunken-name','South','ExitToZone','harbour-stair'),
 ('437d5d3a-2d52-42e1-a8eb-1d765456183b','zone:tolvaren','zone:tolvaren:exit:zone:sunken-name','zone:tolvaren:location:tolvaren-harbour','North','ExitToZone','harbour-stair'),
 ('c5f24ce4-3ad2-4e1d-89aa-b522d5505f58','zone:tolvaren','zone:tolvaren:location:cliff-road-market','zone:tolvaren:exit:zone:tidewrack-flats','South','ExitToZone','shore-track'),
 ('77a606ba-c181-405a-9db8-a139b4eab06e','zone:tolvaren','zone:tolvaren:exit:zone:tidewrack-flats','zone:tolvaren:location:cliff-road-market','North','ExitToZone','shore-track'),
 ('9fe48038-d744-4052-959e-91fb32adc5c9','region:cinderplain','region:cinderplain:entry','region:cinderplain:zone:skarhold','East','Walk',NULL),
 ('66e2b0f4-f9c7-4697-9430-046667d651cb','region:saltcliff','region:saltcliff:entry','region:saltcliff:zone:saltcliff-heights','South','Walk',NULL);
SQL
}

assert_doors_absent() {
  local db=$1 label=$2 nodes slots guids displaced
  nodes="$(db_query "$db" 'SELECT count(*) FROM _verify_nodes e JOIN "MapNodes" n ON n."Id" = e."Id";')"
  slots="$(db_query "$db" 'SELECT count(*) FROM _verify_edges e JOIN "MapEdges" x ON x."MapId" = e."MapId" AND x."FromNodeId" = e."FromNodeId" AND x."Direction" = e."Direction" WHERE x."ToNodeId" = e."ToNodeId";')"
  guids="$(db_query "$db" 'SELECT count(*) FROM _verify_edges e JOIN "MapEdges" x ON x."Id" = e."Id";')"
  # A slot may be occupied by a seed-time walk the new door displaces; that is the declared re-home, not a door.
  displaced="$(db_query "$db" 'SELECT count(*) FROM _verify_edges e WHERE EXISTS (SELECT 1 FROM "MapEdges" x WHERE x."MapId" = e."MapId" AND x."FromNodeId" = e."FromNodeId" AND x."Direction" = e."Direction" AND x."ToNodeId" <> e."ToNodeId");')"
  assert_eq "$label: door nodes absent before the upgrade" "$nodes" "0"
  assert_eq "$label: door/Arrival edges absent before the upgrade" "$slots" "0"
  assert_eq "$label: frozen edge uuids absent before the upgrade" "$guids" "0"
  pass "$label: $displaced expected slot(s) hold a seed-time walk the patch re-homes"
  printf '[%s] before: %s, doors 0/12, slots 0/26, history %s\n' "$label" "$(graph_counts "$db")" "$(history_count "$db")"
}

assert_upgrade() {
  local db=$1 label=$2 nodes edges slots distinct_slots history
  nodes="$(db_query "$db" 'SELECT count(*) FROM _verify_nodes e JOIN "MapNodes" n ON n."Id" = e."Id" WHERE n."MapId" = e."MapId" AND n."NodeType" = $q$ZoneExit$q$ AND n."RefId" = e."RefId" AND n."DisplayName" = e."DisplayName" AND n."IsExitPoint" AND NOT n."IsEntryPoint" AND NOT n."IsHidden" AND n."TargetMapId" = e."TargetMapId" AND n."TargetNodeId" = e."TargetNodeId" AND n."UnlockType" IS NULL AND n."UnlockKey" IS NULL AND n."DiscoverThreshold" IS NULL AND n."LayoutX" = e."LayoutX" AND n."LayoutY" = e."LayoutY" AND n."SortOrder" = e."SortOrder";')"
  edges="$(db_query "$db" 'SELECT count(*) FROM _verify_edges e JOIN "MapEdges" x ON x."Id" = e."Id" WHERE x."MapId" = e."MapId" AND x."FromNodeId" = e."FromNodeId" AND x."ToNodeId" = e."ToNodeId" AND x."Direction" = e."Direction" AND x."EdgeType" = e."EdgeType" AND x."Label" IS NOT DISTINCT FROM e."Label" AND x."IsTraversable" AND NOT x."IsHidden";')"
  slots="$(db_query "$db" 'SELECT count(*) FROM _verify_edges e WHERE EXISTS (SELECT 1 FROM "MapEdges" x WHERE x."MapId" = e."MapId" AND x."FromNodeId" = e."FromNodeId" AND x."Direction" = e."Direction");')"
  distinct_slots="$(db_query "$db" 'SELECT count(DISTINCT (e."MapId", e."FromNodeId", e."Direction")) FROM _verify_edges e WHERE EXISTS (SELECT 1 FROM "MapEdges" x WHERE x."MapId" = e."MapId" AND x."FromNodeId" = e."FromNodeId" AND x."Direction" = e."Direction");')"
  history="$(history_count "$db")"
  assert_eq "$label: 12 door nodes read back with the frozen topology" "$nodes" "12"
  assert_eq "$label: 26 door/Arrival edges read back with the frozen uuid, slot and label" "$edges" "26"
  assert_eq "$label: all 26 natural slots occupied" "$slots" "26"
  assert_eq "$label: no duplicate slots" "$distinct_slots" "26"
  assert_eq "$label: migration recorded exactly once" "$history" "1"
  printf '[%s] after:  %s, doors 12/12, slots 26/26, history %s\n' "$label" "$(graph_counts "$db")" "$history"
}

snapshot_versions() {
  local db=$1
  db_sql "$db" >/dev/null <<'SQL'
SET client_min_messages = warning;
DROP TABLE IF EXISTS _verify_versions;
CREATE TABLE _verify_versions AS SELECT "Id", "Version" FROM "Maps";
SQL
}

assert_single_bump() {
  local db=$1 label=$2 changed bumped
  changed="$(db_query "$db" 'SELECT count(*) FROM "Maps" m JOIN _verify_versions v USING ("Id") WHERE m."Version" <> v."Version";')"
  bumped="$(db_query "$db" 'SELECT count(*) FROM "Maps" m JOIN _verify_versions v USING ("Id") WHERE m."Version" = v."Version" + 1;')"
  assert_eq "$label: 9 changed maps were version-bumped once" "$bumped" "9"
  assert_eq "$label: no map was version-bumped twice or by another amount" "$changed" "9"
}

# --- 0. toolchain and sources ------------------------------------------------------------------------------------

"$DOTNET" tool restore >/dev/null 2>&1 || true

MAIN_TIP="$(git -C "$ROOT" rev-parse origin/main 2>/dev/null)" || fail "origin/main is not available; fetch it first"
OLD_DOORS_SHA="$(git -C "$ROOT" rev-parse "$OLD_DOORS_SHA")"
HEAD_SHA="$(git -C "$ROOT" rev-parse HEAD)"
PATCH_FILE="$(ls "$ROOT"/RealmEngine.Data/Migrations/*_"${MIGRATION_NAME}".cs 2>/dev/null | head -n 1)"
[ -n "$PATCH_FILE" ] || fail "migration $MIGRATION_NAME not found"
MIGRATION_PATH="${PATCH_FILE#"$ROOT"/}"

# The predecessor on main is the last commit there before this migration landed. Once main carries the migration the
# tip is not that commit any more — a server built from the tip has already applied the patch, so its seed contains
# the twelve doors and every "absent before the upgrade" check is false. Walk main's first-parent chain back to the
# newest commit without the file; while main still predates the migration the loop exits immediately on the tip.
MAIN_SHA="$MAIN_TIP"
while git -C "$ROOT" cat-file -e "$MAIN_SHA:$MIGRATION_PATH" 2>/dev/null; do
  MAIN_SHA="$(git -C "$ROOT" rev-parse "$MAIN_SHA^1" 2>/dev/null)" \
    || fail "no commit on main's first-parent chain predates the migration"
done
PATCH_SQL="$WORK/patch.sql"
python3 - "$PATCH_FILE" > "$PATCH_SQL" <<'PY'
import sys
text = open(sys.argv[1], encoding='utf-8').read()
marker = 'private const string PatchSql = """\n'
start = text.index(marker) + len(marker)
lines = text[start:].split('\n')
out, indent = [], None
for line in lines:
    if line.strip() == '""";':
        indent = len(line) - len(line.lstrip(' '))
        break
    out.append(line)
if indent is None:
    raise SystemExit('PatchSql closing delimiter not found')
body = ['' if not line.strip() else line[indent:] for line in out]
sys.stdout.write('\n'.join(body) + '\n')
PY

log "head $HEAD_SHA"
log "pre-#270 seed $OLD_DOORS_SHA"
log "origin/main ${MAIN_TIP:0:8}; last pre-migration commit on it $MAIN_SHA"

start_postgres

add_worktree "$OLD_DOORS_SHA" old-doors
OLD_DOORS_WT="$WT_LAST"
add_worktree "$MAIN_SHA" old-main
MAIN_WT="$WT_LAST"
build_server "$OLD_DOORS_WT" old-doors
build_server "$MAIN_WT" old-main
build_server "$ROOT" head

# A long run creates and builds two throwaway worktrees; if one ever disappears under us, recreate it rather than
# starting a server from a half-populated tree.
ensure_server_dir() {
  local dir=$1 sha=$2 label=$3
  if [ ! -d "$dir/Veldrath.Server" ]; then
    log "$label worktree is missing at $dir; recreating it"
    git -C "$ROOT" worktree remove --force "$dir" >/dev/null 2>&1 || true
    git -C "$ROOT" worktree add --detach "$dir" "$sha" >/dev/null 2>&1 || fail "could not recreate $label at $sha"
    build_server "$dir" "$label"
  fi
}

# --- 1. seed the two old databases with the real old servers ------------------------------------------------------

create_db old_seed
ensure_server_dir "$OLD_DOORS_WT" "$OLD_DOORS_SHA" old-doors
start_and_wait "$OLD_DOORS_WT" old_seed "$WORK/old-doors.log" 'pre-#270 server'
OLD_PID="$API_LAST_PID"
OLD_PORT="$API_LAST_PORT"
wait_seeded old_seed "$OLD_PID" || { tail -n 40 "$WORK/old-doors.log" >&2; fail "pre-#270 server did not seed"; }
stop_api "$OLD_PID" "$OLD_PORT"
OLD_COUNTS="$(graph_counts old_seed)"
log "pre-#270 seed populated: $OLD_COUNTS"
install_expectations old_seed
assert_doors_absent old_seed pre-270

create_db main_seed
ensure_server_dir "$MAIN_WT" "$MAIN_SHA" old-main
start_and_wait "$MAIN_WT" main_seed "$WORK/old-main.log" 'origin/main server'
MAIN_PID="$API_LAST_PID"
MAIN_PORT="$API_LAST_PORT"
wait_seeded main_seed "$MAIN_PID" || { tail -n 40 "$WORK/old-main.log" >&2; fail "origin/main server did not seed"; }
stop_api "$MAIN_PID" "$MAIN_PORT"
log "origin/main seed populated: $(graph_counts main_seed)"
install_expectations main_seed
assert_doors_absent main_seed main
snapshot_versions main_seed

# --- 2. the plain upgrade, with unrelated authoring on the old database --------------------------------------------

clone_db upgrade old_seed
install_expectations upgrade
# Unrelated authoring a person could have done on this instance: a user-owned map, a renamed seeded node, and a
# reviewed version on a map the migration will patch.
db_sql upgrade >/dev/null <<'SQL'
SET client_min_messages = warning;
INSERT INTO "Maps" ("Id", "MapType", "OwnerId", "Name", "Description", "EntryNodeId", "IsActive", "Version", "UpdatedAt", "CreatedByAccountId")
VALUES ('zone:foundry-homestead', 'Zone', 'foundry-homestead', 'Foundry Homestead', 'A hand-authored map that the backfill must not touch.', 'zone:foundry-homestead:entry', true, 1, '2026-02-01T00:00:00Z', 'aaaaaaaa-1111-4111-8111-111111111111');
INSERT INTO "MapNodes" ("Id", "MapId", "NodeType", "RefId", "DisplayName", "Description", "IconKey", "LayoutX", "LayoutY", "IsEntryPoint", "IsExitPoint", "TargetMapId", "TargetNodeId", "IsHidden", "UnlockType", "UnlockKey", "DiscoverThreshold", "SortOrder")
VALUES ('zone:foundry-homestead:entry', 'zone:foundry-homestead', 'EntryPoint', NULL, 'Homestead Gate', 'Arrival.', 'login', 4, 4, true, false, NULL, NULL, false, NULL, NULL, NULL, 0),
       ('zone:foundry-homestead:hearth', 'zone:foundry-homestead', 'Location', 'hearth', 'The Hearth', 'A fire that is always lit.', 'place', 12, 4, false, false, NULL, NULL, false, NULL, NULL, NULL, 10);
INSERT INTO "MapEdges" ("Id", "MapId", "FromNodeId", "ToNodeId", "Direction", "EdgeType", "Label", "IsTraversable", "IsHidden")
VALUES ('cccccccc-1111-4111-8111-111111111111', 'zone:foundry-homestead', 'zone:foundry-homestead:entry', 'zone:foundry-homestead:hearth', 'East', 'Walk', NULL, true, false),
       ('cccccccc-2222-4222-8222-222222222222', 'zone:foundry-homestead', 'zone:foundry-homestead:hearth', 'zone:foundry-homestead:entry', 'West', 'Walk', NULL, true, false);
UPDATE "MapNodes" SET "DisplayName" = 'Arrival (edited by a person)' WHERE "Id" = 'zone:crestfall:entry';
UPDATE "Maps" SET "Version" = 7, "ApprovedVersion" = 1, "ApprovedAt" = '2026-02-03T04:05:06Z',
       "ApprovedByAccountId" = 'bbbbbbbb-2222-4222-8222-222222222222',
       "LastReviewNotes" = 'approved on the pre-#270 graph', "LastReviewedAt" = '2026-02-03T04:05:06Z',
       "SubmittedAt" = '2026-02-02T04:05:06Z', "SubmittedByAccountId" = 'bbbbbbbb-3333-4333-8333-333333333333'
 WHERE "Id" = 'zone:aldenmere';
DROP TABLE IF EXISTS _verify_unrelated;
CREATE TABLE _verify_unrelated AS
SELECT md5(string_agg(line, E'\n' ORDER BY line)) AS checksum FROM (
  SELECT 'map|' || "Id" || '|' || "Name" || '|' || "Version" || '|' || COALESCE("CreatedByAccountId"::text, '-') AS line
  FROM "Maps" WHERE "Id" = 'zone:foundry-homestead'
  UNION ALL
  SELECT 'node|' || "Id" || '|' || "DisplayName" || '|' || "LayoutX" || '|' || "SortOrder"
  FROM "MapNodes" WHERE "MapId" = 'zone:foundry-homestead'
     OR "Id" IN ('region:cinderplain:exit:greymoor', 'region:saltcliff:exit:greymoor', 'zone:crestfall:entry')
  UNION ALL
  SELECT 'edge|' || "Id"::text || '|' || "MapId" || '|' || "FromNodeId" || '|' || "ToNodeId" || '|' || "Direction" || '|' || "EdgeType" || '|' || COALESCE("Label", '-')
  FROM "MapEdges" WHERE "MapId" = 'zone:foundry-homestead'
     OR "FromNodeId" IN ('region:cinderplain:exit:greymoor', 'region:saltcliff:exit:greymoor')
     OR "ToNodeId" IN ('region:cinderplain:exit:greymoor', 'region:saltcliff:exit:greymoor')
) t;
SQL
UNRELATED_BEFORE="$(db_query upgrade 'SELECT checksum FROM _verify_unrelated;')"
REHOME_UUID_BEFORE="$(db_query upgrade 'SELECT "Id"::text FROM "MapEdges" WHERE "MapId" = $q$zone:aldenmere$q$ AND "FromNodeId" = $q$zone:aldenmere:location:aldenmere-marketplace$q$ AND "Direction" = $q$East$q$;')"
snapshot_versions upgrade
preflight_db upgrade plain
start_and_wait "$ROOT" upgrade "$WORK/upgrade.log" 'PR server on the pre-#270 database'
UPGRADE_PID="$API_LAST_PID"
UPGRADE_PORT="$API_LAST_PORT"
install_expectations upgrade
assert_upgrade upgrade plain
assert_single_bump upgrade plain
assert_eq "plain: the re-homed walk kept its uuid" "$(db_query upgrade 'SELECT "Id"::text FROM "MapEdges" WHERE "MapId" = $q$zone:aldenmere$q$ AND "FromNodeId" = $q$zone:aldenmere:location:aldenmere-marketplace$q$ AND "Direction" = $q$North$q$;')" "$REHOME_UUID_BEFORE"
assert_eq "plain: the old East walk slot now holds the moor-road door" "$(db_query upgrade 'SELECT "ToNodeId" FROM "MapEdges" WHERE "MapId" = $q$zone:aldenmere$q$ AND "FromNodeId" = $q$zone:aldenmere:location:aldenmere-marketplace$q$ AND "Direction" = $q$East$q$;')" "zone:aldenmere:exit:zone:pale-moor"
assert_eq "plain: user-authored map, portals and renamed node survive untouched" "$(db_query upgrade 'SELECT checksum FROM _verify_unrelated;')" "$UNRELATED_BEFORE"
assert_eq "plain: the reviewed map was bumped exactly once (7 -> 8)" "$(db_query upgrade 'SELECT "Version" FROM "Maps" WHERE "Id" = $q$zone:aldenmere$q$;')" "8"
assert_eq "plain: activation and review fields preserved on aldenmere" "$(db_query upgrade 'SELECT count(*) FROM "Maps" WHERE "Id" = $q$zone:aldenmere$q$ AND "IsActive" AND "CreatedByAccountId" IS NULL AND "ApprovedVersion" = 1 AND "ApprovedAt" = $q$2026-02-03T04:05:06Z$q$::timestamptz AND "ApprovedByAccountId" = $q$bbbbbbbb-2222-4222-8222-222222222222$q$ AND "LastReviewNotes" = $q$approved on the pre-#270 graph$q$ AND "LastReviewedAt" = $q$2026-02-03T04:05:06Z$q$::timestamptz AND "SubmittedAt" = $q$2026-02-02T04:05:06Z$q$::timestamptz AND "SubmittedByAccountId" = $q$bbbbbbbb-3333-4333-8333-333333333333$q$;')" "1"

# Restart on the same database: no re-seed, no reset, no duplicates, no second version bump.
RESTART_COUNTS="$(graph_counts upgrade)"
RESTART_CHECKSUM="$(graph_checksum upgrade)"
stop_api "$UPGRADE_PID" "$UPGRADE_PORT"
start_and_wait "$ROOT" upgrade "$WORK/upgrade-restart.log" 'PR server restart'
assert_eq "restart: no duplicate rows" "$(graph_counts upgrade)" "$RESTART_COUNTS"
assert_eq "restart: no second version bump or rewrite" "$(graph_checksum upgrade)" "$RESTART_CHECKSUM"
assert_eq "restart: still one migration-history row" "$(history_count upgrade)" "1"

# Replay the exact Up body: it must be a no-op (this is the migration body, rolled back).
REPLAY_OUT="$WORK/replay.log"
{ echo 'BEGIN;'; cat "$PATCH_SQL"; echo 'ROLLBACK;'; } | db_sql upgrade >"$REPLAY_OUT" 2>&1 || { cat "$REPLAY_OUT" >&2; fail "replaying the Up body failed"; }
assert_contains "replay: the body inserts nothing" "$REPLAY_OUT" "inserted 0 node(s) and 0 edge(s); re-homed 0 seed-time walk(s)"
assert_contains "replay: the body bumps no map" "$REPLAY_OUT" "bumped 0 map version(s)"
assert_eq "replay: the graph is unchanged afterwards" "$(graph_counts upgrade)" "$RESTART_COUNTS"

RESULTS+=("plain upgrade from pre-#270: PASS ($RESTART_COUNTS)")

# --- 3. the actual predecessor on main -----------------------------------------------------------------------------

preflight_db main_seed main-predecessor
start_and_wait "$ROOT" main_seed "$WORK/main-upgrade.log" 'PR server on the origin/main database'
install_expectations main_seed
assert_upgrade main_seed main-predecessor
assert_single_bump main_seed main-predecessor
RESULTS+=("upgrade from origin/main ${MAIN_SHA:0:8}: PASS")

# --- 4. fresh database ---------------------------------------------------------------------------------------------

create_db fresh
install_expectations fresh
start_and_wait "$ROOT" fresh "$WORK/fresh.log" 'PR server on an empty database'
install_expectations fresh
assert_upgrade fresh fresh
assert_eq "fresh: the seeder created the whole graph" "$(graph_counts fresh)" "maps=23 nodes=148 edges=332"
assert_eq "fresh: the migration returned without bumping anything" "$(db_query fresh 'SELECT count(*) FROM "Maps" WHERE "Version" <> 1;')" "0"
RESULTS+=("fresh database: PASS (seeded, migration returned)")

# --- 5. an equivalent door node and Arrival hop already present ----------------------------------------------------

clone_db equivalent old_seed
install_expectations equivalent
db_sql equivalent >/dev/null <<'SQL'
INSERT INTO "MapNodes" ("Id", "MapId", "NodeType", "RefId", "DisplayName", "Description", "IconKey", "LayoutX", "LayoutY", "IsEntryPoint", "IsExitPoint", "TargetMapId", "TargetNodeId", "IsHidden", "UnlockType", "UnlockKey", "DiscoverThreshold", "SortOrder")
VALUES ('zone:saltcliff-heights:exit:zone:tolvaren', 'zone:saltcliff-heights', 'ZoneExit', 'tolvaren', 'Custom Tolvaren Gate', 'Hand-authored before the backfill.', 'exit_to_app', 99, 99, false, true, 'zone:tolvaren', 'zone:tolvaren:entry', false, NULL, NULL, NULL, 50);
INSERT INTO "MapEdges" ("Id", "MapId", "FromNodeId", "ToNodeId", "Direction", "EdgeType", "Label", "IsTraversable", "IsHidden")
VALUES ('11111111-1111-4111-8111-111111111111', 'region:cinderplain', 'region:cinderplain:entry', 'region:cinderplain:zone:skarhold', 'East', 'Walk', 'intervening-by-foundry', true, false);
SQL
preflight_db equivalent equivalent
start_and_wait "$ROOT" equivalent "$WORK/equivalent.log" 'PR server over an equivalent edge'
install_expectations equivalent
assert_eq "equivalent: the pre-existing door node keeps its authored text and layout" "$(db_query equivalent 'SELECT "DisplayName" || $q$|$q$ || "LayoutX" || $q$|$q$ || "LayoutY" FROM "MapNodes" WHERE "Id" = $q$zone:saltcliff-heights:exit:zone:tolvaren$q$;')" "Custom Tolvaren Gate|99|99"
assert_eq "equivalent: both door edges were added around the kept node" "$(db_query equivalent 'SELECT count(*) FROM "MapEdges" WHERE "Id" IN ($q$8d69e45a-18f8-42ca-8f73-8631f94842a8$q$,$q$a7cb64b6-1572-47a7-a671-373fa2098fb6$q$);')" "2"
assert_eq "equivalent: the intervening hop keeps its uuid and label" "$(db_query equivalent 'SELECT "Id" || $q$|$q$ || COALESCE("Label", $q$-$q$) FROM "MapEdges" WHERE "MapId" = $q$region:cinderplain$q$ AND "FromNodeId" = $q$region:cinderplain:entry$q$ AND "Direction" = $q$East$q$;')" "11111111-1111-4111-8111-111111111111|intervening-by-foundry"
assert_eq "equivalent: the frozen hop uuid was not inserted" "$(db_query equivalent 'SELECT count(*) FROM "MapEdges" WHERE "Id" = $q$9fe48038-d744-4052-959e-91fb32adc5c9$q$;')" "0"
assert_eq "equivalent: exactly one edge in that slot" "$(db_query equivalent 'SELECT count(*) FROM "MapEdges" WHERE "MapId" = $q$region:cinderplain$q$ AND "FromNodeId" = $q$region:cinderplain:entry$q$ AND "Direction" = $q$East$q$;')" "1"
assert_eq "equivalent: the other 25 frozen uuids (24 doors and the Saltcliff hop) were inserted as declared" "$(db_query equivalent 'SELECT count(*) FROM _verify_edges e JOIN "MapEdges" x ON x."Id" = e."Id" WHERE NOT (e."MapId" = $q$region:cinderplain$q$ AND e."FromNodeId" = $q$region:cinderplain:entry$q$);')" "25"
assert_eq "equivalent: all 26 natural slots occupied exactly once" "$(db_query equivalent 'SELECT count(*) FROM _verify_edges e WHERE (SELECT count(*) FROM "MapEdges" x WHERE x."MapId" = e."MapId" AND x."FromNodeId" = e."FromNodeId" AND x."Direction" = e."Direction") = 1;')" "26"
assert_eq "equivalent: 12 door nodes and one history row" "$(db_query equivalent 'SELECT (SELECT count(*) FROM _verify_nodes e JOIN "MapNodes" n ON n."Id" = e."Id") || $q$/$q$ || (SELECT count(*) FROM "__EFMigrationsHistory" WHERE "MigrationId" LIKE $q$%BackfillSeededMapDoorsAndArrivalHops$q$);')" "12/1"
RESULTS+=("equivalent intervening edge/node: PASS (kept uuid and label)")

# --- 6. a legacy seed: the walk was never derived, region markers were never linked --------------------------------

# The plain pre-#270 seed cannot produce the shape production was actually in. Production was seeded several
# generations earlier: the marketplace's walk to the Grey Cup does not exist at all (so there is nothing to re-home,
# and demanding one is a boot loop), and the region boards' zone markers have no target (so the Arrival hop would land
# on nothing). The patch must converge on both — and a marker that points somewhere else must still abort.
clone_db legacy old_seed
install_expectations legacy
db_sql legacy >/dev/null <<'SQL'
SET client_min_messages = warning;
DELETE FROM "MapEdges"
 WHERE "MapId" = 'zone:aldenmere'
   AND "FromNodeId" = 'zone:aldenmere:location:aldenmere-marketplace'
   AND "ToNodeId" = 'zone:aldenmere:location:grey-cup';
UPDATE "MapNodes" SET "TargetMapId" = NULL, "TargetNodeId" = NULL
 WHERE "NodeType" = 'Zone' AND "MapId" LIKE 'region:%';
SQL
assert_eq "legacy: the seed-time walk is absent before the upgrade" "$(db_query legacy 'SELECT count(*) FROM "MapEdges" WHERE "MapId" = $q$zone:aldenmere$q$ AND "FromNodeId" = $q$zone:aldenmere:location:aldenmere-marketplace$q$ AND "ToNodeId" = $q$zone:aldenmere:location:grey-cup$q$;')" "0"
assert_eq "legacy: no region marker carries a target before the upgrade" "$(db_query legacy 'SELECT count(*) FROM "MapNodes" WHERE "NodeType" = $q$Zone$q$ AND "MapId" LIKE $q$region:%$q$ AND "TargetMapId" IS NOT NULL;')" "0"
snapshot_versions legacy
preflight_db legacy legacy
start_and_wait "$ROOT" legacy "$WORK/legacy.log" 'PR server on a legacy seed'
install_expectations legacy
assert_upgrade legacy legacy
assert_single_bump legacy legacy
assert_eq "legacy: the absent walk was added on the seeded North direction" "$(db_query legacy 'SELECT "ToNodeId" || $q$|$q$ || "EdgeType" FROM "MapEdges" WHERE "MapId" = $q$zone:aldenmere$q$ AND "FromNodeId" = $q$zone:aldenmere:location:aldenmere-marketplace$q$ AND "Direction" = $q$North$q$;')" "zone:aldenmere:location:grey-cup|Walk"
assert_eq "legacy: the market holds exactly one walk to the Grey Cup" "$(db_query legacy 'SELECT count(*) FROM "MapEdges" WHERE "MapId" = $q$zone:aldenmere$q$ AND "FromNodeId" = $q$zone:aldenmere:location:aldenmere-marketplace$q$ AND "ToNodeId" = $q$zone:aldenmere:location:grey-cup$q$;')" "1"
assert_eq "legacy: the East slot holds the moor-road door, not a second walk" "$(db_query legacy 'SELECT count(*) FROM "MapEdges" WHERE "MapId" = $q$zone:aldenmere$q$ AND "FromNodeId" = $q$zone:aldenmere:location:aldenmere-marketplace$q$ AND "Direction" = $q$East$q$ AND "EdgeType" = $q$Walk$q$;')" "0"
assert_eq "legacy: both region markers now lead to their own zone map" "$(db_query legacy 'SELECT count(*) FROM "MapNodes" WHERE "Id" IN ($q$region:cinderplain:zone:skarhold$q$,$q$region:saltcliff:zone:saltcliff-heights$q$) AND "TargetMapId" = $q$zone:$q$ || "RefId" AND "TargetNodeId" IS NULL;')" "2"
assert_eq "legacy: the linked markers keep their identity and stay visible on the board" "$(db_query legacy 'SELECT count(*) FROM "MapNodes" WHERE "Id" IN ($q$region:cinderplain:zone:skarhold$q$,$q$region:saltcliff:zone:saltcliff-heights$q$) AND "NodeType" = $q$Zone$q$ AND "MapId" LIKE $q$region:%$q$ AND NOT "IsHidden";')" "2"
LEGACY_COUNTS="$(graph_counts legacy)"
LEGACY_CHECKSUM="$(graph_checksum legacy)"
stop_api "$API_LAST_PID" "$API_LAST_PORT"
start_and_wait "$ROOT" legacy "$WORK/legacy-restart.log" 'PR server restart on the legacy seed'
assert_eq "legacy restart: no duplicate rows" "$(graph_counts legacy)" "$LEGACY_COUNTS"
assert_eq "legacy restart: no second version bump or rewrite" "$(graph_checksum legacy)" "$LEGACY_CHECKSUM"
assert_eq "legacy restart: still one migration-history row" "$(history_count legacy)" "1"
RESULTS+=("legacy seed (absent walk, unlinked markers): PASS ($LEGACY_COUNTS)")

# --- 7. conflicts must abort atomically ----------------------------------------------------------------------------

conflict_case() {
  local db=$1 label=$2 setup_sql=$3 expected_message=$4 expected_doors=${5:-0}
  local before checksum_before versions_before port pid logfile="$WORK/$db.log"
  clone_db "$db" old_seed
  install_expectations "$db"
  db_sql "$db" <<< "$setup_sql" >/dev/null
  before="$(graph_counts "$db")"
  checksum_before="$(graph_checksum "$db")"
  versions_before="$(db_query "$db" 'SELECT coalesce(sum("Version"), 0) FROM "Maps";')"
  port="$(free_port)"
  start_api "$ROOT" "$db" "$port" "$logfile"
  pid="${API_PIDS[${#API_PIDS[@]}-1]}"
  if wait_exit_without_listening "$pid" "$port" 240; then
    pass "$label: the server refused to start"
  else
    tail -n 40 "$logfile" >&2 || true
    fail "$label: the server started or never exited"
  fi
  assert_contains "$label: the failure names the exact conflict" "$logfile" "$expected_message"
  assert_eq "$label: no graph row changed" "$(graph_counts "$db")" "$before"
  assert_eq "$label: no row was rewritten" "$(graph_checksum "$db")" "$checksum_before"
  assert_eq "$label: no map was version-bumped" "$(db_query "$db" 'SELECT coalesce(sum("Version"), 0) FROM "Maps";')" "$versions_before"
  assert_eq "$label: no door row was added beyond the scenario's own starting state" "$(db_query "$db" 'SELECT count(*) FROM _verify_nodes e JOIN "MapNodes" n ON n."Id" = e."Id";')" "$expected_doors"
  assert_eq "$label: no migration-history row was written" "$(history_count "$db")" "0"
  RESULTS+=("$label: PASS (aborted, no partial writes, no history row)")
}

conflict_case conflict_slot "occupied slot" \
"INSERT INTO \"MapEdges\" (\"Id\", \"MapId\", \"FromNodeId\", \"ToNodeId\", \"Direction\", \"EdgeType\", \"Label\", \"IsTraversable\", \"IsHidden\")
 VALUES ('22222222-2222-4222-8222-222222222222', 'zone:aldenmere', 'zone:aldenmere:location:ironhollow-keep', 'zone:aldenmere:entry', 'South', 'Walk', 'stale-walk', true, false);" \
"an occupied direction holds a different edge"

conflict_case conflict_node "incompatible node" \
"INSERT INTO \"MapNodes\" (\"Id\", \"MapId\", \"NodeType\", \"RefId\", \"DisplayName\", \"Description\", \"IconKey\", \"LayoutX\", \"LayoutY\", \"IsEntryPoint\", \"IsExitPoint\", \"TargetMapId\", \"TargetNodeId\", \"IsHidden\", \"UnlockType\", \"UnlockKey\", \"DiscoverThreshold\", \"SortOrder\")
 VALUES ('zone:pale-moor:exit:zone:soddenfen', 'zone:pale-moor', 'ZoneExit', 'soddenfen', 'Track to Soddenfen', 'A different door wearing the backfill id.', 'exit_to_app', 1, 1, false, true, 'zone:tolvaren', 'zone:tolvaren:entry', false, NULL, NULL, NULL, 60);" \
"existing node(s) with a backfill id carry different topology" 1

# --- summary -------------------------------------------------------------------------------------------------------

printf '\n[verify-map-graph] all checks passed\n'
for line in "${RESULTS[@]}"; do printf '  PASS %s\n' "$line"; done
printf '\n  pre-#270 seed (%s): %s\n' "${OLD_DOORS_SHA:0:8}" "$OLD_COUNTS"
printf '  head (%s), migration %s: verified on old, main, fresh, equivalent, legacy and two conflict databases\n' "${HEAD_SHA:0:8}" "$MIGRATION_NAME"
