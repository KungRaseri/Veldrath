/**
 * @veldrath/shared/db — shared Drizzle schema entrypoint.
 *
 * Re-exports every table definition. The SvelteKit server imports this (rather
 * than the package root) so shared protocol types stay free of Drizzle
 * dependencies.
 */
export * from "./schema.js";
