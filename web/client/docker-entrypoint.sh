#!/bin/sh
set -eu

cd /app

if [ -z "${DATABASE_URL:-}" ]; then
	echo "[entrypoint] DATABASE_URL is required for auth migrations." >&2
	exit 1
fi

echo "[entrypoint] waiting for the auth database..."
attempt=0
until node --input-type=module -e '
import postgres from "postgres";
const sql = postgres(process.env.DATABASE_URL, { max: 1 });
try {
  await sql`select 1`;
} finally {
  await sql.end();
}
' >/dev/null 2>&1; do
	attempt=$((attempt + 1))
	if [ "$attempt" -ge 30 ]; then
		echo "[entrypoint] auth database did not become reachable." >&2
		exit 1
	fi
	sleep 2
done

echo "[entrypoint] applying Drizzle auth migrations..."
node migrate.mjs

echo "[entrypoint] starting SvelteKit..."
cd /app/client
exec node build
