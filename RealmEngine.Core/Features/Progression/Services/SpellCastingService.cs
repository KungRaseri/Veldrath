using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealmEngine.Core.Features.Progression.Services;

/// <summary>
/// Service for spell casting mechanics, learning spells, and spell effects.
/// Handles mana costs, skill checks, success rates, and spell progression.
/// </summary>
public class SpellCastingService
{
    private readonly PowerDataService _powerCatalog;
    private readonly SkillProgressionService _skillProgression;
    private readonly ILogger<SpellCastingService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpellCastingService"/> class.
    /// </summary>
    /// <param name="powerCatalog">The power catalog service.</param>
    /// <param name="skillProgression">The skill progression service.</param>
    /// <param name="logger">Optional logger instance.</param>
    public SpellCastingService(
        PowerDataService powerCatalog,
        SkillProgressionService skillProgression,
        ILogger<SpellCastingService>? logger = null)
    {
        _powerCatalog = powerCatalog ?? throw new ArgumentNullException(nameof(powerCatalog));
        _skillProgression = skillProgression ?? throw new ArgumentNullException(nameof(skillProgression));
        _logger = logger ?? NullLogger<SpellCastingService>.Instance;
    }

    /// <summary>
    /// Learn a spell from a spellbook.
    /// Checks skill requirements and adds spell to character's learned spells.
    /// </summary>
    public SpellLearningResult LearnSpell(Character character, string spellId)
    {
        var power = _powerCatalog.GetPower(spellId);
        if (power == null)
        {
            return new SpellLearningResult
            {
                Success = false,
                Message = $"Unknown spell: {spellId}"
            };
        }

        // Check if already learned
        if (character.LearnedSpells.ContainsKey(spellId))
        {
            return new SpellLearningResult
            {
                Success = false,
                Message = $"You already know {power.DisplayName}!"
            };
        }

        // Check tradition skill requirement
        if (!power.Tradition.HasValue)
        {
            return new SpellLearningResult
            {
                Success = false,
                Message = $"{power.DisplayName} has no tradition — it cannot be learned from a spellbook."
            };
        }

        var traditionSkillId = _powerCatalog.GetTraditionSkillId(power.Tradition.Value);
        if (!character.Skills.TryGetValue(traditionSkillId, out var traditionSkill))
        {
            return new SpellLearningResult
            {
                Success = false,
                Message = $"You need {power.Tradition} magic skill to learn this spell."
            };
        }

        // Can learn if within reasonable range of skill rank
        if (traditionSkill.CurrentRank + 20 < power.MinimumSkillRank)
        {
            return new SpellLearningResult
            {
                Success = false,
                Message = $"Your {power.Tradition} skill (rank {traditionSkill.CurrentRank}) is too low. Requires rank {power.MinimumSkillRank}."
            };
        }

        // Learn the spell
        character.LearnedSpells[spellId] = new CharacterSpell
        {
            SpellId = spellId,
            LearnedDate = DateTime.UtcNow,
            TimesCast = 0,
            TimesFizzled = 0,
            IsFavorite = false
        };

        _logger.LogInformation("Character {Character} learned spell {Spell}", character.Name, power.DisplayName);

        return new SpellLearningResult
        {
            Success = true,
            Message = $"You have learned {power.DisplayName}!",
            SpellLearned = power
        };
    }

