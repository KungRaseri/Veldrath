---
kind: work-item
date: 2026-11-20
area: content
title: "Dialogue depth: E4 is shipped"
---

### Session-107 (2026-11-20) — Dialogue depth: E4 is shipped

Milestone E's last phase. The content schema is in [`foundry-content-schema.md`](../foundry-content-schema.md); this is
the runtime and the panel.

- **Two bridges, one shared evaluation.** `GetNpcDialogue` and `ChooseNpcOption`
  ([`DialogueHubCommands.cs`](../../../Veldrath.Server/Features/Characters/Npcs/DialogueHubCommands.cs)) both go through
  `NpcDialogueReader.BlockedReason`, so the reason a button is disabled and the reason the server refuses are the same
  computation. The read payload carries `Enabled`, `BlockedReason` and the grant preview; `Response` is deliberately
  **not** sent until the choice is made.
- **One-shot is claimed, not checked and then granted.** `ClaimAsync` inserts the `CharacterDialogueChoice` row first;
  a duplicate key means "already spoken about that" and pays nothing. Grabbing the gold before the claim would have
  made a double click pay twice.
- **Grants go through their owners**: items into `Characters.InventoryBlob` via `CharacterBag`, gold through
  `AddGoldHubCommand`, quests through `AcceptQuestHubCommand`, objectives through `QuestJournal.Advance`. Quest
  enrolment is applied **first** because it reloads and saves the character, then the journal/bag are written once,
  then gold — the order in `ApplyGrantsAsync`.
- **Entry selection**: the named `entrySlug` must belong to that speaker, otherwise the speaker's entry with options
  wins. Chaining is one `NextSlug` hop, and the chained conversation is rebuilt against the **reloaded** character so
  a granted item changes what the next entry offers.
- **Client**: `DialogueOverlay.svelte` + `dialogue.ts` (pure display rules, `dialogue.test.ts`), `session.dialogue`,
  and the People block's Talk now calls `startConversation`, which still runs `TalkToNpc` first — that is what advances
  `talk:` objectives, and dropping it would silently break the quest path.
- **Modal consequences, twice**: `harvest.e2e.ts` had to close the panel after talking, and the paywall of a modal
  panel over the map is the failure mode to remember for any future "Talk" affordance.
- `dialogue.e2e.ts` asserts the whole transport today (People entry → hub → panel → greeting still logged). It asserts
  "nothing more to say" rather than a choice because no conversation is authored in the seed — the choose path is
  covered by 16 handler tests. **When the content team authors the elder's options, extend that spec with a choice and
  its consequence.**
