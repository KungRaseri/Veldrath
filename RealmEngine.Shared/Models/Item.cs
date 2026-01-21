namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents an item in the game.
/// Supports the Hybrid Enhancement System v1.0 with materials, enchantments, and gem sockets.
/// </summary>
public class Item : ITraitable
{
    /// <summary>
    /// Gets or sets the unique identifier for this item.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the URL-safe identifier for this item (kebab-case).
    /// Used for lookups, references, and API endpoints.
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the item (may include enhancements).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the descriptive text for the item (mechanical description).
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the lore/flavor text for the item (history, significance).
    /// Optional field that provides immersive backstory. May be procedurally generated.
    /// </summary>
    public string? Lore { get; set; }

    /// <summary>
    /// Gets or sets the market value of the item in gold.
    /// </summary>
    public int Price { get; set; }

    /// <summary>
    /// Gets or sets the physical weight of the item in pounds.
    /// Used for inventory encumbrance calculations.
    /// </summary>
    public double Weight { get; set; } = 0.0;

    /// <summary>
    /// Gets or sets the item rarity (Common, Uncommon, Rare, Epic, Legendary, Mythic).
    /// </summary>
    public ItemRarity Rarity { get; set; } = ItemRarity.Common;

    /// <summary>
    /// Gets or sets the item type/category (Weapon, Armor, Consumable, Quest, Material, etc.).
    /// </summary>
    public ItemType Type { get; set; } = ItemType.Consumable;

    /// <summary>
    /// Gets or sets the requirements to equip or use this item.
    /// Includes level, attribute, and skill requirements.
    /// </summary>
    public ItemRequirements? Requirements { get; set; }

