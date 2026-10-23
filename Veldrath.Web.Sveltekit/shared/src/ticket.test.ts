import { createHmac } from "node:crypto";
import { describe, expect, it } from "vitest";
import { signTicket, verifyTicket } from "./ticket.js";

const SECRET = "test-secret-with-enough-entropy-1234567890";

/** Build a ticket whose payload decodes to the given JSON text. */
function ticketForRawPayload(raw: string, secret: string): string {
  const encoded = Buffer.from(raw, "utf8").toString("base64url");
  const signature = createHmac("sha256", secret).update(encoded).digest("base64url");
  return `${encoded}.${signature}`;
}

describe("ticket", () => {
  it("round-trips a payload through sign + verify", () => {
    const payload = { userId: "u_123", iat: Date.now() };
    const ticket = signTicket(payload, SECRET);

    expect(verifyTicket(ticket, SECRET)).toEqual(payload);
  });

  it("accepts optional fields (exp not reached)", () => {
    const payload = { userId: "u_123", iat: Date.now(), exp: Date.now() + 60_000 };
    const ticket = signTicket(payload, SECRET);

    expect(verifyTicket(ticket, SECRET)).toEqual(payload);
  });

  it("rejects a ticket signed with a different secret", () => {
    const ticket = signTicket({ userId: "u_123" }, SECRET);

    expect(verifyTicket(ticket, "another-secret")).toBeNull();
  });

  it("rejects a tampered payload", () => {
    const ticket = signTicket({ userId: "u_123" }, SECRET);
    const [encoded] = ticket.split(".");
    const tamperedPayload = encoded!.slice(0, -1) + (encoded!.endsWith("A") ? "B" : "A");
    const tampered = `${tamperedPayload}.${ticket.split(".")[1]}`;

    expect(verifyTicket(tampered, SECRET)).toBeNull();
  });

  it("rejects malformed tickets", () => {
    expect(verifyTicket("", SECRET)).toBeNull();
    expect(verifyTicket("no-separator-here", SECRET)).toBeNull();
    expect(verifyTicket("abc.", SECRET)).toBeNull();
    expect(verifyTicket(".abc", SECRET)).toBeNull();
  });

  it("rejects expired tickets", () => {
    const ticket = signTicket({ userId: "u_123", exp: Date.now() - 1000 }, SECRET);

    expect(verifyTicket(ticket, SECRET)).toBeNull();
  });

  it("rejects non-object payloads", () => {
    const nullTicket = ticketForRawPayload("null", SECRET);
    const stringTicket = ticketForRawPayload('"hello"', SECRET);

    expect(verifyTicket(nullTicket, SECRET)).toBeNull();
    expect(verifyTicket(stringTicket, SECRET)).toBeNull();
  });
});
