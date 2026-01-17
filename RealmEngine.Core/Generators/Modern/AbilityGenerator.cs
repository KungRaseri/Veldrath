using RealmEngine.Data.Services;
using RealmEngine.Shared.Models;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealmEngine.Core.Generators.Modern;

/// <summary>
/// Generates Ability instances from abilities catalog JSON files.
/// Supports different ability types (passive, active, offensive, defensive, support) and rarity-based selection.
/// </summary>
public class AbilityGenerator
{
    private readonly GameDataCache _dataCache;
    private readonly ReferenceResolverService _referenceResolver;
    private readonly Random _random;
    private readonly ILogger<AbilityGenerator> _logger;
    private readonly NameComposer _nameComposer;

    /// <summary>
    /// Initializes a new instance of the AbilityGenerator class.
    /// </summary>
    /// <param name="dataCache">The game data cache for accessing ability catalog files.</param>
    /// <param name="referenceResolver">The reference resolver for resolving JSON references.</param>
    /// <param name="logger">Logger for this generator.</param>
    public AbilityGenerator(GameDataCache dataCache, ReferenceResolverService referenceResolver, ILogger<AbilityGenerator> logger)
    {
        _dataCache = dataCache ?? throw new ArgumentNullException(nameof(dataCache));
        _referenceResolver = referenceResolver ?? throw new ArgumentNullException(nameof(referenceResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _random = new Random();
        _nameComposer = new NameComposer(NullLogger<NameComposer>.Instance);
    }

    /// <summary>
    /// Generates a list of random abilities from a specific category and subcategory.
    /// </summary>
    /// <param name="category">The ability category (e.g., "active", "passive").</param>
    /// <param name="subcategory">The ability subcategory (e.g., "offensive", "defensive", "support").</param>
    /// <param name="count">The number of abilities to generate (default: 5).</param>
    /// <param name="hydrate">If true, populates resolved RequiredItems and RequiredAbilities properties (default: true).</param>
    /// <returns>A list of generated Ability instances.</returns>
    public async Task<List<Ability>> GenerateAbilitiesAsync(string category, string subcategory, int count = 5, bool hydrate = true)
    {
        try
        {
            // Load from consolidated catalog: abilities/{category}/catalog.json
            var catalogFile = _dataCache.GetFile($"abilities/{category}/catalog.json");
            if (catalogFile?.JsonData == null)
            {
                return new List<Ability>();
            }

            var catalog = catalogFile.JsonData;
            var items = GetItemsFromSubcategory(catalog, subcategory);
            
            if (items == null || !items.Any())
            {
                return new List<Ability>();
            }

            var result = new List<Ability>();

            for (int i = 0; i < count; i++)
            {
                var randomAbility = GetRandomWeightedItem(items);
                if (randomAbility != null)
                {
                    var ability = await ConvertToAbilityAsync(randomAbility, category, subcategory);
                    if (ability != null)
                    {
                        if (hydrate)
                        {
                            await HydrateAbilityAsync(ability);
                        }
                        result.Add(ability);
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating abilities for category {Category}/{Subcategory}", category, subcategory);
            return new List<Ability>();
        }
    }

    /// <summary>
    /// Generates a specific ability by name from a category and subcategory.
    /// </summary>
    /// <param name="category">The ability category to search in.</param>
    /// <param name="subcategory">The ability subcategory to search in.</param>
    /// <param name="abilityName">The name of the ability to generate.</param>
    /// <param name="hydrate">If true, populates resolved RequiredItems and RequiredAbilities properties (default: true).</param>
    /// <returns>The generated Ability instance, or null if not found.</returns>
    public async Task<Ability?> GenerateAbilityByNameAsync(string category, string subcategory, string abilityName, bool hydrate = true)
    {
        try
        {
            // Load from consolidated catalog: abilities/{category}/catalog.json
            var catalogFile = _dataCache.GetFile($"abilities/{category}/catalog.json");
            if (catalogFile?.JsonData == null)
            {
                return null;
            }

            var catalog = catalogFile.JsonData;
            var items = GetItemsFromSubcategory(catalog, subcategory);
            
            var catalogAbility = items?.FirstOrDefault(a => 
                string.Equals(GetStringProperty(a, "slug"), abilityName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(GetStringProperty(a, "name"), abilityName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(GetStringProperty(a, "displayName"), abilityName, StringComparison.OrdinalIgnoreCase));

            if (catalogAbility != null)
            {
                var ability = await ConvertToAbilityAsync(catalogAbility, category, subcategory);
                if (ability != null && hydrate)
                {
                    await HydrateAbilityAsync(ability);
                }
                return ability;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating ability {AbilityName} from category {Category}/{Subcategory}", abilityName, category, subcategory);
            return null;
        }
    }

    /// <summary>
    /// Gets items from a specific subcategory within the consolidated catalog structure.
    /// Consolidated format: { "ability_types": { "offensive": { "items": [...] }, "defensive": { "items": [...] } } }
    /// </summary>
    private static IEnumerable<JToken>? GetItemsFromSubcategory(JToken catalog, string subcategory)
    {
        try
        {
            // Navigate: catalog -> ability_types -> {subcategory} -> items
            var abilityTypes = catalog["ability_types"];
            if (abilityTypes == null) return null;

            var subcategoryData = abilityTypes[subcategory];
            if (subcategoryData == null) return null;

            var items = subcategoryData["items"];
            if (items == null || !items.HasValues) return null;

            return items.Children();
        }
        catch
        {
            return null;
        }
    }

    private async Task<Ability?> ConvertToAbilityAsync(JToken catalogAbility, string category, string subcategory)
    {
        try
        {
            // Consolidated catalogs use "slug" as primary identifier
            var slug = GetStringProperty(catalogAbility, "slug");
            var name = GetStringProperty(catalogAbility, "name") ?? GetStringProperty(catalogAbility, "displayName") ?? slug ?? "unknown-ability";
            var displayName = GetStringProperty(catalogAbility, "displayName") ?? name;
            
            var ability = new Ability
            {
                Id = $"{category}/{subcategory}:{slug ?? name}",
                Slug = slug ?? string.Empty,
                Name = name,
                DisplayName = displayName,
                Description = GetStringProperty(catalogAbility, "description") ?? "An ability",
                Tier = GetIntProperty(catalogAbility, "tier", 1),
                // Consolidated catalogs use "selectionWeight", old structure uses "rarityWeight"
                RarityWeight = catalogAbility["selectionWeight"] != null 
                    ? GetIntProperty(catalogAbility, "selectionWeight", 1) 
                    : GetIntProperty(catalogAbility, "rarityWeight", 1),
                // BaseDamage can be at top level or in traits (consolidated structure)
                BaseDamage = GetStringProperty(catalogAbility, "baseDamage") ?? GetStringPropertyFromTraits(catalogAbility, "baseDamage"),
                // Cooldown can be at top level or in traits (consolidated structure)
                Cooldown = catalogAbility["cooldown"] != null 
                    ? GetIntProperty(catalogAbility, "cooldown", 0)
                    : GetIntPropertyFromTraits(catalogAbility, "cooldown", 0),
                // Range can be at top level or in traits (consolidated structure)
                Range = catalogAbility["range"] != null
                    ? GetIntProperty(catalogAbility, "range", null)
                    : GetIntPropertyFromTraits(catalogAbility, "range", null),
                ManaCost = GetIntPropertyFromTraits(catalogAbility, "manaCost", 0),
                Duration = GetIntPropertyFromTraits(catalogAbility, "duration", null),
                IsPassive = category == "passive",
                RequiredLevel = GetIntProperty(catalogAbility, "requiredLevel", 1),
                Type = MapAbilityType(category, subcategory)
            };

            // Resolve required item references
            if (catalogAbility["requiredItems"] is JArray requiredItems)
            {
                ability.RequiredItemIds = await ResolveReferencesAsync(requiredItems);
            }

            // Resolve required ability references (prerequisites)
            if (catalogAbility["requiredAbilities"] is JArray requiredAbilities)
            {
                ability.RequiredAbilityIds = await ResolveReferencesAsync(requiredAbilities);
            }

            // Parse traits dictionary
            var traits = catalogAbility["traits"];
            if (traits != null && traits is JObject traitsObj)
            {
                foreach (var trait in traitsObj.Properties())
                {
                    var traitValue = trait.Value;
                    if (traitValue is JObject traitObj && traitObj["value"] != null)
                    {
                        ability.Traits[trait.Name] = ExtractTraitValue(traitObj["value"]!);
                    }
                    else
                    {
                        ability.Traits[trait.Name] = traitValue?.ToString() ?? "";
                    }
                }
            }

            // Apply pattern-based naming from names.json
            ApplyNameFromPattern(ability, category, subcategory);

            return ability;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting catalog ability to Ability");
            return null;
        }
    }

    /// <summary>
    /// Apply pattern-based naming from names.json to populate Prefixes and enhanced Name.
    /// </summary>
    private void ApplyNameFromPattern(Ability ability, string category, string subcategory)
    {
        try
        {
            // Try consolidated names file first: abilities/{category}/names.json
            var namesPath = $"abilities/{category}/names.json";
            if (!_dataCache.FileExists(namesPath))
            {
                // Fallback to old structure if needed
                namesPath = $"abilities/{category}/{subcategory}/names.json";
                if (!_dataCache.FileExists(namesPath)) return;
            }

            var namesFile = _dataCache.GetFile(namesPath);
            if (namesFile?.JsonData == null) return;

            var patterns = namesFile.JsonData["patterns"];
            if (patterns == null) return;

            // Select random pattern
            var pattern = GetRandomWeightedPattern(patterns);
            if (pattern == null) return;

            var patternString = GetStringProperty(pattern, "pattern");
            if (string.IsNullOrEmpty(patternString)) return;

            // Get components
            var components = namesFile.JsonData["components"];
            if (components == null) return;

            // Use NameComposer to resolve pattern
            var (name, baseName, prefixes, suffixes) = _nameComposer.ComposeNameWithComponents(patternString, components);

            // Populate component lists (abilities typically only have prefixes)
            ability.Prefixes.AddRange(prefixes);

            // Update the name if we got a better one from the pattern
            if (!string.IsNullOrEmpty(name) && name != ability.DisplayName)
            {
                ability.DisplayName = name;
                ability.Name = name;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error applying name pattern for {Category}/{Subcategory}", category, subcategory);
            // Fallback: keep catalog name
        }
    }

    private JToken? GetRandomWeightedPattern(JToken patterns)
    {
        if (patterns == null || !patterns.Any()) return null;

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

        return patternList.FirstOrDefault();
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

    private static object ExtractTraitValue(JToken valueToken)
    {
        return valueToken.Type switch
        {
            JTokenType.String => valueToken.Value<string>() ?? "",
            JTokenType.Integer => valueToken.Value<int>(),
            JTokenType.Float => valueToken.Value<double>(),
            JTokenType.Boolean => valueToken.Value<bool>(),
            _ => valueToken.ToString()
        };
    }

    private static AbilityTypeEnum MapAbilityType(string category, string subcategory)
    {
        return category.ToLower() switch
        {
            "active" => subcategory.ToLower() switch
            {
                "offensive" => AbilityTypeEnum.Offensive,
                "defensive" => AbilityTypeEnum.Defensive,
                "support" => AbilityTypeEnum.Support,
                "utility" => AbilityTypeEnum.Utility,
                "mobility" => AbilityTypeEnum.Mobility,
                "summon" => AbilityTypeEnum.Summon,
                "control" => AbilityTypeEnum.Crowd_Control,
                _ => AbilityTypeEnum.Offensive
            },
            "passive" => AbilityTypeEnum.Passive,
            "reactive" => AbilityTypeEnum.Defensive,
            "ultimate" => AbilityTypeEnum.Offensive,
            _ => AbilityTypeEnum.Offensive
        };
    }

    private JToken? GetRandomWeightedItem(IEnumerable<JToken> items)
    {
        var itemList = items.ToList();
        if (!itemList.Any()) return null;

        // Support both selectionWeight (consolidated) and rarityWeight (old structure)
        var totalWeight = itemList.Sum(item => 
            item["selectionWeight"] != null 
                ? GetIntProperty(item, "selectionWeight", 1)
                : GetIntProperty(item, "rarityWeight", 1));
        var randomValue = _random.Next(1, totalWeight + 1);

        int currentWeight = 0;
        foreach (var item in itemList)
        {
            currentWeight += item["selectionWeight"] != null 
                ? GetIntProperty(item, "selectionWeight", 1)
                : GetIntProperty(item, "rarityWeight", 1);
            if (randomValue <= currentWeight)
            {
                return item;
            }
        }

        return itemList.First();
    }

    private static int GetIntProperty(JToken obj, string propertyName, int? defaultValue)
    {
        try
        {
            var value = obj[propertyName];
            if (value != null)
            {
                return value.Value<int>();
            }
            return defaultValue ?? 0;
        }
        catch
        {
            return defaultValue ?? 0;
        }
    }

    private static int GetIntPropertyFromTraits(JToken obj, string propertyName, int? defaultValue)
    {
        try
        {
            var traits = obj["traits"];
            if (traits != null && traits[propertyName] != null)
            {
                var trait = traits[propertyName];
                if (trait is JObject traitObj && traitObj["value"] != null)
                {
                    return traitObj["value"]!.Value<int>();
                }
            }
            return defaultValue ?? 0;
        }
        catch
        {
            return defaultValue ?? 0;
        }
    }

    private static string? GetStringPropertyFromTraits(JToken obj, string propertyName)
    {
        try
        {
            var traits = obj["traits"];
            if (traits != null && traits[propertyName] != null)
            {
                var trait = traits[propertyName];
                if (trait is JObject traitObj && traitObj["value"] != null)
                {
                    return traitObj["value"]!.Value<string>();
                }
            }
            return null;
        }
        catch
        {
            return null;
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
    /// Hydrates an ability by resolving reference IDs to full objects.
    /// Populates RequiredItems and RequiredAbilities properties.
    /// </summary>
    /// <param name="ability">The ability to hydrate.</param>
    private async Task HydrateAbilityAsync(Ability ability)
    {
        // Resolve required items
        if (ability.RequiredItemIds != null && ability.RequiredItemIds.Any())
        {
            var requiredItems = new List<Item>();
            foreach (var refId in ability.RequiredItemIds)
            {
                try
                {
                    var itemJson = await _referenceResolver.ResolveToObjectAsync(refId);
                    if (itemJson != null)
                    {
                        var item = itemJson.ToObject<Item>();
                        if (item != null)
                        {
                            requiredItems.Add(item);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to resolve required item '{RefId}'", refId);
                }
            }
            ability.RequiredItems = requiredItems;
        }

        // Resolve required abilities (prerequisites)
        if (ability.RequiredAbilityIds != null && ability.RequiredAbilityIds.Any())
        {
            var requiredAbilities = new List<Ability>();
            foreach (var refId in ability.RequiredAbilityIds)
            {
                try
                {
                    var abilityJson = await _referenceResolver.ResolveToObjectAsync(refId);
                    if (abilityJson != null)
                    {
                        var requiredAbility = abilityJson.ToObject<Ability>();
                        if (requiredAbility != null)
                        {
                            requiredAbilities.Add(requiredAbility);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to resolve required ability '{RefId}'", refId);
                }
            }
            ability.RequiredAbilities = requiredAbilities;
        }
    }
}
