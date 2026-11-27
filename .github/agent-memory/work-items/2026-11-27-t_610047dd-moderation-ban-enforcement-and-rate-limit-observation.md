---
kind: work-item
date: 2026-11-27
area: auth
card: t_610047dd
title: Moderation enforcement — five mute paths, live-session bans, rate-limit observation
---

# Moderation enforcement and rate-limit observation (PR4 of the security epic)

## What shipped

- **F6 — account mute on every direct sender.** `GameHub.SendZoneMessage` and `SendPartyMessage` never consulted the
  persisted account mute; `SendGlobalMessage`, `SendWhisper` and `SendChatMessage` each carried their own copy. The rule
  now lives once in `GameHub.RefuseIfMutedAsync` (active `IsMuted`, no expiry or a future `MutedUntil`), called from all
  five senders after their character/zone prerequisites and before routing, party lookup, target lookup or slash-command
  dispatch. Caller-only `SystemMessage`; nothing is broadcast. No guild or party-specific policy was added.
- **F5 — observation, not enforcement.** `RateLimit:Observe` (off by default) turns on `RateLimitObservation`
  (`Infrastructure/RateLimitObservation.cs`), `RateLimitObservationMiddleware` in front of `UseRateLimiter`, the
  limiter's `OnRejected` hook, and `Hubs/RateLimitObservationFilter` on `GameHub`. It counts 429s by policy and route
  template (hub negotiate split from transport), eligible HTTP requests per authenticated account, and hub invocations
  per account and handler. The effective partition is the policy name. Account ids never leave the process: the snapshot
  carries distinct counts and a peak share only. Permit, window, policy assignment and rejection behaviour are untouched.
- **F4 — bans reach established sessions and bearer validation.** `AccountAccessSnapshot.IsActivelyBanned` is the one
  expiry rule. `IAccountAccessReader` (declared in `AccountProvisioningService.cs`) is implemented by
  `AccountProvisioningService`, and `EvictAsync` takes the same per-account fill gate `EnsureAccountAsync` holds, so an
  in-flight fill cannot reinsert a pre-ban snapshot. `JwtClaimsEnrichment` now reads the snapshot for **both** token
  shapes (a roles-carrying token is still a live credential) and `context.Fail`s an active ban with a generic message →
  401. `BanGuardFilter` refuses the next hub invocation for an active ban, sends the existing `OnKicked` shape, aborts
  the caller and throws `HubException`; a failed or missing lookup is also a refusal. REST ban/unban/warning auto-ban
  and hub `/warn` (auto-ban), `/ban`, `/suspend` evict only after `IdentityResult.Succeeded`.
- The browser bearer is withheld on refusal: `/api/game-token` preflights `GET /api/account/profile` with the internal
  server-side bearer and returns 403 (upstream 401/403) or 503 (unreachable/other) instead of a token.

## Source-derived answer for the PR body (F5)

There is no `VeldrathModule.cs`, no literal `"global"` partition and no site-wide bucket. `Program.cs` registers three
named fixed-window policies — `foundry-writes`, `admin-actions`, `hub-commands` — each one shared bucket per policy per
process. `hub-commands` is HTTP middleware on the SignalR endpoints: it meters negotiate and transport requests,
including a fallback transport, and does **not** meter individual hub invocations, which travel inside the upgraded
WebSocket frame. That distinction is why invocation counting is a hub filter and not a policy.

## Evidence gap (recorded, not invented)

No player-traffic measurement ships with this card. The work is source-verified only and no passive collection was
authorised, so the PR records: measurement is installed and off by default; a normal week including a weekend peak is
the target window; shared-egress share is **unknown** (no approved dataset with verified client-address provenance —
the forwarded-header setup at `Program.cs:329-339` makes a proxy address not a client egress). Fixture counts in the
tests verify instrumentation, not player behaviour. PR7 (`t_66e06bcf`) owns any limit choice and stays blocked until
PR4 ships and data exists.

## Verification actually run

- `dotnet build Veldrath.Server.slnx` — 0 warnings, 0 errors.
- `dotnet test Veldrath.Server.slnx` — 2135 server tests passed (2748 engine, 697 data).
- `dotnet format Veldrath.Server.slnx --verify-no-changes` — exit 0.
- `cd web && npm run typecheck` (0/0), `npm run lint`, `npm run test:unit --workspace client -- --run`
  (108 files, 1630 tests), `npm run build`.
- Negative controls, each reverted with the tree clean:
  - `RefuseIfMutedAsync` short-circuited → all five mute denial tests fail (7 total including the pre-existing three).
  - REST ban without `EvictAsync` → `Ban_RejectsTheNextBearerRequestWithAWarmCache` returns 200, not 401.
  - `JwtClaimsEnrichment` ban branch disabled → both `EnrichAsync_RejectsAnActiveBan...` cases and the warm-cache
    integration test fail.
  - `BanGuardFilter` ban branch disabled → `Invocation_RefusesAnAccountBannedAfterConnect` runs the handler.
  - `/api/game-token` preflight disabled → both new web denial tests fail.

## Notes for later work

- Scope additions beyond the declared 16 paths, all supporting changes: `Veldrath.Server.Tests/Features/GameHubTestHarness.cs`
  (optional `IAccountAccessReader` so hub writers can be asserted) and
  `Veldrath.Server.Tests/Features/Auth/AccountProvisioningServiceTests.cs` (its "a roles-carrying token skips the
  database" premise is exactly what F4 changes). Both are covered by the note above.
- The guarantee is per server process: eviction makes the next call see a ban immediately; it does not close an idle
  socket, cancel in-flight work, or propagate to a replica faster than the snapshot TTL.
- `GameHub.OnConnectedAsync` still refuses new connections for an active ban; it was left in place.
