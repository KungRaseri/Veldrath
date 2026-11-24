---
kind: area
date: undated
area: engine
title: "GainExperience Client Wiring Details (session-11)"
---

## GainExperience Client Wiring Details (session-11)

- `GameViewModel.Level` (int) and `Experience` (long) reactive properties added
- `GainExperienceCommand : ReactiveCommand<(int Amount, string? Source), Unit>` — sends `"GainExperience"` with `new { Amount, Source }`
- `OnExperienceGained(int newLevel, long newExperience, bool leveledUp, int? leveledUpTo)` — updates `Level`, `Experience`, `AppendLog` with level-up branch
- `ExperienceGainedPayload(Guid CharacterId, int NewLevel, long NewExperience, bool LeveledUp, int? LeveledUpTo, string? Source)` internal record in `CharacterSelectViewModel.cs`
- `_experienceGainedSub` field + disposal + subscription added to `CharacterSelectViewModel.DoSelectAsync` — 14th subscription
- Server broadcasts payload via `GainExperience` hub method; field names match result properties: `NewLevel`, `NewExperience`, `LeveledUp`, `LeveledUpTo`
