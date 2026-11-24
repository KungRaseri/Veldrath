---
kind: work-item
date: 2026-10-25
area: engine
pr: 37
title: "C1: party schema and hub commands (#37)"
---

### Session-79 (2026-10-25) — C1: party schema and hub commands (#37)

**Schema (migration `AddPartyTables`, applied to dev):** `Parties` (`Id`, `LeaderCharacterId` → Characters cascade,
`CreatedAt`, `LootRule` default `contribution`), `PartyMembers` (PK `(PartyId, CharacterId)`, **UNIQUE `CharacterId`** —
"one party per character" is a constraint), `PartyInvites` (`PartyId`+`InvitedCharacterId` unique, `ExpiresAt` indexed,
invitee cascade, inviter restrict). Entities are `Veldrath.Server.Data.Entities.Party`, `PartyMemberEntity` (name avoids
clashing with the save-game `PartyMember`) and `PartyInvite`.

**Command layer** (`Features/Party/`):
- `PartyStore` (scoped): `GetPartyForCharacterAsync`, `GetPartyAsync`, `CreateAsync`, `RemoveMemberAsync`,
  `DeleteAsync`, `PromoteLeaderAsync`, `ToSnapshotAsync`. It clears the change tracker after every
  `ExecuteUpdate`/`ExecuteDelete` — the same discipline D2/D4 established.
- `PartySnapshot` / `PartyMemberView` are the *only* shapes clients see; vitals are read from the character attribute
  blob, and a malformed blob yields `0/0` rather than throwing.
- Commands: `CreatePartyHubCommand`, `LeavePartyHubCommand`, `KickPartyMemberHubCommand`, `GetPartyHubQuery`. The
  result carries `PartyId` so the hub can move a connection between SignalR groups even when the caller ends up solo.
- Leader leaving promotes the longest-standing member; the last member leaving deletes the party; kicking yourself is
  refused with "leave the party instead".

**Hub surface:** `CreateParty`, `LeaveParty`, `KickPartyMember(Guid)`, `GetParty`, group `party:{partyId}`,
events `PartyUpdated` (full snapshot, or `null` when solo) and `PartyDisbanded`. `PartyUpdated` is snapshot-first —
clients never fold deltas.

**Bug the tests caught:** the first `GetPartyForCharacterAsync` applied `.Include` after a `.Select` projection, which
EF rejects outright. The lookup now starts from `Parties` with `Members.Any(...)`. Worth remembering: `Include` must
come before any projection that changes the entity type.

**Tests:** `PartyCommandTests` (10) — create makes the creator sole leader with real vitals, a second create is
refused without touching the DB, last-member leave disbands, leader leave promotes the veteran, solo leave refused,
kick removes and re-snapshots, non-leader kick refused, self-kick refused, solo read is a successful empty answer, and
a party read from a *fresh context* (the restart case) still comes back.

Server suite 733 → **743**; full solution **3,888**; format clean. Next: C2 (invites + party panel).
