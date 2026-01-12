using Newtonsoft.Json.Linq;
using RealmEngine.Shared.Models;
using Serilog;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RealmEngine.Core.Features.Reputation.Services;

/// <summary>
/// Service for loading faction data from JSON catalogs.
/// </summary>
public class FactionDataService
{
    private readonly string _dataPath;
    private List<Faction>? _cachedFactions;

    /// <summary>
    /// Initializes a new instance of the <see cref="FactionDataService"/> class.
    /// </summary>
    /// <param name="dataPath">The base data path for the game.</param>
    public FactionDataService(string dataPath)
    {
        _dataPath = dataPath;
    }

    /// <summary>
    /// Loads all factions from the organizations/factions catalog.
    /// </summary>
    public List<Faction> LoadFactions()
    {
        if (_cachedFactions != null)
        {
            return _cachedFactions;
        }

        var catalogPath = Path.Combine(_dataPath, "Data", "Json", "organizations", "factions", "catalog.json");
        
        if (!File.Exists(catalogPath))
        {
            Log.Warning("Faction catalog not found at {Path}", catalogPath);
            return new List<Faction>();
        }

        try
        {
            var json = File.ReadAllText(catalogPath);
            var catalog = JObject.Parse(json);

            var factions = new List<Faction>();

            // Parse the faction_types structure
            var factionTypes = catalog["faction_types"] as JObject;
            if (factionTypes != null)
            {
                foreach (var typeProperty in factionTypes.Properties())
                {
                    var typeName = typeProperty.Name;
                    var typeObject = typeProperty.Value as JObject;
                    
                    if (typeObject?["items"] is JArray items)
                    {
                        foreach (var item in items)
                        {
                            var faction = ParseFactionItem(item, typeName);
                            if (faction != null)
                            {
                                factions.Add(faction);
                            }
                        }
                    }
                }
            }

            _cachedFactions = factions;
            Log.Information("Loaded {Count} factions from catalog", factions.Count);
            return factions;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load faction catalog from {Path}", catalogPath);
            return new List<Faction>();
        }
    }

    /// <summary>
    /// Gets a faction by its slug/name.
    /// </summary>
    public Faction? GetFactionBySlug(string slug)
    {
        var factions = LoadFactions();
        return factions.FirstOrDefault(f => 
            f.Id.Equals(slug, StringComparison.OrdinalIgnoreCase) ||
            f.Name.Equals(slug, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets all factions of a specific type.
    /// </summary>
    public List<Faction> GetFactionsByType(FactionType type)
    {
        var factions = LoadFactions();
        return factions.Where(f => f.Type == type).ToList();
    }

    private Faction? ParseFactionItem(JToken item, string typeName)
    {
        try
        {
            var slug = item["slug"]?.ToString();
            var name = item["name"]?.ToString();
            var displayName = item["displayName"]?.ToString();
            var description = item["description"]?.ToString();

            if (string.IsNullOrEmpty(slug))
            {
                Log.Warning("Faction item missing slug in type {TypeName}", typeName);
                return null;
            }

            // Map type name to FactionType enum
            var factionType = MapTypeNameToEnum(typeName);

            // Parse allies and enemies
            var allies = item["allies"]?.ToObject<List<string>>() ?? new List<string>();
            var enemies = item["enemies"]?.ToObject<List<string>>() ?? new List<string>();

            // Determine starting reputation based on reputation field
            var reputationStr = item["reputation"]?.ToString()?.ToLower();
            int startingReputation = reputationStr switch
            {
                "criminal" => -500,
                "lawful" => 0,
                "lawful_good" => 500,
                "lawful_neutral" => 0,
                _ => 0
            };

            var faction = new Faction
            {
                Id = slug,
                Name = displayName ?? name ?? slug,
                Type = factionType,
                Description = description ?? "",
                HomeLocation = "", // Not in old schema
                AllyFactionIds = allies,
                EnemyFactionIds = enemies
            };

            return faction;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to parse faction item in type {TypeName}", typeName);
            return null;
        }
    }

    private FactionType MapTypeNameToEnum(string typeName)
    {
        return typeName.ToLower() switch
        {
            "trade" => FactionType.Guild,
            "labor" => FactionType.Guild,
            "criminal" => FactionType.Criminal,
            "military" => FactionType.Guild,
            "magical" => FactionType.Guild,
            "academic" => FactionType.Guild,
            "religious" => FactionType.Religious,
            "social" => FactionType.Neutral,
            "political" => FactionType.Kingdom,
            _ => FactionType.Neutral
        };
    }
}
