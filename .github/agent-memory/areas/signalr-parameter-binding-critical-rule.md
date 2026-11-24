---
kind: area
date: undated
area: engine
title: "SignalR Parameter Binding — Critical Rule"
---

## SignalR Parameter Binding — Critical Rule

> **See [`.github/instructions/hub-development.md`](../../../.github/instructions/hub-development.md#the-single-dto-parameter-rule-critical) for the single-DTO parameter rule and binding failure explanation.**

Hub methods that the client calls **must** use a single request DTO (record) as their parameter — NOT multiple separate primitives. Fixed 2026-03-21 (session-17): `GainExperience`, `AddGold` and `TakeDamage` were converted from primitive parameters to single request DTOs. All of those, plus `AwardSkillXp`, were later retired from the hub entirely — `GainExperience` and `TakeDamage` on 2026-11-23, `AddGold` and `AwardSkillXp` on 2026-11-24 (#269) — so progression, gold, damage and skill experience are awarded by server-side commands only, and the request DTOs went with the methods. `GameHubTests.GameHub_Does_Not_Expose_ClientCallableProgression_Or_Damage_Methods` pins all four absences. The rule still governs every client-callable hub method that is added.
