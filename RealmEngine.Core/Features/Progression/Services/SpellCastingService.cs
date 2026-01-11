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
    private readonly SpellCatalogService _spellCatalog;
    private readonly SkillProgressionService _skillProgression;
    private readonly ILogger<SpellCastingService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpellCastingService"/> class.
    /// </summary>
    /// <param name="spellCatalog">The spell catalog service.</param>
    /// <param name="skillProgression">The skill progression service.</param>
    /// <param name="logger">Optional logger instance.</param>
    public SpellCastingService(
        SpellCatalogService spellCatalog,
        SkillProgressionService skillProgression,
        ILogger<SpellCastingService>? logger = null)
    {
        _spellCatalog = spellCatalog ?? throw new ArgumentNullException(nameof(spellCatalog));
        _skillProgression = skillProgression ?? throw new ArgumentNullException(nameof(skillProgression));
        _logger = logger ?? NullLogger<SpellCastingService>.Instance;
    }

    /// <summary>
    /// Learn a spell from a spellbook.
    /// Checks skill requirements and adds spell to character's learned spells.
    /// </summary>
    public SpellLearningResult LearnSpell(Character character, string spellId)
    {
        var spell = _spellCatalog.GetSpell(spellId);
        if (spell == null)
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
                Message = $"You already know {spell.DisplayName}!"
            };
        }

        // Check tradition skill requirement
        var traditionSkillId = _spellCatalog.GetTraditionSkillId(spell.Tradition);
        if (!character.Skills.TryGetValue(traditionSkillId, out var traditionSkill))
        {
            return new SpellLearningResult
            {
                Success = false,
                Message = $"You need {spell.Tradition} magic skill to learn this spell."
            };
        }

        // Can learn if within reasonable range of skill rank
        if (traditionSkill.CurrentRank + 20 < spell.MinimumSkillRank)
        {
            return new SpellLearningResult
            {
                Success = false,
                Message = $"Your {spell.Tradition} skill (rank {traditionSkill.CurrentRank}) is too low. Requires rank {spell.MinimumSkillRank}."
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

        _logger.LogInformation("Character {Character} learned spell {Spell}", character.Name, spell.DisplayName);

        return new SpellLearningResult
        {
            Success = true,
            Message = $"You have learned {spell.DisplayName}!",
            SpellLearned = spell
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

        var spell = _spellCatalog.GetSpell(spellId);
        if (spell == null)
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
                Message = $"{spell.DisplayName} is still cooling down ({cooldownRemaining} turns)."
            };
        }

        // Get magic skill
        var traditionSkillId = _spellCatalog.GetTraditionSkillId(spell.Tradition);
        if (!caster.Skills.TryGetValue(traditionSkillId, out var magicSkill))
        {
            return new SpellCastResult
            {
                Success = false,
                Message = $"You lack the {spell.Tradition} magic skill!"
            };
        }

        // Calculate actual mana cost (reduced by skill)
        var actualManaCost = CalculateManaCost(spell, magicSkill);

        // Check mana
        if (caster.Mana < actualManaCost)
        {
            return new SpellCastResult
            {
                Success = false,
                Message = $"Not enough mana! {spell.DisplayName} requires {actualManaCost} mana."
            };
        }

        // Consume mana
        caster.Mana -= actualManaCost;

        // Success check (based on skill vs requirement)
        var castCheck = CheckCastSuccess(magicSkill, spell);
        
        if (!castCheck.Success)
        {
            // Fizzle - spell fails but mana already spent
            learnedSpell.TimesFizzled++;
            
            _logger.LogInformation("Character {Character} fizzled {Spell} (success rate: {Rate:P0})",
                caster.Name, spell.DisplayName, castCheck.SuccessRate);

            return new SpellCastResult
            {
                Success = false,
                Message = $"{spell.DisplayName} fizzled! (Success rate was {castCheck.SuccessRate:P0})",
                ManaCostPaid = actualManaCost,
                WasFizzle = true
            };
        }

        // Successful cast - calculate effect value
        var effectValue = CalculateSpellEffect(spell, magicSkill, caster);

        // Apply effect
        var effectResult = ApplySpellEffect(spell, effectValue, caster, target);

        // Update statistics
        learnedSpell.TimesCast++;

        // Award skill XP
        var xpAmount = CalculateSpellXP(spell);
        _skillProgression.AwardSkillXP(caster, traditionSkillId, xpAmount, $"cast_{spellId}");

        // Apply cooldown
        if (spell.Cooldown > 0)
        {
            caster.SpellCooldowns[spellId] = spell.Cooldown;
        }

        _logger.LogInformation("Character {Character} cast {Spell} for {Effect}",
            caster.Name, spell.DisplayName, effectValue);

        return new SpellCastResult
        {
            Success = true,
            Message = effectResult,
            ManaCostPaid = actualManaCost,
            EffectValue = effectValue,
            SpellCast = spell
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
    private int CalculateManaCost(Spell spell, CharacterSkill magicSkill)
    {
        var ranksAboveRequirement = Math.Max(0, magicSkill.CurrentRank - spell.MinimumSkillRank);
        var costReduction = Math.Min(0.5, ranksAboveRequirement * 0.005); // -0.5% per rank, max 50%

        return (int)(spell.ManaCost * (1.0 - costReduction));
    }

    /// <summary>
    /// Check if spell cast succeeds based on skill.
    /// </summary>
    private CastSuccessResult CheckCastSuccess(CharacterSkill magicSkill, Spell spell)
    {
        var rankDifference = magicSkill.CurrentRank - spell.MinimumSkillRank;

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
    private string CalculateSpellEffect(Spell spell, CharacterSkill magicSkill, Character caster)
    {
        // If base effect is a number, scale with skill level
        if (int.TryParse(spell.BaseEffectValue, out int baseValue))
        {
            // Add skill-based scaling: +1 per 5 skill ranks
            int skillBonus = magicSkill.CurrentRank / 5;
            return (baseValue + skillBonus).ToString();
        }
        
        // If it's dice notation, return as-is for now (will be rolled in ApplySpellEffect)
        return spell.BaseEffectValue;
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
    private string ApplySpellEffect(Spell spell, string effectValue, Character caster, Character? target)
    {
        switch (spell.EffectType)
        {
            case SpellEffectType.Damage:
                if (target != null)
                {
                    // Parse dice notation or use direct value
                    int damage = ParseDiceOrValue(effectValue);
                    target.Health = Math.Max(0, target.Health - damage);
                    return $"Dealt {damage} {spell.Traits.GetValueOrDefault("damageType", "magic")} damage to {target.Name}!";
                }
                return "No valid target!";

            case SpellEffectType.Heal:
                // Parse dice notation or use direct value
                int healing = ParseDiceOrValue(effectValue);
                int maxHealth = caster.GetMaxHealth();
                int actualHealing = Math.Min(healing, maxHealth - caster.Health);
                caster.Health = Math.Min(maxHealth, caster.Health + healing);
                return $"Restored {actualHealing} health!";

            case SpellEffectType.Buff:
                return $"Applied {spell.DisplayName} buff!";

            case SpellEffectType.Debuff:
                if (target != null)
                {
                    return $"Applied {spell.DisplayName} debuff to {target.Name}!";
                }
                return "No valid target!";

            default:
                return $"Cast {spell.DisplayName}!";
        }
    }

    /// <summary>
    /// Calculate spell XP award based on rank.
    /// </summary>
    private int CalculateSpellXP(Spell spell)
    {
        return spell.Rank switch
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
    /// <summary>Gets or sets the spell that was learned.</summary>
    public Spell? SpellLearned { get; set; }
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
    /// <summary>Gets or sets the spell that was cast.</summary>
    public Spell? SpellCast { get; set; }
}

/// <summary>
/// Internal result of cast success check.
/// </summary>
internal class CastSuccessResult
{
    public bool Success { get; set; }
    public double SuccessRate { get; set; }
}
