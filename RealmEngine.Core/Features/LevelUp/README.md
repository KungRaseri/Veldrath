# Level Up & Progression Feature

**Feature:** Character leveling, experience, and attribute allocation
**Location:** `RealmEngine.Core/Features/LevelUp/`
**Domain Service:** `LevelUpService` (`RealmEngine.Core/Services/LevelUpService.cs`)

---

## Commands

### GainExperienceCommand
Award experience points to a character.

**Parameters:**
- `CharacterName` (string): Character receiving XP
- `ExperienceAmount` (int): Amount of XP to award
- `Source` (string, optional): Source of XP (e.g., "Combat", "Quest")

**Returns:**
- `NewExperience`: Updated experience total
- `CurrentLevel`: Character's level
- `LeveledUp`: Whether character leveled up
- `NewLevel`: New level if leveled up
- `ExperienceToNextLevel`: XP needed for next level

**Example:**
```csharp
var result = await mediator.Send(new GainExperienceCommand
{
    CharacterName = "Player",
    ExperienceAmount = 150,
    Source = "Combat"
});

if (result.LeveledUp)
{
    GD.Print($"Level up! Now level {result.NewLevel}");
}
```

---

### LevelUpCommand
Explicitly level up a character (if XP requirements are met).

**Parameters:**
- `CharacterName` (string): Character to level up

**Returns:**
- `OldLevel`: Level before level up
- `NewLevel`: Level after level up
- `AttributePointsGained`: Attribute points awarded
- `SkillPointsGained`: Skill points awarded
- `StatIncreases`: Dictionary of stat gains (HP, Mana)
- `UnlockedAbilities`: Abilities unlocked at this level

**Example:**
```csharp
var result = await mediator.Send(new LevelUpCommand
{
    CharacterName = "Player"
});

if (result.Success)
{
    GD.Print($"Leveled up from {result.OldLevel} to {result.NewLevel}!");
    GD.Print($"+{result.StatIncreases["MaxHealth"]} HP");
}
```

---

### AllocateAttributePointsCommand
Allocate unallocated attribute points to character attributes.

**Parameters:**
- `CharacterName` (string): Character allocating points
- `AttributeAllocations` (Dictionary<string, int>): Points to allocate per attribute

**Returns:**
- `PointsSpent`: Total points spent
- `RemainingPoints`: Unallocated points remaining
- `NewAttributeValues`: Updated attribute values

**Example:**
```csharp
var result = await mediator.Send(new AllocateAttributePointsCommand
{
    CharacterName = "Player",
    AttributeAllocations = new Dictionary<string, int>
    {
        ["Strength"] = 2,
        ["Intelligence"] = 3
    }
});

GD.Print($"Allocated {result.PointsSpent} points. Remaining: {result.RemainingPoints}");
```

---

## Queries

### GetNextLevelRequirementQuery
Get experience requirements for the next level.

**Parameters:**
- `CharacterName` (string): Character to query

**Returns:**
- `CurrentLevel`: Current level
- `CurrentExperience`: Current XP total
- `RequiredExperience`: Total XP required for next level
- `RemainingExperience`: XP still needed
- `ProgressPercentage`: Progress percentage (0-100)

**Example:**
```csharp
var result = await mediator.Send(new GetNextLevelRequirementQuery
{
    CharacterName = "Player"
});

GD.Print($"Level {result.CurrentLevel}: {result.ProgressPercentage:F1}% to next level");
GD.Print($"Need {result.RemainingExperience} more XP");
```

---

### GetCharacterProgressionQuery
Get complete character progression information.

**Parameters:**
- `CharacterName` (string): Character to query

**Returns:**
- `Level`: Character level
- `Experience`: Total experience points
- `ExperienceToNextLevel`: XP needed for next level
- `UnallocatedAttributePoints`: Unspent attribute points
- `UnallocatedSkillPoints`: Unspent skill points
- `Attributes`: Current attribute values
- `Skills`: Current skill values
- `LearnedAbilities`: List of learned abilities
- `LearnedSpells`: List of learned spells
- `PlaytimeSeconds`: Total playtime
- `EnemiesDefeated`: Enemy kill count
- `QuestsCompleted`: Completed quest count

