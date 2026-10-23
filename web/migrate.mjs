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

console.log("[migrate] Connecting to database...");
const connection = postgres(DATABASE_URL, { max: 1 });

try {
  // Drizzle tables are defined with pgSchema("auth"), so all generated SQL is
  // schema-qualified and works regardless of the role's search_path. Creating
  // the schema here makes a fresh Coolify-managed database work out of the box.
  await connection`create schema if not exists auth`;

  const [row] = await connection`select current_user as db_user, current_schema() as schema`;
  console.log(`[migrate] db_user=${row.db_user} schema=${row.schema ?? "null"}`);

  const db = drizzle(connection);
  console.log("[migrate] Running migrations from", migrationsFolder);
  await migrate(db, {
    migrationsFolder,
    // Keep runtime and drizzle-kit bookkeeping in the same table.
    migrationsSchema: "auth",
    migrationsTable: "__drizzle_migrations",
  });
  console.log("[migrate] Migrations complete");
} finally {
  await connection.end();
}
