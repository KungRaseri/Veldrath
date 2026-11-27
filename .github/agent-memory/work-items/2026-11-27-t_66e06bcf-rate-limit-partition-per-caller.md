---
kind: work-item
date: 2026-11-27
area: auth
card: t_66e06bcf
title: Named rate limits are partitioned per caller, not one bucket per process
---

# Rate-limit starvation: one account could spend every account's permits (PR7 of the security epic)

## The finding

The three named fixed-window policies — `foundry-writes`, `admin-actions`, `hub-commands` — were registered with
`AddFixedWindowLimiter`. That gives each policy **one bucket per process**: the partition was the policy name, so
nothing distinguished an account from the account next to it. One caller could spend the whole allowance and every
other caller was rejected with 429 until the window rolled.

The earlier work-item note (`2026-11-27-t_610047dd-...`) recorded the shared bucket as the source-level answer. This
note corrects it: shared was not an observation about traffic, it was the implementation, and it was the defect.

## Evidence

Production on 2026-11-27 had no traffic to size a limit from: 4 `AspNetUsers`, 3 characters, 0 player sessions,
0 combat sessions, and **0 HTTP 429s across 18,778 Traefik log lines**. The attached local harness ran the real
pipeline through `WebApplicationFactory` with limits pinned low (5 hub, 3 foundry):

| Policy | Account A | Account B, different address |
| --- | --- | --- |
| `hub-commands` (5) | five 200s, then 429 | **429 on the first request** |
| `foundry-writes` (3) | three 400s (each still spent a permit), then 429 | **429 on the first request** |

The regression run before the fix reproduced it through the real pipeline: account A's exhausted permits produced
429s for account B's first request on both policies.

## What changed

- `Program.cs` registers each policy with `AddPolicy` + `RateLimitPartition.GetFixedWindowLimiter`, partitioned by
  `Infrastructure.RateLimitPartitionKey`. Policy names and config keys are unchanged, so
  `RateLimitObservationMiddleware` and the `OnRejected` hook keep reporting.
- `RateLimitPartitionKey.For` keys an authenticated caller on the GUID subject (`sub` or the mapped
  `ClaimTypes.NameIdentifier`), and a request without a usable subject on the client address: the right-most
  `X-Forwarded-For` entry (what the proxy appended, the only unforgeable part) then the connection address. An
  IPv4-mapped IPv6 address is folded to IPv4 so one caller cannot get two buckets.
- Defaults are per account per minute: `hub-commands` 30 (was 120 process-wide), `foundry-writes` 20 (was 5),
  `admin-actions` 40 (was 20). These are capacity assumptions, not measurements — the code comment says so and
  cites the evidence. 30 hub permits covers one connection (2 permits: negotiate + upgrade) and a full automatic
  reconnect cycle (up to 8 more) several times over.
- `RateLimitObservation`'s remarks no longer claim a policy is one process-wide bucket; the snapshot still reports
  per policy and never per caller.

## Verification actually run

- Regression tests **before** the fix: 3 of 3 integration assertions failed — `foundry-writes` account B got 429
  after A's malformed writes; `hub-commands` account A got 429 on its first request because a preceding test had
  already drained the shared bucket; the same-account test passed (it does not depend on the defect).
- After the fix: 13/13 targeted tests pass — two accounts get separate buckets on both policies, one account keeps
  one bucket across two forwarded addresses, and the partition-key unit tests cover subject, mapped claim, address
  fallback, spoofed left-most XFF entry, IPv4-mapped normalization and the no-address fallback.

## Notes for later work

- **The address branch is unreachable through today's policies.** `UseAuthorization` runs before `UseRateLimiter`,
  and every endpoint carrying one of these policies requires authentication, so an anonymous request is rejected
  with 401 before it can spend a permit. A token whose subject is not a GUID also fails authentication
  (`JwtClaimsEnrichment`). The branch is asserted at unit level and keeps the partitioner honest if a policy is ever
  put on an anonymous endpoint.
- **Partition cardinality is unbounded.** `RateLimitPartition` keeps one limiter per key for the process lifetime,
  so a caller that can rotate addresses (or spoof `X-Forwarded-For` if the proxy does not overwrite it) allocates
  limiters. A process-wide ceiling and a bounded/expiring partition cache are the follow-up, together with
  per-account hub *invocation* limiting — the hub filter still only counts invocations.
