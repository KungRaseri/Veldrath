---
kind: work-item
date: 2026-11-24
area: engine
card: t_181315e1
title: "Quest turn-in announces its gold with GoldChanged"
---

### Session_t_181315e1 (2026-11-24) — Quest turn-in gold reaches the purse, not just the toast

#279 announced chest and dialogue gold from the server's own total, but the third server-side award path
still said nothing: `GameHub.TurnInQuest` paid `AddGoldHubCommand` and then sent only `QuestTurnedIn`
(rewards, for the toast) and `ExperienceGained` (level/pools). The client's `QuestTurnedIn` handler fills
`questTurnInToast` and never assigns `stats.gold`, so the HUD kept the value `CharacterSelected` seeded
while the toast claimed "+25 gold".

- **Emit from the writer's figures.** After a successful `AddGoldHubCommand`, `TurnInQuest` sends
  `GoldChanged { CharacterId, GoldAdded = goldResult.GoldAdded, NewGoldTotal = goldResult.NewGoldTotal,
  Source = $"Quest:{result.Slug}" }` to `Clients.Caller`. The reward in `QuestTurnedIn.goldReward` is what
  the quest offered; the purse is what the gold writer actually stored — the same decision #279 made for
  chests and dialogue, so every sender keeps one payload contract.
- **Caller-only, and only when gold moved.** The send is guarded by `goldResult.Success && GoldAdded > 0`,
  so a failed write or a zero reward announces nothing. Gold is private; the zone group is not told.
- **Unchanged on purpose.** `QuestTurnedIn.goldReward` and its toast stay as the announcement of the
  reward; no client-callable gold bridge is reintroduced (`GameHubTests` still pins `AddGold`'s absence).
- **Pinned by tests.** `GameHubQuestItemTests.TurnInQuest_Grants_Gold_And_Experience_Then_Announces_The_Rewards`
  now asserts exactly one `GoldChanged` with the writer's total and source `Quest:rats`, and that the zone
  group receives none; `TurnInQuest_With_No_Rewards_Sends_No_Reward_Commands` asserts no `GoldChanged` when
  the quest paid nothing. `session.test.ts` pins that the turn-in event alone leaves `stats.gold` alone and
  that `GoldChanged` is what moves it.
- **Measured.** `dotnet build Veldrath.Server.slnx` 0 warnings / 0 errors; `GameHubQuestItemTests` 44
  passed; web unit `src/lib/game/session.test.ts` 131 passed; `payload-contract` + `src/lib/parity` 43 passed.