    /// <summary>
    /// Gets or sets the quantity of this item (for stackable items like consumables, materials).
    /// Default is 1 for non-stackable items (weapons, armor).
    /// </summary>
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Gets or sets the maximum stack size for this item (from JSON catalog).
    /// Default is 1 (non-stackable). Values > 1 indicate stackable items.
    /// </summary>
    public int StackSize { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether this item can stack with others of the same type.
    /// Determined by StackSize > 1.
    /// </summary>
    public bool IsStackable { get; set; } = false;

    /// <summary>
    /// Gets or sets the attribute bonuses provided by this item's name components, materials, and enchantments.
    /// These are bonuses GIVEN to the character when equipped (e.g., +5 STR from "Herculean" prefix).
    /// DO NOT CONFUSE with Requirements.Attributes (which are stats NEEDED to equip the item).
    /// </summary>
    /// <remarks>
    /// <para><strong>Requirements vs Bonuses:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Requirements.Attributes</strong> = Stats needed to EQUIP (e.g., "Need 16 STR to use this sword")</description></item>
    /// <item><description><strong>BaseAttributes</strong> = Bonuses GIVEN when equipped (e.g., "+5 STR from Herculean prefix")</description></item>
    /// </list>
    /// <para><strong>Sources of Attribute Bonuses:</strong></para>
    /// <list type="bullet">
    /// <item><description>Name components (prefix/suffix in names.json): "Herculean" = +STR, "Swift" = +DEX</description></item>
    /// <item><description>Materials: Iron weapons may give +CON, Mithril may give +DEX</description></item>
    /// <item><description>Enchantments: "of the Bear" = +STR, "of the Fox" = +DEX</description></item>
    /// </list>
    /// <para><strong>NOT from base catalog:</strong> Base item templates (weapons/catalog.json) do NOT provide attribute bonuses.</para>
    /// </remarks>
    public Dictionary<string, int> BaseAttributes { get; set; } = new();

    /// <summary>
    /// Gets or sets the implicit traits from the base item type (from catalog).
    /// These are inherent properties that define the item type (e.g., base damage, armor class).
    /// </summary>
    public Dictionary<string, TraitValue> BaseTraits { get; set; } = new();

    /// <summary>
    /// Gets or sets the trait system dictionary for dynamic properties defined in JSON.
    /// Implements ITraitable interface.
    /// NOTE: This is combined data - prefer BaseTraits for display separation.
    /// </summary>
    public Dictionary<string, TraitValue> Traits { get; set; } = new();

    /// <summary>
    /// Gets or sets the formula-based stats for this item.
    /// Used for armor defense calculations, weapon attack bonuses, etc.
    /// Keys like "defense", "attack", with string formulas as values.
    /// </summary>
    public Dictionary<string, string> Stats { get; set; } = new();

    /// <summary>
    /// Gets or sets the equipment set name this item belongs to (if any).
    /// Items in a set grant bonuses when multiple pieces are equipped.
    /// </summary>
    public string? SetName { get; set; }

    /// <summary>
    /// Gets or sets whether this weapon requires both hands to wield.
    /// Two-handed weapons cannot be used with shields.
    /// </summary>
    public bool IsTwoHanded { get; set; } = false;

    /// <summary>
    /// Gets or sets the damage configuration for weapons.
    /// Contains min/max damage dice and modifier formula.
    /// </summary>
    public ItemDamage? Damage { get; set; }

    /// <summary>
    /// Gets or sets the armor class for armor items.
    /// Values: "light", "medium", "heavy"
    /// </summary>
    public string? ArmorClass { get; set; }

    /// <summary>
    /// Gets or sets the effect type for consumable items.
    /// Examples: "heal", "buff", "heal_overtime", "cure_poison", "restore", "stat_boost"
    /// </summary>
    public string? Effect { get; set; }

    /// <summary>
    /// Gets or sets the power/magnitude of the effect for consumables.
    /// For healing potions, this is HP restored. For buffs, this is bonus amount.
    /// </summary>
    public int Power { get; set; } = 0;

    /// <summary>
    /// Gets or sets the duration of the effect in turns/seconds.
    /// 0 means instant effect (like healing potions).
    /// </summary>
    public int Duration { get; set; } = 0;

    // Enhancement System v1.0 (Hybrid Model)
    // ========================================

    /// <summary>
    /// Gets or sets the traits provided by this item's material.
    /// Materials contribute to the item's overall power level.
    /// </summary>
    public Dictionary<string, TraitValue> MaterialTraits { get; set; } = new();

    /// <summary>
    /// Gets or sets the collection of enchantments applied to this item.
    /// Enchantments are baked into the item at generation time.
    /// </summary>
    public List<Enchantment> Enchantments { get; set; } = new();

    /// <summary>
    /// Gets or sets the collection of player-applied enchantments (post-crafting).
    /// Separate from generation Enchantments. These are applied via enchantment scrolls.
    /// Limited by MaxPlayerEnchantments based on rarity and socket crystals.
    /// </summary>
    public List<Enchantment> PlayerEnchantments { get; set; } = new();

    /// <summary>
    /// Gets or sets the maximum number of player-applied enchantments allowed.
    /// Determined by item rarity: Common=1, Rare=2, Legendary=3.
    /// Can be increased up to 3 using socket crystals (requires Enchanting skill).
    /// </summary>
    public int MaxPlayerEnchantments { get; set; } = 1;

    /// <summary>
    /// Gets or sets the collection of sockets available on this item, organized by socket type.
    /// Sockets are player-customizable after generation.
    /// Key = SocketType, Value = List of sockets for that type.
    /// </summary>
    public Dictionary<SocketType, List<Socket>> Sockets { get; set; } = new();

    /// <summary>
    /// Gets or sets the total rarity weight calculated from base item, material, enchantments, and sockets.
    /// </summary>
    public int TotalRarityWeight { get; set; } = 0;

    /// <summary>
    /// Gets or sets the base item name before enhancements are applied (e.g., \"Longsword\").
    /// </summary>
    public string BaseName { get; set; } = string.Empty;

    // ========================================
    // NEW v4.3 Component System
    // ========================================

    /// <summary>
    /// Gets or sets the quality tier of this item (Fine, Superior, Exceptional, Masterwork, Legendary).
    /// Quality provides stat bonuses that vary by item type (weapon vs armor).
    /// </summary>
    public ItemQuality? Quality { get; set; }

    /// <summary>
    /// Gets or sets the material this item is made from (Iron, Mithril, Oak, Dragonhide, etc.).
    /// Material provides durability, weight modifiers, and base attribute bonuses.
    /// </summary>
    public ItemMaterial? Material { get; set; }

    /// <summary>
    /// Gets or sets the list of prefix modifiers applied to this item.
    /// Prefixes provide various bonuses and appear before the base name.
    /// Multiple prefixes can be present, but only the first appears in the display name.
    /// </summary>
    public List<ItemPrefix> PrefixComponents { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of suffix modifiers applied to this item.
    /// Suffixes provide various bonuses and appear after the base name.
    /// Multiple suffixes can be present, but only the first appears in the display name.
    /// </summary>
    public List<ItemSuffix> SuffixComponents { get; set; } = new();

    /// <summary>
    /// Gets or sets the upgrade level of this item (+1, +2, +3, etc.).
    /// Higher upgrade levels increase attribute bonuses.
    /// </summary>
    public int UpgradeLevel { get; set; } = 0;

    /// <summary>
    /// Gets or sets the maximum number of enchantments that can be applied to this item.
    /// Determined at crafting time based on rarity and catalyst materials.
    /// </summary>
    public int MaxEnchantments { get; set; } = 0;

    /// <summary>
    /// Gets or sets the binding behavior of this item.
    /// </summary>
    public BindingType Binding { get; set; } = BindingType.Unbound;

    /// <summary>
    /// Gets or sets whether this item is currently bound to a character.
    /// </summary>
    public bool IsBound { get; set; } = false;

    /// <summary>
    /// Gets or sets the name of the character this item is bound to (if IsBound is true).
    /// </summary>
    public string? BoundToCharacter { get; set; } = null;

    /// <summary>
    /// Collection of enchantment reference IDs (v4.1 format) that can be applied to this item.
    /// ⚠️ HYBRID PATTERN: Both EnchantmentIds (templates) and Enchantments (resolved) exist.
    /// </summary>
    /// <remarks>
    /// <para><strong>✅ HOW TO RESOLVE - Use ReferenceResolverService:</strong></para>
    /// <code>
    /// // C# - Apply enchantments during item generation
    /// var resolver = new ReferenceResolverService(dataCache);
    /// var enchantments = new List&lt;ItemEnhancement&gt;();
    /// foreach (var refId in item.EnchantmentIds)
    /// {
    ///     var enchantJson = await resolver.ResolveToObjectAsync(refId);
    ///     var enchantment = enchantJson.ToObject&lt;ItemEnhancement&gt;();
    ///     enchantments.Add(enchantment);
    /// }
    /// item.Enchantments = enchantments; // Store resolved enchantments
    /// item.Name = GenerateEnchantedName(item.BaseName, enchantments);
    /// </code>
    /// <code>
    /// // GDScript - Apply enchantments in Godot
    /// var resolver = ReferenceResolverService.new(data_cache)
    /// var enchantments = []
    /// for ref_id in item.EnchantmentIds:
    ///     var enchant_data = await resolver.ResolveToObjectAsync(ref_id)
    ///     enchantments.append(enchant_data)
    /// item.enchantments = enchantments
    /// </code>
    /// <para><strong>⚠️ Hybrid Pattern Explained:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>EnchantmentIds</strong> = Template references from item catalog</description></item>
    /// <item><description><strong>Enchantments</strong> = Resolved enhancement objects baked into item</description></item>
    /// <item><description>IDs used during generation, then resolved objects stored</description></item>
    /// <item><description>At runtime, use Enchantments list (already resolved)</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// Example enchantment reference IDs:
    /// <code>
    /// [
    ///   "@items/enchantments/elemental:fire",
    ///   "@items/enchantments/attribute:strength-boost"
    /// ]
    /// </code>
    /// </example>
    public List<string> EnchantmentIds { get; set; } = new();

    /// <summary>
    /// Collection of material reference IDs (v4.1 format) this item can be crafted from.
    /// ⚠️ HYBRID PATTERN: Materials resolve to Material property string at generation time.
    /// </summary>
    /// <remarks>
    /// <para><strong>✅ HOW TO RESOLVE - Use ReferenceResolverService:</strong></para>
    /// <code>
    /// // C# - Apply material during item generation
    /// var resolver = new ReferenceResolverService(dataCache);
    /// if (item.MaterialIds.Any())
    /// {
    ///     var randomMaterialRefId = item.MaterialIds.PickRandom();
    ///     var materialJson = await resolver.ResolveToObjectAsync(randomMaterialRefId);
    ///     var material = materialJson.ToObject&lt;Material&gt;();
    ///     item.Material = material.Name; // Store resolved name
    ///     item.Name = $"{material.Name} {item.BaseName}";
    /// }
    /// </code>
    /// <code>
    /// // GDScript - Apply material in Godot
    /// var resolver = ReferenceResolverService.new(data_cache)
    /// if item.MaterialIds.size() > 0:
    ///     var mat_ref_id = item.MaterialIds.pick_random()
    ///     var mat_data = await resolver.ResolveToObjectAsync(mat_ref_id)
    ///     item.material = mat_data.name
    ///     item.name = mat_data.name + " " + item.base_name
    /// </code>
    /// <para><strong>⚠️ Hybrid Pattern:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>MaterialIds</strong> = Template references from item catalog</description></item>
    /// <item><description><strong>Material</strong> = Resolved material name string ("Iron", "Steel")</description></item>
    /// <item><description>IDs used during generation, then resolved name stored</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// Example material reference IDs:
    /// <code>
    /// [
    ///   "@materials/properties/metals:iron",
    ///   "@materials/properties/metals:steel",
    ///   "@materials/properties/woods:oak"
    /// ]
    /// </code>
    /// </example>
    public List<string> MaterialIds { get; set; } = new();

    /// <summary>
    /// Collection of item reference IDs (v4.1 format) required for crafting recipes or upgrades.
    /// Each ID is a JSON reference like "@items/materials/ingots:iron-ingot".
    /// </summary>
    /// <remarks>
    /// <para><strong>✅ HOW TO RESOLVE - Use ReferenceResolverService:</strong></para>
    /// <code>
    /// // C# - Check crafting requirements
    /// var resolver = new ReferenceResolverService(dataCache);
    /// var requiredItems = new List&lt;Item&gt;();
    /// foreach (var refId in item.RequiredItemIds)
    /// {
    ///     var itemJson = await resolver.ResolveToObjectAsync(refId);
    ///     var requiredItem = itemJson.ToObject&lt;Item&gt;();
    ///     requiredItems.Add(requiredItem);
    /// }
    /// bool canCraft = requiredItems.All(req => player.Inventory.Contains(req.Name));
    /// if (canCraft)
    /// {
    ///     CraftItem(item);
    ///     player.Inventory.RemoveRange(requiredItems);
    /// }
    /// </code>
    /// <code>
    /// // GDScript - Verify crafting materials in Godot
    /// var resolver = ReferenceResolverService.new(data_cache)
    /// var can_craft = true
    /// for ref_id in item.RequiredItemIds:
    ///     var required_item = await resolver.ResolveToObjectAsync(ref_id)
    ///     if not player.inventory.has_item(required_item.name):
    ///         can_craft = false
    ///         break
    /// if can_craft:
    ///     craft_item(item)
    /// </code>
    /// </remarks>
    /// <example>
    /// Example required item reference IDs:
    /// <code>
    /// [
    ///   "@items/materials/ingots:iron-ingot",
    ///   "@items/materials/leather:leather-padding"
    /// ]
    /// </code>
    /// </example>
    public List<string> RequiredItemIds { get; set; } = new();

    /// <summary>
    /// Fully resolved Item objects required for crafting this item.
    /// Populated by ItemGenerator.GenerateAsync() when hydrating templates.
    /// Not serialized to JSON (template IDs stored in RequiredItemIds instead).
    /// </summary>
    /// <remarks>
    /// <para><strong>For Runtime Use:</strong></para>
    /// <list type="bullet">
    /// <item><description>Use this property to check if player has crafting materials</description></item>
    /// <item><description>Already resolved - no need to call ReferenceResolverService</description></item>
    /// <item><description>Null if item loaded from template without hydration</description></item>
    /// </list>
    /// </remarks>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<Item> RequiredItems { get; set; } = new();



    /// <summary>
    /// Get the total value of a specific trait, including base item, enchantments, material, gems, and upgrade bonuses.
    /// </summary>
    /// <param name="traitName">Name of the trait (e.g., "Strength", "FireDamage")</param>
    /// <param name="defaultValue">Default value if trait not found</param>
    /// <returns>Total trait value with all bonuses applied</returns>
    public double GetTotalTrait(string traitName, double defaultValue = 0)
    {
        var totalTraits = GetTotalTraits();
        if (totalTraits.TryGetValue(traitName, out var traitValue))
        {
            return traitValue.AsDouble();
        }
        return defaultValue;
    }

    /// <summary>
    /// Get all traits merged from base item, material, enchantments, and gems.
    /// Follows trait merging rules from ITEM_ENHANCEMENT_SYSTEM.md.
    /// </summary>
    public Dictionary<string, TraitValue> GetTotalTraits()
    {
        var mergedTraits = new Dictionary<string, TraitValue>();

        // 1. Start with base item traits
        foreach (var trait in Traits)
        {
            mergedTraits[trait.Key] = trait.Value;
        }

        // 2. Add material traits (additive for numeric, override for text)
        foreach (var trait in MaterialTraits)
        {
            if (mergedTraits.ContainsKey(trait.Key))
            {
                // Merge existing trait
                var existing = mergedTraits[trait.Key];
                if (existing.Type == TraitType.Number && trait.Value.Type == TraitType.Number)
                {
                    // Numeric: add values
                    var sum = existing.AsDouble() + trait.Value.AsDouble();
                    mergedTraits[trait.Key] = new TraitValue(sum, TraitType.Number);
                }
                else
                {
                    // Text/Boolean: material overrides
                    mergedTraits[trait.Key] = trait.Value;
                }
            }
            else
            {
                // New trait from material
                mergedTraits[trait.Key] = trait.Value;
            }
        }

        // 3. Add enchantment traits (additive for numeric, last one wins for text)
        foreach (var enchantment in Enchantments)
        {
            foreach (var trait in enchantment.Traits)
            {
                if (mergedTraits.ContainsKey(trait.Key))
                {
                    var existing = mergedTraits[trait.Key];
                    if (existing.Type == TraitType.Number && trait.Value.Type == TraitType.Number)
                    {
                        var sum = existing.AsDouble() + trait.Value.AsDouble();
                        mergedTraits[trait.Key] = new TraitValue(sum, TraitType.Number);
                    }
                    else
                    {
                        mergedTraits[trait.Key] = trait.Value;
                    }
                }
                else
                {
                    mergedTraits[trait.Key] = trait.Value;
                }
            }
        }

        // 3a. Add player-applied enchantment traits (post-crafting)
        foreach (var enchantment in PlayerEnchantments)
        {
            foreach (var trait in enchantment.Traits)
            {
                if (mergedTraits.ContainsKey(trait.Key))
                {
                    var existing = mergedTraits[trait.Key];
                    if (existing.Type == TraitType.Number && trait.Value.Type == TraitType.Number)
                    {
                        var sum = existing.AsDouble() + trait.Value.AsDouble();
                        mergedTraits[trait.Key] = new TraitValue(sum, TraitType.Number);
                    }
                    else
                    {
                        mergedTraits[trait.Key] = trait.Value;
                    }
                }
                else
                {
                    mergedTraits[trait.Key] = trait.Value;
                }
            }
        }

        // 4. Add socket content traits (additive for numeric, last one wins for text)
        foreach (var socketList in Sockets.Values)
        {
            foreach (var socket in socketList)
            {
                if (socket.Content != null)
                {
                    foreach (var trait in socket.Content.Traits)
                    {
                        if (mergedTraits.ContainsKey(trait.Key))
                        {
                            var existing = mergedTraits[trait.Key];
                            if (existing.Type == TraitType.Number && trait.Value.Type == TraitType.Number)
                            {
                                var sum = existing.AsDouble() + trait.Value.AsDouble();
                                mergedTraits[trait.Key] = new TraitValue(sum, TraitType.Number);
                            }
                            else
                            {
                                mergedTraits[trait.Key] = trait.Value;
                            }
                        }
                        else
                        {
                            mergedTraits[trait.Key] = trait.Value;
                        }
                    }
                }
            }
        }

        // 5. Add upgrade level bonuses (+2 per level to all numeric traits)
        // Design: ITEM_ENHANCEMENT_SYSTEM.md - "+2 to attribute bonuses"
        if (UpgradeLevel > 0)
        {
            // Calculate bonus: +2 per upgrade level
            // Examples: +1=+2, +5=+10, +8=+16, +10=+20
            var upgradeBonus = UpgradeLevel * 2.0;

            // Apply to all numeric traits
            foreach (var traitKey in mergedTraits.Keys.ToList())
            {
                if (mergedTraits[traitKey].Type == TraitType.Number)
                {
                    var baseValue = mergedTraits[traitKey].AsDouble();
                    var upgradedValue = baseValue + upgradeBonus;
                    mergedTraits[traitKey] = new TraitValue(upgradedValue, TraitType.Number);
                }
            }
        }

        return mergedTraits;
    }

    /// <summary>
    /// Get the display name for this item including upgrade level and enchantments.
    /// </summary>
    public string GetDisplayName()
    {
        var nameParts = new List<string>();

        // Add upgrade level prefix
        if (UpgradeLevel > 0)
        {
            nameParts.Add($"+{UpgradeLevel}");
        }

        // Add base name
        nameParts.Add(Name);

        // Add generation enchantment suffixes
        foreach (var enchantment in Enchantments)
        {
            nameParts.Add($"({enchantment.Name})");
        }

        // Add player-applied enchantment suffixes
        foreach (var enchantment in PlayerEnchantments)
        {
            nameParts.Add($"[{enchantment.Name}]");
        }

        return string.Join(" ", nameParts);
    }

    /// <summary>
    /// Get rich socket information for all socket types on this item.
    /// Useful for Godot UI display.
    /// </summary>
    public List<SocketInfo> GetSocketsInfo()
    {
        return Sockets
            .Where(kvp => kvp.Value.Any())
            .Select(kvp => new SocketInfo
            {
                Type = kvp.Key,
                Sockets = kvp.Value,
                FilledCount = kvp.Value.Count(s => s.Content != null),
                TotalCount = kvp.Value.Count
            })
            .OrderBy(info => info.Type)
            .ToList();
    }

    /// <summary>
    /// Get a display string showing all socket types and their fill status.
    /// Example: "Gem: 1/2 | Essence: 0/1 | Rune: 3/3"
    /// </summary>
    public string GetSocketsDisplayText()
    {
        var infos = GetSocketsInfo();
        if (!infos.Any()) return string.Empty;

        return string.Join(" | ", infos.Select(info => info.DisplayText));
    }

    /// <summary>
    /// Check if this item can accept an additional player-applied enchantment.
    /// </summary>
    public bool CanAddPlayerEnchantment() => PlayerEnchantments.Count < MaxPlayerEnchantments;

    /// <summary>
    /// Check if this item has any player enchantment slots.
    /// </summary>
    public bool HasPlayerEnchantmentSlots() => MaxPlayerEnchantments > 0;

    /// <summary>
    /// Get the number of available (unfilled) player enchantment slots.
    /// </summary>
    public int AvailablePlayerEnchantmentSlots() => MaxPlayerEnchantments - PlayerEnchantments.Count;

    /// <summary>
    /// Get the maximum upgrade level allowed for this item based on rarity.
    /// Common/Uncommon: +5, Rare: +7, Epic: +9, Legendary: +10
    /// </summary>
    public int GetMaxUpgradeLevel() => Rarity switch
    {
        ItemRarity.Common => 5,
        ItemRarity.Uncommon => 5,
        ItemRarity.Rare => 7,
        ItemRarity.Epic => 9,
        ItemRarity.Legendary => 10,
        _ => 0
    };

    /// <summary>
    /// Check if this item can be upgraded further.
    /// </summary>
    public bool CanUpgrade() => UpgradeLevel < GetMaxUpgradeLevel();

    /// <summary>
    /// Bind this item to a specific character.
    /// </summary>
    public void BindToCharacter(string characterName)
    {
        IsBound = true;
        BoundToCharacter = characterName;
    }

    /// <summary>
    /// Checks if this item can stack with another item.
    /// Items can stack if they are stackable, have the same name, type, and no unique properties (like enchantments).
    /// </summary>
    public bool CanStackWith(Item other)
    {
        if (!IsStackable || !other.IsStackable)
            return false;

        // Must have same name and type
        if (Name != other.Name || Type != other.Type)
            return false;

        // Items with enchantments, sockets, or upgrades cannot stack
        if (PlayerEnchantments.Count > 0 || other.PlayerEnchantments.Count > 0)
            return false;

        if (Sockets.Count > 0 || other.Sockets.Count > 0)
            return false;

        if (UpgradeLevel > 0 || other.UpgradeLevel > 0)
            return false;

        // Items with different materials cannot stack
        if (Material != other.Material)
            return false;

        return true;
    }

    /// <summary>
    /// Adds quantity to this item stack.
    /// </summary>
    public void AddQuantity(int amount)
    {
        if (!IsStackable)
            throw new InvalidOperationException($"Cannot add quantity to non-stackable item: {Name}");

        Quantity += amount;
    }

    /// <summary>
    /// Removes quantity from this item stack.
    /// </summary>
    public bool RemoveQuantity(int amount)
    {
        if (!IsStackable)
            throw new InvalidOperationException($"Cannot remove quantity from non-stackable item: {Name}");

        if (amount > Quantity)
            return false;

        Quantity -= amount;
        return true;
    }

    // ========================================
    // NEW v4.3 Methods
    // ========================================

    /// <summary>
    /// Gets the full display name of the item with quality, material, first prefix, and first suffix.
    /// Format: "{Quality} {Material} {FirstPrefix} {BaseName} {FirstSuffix}"
    /// Example: "Fine Iron Flaming Longsword of the Bear"
    /// </summary>
    public string GetFullDisplayName()
    {
        var parts = new List<string>();

        if (Quality != null)
            parts.Add(Quality.Name);

        if (Material != null)
            parts.Add(Material.Name);

        if (PrefixComponents.Count > 0)
            parts.Add(PrefixComponents[0].Name);

        parts.Add(BaseName);

        if (SuffixComponents.Count > 0)
            parts.Add(SuffixComponents[0].Name);

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Gets the short name of the item without quality or material.
    /// Format: "{FirstPrefix} {BaseName} {FirstSuffix}"
    /// Example: "Flaming Longsword of the Bear"
    /// </summary>
    public string GetShortName()
    {
        var parts = new List<string>();

        if (PrefixComponents.Count > 0)
            parts.Add(PrefixComponents[0].Name);

        parts.Add(BaseName);

        if (SuffixComponents.Count > 0)
            parts.Add(SuffixComponents[0].Name);

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Gets structured tooltip data with bonuses broken down by source.
    /// Enables clear attribution of traits to specific components.
    /// </summary>
    public ItemTooltipData GetTooltipData()
    {
        var tooltip = new ItemTooltipData
        {
            DisplayName = GetFullDisplayName(),
            Rarity = Rarity,
            Type = Type,
            Lore = Lore
        };

        // Base section
        tooltip.BaseSection.Header = BaseName;
        tooltip.BaseSection.Bonuses = FormatTraits(Traits);

        // Quality section
        if (Quality != null)
        {
            tooltip.QualitySection = new TooltipSection
            {
                Header = $"Quality: {Quality.Name}",
                Bonuses = FormatTraits(Quality.Traits)
            };
        }

        // Material section
        if (Material != null)
        {
            tooltip.MaterialSection = new TooltipSection
            {
                Header = $"Material: {Material.Name}",
                Bonuses = FormatTraits(Material.Traits)
            };
        }

        // Prefix sections
        foreach (var prefix in PrefixComponents)
        {
            tooltip.PrefixSections.Add(new TooltipSection
            {
                Header = prefix.Name,
                Bonuses = FormatTraits(prefix.Traits)
            });
        }

        // Suffix sections
        foreach (var suffix in SuffixComponents)
        {
            tooltip.SuffixSections.Add(new TooltipSection
            {
                Header = suffix.Name,
                Bonuses = FormatTraits(suffix.Traits)
            });
        }

        // Enchantment sections
        foreach (var enchantment in Enchantments)
        {
            tooltip.EnchantmentSections.Add(new TooltipSection
            {
                Header = $"Enchantment: {enchantment.Name}",
                Bonuses = FormatTraits(enchantment.Traits)
            });
        }

        return tooltip;
    }

    /// <summary>
    /// Formats traits into human-readable bonus strings.
    /// </summary>
    private List<string> FormatTraits(Dictionary<string, object> traits)
    {
        var bonuses = new List<string>();
        foreach (var trait in traits)
        {
            bonuses.Add($"{trait.Key}: {trait.Value}");
        }
        return bonuses;
    }

    /// <summary>
    /// Formats trait values into human-readable bonus strings.
    /// </summary>
    private List<string> FormatTraits(Dictionary<string, TraitValue> traits)
    {
        var bonuses = new List<string>();
        foreach (var trait in traits)
        {
            var value = trait.Value.Type == TraitType.Number 
                ? trait.Value.AsDouble().ToString()
                : trait.Value.AsString();
            bonuses.Add($"{trait.Key}: {value}");
        }
        return bonuses;
    }
}

/// <summary>
/// Represents weapon damage configuration.
/// </summary>
public class ItemDamage
{
    /// <summary>
    /// Gets or sets the minimum damage value.
    /// </summary>
    public int Min { get; set; } = 1;

    /// <summary>
    /// Gets or sets the maximum damage value.
    /// </summary>
    public int Max { get; set; } = 4;

    /// <summary>
    /// Gets or sets the damage modifier formula (e.g., "wielder.strength_mod").
    /// </summary>
    public string Modifier { get; set; } = string.Empty;
}

/// <summary>
/// Defines how and when an item becomes bound to a character.
/// </summary>
public enum BindingType
{
    /// <summary>Item can be freely traded and sold.</summary>
    Unbound,

    /// <summary>Item binds to character when equipped.</summary>
    BindOnEquip,

    /// <summary>Item binds to character when enchanted (for enchantment scrolls).</summary>
    BindOnApply,

    /// <summary>Item is permanently bound to character (quest rewards).</summary>
    CharacterBound
}
