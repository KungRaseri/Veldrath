/**
 * @your-scope/shared/schema — Colyseus room schemas.
 *
 * Colyseus Schema classes for the real-time rooms. These are **state-only**
 * projections (per Colyseus best practices: schema classes hold field
 * definitions, no game logic).
 *
 * This subpath is the ONLY place Colyseus schemas live — the main
 * `@your-scope/shared` entry stays framework-free (see AGENTS.md conventions).
 */
export * from "./room-state.js";
