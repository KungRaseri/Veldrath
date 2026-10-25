# Party Play (Milestone C)

> **Position in the roadmap:** D → **C** → A → B.
> C runs directly on top of Milestone D: a party is a group of characters fighting the *same* persistent
> enemy instances, so enemy and combat state must already live in Postgres ([`server-first-world-state.md`](server-first-world-state.md)).
> Guilds, trading and duels stay out of this milestone; they extend the same storage layer afterwards.
> **Tracker:** epic #37 — C1 #43 (schema + hub commands, shipped in `c7133ac6` and closed), C2 #40 (invites,
> snapshot fan-out, party panel — shipped server `99ebe3a4`/`07ac81b7`, client in the same session, and closed),
> C3 #41 (shared combat + contribution rewards), C4 #42 (party chat).

---

## 1. What exists today (verified)

| Thing | State | Consequence |
|---|---|---|
| [`Party`](../RealmEngine.Shared/Models/Party.cs:6) / [`PartyMember`](../RealmEngine.Shared/Models/PartyMember.cs:6) | Exist, but `PartyMember` is documented as *"an NPC recruited as a party member"* | This is the **single-player** NPC party, not multiplayer grouping |
| [`PartyService`](../RealmEngine.Core/Features/Party/Services/PartyService.cs), [`PartyAIService`](../RealmEngine.Core/Features/Party/Services/PartyAIService.cs), `RecruitNPCCommand`, `PartyCombatTurnCommand` | Depend on `ISaveGameService` (local save file) | Unusable for two remote players |
| Server party state, invites, hub methods, REST | None | Nothing to extend — this is greenfield |
| Multi-participant enemies | `SpawnedEnemy.Participants`, `DamageContributions`, `LastAttackerId` already track several characters on one enemy | **The hard part already exists**; rewards can be split by contribution |
| Chat | zone / global / whisper channels in [`session.svelte.ts`](../web/client/src/lib/game/session.svelte.ts:438) | A `party` channel is a small addition |
| UI | No party panel, no invite surface | New overlay work in `GameOverlays.svelte` |

**Key decision:** do not try to repurpose the single-player `Party`/`PartyService`. Keep it as the
save-game NPC party (RealmForge and single-player consumers use it) and add a separate server-side
party aggregate. Sharing the model would tie a multiplayer invariant to a save file.

## 2. Target design

### 2.1 Schema (persisted, like everything else)

```text
Parties
  Id                 uuid PK
  LeaderCharacterId  uuid FK Characters
  CreatedAt          timestamptz
  LootRule           text  -- 'contribution' (default), later 'leader-assign'

PartyMembers
  PartyId            uuid FK Parties ON DELETE CASCADE
  CharacterId        uuid FK Characters
  JoinedAt           timestamptz
  PK (PartyId, CharacterId)
  UNIQUE (CharacterId)            -- one party per character, enforced by the DB

PartyInvites
  Id                 uuid PK
  PartyId            uuid FK Parties ON DELETE CASCADE
  InvitedCharacterId uuid FK Characters
  InvitedByCharacterId uuid FK Characters
  ExpiresAt          timestamptz  -- default +5 min
  UNIQUE (PartyId, InvitedCharacterId)
```

`UNIQUE (CharacterId)` on `PartyMembers` is the whole "a character is in at most one party" invariant,
enforced by the database instead of by code — the same pattern D uses for claims.

### 2.2 Hub surface

| Method | Behaviour |
|---|---|
| `CreateParty` | Leader becomes the only member; fails if already in a party |
| `InviteToParty(characterName)` | Leader only; refused when the invitee is already grouped, offline, or in combat; creates an invite row + `PartyInvited` event to the invitee |
| `AcceptPartyInvite(partyId)` / `DeclinePartyInvite(partyId)` | Validates the invite has not expired; enforces the party size cap |
| `LeaveParty` | Leader leaving promotes the oldest member; the last member leaving deletes the party |
| `KickPartyMember(characterId)` | Leader only |
| `GetParty` | Returns members with level, HP/max HP, zone, and online state |

Events: `PartyInvited`, `PartyUpdated` (full snapshot, idempotent), `PartyDisbanded`, `PartyMemberLeft`.

**Snapshot-first rule:** every mutation ends by broadcasting the complete `PartyUpdated` snapshot. Clients
never assemble party state from deltas, which is what makes reconnects and the D4 resume path trivial.

### 2.3 Shared combat and rewards

- On `EngageEnemy`, if the engaging character is in a party, every online party member standing at the
  same `(ZoneId, LocationSlug)` is added to the enemy's participants (D makes this a DB write).
- A party member who walks away is removed from participation but keeps their recorded damage.
- On kill: XP and gold are split by `DamageContributions` share (already tracked), rounded so the sum equals
  the total. Quest `kill:` progress is advanced for every participant, not just the killer.
- Loot rule `contribution` for now: the item drop goes to the highest contributor; `leader-assign` is a
  later refinement behind the same column so no schema change is needed.
- **Failsafe:** if the enemy row is missing when a member joins (content deleted mid-fight), the join fails
  loudly with a client message and an `Error` log naming the enemy id — never a silent no-op.

### 2.4 Party chat

`sendChat` gains a `party` channel. The server routes to the party's SignalR group; a character with no
party gets a client-visible error rather than a message into the void. Moderation keeps working: party
messages are logged like zone messages.

## 3. Phases

| Phase | Work | Done when |
|---|---|---|
| **C1** | Schema + migration for `Parties`, `PartyMembers`, `PartyInvites`; `PartiesHubCommands` (create/leave/kick/get); group naming `party:{partyId}` | Two characters can form and dissolve a party via hub calls; the DB unique constraint refuses double membership |
| **C2** | Invite flow + `PartyUpdated` snapshots + party panel and invite toast in the client | Player A invites B by name; B gets a toast, accepts, and both see the same member list with HP |
| **C3** | Shared combat participation, contribution-based XP/gold split, quest `kill:` progress for every participant | Two players kill one enemy: both appear as participants, both receive a share, both advance the quest |
| **C4** | Party chat channel + moderation logging | Party messages reach only the party; leaving the party removes access immediately |

## 4. Acceptance criteria

1. **Restart-safe:** form a party, restart the server (rolling deploy), reconnect both clients — the party is
   intact and correctly rendered from `GetParty`.
2. **One party per character:** a second party cannot claim a character already in one; the refusal is a
   client-visible message, not a silent failure.
3. **Contribution split:** totals match; a member who contributed nothing gets nothing beyond quest progress.
4. **Disconnect mid-fight:** the D4 grace window applies per member; a member whose grace expires stops being
   a participant without breaking the fight for the others.
5. **Failsafe:** delete the enemy row mid-fight and confirm the join/disengage paths log an `Error` and tell
   the client, instead of hanging.

## 5. Explicitly deferred

- Guilds (rewritten #4 body lives in [`github-issue-refresh.md`](github-issue-refresh.md)).
- Trading (#6) and PvP/duels (#5) — both extend `PartyMembers`-style persistence and land as their own epics.
- Leader-assign loot, need/greed rolls, master looter.
- Cross-instance SignalR backplane (still out of scope; party state is in the DB, delivery is single-instance).
