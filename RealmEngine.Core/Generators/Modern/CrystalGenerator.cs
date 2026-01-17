using RealmEngine.Data.Services;
using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RealmEngine.Core.Generators.Modern;

/// <summary>
/// Generator for creating crystals (socketable items).
/// </summary>
public class CrystalGenerator
{
    private readonly GameDataCache _dataCache;
    private readonly ILogger<CrystalGenerator> _logger;
    private readonly Random _random = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CrystalGenerator"/> class.
    /// </summary>
    /// <param name="dataCache">The game data cache.</param>
    /// <param name="logger">The logger.</param>
    public CrystalGenerator(GameDataCache dataCache, ILogger<CrystalGenerator> logger)
    {
        _dataCache = dataCache ?? throw new ArgumentNullException(nameof(dataCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates a crystal with optional category filter.
    /// </summary>
    /// <param name="category">The category filter.</param>
    /// <returns>The generated crystal.</returns>
    public Crystal? Generate(string? category = null)
    {
        try
        {
            var crystals = LoadAllCrystals();
            if (!crystals.Any()) { _logger.LogWarning("No crystal catalog items found"); return null; }
            if (!string.IsNullOrEmpty(category))
            {
                var filtered = crystals.Where(c => c.Category?.Equals(category, StringComparison.OrdinalIgnoreCase) == true).ToList();
                if (filtered.Any()) crystals = filtered;
            }
            var selected = SelectWeighted(crystals);
            return new Crystal
            {
                Id = Guid.NewGuid().ToString(),
                Slug = selected.Slug,
                Name = selected.Name,
                Description = selected.Description,
                SocketType = SocketType.Crystal,
                Category = selected.Category,
                Rarity = selected.Rarity,
                Price = selected.Price,
                Traits = new Dictionary<string, TraitValue>(selected.Traits),
                RarityWeight = selected.RarityWeight
            };
        }
        catch (Exception ex) { _logger.LogError(ex, "Error generating crystal"); return null; }
    }

    /// <summary>
    /// Generates multiple crystals with optional category filter.
    /// </summary>
    /// <param name="count">Number of crystals to generate.</param>
    /// <param name="category">Optional category filter.</param>
    /// <returns>List of generated crystals.</returns>
    public List<Crystal> GenerateMany(int count, string? category = null)
    {
        var crystals = new List<Crystal>();
        for (int i = 0; i < count; i++)
        {
            var crystal = Generate(category);
            if (crystal != null) crystals.Add(crystal);
        }
        return crystals;
    }

    private List<Crystal> LoadAllCrystals()
    {
        var all = new List<Crystal>();
        var categories = new[] { "mana", "life", "energy", "stamina" };
        foreach (var category in categories)
        {
            // Try both paths: source data (items/crystals/) and package data (crystals/)
            var catalogFile = _dataCache.GetFile($"items/crystals/{category}/catalog.json") 
                           ?? _dataCache.GetFile($"crystals/{category}/catalog.json");
            if (catalogFile?.JsonData == null) continue;
            
            var crystalTypes = catalogFile.JsonData["crystal_types"] as JObject;
            if (crystalTypes == null) continue;
            
            var categoryData = crystalTypes[category] as JObject;
            if (categoryData == null) continue;
            
            var items = categoryData["items"] as JArray;
            if (items == null) continue;
            foreach (var item in items)
            {
                all.Add(new Crystal
                {
                    Slug = item["slug"]?.ToString() ?? string.Empty,
                    Name = item["name"]?.ToString() ?? "Unknown Crystal",
                    Category = item["category"]?.ToString() ?? category,
                    Description = item["description"]?.ToString() ?? string.Empty,
                    RarityWeight = item["rarityWeight"]?.Value<int>() ?? 50,
                    Traits = ParseTraits(item["traits"])
                });
            }
        }
        return all;
    }

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
            if (value != null) traits[prop.Name] = new TraitValue { Value = value.ToString(), Type = traitType };
        }
        return traits;
    }

    private Crystal SelectWeighted(List<Crystal> items)
    {
        var totalWeight = items.Sum(i => i.RarityWeight);
        var roll = _random.Next(1, totalWeight + 1);
        var runningTotal = 0;
        foreach (var item in items) { runningTotal += item.RarityWeight; if (roll <= runningTotal) return item; }
        return items.Last();
    }
}
