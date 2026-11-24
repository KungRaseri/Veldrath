---
kind: area
date: undated
area: engine
title: "GainExperience Client Wiring Details (session-11)"
---

## GainExperience Client Wiring Details (session-11)

> **Rewritten 2026-11-24.** The client-callable `GameHub.GainExperience` method was retired on 2026-11-23
> (recorded in `areas/wired-hub-mediatr-bridges.md`), and this note's `GameViewModel` commands, reactive
> properties and `ExperienceGainedPayload` record lived in the deleted Avalonia client. `GainExperienceHubCommand`
> itself survives and is dispatched server-side only.

- Server dispatch: `GameHub.TurnInQuest` sends `GainExperienceHubCommand` for a quest's `XpReward` and announces
  the result with `ExperienceGained` **to the caller** — `{ CharacterId, NewLevel, NewExperience, LeveledUp,
  LeveledUpTo, MaxHealth, CurrentHealth, MaxMana, CurrentMana, UnspentAttributePoints, Source }`. The payload
  carries the re-derived pools because a level-up rewrites them server-side; a client that only added XP would
  draw the old bar beside the new level.
- The second dispatcher is `POST /api/dev/experience` (`Veldrath.Server/Features/Dev/DevEndpoints.cs`), reachable
  only in Development/Test. It returns the command result over HTTP and broadcasts nothing.
- Web client: `ExperienceGainedPayload` in `web/client/src/lib/game/types.ts`; `Session` subscribes with
  `hub.on('ExperienceGained')` in `web/client/src/lib/game/session.svelte.ts`, merging `level`, `experience` and
  any pool / attribute-point fields into `stats`, and adding a level-up system message when `leveledUp` is true.
  Absent optional fields leave the existing balance alone — 0 is a real balance, so the reducer checks
  `undefined`, not truthiness.
- Do not add a client-callable gain-experience method back: progression is server-side, and the dev endpoint is
  the documented journey-spec path.
