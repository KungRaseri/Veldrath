---
kind: area
date: undated
area: engine
title: "AddGold Handler Details (session-9)"
---

## AddGold Handler Details (session-9)

- Blob key: `Gold` (same key used by `RestAtLocationHubCommandHandler.KeyGold`)
- `Amount` can be positive (add) or negative (spend); zero is rejected
- Over-spend guard: if `-Amount > currentGold`, fails with "Not enough gold" message
- Broadcasts `GoldChanged` payload: `{ CharacterId, GoldAdded, NewGoldTotal, Source }`
- `GoldChangedPayload` internal record lives in `CharacterSelectViewModel.cs`
- Client callback: `GameViewModel.OnGoldChanged(int goldAdded, int newGoldTotal)` → updates `Gold` property + AppendLog
- Hub sends: `SendCommandAsync<object>("AddGold", new { Amount, Source })`
