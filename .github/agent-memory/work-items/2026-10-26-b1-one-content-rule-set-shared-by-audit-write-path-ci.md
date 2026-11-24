---
kind: work-item
date: 2026-10-26
area: infra
title: "B1: one content rule set, shared by audit / write path / CI"
---

### Session-91 (2026-10-26) — B1: one content rule set, shared by audit / write path / CI

**Shipped:** `RealmEngine.Data/Validation/Content/` — a pure content-validation library. `ContentIssue`
(rule, message, severity, field, identifier) + `ContentValidationResult`, `ContentRules` (stable ids),
`ContentReferenceCatalog` (+ `LoadAsync(ContentDbContext)`, `ForDomain`), `IContentValidator` /
`ContentValidator<TEntity>`, `ContentShapeRules`, one validator per family (item/weapon/armor, ability/spell,
quest, recipe, loottable, zonelocation, archetype, instance, dialogue, plus `StructuralContentValidator` for
species/class/background/skill/material/materialproperty/enchantment/organization/language), and
`ContentValidatorRegistry.CreateDefault()`.

**Key decisions**
- `QuestObjectiveKinds` moved to `RealmEngine.Shared/Models` (gameplay *and* validation need it; the server
  copy in `CharacterQuestEntry.cs` is gone). `ContentReferenceRules` moved into the same library with its
  rule-id constants aliased to `ContentRules`, so the audit's existing logs stay comparable — the previous
  `ContentReferenceAuditTests`/`QuestObjectiveAuditTests` now prove "same rule, same identifier".
- The registry is keyed by the **`ContentSchemaRegistry`** keys, which is what B2's `/{type}` segment will
  resolve — including the easy-to-miss ones: `zonelocation` (not `worldlocation`) and `language`.
- `spawn_pool_empty`, `rarity_weight_out_of_range`, `craftingSkill` misses and "recipe consumes nothing" are
  **warnings**, not errors. Safe locations and free recipes are legitimate; blocking them would be a false block.

**Three real content bugs the validators found** (allow-listed by name in
`SeededContentValidationTests`, for B3 to fix):
1. `the-lost-shipment` objective `collect:lost-cargo` — item does not exist.
2. `gather-mushrooms` objective `collect:glowcap-mushroom` — item does not exist.
3. `arcane-focus` has **ManaCost = -5** (a negative cost would refund mana if it were ever cast).

**Trap found once:** recipe ingredients point at **materials** with `ItemDomain = "items/materials"`, not at
items. Resolving every ingredient against `Items` reports ten false "unknown item" errors — always resolve
through `ContentReferenceCatalog.ForDomain(ingredient.ItemDomain)`.

**Tests:** `ContentValidatorTests` (36, Data.Tests) covers every family with good/bad fixtures plus
registry behaviour; `ContentValidatorCoverageTests` (4, Server.Tests) asserts every schema key has a validator
and no validator exists for a key the schema cannot describe; `SeededContentValidationTests` (3, Server.Tests)
runs all validators over the real `DatabaseSeeder` output. Data 265 → **301**, Server 807 → **814**, full
solution 3,925 → **3,968**.

**Also fixed:** two latent `CS8602`/`CS8604` warnings in `RealmEngine.Core.Tests/Behaviors/PipelineBehaviorTests.cs`
(they only surfaced once the project recompiled) — Moq `It.Is` takes an expression tree, so the null-safe
`ToString()` comparison now lives in `LogMatchers.MessageContains`.
