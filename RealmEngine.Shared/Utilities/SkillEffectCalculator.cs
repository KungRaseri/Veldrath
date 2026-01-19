using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Utilities;

/// <summary>
/// Utility class for calculating skill effects and bonuses.
/// </summary>
public static class SkillEffectCalculator
{
    /// <summary>
    /// Get total physical damage bonus from skills.
    /// Uses new Skills dictionary (v4.2).
    /// </summary>
    public static double GetPhysicalDamageMultiplier(Character character, string? weaponSkillSlug = null)
    {
        double multiplier = 1.0;

        // Apply weapon skill damage multiplier
        if (!string.IsNullOrEmpty(weaponSkillSlug) && character.Skills.TryGetValue(weaponSkillSlug, out var weaponSkill))
        {
            // Most weapon skills give 0.005 damage multiplier per rank (0.5% per rank)
            // At rank 100, that's +50% damage
            multiplier += (weaponSkill.CurrentRank * 0.005);
        }

        return multiplier;
    }

    /// <summary>
    /// Get total magic damage bonus from skills.
    /// Uses new Skills dictionary (v4.2).
    /// </summary>
    public static double GetMagicDamageMultiplier(Character character, string? magicSkillSlug = null)
    {
        double multiplier = 1.0;

        // Apply magic tradition skill multiplier
        if (!string.IsNullOrEmpty(magicSkillSlug) && character.Skills.TryGetValue(magicSkillSlug, out var magicSkill))
        {
            // Magic skills typically give 0.008 damage multiplier per rank
            // At rank 100, that's +80% damage
            multiplier += (magicSkill.CurrentRank * 0.008);
        }

        return multiplier;
    }

    /// <summary>
    /// Get total critical hit chance bonus from skills.
    /// Uses new Skills dictionary (v4.2).
    /// </summary>
    public static double GetCriticalChanceBonus(Character character)
    {
        double bonus = 0.0;

        // Check for precision skill
        if (character.Skills.TryGetValue("precision", out var precision))
        {
            // Precision gives +0.5% crit per rank, maxing at +50% at rank 100
            bonus += (precision.CurrentRank * 0.5);
        }

        return bonus;
    }

    /// <summary>
    /// Get total physical defense bonus from skills.
    /// Uses new Skills dictionary (v4.2).
    /// </summary>
    public static double GetPhysicalDefenseMultiplier(Character character)
    {
        double multiplier = 1.0;

        // Check for armor skill and block skill
        if (character.Skills.TryGetValue("armor", out var armorSkill))
        {
            // Armor skill gives +0.003 defense multiplier per rank
            // At rank 100, that's +30% damage reduction
            multiplier += (armorSkill.CurrentRank * 0.003);
        }

        if (character.Skills.TryGetValue("block", out var blockSkill))
        {
            // Block skill gives additional +0.002 defense when blocking
            multiplier += (blockSkill.CurrentRank * 0.002);
        }

        return multiplier;
    }

    /// <summary>
    /// Get total dodge chance bonus from skills.
    /// Uses new Skills dictionary (v4.2).
    /// </summary>
    public static double GetDodgeChanceBonus(Character character)
    {
        double bonus = 0.0;

        // Check for acrobatics and light-armor skills
        if (character.Skills.TryGetValue("acrobatics", out var acrobatics))
        {
            // Acrobatics gives +0.3% dodge per rank, maxing at +30% at rank 100
            bonus += (acrobatics.CurrentRank * 0.3);
        }

        if (character.Skills.TryGetValue("light-armor", out var lightArmor))
        {
            // Light armor skill gives +0.1% dodge per rank
            bonus += (lightArmor.CurrentRank * 0.1);
        }

        return bonus;
    }

    /// <summary>
    /// Get total rare item find bonus from skills.
    /// Uses new Skills dictionary (v4.2).
    /// </summary>
    public static double GetRareItemFindBonus(Character character)
    {
        double bonus = 0.0;

        // Check for luck skill
        if (character.Skills.TryGetValue("luck", out var luck))
        {
            // Luck gives +0.5% rare find per rank, maxing at +50% at rank 100
            bonus += (luck.CurrentRank * 0.5);
        }

        return bonus;
    }

    /// <summary>
    /// Get total max mana bonus from skills.
    /// Uses new Skills dictionary (v4.2).
    /// </summary>
    public static double GetMaxManaMultiplier(Character character)
    {
        double multiplier = 1.0;

        // Check for mana pool skill
        if (character.Skills.TryGetValue("mana-pool", out var manaPool))
        {
            // Mana pool skill gives +0.01 multiplier per rank
            // At rank 100, that's +100% max mana (double)
            multiplier += (manaPool.CurrentRank * 0.01);
        }

        return multiplier;
    }

    /// <summary>
    /// Get health regeneration per turn from skills.
    /// Uses new Skills dictionary (v4.2).
    /// </summary>
    public static int GetHealthRegeneration(Character character)
    {
        int regen = 0;

        // Check for vitality skill
        if (character.Skills.TryGetValue("vitality", out var vitality))
        {
            // Vitality gives +0.5 HP regen per rank, rounded down
            // At rank 100, that's +50 HP per turn
            regen += (int)(vitality.CurrentRank * 0.5);
        }

        return regen;
    }

