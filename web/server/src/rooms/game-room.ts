/**
 * @your-scope/server — placeholder `GameRoom`.
 *
 * Minimal Colyseus room for the template boot. Transport/state-sync only: it
 * exposes the shared room-state schema and authenticates joiners against the
 * shared HMAC ticket helper. No game logic and no persistence (see AGENTS.md:
 * Colyseus is transport + state-sync only; the engine and DB stay out of the
 * room).
 */
import { Room, type AuthContext, type Client } from "@colyseus/core";
import { GameRoomState } from "@your-scope/shared/schema";
import { verifyTicket, type TicketPayload } from "@your-scope/shared/ticket";

export class GameRoom extends Room<{ state: GameRoomState }> {
  override maxClients = 4;

  override onCreate() {
    this.setState(new GameRoomState());
  }

  /**
   * Verify the HMAC-signed ticket. The ticket is accepted from the join
   * `options.ticket` or the request's `AuthContext.token` (Bearer). The
   * decoded payload becomes `client.auth`; a missing/invalid ticket rejects
   * the connection. Only runs per connection — never at boot.
   */
  override onAuth(_client: Client, options: unknown, context: AuthContext): TicketPayload | false {
    const raw =
      (typeof options === "object" && options !== null && "ticket" in options
        ? (options as { ticket?: unknown }).ticket
        : undefined) ?? context.token;

    if (typeof raw !== "string" || raw.length === 0) return false;

    return verifyTicket(raw, process.env.WS_AUTH_SECRET ?? "") ?? false;
  }

  override onJoin(client: Client) {
    console.log(`[GameRoom] ${client.sessionId} joined`);
  }

  override onLeave(client: Client) {
    console.log(`[GameRoom] ${client.sessionId} left`);
  }
}
