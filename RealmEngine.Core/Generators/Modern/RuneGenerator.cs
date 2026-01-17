using RealmEngine.Data.Services;
using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RealmEngine.Core.Generators.Modern;

/// <summary>
/// Generator for creating runes (socketable items).
/// </summary>
public class RuneGenerator
{
    private readonly GameDataCache _dataCache;
    private readonly ILogger<RuneGenerator> _logger;
    private readonly Random _random = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="RuneGenerator"/> class.
    /// </summary>
    /// <param name="dataCache">The game data cache.</param>
    /// <param name="logger">The logger.</param>
    public RuneGenerator(GameDataCache dataCache, ILogger<RuneGenerator> logger)
    {
        _dataCache = dataCache ?? throw new ArgumentNullException(nameof(dataCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates a rune with optional category filter.
    /// </summary>
    /// <param name="category">The category filter.</param>
    /// <returns>The generated rune.</returns>
    public Rune? Generate(string? category = null)
    {
        try
        {
            var runes = LoadAllRunes();
            if (!runes.Any()) { _logger.LogWarning("No rune catalog items found"); return null; }
            if (!string.IsNullOrEmpty(category))
            {
                var filtered = runes.Where(r => r.Category?.Equals(category, StringComparison.OrdinalIgnoreCase) == true).ToList();
                if (filtered.Any()) runes = filtered;
            }
            var selected = SelectWeighted(runes);
            return new Rune
            {
                Id = Guid.NewGuid().ToString(),
                Slug = selected.Slug,
                Name = selected.Name,
                Description = selected.Description,
                SocketType = SocketType.Rune,
                Category = selected.Category,
                Rarity = selected.Rarity,
                Price = selected.Price,
                Traits = new Dictionary<string, TraitValue>(selected.Traits),
                RarityWeight = selected.RarityWeight
            };
        }
        catch (Exception ex) { _logger.LogError(ex, "Error generating rune"); return null; }
    }

    /// <summary>
    /// Generates multiple runes with optional category filter.
    /// </summary>
    /// <param name="count">Number of runes to generate.</param>
    /// <param name="category">Optional category filter.</param>
    /// <returns>List of generated runes.</returns>
    public List<Rune> GenerateMany(int count, string? category = null)
    {
        var runes = new List<Rune>();
        for (int i = 0; i < count; i++)
        {
            var rune = Generate(category);
            if (rune != null) runes.Add(rune);
        }
        return runes;
    }

    private List<Rune> LoadAllRunes()
    {
        var all = new List<Rune>();
        var categories = new[] { "offensive", "defensive", "utility" };
        foreach (var category in categories)
        {
            // Try both paths: source data (items/runes/) and package data (runes/)
            var catalogFile = _dataCache.GetFile($"items/runes/{category}/catalog.json") 
                           ?? _dataCache.GetFile($"runes/{category}/catalog.json");
            if (catalogFile?.JsonData == null) continue;
            
            var runeTypes = catalogFile.JsonData["rune_types"] as JObject;
            if (runeTypes == null) continue;
            
            var categoryData = runeTypes[category] as JObject;
            if (categoryData == null) continue;
            
            var items = categoryData["items"] as JArray;
            if (items == null) continue;
            foreach (var item in items)
            {
                all.Add(new Rune
                {
                    Slug = item["slug"]?.ToString() ?? string.Empty,
                    Name = item["name"]?.ToString() ?? "Unknown Rune",
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

    private Rune SelectWeighted(List<Rune> items)
    {
        var totalWeight = items.Sum(i => i.RarityWeight);
        var roll = _random.Next(1, totalWeight + 1);
        var runningTotal = 0;
        foreach (var item in items) { runningTotal += item.RarityWeight; if (roll <= runningTotal) return item; }
        return items.Last();
    }
}