    /// <summary>
    /// Cast a spell in combat.
    /// Checks cooldown, mana cost, skill requirements, and calculates success rate.
    /// </summary>
    public SpellCastResult CastSpell(Character caster, string spellId, Character? target = null)
    {
        // Verify spell is known
        if (!caster.LearnedSpells.TryGetValue(spellId, out var learnedSpell))
        {
            return new SpellCastResult
            {
                Success = false,
                Message = "You don't know that spell!"
            };
        }

        var power = _powerCatalog.GetPower(spellId);
        if (power == null)
        {
            return new SpellCastResult
            {
                Success = false,
                Message = "Spell not found!"
            };
        }

        // Check cooldown
        if (caster.SpellCooldowns.TryGetValue(spellId, out var cooldownRemaining) && cooldownRemaining > 0)
        {
            return new SpellCastResult
            {
                Success = false,
                Message = $"{power.DisplayName} is still cooling down ({cooldownRemaining} turns)."
            };
        }

        // Get magic skill
        if (!power.Tradition.HasValue)
        {
            return new SpellCastResult { Success = false, Message = $"{power.DisplayName} has no tradition!" };
        }

        var traditionSkillId = _powerCatalog.GetTraditionSkillId(power.Tradition.Value);
        if (!caster.Skills.TryGetValue(traditionSkillId, out var magicSkill))
        {
            return new SpellCastResult
            {
                Success = false,
                Message = $"You lack the {power.Tradition} magic skill!"
            };
        }

        // Calculate actual mana cost (reduced by skill)
        var actualManaCost = CalculateManaCost(power, magicSkill);

        // Check mana
        if (caster.Mana < actualManaCost)
        {
            return new SpellCastResult
            {
                Success = false,
                Message = $"Not enough mana! {power.DisplayName} requires {actualManaCost} mana."
            };
        }

        // Consume mana
        caster.Mana -= actualManaCost;

        // Success check (based on skill vs requirement)
        var castCheck = CheckCastSuccess(magicSkill, power);
        
        if (!castCheck.Success)
        {
            // Fizzle - spell fails but mana already spent
            learnedSpell.TimesFizzled++;
            
            _logger.LogInformation("Character {Character} fizzled {Spell} (success rate: {Rate:P0})",
                caster.Name, power.DisplayName, castCheck.SuccessRate);

            return new SpellCastResult
            {
                Success = false,
                Message = $"{power.DisplayName} fizzled! (Success rate was {castCheck.SuccessRate:P0})",
                ManaCostPaid = actualManaCost,
                WasFizzle = true
            };
        }

        // Successful cast - calculate effect value
        var effectValue = CalculateSpellEffect(power, magicSkill, caster);

        // Apply effect
        var effectResult = ApplySpellEffect(power, effectValue, caster, target);

        // Update statistics
        learnedSpell.TimesCast++;

        // Award skill XP
        var xpAmount = CalculateSpellXP(power);
        _skillProgression.AwardSkillXP(caster, traditionSkillId, xpAmount, $"cast_{spellId}");

        // Apply cooldown
        if (power.Cooldown > 0)
        {
            caster.SpellCooldowns[spellId] = power.Cooldown;
        }

        _logger.LogInformation("Character {Character} cast {Spell} for {Effect}",
            caster.Name, power.DisplayName, effectValue);

        return new SpellCastResult
        {
            Success = true,
            Message = effectResult,
            ManaCostPaid = actualManaCost,
            EffectValue = effectValue,
            SpellCast = power
        };
    }

    /// <summary>
    /// Decrease all spell cooldowns by 1 turn.
    /// Call this at the end of each combat turn.
    /// </summary>
    public void DecreaseSpellCooldowns(Character character)
    {
        var cooldownsToRemove = new List<string>();

        foreach (var (spellId, cooldown) in character.SpellCooldowns)
        {
            var newCooldown = cooldown - 1;
            if (newCooldown <= 0)
            {
                cooldownsToRemove.Add(spellId);
            }
            else
            {
                character.SpellCooldowns[spellId] = newCooldown;
            }
        }

        foreach (var spellId in cooldownsToRemove)
        {
            character.SpellCooldowns.Remove(spellId);
        }
    }

    /// <summary>
    /// Calculate mana cost with skill efficiency reduction.
    /// Higher skill = lower mana cost (max 50% reduction at rank 100).
    /// </summary>
    private int CalculateManaCost(Power power, CharacterSkill magicSkill)
    {
        var ranksAboveRequirement = Math.Max(0, magicSkill.CurrentRank - power.MinimumSkillRank);
        var costReduction = Math.Min(0.5, ranksAboveRequirement * 0.005); // -0.5% per rank, max 50%

        return (int)(power.ManaCost * (1.0 - costReduction));
    }

    /// <summary>
    /// Check if spell cast succeeds based on skill.
    /// </summary>
    private CastSuccessResult CheckCastSuccess(CharacterSkill magicSkill, Power power)
    {
        var rankDifference = magicSkill.CurrentRank - power.MinimumSkillRank;

        // Success rate formula:
        // - At minimum rank: 90% success
        // - 20 ranks above: 99% success
        // - 10 ranks below: 60% success (risky but possible)
        var baseSuccessRate = 0.90;
        var successRate = baseSuccessRate + (rankDifference * 0.005); // +0.5% per rank above requirement
        successRate = Math.Clamp(successRate, 0.60, 0.99);

        var roll = Random.Shared.NextDouble();
        var succeeded = roll < successRate;

        return new CastSuccessResult
        {
            Success = succeeded,
            SuccessRate = successRate
        };
    }

