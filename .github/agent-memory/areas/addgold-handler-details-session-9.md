---
kind: area
date: undated
area: engine
title: "AddGold Handler Details (session-9)"
---

## AddGold Handler Details (session-9)

> Corrected 2026-11-24 (t_00ce09b5). Session-9 described a client-callable hub bridge and an Avalonia payload
> record; both are retired. The mechanism below is the one on `development`.

- Blob key: `Gold` — `AddGoldHubCommandHandler.KeyGold`, the same key `RestAtLocationHubCommandHandler.KeyGold`
  uses. Chest, dialogue and quest pay through `AddGoldHubCommandHandler` rather than re-implementing the key;
  `RestAtLocation` still deducts the rest cost from `attrs[KeyGold]` directly, and a GM `/setgold` writes it
  directly too, so this handler is the shared award path, not the only writer
- `Amount` can be positive (add) or negative (spend); zero is rejected — "Gold amount cannot be zero"
- Over-spend guard: if `-Amount > currentGold`, fails with "Not enough gold. Have {currentGold}, need
  {-Amount}."
- The handler writes the attributes blob and `LastPlayedAt`, and returns
  `AddGoldHubResult { Success, ErrorMessage, GoldAdded, NewGoldTotal }`. It does not broadcast anything itself —
  the calling hub method announces `GoldChanged` from the returned total
- **No client-callable hub method.** `GameHub.AddGold` was retired 2026-11-24 (#269);
  `GameHubTests.GameHub_Does_Not_Expose_ClientCallableProgression_Or_Damage_Methods` pins its absence.
  `AddGoldHubCommand` has three production dispatchers, all server-side:
  - quest turn-in — `GameHub.TurnInQuest` sends it for `TurnInQuestHubResult.GoldReward`
  - chest open — the chest command in `ChestHubCommands.cs`, reached through `GameHub.OpenChest`
  - dialogue choice — the dialogue command in `DialogueHubCommands.cs`, reached through `GameHub.ChooseNpcOption`
- `GoldChanged` payload: `{ CharacterId, GoldAdded, NewGoldTotal, Source }`, sent to the caller only — gold is
  private, so it never goes to the zone group. The hub announces the total the writer actually stored
  (`NewGoldTotal`), not a figure re-derived from the reward the toast names. A GM `/setgold` bypasses the
  command and writes the blob directly, then announces the same shape with `Source = "Admin"` and the
  difference in `GoldAdded`
- Client side: `web/client/src/lib/game/session.svelte.ts` subscribes `hub.on('GoldChanged', …)` and assigns
  `stats.gold = payload.newGoldTotal`; the payload type is `GoldChangedPayload` in
  `web/client/src/lib/game/types.ts`. The Avalonia `GoldChangedPayload` record and
  `GameViewModel.OnGoldChanged` callback session-9 listed went with the desktop client
