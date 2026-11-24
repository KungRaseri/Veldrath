---
kind: area
date: undated
area: engine
title: "Wired Hub→MediatR Bridges"
---

## Wired Hub→MediatR Bridges

| Hub Method | HubCommand | Session |
|---|---|---|
| `GainExperience` | `GainExperienceHubCommand` | retired from the hub 2026-11-23 — dispatched by the quest turn-in and by `POST /api/dev/experience` (Development/Test only) |
| `RestAtLocation` | `RestAtLocationHubCommand` | 2026-03-19 session-2 |
| `AllocateAttributePoints` | `AllocateAttributePointsHubCommand` | 2026-03-19 session-3 |
| `UseAbility` | `UseAbilityHubCommand` | 2026-03-19 session-4 |
| `AwardSkillXp` | `AwardSkillXpHubCommand` | hub method retired 2026-11-24 (#269) — dispatched by `CraftItemHubCommandHandler`, which passes the character it loaded so the craft and the award share one blob write |
| `EquipItem` | `EquipItemHubCommand` | 2026-03-20 session-8 |
| `AddGold` | `AddGoldHubCommand` | 2026-03-20 session-9 |
| `TakeDamage` | `TakeDamageHubCommand` | retired 2026-11-23 — hub method and command removed; combat handlers apply damage |
| `CraftItem` | `CraftItemHubCommand` | 2026-03-20 session-13 |
| `EnterDungeon` | `EnterDungeonHubCommand` | 2026-03-20 session-13 |
| `VisitShop` | `VisitShopHubCommand` | 2026-03-21 session-17 |
| `NavigateToLocation` | `NavigateToLocationHubCommand` | 2026-03-24 session-18 |
| `UnlockZoneLocation` | `UnlockZoneLocationHubCommand` | 2026-03-24 session-18 |
| `SearchArea` | `SearchAreaHubCommand` | 2026-03-24 session-18 |
| `TraverseConnection` | `TraverseConnectionHubCommand` | 2026-03-24 session-18 |
| `EngageEnemy` | `EngageEnemyHubCommand` | 2026-03-30 session-27 |
| `AttackEnemy` | `AttackEnemyHubCommand` | 2026-03-30 session-27 |
| `DefendAction` | `DefendActionHubCommand` | 2026-03-30 session-27 |
| `FleeFromCombat` | `FleeFromCombatHubCommand` | 2026-03-30 session-27 |
| `UseAbilityInCombat` | `UseAbilityInCombatHubCommand` | 2026-03-30 session-27 |
| `Respawn` | `RespawnHubCommand` | 2026-03-30 session-27 |
| `GetNpcDialogue` | `GetNpcDialogueHubCommand` | 2026-11-20 (dialogue depth) |
| `ChooseNpcOption` | `ChooseNpcOptionHubCommand` | 2026-11-20 (dialogue depth) |

> **Note**: All 15 bridges above are server-side complete and fully wired end-to-end.
>
> **Note**: Sessions 32-39 added no new Hub→MediatR bridges. Moderation commands (`/ignore`, `/warn`, `/mute`, `/ban`) are processed via the existing SignalR chat infrastructure. Dev endpoints (`DevEndpoints.cs`) use minimal API endpoints, not the hub bridge pattern.
>
> **Note**: Session-40 achievement triggers (`CheckAchievementProgressCommand`) were wired into 5 Core handlers (AttackEnemy, CompleteQuest, GainExperience, LevelUp, HandlePlayerDeath) — not as Hub→MediatR bridges. Achievement progress is evaluated server-side within the handler pipeline, not via SignalR hub methods.