**Example:**
```csharp
var result = await mediator.Send(new GetCharacterProgressionQuery
{
    CharacterName = "Player"
});

GD.Print($"Level {result.Level} - {result.LearnedAbilities.Count} abilities, {result.LearnedSpells.Count} spells");
GD.Print($"Unspent points: {result.UnallocatedAttributePoints} attributes, {result.UnallocatedSkillPoints} skills");
```

---

### PreviewLevelUpQuery
Preview stat gains at the next level.

**Parameters:**
- `CharacterName` (string): Character to preview

**Returns:**
- `CurrentLevel`: Current level
- `NextLevel`: Level after leveling up
- `AttributePointsGain`: Attribute points to be gained
- `SkillPointsGain`: Skill points to be gained
- `StatGains`: Dictionary of stat increases (HP, Mana)
- `UnlockedAbilities`: Abilities unlocked at next level
- `CanLevelUp`: Whether character can level up now
- `RequiredExperience`: XP required for next level

**Example:**
```csharp
var result = await mediator.Send(new PreviewLevelUpQuery
{
    CharacterName = "Player"
});

if (result.CanLevelUp)
{
    GD.Print($"Ready to level up! Next level gains:");
    GD.Print($"  +{result.StatGains["MaxHealth"]} HP");
    GD.Print($"  +{result.StatGains["MaxMana"]} Mana");
    GD.Print($"  +{result.AttributePointsGain} Attribute Points");
}
```

---

## Domain Service Integration

The CQRS handlers delegate complex calculations to `LevelUpService`:

- `AwardExperience(character, xp)` - Award XP and handle auto-leveling
- `LevelUp(character)` - Perform level up and apply stat gains
- `CalculateExperienceForLevel(level)` - Get XP requirement for a level
- `CalculateStatGainsForLevel(level, className)` - Calculate HP/Mana gains

---

## Godot Integration Example

```csharp
// In Godot character controller
public class PlayerController : Node
{
    private readonly IMediator _mediator;

    // After defeating enemy
    private async Task OnEnemyDefeated(Enemy enemy)
    {
        var xpResult = await _mediator.Send(new GainExperienceCommand
        {
            CharacterName = "Player",
            ExperienceAmount = enemy.XP,
            Source = "Combat"
        });

        UpdateXPBar(xpResult.NewExperience, xpResult.ExperienceToNextLevel);

        if (xpResult.LeveledUp)
        {
            ShowLevelUpNotification(xpResult.NewLevel);
            await PlayLevelUpAnimation();
        }
    }

    // Show level up UI
    private async Task ShowLevelUpScreen()
    {
        var preview = await _mediator.Send(new PreviewLevelUpQuery
        {
            CharacterName = "Player"
        });

        if (preview.CanLevelUp)
        {
            var result = await _mediator.Send(new LevelUpCommand
            {
                CharacterName = "Player"
            });

            DisplayStatGains(result.StatIncreases);
            EnableAttributeAllocation(result.AttributePointsGained);
        }
    }

    // Allocate attribute points
    private async Task OnAttributePointsAllocated(Dictionary<string, int> allocations)
    {
        var result = await _mediator.Send(new AllocateAttributePointsCommand
        {
            CharacterName = "Player",
            AttributeAllocations = allocations
        });

        if (result.Success)
        {
            UpdateAttributeDisplay(result.NewAttributeValues);
            UpdateRemainingPoints(result.RemainingPoints);
        }
    }
}
```

---

## Notes

- Experience gains are automatically multiplied by difficulty settings
- Leveling up may be automatic (on XP gain) or manual (explicit command)
- Attribute/skill points must be explicitly allocated
- Stat gains are class-dependent (warriors get more HP, mages get more mana)
- Level cap and XP scaling defined in `LevelUpService`
