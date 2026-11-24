---
kind: work-item
date: 2026-10-25
area: content
title: "A4: quest HUD tracker, completion diffing, turn-in toast (client only)"
---

### Session-87 (2026-10-25) — A4: quest HUD tracker, completion diffing, turn-in toast (client only)

**No new wire event.** `GameSession` keeps the previous journal snapshot (`previousQuests`) and
`announceQuestProgress` diffs each `QuestLogReceived` / `QuestTurnedIn` against it, emitting one
`Objective complete: <key> (<title>).` system line per objective that flipped to complete. The **first** snapshot
produces nothing — there is nothing to compare against, and a reload must not spam the log. Repeating the same
snapshot announces nothing because the objective is already complete in the previous one.

**Tracker.** `trackedQuests` is a pure getter: active quests mapped to `{ slug, title, objective }` where `objective`
is the nearest incomplete `QuestObjective` (or null ⇒ "Ready to turn in"). New `TrackedQuest` type in `types.ts`, new
[`QuestTracker.svelte`](../../../web/client/src/lib/game/QuestTracker.svelte) rendered from `LeftSidebar` above the character
menu, and it renders nothing when no quest is active.

**Turn-in toast.** `questTurnInToast` state + `dismissQuestToast()`, rendered in `GameOverlays` at the top right
(the party invite toast owns the bottom right). Deliberately **no auto-dismiss timer**: a testable state beats a
setTimeout, and the player dismisses it when they have read it.

**Tests:** 3 Vitest (diff announces once, tracker derivation, toast set/dismiss) + 3 component tests (tracker lists the
nearest objective, tracker renders nothing when empty, toast renders and dismisses). Vitest 179 → **185**, typecheck
and lint clean, `npm run build` clean.

---
