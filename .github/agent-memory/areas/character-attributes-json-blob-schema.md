---
kind: area
date: undated
area: engine
title: "Character Attributes JSON Blob Schema"
---

## Character Attributes JSON Blob Schema

- `UnspentAttributePoints`: int — granted by a level-up (`CharacterProgression.AwardExperience`) and spent via `AllocateAttributePoints`
- `Strength`, `Dexterity`, etc.: int — core stats
- `Gold`: int — currency; several paths write it (see below); no client-callable `GameHub.AddGold` exists (retired 2026-11-24, #269)
- `CurrentHealth`, `MaxHealth`: int — rewritten by a level-up (`CharacterProgression`); restored to max by `RestAtLocation`
- `CurrentMana`, `MaxMana`: int — rewritten by a level-up (`CharacterProgression`); restored to max by `RestAtLocation`
- Defaults when absent: `MaxHealth = Level * 10`, `MaxMana = Level * 5`
- Handler key constants are `internal const string KeyXxx` on the handler class
- **DO NOT store string-valued data in Attributes blob** — it's deserialized as `Dictionary<string, int>`; mixing types causes `JsonException` and data loss

## `Gold` has several writers, and no hub method of its own

Quest, chest and dialogue awards go through `AddGoldHubCommandHandler` (`internal const string KeyGold = "Gold"`,
which refuses a debit larger than the purse). It has three server-side dispatchers and no client-callable one: `GameHub.TurnInQuest`,
`ChestHubCommands` (reached through `GameHub.OpenChest`) and `DialogueHubCommands` (reached through
`GameHub.ChooseNpcOption`). `GameHubTests.GameHub_Does_Not_Expose_ClientCallableProgression_Or_Damage_Methods`
pins the absence of the `AddGold` hub method (retired 2026-11-24, #269).

The handler is **not** the only writer of the key. The "exactly one writer" / "single writer" wording that
`ChestHubCommands`, `DialogueHubCommands`, `GameHub` and `LootChestsTests` carried was corrected on 2026-11-24
(comment-only) to call it the award path and the owner of its own write. As of `development@8b22ef11` the others are:

- `RestAtLocationHubCommand` — deducts the rest cost (`attrs[KeyGold] = gold - request.CostInGold`; `CostInGold`
  defaults to 10) and refuses when the purse is short.
- `BuyItemHubCommand` — purchase debit; `SellItemHubCommand` — sale credit.
- `CombatHelpers` — the loot split (`attrs["Gold"] = attrs.GetValueOrDefault("Gold") + share.Gold`) and the death
  penalty (`Math.Max(0, gold - applied.GoldLost)`).
- `GameHub`'s GM `/setgold` — writes the requested total straight into the blob and sends `GoldChanged` with the
  diff to that character's own connection; it does not go through `AddGoldHubCommand`.

## Derived pools are the class curve, not an attribute formula

`CharacterProgression.AwardExperience` writes `MaxHealth`/`MaxMana` on a level-up from the class row creation encoded
(`StartingHealth + HealthGrowth × (level − 1)`, same for mana: warrior 120 + 12/level, mage 60 + 6/level), falling back
to the engine's `Constitution × 10 + Level × 5` / `Wisdom × 5 + Level × 3` only when no class row resolves. The stored
pool is always the floor, and the current pools are clamped to the new ceiling but never refilled.

`AllocateAttributePoints` writes the attribute and the unspent balance and deliberately leaves the pools alone. **That
is a recorded decision, not a bug** — the next reader should not "fix" it without changing the pool model:

- At the character's current level the class curve returns the value the blob already holds, so calling the level-up
  recompute from the allocation path would be a no-op. Only the no-class-row fallback would pay the engine's
  `Constitution × 10`, which would make one point worth 10 health for a character whose class row is missing and 0 for
  everyone else — the split-brain the class curve exists to avoid.
- The attributes take effect where combat reads them: `Strength` (player damage), `Constitution` (incoming-damage
  defence in `CombatHelpers.CalculateEnemyDamage`) and `Intelligence` (magic damage in `UseAbilityInCombatHubCommand`).
  `Dexterity` and `Charisma` have no reader yet, so a point there is inert until a rule consumes it. `Wisdom` is **not**
  inert: it feeds the engine's no-class-row mana fallback on a level-up (`CharacterProgression`) and the starting mana
  `CharacterEndpoints` derives from `GetMaxMana`.
- The client's health bar is the stored `MaxHealth` from the `CharacterSelected` payload, so allocating `Constitution`
  does not move it.

## The level-up panel withholds spending for the three ruleless attributes

Decision (acceptance option 2, `t_e5288aba`): the web client's Attributes dialog spends points only into `Strength`,
`Constitution` and `Intelligence` — the three stats a combat path actually reads. `Dexterity`, `Wisdom` and `Charisma`
stay in the panel with their name, saved value and order, each carrying the visible note `Not available for point
spending yet.` and no Increase/Decrease control. `GameOverlays.svelte` keeps the allowlist (`ALLOCATABLE_ATTRIBUTES`),
guards additions with it and filters the outgoing payload, so an unavailable key never reaches
`AllocateAttributePoints`; `game-overlays-arms.test.ts` pins the note, the absent controls, the three spendable rows
and that opening the panel allocates nothing.

That is a panel policy, not a server rule. The handler still accepts any attribute key, nothing is refunded, banked
balances and saved values are untouched, and `ATTRIBUTE_ORDER`/`LeftSidebar` keep the full six-stat readout. Extend
the allowlist only once a regular-play rule reads the attribute; do not promise the three "have no effect anywhere",
since `Wisdom` moves the no-class-row mana fallback.

`Veldrath.Server.Tests/Features/AllocateAttributePointsHubCommandTests.cs` pins the behaviour: the only keys an
allocation changes are the allocated attribute and `UnspentAttributePoints`.
