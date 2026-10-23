/**
 * @your-scope/shared/schema — placeholder room state schema.
 *
 * State-only projection for the `GameRoom` (Colyseus best practices: schema
 * classes hold field definitions, no game logic). Extend with your own fields
 * as game features land.
 */
import { Schema, type } from "@colyseus/schema";

export class GameRoomState extends Schema {
  /** Lifecycle status of the room (e.g. "waiting", "playing", "finished"). */
  @type("string")
  status = "waiting";
}
