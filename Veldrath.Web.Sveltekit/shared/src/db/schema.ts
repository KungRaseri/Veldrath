/**
 * @your-scope/shared — shared Drizzle schema (single source of truth).
 *
 * Both the client and the server build their Drizzle instances from THIS schema
 * so table definitions never drift between workspaces:
 *
 * - client: auth/session tables only (Better Auth needs them at runtime).
 * - server: authoritative persistence (owns validation + app data).
 *
 * Driver-agnostic `drizzle-orm/pg-core` definitions — each workspace wires its
 * own driver (`postgres` in the client, `pg` in the server) around the same
 * schema object.
 *
 * Add your own tables in this file (or a dedicated module) and re-export them.
 */
export * from "./auth.schema.js";
