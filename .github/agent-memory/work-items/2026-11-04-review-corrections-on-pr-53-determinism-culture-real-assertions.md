---
kind: work-item
date: 2026-11-04
area: web
pr: 53
title: "Review corrections on PR #53: determinism, culture, real assertions"
---

### Session-100 (2026-11-04) — Review corrections on PR #53: determinism, culture, real assertions

Round-1 review (`REQUEST_CHANGES`, comment #748) blocked on six test-quality points; all six are fixed on
the same branch (6,004 → 6,011 tests, still no test touches a boundary it does not assert).

- **Scripted, not sampled.** `EnemySpellCastingService`, `CraftingService`, `PartyCombatTurnHandler` and
  `EnemyGenerator` now take `IRandomSource? randomSource = null` (defaulting to `SystemRandomSource
  .Shared`) so `CombatService`, crafting and party-turn tests script the exact rolls and assert exact
  damage/heal/quality/target outcomes. That deleted every retry loop the review named; the location
  test's ten-enemy candidate sample is scripted too (0 failures in 30 repeats, where the old test failed
  at repeat 44).
- **Culture-pinned formatting tests.** `RealmEngine.Shared.Tests/Infrastructure/UseCultureAttribute.cs`
  (a `BeforeAfterTestAttribute`) pins `en-US` for the tests that hard-code dot decimals. The whole
  Shared suite now passes under `LANG=de_DE.UTF-8 LC_ALL=de_DE.UTF-8` (1,158/1,158); the review had
  reproduced 9 failures there.
- **Assertions that can fail for the right reason.** Durability is observed through the new
  `InMemoryInventoryService.GetDurability` (null → 100 start, accumulation, zero floor, per-character
  isolation); the SMTP default port is asserted through the new protected `SmtpEmailSender.ResolvePort`
  instead of dialling an ambient `localhost:587`; eviction tests set `LastUpdatedAt` explicitly rather
  than assuming `TimeSpan.Zero` expires a fresh session; the ability-reward test seeds nonzero XP/gold
  and asserts the persisted row (which is what exposed the overwritten-gold bug above).
- **Verified:** Release build 0 warnings / 0 errors on SDK 10.0.402 (the review's CS8602 is gone);
  6,011 tests pass in three consecutive full runs; three merged reports all print "Coverage contract
  satisfied" with no `--suggest` output; per-assembly spread ≤0.02 points (Core 91.95, Server
  95.57–95.59, Data 99.78, Shared 98.79, Contracts 100.00, Discord 89.59); `dotnet format
  --verify-no-changes` and `git diff --check` clean. Floors unchanged — nothing is 3+ points stale.
