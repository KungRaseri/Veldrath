using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// EF Core-backed repository for character classes, reading from <see cref="ContentDbContext"/>.
/// </summary>
public class EfCoreCharacterClassRepository(ContentDbContext db, ILogger<EfCoreCharacterClassRepository> logger)
    : ICharacterClassRepository
{
    private List<CharacterClass>? _cache;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    private async Task<List<CharacterClass>> GetCachedAsync()
    {
        if (_cache is not null)
            return _cache;

        await _cacheLock.WaitAsync();
        try
        {
            if (_cache is not null)
                return _cache;

            var entities = await db.ActorClasses
                .Where(c => c.IsActive)
                .Include(c => c.PowerUnlocks)
                    .ThenInclude(u => u.Power)
                .ToListAsync();

            _cache = entities.Select(MapToModel).ToList();
            logger.LogDebug("Loaded {Count} actor classes from database", _cache.Count);
            return _cache;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <inheritdoc />
    public List<CharacterClass> GetAll() => GetCachedAsync().GetAwaiter().GetResult();

    /// <inheritdoc />
    public List<CharacterClass> GetClassesByType(string classType) =>
        GetAll()
            .Where(c => c.Id.StartsWith($"{classType}:", StringComparison.OrdinalIgnoreCase))
            .ToList();

    /// <inheritdoc />
    public List<CharacterClass> GetBaseClasses() =>
        GetAll().Where(c => !c.IsSubclass).ToList();

    /// <inheritdoc />
    public List<CharacterClass> GetSubclasses() =>
        GetAll().Where(c => c.IsSubclass).ToList();

    /// <inheritdoc />
    public List<CharacterClass> GetSubclassesForParent(string parentClassId) =>
        GetAll()
            .Where(c => c.IsSubclass &&
                        c.ParentClassId != null &&
                        c.ParentClassId.Equals(parentClassId, StringComparison.OrdinalIgnoreCase))
            .ToList();

    /// <inheritdoc />
    public CharacterClass? GetByName(string name) =>
        GetAll().FirstOrDefault(c =>
            c.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
            c.DisplayName.Equals(name, StringComparison.OrdinalIgnoreCase) ||
            c.Slug.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public CharacterClass? GetById(string id)
    {
        var byId = GetAll().FirstOrDefault(c => c.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        return byId ?? GetByName(id);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cache = null;
        _cacheLock.Dispose();
    }

    private static CharacterClass MapToModel(Entities.ActorClass entity) => new()
    {
        Id           = $"{entity.TypeKey}:{entity.DisplayName ?? entity.Slug}",
        Slug         = entity.Slug,
        Name         = entity.DisplayName ?? entity.Slug,
        DisplayName  = entity.DisplayName ?? entity.Slug,
        Description  = string.Empty,
        RarityWeight = entity.RarityWeight,
        IsSubclass   = false,
        ParentClassId = null,
        PrimaryAttributes  = string.IsNullOrEmpty(entity.PrimaryStat) ? [] : [entity.PrimaryStat],
        ArmorProficiency   = BuildArmorProficiencies(entity),
        WeaponProficiency  = BuildWeaponProficiencies(entity),
        StartingHealth     = entity.Stats.BaseHealth ?? 100,
        StartingMana       = entity.Stats.BaseMana   ?? 50,
        BonusStrength      = 0,
        BonusDexterity     = 0,
        BonusConstitution  = 0,
        BonusIntelligence  = 0,
        BonusWisdom        = 0,
        BonusCharisma      = 0,
        StartingPowerIds = entity.PowerUnlocks
            .Where(u => u.LevelRequired == 1 && u.Power is not null)
            .Select(u => $"@powers/{u.Power!.TypeKey}:{u.Power.Slug}")
            .ToList(),
        StartingEquipmentIds = [],
        Traits = new Dictionary<string, object>
        {
            ["canDualWield"]   = entity.Traits.CanDualWield  ?? false,
            ["canWearHeavy"]   = entity.Traits.CanWearHeavy  ?? false,
            ["spellcaster"]    = entity.Traits.Spellcaster   ?? false,
            ["canWearShield"]  = entity.Traits.CanWearShield ?? false,
            ["melee"]          = entity.Traits.Melee         ?? false,
            ["ranged"]         = entity.Traits.Ranged        ?? false,
            ["stealth"]        = entity.Traits.Stealth       ?? false,
        },
        Progression = new ClassProgression(),
    };

    // ActorClass stores playstyle traits rather than explicit proficiency lists. Derive the
    // proficiency tokens expected by GetEquipmentForClassHandler from those traits.
    private static List<string> BuildArmorProficiencies(Entities.ActorClass entity)
    {
        var proficiencies = new List<string> { "light" };

        if (entity.Traits.CanWearHeavy == true)
        {
            proficiencies.Add("medium");
            proficiencies.Add("heavy");
        }

        if (entity.Traits.CanWearShield == true)
            proficiencies.Add("shields");

        return proficiencies;
    }

    private static List<string> BuildWeaponProficiencies(Entities.ActorClass entity)
    {
        var proficiencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "simple",
            "daggers",
        };

        if (entity.Traits.Melee == true)
            proficiencies.UnionWith(["swords", "axes", "maces", "polearms"]);

        if (entity.Traits.Ranged == true)
            proficiencies.UnionWith(["bows", "crossbows"]);

        if (entity.Traits.Spellcaster == true)
            proficiencies.UnionWith(["staves", "wands"]);

        if (entity.Traits.Stealth == true)
            proficiencies.UnionWith(["swords", "shortswords", "rapiers"]);

        if (entity.Traits.CanDualWield == true)
            proficiencies.UnionWith(["shortswords", "rapiers"]);

        return [.. proficiencies];
    }
}
