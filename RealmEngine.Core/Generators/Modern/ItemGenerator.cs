using RealmEngine.Data.Services;
using RealmEngine.Shared.Models;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Services.Budget;

namespace RealmEngine.Core.Generators.Modern;

/// <summary>
/// Generates items using the Hybrid Enhancement System v1.0 and Budget-Based Generation v2.0.
/// Supports materials (baked), enchantments (baked), and gem sockets (player customizable).
/// </summary>
public class ItemGenerator
{
    private readonly GameDataCache _dataCache;
    private readonly ReferenceResolverService _referenceResolver;
    private readonly Random _random;
    private readonly ILogger<ItemGenerator> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly NameComposer _nameComposer;
    private EnchantmentGenerator? _enchantmentGenerator;
    private BudgetItemGenerationService? _budgetGenerator;
    private SocketGenerator? _socketGenerator;

    /// <summary>
    /// Initializes a new instance of the ItemGenerator class.
    /// </summary>
    /// <param name="dataCache">The game data cache for accessing item catalog files.</param>
    /// <param name="referenceResolver">The reference resolver for resolving JSON references.</param>
    /// <param name="logger">Logger for this generator.</param>
    /// <param name="loggerFactory">Factory for creating loggers for sub-generators.</param>
    public ItemGenerator(GameDataCache dataCache, ReferenceResolverService referenceResolver, ILogger<ItemGenerator> logger, ILoggerFactory loggerFactory)
    {
        _dataCache = dataCache ?? throw new ArgumentNullException(nameof(dataCache));
        _referenceResolver = referenceResolver ?? throw new ArgumentNullException(nameof(referenceResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _random = new Random();
        _nameComposer = new NameComposer(NullLogger<NameComposer>.Instance);
    }

    /// <summary>
    /// Gets or creates the enchantment generator (lazy initialization).
    /// </summary>
    private EnchantmentGenerator EnchantmentGenerator
    {
        get
        {
            _enchantmentGenerator ??= new EnchantmentGenerator(_dataCache, _referenceResolver, _loggerFactory.CreateLogger<EnchantmentGenerator>());
            return _enchantmentGenerator;
        }
    }

    /// <summary>
    /// Gets or creates the socket generator (lazy initialization).
    /// </summary>
    private SocketGenerator SocketGenerator
    {
        get
        {
            _socketGenerator ??= new SocketGenerator(_dataCache, _loggerFactory.CreateLogger<SocketGenerator>());
            return _socketGenerator;
        }
    }

    /// <summary>
    /// Gets or creates the budget generator (lazy initialization).
    /// </summary>
    private BudgetItemGenerationService BudgetGenerator
    {
        get
        {
            if (_budgetGenerator == null)
            {
                var configFactory = new BudgetConfigFactory(_dataCache, _loggerFactory.CreateLogger<BudgetConfigFactory>());
                var budgetConfig = configFactory.GetBudgetConfig();
                var materialPools = configFactory.GetMaterialPools();
                var enemyTypes = configFactory.GetEnemyTypes();

                var budgetCalculator = new BudgetCalculator(budgetConfig, _loggerFactory.CreateLogger<BudgetCalculator>());
                var materialPoolService = new MaterialPoolService(
                    _dataCache,
                    _referenceResolver,
                    budgetCalculator,
                    materialPools,
                    enemyTypes,
                    _loggerFactory.CreateLogger<MaterialPoolService>());

                _budgetGenerator = new BudgetItemGenerationService(
                    _dataCache,
                    _referenceResolver,
                    budgetCalculator,
                    materialPoolService,
                    _loggerFactory.CreateLogger<BudgetItemGenerationService>());
            }
            return _budgetGenerator;
        }
    }

    /// <summary>
    /// Generates a list of random items from a specific category.
    /// Items may include materials, enchantments, and gem sockets based on the Hybrid Enhancement System.
    /// </summary>
    /// <param name="category">The item category (e.g., "weapons", "armor", "consumables").</param>
    /// <param name="count">The number of items to generate (default: 10).</param>
    /// <param name="hydrate">If true, populates resolved RequiredItems properties (default: true).</param>
    /// <returns>A list of generated Item instances.</returns>
    public async Task<List<Item>> GenerateItemsAsync(string category, int count = 10, bool hydrate = true)
    {
        try
        {
            var catalogFile = _dataCache.GetFile($"items/{category}/catalog.json");
            if (catalogFile?.JsonData == null)
            {
                return new List<Item>();
            }

            var catalog = catalogFile.JsonData;
            var items = GetItemsFromCatalog(catalog);
            
            if (items == null || !items.Any())
            {
                return new List<Item>();
            }

            var result = new List<Item>();

            for (int i = 0; i < count; i++)
            {
                var randomItem = GetRandomWeightedItem(items);
                if (randomItem != null)
                {
                    var item = await ConvertToItemAsync(randomItem, category);
                    if (item != null)
                    {
                        if (hydrate)
                        {
                            await HydrateItemAsync(item);
                        }
                        result.Add(item);
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating items for category {Category}", category);
            return new List<Item>();
        }
    }

    /// <summary>
    /// Generates a specific item by name from a category.
    /// The item will include enhancement system features (materials, enchantments, gem sockets).
    /// </summary>
    /// <param name="category">The item category to search in.</param>
    /// <param name="itemName">The name of the item to generate.</param>
    /// <param name="hydrate">If true, populates resolved RequiredItems properties (default: true).</param>
    /// <returns>The generated Item instance, or null if not found.</returns>
    public async Task<Item?> GenerateItemByNameAsync(string category, string itemName, bool hydrate = true)
    {
        try
        {
            var catalogFile = _dataCache.GetFile($"items/{category}/catalog.json");
            if (catalogFile?.JsonData == null)
            {
                return null;
            }

            var catalog = catalogFile.JsonData;
            var items = GetItemsFromCatalog(catalog);
            
            var catalogItem = items?.FirstOrDefault(i => 
                string.Equals(GetStringProperty(i, "name"), itemName, StringComparison.OrdinalIgnoreCase));

            if (catalogItem != null)
            {
                var item = await ConvertToItemAsync(catalogItem, category);
                if (item != null && hydrate)
                {
                    await HydrateItemAsync(item);
                }
                return item;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating item {ItemName} from category {Category}", itemName, category);
            return null;
        }
    }

    private static IEnumerable<JToken>? GetItemsFromCatalog(JToken catalog)
    {
        var allItems = new List<JToken>();
        
        // Try different possible structures
        if (catalog["items"] != null)
        {
            return catalog["items"]?.Children();
        }
        
        // For hierarchical catalogs like weapon_types, armor_types, etc.
        // Look for type containers (weapon_types, armor_types, etc.)
        foreach (var property in catalog.Children<JProperty>())
        {
            // Skip metadata
            if (property.Name == "metadata") continue;
            
            // Check if this property has nested items arrays
            var typeContainer = property.Value;
            if (typeContainer is JObject typeObj)
            {
                // Look through each weapon/armor type (swords, axes, etc.)
                foreach (var typeProperty in typeObj.Children<JProperty>())
                {
                    var items = typeProperty.Value["items"];
                    if (items != null)
                    {
                        foreach (var item in items.Children())
                        {
                            allItems.Add(item);
                        }
                    }
                }
            }
        }

        return allItems.Any() ? allItems : null;
    }

    private async Task<Item?> ConvertToItemAsync(JToken catalogItem, string category)
    {
        try
        {
            var baseName = GetStringProperty(catalogItem, "name") ?? "Unknown Item";
            
            var item = new Item
            {
                Id = $"{category}:{baseName}",
                Slug = GetStringProperty(catalogItem, "slug") ?? string.Empty,
                BaseName = baseName,
                Name = baseName, // Will be updated after enhancements
                Description = GetStringProperty(catalogItem, "description") ?? "No description available",
                Price = GetIntProperty(catalogItem, "value", 1),
                Weight = GetDoubleProperty(catalogItem, "weight", 0.0),
                BaseAttributes = new Dictionary<string, int>(), // Populated from name components during generation
                Stats = GetDictionaryProperty<string>(catalogItem, "stats"),
                ArmorClass = GetStringProperty(catalogItem, "armorClass"),
                Effect = GetStringProperty(catalogItem, "effect"),
                Power = GetIntProperty(catalogItem, "power", 0),
                Duration = GetIntProperty(catalogItem, "duration", 0)
            };

            // Resolve item type from category
            item.Type = category switch
            {
                "weapons" => ItemType.Weapon,
                "armor" => ItemType.Chest,
                "consumables" => ItemType.Consumable,
                "shields" => ItemType.Shield,
                _ => ItemType.Consumable
            };

            // Read stackSize from catalog and set stackability
            item.StackSize = GetIntProperty(catalogItem, "stackSize", 1);
            item.IsStackable = item.StackSize > 1;

            // Base rarity weight from catalog
            var baseRarityWeight = GetIntProperty(catalogItem, "rarityWeight", 50);
            item.TotalRarityWeight = baseRarityWeight;

            // Resolve enchantment references
            if (catalogItem["enchantments"] is JArray enchantments)
            {
                item.EnchantmentIds = await ResolveReferencesAsync(enchantments);
            }

            // Resolve material references
            if (catalogItem["materials"] is JArray materials)
            {
                item.MaterialIds = await ResolveReferencesAsync(materials);
            }

            // Resolve required item references
            if (catalogItem["requiredItems"] is JArray requiredItems)
            {
                item.RequiredItemIds = await ResolveReferencesAsync(requiredItems);
            }

            // Apply traits from catalog
            await ApplyTraitsFromCatalogAsync(item, catalogItem);

            // Apply enhancements from names.json pattern (v4.2)
            await ApplyEnhancementsFromPatternAsync(item, category);

            // Calculate final rarity from total weight
            item.Rarity = ConvertWeightToRarity(item.TotalRarityWeight);

            // Generate sockets based on item rarity and type
            try
            {
                var socketList = SocketGenerator.GenerateSockets(item.Rarity, item.Type, item.Material?.Name);
                if (socketList.Count > 0)
                {
                    // Group sockets by type for the dictionary structure
                    item.Sockets = socketList
                        .GroupBy(s => s.Type)
                        .ToDictionary(g => g.Key, g => g.ToList());
                    
                    _logger.LogDebug("Generated {SocketCount} sockets for {ItemName}", socketList.Count, item.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate sockets for {ItemName}, continuing without sockets", item.Name);
            }

            // Update item name with enhancements
            item.Name = BuildEnhancedName(item);

            return item;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting catalog item to Item");
            return null;
        }
    }

    /// <summary>
    /// Apply enhancements using budget-based generation system.
    /// Uses the multi-step budget process: material selection → base selection → pattern → components.
    /// </summary>
    private async Task ApplyEnhancementsFromPatternAsync(Item item, string category)
    {
        try
        {
            // Use budget-based generation to apply materials and enhancements
            // Create a default budget request for simple item generation
            var request = new BudgetItemRequest
            {
                ItemCategory = category,
                EnemyType = "common", // Default to common enemy type
                EnemyLevel = 5, // Default level 5 (mid-tier)
                IsBoss = false,
                IsElite = false,
                AllowQuality = true
            };

            var budgetResult = await BudgetGenerator.GenerateItemAsync(request);
            if (budgetResult == null) return;

            // Apply material from budget generation
            if (budgetResult.Material != null)
            {
                item.Material = ConvertToItemMaterial(budgetResult.Material);
                var materialName = item.Material?.Name ?? string.Empty;
                
                if (!string.IsNullOrEmpty(materialName))
                {
                    // Apply material traits
                    var traits = budgetResult.Material["traits"];
                    if (traits != null && traits.Type == JTokenType.Object)
                    {
                        foreach (var traitProp in traits.Children<JProperty>())
                        {
                            var traitName = traitProp.Name;
                            var traitValue = traitProp.Value;
                            
                            object? value;
                            TraitType type;
                            
                            if (traitValue.Type == JTokenType.Object && traitValue["value"] != null)
                            {
                                value = traitValue["value"]?.ToObject<object>();
                                type = ParseTraitType(traitValue["type"]?.ToString() ?? "number");
                            }
                            else
                            {
                                value = traitValue.ToObject<object>();
                                type = ParseTraitType(traitValue.Type.ToString());
                            }
                            
                            item.MaterialTraits[traitName] = new TraitValue
                            {
                                Value = value,
                                Type = type
                            };
                        }
                    }
                    
                    // Add material rarity weight
                    var materialWeight = GetIntProperty(budgetResult.Material, "rarityWeight", 0);
                    if (materialWeight == 0)
                    {
                        materialWeight = GetIntProperty(budgetResult.Material, "selectionWeight", 0);
                    }
                    item.TotalRarityWeight += materialWeight;
                }
            }

            // Apply components from budget generation
            foreach (var component in budgetResult.Components)
            {
                var value = GetStringProperty(component, "value");
                var token = GetStringProperty(component, "token") ?? "component";
                
                if (!string.IsNullOrEmpty(value))
                {
                    // Determine if prefix or suffix based on token name
                    var isPrefixToken = token is "prefix" or "quality" or "descriptive";
                    
                    // Note: Component tracking not implemented yet - will be added when v4.3 migration completes
                }
            }

            // Legacy pattern-based support for enchantments and sockets
            var namesPath = $"items/{category}/names.json";
            if (_dataCache.FileExists(namesPath))
            {
                var namesFile = _dataCache.GetFile(namesPath);
                if (namesFile?.JsonData != null)
                {
                    var patterns = namesFile.JsonData["patterns"];
                    if (patterns != null)
                    {
                        var pattern = GetRandomWeightedPattern(patterns);
                        if (pattern != null)
                        {
                            // Apply enchantments from enchantmentSlots
                            var enchantmentSlots = pattern["enchantmentSlots"];
                            if (enchantmentSlots != null && enchantmentSlots.Any())
                            {
                                await GenerateEnchantmentsAsync(item, enchantmentSlots);
                            }

                            // Generate sockets from socketSlots
                            var socketSlots = pattern["socketSlots"];
                            if (socketSlots != null)
                            {
                                GenerateSockets(item, socketSlots);
                            }
                            else
                            {
                                var gemSocketCount = pattern["gemSocketCount"];
                                if (gemSocketCount != null)
                                {
                                    GenerateGemSocketsLegacy(item, gemSocketCount);
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying enhancements");
        }
    }

    private async Task<List<string>> ResolveReferencesAsync(JArray? referenceArray)
    {
        var resolvedIds = new List<string>();
        if (referenceArray == null) return resolvedIds;

        foreach (var item in referenceArray)
        {
            var reference = item.ToString();
            
            if (reference.StartsWith("@"))
            {
                var resolvedId = await _referenceResolver.ResolveAsync(reference);
                if (resolvedId != null)
                {
                    resolvedIds.Add(resolvedId.ToString() ?? string.Empty);
                }
            }
        }

        return resolvedIds;
    }

    /// <summary>
    /// Select and apply a material to the item.
    /// </summary>
    private async Task SelectMaterialAsync(Item item, string materialReference)
    {
        try
        {
            // Use reference resolver to get materials
            var materialsObj = await _referenceResolver.ResolveAsync(materialReference);
            if (materialsObj == null) return;

            // Cast to JArray
            var materials = materialsObj as JArray;
            if (materials == null || !materials.Any()) return;

            // Select random material by weight
            var material = GetRandomWeightedItem(materials);
            if (material == null) return;

            item.Material = ConvertToItemMaterial(material);
            
            // Add material rarity weight to total
            var materialWeight = GetIntProperty(material, "rarityWeight", 0);
            item.TotalRarityWeight += materialWeight;

            // Apply material traits
            var traits = material["traits"];
            if (traits != null && traits.Type == JTokenType.Object)
            {
                foreach (var traitProp in traits.Children<JProperty>())
                {
                    var traitName = traitProp.Name;
                    var traitValue = traitProp.Value;
                    
                    // Handle both simple values and structured trait objects
                    object? value;
                    TraitType type;
                    
                    if (traitValue.Type == JTokenType.Object && traitValue["value"] != null)
                    {
                        // Structured trait: { "value": 100, "type": "number" }
                        value = traitValue["value"]?.ToObject<object>();
                        type = ParseTraitType(traitValue["type"]?.ToString() ?? "number");
                    }
                    else
                    {
                        // Simple trait: just a value (100, 1.5, "metal", etc.)
                        value = traitValue.ToObject<object>();
                        type = ParseTraitType(traitValue.Type.ToString());
                    }
                    
                    item.MaterialTraits[traitName] = new TraitValue
                    {
                        Value = value,
                        Type = type
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error selecting material");
        }
    }

    /// <summary>
    /// Generate enchantments for the item based on enchantmentSlots.
    /// </summary>
    private async Task GenerateEnchantmentsAsync(Item item, JToken enchantmentSlots)
    {
        try
        {
            foreach (var slot in enchantmentSlots.Children())
            {
                var slotPosition = GetStringProperty(slot, "position");
                var reference = GetStringProperty(slot, "reference");
                
                if (string.IsNullOrEmpty(reference)) continue;

                var enchantment = await EnchantmentGenerator.GenerateEnchantmentAsync(reference);
                if (enchantment != null)
                {
                    // Only override position if reference is NOT to enchantments domain
                    // Enchantments from @items/enchantments have position metadata in names.json
                    // Materials and other references need position from slot
                    if (!reference.StartsWith("@items/enchantments", StringComparison.OrdinalIgnoreCase) 
                        && !string.IsNullOrEmpty(slotPosition))
                    {
                        enchantment.Position = slotPosition.ToLower() == "prefix" 
                            ? EnchantmentPosition.Prefix 
                            : EnchantmentPosition.Suffix;
                    }
                    
                    item.Enchantments.Add(enchantment);
                    
                    // Add enchantment weight to total
                    item.TotalRarityWeight += enchantment.RarityWeight;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating enchantments");
        }
    }

    /// <summary>
    /// Generate sockets for the item using the new multi-type socket system.
    /// Supports weighted generation of Gem, Essence, Rune, Crystal, and Orb sockets.
    /// </summary>
    private void GenerateSockets(Item item, JToken socketSlots)
    {
        try
        {
            // socketSlots format: array of { "type": "gem", "count": { "min": 0, "max": 2 }, "rarityWeight": 80 }
            foreach (var slotConfig in socketSlots.Children())
            {
                var socketTypeName = GetStringProperty(slotConfig, "type");
                if (string.IsNullOrEmpty(socketTypeName)) continue;
                
                // Parse socket type
                if (!Enum.TryParse<SocketType>(socketTypeName, true, out var socketType))
                {
                    _logger.LogWarning("Invalid socket type: {SocketType}", socketTypeName);
                    continue;
                }
                
                var weight = GetIntProperty(slotConfig, "rarityWeight", 0);
                
                // Roll 1: Should this socket type appear?
                if (!RollForSocketType(weight, item.TotalRarityWeight))
                    continue;
                
                var countConfig = slotConfig["count"];
                if (countConfig == null) continue;
                
                var min = GetIntProperty(countConfig, "min", 0);
                var max = GetIntProperty(countConfig, "max", 0);
                
                if (max == 0) continue;
                
                // Roll 2: How many sockets? (weighted toward max for higher rarity)
                var count = GetWeightedSocketCount(min, max, item.TotalRarityWeight);
                
                if (count == 0) continue;
                
                var sockets = new List<Socket>();
                for (int i = 0; i < count; i++)
                {
                    sockets.Add(new Socket 
                    { 
                        Type = socketType,
                        Content = null,
                        IsLocked = false
                    });
                    
                    // Each socket adds to rarity weight
                    item.TotalRarityWeight += 10;
                }
                
                item.Sockets[socketType] = sockets;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating sockets");
        }
    }
    
    /// <summary>
    /// Roll to determine if a socket type should appear on the item.
    /// Combines socket weight and item rarity for probability.
    /// </summary>
    private bool RollForSocketType(int socketWeight, int itemRarityWeight)
    {
        if (socketWeight == 0) return false;
        
        // Combine socket weight + half of item rarity for effective weight
        var effectiveWeight = socketWeight + (itemRarityWeight / 2);
        var roll = _random.Next(1, 200);
        return roll <= effectiveWeight;
    }
    
    /// <summary>
    /// Get a weighted socket count biased toward max for higher rarity items.
    /// </summary>
    private int GetWeightedSocketCount(int min, int max, int itemRarityWeight)
    {
        if (max == min) return min;
        
        var range = max - min;
        // Higher rarity = bias toward max (0.0 to 1.0 bonus)
        var rarityBonus = Math.Min(itemRarityWeight / 150.0, 1.0);
        var weightedRoll = _random.NextDouble() + (rarityBonus * 0.3);
        
        return min + (int)(range * Math.Min(weightedRoll, 1.0));
    }
    
    /// <summary>
    /// Legacy support: Generate gem sockets from old gemSocketCount format.
    /// Converts to new socket system format.
    /// </summary>
    private void GenerateGemSocketsLegacy(Item item, JToken gemSocketCount)
    {
        try
        {
            var min = GetIntProperty(gemSocketCount, "min", 0);
            var max = GetIntProperty(gemSocketCount, "max", 0);
            
            if (max == 0) return;

            var socketCount = _random.Next(min, max + 1);
            
            var sockets = new List<Socket>();
            for (int i = 0; i < socketCount; i++)
            {
                sockets.Add(new Socket
                {
                    Type = SocketType.Gem,
                    Content = null,
                    IsLocked = false
                });
                
                // Each socket adds 10 to rarity weight
                item.TotalRarityWeight += 10;
            }
            
            if (sockets.Any())
            {
                item.Sockets[SocketType.Gem] = sockets;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating legacy gem sockets");
        }
    }

    /// <summary>
    /// Build the enhanced item name from all components and populate naming component properties.
    /// Order: [Enchantment Prefixes] [Pattern Prefixes] [BaseName] [Pattern Suffixes] [Enchantment Suffixes] [Sockets]
    /// </summary>
    private string BuildEnhancedName(Item item)
    {
        var nameParts = new List<string>();
        
        // NOTE: Using new component system (Quality, Material, PrefixComponents, SuffixComponents)
        
        // 1. Prefix enchantments
        var prefixEnchantments = item.Enchantments
            .Where(e => e.Position == EnchantmentPosition.Prefix)
            .ToList();
        
        foreach (var enchantment in prefixEnchantments)
        {
            nameParts.Add(enchantment.Name);
        }

        // 2. Quality and Material
        if (item.Quality != null)
            nameParts.Add(item.Quality.Name);
            
        if (item.Material != null)
            nameParts.Add(item.Material.Name);

        // 3. First prefix only (multiple tracked but only first displayed)
        if (item.PrefixComponents.Count > 0)
        {
            nameParts.Add(item.PrefixComponents[0].Name);
        }

        // 4. Base name
        nameParts.Add(item.BaseName);

        // 5. First suffix only (multiple tracked but only first displayed)
        if (item.SuffixComponents.Count > 0)
        {
            nameParts.Add(item.SuffixComponents[0].Name);
        }

        // 6. Suffix enchantments
        var suffixEnchantments = item.Enchantments
            .Where(e => e.Position == EnchantmentPosition.Suffix)
            .ToList();
        
        foreach (var enchantment in suffixEnchantments)
        {
            nameParts.Add(enchantment.Name);
        }

        // 6. Socket indicator (separate from naming components per design decision)
        var socketsDisplay = item.GetSocketsDisplayText();
        if (!string.IsNullOrEmpty(socketsDisplay))
        {
            nameParts.Add($"[{socketsDisplay}]");
        }

        return string.Join(" ", nameParts);
    }

    /// <summary>
    /// Apply traits from catalog item to the item model.
    /// </summary>
    private Task ApplyTraitsFromCatalogAsync(Item item, JToken catalogItem)
    {
        try
        {
            var traits = catalogItem["traits"];
            if (traits != null && traits.Type == JTokenType.Object)
            {
                foreach (var traitProp in traits.Children<JProperty>())
                {
                    var traitName = traitProp.Name;
                    var traitData = traitProp.Value;
                    
                    // If trait data is a simple value (not an object), create TraitValue directly
                    if (traitData.Type != JTokenType.Object)
                    {
                        item.Traits[traitName] = new TraitValue
                        {
                            Value = traitData.ToObject<object>(),
                            Type = ParseTraitType(traitData.Type.ToString())
                        };
                    }
                    else
                    {
                        // Trait data is an object with value and type
                        var traitValue = new TraitValue
                        {
                            Value = traitData["value"]?.ToObject<object>(),
                            Type = ParseTraitType(traitData["type"]?.ToString() ?? "number")
                        };
                        item.Traits[traitName] = traitValue;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying traits");
        }
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Get a random gem color for socket generation.
    /// </summary>
    private GemColor GetRandomGemColor()
    {
        var colors = Enum.GetValues<GemColor>();
        return colors[_random.Next(colors.Length)];
    }

    /// <summary>
    /// Parse trait type string to TraitType enum.
    /// </summary>
    private static TraitType ParseTraitType(string typeString)
    {
        return typeString?.ToLower() switch
        {
            "number" or "integer" or "float" => TraitType.Number,
            "string" => TraitType.String,
            "boolean" => TraitType.Boolean,
            "stringarray" or "array" => TraitType.StringArray,
            "numberarray" => TraitType.NumberArray,
            _ => TraitType.Number
        };
    }

    /// <summary>
    /// Get a random weighted pattern from the patterns array.
    /// </summary>
    private JToken? GetRandomWeightedPattern(JToken patterns)
    {
        var patternList = patterns.Children().ToList();
        if (!patternList.Any()) return null;

        var totalWeight = patternList.Sum(p => GetIntProperty(p, "rarityWeight", 1));
        var randomValue = _random.Next(1, totalWeight + 1);

        int currentWeight = 0;
        foreach (var pattern in patternList)
        {
            currentWeight += GetIntProperty(pattern, "rarityWeight", 1);
            if (randomValue <= currentWeight)
            {
                return pattern;
            }
        }

        return patternList.First();
    }

    private JToken? GetRandomWeightedItem(IEnumerable<JToken> items)
    {
        var itemList = items.ToList();
        if (!itemList.Any()) return null;

        var totalWeight = itemList.Sum(item => GetIntProperty(item, "rarityWeight", 1));
        var randomValue = _random.Next(1, totalWeight + 1);

        int currentWeight = 0;
        foreach (var item in itemList)
        {
            currentWeight += GetIntProperty(item, "rarityWeight", 1);
            if (randomValue <= currentWeight)
            {
                return item;
            }
        }

        return itemList.First();
    }

    private static int GetIntProperty(JToken obj, string propertyName, int defaultValue)
    {
        try
        {
            var value = obj[propertyName];
            return value != null ? value.Value<int>() : defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    private static double GetDoubleProperty(JToken obj, string propertyName, double defaultValue)
    {
        try
        {
            var value = obj[propertyName];
            return value != null ? value.Value<double>() : defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    private static bool GetBoolProperty(JToken obj, string propertyName, bool defaultValue)
    {
        try
        {
            var value = obj[propertyName];
            return value != null ? value.Value<bool>() : defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    private static string? GetStringProperty(JToken obj, string propertyName)
    {
        try
        {
            var value = obj[propertyName];
            return value?.Value<string>();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Hydrates an item by resolving reference IDs to full objects.
    /// Populates RequiredItems property.
    /// </summary>
    /// <param name="item">The item to hydrate.</param>
    private async Task HydrateItemAsync(Item item)
    {
        // Resolve required items
        if (item.RequiredItemIds != null && item.RequiredItemIds.Any())
        {
            var requiredItems = new List<Item>();
            foreach (var refId in item.RequiredItemIds)
            {
                try
                {
                    var itemJson = await _referenceResolver.ResolveToObjectAsync(refId);
                    if (itemJson != null)
                    {
                        // Convert resolved JSON to Item
                        var resolvedItem = itemJson.ToObject<Item>();
                        if (resolvedItem != null)
                        {
                            requiredItems.Add(resolvedItem);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to resolve required item: {RefId}", refId);
                }
            }
            item.RequiredItems = requiredItems;
        }
    }

    #region Budget-Based Generation v2.0

    /// <summary>
    /// Generate an item using budget-based generation (v2.0).
    /// Uses forward-building approach with material pools and budget constraints.
    /// Always succeeds by fitting components to available budget.
    /// </summary>
    /// <param name="request">Budget generation request parameters.</param>
    /// <returns>Generated item with full budget breakdown.</returns>
    public async Task<Item> GenerateItemWithBudgetAsync(BudgetItemRequest request)
    {
        var budgetResult = await BudgetGenerator.GenerateItemAsync(request);
        var item = ConvertBudgetResultToItem(budgetResult, request.ItemCategory);
        return item;
    }

    /// <summary>
    /// Generate multiple items using budget-based generation.
    /// </summary>
    public async Task<List<Item>> GenerateItemsWithBudgetAsync(BudgetItemRequest request, int count = 10)
    {
        var items = new List<Item>();

        for (int i = 0; i < count; i++)
        {
            var item = await GenerateItemWithBudgetAsync(request);
            items.Add(item);
        }

        return items;
    }

    /// <summary>
    /// Convert BudgetItemResult to Item model with composed name.
    /// </summary>
    private Item ConvertBudgetResultToItem(BudgetItemResult result, string category)
    {
        var baseItemName = GetStringProperty(result.BaseItem!, "name") ?? "Unknown";
        
        var item = new Item
        {
            Id = $"{category}:{baseItemName}",
            BaseName = baseItemName,
            Name = baseItemName, // Will be updated
            Description = GetStringProperty(result.BaseItem!, "description") ?? "No description available",
            Price = GetIntProperty(result.BaseItem!, "value", 1),
            Type = category switch
            {
                "weapons" => ItemType.Weapon,
                "armor" => ItemType.Chest,
                "consumables" => ItemType.Consumable,
                "shields" => ItemType.Shield,
                _ => ItemType.Consumable
            }
        };

        // Read stackSize from catalog and set stackability
        item.StackSize = GetIntProperty(result.BaseItem!, "stackSize", 1);
        item.IsStackable = item.StackSize > 1;

        // Store budget metadata
        item.Traits["Budget.Total"] = new TraitValue
        {
            Value = result.AdjustedBudget,
            Type = TraitType.Number
        };

        item.Traits["Budget.Spent"] = new TraitValue
        {
            Value = result.SpentBudget,
            Type = TraitType.Number
        };

        // Apply material
        if (result.Material != null)
        {
            item.Material = ConvertToItemMaterial(result.Material);
            if (item.Material != null)
            {
                ApplyMaterialTraits(item, result.Material);
            }
        }

        // Apply quality modifier
        if (result.Quality != null)
        {
            // Convert to ItemQuality object (v4.3 structure)
            item.Quality = new ItemQuality
            {
                Name = GetStringProperty(result.Quality, "value") ?? GetStringProperty(result.Quality, "name") ?? "",
                RarityWeight = GetIntProperty(result.Quality, "rarityWeight", 50),
                Traits = new Dictionary<string, TraitValue>()
            };
            
            // Copy traits from JSON if available
            var traitsToken = result.Quality["traits"];
            if (traitsToken != null && traitsToken is JObject traitsObj)
            {
                foreach (var prop in traitsObj.Properties())
                {
                    var traitValue = prop.Value;
                    if (traitValue != null && traitValue.Type != JTokenType.Object)
                    {
                        item.Quality.Traits[prop.Name] = new TraitValue
                        {
                            Value = traitValue.Type == JTokenType.Integer ? 
                                traitValue.Value<int>() : 
                                traitValue.Value<double>()
                        };
                    }
                }
            }
        }

        // Apply components from budget
        foreach (var component in result.Components)
        {
            var value = GetStringProperty(component, "value") ?? "";
            var token = GetStringProperty(component, "token") ?? "component";
            
            // Note: Component tracking not implemented yet - will be added when v4.3 migration completes
        }

        // Apply base item stats
        ApplyBaseItemStats(item, result.BaseItem!);

        // Calculate total rarity weight from all components
        var totalRarityWeight = GetIntProperty(result.BaseItem!, "rarityWeight", 50);
        if (item.Material != null)
        {
            totalRarityWeight += item.Material.RarityWeight;
        }
        item.TotalRarityWeight = totalRarityWeight;

        // Compose final name
        item.Name = ComposeBudgetItemName(item, result);

        // Calculate rarity from budget
        item.Rarity = CalculateRarityFromBudget(result.AdjustedBudget);

        // Apply sockets from budget generation result
        if (result.Sockets != null && result.Sockets.Count > 0)
        {
            item.Sockets = result.Sockets;
            var totalSocketCount = result.Sockets.Values.Sum(list => list.Count);
            _logger.LogDebug("Applied {SocketCount} sockets from budget result for {ItemName}", totalSocketCount, item.Name);
        }
        else
        {
            // Fallback to legacy SocketGenerator if budget generation didn't provide sockets
            try
            {
                var socketList = SocketGenerator.GenerateSockets(item.Rarity, item.Type, item.Material?.Name);
                if (socketList.Count > 0)
                {
                    // Group sockets by type for the dictionary structure
                    item.Sockets = socketList
                        .GroupBy(s => s.Type)
                        .ToDictionary(g => g.Key, g => g.ToList());
                    
                    _logger.LogDebug("Generated {SocketCount} sockets (legacy) for {ItemName}", socketList.Count, item.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate sockets for {ItemName}, continuing without sockets", item.Name);
            }
        }

        return item;
    }

    /// <summary>
    /// Compose the final item name from budget result components.
    /// </summary>
    private string ComposeBudgetItemName(Item item, BudgetItemResult result)
    {
        var nameParts = new List<string>();

        // Quality (if present)
        if (result.Quality != null)
        {
            nameParts.Add(GetStringProperty(result.Quality, "value") ?? "");
        }

        // Material
        if (item.Material != null)
        {
            nameParts.Add(item.Material.Name);
        }

        // First prefix only (multiple tracked but only first displayed)
        if (item.PrefixComponents.Count > 0)
        {
            nameParts.Add(item.PrefixComponents[0].Name);
        }

        // Base name
        nameParts.Add(item.BaseName);

        // First suffix only (multiple tracked but only first displayed)
        if (item.SuffixComponents.Count > 0)
        {
            // Add "of" prefix for suffix names that don't already have it
            var suffixName = item.SuffixComponents[0].Name;
            if (!suffixName.StartsWith("of ", StringComparison.OrdinalIgnoreCase))
                suffixName = $"of {suffixName}";
            nameParts.Add(suffixName);
        }

        return string.Join(" ", nameParts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    /// <summary>
    /// Apply material traits to item from material catalog data.
    /// </summary>
    private void ApplyMaterialTraits(Item item, JToken material)
    {
        var traits = material["traits"];
        if (traits != null)
        {
            foreach (var traitProp in traits.Children<JProperty>())
            {
                var traitName = traitProp.Name;
                var traitValue = traitProp.Value;

                var traitVal = new TraitValue
                {
                    Value = traitValue.Type == JTokenType.Integer ? traitValue.Value<int>() : 
                            traitValue.Type == JTokenType.Float ? traitValue.Value<double>() :
                            traitValue.Type == JTokenType.Boolean ? traitValue.Value<bool>() :
                            traitValue.Value<string>(),
                    Type = traitValue.Type == JTokenType.Integer ? TraitType.Number :
                           traitValue.Type == JTokenType.Float ? TraitType.Number :
                           traitValue.Type == JTokenType.Boolean ? TraitType.Boolean :
                           TraitType.String
                };

                item.Traits[$"Material.{traitName}"] = traitVal;
#pragma warning disable CS0618 // Obsolete warning
                item.MaterialTraits[traitName] = traitVal; // Also populate legacy field
#pragma warning restore CS0618
            }
        }

        // Apply item-type-specific traits
        var itemTypeTraits = material["itemTypeTraits"];
        if (itemTypeTraits != null)
        {
            var typeKey = item.Type == ItemType.Weapon ? "weapon" : "armor";
            var specificTraits = itemTypeTraits[typeKey];
            
            if (specificTraits != null)
            {
                foreach (var traitProp in specificTraits.Children<JProperty>())
                {
                    var traitName = traitProp.Name;
                    var traitValue = traitProp.Value;

                    var traitVal = new TraitValue
                    {
                        Value = traitValue.Type == JTokenType.Integer ? traitValue.Value<int>() :
                                traitValue.Type == JTokenType.Float ? traitValue.Value<double>() :
                                traitValue.Value<string>(),
                        Type = traitValue.Type == JTokenType.Integer ? TraitType.Number :
                               traitValue.Type == JTokenType.Float ? TraitType.Number :
                               TraitType.String
                    };

                    item.Traits[$"Material.{traitName}"] = traitVal;
#pragma warning disable CS0618 // Obsolete warning
                    item.MaterialTraits[traitName] = traitVal; // Also populate legacy field
#pragma warning restore CS0618
                }
            }
        }
    }

    /// <summary>
    /// Apply base item stats (damage, defense, etc.) from catalog.
    /// Supports both v4.0 (simple fields) and v5.1 (structured objects/stats) formats.
    /// </summary>
    private void ApplyBaseItemStats(Item item, JToken baseItem)
    {
        // Weapons: damage (v5.1 has structured object, v4.0 has string)
        if (baseItem["damage"] != null)
        {
            var damageToken = baseItem["damage"];
            if (damageToken is JObject damageObj)
            {
                // v5.1 format: { "min": 1, "max": 8, "modifier": "wielder.strength_mod" }
                var min = damageObj["min"]?.Value<int>() ?? 1;
                var max = damageObj["max"]?.Value<int>() ?? 1;
                var modifier = damageObj["modifier"]?.ToString() ?? "";
                
                // Store as dice notation for compatibility
                item.Traits["Damage"] = new TraitValue
                {
                    Value = $"{min}d{max}",
                    Type = TraitType.String
                };
                
                if (!string.IsNullOrEmpty(modifier))
                {
                    item.Traits["DamageModifier"] = new TraitValue
                    {
                        Value = modifier,
                        Type = TraitType.String
                    };
                }
            }
            else
            {
                // v4.0 format: "1d8" string
                item.Traits["Damage"] = new TraitValue
                {
                    Value = GetStringProperty(baseItem, "damage"),
                    Type = TraitType.String
                };
            }
        }

        // Armor: defense (v5.1 in stats object, v4.0 as direct field)
        var defenseValue = baseItem["defense"];
        if (defenseValue == null && baseItem["stats"] is JObject statsObj)
        {
            // v5.1 format: stats.defense formula
            defenseValue = statsObj["defense"];
        }
        
        if (defenseValue != null)
        {
            if (defenseValue.Type == JTokenType.Integer)
            {
                // Numeric defense value
                item.Traits["Defense"] = new TraitValue
                {
                    Value = defenseValue.Value<int>(),
                    Type = TraitType.Number
                };
            }
            else if (defenseValue.Type == JTokenType.String)
            {
                // Formula defense value (v5.1)
                item.Traits["Defense"] = new TraitValue
                {
                    Value = defenseValue.ToString(),
                    Type = TraitType.String
                };
            }
        }

        // Weight
        if (baseItem["weight"] != null)
        {
            item.Traits["Weight"] = new TraitValue
            {
                Value = GetDoubleProperty(baseItem, "weight", 0.0),
                Type = TraitType.Number
            };
        }
    }

    /// <summary>
    /// Calculate item rarity from budget value.
    /// </summary>
    private ItemRarity CalculateRarityFromBudget(int budget)
    {
        return budget switch
        {
            < 50 => ItemRarity.Common,
            < 100 => ItemRarity.Uncommon,
            < 200 => ItemRarity.Rare,
            < 350 => ItemRarity.Epic,
            _ => ItemRarity.Legendary
        };
    }

    private static ItemRarity ConvertWeightToRarity(int weight)
    {
        // v4.2 Rarity Weight System (from ITEM_ENHANCEMENT_SYSTEM.md)
        // Common: < 50
        // Uncommon: 50-99
        // Rare: 100-199
        // Epic: 200-349
        // Legendary: 350+
        return weight switch
        {
            < 50 => ItemRarity.Common,
            < 100 => ItemRarity.Uncommon,
            < 200 => ItemRarity.Rare,
            < 350 => ItemRarity.Epic,
            _ => ItemRarity.Legendary
        };
    }

    /// <summary>
    /// Safely extracts a dictionary property from a JToken with type conversion support.
    /// </summary>
    /// <typeparam name="T">The value type (int, string, or double).</typeparam>
    /// <param name="obj">The JToken to extract from.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <returns>A dictionary with the extracted values, or an empty dictionary if the property doesn't exist or conversion fails.</returns>
    private static Dictionary<string, T> GetDictionaryProperty<T>(JToken obj, string propertyName)
    {
        try
        {
            var dict = new Dictionary<string, T>();
            var value = obj[propertyName];
            if (value is JObject jObj)
            {
                foreach (var prop in jObj.Properties())
                {
                    if (typeof(T) == typeof(int))
                    {
                        dict[prop.Name] = (T)(object)prop.Value.Value<int>();
                    }
                    else if (typeof(T) == typeof(string))
                    {
                        dict[prop.Name] = (T)(object)(prop.Value.Value<string>() ?? string.Empty);
                    }
                    else if (typeof(T) == typeof(double))
                    {
                        dict[prop.Name] = (T)(object)prop.Value.Value<double>();
                    }
                }
            }
            return dict;
        }
        catch
        {
            return new Dictionary<string, T>();
        }
    }

    /// <summary>
    /// Converts a JToken material to an ItemMaterial object.
    /// </summary>
    private ItemMaterial? ConvertToItemMaterial(JToken? materialToken)
    {
        if (materialToken == null) return null;

        var material = new ItemMaterial
        {
            Name = GetStringProperty(materialToken, "name") ?? "Unknown",
            RarityWeight = GetIntProperty(materialToken, "rarityWeight", 0),
            Description = GetStringProperty(materialToken, "description") ?? string.Empty,
            CostScale = GetDoubleProperty(materialToken, "costScale", 1.0),
            Category = GetStringProperty(materialToken, "category") ?? string.Empty
        };

        // Convert traits
        var traits = materialToken["traits"];
        if (traits != null && traits.Type == JTokenType.Object)
        {
            foreach (var traitProp in traits.Children<JProperty>())
            {
                var traitName = traitProp.Name;
                var traitValue = traitProp.Value;

                if (traitValue.Type == JTokenType.Object)
                {
                    var typeStr = GetStringProperty(traitValue, "type");
                    var valueObj = traitValue["value"];

                    if (typeStr == "number" && valueObj != null)
                    {
                        material.Traits[traitName] = new TraitValue
                        {
                            Value = valueObj.Value<double>(),
                            Type = TraitType.Number
                        };
                    }
                    else if (typeStr == "boolean" && valueObj != null)
                    {
                        material.Traits[traitName] = new TraitValue
                        {
                            Value = valueObj.Value<bool>(),
                            Type = TraitType.Boolean
                        };
                    }
                    else if (valueObj != null)
                    {
                        material.Traits[traitName] = new TraitValue
                        {
                            Value = valueObj.Value<string>() ?? string.Empty,
                            Type = TraitType.String
                        };
                    }
                }
                else if (traitValue.Type == JTokenType.Integer || traitValue.Type == JTokenType.Float)
                {
                    material.Traits[traitName] = new TraitValue
                    {
                        Value = traitValue.Value<double>(),
                        Type = TraitType.Number
                    };
                }
                else
                {
                    material.Traits[traitName] = new TraitValue
                    {
                        Value = traitValue.Value<string>() ?? string.Empty,
                        Type = TraitType.String
                    };
                }
            }
        }

        return material;
    }

    #endregion
}