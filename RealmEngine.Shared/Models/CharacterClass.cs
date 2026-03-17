namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents a character class/archetype with starting bonuses and proficiencies.
/// Maps to v4.1 JSON classes/catalog.json structure.
/// </summary>
public class CharacterClass
{
    /// <summary>Gets or sets the unique identifier for this character class.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Gets or sets the URL-safe identifier for this class (kebab-case).
    /// Used for lookups, references, and catalog identification. Maps to "slug" in JSON.
    /// </summary>
    public string Slug { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the internal name of the character class.</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name shown to players (may differ from internal name).
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the description of the character class.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Rarity weight for procedural generation (lower = more common).
    /// </summary>
    public int RarityWeight { get; set; } = 1;

    /// <summary>
    /// Whether this is a subclass/specialization of another class.
    /// </summary>
    public bool IsSubclass { get; set; } = false;

    /// <summary>
    /// Parent class ID if this is a subclass (resolved from @classes reference).
    /// </summary>
    public string? ParentClassId { get; set; }

    /// <summary>
    /// Primary attributes for this class (used for recommendations).
    /// </summary>
    public List<string> PrimaryAttributes { get; set; } = new();

    /// <summary>
    /// Armor types this class is proficient with (light, medium, heavy, shields).
    /// Maps to armorProficiency in metadata.
    /// </summary>
    public List<string> ArmorProficiency { get; set; } = new();

    /// <summary>
    /// Weapon types this class is proficient with (swords, axes, bows, etc.).
    /// Maps to weaponProficiency in metadata.
    /// </summary>
    public List<string> WeaponProficiency { get; set; } = new();

    /// <summary>
    /// Starting attribute bonuses applied at character creation.
    /// Maps to startingStats in JSON.
    /// </summary>
    public int StartingHealth { get; set; } = 100;
    /// <summary>Gets or sets the starting mana points for this class.</summary>
    public int StartingMana { get; set; } = 50;
    /// <summary>Gets or sets the bonus strength modifier for this class.</summary>
    public int BonusStrength { get; set; } = 10;
    /// <summary>Gets or sets the bonus dexterity modifier for this class.</summary>
    public int BonusDexterity { get; set; } = 10;
    /// <summary>Gets or sets the bonus constitution modifier for this class.</summary>
    public int BonusConstitution { get; set; } = 10;
    /// <summary>Gets or sets the bonus intelligence modifier for this class.</summary>
    public int BonusIntelligence { get; set; } = 10;
    /// <summary>Gets or sets the bonus wisdom modifier for this class.</summary>
    public int BonusWisdom { get; set; } = 10;
    /// <summary>Gets or sets the bonus charisma modifier for this class.</summary>
    public int BonusCharisma { get; set; } = 10;
    
    /// <summary>
    /// Gets or sets the level-based progression data for this class.
    /// Contains stat growth per level and ability unlock schedules.
    /// </summary>
    public ClassProgression? Progression { get; set; }

    /// <summary>
    /// Collection of starting ability reference IDs (v4.1 format) that characters of this class begin with.
    /// Each ID is a JSON reference like "@abilities/active/offensive:basic-attack".
    /// </summary>
    /// <remarks>
    /// <para><strong>✅ HOW TO RESOLVE - Use ReferenceResolverService:</strong></para>
    /// <code>
    /// // C# - Resolve each reference to a full Ability object
    /// var resolver = new ReferenceResolverService(dataCache);
    /// var abilities = new List&lt;Ability&gt;();
    /// foreach (var refId in characterClass.StartingAbilityIds)
    /// {
    ///     var abilityJson = await resolver.ResolveToObjectAsync(refId);
    ///     var ability = abilityJson.ToObject&lt;Ability&gt;();
    ///     abilities.Add(ability);
    /// }
    /// </code>
    /// <code>
    /// // GDScript - Resolve references in Godot
    /// var resolver = ReferenceResolverService.new(data_cache)
    /// var abilities = []
    /// for ref_id in character_class.StartingAbilityIds:
    ///     var ability_data = await resolver.ResolveToObjectAsync(ref_id)
    ///     abilities.append(ability_data)
    /// </code>
    /// <para><strong>Why reference IDs instead of embedded objects?</strong></para>
    /// <list type="bullet">
    /// <item><description>Memory efficiency - templates don't duplicate full ability data</description></item>
    /// <item><description>Lazy loading - resolve only when creating a character instance</description></item>
    /// <item><description>Cross-domain references - abilities live in separate catalog files</description></item>
    /// <item><description>Save file optimization - serialize IDs instead of full object graphs</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// Example reference IDs:
    /// <code>
    /// [
    ///   "@abilities/active/offensive:basic-attack",
    ///   "@abilities/active/offensive:power-strike",
    ///   "@abilities/passive/defensive:shield-mastery"
    /// ]
    /// </code>
    /// </example>
    public List<string> StartingAbilityIds { get; set; } = new();

    /// <summary>
    /// Spell reference IDs this class starts with (level-1 unlocks from ClassSpellUnlocks).
    /// Empty for non-spellcaster classes.
    /// </summary>
    public List<string> StartingSpellIds { get; set; } = new();

    /// <summary>
    /// Collection of starting equipment reference IDs (v4.1 format) that characters of this class begin with.
    /// Each ID is a JSON reference like "@items/weapons/swords:longsword".
    /// </summary>
    /// <remarks>
    /// <para><strong>✅ HOW TO RESOLVE - Use ReferenceResolverService:</strong></para>
    /// <code>
    /// // C# - Resolve each reference to a full Item object
    /// var resolver = new ReferenceResolverService(dataCache);
    /// var equipment = new List&lt;Item&gt;();
    /// foreach (var refId in characterClass.StartingEquipmentIds)
    /// {
    ///     var itemJson = await resolver.ResolveToObjectAsync(refId);
    ///     var item = itemJson.ToObject&lt;Item&gt;();
    ///     equipment.Add(item);
    /// }
    /// </code>
    /// <code>
    /// // GDScript - Resolve references in Godot
    /// var resolver = ReferenceResolverService.new(data_cache)
    /// var equipment = []
    /// for ref_id in character_class.StartingEquipmentIds:
    ///     var item_data = await resolver.ResolveToObjectAsync(ref_id)
    ///     equipment.append(item_data)
    /// </code>
    /// <para><strong>Typical starting equipment includes:</strong></para>
    /// <list type="bullet">
    /// <item><description>Primary weapon (sword, staff, bow, etc.)</description></item>
    /// <item><description>Armor pieces (helmet, chest, legs, etc.)</description></item>
    /// <item><description>Consumables (health potions, mana potions)</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// Example reference IDs:
    /// <code>
    /// [
    ///   "@items/weapons/swords:longsword",
    ///   "@items/armor/chest:leather-armor",
    ///   "@items/consumables/potions:health-potion"
    /// ]
    /// </code>
    /// </example>
    public List<string> StartingEquipmentIds { get; set; } = new();

    /// <summary>
    /// Fully resolved Ability objects for this class's starting abilities.
    /// Populated by CharacterClassGenerator.GenerateAsync() when hydrating templates.
    /// Not serialized to JSON (template IDs stored in StartingAbilityIds instead).
    /// </summary>
    /// <remarks>
    /// <para><strong>For Runtime Use:</strong></para>
    /// <list type="bullet">
    /// <item><description>Use this property during character creation to grant abilities</description></item>
    /// <item><description>Already resolved - no need to call ReferenceResolverService</description></item>
    /// <item><description>Null if class loaded from template without hydration</description></item>
    /// </list>
    /// </remarks>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<Ability> StartingAbilities { get; set; } = new();

    /// <summary>
    /// Fully resolved Item objects for this class's starting equipment.
    /// Populated by CharacterClassGenerator.GenerateAsync() when hydrating templates.
    /// Not serialized to JSON (template IDs stored in StartingEquipmentIds instead).
    /// </summary>
    /// <remarks>
    /// <para><strong>For Runtime Use:</strong></para>
    /// <list type="bullet">
    /// <item><description>Use this property during character creation to grant equipment</description></item>
    /// <item><description>Already resolved - no need to call ReferenceResolverService</description></item>
    /// <item><description>Null if class loaded from template without hydration</description></item>
    /// </list>
    /// </remarks>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<Item> StartingEquipment { get; set; } = new();

    /// <summary>
    /// Traits/properties specific to this class (bonus damage types, resistances, etc.).
    /// Values may be strings, numbers, or resolved references.
    /// </summary>
    public Dictionary<string, object> Traits { get; set; } = new();

    /// <summary>
    /// Flavor text for class selection.
    /// </summary>
    public string FlavorText { get; set; } = string.Empty;
}

/// <summary>
/// Class progression data (stat growth, ability unlocks).
/// </summary>
public class ClassProgression
{
    /// <summary>
    /// Gets or sets the stat growth configuration per level.
    /// </summary>
    public StatGrowth? StatGrowth { get; set; }

