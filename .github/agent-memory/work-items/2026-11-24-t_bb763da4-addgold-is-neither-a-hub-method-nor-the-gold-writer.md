---
kind: work-item
date: 2026-11-24
area: engine
card: t_bb763da4
title: "AddGold is neither a hub method nor the only gold writer"
---

### Session-t_bb763da4 (2026-11-24) — AddGold is neither a hub method nor the only gold writer

Two memory notes still described the retired client-callable `GameHub.AddGold` as current wiring after #269
(`ef1a14b0`) removed it. Both are corrected here; verified against `development@58d560fa` (the two notes are
byte-identical there and at `8b22ef11`).

- **`areas/wired-hub-mediatr-bridges.md`** — the `AddGold` row carried only its session date. It now carries
  the retirement the way the `AwardSkillXp` row above it already did: hub method retired 2026-11-24 (#269),
  `AddGoldHubCommand` dispatched by `GameHub.TurnInQuest`, `ChestHubCommands` (reached through
  `GameHub.OpenChest`) and `DialogueHubCommands` (reached through `GameHub.ChooseNpcOption`).
- **The file's closing note was stale independently of this card.** It read "All 15 bridges above are
  server-side complete and fully wired end-to-end" over a 23-row table in which five bridges were already
  gone (`GainExperience`, `AwardSkillXp`, `AddGold`, `TakeDamage`, `TraverseConnection`) — though at the base
  only three of those rows said so. It now states the rule instead of a count: a row naming a live hub method
  is wired end-to-end; a row marked retired has no client-callable hub method, and where its command still
  exists that command is dispatch-only. All five rows carry the retirement marker at this head.
- **The `TraverseConnection` row was still presented as current wiring.** `TraverseConnectionHubCommand` was
  deleted by `72024d32` (2026-04-08, "Refactor zone connection handling and related entities", on
  development's history) and no `TraverseConnection` symbol survives anywhere in the tree — only the memory
  row named it. The row is now marked retired and points at the replacement paths: `GameHub.TravelToNode`
  (`TravelToNodeHubCommand`) and `GameHub.EnterLocation` (`EnterZoneLocationHubCommand`). Note the trap:
  `09206837` is the same refactor carrying the other branch's hash and is *not* an ancestor of
  `development`; `72024d32` is the one to cite.
- **The `TakeDamage` row was already correct, the rule around it was not.** The closing note said every
  retired row's command is dispatch-only, but `TakeDamageHubCommand` does not exist either (the row itself
  says "hub method and command removed"). The rule now says "where its command still exists", and names
  `TakeDamage` and `TraverseConnection` as the two rows that lost the command too.
- **`areas/character-attributes-json-blob-schema.md`** — "`Gold`: … modified directly by `AddGold`" named a
  method no client can call, and the file said nothing about which paths actually write the key. The bullet now
  points at a new section, `` `Gold` has several writers, and no hub method of its own ``.
- **Deliberately not a "single writer" claim.** `AddGoldHubCommandHandler` is the quest/chest/dialogue award
  path, not the only writer. At this commit the key is also written by `RestAtLocationHubCommand` (rest cost,
  `CostInGold` defaulting to 10), `BuyItemHubCommand` (debit) and `SellItemHubCommand` (credit),
  `CombatHelpers` (loot split and the death penalty) and `GameHub`'s GM `/setgold`, which writes the total
  straight into the blob and sends `GoldChanged` with the diff to that character's own connection. The
  "exactly one writer" / "single writer" comments in `ChestHubCommands` and `DialogueHubCommands` contradict
  those call sites; the note says so and quotes neither as fact. Fixing those comments is `t_1abb02c7`.
- **Pinned by a test, not by the prose.** `GameHubTests` reflects over `GameHub`'s declared public methods in
  `GameHub_Does_Not_Expose_ClientCallableProgression_Or_Damage_Methods` and asserts `AddGold` (with `GainExperience`,
  `TakeDamage`, `AwardSkillXp`) is absent; it is named in both corrected notes.
- **Sweep result.** `grep -n "AddGold\|AwardSkillXp"` over both files returns six lines and every one is a
  retirement statement or a name inside one: the two table rows, the `Gold` bullet, and three lines of the new
  section. Nothing left in either file presents `AddGold` as a hub method a client can call. The wider sweep
  checked every command class named in the table against `development@58d560fa`: 21 of 23 resolve to a file
  (the two dialogue commands live in `DialogueHubCommands.cs`); only `TakeDamageHubCommand` and
  `TraverseConnectionHubCommand` have none, and both rows are now marked retired.
- **Out of scope, already tracked:** the Avalonia-era notes and the `craftitem`/`takedamage` handler notes
  (`t_57d2edd8`).
- **Retired-row count corrected (review 302).** An earlier draft gave a count of retired rows that matched only
  the round-1 intermediate head. Five bridges were already gone at the base — `GainExperience`, `AwardSkillXp`,
  `AddGold`, `TakeDamage`, `TraverseConnection` — while only three of those rows said so; after the
  `TraverseConnection` fix all five carry the marker.
- **Gates:** `node scripts/memory/memory-index.mjs --write` then `--check` (exit 0);
  `node scripts/check-markdown-links.mjs` (exit 0).
