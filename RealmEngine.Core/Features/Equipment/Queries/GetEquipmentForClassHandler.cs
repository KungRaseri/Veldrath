using MediatR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using RealmEngine.Data.Services;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Equipment.Queries;

/// <summary>
/// Handler for GetEquipmentForClassQuery.
/// Loads base equipment items and filters by class proficiencies.
/// </summary>
public class GetEquipmentForClassHandler : IRequestHandler<GetEquipmentForClassQuery, GetEquipmentForClassResult>
{
    private readonly ICharacterClassRepository _classRepository;
    private readonly GameDataCache _dataCache;
    private readonly ILogger<GetEquipmentForClassHandler> _logger;
    private readonly Random _random = new();

    /// <summary>
    /// Initializes a new instance of the GetEquipmentForClassHandler class.
    /// </summary>
    public GetEquipmentForClassHandler(
        ICharacterClassRepository classRepository,
        GameDataCache dataCache,
        ILogger<GetEquipmentForClassHandler> logger)
    {
        _classRepository = classRepository ?? throw new ArgumentNullException(nameof(classRepository));
        _dataCache = dataCache ?? throw new ArgumentNullException(nameof(dataCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles the GetEquipmentForClassQuery request.
    /// </summary>
    public async Task<GetEquipmentForClassResult> Handle(GetEquipmentForClassQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Get class details
            var characterClass = _classRepository.GetById(request.ClassId);
            if (characterClass == null)
            {
                return new GetEquipmentForClassResult
                {
                    Success = false,
                    ErrorMessage = $"Class '{request.ClassId}' not found"
                };
            }

            // Get proficiencies
            var armorProfs = characterClass.ArmorProficiency ?? new List<string>();
            var weaponProfs = characterClass.WeaponProficiency ?? new List<string>();

            _logger.LogInformation(
                "Loading equipment for class {ClassName} (Armor: {ArmorProfs}, Weapons: {WeaponProfs})",
                characterClass.Name,
                string.Join(", ", armorProfs),
                string.Join(", ", weaponProfs));

            var result = new GetEquipmentForClassResult
            {
                Success = true,
                ClassName = characterClass.Name,
                ArmorProficiencies = armorProfs,
                WeaponProficiencies = weaponProfs
            };

            // Load equipment based on filter
            if (request.EquipmentType == null || request.EquipmentType.Equals("weapons", StringComparison.OrdinalIgnoreCase))
            {
                result.Weapons = await LoadWeapons(weaponProfs, request.MaxItemsPerCategory, request.RandomizeSelection);
            }

            if (request.EquipmentType == null || request.EquipmentType.Equals("armor", StringComparison.OrdinalIgnoreCase))
            {
                result.Armor = await LoadArmor(armorProfs, request.MaxItemsPerCategory, request.RandomizeSelection);
            }

            _logger.LogInformation(
                "Loaded {WeaponCount} weapons and {ArmorCount} armor items for class {ClassName}",
                result.Weapons.Count,
                result.Armor.Count,
                characterClass.Name);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading equipment for class {ClassId}", request.ClassId);
            return new GetEquipmentForClassResult
            {
                Success = false,
                ErrorMessage = $"Error loading equipment: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Loads weapons that match the class proficiencies.
    /// </summary>
    private Task<List<Item>> LoadWeapons(List<string> proficiencies, int maxItems, bool randomize)
    {
        var weapons = new List<Item>();

        // Load main weapons catalog
        var catalogPath = "items/weapons/catalog.json";
        var catalogFile = _dataCache.GetFile(catalogPath);
        
        if (catalogFile == null)
        {
            _logger.LogWarning("Weapons catalog not found: {CatalogPath}", catalogPath);
            return Task.FromResult(weapons);
        }

        try
        {
            var catalog = JObject.Parse(catalogFile.JsonData.ToString());
            var weaponTypes = catalog["weapon_types"] as JObject;
            
            if (weaponTypes == null)
            {
                _logger.LogWarning("No weapon_types found in catalog");
                return Task.FromResult(weapons);
            }

            // Map catalog keys to proficiency categories
            var typeMapping = new Dictionary<string, string[]>
            {
                ["heavy-blades"] = new[] { "swords", "greatswords", "all" },
                ["light-blades"] = new[] { "swords", "daggers", "rapiers", "shortswords", "all" },
                ["axes"] = new[] { "axes", "all" },
                ["bludgeons"] = new[] { "maces", "warhammers", "simple", "all" },
                ["polearms"] = new[] { "polearms", "all" },
                ["bows"] = new[] { "bows", "all" },
                ["crossbows"] = new[] { "crossbows", "all" },
                ["staves"] = new[] { "staves", "simple", "all" },
                ["wands"] = new[] { "wands", "all" }
            };

            // Map weapon slugs to specific proficiency types (for more granular filtering)
            var slugToType = new Dictionary<string, string>
            {
                ["dagger"] = "daggers",
                ["shortsword"] = "shortswords",
                ["rapier"] = "rapiers",
                ["longsword"] = "swords",
                ["broadsword"] = "swords",
                ["scimitar"] = "swords",
                ["greatsword"] = "greatswords",
                ["claymore"] = "greatswords",
                ["battleaxe"] = "axes",
                ["handaxe"] = "axes",
                ["greataxe"] = "axes",
                ["mace"] = "maces",
                ["warhammer"] = "warhammers",
                ["maul"] = "warhammers",
                ["club"] = "simple",
                ["quarterstaff"] = "staves",
                ["staff"] = "staves",
                ["spear"] = "polearms",
                ["halberd"] = "polearms",
                ["pike"] = "polearms",
                ["glaive"] = "polearms",
                ["shortbow"] = "bows",
                ["longbow"] = "bows",
                ["crossbow"] = "crossbows",
                ["light-crossbow"] = "crossbows",
                ["heavy-crossbow"] = "crossbows"
            };

            foreach (var weaponType in weaponTypes.Properties())
            {
                var typeName = weaponType.Name;
                
                // Check if class has proficiency for this category
                bool hasProficiency = typeMapping.ContainsKey(typeName) &&
                    typeMapping[typeName].Any(prof => proficiencies.Contains(prof, StringComparer.OrdinalIgnoreCase));
                
                if (!hasProficiency)
                {
                    continue;
                }

                var typeData = weaponType.Value as JObject;
                var items = typeData?["items"] as JArray;
                
                if (items == null)
                {
                    continue;
                }

                foreach (var itemToken in items)
                {
                    var item = ParseItemFromToken(itemToken, typeName, ItemType.Weapon);
                    if (item != null)
                    {
                        // Determine specific weapon type from slug for finer proficiency check
                        var specificType = slugToType.ContainsKey(item.Slug) ? slugToType[item.Slug] : typeName;
                        item.WeaponType = specificType;
                        
                        // Filter by specific proficiency if not "all"
                        if (proficiencies.Contains("all", StringComparer.OrdinalIgnoreCase) ||
                            proficiencies.Contains(specificType, StringComparer.OrdinalIgnoreCase))
                        {
                            weapons.Add(item);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading weapons catalog");
        }

        // Apply randomization and limit
        if (randomize)
        {
            weapons = weapons.OrderBy(_ => _random.Next()).ToList();
        }

        if (maxItems > 0 && weapons.Count > maxItems)
        {
            weapons = weapons.Take(maxItems).ToList();
        }

        return Task.FromResult(weapons);
    }

    /// <summary>
    /// Loads armor that matches the class proficiencies.
    /// </summary>
    private Task<List<Item>> LoadArmor(List<string> proficiencies, int maxItems, bool randomize)
    {
        var armorItems = new List<Item>();

        // Load main armor catalog
        var catalogPath = "items/armor/catalog.json";
        var catalogFile = _dataCache.GetFile(catalogPath);
        
        if (catalogFile == null)
        {
            _logger.LogWarning("Armor catalog not found: {CatalogPath}", catalogPath);
            return Task.FromResult(armorItems);
        }

        try
        {
            var catalog = JObject.Parse(catalogFile.JsonData.ToString());
            var armorTypes = catalog["armor_types"] as JObject;
            
            if (armorTypes == null)
            {
                _logger.LogWarning("No armor_types found in catalog");
                return Task.FromResult(armorItems);
            }

            _logger.LogInformation("Found {Count} armor type categories in catalog", armorTypes.Properties().Count());
            
            // Map armor catalog keys to proficiency categories
            var typeMapping = new Dictionary<string, string[]>
            {
                ["light-armor"] = new[] { "light", "all" },
                ["medium-armor"] = new[] { "medium", "all" },
                ["heavy-armor"] = new[] { "heavy", "all" },
                ["shields"] = new[] { "shields", "all" }
            };

            // Map catalog keys to proficiency names (remove "-armor" suffix)
            var keyToProf = new Dictionary<string, string>
            {
                ["light-armor"] = "light",
                ["medium-armor"] = "medium",
                ["heavy-armor"] = "heavy",
                ["shields"] = "shields"
            };

            foreach (var armorType in armorTypes.Properties())
            {
                var typeName = armorType.Name;
                
                // Check if class has proficiency
                bool hasProficiency = typeMapping.ContainsKey(typeName) &&
                    typeMapping[typeName].Any(prof => proficiencies.Contains(prof, StringComparer.OrdinalIgnoreCase));
                
                _logger.LogInformation("Armor type {TypeName}: hasProficiency={HasProf}, proficiencies={Profs}", 
                    typeName, hasProficiency, string.Join(",", proficiencies));
                
                if (!hasProficiency)
                {
                    continue;
                }

                var typeData = armorType.Value as JObject;
                var items = typeData?["items"] as JArray;
                
                if (items == null)
                {
                    _logger.LogWarning("No items array found for armor type: {TypeName}", typeName);
                    continue;
                }

                _logger.LogInformation("Processing {Count} items for armor type: {TypeName}", items.Count, typeName);

                // Get the proficiency name for this armor type
                var profName = keyToProf.ContainsKey(typeName) ? keyToProf[typeName] : typeName;

                int parsedCount = 0;
                int addedCount = 0;
                foreach (var itemToken in items)
                {
                    parsedCount++;
                    var item = ParseItemFromToken(itemToken, typeName, ItemType.Chest);
                    if (item != null)
                    {
                        // Override ArmorType with proficiency name instead of catalog key
                        // Also check if item has armorClass field
                        var armorClass = itemToken["armorClass"]?.ToString();
                        item.ArmorType = armorClass ?? profName;
                        item.ArmorClass = item.ArmorType;
                        
                        armorItems.Add(item);
                        addedCount++;
                    }
                    else
                    {
                        _logger.LogWarning("Failed to parse item #{Index} from token in {TypeName}", parsedCount, typeName);
                    }
                }
                
                _logger.LogInformation("Processed {TypeName}: parsed={Parsed}, added={Added}, total now={Total}", 
                    typeName, parsedCount, addedCount, armorItems.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading armor catalog");
        }

        // Apply randomization and limit
        if (randomize)
        {
            armorItems = armorItems.OrderBy(_ => _random.Next()).ToList();
        }

        if (maxItems > 0 && armorItems.Count > maxItems)
        {
            armorItems = armorItems.Take(maxItems).ToList();
        }

        return Task.FromResult(armorItems);
    }

    /// <summary>
    /// Extracts items from a catalog JSON structure.
    /// Supports both flat and categorized catalog formats.
    /// </summary>
    private List<Item> ExtractItemsFromCatalog(JObject catalog, string itemSubtype, ItemType itemType)
    {
        var items = new List<Item>();

        // Try to find items array (v4/v5 format)
        var itemsArray = catalog["items"] as JArray;
        if (itemsArray != null)
        {
            foreach (var itemToken in itemsArray)
            {
                var item = ParseItemFromToken(itemToken, itemSubtype, itemType);
                if (item != null)
                {
                    items.Add(item);
                }
            }
        }

        // Try hierarchical format (weapon_types, armor_types)
        var categoriesToken = catalog["weapon_types"] ?? catalog["armor_types"];
        if (categoriesToken != null && categoriesToken is JObject categories)
        {
            foreach (var category in categories)
            {
                var categoryObj = category.Value as JObject;
                if (categoryObj == null) continue;

                var categoryItems = categoryObj["items"] as JArray;
                if (categoryItems != null)
                {
                    foreach (var itemToken in categoryItems)
                    {
                        var item = ParseItemFromToken(itemToken, itemSubtype, itemType);
                        if (item != null)
                        {
                            items.Add(item);
                        }
                    }
                }
            }
        }

        return items;
    }

    /// <summary>
    /// Parses an Item object from a JToken.
    /// </summary>
    private Item? ParseItemFromToken(JToken token, string itemSubtype, ItemType itemType)
    {
        try
        {
            var slug = token["slug"]?.ToString() ?? string.Empty;
            var name = token["name"]?.ToString() ?? string.Empty;
            var description = token["description"]?.ToString() ?? string.Empty;
            var price = token["price"]?.Value<int>() ?? token["value"]?.Value<int>() ?? 0;
            var weight = token["weight"]?.Value<double>() ?? 0.0;
            var rarityWeight = token["rarityWeight"]?.Value<int>() ?? 50;

            var item = new Item
            {
                Id = $"{itemType.ToString().ToLower()}:{itemSubtype}:{slug}",
                Slug = slug,
                Name = name,
                BaseName = name,
                Description = description,
                Price = price,
                Weight = weight,
                Type = itemType,
                TotalRarityWeight = rarityWeight
            };

            // Set weapon/armor type for proficiency matching
            if (itemType == ItemType.Weapon)
            {
                item.WeaponType = itemSubtype;

                // Parse damage if present
                var damageToken = token["damage"];
                if (damageToken != null)
                {
                    var minDamage = damageToken["min"]?.Value<int>() ?? 1;
                    var maxDamage = damageToken["max"]?.Value<int>() ?? 4;
                    var modifier = damageToken["modifier"]?.ToString() ?? string.Empty;

                    item.Damage = new ItemDamage
                    {
                        Min = minDamage,
                        Max = maxDamage,
                        Modifier = modifier
                    };
                }
            }
            else if (itemType == ItemType.Chest || itemType == ItemType.Shield)
            {
                item.ArmorType = itemSubtype;
                item.ArmorClass = itemSubtype;

                // Parse armor class value if present (some catalogs use int, some use string)
                var armorClassToken = token["armorClass"];
                if (armorClassToken != null)
                {
                    // Try parsing as int first (for numeric armorClass values)
                    if (armorClassToken.Type == JTokenType.Integer)
                    {
                        var armorValue = armorClassToken.Value<int>();
                        item.BaseTraits["armorClass"] = new TraitValue(armorValue, TraitType.Number);
                    }
                    // Otherwise treat as string (for "light", "medium", "heavy")
                    else if (armorClassToken.Type == JTokenType.String)
                    {
                        item.ArmorClass = armorClassToken.ToString();
                    }
                }
            }

            return item;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing item from token: {Token}", token.ToString());
            return null;
        }
    }
}
