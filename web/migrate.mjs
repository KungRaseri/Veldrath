/**
 * Runtime migration script — runs Drizzle migrations using drizzle-orm/migrator.
 * Called by docker-entrypoint.sh before the server starts.
 * Does NOT require drizzle-kit (a dev dependency).
 */
import postgres from "postgres";
import { drizzle } from "drizzle-orm/postgres-js";
import { migrate } from "drizzle-orm/postgres-js/migrator";
import { existsSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const DATABASE_URL = process.env.DATABASE_URL;
if (!DATABASE_URL) {
  console.error("[migrate] DATABASE_URL is not set — aborting");
  process.exit(1);
}

const __dirname = dirname(fileURLToPath(import.meta.url));
// Prefer the repo layout (shared/db/migrations — works locally and in the
// image, where the shared workspace is copied to /app/shared); fall back to
// ./migrations (migrations copied next to this script in the Docker image).
const workspaceMigrations = join(__dirname, "shared", "db", "migrations");
const migrationsFolder = existsSync(workspaceMigrations) ? workspaceMigrations : join(__dirname, "migrations");

console.log("[migrate] Connecting to database…");
const connection = postgres(DATABASE_URL, { max: 1 });
const db = drizzle(connection);

console.log("[migrate] Running migrations from", migrationsFolder);
await migrate(db, { migrationsFolder });
console.log("[migrate] Migrations complete");

await connection.end();
