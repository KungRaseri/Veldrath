/**
 * @your-scope/shared/db — shared Drizzle schema entrypoint.
 *
 * Re-exports every table definition. Workspaces import this (rather than the
 * package root) so the shared protocol types stay free of Drizzle dependencies.
 */
export * from "./schema.js";
