import { defineConfig } from "drizzle-kit";
import { loadEnvFile } from "node:process";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = fileURLToPath(new URL(".", import.meta.url));

// DATABASE_URL lives in client/.env — the SvelteKit auth-side connection.
// drizzle-kit runs from THIS workspace (shared/), so load client/.env to keep
// `npm run db:*` (root) working without exporting DATABASE_URL. The file is
// optional in CI; DATABASE_URL can still come from the shell.
try {
  loadEnvFile(resolve(here, "../client/.env"));
} catch {
  // .env is absent — rely on the environment.
}

if (!process.env.DATABASE_URL)
  throw new Error("DATABASE_URL is not set (expected in client/.env or the environment)");

export default defineConfig({
  schema: "./src/db/schema.ts",
  out: "./db/migrations",
  dialect: "postgresql",
  // Keep Drizzle's bookkeeping table inside the auth schema. The auth role has
  // CREATE on `auth` but not on the database, so it cannot create a separate
  // `drizzle` schema.
  migrations: {
    schema: "auth",
    table: "__drizzle_migrations",
  },
  dbCredentials: { url: process.env.DATABASE_URL },
  verbose: true,
  strict: true,
});
