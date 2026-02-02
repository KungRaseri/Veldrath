using RealmEngine.Shared.Utilities;
using LiteDB;

namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents a player character in the game.
/// </summary>
public class Character
{
    /// <summary>
    /// Gets or sets the character's name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the character's class name (e.g., "Warrior", "Mage", "Rogue").
    /// </summary>
    public string ClassName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the character's current level.
    /// </summary>
    public int Level { get; set; } = 1;
    
    /// <summary>
    /// Gets or sets the character's current experience points.
    /// </summary>
    public int Experience { get; set; } = 0;
    
    /// <summary>
    /// Gets or sets the character's current health points.
    /// </summary>
    public int Health { get; set; } = 100;
    
    /// <summary>
    /// Gets or sets the character's maximum health points.
    /// </summary>
    public int MaxHealth { get; set; } = 100;
    
    /// <summary>
    /// Gets or sets the character's current mana points.
    /// </summary>
    public int Mana { get; set; } = 50;
    
    /// <summary>
    /// Gets or sets the character's maximum mana points.
    /// </summary>
    public int MaxMana { get; set; } = 50;
    
    /// <summary>
    /// Gets or sets the amount of gold the character possesses.
    /// </summary>
    public int Gold { get; set; } = 0;

    /// <summary>
    /// Gets or sets the current location ID where the character is located.
    /// Example: "locations/settlements:starting-village"
    /// </summary>
    public string? CurrentLocationId { get; set; }

    /// <summary>
    /// Gets or sets the current zone name for display purposes.
    /// Example: "Starting Village", "Dark Forest"
    /// </summary>
    public string? CurrentZone { get; set; }

    /// <summary>
    /// Gets or sets the background ID selected during character creation.
    /// Example: "backgrounds/strength:soldier"
    /// </summary>
    public string? BackgroundId { get; set; }

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
    /// Gets or sets the Charisma attribute. Affects shop prices and rare loot drops.
    /// </summary>
    public int Charisma { get; set; } = 10;

    /// <summary>
    /// Gets or sets the number of unspent attribute points available for allocation.
    /// </summary>
    public int UnspentAttributePoints { get; set; } = 0;
    
    /// <summary>
    /// Gets or sets the number of unspent skill points available for learning new abilities.
    /// </summary>
    public int UnspentSkillPoints { get; set; } = 0;
    
