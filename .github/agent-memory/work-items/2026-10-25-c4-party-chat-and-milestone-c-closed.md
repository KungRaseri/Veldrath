---
kind: work-item
date: 2026-10-25
area: content
title: "C4: party chat, and Milestone C closed"
---

### Session-83 (2026-10-25) — C4: party chat, and Milestone C closed

**Server.** `GameHub.SendPartyMessage(SendZoneChatMessageHubRequest)` reuses the zone-chat request record, reads the
party id through the existing `GetPartyHubQuery` (so the hub needed no new store dependency and **no ctor change** —
which is why no other hub test needed touching), and sends `ReceiveChatMessage` with `Channel = "Party"` to
`party:{partyId}` only. A character with no party gets `Error` "You are not in a party." instead of a message into
the void. Because it reuses the `ReceiveChatMessage` event, logging/moderation tooling sees party traffic like any
other channel.

**Client.** `ChatChannel` gained `'Party'`; `sendChat` routes it to `SendPartyMessage`; `RightSidebar` derives its
channel list from `session.party`, so the Party tab only exists while the character is grouped, and
`GameSession.endPartyChat()` drops every `Party` message and falls back to `Zone` when the party becomes null or
disbands. Party chat is **live-only** — the UI must never imply a backlog the server does not keep. The tab/history
half landed in the follow-up commit `95b3cd37` (Vitest 177).

**Tests:** 2 hub tests (solo refusal asserts the exact error and that no group send happened; in-party asserts a
single `ReceiveChatMessage` with the trimmed message and nothing sent to the caller) + 1 Vitest for the routing.
Server **769**, full solution **3,914**, web typecheck/lint clean, Vitest 176, Playwright 22 passed.

**Milestone C wrap-up.** All of #43/#40/#41/#42 are closed, so epic #37 is complete. Party state is in Postgres,
snapshots are the only wire shape, invites are re-read rather than pushed, combat is shared through the existing
enemy rows, and the reward split cannot lose a coin.

**Gap closure.** Both gaps named at wrap-up were then closed in the same session:
1. `PartyCommandTests.GetOnlinePartyMembersAtLocation_Returns_Only_Members_Online_At_That_Location` seeds a real
   World → Region → `PlayerSession` chain and proves the filter: three online members, only the one at the same zone
   **and** location slug comes back. Writing it exposed a test-harness trap — the `Character` instances returned by
   `SeedCharacterAsync` are detached by the time `PartyStore` has attached its own tracked copies, so `UpdateRange`
   throws *"another instance with the same key value for {'Id'} is already being tracked"* and in-memory mutations are
   silently never saved. Use `ExecuteUpdateAsync` to move the row, then read it back: the store reads with
   `AsNoTracking`, so the database is the only source of truth that matters.
2. `GameHubTests.AttackEnemy_Advances_The_Kill_Objective_For_Every_Participant_By_Slug_And_Name` mocks `ISender`, so it
   asserts the commands themselves — four `AdvanceQuestObjectivesHubCommand`s (slug and name for each participant).
   Note `FakeHubCallerClients.Client(id)` returns a **no-op** proxy, so outbound per-connection sends cannot be observed
   from `GameHubTests`; assert the command fan-out (or extend the fake) rather than reading `SentMessages`.

Server **771**, full solution **3,916**, format clean.

---
