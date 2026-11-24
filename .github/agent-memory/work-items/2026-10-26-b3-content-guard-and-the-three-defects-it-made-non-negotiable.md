---
kind: work-item
date: 2026-10-26
area: content
title: "B3: content guard, and the three defects it made non-negotiable"
---

### Session-93 (2026-10-26) - B3: content guard, and the three defects it made non-negotiable

**Content fixed (not tolerated):** `glowcap-mushroom` and `lost-cargo` added to `ItemsSeeder` as real quest
items; `the-lost-shipment` objective `interact:village-blacksmith` changed to `talk:` (the implemented kind
advanced by `TalkToNpc`); `arcane-focus` `ManaCost = -5` changed to `0` (a passive is never cast, so a negative
cost was only a landmine that would refund mana). `SeededContentValidationTests` lost its allow-list and now
demands **zero** errors over the real seeder.

**The hole this exposed:** the audit *skipped* objective kinds it did not recognise (`_ => null`), which is why
`interact:` had been invisible since A5. Unknown kinds are now `unsupported-quest-kind` errors, `talk:` is
explicitly recognised as implemented-but-unverifiable (no NPC catalog yet), and
`AuditQuestObjectives_Ignores_Objective_Types_Without_A_Content_Reference` became
`..._Reports_An_Unknown_Objective_Kind` - the old test was encoding the bug.

**CI:** `.github/workflows/content-guard.yml` builds `Veldrath.Server.Tests` and runs only
`SeededContentValidationTests`, path-filtered to seeders/entities/validators/content feature/this workflow.
Pinned SDK `10.0.402`, matching `ci-server.yml`.

**Counts:** Server 832 -> **834**, full solution **3,988**.
