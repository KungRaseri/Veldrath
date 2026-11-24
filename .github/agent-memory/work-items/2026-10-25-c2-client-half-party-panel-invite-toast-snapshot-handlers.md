---
kind: work-item
date: 2026-10-25
area: engine
title: "C2 client half: party panel, invite toast, snapshot handlers"
---

### Session-81 (2026-10-25) — C2 client half: party panel, invite toast, snapshot handlers

**Wire shape stays snapshot-first.** `PartyUpdated` carries a whole `PartySnapshot` (or `null` for solo);
`PartyDisbanded` sends `{ partyId }`; `PartyInvitesChanged` sends `{ partyId: string | null }` — a value means *that*
invite is gone, `null` means "an invite arrived, re-read". The hub never pushes invite data, so login, reconnect and a
live nudge all take the same `GetParty` path.

**Client (`web/client/src/lib/game/`):**
- `types.ts`: `PartyMemberView`, `PartySnapshot`, `PartyInviteView`, `PartyDisbandedPayload`,
  `PartyInvitesChangedPayload`.
- `session.svelte.ts`: `party`, `partyInvites`, `showParty` state; `isPartyLeader` getter; `requestParty`,
  `openParty`, `createParty`, `leaveParty`, `kickPartyMember`, `inviteToParty`, `acceptPartyInvite`,
  `declinePartyInvite`; handlers that reduce the snapshot and announce joins/leaves; `handleCharacterSelected` now
  calls `requestParty()` **before** `openInventory()`.
- `GameOverlays.svelte`: party dialog (member list with HP, leader chip, leader-only Remove, invite-by-name box,
  leave/disband) plus a fixed bottom-right invite toast with Accept/Decline.
- `LeftSidebar.svelte`: **Party** entry in the character menu, badged with the invite count or party size.

**Two gotchas worth remembering:**
1. `payload-contract.test.ts` only sees *anonymous* payloads. `SendAsync("PartyUpdated", result.Party)` and
   `SendAsync("PartyUpdated", null)` are skipped, so the snapshot needs no `<Event>Payload` interface — but
   `PartyDisbanded`/`PartyInvitesChanged` do. Add any new mirrored interface to the third test's `expected` list,
   otherwise the contract check silently stops covering it.
2. `handleCharacterSelected` grows a hub call whenever login needs more state. Every test that asserts an exact
   `hub.methods()`/`hub.invocations` list after `CharacterSelected` has to be updated (two in `session.test.ts`),
   and `components.test.ts`'s `fakeSession` needs a stub for each new session field or the components throw
   `Cannot read properties of undefined`.

**Verification:** `npm run typecheck` 0 errors, `npm run lint` clean, Vitest **175 passed** (session 90, components
21), Playwright 22 passed, `npm run build` clean. C3 (#41) is next.

---
