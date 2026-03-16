using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealmEngine.Core.Features.Progression.Services;

/// <summary>
/// Service for managing skill XP awards, rank-ups, and progression.
/// Handles skill initialization, XP calculations, and rank-up logic.
/// </summary>
public class SkillProgressionService
{
    private readonly SkillDataService _catalogService;
    private readonly ILogger<SkillProgressionService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkillProgressionService"/> class.
    /// </summary>
    /// <param name="catalogService">The skill catalog service.</param>
    /// <param name="logger">Optional logger instance.</param>
    public SkillProgressionService(
        SkillDataService catalogService, 
        ILogger<SkillProgressionService>? logger = null)
    {
        _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        _logger = logger ?? NullLogger<SkillProgressionService>.Instance;
    }

    /// <summary>
    /// Initialize all skills for a new character.
    /// Sets all skills to rank 0 with initial XP requirements.
    /// </summary>
    public void InitializeAllSkills(Character character)
    {
        var allSkills = _catalogService.GetAllSkills();
        
        foreach (var (skillId, skillDef) in allSkills)
        {
            if (character.Skills.ContainsKey(skillId))
            {
                continue; // Already initialized
            }

            character.Skills[skillId] = new CharacterSkill
            {
                SkillId = skillId,
                Name = skillDef.Name,
                Category = skillDef.Category,
                CurrentRank = 0,
                CurrentXP = 0,
                XPToNextRank = skillDef.BaseXPCost,
                TotalXP = 0,
                GoverningAttribute = skillDef.GoverningAttribute
            };
        }

        _logger.LogInformation("Initialized {Count} skills for character {Name}", 
            allSkills.Count, character.Name);
    }

    /// <summary>
    /// Award XP to a skill and check for rank-ups.
    /// Can trigger multiple rank-ups if XP is sufficient.
    /// </summary>
    public SkillRankUpResult AwardSkillXP(Character character, string skillId, int xpAmount, string actionSource = "")
    {
        if (xpAmount <= 0)
        {
            return new SkillRankUpResult
            {
                SkillId = skillId,
                NewRank = character.Skills.GetValueOrDefault(skillId)?.CurrentRank ?? 0,
                RanksGained = 0,
                Notifications = new List<string>()
            };
        }

        // Initialize skill if not exists
        if (!character.Skills.TryGetValue(skillId, out var skill))
        {
            skill = InitializeSkill(skillId);
            character.Skills[skillId] = skill;
        }

        // Award XP
        skill.CurrentXP += xpAmount;
        skill.TotalXP += xpAmount;

        // Track XP source
        if (!string.IsNullOrEmpty(actionSource))
        {
            if (!skill.XPSources.ContainsKey(actionSource))
            {
                skill.XPSources[actionSource] = 0;
            }
            skill.XPSources[actionSource] += xpAmount;
        }

        var ranksGained = 0;
        var notifications = new List<string>();

        // Check for rank-ups (can rank up multiple times with large XP awards)
        while (skill.CurrentXP >= skill.XPToNextRank && skill.CurrentRank < 100)
        {
            skill.CurrentXP -= skill.XPToNextRank;
            skill.CurrentRank++;
            ranksGained++;

            // Recalculate XP needed for next rank
            skill.XPToNextRank = _catalogService.CalculateXPToNextRank(skillId, skill.CurrentRank);

            var message = $"{skill.Name} increased to rank {skill.CurrentRank}!";
            notifications.Add(message);
            
            _logger.LogInformation("Character {Character}: {Message}", character.Name, message);

            // Check for milestone ranks
            if (skill.CurrentRank % 25 == 0)
            {
                var milestone = $"{skill.Name} reached milestone rank {skill.CurrentRank}! 🎉";
                notifications.Add(milestone);
                _logger.LogInformation("Character {Character}: {Message}", character.Name, milestone);
            }
        }

        return new SkillRankUpResult
        {
            SkillId = skillId,
            NewRank = skill.CurrentRank,
            RanksGained = ranksGained,
            Notifications = notifications
        };
    }

    /// <summary>
    /// Get skill progress information for character sheet display.
    /// </summary>
    public SkillProgressDisplay GetSkillProgress(Character character, string skillId)
    {
        if (!character.Skills.TryGetValue(skillId, out var skill))
        {
            // Return unlearned skill data
            var skillDef = _catalogService.GetSkillDefinition(skillId);
            return new SkillProgressDisplay
            {
                SkillId = skillId,
                Name = skillDef?.Name ?? skillId,
                Category = skillDef?.Category ?? "unknown",
                CurrentRank = 0,
                CurrentXP = 0,
                XPToNextRank = skillDef?.BaseXPCost ?? 100,
                ProgressPercent = 0,
                CurrentEffect = "None (untrained)",
                NextRankEffect = GetEffectDescription(skillId, 1)
            };
        }

        return new SkillProgressDisplay
        {
            SkillId = skillId,
            Name = skill.Name,
            Category = skill.Category,
            CurrentRank = skill.CurrentRank,
            CurrentXP = skill.CurrentXP,
            XPToNextRank = skill.XPToNextRank,
            ProgressPercent = skill.XPToNextRank > 0 
                ? (double)skill.CurrentXP / skill.XPToNextRank * 100 
                : 0,
            CurrentEffect = GetEffectDescription(skillId, skill.CurrentRank),
            NextRankEffect = GetEffectDescription(skillId, skill.CurrentRank + 1)
        };
    }

