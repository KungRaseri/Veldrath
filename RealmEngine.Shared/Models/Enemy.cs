namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents an enemy combatant.
/// </summary>
public class Enemy : ITraitable
{
    /// <summary>
    /// Gets or sets the unique identifier for this enemy.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Gets or sets the URL-safe identifier for this enemy (kebab-case).
    /// Used for lookups, references, and catalog identification.
    /// </summary>
    public string Slug { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the display name of the enemy.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the base name of the enemy without modifiers (e.g., "Wolf", "Dragon").
    /// </summary>
    public string BaseName { get; set; } = string.Empty;
    
    /// <summary>
    /// Ordered list of prefix components (size, type, descriptive) that appear before the base name.
    /// Each component preserves its token identifier and display value.
    /// </summary>
    public List<NameComponent> Prefixes { get; set; } = new();
    
    /// <summary>
    /// Ordered list of suffix components (titles) that appear after the base name.
    /// Each component preserves its token identifier and display value.
    /// </summary>
    public List<NameComponent> Suffixes { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the descriptive text for the enemy.
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the base attribute bonuses from JSON catalog.
    /// Maps to attributes field in JSON (strength, dexterity, constitution, intelligence, wisdom, charisma).
    /// </summary>
    public Dictionary<string, int> Attributes { get; set; } = new();

    /// <summary>
    /// Gets or sets the trait system dictionary for dynamic properties.
    /// Implements ITraitable interface.
    /// </summary>
    public Dictionary<string, TraitValue> Traits { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the formula-based combat stats for this enemy.
    /// Contains attack, defense, speed, and health formulas from JSON.
    /// Keys: "attack", "defense", "speed", "health" with string formulas as values.
    /// Example: "attack": "strength_mod + level + 8"
    /// </summary>
    public Dictionary<string, string> Stats { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the combat configuration for this enemy.
    /// Contains ability references and level-based unlock data.
    /// </summary>
    public EnemyCombat? Combat { get; set; }

    /// <summary>
    /// Gets or sets the enemy's current level.
    /// </summary>
    public int Level { get; set; } = 1;
    
    /// <summary>
    /// Gets or sets the enemy's current health points.
    /// </summary>
    public int Health { get; set; } = 50;
    
    /// <summary>
    /// Gets or sets the enemy's maximum health points.
    /// </summary>
    public int MaxHealth { get; set; } = 50;

    /// <summary>
    /// Gets or sets the enemy's current mana points (for spellcasting enemies).
    /// </summary>
    public int Mana { get; set; } = 0;
    
    /// <summary>
    /// Gets or sets the enemy's maximum mana points.
    /// </summary>
    public int MaxMana { get; set; } = 0;

    /// <summary>
    /// Gets or sets the Strength attribute. Affects melee damage and carry weight.
    /// </summary>
    public int Strength { get; set; } = 10;
    
    /// <summary>
    /// Gets or sets the Dexterity attribute. Affects dodge, accuracy, and critical hit chance.
    /// </summary>
    public int Dexterity { get; set; } = 10;
    
    /// <summary>
    /// Gets or sets the Constitution attribute. Affects max HP and physical defense.
    /// </summary>
    public int Constitution { get; set; } = 10;
    
    /// <summary>
    /// Gets or sets the Intelligence attribute. Affects magic damage and crafting.
    /// </summary>
    public int Intelligence { get; set; } = 10;
    
    /// <summary>
    /// Gets or sets the Wisdom attribute. Affects max mana and magic defense.
    /// </summary>
    public int Wisdom { get; set; } = 10;
    
    /// <summary>
    /// Gets or sets the Charisma attribute. Affects NPC interactions and rare loot drops.
    /// </summary>
    public int Charisma { get; set; } = 10;

    /// <summary>
    /// Gets or sets the base physical damage dealt by this enemy.
    /// </summary>
    public int BasePhysicalDamage { get; set; } = 5;
    
    /// <summary>
    /// Gets or sets the base magic damage dealt by this enemy.
    /// </summary>
    public int BaseMagicDamage { get; set; } = 0;
    
    /// <summary>
    /// Gets or sets the experience points rewarded when this enemy is defeated.
    /// Maps to "xp" field in JSON catalogs.
    /// </summary>
    public int XP { get; set; } = 25;
    
    /// <summary>
    /// Gets or sets the gold rewarded when this enemy is defeated.
    /// </summary>
    public int GoldReward { get; set; } = 10;

    /// <summary>
    /// Gets or sets the classification type of this enemy (Common, Elite, Boss, etc.).
    /// </summary>
    public EnemyType Type { get; set; } = EnemyType.Common;
    
    /// <summary>
    /// Gets or sets the difficulty rating of this enemy (Easy, Medium, Hard, etc.).
    /// </summary>
    public EnemyDifficulty Difficulty { get; set; } = EnemyDifficulty.Easy;

    /// <summary>
    /// Collection of ability reference IDs (v4.1 format) this enemy can use in combat.
    /// Each ID is a JSON reference like "@abilities/active/offensive:fireball".
    /// </summary>
    /// <remarks>
    /// <para><strong>✅ HOW TO RESOLVE - Use ReferenceResolverService:</strong></para>
    /// <code>
    /// // C# - Resolve each reference to a full Ability object
    /// var resolver = new ReferenceResolverService(dataCache);
    /// var abilities = new List&lt;Ability&gt;();
    /// foreach (var refId in enemy.AbilityIds)
    /// {
    ///     var abilityJson = await resolver.ResolveToObjectAsync(refId);
    ///     var ability = abilityJson.ToObject&lt;Ability&gt;();
    ///     abilities.Add(ability);
    /// }
    /// // Now use abilities in combat AI
    /// </code>
    /// <code>
    /// // GDScript - Resolve references in Godot
    /// var resolver = ReferenceResolverService.new(data_cache)
    /// var abilities = []
    /// for ref_id in enemy.AbilityIds:
    ///     var ability_data = await resolver.ResolveToObjectAsync(ref_id)
    ///     abilities.append(ability_data)
    /// enemy.combat_abilities = abilities
    /// </code>
    /// <para><strong>Why reference IDs instead of embedded objects?</strong></para>
    /// <list type="bullet">
    /// <item><description>Enemy is a template, not a live combat instance</description></item>
    /// <item><description>Lazy loading - resolve only when spawning enemy in combat</description></item>
    /// <item><description>Ability data can be updated without changing enemy templates</description></item>
    /// <item><description>Memory efficiency - shared ability data across multiple enemy types</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// Example ability reference IDs:
    /// <code>
    /// [
    ///   "@abilities/active/offensive:bite",
    ///   "@abilities/active/offensive:claw",
    ///   "@abilities/passive/defensive:thick-hide"
    /// ]
    /// </code>
    /// </example>
    public List<string> AbilityIds { get; set; } = new();

    /// <summary>
    /// Collection of spell IDs this enemy can cast in combat.
    /// Only populated for spellcasting enemies (mages, priests, etc.).
    /// </summary>
    /// <example>
    /// Example spell IDs:
    /// <code>
    /// [
    ///   "magic-missile",
    ///   "fireball",
    ///   "shield"
    /// ]
    /// </code>
    /// </example>
    public List<string> SpellIds { get; set; } = new();
    
    /// <summary>
    /// Tracks spell cooldowns for this enemy (spellId -> turns remaining).
    /// </summary>
    public Dictionary<string, int> SpellCooldowns { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the active status effects currently applied to the enemy.
    /// Includes buffs, debuffs, and damage-over-time effects.
    /// </summary>
    public List<StatusEffect> ActiveStatusEffects { get; set; } = new();

    /// <summary>
    /// Collection of loot table reference IDs (v4.1 format) for items this enemy can drop.
    /// Each ID is a JSON reference to an item catalog like \"@items/weapons/swords:*\".
    /// </summary>
    /// <remarks>
    /// <para><strong>✅ HOW TO RESOLVE - Use ReferenceResolverService:</strong></para>
    /// <code>
    /// // C# - Resolve loot table references
    /// var resolver = new ReferenceResolverService(dataCache);
    /// var lootItems = new List&lt;Item&gt;();
    /// foreach (var refId in enemy.LootTableIds)
    /// {
    ///     // Wildcards (*) return random items from category
    ///     var itemJson = await resolver.ResolveToObjectAsync(refId);
    ///     var item = itemJson.ToObject&lt;Item&gt;();
    ///     if (RollForLoot()) lootItems.Add(item);
    /// }
    /// </code>
    /// <code>
    /// // GDScript - Resolve loot tables in Godot
    /// var resolver = ReferenceResolverService.new(data_cache)
    /// var loot = []
    /// for ref_id in enemy.LootTableIds:
    ///     var item_data = await resolver.ResolveToObjectAsync(ref_id)
    ///     if roll_for_loot():
    ///         loot.append(item_data)
    /// </code>
    /// <para><strong>Wildcard support for random loot:</strong></para>
    /// <list type="bullet">
    /// <item><description>\"@items/weapons/swords:*\" - Random sword from catalog</description></item>
    /// <item><description>\"@items/consumables/potions:*\" - Random potion</description></item>
    /// <item><description>\"@items/materials/ores:iron-ore\" - Specific item</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// Example loot table reference IDs:
    /// <code>
    /// [
    ///   \"@items/consumables/potions:*\",
    ///   \"@items/weapons/swords:*\",
    ///   \"@items/materials/gems:raw-ruby\"
    /// ]
    /// </code>
    /// </example>
    public List<string> LootTableIds { get; set; } = new();

    /// <summary>
    /// Fully resolved Ability objects for this enemy's combat actions.
    /// Populated by EnemyGenerator.GenerateAsync() when hydrating templates.
    /// Not serialized to JSON (template IDs stored in AbilityIds instead).
    /// </summary>
    /// <remarks>
    /// <para><strong>For Runtime Use:</strong></para>
    /// <list type="bullet">
    /// <item><description>Use this property in combat AI and ability execution</description></item>
    /// <item><description>Already resolved - no need to call ReferenceResolverService</description></item>
    /// <item><description>Null if enemy loaded from template without hydration</description></item>
    /// </list>
    /// </remarks>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<Ability> Abilities { get; set; } = new();

    /// <summary>
    /// Fully resolved Item objects for this enemy's loot table.
    /// Populated by EnemyGenerator.GenerateAsync() when hydrating templates.
    /// Not serialized to JSON (template IDs stored in LootTableIds instead).
    /// </summary>
    /// <remarks>
    /// <para><strong>For Runtime Use:</strong></para>
    /// <list type="bullet">
    /// <item><description>Use this property when enemy dies to drop loot</description></item>
    /// <item><description>Already resolved with wildcard selection applied</description></item>
    /// <item><description>Null if enemy loaded from template without hydration</description></item>
    /// </list>
    /// </remarks>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<Item> LootTable { get; set; } = new();

    /// <summary>
    /// Calculate physical damage bonus from Strength.
    /// </summary>
    public int GetPhysicalDamageBonus()
    {
        return Strength;
    }

    /// <summary>
    /// Calculate magic damage bonus from Intelligence.
    /// </summary>
    public int GetMagicDamageBonus()
    {
        return Intelligence;
    }

    /// <summary>
    /// Calculate dodge chance from Dexterity.
    /// </summary>
    public double GetDodgeChance()
    {
        return Dexterity * 0.5; // 10 DEX = 5% dodge
    }

    /// <summary>
    /// Calculate critical hit chance from Dexterity.
    /// </summary>
    public double GetCriticalChance()
    {
        return Dexterity * 0.3; // 10 DEX = 3% crit
    }

    /// <summary>
    /// Calculate physical defense from Constitution.
    /// </summary>
    public int GetPhysicalDefense()
    {
        return Constitution;
    }

    /// <summary>
    /// Calculate magic resistance from Wisdom.
    /// </summary>
    public double GetMagicResistance()
    {
        return Wisdom * 0.8; // 10 WIS = 8% resist
    }

    /// <summary>
    /// Check if enemy is still alive.
    /// </summary>
    public bool IsAlive()
    {
        return Health > 0;
    }

    /// <summary>
    /// Gets the value of a specific prefix component by token name.
    /// </summary>
    /// <param name="token">The token name to search for (e.g., "size", "type").</param>
    /// <returns>The component value if found, otherwise null.</returns>
    public string? GetPrefixValue(string token)
    {
        return Prefixes.FirstOrDefault(p => p.Token == token)?.Value;
    }
    
    /// <summary>
    /// Gets the value of a specific suffix component by token name.
    /// </summary>
    /// <param name="token">The token name to search for.</param>
    /// <returns>The component value if found, otherwise null.</returns>
    public string? GetSuffixValue(string token)
    {
        return Suffixes.FirstOrDefault(s => s.Token == token)?.Value;
    }
    
    /// <summary>
    /// Composes the enemy name from individual naming components.
    /// Useful for rebuilding names, localization, or debugging.
    /// </summary>
    /// <returns>The composed name string.</returns>
    public string ComposeNameFromComponents()
    {
        var parts = new List<string>();
        
        // Add all prefixes in order
        parts.AddRange(Prefixes.Select(p => p.Value));
        
        // Add base name
        if (!string.IsNullOrWhiteSpace(BaseName)) parts.Add(BaseName);
        
        // Add all suffixes in order
        parts.AddRange(Suffixes.Select(s => s.Value));
        
        return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }
}

/// <summary>
/// Enemy types for flavor and potential special abilities.
/// </summary>
public enum EnemyType
{
    /// <summary>Regular enemies.</summary>
    Common,
    /// <summary>Animals and monsters.</summary>
    Beast,
    /// <summary>Zombies, skeletons.</summary>
    Undead,
    /// <summary>Hellish creatures.</summary>
    Demon,
    /// <summary>Fire, ice, earth, air elementals.</summary>
    Elemental,
    /// <summary>Bandits, soldiers.</summary>
    Humanoid,
    /// <summary>Dragons and dragonkin.</summary>
    Dragon,
    /// <summary>Special boss enemies.</summary>
    Boss
}

/// <summary>
/// Enemy difficulty scaling.
/// </summary>
public enum EnemyDifficulty
{
    /// <summary>Easy difficulty (0.5x player level).</summary>
    Easy,
    /// <summary>Normal difficulty (0.8x player level).</summary>
    Normal,
    /// <summary>Hard difficulty (1.0x player level).</summary>
    Hard,
    /// <summary>Elite difficulty (1.2x player level).</summary>
    Elite,
    /// <summary>Boss difficulty (1.5x+ player level).</summary>
    Boss
}

/// <summary>
/// Represents combat configuration for an enemy.
/// Contains ability references and level-based unlock data from JSON.
/// </summary>
public class EnemyCombat
{
    /// <summary>
    /// Gets or sets the current ability reference IDs available to this enemy.
    /// Each ID is a JSON reference like "@abilities/active/offensive:fireball".
    /// </summary>
    public List<string> Abilities { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the level-based ability unlock map.
    /// Key = level (int), Value = array of ability reference IDs unlocked at that level.
    /// Example: { 5: ["@abilities/active/offensive:*"], 10: ["@abilities/active/support:heal"] }
    /// </summary>
    public Dictionary<int, List<string>> AbilityUnlocks { get; set; } = new();
}
