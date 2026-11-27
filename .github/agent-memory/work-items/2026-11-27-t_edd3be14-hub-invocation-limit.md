---
kind: work-item
date: 2026-11-27
area: auth
card: t_edd3be14
title: Hub invocations are budgeted per account and per channel
---

# Hub invocations: the filter counted them, nothing limited them

## The finding

`hub-commands` is HTTP middleware on the SignalR endpoints. It meters negotiate and the transport request,
including a long-polling fallback — two permits per connection — and cannot see the invocations that travel
inside the upgraded WebSocket frame. `Hubs/RateLimitObservationFilter` was the only place an individual
invocation was observed, and it recorded the call (`RateLimitObservation.RecordInvocation`) and forwarded it.
A modded client could therefore call `SendChatMessage` (or any other hub method) as fast as the socket accepted
frames, with no throttling at all.

## What shipped

- `Hubs/HubInvocationRateLimiter` — one fixed-window budget per account **and channel**, keyed with
  `Infrastructure/RateLimitPartitionKey` (the same GUID `sub` / mapped `ClaimTypes.NameIdentifier` rule the
  named HTTP policies use). Channels follow the hub's naming contract rather than a list that can drift:
  `Send*` is chat (the slash-command entry point is `SendChatMessage`), `Get*` and `Ping` are reads, everything
  else is gameplay. A method added later lands in gameplay — the safe default — without editing the limiter.
- `Hubs/HubInvocationRateLimitFilter` — registered after the ban guard, charges each invocation and refuses an
  over-budget one with `HubException` ("You are sending commands too quickly…", with the seconds until the
  window resets when the limiter reports them). The connection is not aborted: the invocation promise rejects
  and the socket stays usable.
- `RateLimitPartitionKey.For(ClaimsPrincipal)` — the no-request overload the filter uses, so the account rule
  is stated once. A principal without a GUID subject falls back to the shared `address:unknown` key rather
  than a per-connection bucket, which would let a caller refresh its allowance by reconnecting; the hub's
  `[Authorize]` gate and the ban guard refuse such a caller before it can act.
- `RateLimitObservationFilter` is untouched and still registered first: it counts attempts when
  `RateLimit:Observe` is on and is a pure pass-through when it is off.

## The permit numbers (measured, not invented)

The epic forbids live probing and load tests against a running server, so the measurement came from a local
harness: one account, a real SignalR client over the in-memory test host, `RateLimit:Observe=true`, replaying
the web client's own sequence from `web/client/src/lib/game/session.svelte.ts` — `SelectCharacter`, `GetParty`,
`GetInventory`, `EnterZone`, `GetMapGraph`, `GetLocationRoster`, `GetGroundItems`, `GetLocationNodes`,
`GetLocationChests`, `GetLocationNpcs`, plus `GetChatCommands` on the first connect. The observation snapshot
recorded **11 invocations in one burst: 9 reads and 2 gameplay** (`SelectCharacter`, `EnterZone`). Chat was 0 —
the client only sends chat when a player types. `withAutomaticReconnect()` replays that burst through
`restoreSession()` on every reconnect, and one travel click adds 1 gameplay invocation and 3–4 reads (the
location's nodes, chests, people, and the map when it changed).

From that: chat 30/min (generous sustained typing, one line every two seconds), reads 300/min (~30 reconnects
or ~70 travels a minute), gameplay 120/min (~2 clicks a second against a human pace of about 1). A limit at
zero or below disables that channel. Config keys: `RateLimit:HubChatInvocationsPerMinute`,
`RateLimit:HubQueryInvocationsPerMinute`, `RateLimit:HubActionInvocationsPerMinute`. The harness observations
are repeated in the code comment next to `DefaultChatLimit`/`DefaultQueryLimit`/`DefaultActionLimit`.

## Verification actually run

- `dotnet build Veldrath.Server.slnx` — 0 warnings, 0 errors.
- `dotnet test Veldrath.Server.Tests --filter "HubInvocationRateLimit|RateLimitPartitionKey"` — 26 passed.
- `dotnet test Veldrath.Server.slnx` — 2169 server, 2748 engine, 697 data and 1158 shared tests passed, 0 failed.
- `dotnet format Veldrath.Server.slnx --verify-no-changes` — exit 0.
- `node scripts/memory/memory-index.mjs --check` — 126 work items, 40 areas valid.
- Negative control: with `options.AddFilter<HubInvocationRateLimitFilter>()` removed from `Program.cs`, the
  three integration tests fail with "Assert.Throws() Failure: No exception was thrown" — i.e. before the
  limiter existed an exhausted account was never refused. Restored afterwards.

## Notes for later work

- `Veldrath.Server.Tests` now references `Microsoft.AspNetCore.SignalR.Client` (version already pinned in
  `Directory.Packages.props` from the retired desktop client) so a hub filter can be exercised over the wire
  instead of through a hand-built `HubInvocationContext`.
- The buckets live for the process lifetime — three small limiters per account. Accounts are provisioned from
  Better Auth on first token use and are not self-created, so the map is bounded by the real player base.
- A refusal is per invocation; there is no backoff or strike count. If refusals turn out to be common in
  practice, the next step is measuring the refusal rate through `RateLimitObservation` (which counts the
  attempts) rather than tightening the numbers blind.
