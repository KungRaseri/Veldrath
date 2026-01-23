using Newtonsoft.Json.Linq;
using Serilog;

namespace RealmEngine.Data.Services;

/// <summary>
/// Service for loading and enforcing item generation rules from configuration/generation-rules.json
/// Provides component limits, display formatting, and validation rules for procedural item generation
/// </summary>
public class ItemGenerationRulesService
{
    private readonly GameDataCache _dataCache;
    private GenerationRulesConfig? _config;

    /// <summary>
    /// Initializes a new instance of ItemGenerationRulesService
    /// </summary>
    /// <param name="dataCache">Game data cache instance</param>
    public ItemGenerationRulesService(GameDataCache dataCache)
    {
        _dataCache = dataCache ?? throw new ArgumentNullException(nameof(dataCache));
    }

    /// <summary>
    /// Loads the generation rules configuration
    /// </summary>
    public GenerationRulesConfig LoadConfig()
    {
        if (_config != null)
            return _config;

        try
        {
            var file = _dataCache.GetFile("configuration/generation-rules.json");
            if (file == null)
            {
                Log.Warning("generation-rules.json not found, using defaults");
                return GetDefaultConfig();
            }

            var json = file.JsonData;

            _config = new GenerationRulesConfig
            {
                Version = json["version"]?.Value<string>() ?? "4.3",
                Type = json["type"]?.Value<string>() ?? "generation_rules_config",
                LastUpdated = json["lastUpdated"]?.Value<string>() ?? DateTime.Now.ToString("yyyy-MM-dd"),
                Description = json["description"]?.Value<string>() ?? "Item generation rules and limits",
                ComponentLimits = ParseComponentLimits(json["componentLimits"] as JObject),
                DisplayRules = ParseDisplayRules(json["displayRules"] as JObject),
                ValidationRules = ParseValidationRules(json["validationRules"] as JObject)
            };

            Log.Information("✅ Loaded generation rules config (version {Version})", _config.Version);
            return _config;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load generation-rules.json, using defaults");
            return GetDefaultConfig();
        }
    }

    /// <summary>
    /// Gets the component limits (prefixes/suffixes by rarity)
    /// </summary>
    public ComponentLimits GetComponentLimits()
    {
        var config = LoadConfig();
        return config.ComponentLimits;
    }

    /// <summary>
    /// Gets the display formatting rules
    /// </summary>
    public DisplayRules GetDisplayRules()
    {
        var config = LoadConfig();
        return config.DisplayRules;
    }

    /// <summary>
    /// Gets the validation rules
    /// </summary>
    public ValidationRules GetValidationRules()
    {
        var config = LoadConfig();
        return config.ValidationRules;
    }

    /// <summary>
    /// Validates component counts for a given rarity tier
    /// </summary>
    /// <param name="rarity">Rarity tier (common, uncommon, rare, epic, legendary)</param>
    /// <param name="prefixCount">Number of prefixes</param>
    /// <param name="suffixCount">Number of suffixes</param>
    /// <returns>True if counts are within limits, false otherwise</returns>
    public bool ValidateComponentCounts(string rarity, int prefixCount, int suffixCount)
    {
        var limits = GetComponentLimits();
        var rarityKey = rarity.ToLowerInvariant();

        if (!limits.Prefixes.ByRarity.TryGetValue(rarityKey, out var prefixLimit))
        {
            Log.Warning("Unknown rarity tier: {Rarity}, using common limits", rarity);
            rarityKey = "common";
            prefixLimit = limits.Prefixes.ByRarity[rarityKey];
        }

        if (!limits.Suffixes.ByRarity.TryGetValue(rarityKey, out var suffixLimit))
        {
            suffixLimit = limits.Suffixes.ByRarity["common"];
        }

        var prefixValid = prefixCount >= prefixLimit.Min && prefixCount <= prefixLimit.Max;
        var suffixValid = suffixCount >= suffixLimit.Min && suffixCount <= suffixLimit.Max;

        if (!prefixValid)
        {
            Log.Warning("Invalid prefix count for {Rarity}: {Count} (allowed: {Min}-{Max})",
                rarity, prefixCount, prefixLimit.Min, prefixLimit.Max);
        }

        if (!suffixValid)
        {
            Log.Warning("Invalid suffix count for {Rarity}: {Count} (allowed: {Min}-{Max})",
                rarity, suffixCount, suffixLimit.Min, suffixLimit.Max);
        }

        return prefixValid && suffixValid;
    }

