---
kind: area
date: undated
area: engine
title: "SignalR Parameter Binding — Critical Rule"
---

## SignalR Parameter Binding — Critical Rule

> **See [`.github/instructions/hub-development.md`](../../../.github/instructions/hub-development.md#the-single-dto-parameter-rule-critical) for the single-DTO parameter rule and binding failure explanation.**

Hub methods that the client calls **must** use a single request DTO (record) as their parameter — NOT multiple separate primitives. Fixed 2026-03-21 (session-17): Converted `GainExperience`, `AddGold`, `TakeDamage` hub methods to accept `GainExperienceHubRequest`, `AddGoldHubRequest`, `TakeDamageHubRequest` DTOs. `GainExperience` and `TakeDamage` were later retired from the hub entirely (2026-11-23): progression and damage are server-side commands now.