    /// <summary>
    /// Ability unlock schedule mapping level to ability IDs.
    /// </summary>
    /// <remarks>
    /// <para><strong>Resolution Pattern (C#):</strong></para>
    /// <code>
    /// // Get abilities unlocked at level 5
    /// if (progression.AbilityUnlocks.TryGetValue(5, out var abilityIds))
    /// {
    ///     var unlockedAbilities = await abilityRepository.GetByIdsAsync(abilityIds);
    ///     character.LearnedSkills.AddRange(unlockedAbilities);
    /// }
    /// </code>
    /// <para><strong>Resolution Pattern (GDScript/Godot):</strong></para>
    /// <code>
    /// # Check for abilities at current level
    /// if progression.AbilityUnlocks.has(character.level):
    ///     var ability_ids = progression.AbilityUnlocks[character.level]
    ///     for ability_id in ability_ids:
    ///         var ability = await ability_service.get_by_id(ability_id)
    ///         character.learned_skills.append(ability)
    /// </code>
    /// </remarks>
    /// <example>
    /// Example: { 5: ["power-strike", "cleave"], 10: ["whirlwind", "battle-cry"] }
    /// </example>
    public Dictionary<int, List<string>> AbilityUnlocks { get; set; } = new();
}

/// <summary>
/// Represents stat growth per level for a character class.
/// </summary>
public class StatGrowth
{
    /// <summary>Gets or sets the health points gained per level.</summary>
    public double HealthPerLevel { get; set; } = 0;
    