    /// <summary>
    /// Validates that components are unique (no duplicate prefixes or suffixes)
    /// </summary>
    /// <param name="prefixes">List of prefix names</param>
    /// <param name="suffixes">List of suffix names</param>
    /// <returns>True if all components are unique, false if duplicates found</returns>
    public bool ValidateComponentUniqueness(List<string> prefixes, List<string> suffixes)
    {
        var rules = GetValidationRules();

        if (!rules.EnforceComponentUniqueness)
            return true;

        // Check prefix uniqueness
        if (!rules.AllowDuplicatePrefixes && prefixes.Count != prefixes.Distinct().Count())
        {
            Log.Warning("Duplicate prefixes found: {Prefixes}", string.Join(", ", prefixes));
            return false;
        }

        // Check suffix uniqueness
        if (!rules.AllowDuplicateSuffixes && suffixes.Count != suffixes.Distinct().Count())
        {
            Log.Warning("Duplicate suffixes found: {Suffixes}", string.Join(", ", suffixes));
            return false;
        }

        // Check cross-category uniqueness if not allowed
        if (!rules.AllowDuplicateAcrossCategories)
        {
            var allComponents = prefixes.Concat(suffixes).ToList();
            if (allComponents.Count != allComponents.Distinct().Count())
            {
                Log.Warning("Duplicate components across prefix/suffix categories");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets the maximum allowed prefix count for a rarity tier
    /// </summary>
    public int GetMaxPrefixes(string rarity)
    {
        var limits = GetComponentLimits();
        var rarityKey = rarity.ToLowerInvariant();
        return limits.Prefixes.ByRarity.TryGetValue(rarityKey, out var limit)
            ? limit.Max
            : limits.Prefixes.ByRarity["common"].Max;
    }

    /// <summary>
    /// Gets the maximum allowed suffix count for a rarity tier
    /// </summary>
    public int GetMaxSuffixes(string rarity)
    {
        var limits = GetComponentLimits();
        var rarityKey = rarity.ToLowerInvariant();
        return limits.Suffixes.ByRarity.TryGetValue(rarityKey, out var limit)
            ? limit.Max
            : limits.Suffixes.ByRarity["common"].Max;
    }

    /// <summary>
    /// Formats an item name according to display rules
    /// </summary>
    /// <param name="baseName">Base item name</param>
    /// <param name="quality">Optional quality component</param>
    /// <param name="material">Optional material component</param>
    /// <param name="prefixes">List of prefixes</param>
    /// <param name="suffixes">List of suffixes</param>
    /// <returns>Formatted item name</returns>
    public string FormatItemName(string baseName, string? quality = null, string? material = null,
        List<string>? prefixes = null, List<string>? suffixes = null)
    {
        var rules = GetDisplayRules();
        var parts = new List<string>();

        // Add quality if present
        if (!string.IsNullOrWhiteSpace(quality))
            parts.Add(quality);

        // Add material if present
        if (!string.IsNullOrWhiteSpace(material))
            parts.Add(material);

        // Add prefixes (first only if showFirstPrefixOnly is true)
        if (prefixes != null && prefixes.Count > 0)
        {
            if (rules.ShowFirstPrefixOnly)
                parts.Add(prefixes[0]);
            else if (rules.ShowAllComponents)
                parts.AddRange(prefixes);
        }

        // Add base name
        parts.Add(baseName);

        // Add suffixes (first only if showFirstSuffixOnly is true)
        if (suffixes != null && suffixes.Count > 0)
        {
            if (rules.ShowFirstSuffixOnly)
                parts.Add(suffixes[0]);
            else if (rules.ShowAllComponents)
                parts.AddRange(suffixes);
        }

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Clears the cached configuration, forcing reload on next access
    /// </summary>
    public void ClearCache()
    {
        _config = null;
        Log.Debug("Generation rules cache cleared");
    }

    #region Private Parsing Methods

    private ComponentLimits ParseComponentLimits(JObject? json)
    {
        if (json == null)
            return GetDefaultComponentLimits();

        return new ComponentLimits
        {
            Quality = ParseMinMax(json["quality"] as JObject) ?? new MinMax { Min = 0, Max = 1 },
            Material = ParseMinMax(json["material"] as JObject) ?? new MinMax { Min = 0, Max = 1 },
            Prefixes = ParseRarityLimits(json["prefixes"] as JObject),
            Suffixes = ParseRarityLimits(json["suffixes"] as JObject)
        };
    }

    private RarityLimits ParseRarityLimits(JObject? json)
    {
        if (json == null)
            return new RarityLimits { ByRarity = new Dictionary<string, MinMax>() };

        var byRarity = new Dictionary<string, MinMax>();
        var byRarityJson = json["byRarity"] as JObject;

        if (byRarityJson != null)
        {
            foreach (var prop in byRarityJson.Properties())
            {
                var minMax = ParseMinMax(prop.Value as JObject);
                if (minMax != null)
                    byRarity[prop.Name] = minMax;
            }
        }

        return new RarityLimits { ByRarity = byRarity };
    }

    private MinMax? ParseMinMax(JObject? json)
    {
        if (json == null)
            return null;

        return new MinMax
        {
            Min = json["min"]?.Value<int>() ?? 0,
            Max = json["max"]?.Value<int>() ?? 0
        };
    }

    private DisplayRules ParseDisplayRules(JObject? json)
    {
        if (json == null)
            return GetDefaultDisplayRules();

        return new DisplayRules
        {
            NameFormat = json["nameFormat"]?.Value<string>() ?? "[Quality] [Material] [Prefix₁] [BaseName] [Suffix₁]",
            ShowAllComponents = json["showAllComponents"]?.Value<bool>() ?? false,
            ShowFirstPrefixOnly = json["showFirstPrefixOnly"]?.Value<bool>() ?? true,
            ShowFirstSuffixOnly = json["showFirstSuffixOnly"]?.Value<bool>() ?? true,
            HideComponentsAboveRarity = json["hideComponentsAboveRarity"]?.Value<string>()
        };
    }

    private ValidationRules ParseValidationRules(JObject? json)
    {
        if (json == null)
            return GetDefaultValidationRules();

        return new ValidationRules
        {
            EnforceComponentUniqueness = json["enforceComponentUniqueness"]?.Value<bool>() ?? true,
            AllowDuplicatePrefixes = json["allowDuplicatePrefixes"]?.Value<bool>() ?? false,
            AllowDuplicateSuffixes = json["allowDuplicateSuffixes"]?.Value<bool>() ?? false,
            AllowDuplicateAcrossCategories = json["allowDuplicateAcrossCategories"]?.Value<bool>() ?? true
        };
    }

    #endregion

    #region Default Configuration

    private GenerationRulesConfig GetDefaultConfig()
    {
        return new GenerationRulesConfig
        {
            Version = "4.3",
            Type = "generation_rules_config",
            LastUpdated = DateTime.Now.ToString("yyyy-MM-dd"),
            Description = "Default item generation rules",
            ComponentLimits = GetDefaultComponentLimits(),
            DisplayRules = GetDefaultDisplayRules(),
            ValidationRules = GetDefaultValidationRules()
        };
    }

    private ComponentLimits GetDefaultComponentLimits()
    {
        return new ComponentLimits
        {
            Quality = new MinMax { Min = 0, Max = 1 },
            Material = new MinMax { Min = 0, Max = 1 },
            Prefixes = new RarityLimits
            {
                ByRarity = new Dictionary<string, MinMax>
                {
                    ["common"] = new MinMax { Min = 0, Max = 1 },
                    ["uncommon"] = new MinMax { Min = 0, Max = 1 },
                    ["rare"] = new MinMax { Min = 0, Max = 2 },
                    ["epic"] = new MinMax { Min = 0, Max = 2 },
                    ["legendary"] = new MinMax { Min = 0, Max = 3 }
                }
            },
            Suffixes = new RarityLimits
            {
                ByRarity = new Dictionary<string, MinMax>
                {
                    ["common"] = new MinMax { Min = 0, Max = 0 },
                    ["uncommon"] = new MinMax { Min = 0, Max = 1 },
                    ["rare"] = new MinMax { Min = 0, Max = 1 },
                    ["epic"] = new MinMax { Min = 0, Max = 2 },
                    ["legendary"] = new MinMax { Min = 0, Max = 3 }
                }
            }
        };
    }

    private DisplayRules GetDefaultDisplayRules()
    {
        return new DisplayRules
        {
            NameFormat = "[Quality] [Material] [Prefix₁] [BaseName] [Suffix₁]",
            ShowAllComponents = false,
            ShowFirstPrefixOnly = true,
            ShowFirstSuffixOnly = true,
            HideComponentsAboveRarity = null
        };
    }

    private ValidationRules GetDefaultValidationRules()
    {
        return new ValidationRules
        {
            EnforceComponentUniqueness = true,
            AllowDuplicatePrefixes = false,
            AllowDuplicateSuffixes = false,
            AllowDuplicateAcrossCategories = true
        };
    }

    #endregion
}

#region Configuration Model Classes

/// <summary>
/// Complete generation rules configuration
/// </summary>
public class GenerationRulesConfig
{
    /// <summary>
    /// Configuration version
    /// </summary>
    public required string Version { get; set; }
    
    /// <summary>
    /// Configuration type identifier
    /// </summary>
    public required string Type { get; set; }
    
    /// <summary>
    /// Last update date (ISO format)
    /// </summary>
    public required string LastUpdated { get; set; }
    
    /// <summary>
    /// Configuration description
    /// </summary>
    public required string Description { get; set; }
    
    /// <summary>
    /// Component count limits by rarity
    /// </summary>
    public required ComponentLimits ComponentLimits { get; set; }
    
    /// <summary>
    /// Display formatting rules
    /// </summary>
    public required DisplayRules DisplayRules { get; set; }
    
    /// <summary>
    /// Validation rules for components
    /// </summary>
    public required ValidationRules ValidationRules { get; set; }
}

/// <summary>
/// Component count limits
/// </summary>
public class ComponentLimits
{
    /// <summary>
    /// Quality component limits
    /// </summary>
    public required MinMax Quality { get; set; }
    
    /// <summary>
    /// Material component limits
    /// </summary>
    public required MinMax Material { get; set; }
    
    /// <summary>
    /// Prefix component limits by rarity
    /// </summary>
    public required RarityLimits Prefixes { get; set; }
    
    /// <summary>
    /// Suffix component limits by rarity
    /// </summary>
    public required RarityLimits Suffixes { get; set; }
}

/// <summary>
/// Min/max range for component counts
/// </summary>
public class MinMax
{
    /// <summary>
    /// Minimum allowed count
    /// </summary>
    public required int Min { get; set; }
    
    /// <summary>
    /// Maximum allowed count
    /// </summary>
    public required int Max { get; set; }
}

/// <summary>
/// Rarity-based limits
/// </summary>
public class RarityLimits
{
    /// <summary>
    /// Limits mapped by rarity tier name (common, uncommon, rare, epic, legendary)
    /// </summary>
    public required Dictionary<string, MinMax> ByRarity { get; set; }
}

/// <summary>
/// Display formatting rules
/// </summary>
public class DisplayRules
{
    /// <summary>
    /// Name format template
    /// </summary>
    public required string NameFormat { get; set; }
    
    /// <summary>
    /// Whether to show all components in the name
    /// </summary>
    public required bool ShowAllComponents { get; set; }
    
    /// <summary>
    /// Whether to show only the first prefix
    /// </summary>
    public required bool ShowFirstPrefixOnly { get; set; }
    
    /// <summary>
    /// Whether to show only the first suffix
    /// </summary>
    public required bool ShowFirstSuffixOnly { get; set; }
    
    /// <summary>
    /// Optional rarity tier above which to hide components
    /// </summary>
    public string? HideComponentsAboveRarity { get; set; }
}

/// <summary>
/// Validation rules for component uniqueness
/// </summary>
public class ValidationRules
{
    /// <summary>
    /// Whether to enforce component uniqueness
    /// </summary>
    public required bool EnforceComponentUniqueness { get; set; }
    
    /// <summary>
    /// Whether to allow duplicate prefixes
    /// </summary>
    public required bool AllowDuplicatePrefixes { get; set; }
    
    /// <summary>
    /// Whether to allow duplicate suffixes
    /// </summary>
    public required bool AllowDuplicateSuffixes { get; set; }
    
    /// <summary>
    /// Whether to allow duplicates across prefix/suffix categories
    /// </summary>
    public required bool AllowDuplicateAcrossCategories { get; set; }
}

#endregion
