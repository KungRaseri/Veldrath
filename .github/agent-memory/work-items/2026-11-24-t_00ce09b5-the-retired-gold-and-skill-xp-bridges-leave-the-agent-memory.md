---
kind: work-item
date: 2026-11-24
area: engine
card: t_00ce09b5
title: "The retired gold and skill-XP bridges leave the agent memory"
---

### Session-t_00ce09b5 (2026-11-24) — The retired gold and skill-XP bridges leave the agent memory

PR #269 (`ef1a14b0`) removed the client-callable `GameHub.AddGold` and `GameHub.AwardSkillXp` bridges and their
request DTOs, and `GameHubTests.GameHub_Does_Not_Expose_ClientCallableProgression_Or_Damage_Methods` pins all
four retired methods. Three `areas/` notes named the bridges as current wiring when this card was written, and
AGENTS.md makes those notes required session reading — a session that trusted them would look for a hub method
that no longer exists.

- `areas/awardskillxp-handler-details-session-5.md` had already been rewritten by #286; this card left it alone.
  It describes the craft-owned award, the `SkillXpGained` broadcast from `GameHub.CraftItem`, and the web
  `applySkillXp` subscriber.
- `areas/addgold-handler-details-session-9.md` was corrected whole, not just the `SendCommandAsync("AddGold", …)`
  line: the whole note was Avalonia-era. It now names the three server-side dispatchers — `GameHub.TurnInQuest`,
  the chest command in `ChestHubCommands.cs` (through `GameHub.OpenChest`) and the dialogue command in
  `DialogueHubCommands.cs` (through `GameHub.ChooseNpcOption`) — records that the hub announces `GoldChanged`
  from the handler's stored `NewGoldTotal` to the caller only (gold is private, so it never goes to the zone
  group), and points the client side at `session.svelte.ts` / `GoldChangedPayload` in `types.ts`.
- `areas/signalr-parameter-binding-critical-rule.md` named only `GainExperience` and `TakeDamage` as retired.
  `AddGold` and `AwardSkillXp` went in the same class of change (#269), and the sentence no longer names any of
  the four retired request DTOs.

**Verified:** `node scripts/memory/memory-index.mjs --check` exits 0, and a grep of `.github/agent-memory` for
either retired request-DTO name comes back empty (the note names neither, so it cannot satisfy the search
itself). The hub entry was written by the generator; `veldrath-memory.md` was not hand-edited.

- **Still stale, filed rather than folded in:** `areas/wired-hub-mediatr-bridges.md:18` still lists `AddGold` as
  a live hub method with no retirement note (its `AwardSkillXp` row at `:16` is already marked), and
  `areas/character-attributes-json-blob-schema.md:12` still says the gold key is "modified directly by
  `AddGold`" — `t_bb763da4`. The Avalonia `CharacterSelectViewModel`/`GameViewModel` references in the other
  session notes, plus the false `DefaultCraftingCost = 50` in `craftitem-handler-details-session-13.md` and the
  removed `TakeDamage` command in `takedamage-handler-details-session-10.md`, are the same class and remain
  uncorrected — `t_57d2edd8`. Both are gated behind this card so they start from a tree that already has these
  three notes corrected.