    /// <summary>
    /// Get all skills progress for character sheet.
    /// </summary>
    public List<SkillProgressDisplay> GetAllSkillsProgress(Character character)
    {
        var allSkills = _catalogService.GetAllSkills();
        var progress = new List<SkillProgressDisplay>();

        foreach (var skillId in allSkills.Keys)
        {
            progress.Add(GetSkillProgress(character, skillId));
        }

        return progress.OrderByDescending(s => s.CurrentRank).ThenBy(s => s.Name).ToList();
    }

    /// <summary>
    /// Calculate the total effect value for a skill at a given rank.
    /// Used for combat/stat calculations.
    /// </summary>
    public double CalculateSkillEffect(string skillId, int rank, string effectType)
    {
        var skillDef = _catalogService.GetSkillDefinition(skillId);
        if (skillDef == null) return 0;

        var effect = skillDef.Effects.FirstOrDefault(e => 
            e.EffectType.Equals(effectType, StringComparison.OrdinalIgnoreCase));

        if (effect == null) return 0;

        // Effect value per rank × current rank
        return effect.EffectValue * rank;
    }

    /// <summary>
    /// Get XP amount for a specific action.
    /// </summary>
    public int GetXPForAction(string skillId, string actionName)
    {
        var skillDef = _catalogService.GetSkillDefinition(skillId);
        if (skillDef == null) return 0;

        return skillDef.XPActions.GetValueOrDefault(actionName, 0);
    }

    private CharacterSkill InitializeSkill(string skillId)
    {
        var skillDef = _catalogService.GetSkillDefinition(skillId);
        if (skillDef == null)
        {
            throw new InvalidOperationException($"Unknown skill ID: {skillId}");
        }

        return new CharacterSkill
        {
            SkillId = skillId,
            Name = skillDef.Name,
            Category = skillDef.Category,
            CurrentRank = 0,
            CurrentXP = 0,
            XPToNextRank = skillDef.BaseXPCost,
            TotalXP = 0,
            GoverningAttribute = skillDef.GoverningAttribute
        };
    }

    private string GetEffectDescription(string skillId, int rank)
    {
        var skillDef = _catalogService.GetSkillDefinition(skillId);
        if (skillDef == null || rank <= 0) return "None";

        var effects = new List<string>();
        foreach (var effect in skillDef.Effects)
        {
            var totalEffect = effect.EffectValue * rank;
            var formattedValue = FormatEffectValue(effect.EffectType, totalEffect);
            effects.Add($"+{formattedValue} {effect.EffectType}");
        }

        return effects.Count > 0 ? string.Join(", ", effects) : "None";
    }

    private string FormatEffectValue(string effectType, double value)
    {
        // Format percentages
        if (effectType.Contains("damage") || effectType.Contains("speed") || effectType.Contains("chance"))
        {
            return $"{value * 100:F1}%";
        }

        // Format flat values
        return $"{value:F2}";
    }
}

/// <summary>
/// Result of skill XP award operation.
/// </summary>
public class SkillRankUpResult
{
    /// <summary>Gets or sets the skill identifier.</summary>
    public required string SkillId { get; set; }
    /// <summary>Gets or sets the new rank after XP award.</summary>
    public int NewRank { get; set; }
    /// <summary>Gets or sets the number of ranks gained.</summary>
    public int RanksGained { get; set; }
    /// <summary>Gets or sets the list of rank-up notifications.</summary>
    public List<string> Notifications { get; set; } = new();
    /// <summary>Gets a value indicating whether a rank-up occurred.</summary>
    public bool DidRankUp => RanksGained > 0;
}

/// <summary>
/// Display model for skill progress in character sheet.
/// </summary>
public class SkillProgressDisplay
{
    /// <summary>Gets or sets the skill identifier.</summary>
    public required string SkillId { get; set; }
    /// <summary>Gets or sets the skill name.</summary>
    public required string Name { get; set; }
    /// <summary>Gets or sets the skill category.</summary>
    public required string Category { get; set; }
    /// <summary>Gets or sets the current rank.</summary>
    public int CurrentRank { get; set; }
    /// <summary>Gets or sets the current XP.</summary>
    public int CurrentXP { get; set; }
    /// <summary>Gets or sets the XP needed to reach next rank.</summary>
    public int XPToNextRank { get; set; }
    /// <summary>Gets or sets the progress percentage.</summary>
    public double ProgressPercent { get; set; }
    /// <summary>Gets or sets the current effect description.</summary>
    public required string CurrentEffect { get; set; }
    /// <summary>Gets or sets the next rank effect description.</summary>
    public required string NextRankEffect { get; set; }
}