    /// <summary>Gets or sets the mana points gained per level.</summary>
    public double ManaPerLevel { get; set; } = 0;
    
    /// <summary>Gets or sets the strength gained per level.</summary>
    public double StrengthPerLevel { get; set; } = 0;
    
    /// <summary>Gets or sets the dexterity gained per level.</summary>
    public double DexterityPerLevel { get; set; } = 0;
    
    /// <summary>Gets or sets the constitution gained per level.</summary>
    public double ConstitutionPerLevel { get; set; } = 0;
    
    /// <summary>Gets or sets the intelligence gained per level.</summary>
    public double IntelligencePerLevel { get; set; } = 0;
    
    /// <summary>Gets or sets the wisdom gained per level.</summary>
    public double WisdomPerLevel { get; set; } = 0;
    
    /// <summary>Gets or sets the charisma gained per level.</summary>
    public double CharismaPerLevel { get; set; } = 0;
}

/// <summary>
/// Attribute point allocation for character creation.
/// </summary>
public class AttributeAllocation
{
    /// <summary>Gets or sets strength points allocated.</summary>
    public int Strength { get; set; } = 8;
    /// <summary>Gets or sets dexterity points allocated.</summary>
    public int Dexterity { get; set; } = 8;
    /// <summary>Gets or sets constitution points allocated.</summary>
    public int Constitution { get; set; } = 8;
    /// <summary>Gets or sets intelligence points allocated.</summary>
    public int Intelligence { get; set; } = 8;
    /// <summary>Gets or sets wisdom points allocated.</summary>
    public int Wisdom { get; set; } = 8;
    /// <summary>Gets or sets charisma points allocated.</summary>
    public int Charisma { get; set; } = 8;

