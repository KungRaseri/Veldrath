/**
 * @veldrath/shared — shared Drizzle schema (single source of truth).
 *
 * The SvelteKit server builds its Drizzle instance from THIS schema for Better
 * Auth tables only. Game data remains owned by Veldrath.Server and EF Core.
 *
 * Driver-agnostic `drizzle-orm/pg-core` definitions — each workspace wires its
 * own driver (`postgres` in the client, `pg` in the server) around the same
 * schema object.
 *
 * Add your own tables in this file (or a dedicated module) and re-export them.
 */
export * from "./auth.schema.js";
