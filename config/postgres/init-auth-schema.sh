#!/bin/sh
set -eu

# Runs once on a fresh Postgres volume (docker-entrypoint-initdb.d).
# Existing volumes must run the same SQL manually; see web/shared/db/README.md.

: "${AUTH_DB_USER:=veldrath_auth}"
: "${AUTH_DB_PASSWORD:=veldrath_auth_dev}"

echo "[init-auth] ensuring ${AUTH_DB_USER} owns the auth schema..."

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<SQL
CREATE SCHEMA IF NOT EXISTS auth;

DO \$\$
BEGIN
	IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = '${AUTH_DB_USER}') THEN
		CREATE ROLE ${AUTH_DB_USER} LOGIN PASSWORD '${AUTH_DB_PASSWORD}';
	END IF;
END
\$\$;

GRANT CONNECT ON DATABASE ${POSTGRES_DB} TO ${AUTH_DB_USER};
-- Drizzle runs CREATE SCHEMA IF NOT EXISTS auth before migrating; that
-- statement requires CREATE on the database even when the schema exists.
GRANT CREATE ON DATABASE ${POSTGRES_DB} TO ${AUTH_DB_USER};
GRANT USAGE, CREATE ON SCHEMA auth TO ${AUTH_DB_USER};
ALTER ROLE ${AUTH_DB_USER} SET search_path = auth;
SQL

echo "[init-auth] auth schema and role are ready."
