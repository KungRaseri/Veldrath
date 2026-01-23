using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Services;

/// <summary>
/// Service for level-up calculations and progression logic.
/// Contains domain logic for character advancement, skill availability, and stat calculations.
/// NO UI CODE - All presentation handled by Godot.
/// </summary>
public class LevelUpService
{
    /// <summary>
    /// Calculate the experience required to reach a specific level.
    /// Formula: Level * 100
    /// </summary>
    public int CalculateExperienceForLevel(int level)
    {
        if (level < 1) return 0;
        return level * 100;
    }

    /// <summary>
    /// Calculate the total experience needed from level 1 to reach a target level.
    /// </summary>
    public int CalculateTotalExperienceForLevel(int level)
    {
        if (level < 1) return 0;
        
        int total = 0;
        for (int i = 1; i < level; i++)
        {
            total += CalculateExperienceForLevel(i);
        }
        return total;
    }

    /// <summary>
    /// Calculate attribute points awarded for a level up.
    /// Base: 3 points per level
    /// Bonus: +2 points every 5 levels (5, 10, 15, 20, etc.)
    /// </summary>
    public int CalculateAttributePointsForLevel(int level)
    {
        int basePoints = 3;
        int bonusPoints = (level % 5 == 0) ? 2 : 0;
        return basePoints + bonusPoints;
    }

    /// <summary>
    /// Calculate skill points awarded for a level up.
    /// Base: 1 point per level
    /// Bonus: +1 point every 5 levels (5, 10, 15, 20, etc.)
    /// </summary>
    public int CalculateSkillPointsForLevel(int level)
    {
        int basePoints = 1;
        int bonusPoints = (level % 5 == 0) ? 1 : 0;
        return basePoints + bonusPoints;
    }

    /// <summary>
    /// Preview stat changes if character levels up.
    /// Returns projected HP, Mana, and points gained.
    /// </summary>
    public LevelUpPreview PreviewLevelUp(Character character, int targetLevel)
    {
        if (targetLevel <= character.Level)
        {
            throw new ArgumentException("Target level must be higher than current level");
        }

        int attributePointsGained = 0;
        int skillPointsGained = 0;

        for (int level = character.Level + 1; level <= targetLevel; level++)
        {
            attributePointsGained += CalculateAttributePointsForLevel(level);
            skillPointsGained += CalculateSkillPointsForLevel(level);
        }

        // Use character's built-in calculation methods
        int newMaxHealth = character.GetMaxHealth();
        int newMaxMana = character.GetMaxMana();

        return new LevelUpPreview
        {
            CurrentLevel = character.Level,
            TargetLevel = targetLevel,
            AttributePointsGained = attributePointsGained,
            SkillPointsGained = skillPointsGained,
            CurrentMaxHealth = character.MaxHealth,
            NewMaxHealth = newMaxHealth,
            CurrentMaxMana = character.MaxMana,
            NewMaxMana = newMaxMana,
            HealthGain = newMaxHealth - character.MaxHealth,
            ManaGain = newMaxMana - character.MaxMana
        };
    }

    /// <summary>
    /// Validate attribute point allocation request.
    /// Ensures character has enough unspent points and values are non-negative.
    /// </summary>
    public (bool IsValid, string ErrorMessage) ValidateAttributeAllocation(
        Character character,
        Dictionary<string, int> attributeAllocations)
    {
        if (attributeAllocations == null || !attributeAllocations.Any())
        {
            return (false, "No attributes specified for allocation");
        }

        // Validate all values are non-negative first
        if (attributeAllocations.Values.Any(v => v < 0))
        {
            return (false, "Cannot allocate negative points");
        }

        int totalPointsRequested = attributeAllocations.Values.Sum();

        if (totalPointsRequested <= 0)
        {
            return (false, "Must allocate at least 1 point");
        }

        if (totalPointsRequested > character.UnspentAttributePoints)
        {
            return (false, $"Not enough points. Requested: {totalPointsRequested}, Available: {character.UnspentAttributePoints}");
        }

        // Validate attribute names
        var validAttributes = new[] { "strength", "dexterity", "constitution", "intelligence", "wisdom", "charisma" };
        var invalidAttributes = attributeAllocations.Keys
            .Where(k => !validAttributes.Contains(k.ToLower()))
            .ToList();

        if (invalidAttributes.Any())
        {
            return (false, $"Invalid attributes: {string.Join(", ", invalidAttributes)}");
        }

        return (true, string.Empty);
    }

