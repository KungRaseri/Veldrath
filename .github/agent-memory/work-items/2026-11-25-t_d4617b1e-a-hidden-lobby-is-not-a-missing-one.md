---
kind: work-item
date: 2026-11-25
area: engine
card: t_d4617b1e
title: "A hidden safe location is the zone's lobby, not a missing one — the sweep wake and the respawn now ask the same resolver"
---

### Session t_d4617b1e (2026-11-25) — A hidden lobby is a lobby, not an absent one

Follow-up to [`2026-11-25-t_025183f2-a-swept-run-wakes-in-the-lobby.md`](2026-11-25-t_025183f2-a-swept-run-wakes-in-the-lobby.md),
filed by that card's review. Its last paragraph — "a hidden *lobby* would therefore not be found, which is the fallback
that keeps the saved node rather than an error" — was the defect, not a graceful degradation: the character kept a node
in the shared scope, with no run behind it, and was told nothing. `content/zonelocations/ashlen-wood.yaml` already
authors exactly that shape (`strangled-oak`, `safeRespawn: true`, `isHidden: true`), so one `isInstanced` flag away it
would have bitten.

**What changed**

- New `Veldrath.Server/Features/Zones/ZoneLobbyResolver.cs`: `IZoneLobbyResolver.ResolveAsync(zoneId, characterId)`
  returns `ZoneLobbyStatus` (`Resolved`, `NotUnlocked`, `NoSafeLocation`, `NoLocations`) plus the lobby, the hidden
  lobby that is closed to the character, and the first visible location as the entrance fallback. It reads
  `IZoneLocationRepository.GetByZoneIdAsync(zoneId, unlockedSlugs)` with the character's unlocks from
  `ICharacterUnlockedLocationRepository`, and only when no visible safe location exists does it read
  `GetHiddenByZoneIdAsync` to tell "the zone has none" apart from "the zone has one this character has not opened".
  Registered scoped in `Program.cs` next to `IZoneInstanceService`.
- `GameHub.ResolveSweptInstanceLobbyAsync` now returns a private `SweptRunWake(NodeId, Slug, Message)` instead of a
  tuple: resolved → move and send `ExpiredInstanceMessage`; `NotUnlocked` → keep the saved node and send the new
  `LockedLobbyMessage`; `NoSafeLocation`/`NoLocations` → keep the saved node and stay silent (that decision is the
  parent card's, and its test pins it) but log an `Error`. The relocation that worked before now also gets an
  `Information` line, which the parent review had listed as missing.
- `RespawnHubCommandHandler` lost its `IZoneLocationRepository` and takes `IZoneLobbyResolver`. `NotUnlocked` is a
  distinct outcome now: `Error` naming the hidden slug, fall back to the first visible location, and tell the player
  the haven "is not yet open to you" rather than "this zone has no safe respawn point".
- Deliberately not folded in: `EnterZoneLocationHubCommand.ResolveEntranceNameAsync` and
  `EnterDungeonHubCommand.ResolveEntranceAsync` still pick `IsSafeRespawn` themselves. They name an entrance in a
  message or pick one to walk to; they do not place a character who is already inside the zone, and neither was the
  silent failure this card exists to end. They remain the only two copies of the rule.

**Tests** — ten new ones, no existing assertion weakened. `ZoneLobbyResolverTests` (6) pins Resolved, hidden+unlocked,
hidden+locked (with the fallback), *another character's unlock does not count*, NoSafeLocation and NoLocations.
`GameHubZoneTests` gains `..._Wakes_In_A_Hidden_Lobby_The_Character_Unlocked` (node + `ZoneEntered.CurrentNodeId` +
the shared-zone message) and `..._Keeps_The_Saved_Node_When_The_Lobby_Is_Hidden_And_Not_Unlocked` (saved node kept, one
`LockedLobbyMessage`). `RespawnHubCommandHandlerTests` gains
`Handle_Moves_To_A_Hidden_Safe_Location_The_Character_Unlocked` and
`Handle_Falls_Back_When_The_Safe_Location_Is_Hidden_And_Not_Unlocked`. Both test classes now drive the catalogue through
`Infrastructure/FakeZoneLocationRepository`, which applies the real repository's active + hidden-unless-unlocked rules
instead of a per-test mock that would have glossed over the bug.

**Negative controls, one at a time on the committed tree** (27 focused tests, 2035 in the project):

- Resolver ignores the unlocks (`Array.Empty<string>()`): 3 failed / 24 passed — exactly the three hidden-and-unlocked
  cases (resolver, hub, respawn).
- Resolver skips the hidden-lobby diagnosis: 4 failed / 23 passed — exactly the four locked cases (2 resolver, hub,
  respawn).
- Hub does not send the `SystemMessage`: 4 failed / 4 passed in `EnterZone_Reconnect` — precisely the four cases that
  owe the player a sentence; the four silent ones stay green.
- Respawn handler never falls back to the entrance for a locked lobby: exactly
  `Handle_Falls_Back_When_The_Safe_Location_Is_Hidden_And_Not_Unlocked` failed.

**Gates** — `dotnet build Veldrath.Server.slnx` (0 warnings, 0 errors), `dotnet test Veldrath.Server.Tests`
(2035 passed, 0 failed), `dotnet format Veldrath.Server.slnx --verify-no-changes` (exit 0). No web file touched: the
placement is server-persisted and arrives with `MapGraphReceived`.

**Correction to the parent note.** It says the client "already renders `ZoneEntered`'s `CurrentNodeId`" at
`session.svelte.ts:774`. It does not: the `ZoneEntered` handler (`:783-787`) stores the zone name, occupants and one
"Entered …" line, and nothing under `web/client/src` reads `ZoneEnteredPayload.currentNodeId`. The real path is the
persisted row: `handleCharacterSelected` awaits `enterZone()` (`:1443`) *before* `requestMapGraph()` (`:1450`), and the
`MapGraphReceived` handler (`:797-799`) places the character on `payload.currentNodeId`. Same conclusion (no client
change needed), and this card adds no client coupling — but the parent note names the wrong mechanism; not edited,
per the one-card-one-note rule.
