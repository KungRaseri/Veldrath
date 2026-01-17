using RealmEngine.Data.Services;
using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RealmEngine.Core.Generators.Modern;

/// <summary>
/// Generator for creating gems (socketable items providing stat bonuses)
/// Loads from gems catalog and creates Gem instances
/// </summary>
public class GemGenerator
{
    private readonly GameDataCache _dataCache;
    private readonly ILogger<GemGenerator> _logger;
    private readonly Random _random = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="GemGenerator"/> class.
    /// </summary>
    /// <param name="dataCache">The game data cache.</param>
    /// <param name="logger">The logger.</param>
    public GemGenerator(GameDataCache dataCache, ILogger<GemGenerator> logger)
    {
        _dataCache = dataCache ?? throw new ArgumentNullException(nameof(dataCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generate a gem with optional category filter (red/blue/green/yellow/white)
    /// </summary>
    public Gem? Generate(string? category = null)
    {
        try
        {
            // Load all gem catalog items from subdomain catalogs
            var gems = LoadAllGems();
            
            if (!gems.Any())
            {
                _logger.LogWarning("No gem catalog items found");
                return null;
            }

            // Filter by category if specified
            if (!string.IsNullOrEmpty(category))
            {
                var filtered = gems.Where(g => g.Category?.Equals(category, StringComparison.OrdinalIgnoreCase) == true).ToList();
                if (filtered.Any())
                {
                    gems = filtered;
                }
                else
                {
                    _logger.LogWarning("No gems found for category '{Category}', using all gems", category);
                }
            }

            // Select gem using weighted random
            var selectedGem = SelectWeightedGem(gems);

            // Create final gem instance with new ID
            var gem = new Gem
            {
                Id = Guid.NewGuid().ToString(),
                Slug = selectedGem.Slug,
                Name = selectedGem.Name,
                Description = selectedGem.Description,
                SocketType = SocketType.Gem,
                Category = selectedGem.Category,
                Color = ParseGemColor(selectedGem.Category),
                Rarity = selectedGem.Rarity,
                Price = selectedGem.Price,
                Traits = new Dictionary<string, TraitValue>(selectedGem.Traits),
                RarityWeight = selectedGem.RarityWeight
            };

            _logger.LogDebug("Generated gem: {Name} (Category: {Category})", gem.Name, gem.Category);
            return gem;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating gem");
            return null;
        }
    }

    /// <summary>
    /// Generate multiple gems
    /// </summary>
    public List<Gem> GenerateMany(int count, string? category = null)
    {
        var gems = new List<Gem>();
        for (int i = 0; i < count; i++)
        {
            var gem = Generate(category);
            if (gem != null)
            {
                gems.Add(gem);
            }
        }
        return gems;
    }

    /// <summary>
    /// Load all gems from subdomain catalog files
    /// </summary>
    private List<Gem> LoadAllGems()
    {
        var allGems = new List<Gem>();
        var categories = new[] { "red", "blue", "green", "yellow", "white" };

        foreach (var category in categories)
        {
            // Try both paths: source data (items/gems/) and package data (gems/)
            var catalogFile = _dataCache.GetFile($"items/gems/{category}/catalog.json") 
                           ?? _dataCache.GetFile($"gems/{category}/catalog.json");
            if (catalogFile?.JsonData == null) continue;

            var gemTypes = catalogFile.JsonData["gem_types"] as JObject;
            if (gemTypes == null) continue;
            
            var categoryData = gemTypes[category] as JObject;
            if (categoryData == null) continue;
            
            var items = categoryData["items"] as JArray;
            if (items == null) continue;

            foreach (var item in items)
            {
                var gem = new Gem
                {
                    Slug = item["slug"]?.ToString() ?? string.Empty,
                    Name = item["name"]?.ToString() ?? "Unknown Gem",
                    Category = item["category"]?.ToString() ?? category,
                    Description = item["description"]?.ToString() ?? string.Empty,
                    RarityWeight = item["rarityWeight"]?.Value<int>() ?? 50,
                    Traits = ParseTraits(item["traits"])
                };
                allGems.Add(gem);
            }
        }

        return allGems;
    }

    /// <summary>
    /// Parse gem color from category string
    /// </summary>
    private GemColor ParseGemColor(string? category)
    {
        return category?.ToLower() switch
        {
            "red" => GemColor.Red,
            "blue" => GemColor.Blue,
            "green" => GemColor.Green,
            "yellow" => GemColor.Yellow,
            "white" => GemColor.White,
            _ => GemColor.Red
        };
    }

    /// <summary>
    /// Parse traits from JSON token
    /// </summary>
    private Dictionary<string, TraitValue> ParseTraits(JToken? traitsToken)
    {
        var traits = new Dictionary<string, TraitValue>();
        if (traitsToken == null) return traits;

        foreach (var prop in traitsToken.Children<JProperty>())
        {
            var traitObj = prop.Value as JObject;
            if (traitObj == null) continue;

            var value = traitObj["value"];
            var typeStr = traitObj["type"]?.ToString() ?? "number";
            var traitType = typeStr.ToLower() switch
            {
                "number" => TraitType.Number,
                "string" => TraitType.String,
                "boolean" => TraitType.Boolean,
                "stringarray" => TraitType.StringArray,
                "numberarray" => TraitType.NumberArray,
                _ => TraitType.Number
            };

            if (value != null)
            {
                traits[prop.Name] = new TraitValue
                {
                    Value = value.ToString(),
                    Type = traitType
                };
            }
        }

        return traits;
    }

    /// <summary>
    /// Select a gem using weighted random selection
    /// </summary>
    private Gem SelectWeightedGem(List<Gem> gems)
    {
        var totalWeight = gems.Sum(g => g.RarityWeight);
        var roll = _random.Next(1, totalWeight + 1);

        var runningTotal = 0;
        foreach (var gem in gems)
        {
            runningTotal += gem.RarityWeight;
            if (roll <= runningTotal)
            {
                return gem;
            }
        }

        return gems.Last();
    }
}
