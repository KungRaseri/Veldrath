using RealmEngine.Data.Services;
using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RealmEngine.Core.Generators.Modern;

/// <summary>
/// Generator for creating orbs (socketable items).
/// </summary>
public class OrbGenerator
{
    private readonly GameDataCache _dataCache;
    private readonly ILogger<OrbGenerator> _logger;
    private readonly Random _random = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="OrbGenerator"/> class.
    /// </summary>
    /// <param name="dataCache">The game data cache.</param>
    /// <param name="logger">The logger.</param>
    public OrbGenerator(GameDataCache dataCache, ILogger<OrbGenerator> logger)
    {
        _dataCache = dataCache ?? throw new ArgumentNullException(nameof(dataCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates an orb with optional category filter.
    /// </summary>
    /// <param name="category">The category filter.</param>
    /// <returns>The generated orb.</returns>
    public Orb? Generate(string? category = null)
    {
        try
        {
            var orbs = LoadAllOrbs();
            if (!orbs.Any()) { _logger.LogWarning("No orb catalog items found"); return null; }
            if (!string.IsNullOrEmpty(category))
            {
                var filtered = orbs.Where(o => o.Category?.Equals(category, StringComparison.OrdinalIgnoreCase) == true).ToList();
                if (filtered.Any()) orbs = filtered;
            }
            var selected = SelectWeighted(orbs);
            return new Orb
            {
                Id = Guid.NewGuid().ToString(),
                Slug = selected.Slug,
                Name = selected.Name,
                Description = selected.Description,
                SocketType = SocketType.Orb,
                Category = selected.Category,
                Rarity = selected.Rarity,
                Price = selected.Price,
                Traits = new Dictionary<string, TraitValue>(selected.Traits),
                RarityWeight = selected.RarityWeight
            };
        }
        catch (Exception ex) { _logger.LogError(ex, "Error generating orb"); return null; }
    }

    /// <summary>
    /// Generates multiple orbs with optional category filter.
    /// </summary>
    /// <param name="count">Number of orbs to generate.</param>
    /// <param name="category">Optional category filter.</param>
    /// <returns>List of generated orbs.</returns>
    public List<Orb> GenerateMany(int count, string? category = null)
    {
        var orbs = new List<Orb>();
        for (int i = 0; i < count; i++)
        {
            var orb = Generate(category);
            if (orb != null) orbs.Add(orb);
        }
        return orbs;
    }

    private List<Orb> LoadAllOrbs()
    {
        var all = new List<Orb>();
        var categories = new[] { "combat", "magic", "stealth", "social" };
        foreach (var category in categories)
        {
            // Try both paths: source data (items/orbs/) and package data (orbs/)
            var catalogFile = _dataCache.GetFile($"items/orbs/{category}/catalog.json") 
                           ?? _dataCache.GetFile($"orbs/{category}/catalog.json");
            if (catalogFile?.JsonData == null) continue;
            
            var orbTypes = catalogFile.JsonData["orb_types"] as JObject;
            if (orbTypes == null) continue;
            
            var categoryData = orbTypes[category] as JObject;
            if (categoryData == null) continue;
            
            var items = categoryData["items"] as JArray;
            if (items == null) continue;
            foreach (var item in items)
            {
                all.Add(new Orb
                {
                    Slug = item["slug"]?.ToString() ?? string.Empty,
                    Name = item["name"]?.ToString() ?? "Unknown Orb",
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

    private Orb SelectWeighted(List<Orb> items)
    {
        var totalWeight = items.Sum(i => i.RarityWeight);
        var roll = _random.Next(1, totalWeight + 1);
        var runningTotal = 0;
        foreach (var item in items) { runningTotal += item.RarityWeight; if (roll <= runningTotal) return item; }
        return items.Last();
    }
}
