/**
 * @your-scope/shared/ticket — stateless HMAC-signed WS auth tickets.
 *
 * Minimal helper used by the server's room `onAuth` (and, on the client, to
 * mint tickets for joining rooms). A ticket is a base64url-encoded JSON
 * payload signed with HMAC-SHA256 over `WS_AUTH_SECRET` (the value shared
 * between `client/.env` and `server/.env`) using Node `crypto`.
 *
 * Node-stdlib only — no framework imports — so this stays usable from the
 * SvelteKit server and keeps the `@your-scope/shared` main entry
 * framework-free (this module lives behind its own subpath export).
 */
import { createHmac, timingSafeEqual } from "node:crypto";

export interface TicketPayload {
  /** The authenticated user id. */
  userId: string;
  /** Issued-at timestamp (ms since epoch). */
  iat?: number;
  /** Expiry timestamp (ms since epoch); expired tickets are rejected. */
  exp?: number;
}

const SEPARATOR = ".";

/** HMAC-SHA256 signature (base64url) of the given payload string. */
function sign(payload: string, secret: string): string {
  return createHmac("sha256", secret).update(payload).digest("base64url");
}

/** Sign a ticket payload into a `payload.signature` string. */
export function signTicket(payload: TicketPayload, secret: string): string {
  const encoded = Buffer.from(JSON.stringify(payload), "utf8").toString("base64url");
  return `${encoded}${SEPARATOR}${sign(encoded, secret)}`;
}

/**
 * Verify a ticket against the shared secret. Returns the decoded payload on
 * success, or `null` when the ticket is malformed, tampered, or expired.
 */
export function verifyTicket(ticket: string, secret: string): TicketPayload | null {
  const separatorIndex = ticket.lastIndexOf(SEPARATOR);
  if (separatorIndex <= 0) return null;

  const encoded = ticket.slice(0, separatorIndex);
  const signature = ticket.slice(separatorIndex + 1);

  const expected = Buffer.from(sign(encoded, secret), "base64url");
  const received = Buffer.from(signature, "base64url");
  if (expected.length !== received.length || !timingSafeEqual(expected, received)) {
    return null;
  }

  try {
    const payload: unknown = JSON.parse(Buffer.from(encoded, "base64url").toString("utf8"));
    if (payload === null || typeof payload !== "object") return null;

    const ticketPayload = payload as TicketPayload;
    if (ticketPayload.exp !== undefined && Date.now() > ticketPayload.exp) return null;

    return ticketPayload;
  } catch {
    return null;
  }
}