    private const int MinAttributeValue = 8;
    private const int MaxAttributeValue = 15;
    private const int TotalPoints = 27;

    /// <summary>
    /// Calculate the point cost to increase an attribute from current to target value.
    /// Point buy rules: 8-13 costs 1 point per level, 14 costs 2 points, 15 costs 2 points.
    /// </summary>
    public static int GetPointCost(int fromValue, int toValue)
    {
        if (toValue <= fromValue) return 0;

        int cost = 0;
        for (int i = fromValue; i < toValue; i++)
        {
            if (i >= 13)
                cost += 2; // 13→14 and 14→15 cost 2 points each
            else
                cost += 1; // 8→9 through 12→13 cost 1 point each
        }
        return cost;
    }

    /// <summary>
    /// Get the total points spent on all attributes.
    /// </summary>
    public int GetPointsSpent()
    {
        int total = 0;
        total += GetPointCost(MinAttributeValue, Strength);
        total += GetPointCost(MinAttributeValue, Dexterity);
        total += GetPointCost(MinAttributeValue, Constitution);
        total += GetPointCost(MinAttributeValue, Intelligence);
        total += GetPointCost(MinAttributeValue, Wisdom);
        total += GetPointCost(MinAttributeValue, Charisma);
        return total;
    }

    /// <summary>
    /// Get remaining points to allocate.
    /// </summary>
    public int GetRemainingPoints()
    {
        return TotalPoints - GetPointsSpent();
    }

    /// <summary>
    /// Check if the allocation is valid (within limits and points budget).
    /// </summary>
    public bool IsValid()
    {
        return Strength >= MinAttributeValue && Strength <= MaxAttributeValue &&
               Dexterity >= MinAttributeValue && Dexterity <= MaxAttributeValue &&
               Constitution >= MinAttributeValue && Constitution <= MaxAttributeValue &&
               Intelligence >= MinAttributeValue && Intelligence <= MaxAttributeValue &&
               Wisdom >= MinAttributeValue && Wisdom <= MaxAttributeValue &&
               Charisma >= MinAttributeValue && Charisma <= MaxAttributeValue &&
               GetPointsSpent() <= TotalPoints;
    }

    /// <summary>
    /// Check if we can increase an attribute value.
    /// </summary>
    public bool CanIncrease(string attribute)
    {
        var currentValue = GetAttributeValue(attribute);
        if (currentValue >= MaxAttributeValue) return false;

        var costToIncrease = GetPointCost(currentValue, currentValue + 1);
        return GetRemainingPoints() >= costToIncrease;
    }

    /// <summary>
    /// Check if we can decrease an attribute value.
    /// </summary>
    public bool CanDecrease(string attribute)
    {
        return GetAttributeValue(attribute) > MinAttributeValue;
    }

    /// <summary>
    /// Get attribute value by name.
    /// </summary>
    public int GetAttributeValue(string attribute)
    {
        return attribute switch
        {
            "Strength" => Strength,
            "Dexterity" => Dexterity,
            "Constitution" => Constitution,
            "Intelligence" => Intelligence,
            "Wisdom" => Wisdom,
            "Charisma" => Charisma,
            _ => 0
        };
    }

    /// <summary>
    /// Set attribute value by name.
    /// </summary>
    public void SetAttributeValue(string attribute, int value)
    {
        switch (attribute)
        {
            case "Strength": Strength = value; break;
            case "Dexterity": Dexterity = value; break;
            case "Constitution": Constitution = value; break;
            case "Intelligence": Intelligence = value; break;
            case "Wisdom": Wisdom = value; break;
            case "Charisma": Charisma = value; break;
        }
    }
}
