using RealmEngine.Data.Services;
using RealmEngine.Shared.Models;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealmEngine.Core.Generators.Modern;

/// <summary>
/// Generates enchantments using the v4.1 positional pattern system.
/// Part of the Hybrid Enhancement System v1.0.
/// </summary>
public class EnchantmentGenerator
{
    private readonly GameDataCache _dataCache;
    private readonly ReferenceResolverService _referenceResolver;
    private readonly Random _random;
    private readonly ILogger<EnchantmentGenerator> _logger;
    private readonly NameComposer _nameComposer;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnchantmentGenerator"/> class.
    /// </summary>
    /// <param name="dataCache">The game data cache.</param>
    /// <param name="referenceResolver">The reference resolver service.</param>
    /// <param name="logger">The logger.</param>
    public EnchantmentGenerator(GameDataCache dataCache, ReferenceResolverService referenceResolver, ILogger<EnchantmentGenerator> logger)
    {
        _dataCache = dataCache ?? throw new ArgumentNullException(nameof(dataCache));
        _referenceResolver = referenceResolver ?? throw new ArgumentNullException(nameof(referenceResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _random = new Random();
        
        // Create a NullLogger for NameComposer (it doesn't need logging for our use case)
        _nameComposer = new NameComposer(NullLogger<NameComposer>.Instance);
    }

    /// <summary>
    /// Generate a single enchantment from a reference.
    /// Uses positional pattern system to determine prefix/suffix placement.
    /// </summary>
    public Task<Enchantment?> GenerateEnchantmentAsync(string reference)
    {
        try
        {
            var namesPath = "items/enchantments/names.json";
            if (!_dataCache.FileExists(namesPath)) return Task.FromResult<Enchantment?>(null);
            
            var namesFile = _dataCache.GetFile(namesPath);
            if (namesFile?.JsonData == null)
            {
                return Task.FromResult<Enchantment?>(null);
            }

            // Get patterns
            var patterns = namesFile.JsonData["patterns"];
            if (patterns == null || !patterns.Any())
            {
                return Task.FromResult<Enchantment?>(null);
            }

            // Select random pattern
            var pattern = GetRandomWeightedPattern(patterns);
            if (pattern == null)
            {
                return Task.FromResult<Enchantment?>(null);
            }

            var patternString = GetStringProperty(pattern, "pattern");
            if (string.IsNullOrEmpty(patternString))
            {
                return Task.FromResult<Enchantment?>(null);
            }

            // Get components
            var components = namesFile.JsonData["components"];
            if (components == null)
            {
                return Task.FromResult<Enchantment?>(null);
            }

            // Extract token name from pattern (e.g., "element_prefix" from "{element_prefix}")
            var tokenMatch = System.Text.RegularExpressions.Regex.Match(patternString, @"\{([^}]+)\}");
            if (!tokenMatch.Success)
            {
                _logger.LogWarning("Enchantment pattern '{Pattern}' does not contain a valid token", patternString);
                return Task.FromResult<Enchantment?>(null);
            }
            
            var tokenName = tokenMatch.Groups[1].Value;
            
            // Get the component array for this token
            var componentArray = components[tokenName];
            if (componentArray == null || !componentArray.Any())
            {
                _logger.LogWarning("No components found for enchantment token '{Token}'", tokenName);
                return Task.FromResult<Enchantment?>(null);
            }

            // Select a random component using weighted selection
            var selectedComponent = GetRandomWeightedComponent(componentArray);
            if (selectedComponent == null)
            {
                _logger.LogWarning("Failed to select component for token '{Token}'", tokenName);
                return Task.FromResult<Enchantment?>(null);
            }

            // Get the enchantment name from the component
            var enchantmentName = GetStringProperty(selectedComponent, "value") 
                               ?? GetStringProperty(selectedComponent, "name");
            
            if (string.IsNullOrWhiteSpace(enchantmentName))
            {
                _logger.LogWarning("Component for token '{Token}' has no valid name/value", tokenName);
                return Task.FromResult<Enchantment?>(null);
            }

            // Determine position based on token naming convention (_prefix vs _suffix)
            EnchantmentPosition position;
            if (tokenName.EndsWith("_prefix"))
            {
                position = EnchantmentPosition.Prefix;
            }
            else if (tokenName.EndsWith("_suffix"))
            {
                position = EnchantmentPosition.Suffix;
            }
            else
            {
                _logger.LogWarning("Enchantment token '{Token}' does not follow _prefix or _suffix convention", tokenName);
                position = EnchantmentPosition.Suffix; // Fallback
            }
            
            // Use selected component as componentData
            var componentData = selectedComponent;
            
            // Build enchantment from component
            return Task.FromResult<Enchantment?>(BuildEnchantment(componentData, enchantmentName, position));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating enchantment");
            return Task.FromResult<Enchantment?>(null);
        }
    }
    
    /// <summary>
    /// Find a component by its value.
    /// </summary>
    private JToken? FindComponentByValue(JToken? componentsArray, string value)
    {
        if (componentsArray == null) return null;
        
        foreach (var component in componentsArray.Children())
        {
            if (GetStringProperty(component, "value") == value)
            {
                return component;
            }
        }
        
        return null;
    }

    /// <summary>
    /// Selects a random component from a component array using rarityWeight.
    /// </summary>
    private JToken? GetRandomWeightedComponent(JToken components)
    {
        var componentList = components.ToList();
        if (!componentList.Any()) return null;

        var totalWeight = componentList.Sum(c => GetIntProperty(c, "rarityWeight", 1));
        var randomValue = _random.Next(1, totalWeight + 1);

        int currentWeight = 0;
        foreach (var component in componentList)
        {
            currentWeight += GetIntProperty(component, "rarityWeight", 1);
            if (randomValue <= currentWeight)
            {
                return component;
            }
        }

        return componentList.First();
    }

    /// <summary>
    /// Generate multiple enchantments.
    /// </summary>
    public async Task<List<Enchantment>> GenerateEnchantmentsAsync(int count)
    {
        var result = new List<Enchantment>();
        
        for (int i = 0; i < count; i++)
        {
            var enchantment = await GenerateEnchantmentAsync("@items/enchantments:*");
            if (enchantment != null)
            {
                result.Add(enchantment);
            }
        }
        
        return result;
    }

    /// <summary>
    /// Build an Enchantment model from a component.
    /// </summary>
    private Enchantment BuildEnchantment(JToken component, string name, EnchantmentPosition position)
    {
        var enchantment = new Enchantment
        {
            Slug = GetStringProperty(component, "slug") ?? string.Empty,
            Name = name,
            DisplayName = GetStringProperty(component, "displayName") ?? name,
            RarityWeight = GetIntProperty(component, "rarityWeight", 50),
            Position = position,
            Description = "Enchantment",
            Value = GetIntProperty(component, "value", 0),
            Weight = GetDoubleProperty(component, "weight", 0.0),
            Effect = GetStringProperty(component, "effect"),
            Power = GetIntProperty(component, "power", 0),
            Duration = GetIntProperty(component, "duration", 0),
            Attributes = GetDictionaryProperty<int>(component, "attributes")
        };

        // Set rarity based on weight
        enchantment.Rarity = ConvertWeightToEnchantmentRarity(enchantment.RarityWeight);

        // Apply traits from component
        var traits = component["traits"];
        if (traits != null)
        {
            foreach (var traitProp in traits.Children<JProperty>())
            {
                var traitName = traitProp.Name;
                var traitData = traitProp.Value;
                
                var traitValue = new TraitValue
                {
                    Value = traitData["value"]?.ToObject<object>(),
                    Type = ParseTraitType(traitData["type"]?.ToString() ?? "number")
                };
                
                enchantment.Traits[traitName] = traitValue;
            }
        }

        return enchantment;
    }

    /// <summary>
    /// Convert rarity weight to enchantment rarity enum.
    /// </summary>
    private static EnchantmentRarity ConvertWeightToEnchantmentRarity(int weight)
    {
        return weight switch
        {
            < 40 => EnchantmentRarity.Minor,
            < 80 => EnchantmentRarity.Lesser,
            < 120 => EnchantmentRarity.Greater,
            < 180 => EnchantmentRarity.Superior,
            _ => EnchantmentRarity.Legendary
        };
    }

    /// <summary>
    /// Parse trait type string to TraitType enum.
    /// </summary>
    private static TraitType ParseTraitType(string typeString)
    {
        return typeString?.ToLower() switch
        {
            "number" => TraitType.Number,
            "string" => TraitType.String,
            "boolean" => TraitType.Boolean,
            "stringarray" => TraitType.StringArray,
            "numberarray" => TraitType.NumberArray,
            _ => TraitType.Number
        };
    }

    /// <summary>
    /// Get a random weighted pattern.
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

    /// <summary>
    /// Get a random weighted item from a collection.
    /// </summary>
    private JToken? GetRandomWeightedItem(JToken items)
    {
        var itemList = items.Children().ToList();
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
}