    /// <summary>
    /// Apply regeneration effect to character.
    /// </summary>
    public static int ApplyRegeneration(Character character)
    {
        var regenAmount = GetHealthRegeneration(character);

        if (regenAmount > 0 && character.Health < character.MaxHealth)
        {
            var actualRegen = Math.Min(regenAmount, character.MaxHealth - character.Health);
            character.Health += actualRegen;
            return actualRegen;
        }

        return 0;
    }

    /// <summary>
    /// Get a summary of all active skill bonuses.
    /// </summary>
    public static string GetSkillBonusSummary(Character character)
    {
        var summary = new List<string>();

        var physDmg = GetPhysicalDamageMultiplier(character);
        if (physDmg > 1.0)
            summary.Add($"[green]+{(physDmg - 1.0) * 100:F0}% Physical Damage[/]");

        var magicDmg = GetMagicDamageMultiplier(character);
        if (magicDmg > 1.0)
            summary.Add($"[cyan]+{(magicDmg - 1.0) * 100:F0}% Magic Damage[/]");

        var critBonus = GetCriticalChanceBonus(character);
        if (critBonus > 0)
            summary.Add($"[yellow]+{critBonus:F0}% Critical Chance[/]");

        var defMulti = GetPhysicalDefenseMultiplier(character);
        if (defMulti > 1.0)
            summary.Add($"[blue]+{(defMulti - 1.0) * 100:F0}% Physical Defense[/]");

        var dodgeBonus = GetDodgeChanceBonus(character);
        if (dodgeBonus > 0)
            summary.Add($"[magenta]+{dodgeBonus:F0}% Dodge Chance[/]");

        var lootBonus = GetRareItemFindBonus(character);
        if (lootBonus > 0)
            summary.Add($"[gold1]+{lootBonus:F0}% Rare Item Find[/]");

        var manaMulti = GetMaxManaMultiplier(character);
        if (manaMulti > 1.0)
            summary.Add($"[blue]+{(manaMulti - 1.0) * 100:F0}% Max Mana[/]");

        var regen = GetHealthRegeneration(character);
        if (regen > 0)
            summary.Add($"[green]+{regen} HP Regeneration per turn[/]");

        return summary.Any() ? string.Join("\n", summary) : "[dim]No active skill bonuses[/]";
    }

