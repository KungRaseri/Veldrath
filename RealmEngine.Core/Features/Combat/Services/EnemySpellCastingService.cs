using RealmEngine.Shared.Models;
using RealmEngine.Core.Features.Progression.Services;

namespace RealmEngine.Core.Features.Combat.Services;

/// <summary>
/// Service for determining when and which spells enemies should cast in combat.
/// </summary>
public class EnemySpellCastingService
{
    private readonly SpellDataService? _spellCatalogService;
    private readonly Random _random;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnemySpellCastingService"/> class.
    /// </summary>
    /// <param name="spellCatalogService">The spell catalog service.</param>
    public EnemySpellCastingService(SpellDataService? spellCatalogService = null)
    {
        _spellCatalogService = spellCatalogService;
        _random = new Random();
    }

    /// <summary>
    /// Decides whether an enemy should cast a spell this turn, and if so, which one.
    /// </summary>
    /// <param name="enemy">The enemy making the decision</param>
    /// <param name="player">The target player character</param>
    /// <returns>Spell ID to cast, or null if should use basic attack/ability</returns>
    public string? DecideSpellCasting(Enemy enemy, Character player)
    {
        if (_spellCatalogService == null || enemy.SpellIds.Count == 0)
        {
            return null; // No spells available or catalog not loaded
        }

        // Check if enemy has mana
        if (enemy.Mana <= 0)
        {
            return null; // No mana to cast spells
        }

        // Get available spells (not on cooldown and within mana budget)
        var availableSpells = enemy.SpellIds
            .Select(id => _spellCatalogService.GetSpell(id))
            .Where(spell => spell != null && 
                           (!enemy.SpellCooldowns.ContainsKey(spell.SpellId) || enemy.SpellCooldowns[spell.SpellId] == 0) &&
                           spell.ManaCost <= enemy.Mana)
            .ToList();

        if (availableSpells.Count == 0)
        {
            return null; // All spells on cooldown or not enough mana
        }

        // Calculate enemy's current health percentage
        double healthPercent = (double)enemy.Health / enemy.MaxHealth * 100;
        double manaPercent = (double)enemy.Mana / enemy.MaxMana * 100;

        // Create priority list for spells based on current situation
        var spellPriorities = new List<(Spell spell, int priority)>();

        foreach (var spell in availableSpells)
        {
            if (spell == null) continue;

            int priority = 0;

            // Healing spells when health is low
            if (healthPercent < 30 && spell.EffectType == SpellEffectType.Heal)
            {
                priority = 80; // Highest priority
            }
            // Defensive/buff spells when health is moderate
            else if (healthPercent < 60 && (spell.EffectType == SpellEffectType.Buff || spell.EffectType == SpellEffectType.Protection))
            {
                priority = 60; // High priority
            }
            // Offensive spells when health is good
            else if (healthPercent > 50 && spell.EffectType == SpellEffectType.Damage)
            {
                // Prefer higher damage spells
                if (spell.Rank >= 3)
                {
                    priority = 50; // Medium-high priority
                }
                else
                {
                    priority = 30; // Medium priority
                }
            }
            // Debuff spells when player is strong
            else if (player.Health > player.MaxHealth * 0.7 && spell.EffectType == SpellEffectType.Debuff)
            {
                priority = 45; // Medium priority
            }
            // Utility spells (summons, control) in favorable situations
            else if (healthPercent > 60 && manaPercent > 50 && 
                    (spell.EffectType == SpellEffectType.Summon || spell.EffectType == SpellEffectType.Control))
            {
                priority = 40; // Medium priority
            }
            else
            {
                priority = 20; // Low priority fallback
            }

            // Adjust priority based on mana efficiency
            double manaEfficiency = (double)spell.ManaCost / enemy.MaxMana * 100;
            if (manaPercent < 30 && manaEfficiency > 20)
            {
                priority /= 2; // Reduce priority for expensive spells when low on mana
            }

            spellPriorities.Add((spell, priority));
        }

        // Select spell based on priorities (higher priority = more likely to be selected)
        foreach (var (spell, priority) in spellPriorities.OrderByDescending(x => x.priority))
        {
            if (_random.Next(100) < priority)
            {
                return spell.SpellId;
            }
        }

        // If no spell was selected, 15% chance to cast random spell as fallback
        if (availableSpells.Count > 0 && _random.Next(100) < 15)
        {
            int randomIndex = _random.Next(availableSpells.Count);
            return availableSpells[randomIndex]?.SpellId;
        }

        // Default: don't cast a spell this turn
        return null;
    }

    /// <summary>
    /// Checks if the enemy should prioritize spell casting over physical attacks.
    /// Spellcasters are more likely to use spells.
    /// </summary>
    /// <param name="enemy">The enemy to evaluate</param>
    /// <returns>True if enemy should prefer casting spells</returns>
    public bool ShouldPreferSpellCasting(Enemy enemy)
    {
        // Enemies with high Intelligence are natural spellcasters
        if (enemy.Intelligence > enemy.Strength + 5)
        {
            return true;
        }

        // Enemies with more spells than physical damage are casters
        if (enemy.SpellIds.Count > 3 && enemy.BasePhysicalDamage < 20)
        {
            return true;
        }

        // Enemies with high mana pools are designed for casting
        if (enemy.MaxMana > 100)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Calculates the final mana cost for a spell cast by an enemy.
    /// May be modified by enemy's Intelligence or traits.
    /// </summary>
    /// <param name="spell">The spell being cast</param>
    /// <param name="enemy">The enemy casting the spell</param>
    /// <returns>Actual mana cost</returns>
    public int CalculateManaCost(Spell spell, Enemy enemy)
    {
        int baseCost = spell.ManaCost;
        
        // High Intelligence reduces mana costs
        double intelligenceReduction = Math.Max(0, (enemy.Intelligence - 10) * 0.01); // 1% per point above 10
        int finalCost = (int)(baseCost * (1.0 - intelligenceReduction));
        
        return Math.Max(1, finalCost); // Minimum cost of 1 mana
    }
}
