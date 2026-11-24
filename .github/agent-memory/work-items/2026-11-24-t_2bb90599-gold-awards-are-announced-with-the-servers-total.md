---
kind: work-item
date: 2026-11-24
area: engine
card: t_2bb90599
title: "Server-side gold awards are announced from the server's own total"
---

### Session_t_2bb90599 (2026-11-24) — Chest and dialogue gold reach the client, and /setgold speaks the contract

After #269 retired the client-callable `GameHub.AddGold` bridge, nothing announced the gold that chests and
dialogue options pay through `AddGoldHubCommand`; `stats.gold` stayed at whatever `CharacterSelected` seeded
until a reload. The only surviving `GoldChanged` sender (`/setgold`) also sent `{ CharacterId, Gold, Source }`
into a client handler typed `{ goldAdded, newGoldTotal }`, so a GM set wrote `undefined` into the target's stat.

- **Emit from the hub, from the writer's own figures — not from the client delta.** Both result records now carry
  the gold the award path actually paid: `OpenChestHubResult.NewGoldTotal` (`int?`, set only when the
  `AddGoldHubCommand` succeeded) and `ChooseNpcOptionResult.GoldAwarded`/`NewGoldTotal` (summed over the option's
  gold grants; the dialogue handler's `ApplyGrantsAsync` now returns a private `DialogueGrants` record instead of
  the bare `applied` list). `GameHub.OpenChest` and `ChooseNpcOption` send `GoldChanged`
  (`{ CharacterId, GoldAdded, NewGoldTotal, Source }`) to `Clients.Caller` after the reward event.
- **GoldChanged is caller-only, and that is why `ChestOpened` is now a projection.** The chest broadcast goes to the
  zone group so everyone's chest panel empties, but the opener's purse is not the party's business, so the group gets
  `new { Success, ChestId, DisplayName, Items, Gold, Message, RemainingChests }` (exactly the client's
  `ChestOpenedPayload`) and `NewGoldTotal` never leaves the process. The dialogue reply was already caller-only.
- **`/setgold` now sends `GoldAdded = amount - previousGold` and `NewGoldTotal = amount`.** The delta is the honest
  difference from the stored blob; the client assigns `newGoldTotal` rather than adding, so a diff and an award
  cannot drift.
- **`GoldChangedPayload` in `web/client/src/lib/game/types.ts`.** Adding the interface turns the existing
  `payload-contract.test.ts` into a guard on all three senders: a future sender that renames a field fails the suite.
- **Pinned by tests.** `GameHubHarvestChestTests.OpenChest_Announces_The_Gold_To_The_Opener_Only` (payload shape,
  and that the zone group gets no `GoldChanged`), `OpenChest_Sends_No_GoldChanged_When_The_Chest_Paid_No_Gold`,
  `LootChestsTests` (the total propagates; a failed award leaves it null), `DialogueOptionTests` (awarded/total, and a
  refused grant is reported as nothing paid), `GameHubQuestItemTests.ChooseNpcOption_*` (hub payload, zone silence),
  `GameHubAdminCommandTests.SetGold_Writes_The_Attribute_And_Notifies_The_Target` (the GM payload), and
  `session.test.ts` folds `ChestOpened`/`NpcOptionChosen` + `GoldChanged` into `stats.gold`.
- **Unchanged on purpose:** `TurnInQuest` still pays gold through `AddGoldHubCommand` and reports it on
  `QuestTurnedIn.goldReward`; no client-callable gold bridge was reintroduced (`GameHubTests` still pins the absence).
- **Measured.** `dotnet build Veldrath.Server.slnx` 0 warnings / 0 errors; `dotnet test Veldrath.Server.slnx`
  6562 passed / 0 failed; `dotnet format Veldrath.Server.slnx --verify-no-changes` exit 0; web typecheck 0/0,
  lint clean, `npm run test:unit --workspace client -- --run` 1587 passed / 107 files.