    /// <summary>
    /// Calculate spell effect value scaling with skill.
    /// </summary>
    private string CalculateSpellEffect(Power power, CharacterSkill magicSkill, Character caster)
    {
        var baseEffectValue = power.BaseEffectValue ?? "0";
        // If base effect is a number, scale with skill level
        if (int.TryParse(baseEffectValue, out int baseValue))
        {
            // Add skill-based scaling: +1 per 5 skill ranks
            int skillBonus = magicSkill.CurrentRank / 5;
            return (baseValue + skillBonus).ToString();
        }
        
        // If it's dice notation, return as-is for now (will be rolled in ApplySpellEffect)
        return baseEffectValue;
    }

    /// <summary>
    /// Parse dice notation (e.g., "2d6") or numeric value.
    /// </summary>
    private int ParseDiceOrValue(string value)
    {
        // If it's a plain number, return it
        if (int.TryParse(value, out int numValue))
        {
            return numValue;
        }
        
        // If it's dice notation, roll it (using same logic as CombatService)
        try
        {
            var parts = value.ToLower().Split('d');
            if (parts.Length == 2)
            {
                int count = int.Parse(parts[0].Trim());
                int sides = int.Parse(parts[1].Trim());
                int total = 0;
                for (int i = 0; i < count; i++)
                {
                    total += Random.Shared.Next(1, sides + 1);
                }
                return total;
            }
        }
        catch
        {
            // If parsing fails, return a default value
        }
        
        return 10; // Default fallback
    }

    /// <summary>
    /// Apply spell effect to target.
    /// </summary>
    private string ApplySpellEffect(Power power, string effectValue, Character caster, Character? target)
    {
        switch (power.EffectType)
        {
            case PowerEffectType.Damage:
                if (target != null)
                {
                    // Parse dice notation or use direct value
                    int damage = ParseDiceOrValue(effectValue);
                    target.Health = Math.Max(0, target.Health - damage);
                    return $"Dealt {damage} {power.DamageType ?? "magic"} damage to {target.Name}!";
                }
                return "No valid target!";

            case PowerEffectType.Heal:
                // Parse dice notation or use direct value
                int healing = ParseDiceOrValue(effectValue);
                int maxHealth = caster.GetMaxHealth();
                int actualHealing = Math.Min(healing, maxHealth - caster.Health);
                caster.Health = Math.Min(maxHealth, caster.Health + healing);
                return $"Restored {actualHealing} health!";

            case PowerEffectType.Buff:
                return $"Applied {power.DisplayName} buff!";

            case PowerEffectType.Debuff:
                if (target != null)
                {
                    return $"Applied {power.DisplayName} debuff to {target.Name}!";
                }
                return "No valid target!";

            default:
                return $"Cast {power.DisplayName}!";
        }
    }

    /// <summary>
    /// Calculate spell XP award based on rank.
    /// </summary>
    private int CalculateSpellXP(Power power)
    {
        return power.Rank switch
        {
            0 => 5,   // Cantrips
            1 => 8,
            2 => 10,
            3 => 12,
            4 => 15,
            5 => 18,
            6 => 22,
            7 => 26,
            8 => 30,
            9 => 35,
            10 => 40,
            _ => 8
        };
    }
}

/// <summary>
/// Result of spell learning attempt.
/// </summary>
public class SpellLearningResult
{
    /// <summary>Gets or sets a value indicating whether the learning was successful.</summary>
    public bool Success { get; set; }
    /// <summary>Gets or sets the result message.</summary>
    public required string Message { get; set; }
    /// <summary>Gets or sets the spell (power) that was learned.</summary>
    public Power? SpellLearned { get; set; }
}

/// <summary>
/// Result of spell casting attempt.
/// </summary>
public class SpellCastResult
{
    /// <summary>Gets or sets a value indicating whether the cast was successful.</summary>
    public bool Success { get; set; }
    /// <summary>Gets or sets the result message.</summary>
    public required string Message { get; set; }
    /// <summary>Gets or sets the mana cost paid.</summary>
    public int ManaCostPaid { get; set; }
    /// <summary>Gets or sets the effect value.</summary>
    public string EffectValue { get; set; } = string.Empty;
    /// <summary>Gets or sets a value indicating whether the spell fizzled.</summary>
    public bool WasFizzle { get; set; }
    /// <summary>Gets or sets the spell (power) that was cast.</summary>
    public Power? SpellCast { get; set; }
}

/// <summary>
/// Internal result of cast success check.
/// </summary>
internal class CastSuccessResult
{
    public bool Success { get; set; }
    public double SuccessRate { get; set; }
}