    /// <summary>
    /// Get the skill reference from an item's traits.
    /// Items should have a skillReference trait pointing to the skill they use (e.g., "@skills/weapon:light-blades").
    /// </summary>
    /// <param name="item">The item to get the skill reference from</param>
    /// <returns>Skill slug (extracted from reference) or null if no skill reference found</returns>
    public static string? GetSkillSlugFromItem(Item? item)
    {
        if (item == null)
            return null;

        // Check for skillReference trait (JSON reference format: @skills/weapon:light-blades)
        if (item.Traits.TryGetValue("skillReference", out var skillRef) && 
            skillRef.Type == TraitType.String)
        {
            var reference = skillRef.AsString();
            if (!string.IsNullOrEmpty(reference))
            {
                // Extract slug from reference: @skills/weapon:light-blades -> light-blades
                var colonIndex = reference.IndexOf(':');
                if (colonIndex > 0 && colonIndex < reference.Length - 1)
                {
                    return reference.Substring(colonIndex + 1);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Get skill rank progress as percentage (0-100).
    /// Useful for progress bars in UI.
    /// </summary>
    /// <param name="currentRank">Current skill rank</param>
    /// <param name="maxRank">Maximum rank (default: 100)</param>
    /// <returns>Progress percentage</returns>
    public static double GetRankProgress(int currentRank, int maxRank = 100)
    {
        if (maxRank <= 0) return 0.0;
        return Math.Min((double)currentRank / maxRank * 100.0, 100.0);
    }

    /// <summary>
    /// Get a formatted damage multiplier display.
    /// Example: "1.25x" or "+25%"
    /// </summary>
    /// <param name="multiplier">The damage multiplier (e.g., 1.25)</param>
    /// <param name="asPercentage">If true, shows as "+25%", otherwise "1.25x"</param>
    /// <returns>Formatted string for UI</returns>
    public static string FormatMultiplier(double multiplier, bool asPercentage = false)
    {
        if (asPercentage)
        {
            var percent = (multiplier - 1.0) * 100.0;
            var sign = percent >= 0 ? "+" : "";
            return $"{sign}{percent:F1}%";
        }
        return $"{multiplier:F2}x";
    }

    /// <summary>
    /// Get all active skill bonuses as a dictionary.
    /// Useful for Godot UI data binding.
    /// </summary>
    /// <param name="character">The character</param>
    /// <returns>Dictionary of stat name to bonus value</returns>
    public static Dictionary<string, double> GetAllBonuses(Character character)
    {
        return new Dictionary<string, double>
        {
            ["PhysicalDamage"] = GetPhysicalDamageMultiplier(character) - 1.0,
            ["MagicDamage"] = GetMagicDamageMultiplier(character) - 1.0,
            ["CriticalChance"] = GetCriticalChanceBonus(character),
            ["PhysicalDefense"] = GetPhysicalDefenseMultiplier(character) - 1.0,
            ["DodgeChance"] = GetDodgeChanceBonus(character),
            ["RareItemFind"] = GetRareItemFindBonus(character),
            ["MaxMana"] = GetMaxManaMultiplier(character) - 1.0,
            ["HealthRegen"] = GetHealthRegeneration(character)
        };
    }

    /// <summary>
    /// Get top 3 skill bonuses for compact UI display.
    /// </summary>
    /// <param name="character">The character</param>
    /// <param name="count">Number of top bonuses to return</param>
    /// <returns>List of (name, value) tuples sorted by impact</returns>
    public static List<(string Name, double Value)> GetTopBonuses(Character character, int count = 3)
    {
        var bonuses = GetAllBonuses(character);
        return bonuses
            .Where(b => Math.Abs(b.Value) > 0.001) // Filter near-zero
            .OrderByDescending(b => Math.Abs(b.Value))
            .Take(count)
            .Select(b => (b.Key, b.Value))
            .ToList();
    }

    /// <summary>
    /// Calculate effective damage after skill bonuses.
    /// </summary>
    /// <param name="baseDamage">Base damage value</param>
    /// <param name="character">The character</param>
    /// <param name="weaponSkillSlug">Weapon skill slug (optional)</param>
    /// <returns>Final damage after multipliers</returns>
    public static int CalculateEffectiveDamage(int baseDamage, Character character, string? weaponSkillSlug = null)
    {
        var multiplier = GetPhysicalDamageMultiplier(character, weaponSkillSlug);
        return (int)Math.Round(baseDamage * multiplier);
    }

    /// <summary>
    /// Calculate effective defense after skill bonuses.
    /// </summary>
    /// <param name="baseDefense">Base defense value</param>
    /// <param name="character">The character</param>
    /// <returns>Final defense after multipliers</returns>
    public static int CalculateEffectiveDefense(int baseDefense, Character character)
    {
        var multiplier = GetPhysicalDefenseMultiplier(character);
        return (int)Math.Round(baseDefense * multiplier);
    }

    /// <summary>
    /// Get BBCode formatted skill summary for Godot RichTextLabel.
    /// </summary>
    /// <param name="character">The character</param>
    /// <returns>BBCode formatted string</returns>
    public static string GetSkillBonusSummaryBBCode(Character character)
    {
        var bonuses = GetAllBonuses(character);
        var lines = new List<string>();

        if (bonuses["PhysicalDamage"] > 0)
            lines.Add($"[color=green]+{bonuses["PhysicalDamage"] * 100:F0}% Physical Damage[/color]");

        if (bonuses["MagicDamage"] > 0)
            lines.Add($"[color=cyan]+{bonuses["MagicDamage"] * 100:F0}% Magic Damage[/color]");

        if (bonuses["CriticalChance"] > 0)
            lines.Add($"[color=yellow]+{bonuses["CriticalChance"]:F0}% Critical Chance[/color]");

        if (bonuses["PhysicalDefense"] > 0)
            lines.Add($"[color=blue]+{bonuses["PhysicalDefense"] * 100:F0}% Physical Defense[/color]");

        if (bonuses["DodgeChance"] > 0)
            lines.Add($"[color=magenta]+{bonuses["DodgeChance"]:F0}% Dodge Chance[/color]");

        if (bonuses["RareItemFind"] > 0)
            lines.Add($"[color=orange]+{bonuses["RareItemFind"]:F0}% Rare Item Find[/color]");

        if (bonuses["MaxMana"] > 0)
            lines.Add($"[color=blue]+{bonuses["MaxMana"] * 100:F0}% Max Mana[/color]");

        if (bonuses["HealthRegen"] > 0)
            lines.Add($"[color=green]+{bonuses["HealthRegen"]:F0} HP Regeneration per turn[/color]");

        return lines.Any() ? string.Join("\n", lines) : "[color=gray]No active skill bonuses[/color]";
    }

    /// <summary>
    /// Get skill efficiency rating (0-100).
    /// Measures how well skills are distributed across categories.
    /// </summary>
    /// <param name="character">The character</param>
    /// <returns>Efficiency score (100 = perfectly balanced)</returns>
    public static double GetSkillEfficiencyRating(Character character)
    {
        if (!character.Skills.Any()) return 0.0;

        var totalRanks = character.Skills.Values.Sum(s => s.CurrentRank);
        var avgRank = totalRanks / (double)character.Skills.Count;
        var variance = character.Skills.Values.Average(s => Math.Pow(s.CurrentRank - avgRank, 2));
        var stdDev = Math.Sqrt(variance);

        // Lower std dev = more balanced = higher efficiency
        // Perfect balance (all skills equal) = 100
        var efficiency = Math.Max(0, 100 - (stdDev / avgRank * 50));
        return Math.Min(efficiency, 100.0);
    }
}
