import { defineConfig } from "drizzle-kit";
import { config as loadEnv } from "dotenv";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = fileURLToPath(new URL(".", import.meta.url));

// DATABASE_URL lives in client/.env — the SvelteKit auth-side connection.
// drizzle-kit runs from THIS workspace (shared/), so load client/.env to keep
// `npm run db:*` (root) working without exporting DATABASE_URL. dotenv never
// overrides existing env vars and stays quiet when a file is absent (e.g. CI) —
// DATABASE_URL can still come from the shell.
loadEnv({ path: resolve(here, "../client/.env"), quiet: true });

if (!process.env.DATABASE_URL)
  throw new Error("DATABASE_URL is not set (expected in client/.env or the environment)");

export default defineConfig({
  schema: "./src/db/schema.ts",
  out: "./db/migrations",
  dialect: "postgresql",
  dbCredentials: { url: process.env.DATABASE_URL },
  verbose: true,
  strict: true,
});
