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

        // Load weapon catalogs (swords, axes, bows, daggers, maces, polearms, staves)
        var weaponTypes = new[] { "swords", "axes", "bows", "daggers", "maces", "polearms", "staves", "wands", "crossbows" };

        foreach (var weaponType in weaponTypes)
        {
            // Check if class has proficiency (support "all" wildcard)
            if (!proficiencies.Contains("all", StringComparer.OrdinalIgnoreCase) &&
                !proficiencies.Contains(weaponType, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var catalogPath = $"items/weapons/{weaponType}/catalog.json";
            var catalogFile = _dataCache.GetFile(catalogPath);
            
            if (catalogFile == null)
            {
                _logger.LogWarning("Weapon catalog not found: {CatalogPath}", catalogPath);
                continue;
            }

            try
            {
                var catalog = JObject.Parse(catalogFile.JsonData.ToString());
                var items = ExtractItemsFromCatalog(catalog, weaponType, ItemType.Weapon);
                weapons.AddRange(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing weapon catalog: {CatalogPath}", catalogPath);
            }
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

        // Load armor catalogs (light, medium, heavy, shields)
        var armorTypes = new[] { "light", "medium", "heavy", "shields" };

        foreach (var armorType in armorTypes)
        {
            // Check if class has proficiency
            if (!proficiencies.Contains(armorType, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var catalogPath = $"items/armor/{armorType}/catalog.json";
            var catalogFile = _dataCache.GetFile(catalogPath);
            
            if (catalogFile == null)
            {
                _logger.LogWarning("Armor catalog not found: {CatalogPath}", catalogPath);
                continue;
            }

            try
            {
                var catalog = JObject.Parse(catalogFile.JsonData.ToString());
                // Use Chest as generic armor type
                var items = ExtractItemsFromCatalog(catalog, armorType, ItemType.Chest);
                armorItems.AddRange(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing armor catalog: {CatalogPath}", catalogPath);
            }
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
            var price = token["price"]?.Value<int>() ?? 0;
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

                // Parse armor class value if present
                var armorValue = token["armorClass"]?.Value<int>() ?? 0;
                if (armorValue > 0)
                {
                    item.BaseTraits["armorClass"] = new TraitValue(armorValue, TraitType.Number);
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
