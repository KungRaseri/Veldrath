using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Generators.Modern;

/// <summary>Generates Location instances from the world location catalog in the database.</summary>
public class LocationGenerator
{
    private readonly ContentDbContext _db;
    private readonly ILogger<LocationGenerator> _logger;
    private readonly Random _random = new();

    public LocationGenerator(ContentDbContext db, ILogger<LocationGenerator> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Parameterless constructor for test subclassing.</summary>
    protected LocationGenerator() : this(null!, null!) { }

    /// <summary>Generates a list of locations matching the given location type slug.</summary>
    public virtual async Task<List<Location>> GenerateLocationsAsync(string locationType, int count = 5, bool hydrate = true)
    {
        try
        {
            var all = await _db.ZoneLocations.AsNoTracking()
                .Where(l => l.IsActive && l.TypeKey == locationType)
                .ToListAsync();

            if (all.Count == 0) return [];

            var result = new List<Location>(count);
            for (int i = 0; i < count; i++)
            {
                var entity = all[_random.Next(all.Count)];
                result.Add(MapToModel(entity));
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating locations type={Type}", locationType);
            return [];
        }
    }

    /// <summary>Generates a specific location by slug.</summary>
    public virtual async Task<Location?> GenerateLocationByNameAsync(string locationType, string locationName, bool hydrate = true)
    {
        try
        {
            var entity = await _db.ZoneLocations.AsNoTracking()
                .Where(l => l.IsActive && l.Slug == locationName)
                .FirstOrDefaultAsync();

            return entity is null ? null : MapToModel(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating location by name {Name}", locationName);
            return null;
        }
    }

    /// <summary>Generates location loot result based on danger rating.</summary>
    public LocationLootResult GenerateLocationLoot(Location location)
    {
        var danger = location.DangerRating;
        var gold = _random.Next(danger * 5, danger * 20 + 1);
        var xp = _random.Next(danger * 10, danger * 50 + 1);
        var dropChance = Math.Min(0.1 + danger * 0.05, 0.9);
        var shouldDrop = _random.NextDouble() < dropChance;

        ItemRarity? rarity = null;
        if (shouldDrop)
        {
            rarity = danger switch
            {
                >= 8 => ItemRarity.Legendary,
                >= 6 => ItemRarity.Epic,
                >= 4 => ItemRarity.Rare,
                >= 2 => ItemRarity.Uncommon,
                _ => ItemRarity.Common
            };
        }

        return new LocationLootResult
        {
            GoldAmount = gold,
            ExperienceAmount = xp,
            ShouldDropItem = shouldDrop,
            SuggestedItemRarity = rarity,
            ItemCategory = shouldDrop ? "weapons" : null
        };
    }

    private static Location MapToModel(Data.Entities.ZoneLocation e) => new()
    {
        Id = e.Slug,
        Name = e.DisplayName ?? e.Slug,
        Description = string.Empty,
        Type = e.TypeKey,
        Level = e.Stats.MinLevel ?? 1,
        DangerRating = e.Stats.DangerLevel ?? 1,
        HasShop = e.Traits.HasMerchant ?? false,
        HasInn = e.Traits.IsTown ?? false,
        IsSafeZone = e.Traits.IsTown ?? false,
        IsStartingZone = false,
        LocationType = e.TypeKey,
    };

    /// <summary>Generates an enemy appropriate for the given location's level range and type.</summary>
    public virtual async Task<Enemy?> GenerateLocationAppropriateEnemyAsync(Location location, EnemyGenerator enemyGenerator)
    {
        // Determine family based on location type
        var family = location.LocationType?.ToLowerInvariant() switch
        {
            "dungeon" => "undead",
            "forest" => "beasts",
            "swamp" => "beasts",
            "ruins" => "undead",
            "cave" => "beasts",
            _ => "humanoids"
        };

        var enemies = await enemyGenerator.GenerateEnemiesAsync(family, count: 10, hydrate: true);
        var levelMatches = enemies
            .Where(e => Math.Abs(e.Level - location.Level) <= 3)
            .ToList();

        if (levelMatches.Count > 0)
            return levelMatches[_random.Next(levelMatches.Count)];

        return enemies.Count > 0 ? enemies[_random.Next(enemies.Count)] : null;
    }
}
