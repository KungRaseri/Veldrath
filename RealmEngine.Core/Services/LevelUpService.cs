using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Services;

/// <summary>
/// Service for level-up calculations and progression logic.
/// Contains domain logic for character advancement, skill availability, and stat calculations.
/// </summary>
public class LevelUpService
{
    private readonly ISkillRepository _skillRepository;

    /// <param name="skillRepository">Skill catalog repository.</param>
    public LevelUpService(ISkillRepository skillRepository)
    {
        _skillRepository = skillRepository ?? throw new ArgumentNullException(nameof(skillRepository));
    }

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
    /// Get all skills available for a character to learn or improve.
    /// Skills from the catalog are included unless the character has already reached maximum rank.
    /// </summary>
    public async Task<List<Skill>> GetAvailableSkillsAsync(Character character)
    {
        var catalogSkills = await _skillRepository.GetAllAsync();

        return catalogSkills
            .Where(sd =>
            {
                if (!character.Skills.TryGetValue(sd.SkillId, out var learned))
                    return true;

                return learned.CurrentRank < sd.MaxRank;
            })
            .Select(sd => new Skill
            {
                Name = sd.DisplayName,
                Description = sd.Description,
                RequiredLevel = 1,
                MaxRank = sd.MaxRank,
                Type = MapCategory(sd.Category),
            })
            .ToList();
    }

    private static SkillType MapCategory(string category) => category.ToLowerInvariant() switch
    {
        "combat"      => SkillType.Combat,
        "defense"     => SkillType.Defense,
        "magic"       => SkillType.Magic,
        "passive"     => SkillType.Passive,
        _             => SkillType.Utility,
    };

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