    /// <summary>
    /// Gets or sets the collection of pending level-up bonuses to be applied.
    /// </summary>
    public List<LevelUpInfo> PendingLevelUps { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the character's skill proficiencies by skill ID.
    /// Dictionary key is skillId (e.g., "athletics", "light-blades", "arcane").
    /// Skills rank from 0-100 through practice-based XP gain.
    /// </summary>
    public Dictionary<string, CharacterSkill> Skills { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the learned abilities by ability ID.
    /// Dictionary key is abilityId (e.g., "active/offensive:charge").
    /// Tracks usage statistics and cooldown state.
    /// </summary>
    public Dictionary<string, CharacterAbility> LearnedAbilities { get; set; } = new();
    
    /// <summary>
    /// Gets or sets abilities granted by equipped items.
    /// Dictionary key is abilityId, value is the item ID that grants it.
    /// These abilities are active only while the item is equipped.
    /// </summary>
    public Dictionary<string, string> EquipmentGrantedAbilities { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the learned spells by spell ID.
    /// Dictionary key is spellId (e.g., "fireball", "heal").
    /// Tracks cast statistics and success rates.
    /// </summary>
    public Dictionary<string, CharacterSpell> LearnedSpells { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the active ability cooldowns (ability ID → turns remaining).
    /// Decrements each combat turn. When reaches 0, ability is ready.
    /// </summary>
    public Dictionary<string, int> AbilityCooldowns { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the active spell cooldowns (spell ID → turns remaining).
    /// Decrements each combat turn. When reaches 0, spell is ready.
    /// </summary>
    public Dictionary<string, int> SpellCooldowns { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the learned crafting recipes by recipe ID.
    /// Recipes with unlock method "SkillLevel" auto-unlock when skill is high enough.
    /// Recipes with "Recipe", "Quest", or "Discovery" must be explicitly learned.
    /// </summary>
    public HashSet<string> LearnedRecipes { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the active status effects currently applied to the character.
    /// Includes buffs, debuffs, and damage-over-time effects.
    /// </summary>
    public List<StatusEffect> ActiveStatusEffects { get; set; } = new();

    /// <summary>
    /// Gets or sets the character's inventory of items.
    /// </summary>
    public List<Item> Inventory { get; set; } = new();

    /// <summary>
    /// Gets or sets the weapon equipped in the main hand slot.
    /// </summary>
    public Item? EquippedMainHand { get; set; }
    
    /// <summary>
    /// Gets or sets the item equipped in the off-hand slot (weapon or shield).
    /// </summary>
    public Item? EquippedOffHand { get; set; }

    /// <summary>
    /// Gets or sets the helmet equipped in the head armor slot.
    /// </summary>
    public Item? EquippedHelmet { get; set; }
    
    /// <summary>
    /// Gets or sets the shoulder armor equipped.
    /// </summary>
    public Item? EquippedShoulders { get; set; }
    
    /// <summary>
    /// Gets or sets the chest armor equipped.
    /// </summary>
    public Item? EquippedChest { get; set; }
    
    /// <summary>
    /// Gets or sets the bracer armor equipped on the wrists.
    /// </summary>
    public Item? EquippedBracers { get; set; }
    
    /// <summary>
    /// Gets or sets the glove armor equipped on the hands.
    /// </summary>
    public Item? EquippedGloves { get; set; }
    
    /// <summary>
    /// Gets or sets the belt equipped.
    /// </summary>
    public Item? EquippedBelt { get; set; }
    
    /// <summary>
    /// Gets or sets the leg armor equipped.
    /// </summary>
    public Item? EquippedLegs { get; set; }
    
    /// <summary>
    /// Gets or sets the boot armor equipped.
    /// </summary>
    public Item? EquippedBoots { get; set; }

    /// <summary>
    /// Gets or sets the necklace equipped in the jewelry slot.
    /// </summary>
    public Item? EquippedNecklace { get; set; }
    
    /// <summary>
    /// Gets or sets the first ring equipped.
    /// </summary>
    public Item? EquippedRing1 { get; set; }
    
    /// <summary>
    /// Gets or sets the second ring equipped.
    /// </summary>
    public Item? EquippedRing2 { get; set; }

    /// <summary>
    /// Award experience points to the character.
    /// </summary>
    public void GainExperience(int amount)
    {
        Experience += amount;

        // Simple leveling logic - can be enhanced
        while (Experience >= ExperienceForNextLevel())
        {
            Experience -= ExperienceForNextLevel();
            LevelUp();
        }
    }

    /// <summary>
    /// Calculate experience needed for next level.
    /// </summary>
    private int ExperienceForNextLevel()
    {
        return Level * 100; // Simple formula: Level 1 needs 100, Level 2 needs 200, etc.
    }

    /// <summary>
    /// Increase character level and stats.
    /// </summary>
    private void LevelUp()
    {
        Level++;

        // Recalculate max health and mana from new level
        MaxHealth = GetMaxHealth();
        Health = MaxHealth;  // Fully heal on level up
        MaxMana = GetMaxMana();
        Mana = MaxMana;  // Fully restore mana on level up

        // Award attribute and skill points for player to allocate
        int attributePoints = 3; // 3 attribute points per level
        int skillPoints = 1;     // 1 skill point per level

        // Every 5 levels, award bonus points
        if (Level % 5 == 0)
        {
            attributePoints += 2; // Bonus at levels 5, 10, 15, etc.
            skillPoints += 1;
        }

        UnspentAttributePoints += attributePoints;
        UnspentSkillPoints += skillPoints;

        // Queue this level-up for player interaction
        PendingLevelUps.Add(new LevelUpInfo
        {
            NewLevel = Level,
            AttributePointsGained = attributePoints,
            SkillPointsGained = skillPoints,
            IsProcessed = false
        });
    }

    /// <summary>
    /// Calculate maximum health from Constitution and level.
    /// </summary>
    public int GetMaxHealth()
    {
        return (Constitution * 10) + (Level * 5);
    }

    /// <summary>
    /// Calculate maximum mana from Wisdom and level.
    /// </summary>
    public int GetMaxMana()
    {
        int baseMana = (Wisdom * 5) + (Level * 3);
        double skillMultiplier = SkillEffectCalculator.GetMaxManaMultiplier(this);
        return (int)(baseMana * skillMultiplier);
    }

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
    /// Calculate dodge chance percentage from Dexterity.
    /// </summary>
    public double GetDodgeChance()
    {
        return Dexterity * 0.5;  // 10 DEX = 5% dodge
    }

    /// <summary>
    /// Calculate critical hit chance percentage from Dexterity.
    /// </summary>
    public double GetCriticalChance()
    {
        return Dexterity * 0.3;  // 10 DEX = 3% crit
    }

    /// <summary>
    /// Calculate physical defense from Constitution and equipment.
    /// </summary>
    public int GetPhysicalDefense(List<EquipmentSet>? sets = null)
    {
        // Defense is based on total Constitution (base + equipment + sets)
        return GetTotalConstitution(sets);
    }

    /// <summary>
    /// Calculate magic resistance percentage from Wisdom.
    /// </summary>
    public double GetMagicResistance()
    {
        return Wisdom * 0.8;  // 10 WIS = 8% magic resist
    }

    /// <summary>
    /// Calculate shop discount percentage from Charisma.
    /// </summary>
    public double GetShopDiscount()
    {
        return Charisma * 1.0;  // 10 CHA = 10% discount
    }

    /// <summary>
    /// Calculate rare item find chance from Charisma.
    /// </summary>
    public double GetRareItemChance()
    {
        double baseChance = Charisma * 0.5;  // 10 CHA = 5% rare find
        double skillBonus = SkillEffectCalculator.GetRareItemFindBonus(this);
        return baseChance + skillBonus;
    }

    /// <summary>
    /// Check if character is alive (health > 0).
    /// </summary>
    public bool IsAlive()
    {
        return Health > 0;
    }

    /// <summary>
    /// Helper method to calculate total attribute from base + equipment + set bonuses.
    /// </summary>
    private int GetTotalAttribute(string attributeName, int baseValue, List<EquipmentSet>? sets = null)
    {
        int total = baseValue;

        // Sum all equipped items' trait bonuses
        var equippedItems = new[] 
        { 
            EquippedMainHand, EquippedOffHand, EquippedHelmet, EquippedShoulders,
            EquippedChest, EquippedBracers, EquippedGloves, EquippedBelt,
            EquippedLegs, EquippedBoots, EquippedNecklace, EquippedRing1, EquippedRing2
        };

        foreach (var item in equippedItems)
        {
            if (item != null)
            {
                total += (int)item.GetTotalTrait(attributeName, 0);
            }
        }

        // Add set bonuses
        if (sets != null)
        {
            var setBonuses = GetSetBonuses(sets, attributeName);
            total += setBonuses.Values.Sum();
        }

        return total;
    }

    /// <summary>
    /// Calculate total strength (base + equipment bonuses + set bonuses).
    /// </summary>
    public int GetTotalStrength(List<EquipmentSet>? sets = null) 
        => GetTotalAttribute("Strength", Strength, sets);

    /// <summary>
    /// Calculate total dexterity (base + equipment bonuses + set bonuses).
    /// </summary>
    public int GetTotalDexterity(List<EquipmentSet>? sets = null) 
        => GetTotalAttribute("Dexterity", Dexterity, sets);

    /// <summary>
    /// Calculate total constitution (base + equipment bonuses + set bonuses).
    /// </summary>
    public int GetTotalConstitution(List<EquipmentSet>? sets = null) 
        => GetTotalAttribute("Constitution", Constitution, sets);

    /// <summary>
    /// Calculate total intelligence (base + equipment bonuses + set bonuses).
    /// </summary>
    public int GetTotalIntelligence(List<EquipmentSet>? sets = null) 
        => GetTotalAttribute("Intelligence", Intelligence, sets);

    /// <summary>
    /// Calculate total wisdom (base + equipment bonuses + set bonuses).
    /// </summary>
    public int GetTotalWisdom(List<EquipmentSet>? sets = null) 
        => GetTotalAttribute("Wisdom", Wisdom, sets);

    /// <summary>
    /// Calculate total charisma (base + equipment bonuses + set bonuses).
    /// </summary>
    public int GetTotalCharisma(List<EquipmentSet>? sets = null) 
        => GetTotalAttribute("Charisma", Charisma, sets);

    /// <summary>
    /// Checks if character has access to an ability (either learned or granted by equipment).
    /// </summary>
    public bool HasAbility(string abilityId)
    {
        return LearnedAbilities.ContainsKey(abilityId) || EquipmentGrantedAbilities.ContainsKey(abilityId);
    }
    
    /// <summary>
    /// Gets all available ability IDs (both learned and equipment-granted).
    /// </summary>
    public IEnumerable<string> GetAvailableAbilityIds()
    {
        return LearnedAbilities.Keys.Concat(EquipmentGrantedAbilities.Keys).Distinct();
    }

    /// <summary>
    /// Get all currently equipped items.
    /// </summary>
    public List<Item> GetEquippedItems()
    {
        var items = new List<Item>();

        if (EquippedMainHand != null) items.Add(EquippedMainHand);
        if (EquippedOffHand != null) items.Add(EquippedOffHand);
        if (EquippedHelmet != null) items.Add(EquippedHelmet);
        if (EquippedShoulders != null) items.Add(EquippedShoulders);
        if (EquippedChest != null) items.Add(EquippedChest);
        if (EquippedBracers != null) items.Add(EquippedBracers);
        if (EquippedGloves != null) items.Add(EquippedGloves);
        if (EquippedBelt != null) items.Add(EquippedBelt);
        if (EquippedLegs != null) items.Add(EquippedLegs);
        if (EquippedBoots != null) items.Add(EquippedBoots);
        if (EquippedNecklace != null) items.Add(EquippedNecklace);
        if (EquippedRing1 != null) items.Add(EquippedRing1);
        if (EquippedRing2 != null) items.Add(EquippedRing2);

        return items;
    }

    /// <summary>
    /// Detect which equipment sets are partially or fully equipped.
    /// Returns a dictionary of set name -> number of pieces equipped.
    /// </summary>
    public Dictionary<string, int> GetActiveEquipmentSets()
    {
        var equippedItems = GetEquippedItems();
        var sets = new Dictionary<string, int>();

        foreach (var item in equippedItems)
        {
            if (!string.IsNullOrEmpty(item.SetName))
            {
                if (sets.ContainsKey(item.SetName))
                    sets[item.SetName]++;
                else
                    sets[item.SetName] = 1;
            }
        }

        return sets;
    }

    /// <summary>
    /// Calculate set bonuses from equipped sets.
    /// </summary>
    public Dictionary<string, int> GetSetBonuses(List<EquipmentSet> availableSets, string statType)
    {
        var activeSets = GetActiveEquipmentSets();
        var bonuses = new Dictionary<string, int>();

        foreach (var (setName, piecesEquipped) in activeSets)
        {
            var set = availableSets.FirstOrDefault(s => s.Name == setName);
            if (set == null) continue;

            // Check which bonuses are active based on pieces equipped
            foreach (var (requiredPieces, bonus) in set.Bonuses)
            {
                if (piecesEquipped >= requiredPieces)
                {
                    var bonusValue = statType switch
                    {
                        "Strength" => bonus.BonusStrength,
                        "Dexterity" => bonus.BonusDexterity,
                        "Constitution" => bonus.BonusConstitution,
                        "Intelligence" => bonus.BonusIntelligence,
                        "Wisdom" => bonus.BonusWisdom,
                        "Charisma" => bonus.BonusCharisma,
                        _ => 0
                    };

                    if (bonusValue > 0)
                    {
                        var key = $"{setName} ({requiredPieces} pieces)";
                        bonuses[key] = bonusValue;
                    }
                }
            }
        }

        return bonuses;
    }
}
