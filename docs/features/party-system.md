# Party System

**Status**: ✅ 100% Complete
**Implementation**: `RealmEngine.Core/Features/Party`
**Tests**: 16/16 passing

## Overview

Recruit and manage NPC allies for cooperative combat. Supports up to 4 party members (1 leader + 3 recruits) with AI-controlled combat, role assignment, and shared rewards.

## Implementation Details

### Party Mechanics
- **Max Party Size**: 4 members (1 leader + 3 recruits)
- **Recruitment**: Hire friendly NPCs with level/charisma requirements
- **Party Combat**: Multi-character turns with AI ally actions
- **Party Management**: Equip allies, assign roles, heal party
- **Party Progression**: Allies level up and gain role-based stat bonuses
- **Shared Rewards**: XP and gold distributed among party members

### Party Roles (Auto-Assigned by Occupation)
- **Tank**: High HP, defense focus (Warriors, Paladins)
- **DPS**: High damage output (Rogues, Rangers)
- **Healer**: Support and healing (Clerics)
- **Support**: Utility and buffs (Mages)

### AI Behaviors
- **Aggressive**: 1.3× damage, attack focus
- **Balanced**: 1.0× damage, mixed actions
- **Defensive**: 0.8× damage, survival focus
- **SupportFocus**: 0.9× damage, ally support priority

### Combat Flow
1. Player character takes action
2. Allied party members act (AI-controlled)
3. Enemy takes action
4. Repeat until victory or defeat

### Enemy Targeting
- **60%** chance to target player character
- **40%** chance to target random alive party member

## Services & Commands

### Services
- **PartyService**: CreateParty, RecruitNPC, DismissPartyMember, DistributeExperience, DistributeGold, HealParty, EquipItem
- **PartyAIService**: DetermineAction, ShouldHeal, DetermineHealAction, DetermineAttackAction, ApplyHeal

### Commands
- **RecruitNPCCommand**: Recruit friendly NPC to party (checks: level requirement, charisma, party not full, NPC friendly)
- **DismissPartyMemberCommand**: Remove party member, optionally transfer items
- **PartyCombatTurnCommand**: Execute full party combat turn with AI allies

### Queries
- **GetPartyQuery**: Get party composition with member details

## Godot Integration Example

```csharp
// Recruit NPC to party
var recruitResult = await mediator.Send(new RecruitNPCCommand
{
    CharacterName = "PlayerHero",
    NpcId = "friendly-warrior-npc",
    SaveGameId = saveId
});

if (recruitResult.Success)
{
    UpdatePartyUI(recruitResult.Party);
}

// Execute party combat turn
var combatResult = await mediator.Send(new PartyCombatTurnCommand
{
    CharacterName = "PlayerHero",
    Action = CombatActionType.Attack,
    SaveGameId = saveId
});

if (combatResult.Success)
{
    DisplayCombatLog(combatResult.Messages);
    UpdateHealthBars(combatResult);
}
```

## Related Systems

- [Character System](character-system.md) - NPCs use character rules
- [Combat System](combat-system.md) - Party combat mechanics
- [Progression System](progression-system.md) - Ally leveling and growth
