---
kind: work-item
date: 2026-10-25
area: engine
title: "C2 server half: invite flow"
---

### Session-80 (2026-10-25) — C2 server half: invite flow

**Commands** (`Features/Party/PartyInviteCommands.cs`): `InviteToPartyHubCommand(Inviter, TargetName)`,
`AcceptPartyInviteHubCommand(Character, Party)`, `DeclinePartyInviteHubCommand`. Refusals each name a reason —
unknown name, offline (checked through `IPlayerSessionRepository`), self-invite, already grouped, in combat (through
`ICombatSessionStore`), party full. `PartyOptions` (section `Party`, `ValidateOnStart`) supplies `MaxSize` (5) and
`InviteExpiryMinutes` (5).

**Store additions:** `GetMemberCountAsync`, `AddMemberAsync` (unique-index violation → "already in a party"),
`CreateOrRefreshInviteAsync`, `GetValidInviteAsync`, `GetPendingInvitesAsync`, `DeleteInviteAsync`,
`DeleteInvitesForCharacterAsync`, `PurgeExpiredInvitesAsync`. `GetParty` now purges expired invites and returns
`PartyCommandResult.PendingInvites`, so an invite sent to an offline character still appears at login.

**Hub:** `InviteToParty(string)`, `AcceptPartyInvite(Guid)`, `DeclinePartyInvite(Guid)`; accept joins
`party:{id}` and broadcasts `PartyUpdated` to the group. Invites are **not pushed as payloads** — the invitee gets
`PartyInvitesChanged` on their connection and re-reads through `GetParty`, so offline/reconnect follows one path.
`InviteToParty` returns `InvitedCharacterId` so the hub can find that connection.

**Two traps, both caught by tests:**
1. `DateTimeOffset.UtcNow` inside a LINQ predicate is **not translatable** once `DateTimeOffset` columns go through the
   SQLite string converter — capture `var now = DateTimeOffset.UtcNow;` first. This broke every `GetParty` call.
2. Seeding `PlayerSession` rows in tests to fake presence hit an SQLite FK failure; a
   `FakePlayerSessionRepository` (presence-only, everything else throws) is deterministic and faster. Prefer it for any
   future "is the character online" test.

Also: a local helper named `Options(...)` shadows `Microsoft.Extensions.Options.Options` — name test helpers
`PartyOptionsFor(...)` instead.

**Tests:** `PartyInviteTests` (11). Server 743 → **754**, full solution **3,899**, format clean. Client half of C2
(handlers + party panel + invite toast) still to do.

---
