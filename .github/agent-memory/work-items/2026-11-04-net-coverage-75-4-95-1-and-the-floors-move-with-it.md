---
kind: work-item
date: 2026-11-04
area: web
title: ".NET coverage 75.4% → 95.1%, and the floors move with it"
---

### Session-99 (2026-11-04) — .NET coverage 75.4% → 95.1%, and the floors move with it

**PR #53 (open, head 50646481) raises .NET coverage to 95.1% line / 82.7% branch** (30,443 / 31,995
lines), up from 75.4% / 55.8% measured on `development`. Six assemblies: Core 92.0%, Server 95.6%,
Data 99.8%, Shared 98.8%, Contracts 100.0%, Discord 89.6%. `scripts/coverage-floors.json` is ratcheted
in the same PR: global 94/81, Core 90/79, Server 94/82, Data 98/83, Shared 97/94, Contracts 99/—,
Discord 88/91. The delivered-on-`development` figure only becomes "merged" once the PR lands.

- **The 0.41-point oscillation is gone in practice.** Four consecutive full runs of the final
  tree spanned at most **0.02 points** on any assembly (Core 91.968 ±0.00, Server 95.57–95.59),
  because the RNG-dependent paths are now driven through seeded `IRandomSource` values or
  range/invariant assertions instead of retry loops. Floors are still `trunc(lowest run) − 1`,
  never `trunc(measured)`, and were derived from those four runs.
- **`--suggest` proposes `floor(measured)`, not `floor(measured) − 1`.** The gate note says
  "raise it to 91" for a 91.97% measurement; the policy in the skill/plan is one point of
  headroom below the truncated minimum. Do not copy the suggestion verbatim or the ratchet
  will sit inside the noise band.
- **Test-only seams added.** `Veldrath.Discord.Tests/Infrastructure/
  DiscordInteractionHarness.cs` emits a `SocketInteraction` subclass in a dynamic assembly
  (`IgnoresAccessChecksToAttribute`) and assigns it through `InteractionModuleBase<T>.Context`'s
  private setter, so the slash-command bodies actually run offline and their embeds are asserted;
  `RealmEngine.Core.Tests/Infrastructure/ThrowingInformationLogger.cs` reaches handler catch arms.
  Both live in test projects and are excluded from the report by `coverage.runsettings`. Session-100
  then added four deliberate production seams (`IRandomSource` parameters on `EnemySpellCastingService`,
  `CraftingService`, `PartyCombatTurnHandler` and `EnemyGenerator`, `InMemoryInventoryService
  .GetDurability`, `SmtpEmailSender.ResolvePort`) plus one real reward-persistence fix, all listed in
  the PR body.
- **A persisted-state assertion exposed a product bug.** `UseAbilityInCombatHubCommandHandler` awarded
  XP/gold by writing the character row and then overwrote its attribute blob with the pre-reward copy,
  silently dropping the gold. The handler now re-reads the row before writing the decremented cooldowns
  (as `AttackEnemyHubCommandHandler` already did); the test asserts the persisted `Gold`, not just the
  returned `GoldEarned`.
- **What is still uncovered, and why** (1,552 lines): Discord 41 (`BotWorker`,
  `InteractionHandlingService` need a live gateway/REST event), Server 398 (Postgres-only
  `ILike`/`APPLY`/`SKIP LOCKED` arms, `internal` `DotEnvConfiguration` without
  `InternalsVisibleTo`, sealed `AccountProvisioningService`), Core ~1,081 (defensive switch
  default arms over closed enums, dead private members such as
  `CreateCharacterHandler.GetClassIdFromName`, `GameHub`'s ban-on-connect path), Data 14.
- **Test count**: 4,011 → 6,004 across the five test projects.