    /// <summary>
    /// Get all skills available for a character based on level and class.
    /// Returns skills the character can learn or improve.
    /// </summary>
    public List<Skill> GetAvailableSkills(Character character)
    {
        var allSkills = GetAllSkills();

        return allSkills
            .Where(s => s.RequiredLevel <= character.Level)
            .Where(s =>
            {
                // If not learned yet, it's available
                if (!character.Skills.ContainsKey(s.Name))
                    return true;

                // If learned but not maxed, it's available
                var learned = character.Skills[s.Name];
                return learned.CurrentRank < s.MaxRank;
            })
            .ToList();
    }

    /// <summary>
    /// Get all skills in the game.
    /// TODO: Move to JSON data files and load via IDataService
    /// </summary>
    private List<Skill> GetAllSkills()
    {
        return new List<Skill>
        {
            new Skill
            {
                Name = "Power Attack",
                Description = "+10% melee damage per rank",
                RequiredLevel = 2,
                MaxRank = 5,
                Type = SkillType.Combat,
                Effect = "Increases physical damage"
            },
            new Skill
            {
                Name = "Critical Strike",
                Description = "+2% critical chance per rank",
                RequiredLevel = 3,
                MaxRank = 5,
                Type = SkillType.Combat,
                Effect = "Increases critical hit chance"
            },
            new Skill
            {
                Name = "Iron Skin",
                Description = "+5% physical defense per rank",
                RequiredLevel = 2,
                MaxRank = 5,
                Type = SkillType.Defense,
                Effect = "Reduces physical damage taken"
            },
            new Skill
            {
                Name = "Arcane Knowledge",
                Description = "+10% magic damage per rank",
                RequiredLevel = 3,
                MaxRank = 5,
                Type = SkillType.Magic,
                Effect = "Increases magical damage"
            },
            new Skill
            {
                Name = "Quick Reflexes",
                Description = "+3% dodge chance per rank",
                RequiredLevel = 4,
                MaxRank = 5,
                Type = SkillType.Defense,
                Effect = "Increases dodge chance"
            },
            new Skill
            {
                Name = "Treasure Hunter",
                Description = "+10% rare item find per rank",
                RequiredLevel = 5,
                MaxRank = 3,
                Type = SkillType.Utility,
                Effect = "Increases chance to find rare items"
            },
            new Skill
            {
                Name = "Regeneration",
                Description = "+2 HP regen per turn per rank",
                RequiredLevel = 6,
                MaxRank = 3,
                Type = SkillType.Passive,
                Effect = "Slowly regenerates health"
            },
            new Skill
            {
                Name = "Mana Efficiency",
                Description = "+10% mana pool per rank",
                RequiredLevel = 4,
                MaxRank = 5,
                Type = SkillType.Magic,
                Effect = "Increases maximum mana"
            }
        };
    }

    /// <summary>
    /// Calculate how many levels a character can gain with their current experience.
    /// </summary>
    public int CalculateLevelsGainableFromExperience(int currentLevel, int currentExperience)
    {
        int levelsGained = 0;
        int tempLevel = currentLevel;
        int tempXP = currentExperience;

        while (tempXP >= CalculateExperienceForLevel(tempLevel))
        {
            tempXP -= CalculateExperienceForLevel(tempLevel);
            tempLevel++;
            levelsGained++;

            // Safety cap at 100 levels
            if (levelsGained >= 100) break;
        }

        return levelsGained;
    }
}

/// <summary>
/// Preview information for a level-up.
/// </summary>
public class LevelUpPreview
{
    /// <summary>
    /// Gets or sets the character's current level.
    /// </summary>
    public int CurrentLevel { get; set; }
    
    /// <summary>
    /// Gets or sets the target level to preview.
    /// </summary>
    public int TargetLevel { get; set; }
    
    /// <summary>
    /// Gets or sets the total attribute points gained.
    /// </summary>
    public int AttributePointsGained { get; set; }
    
    /// <summary>
    /// Gets or sets the total skill points gained.
    /// </summary>
    public int SkillPointsGained { get; set; }
    
    /// <summary>
    /// Gets or sets the current maximum health.
    /// </summary>
    public int CurrentMaxHealth { get; set; }
    
    /// <summary>
    /// Gets or sets the new maximum health after leveling.
    /// </summary>
    public int NewMaxHealth { get; set; }
    
    /// <summary>
    /// Gets or sets the current maximum mana.
    /// </summary>
    public int CurrentMaxMana { get; set; }
    
    /// <summary>
    /// Gets or sets the new maximum mana after leveling.
    /// </summary>
    public int NewMaxMana { get; set; }
    
    /// <summary>
    /// Gets or sets the health gained from leveling.
    /// </summary>
    public int HealthGain { get; set; }
    
    /// <summary>
    /// Gets or sets the mana gained from leveling.
    /// </summary>
    public int ManaGain { get; set; }
}