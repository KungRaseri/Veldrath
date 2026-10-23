/**
 * @your-scope/server — Colyseus server boot.
 *
 * Boot flow:
 *   1. Load `server/.env` via dotenv (existing env vars win).
 *   2. Construct the Colyseus `Server` on a WebSocket transport.
 *   3. Register the placeholder `GameRoom` with the matchmaker.
 *   4. `listen()` on PORT/HOST (defaults: 3000 / all interfaces).
 *
 * Persistence (Drizzle/pg) is intentionally NOT wired here — the server must
 * boot without a live Postgres. DB/auth stay lazy/optional: `onAuth` runs per
 * connection only and never touches the database at startup.
 *
 * The Docker entrypoint (`server/docker-entrypoint.sh`) runs Drizzle migrations
 * (root `migrate.mjs`) before this process boots.
 */
import { config as loadEnv } from "dotenv";
import { fileURLToPath } from "node:url";
import { Server } from "@colyseus/core";
import { WebSocketTransport } from "@colyseus/ws-transport";
import { GameRoom } from "./rooms/game-room.js";

// Module-relative `server/.env` path — independent of the invoking CWD.
loadEnv({ path: fileURLToPath(new URL("../.env", import.meta.url)) });

const PORT = Number(process.env.PORT ?? 3000);
// Default to all interfaces (Node listens on `::`) when HOST is unset.
const HOST = process.env.HOST?.trim() || undefined;

async function main() {
  const gameServer = new Server({
    transport: new WebSocketTransport(),
  });

  gameServer.define("game", GameRoom);

  await gameServer.listen(PORT, HOST);
  console.log(`[server] Colyseus listening on ${HOST ?? "all interfaces"}:${PORT}`);

  const shutdown = (signal: string) => {
    console.log(`[server] received ${signal}, shutting down…`);
    // Closes the transport (releasing the port) and exits the process.
    void gameServer.gracefullyShutdown(true);
  };

  process.once("SIGINT", () => shutdown("SIGINT"));
  process.once("SIGTERM", () => shutdown("SIGTERM"));
}

main().catch((error) => {
  console.error("[server] failed to boot:", error);
  process.exit(1);
});
