using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Equipment.Queries;

/// <summary>
/// Handler for GetEquipmentForClassQuery.
/// Loads base equipment items and filters by class proficiencies.
/// </summary>
public class GetEquipmentForClassHandler : IRequestHandler<GetEquipmentForClassQuery, GetEquipmentForClassResult>
{
    private readonly ICharacterClassRepository _classRepository;
    private readonly IDbContextFactory<ContentDbContext> _dbFactory;
    private readonly ILogger<GetEquipmentForClassHandler> _logger;
    private readonly Random _random = new();

    public GetEquipmentForClassHandler(
        ICharacterClassRepository classRepository,
        IDbContextFactory<ContentDbContext> dbFactory,
        ILogger<GetEquipmentForClassHandler> logger)
    {
        _classRepository = classRepository ?? throw new ArgumentNullException(nameof(classRepository));
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GetEquipmentForClassResult> Handle(GetEquipmentForClassQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var characterClass = _classRepository.GetById(request.ClassId);
            if (characterClass == null)
                return new GetEquipmentForClassResult { Success = false, ErrorMessage = $"Class '{request.ClassId}' not found" };

            var armorProfs = characterClass.ArmorProficiency ?? [];
            var weaponProfs = characterClass.WeaponProficiency ?? [];

            _logger.LogInformation("Loading equipment for class {ClassName} (Armor: {ArmorProfs}, Weapons: {WeaponProfs})",
                characterClass.Name, string.Join(", ", armorProfs), string.Join(", ", weaponProfs));

            var result = new GetEquipmentForClassResult
            {
                Success = true,
                ClassName = characterClass.Name,
                ArmorProficiencies = armorProfs,
                WeaponProficiencies = weaponProfs
            };

            using var db = _dbFactory.CreateDbContext();

            if (request.EquipmentType == null || request.EquipmentType.Equals("weapons", StringComparison.OrdinalIgnoreCase))
                result.Weapons = await LoadWeapons(db, weaponProfs, request.MaxItemsPerCategory, request.RandomizeSelection, cancellationToken);

            if (request.EquipmentType == null || request.EquipmentType.Equals("armor", StringComparison.OrdinalIgnoreCase))
                result.Armor = await LoadArmor(db, armorProfs, request.MaxItemsPerCategory, request.RandomizeSelection, cancellationToken);

            _logger.LogInformation("Loaded {WeaponCount} weapons and {ArmorCount} armor items for class {ClassName}",
                result.Weapons.Count, result.Armor.Count, characterClass.Name);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading equipment for class {ClassId}", request.ClassId);
            return new GetEquipmentForClassResult { Success = false, ErrorMessage = $"Error loading equipment: {ex.Message}" };
        }
    }

    private static readonly Dictionary<string, string[]> WeaponCategoryToProficiencies = new(StringComparer.OrdinalIgnoreCase)
    {
        ["heavy-blades"] = ["swords", "greatswords", "all"],
        ["light-blades"] = ["swords", "daggers", "rapiers", "shortswords", "all"],
        ["axes"]         = ["axes", "all"],
        ["bludgeons"]    = ["maces", "warhammers", "simple", "all"],
        ["polearms"]     = ["polearms", "all"],
        ["bows"]         = ["bows", "all"],
        ["crossbows"]    = ["crossbows", "all"],
        ["staves"]       = ["staves", "simple", "all"],
        ["wands"]        = ["wands", "all"],
    };

    private static readonly Dictionary<string, string[]> ArmorCategoryToProficiencies = new(StringComparer.OrdinalIgnoreCase)
    {
        ["light"]  = ["light", "all"],
        ["medium"] = ["medium", "all"],
        ["heavy"]  = ["heavy", "all"],
        ["shield"] = ["shields", "all"],
    };

    private async Task<List<Item>> LoadWeapons(ContentDbContext db, List<string> proficiencies, int maxItems, bool randomize, CancellationToken ct)
    {
        var weapons = await db.Weapons.Where(w => w.IsActive).ToListAsync(ct);

        var matched = weapons
            .Where(w => WeaponCategoryToProficiencies.TryGetValue(w.TypeKey, out var profs) &&
                        profs.Any(p => proficiencies.Contains(p, StringComparer.OrdinalIgnoreCase)))
            .Select(w => new Item
            {
                Id = $"weapon:{w.TypeKey}:{w.Slug}",
                Slug = w.Slug,
                Name = w.DisplayName ?? w.TypeKey,
                BaseName = w.DisplayName ?? w.TypeKey,
                Type = ItemType.Weapon,
                WeaponType = w.WeaponType,
                Price = w.Stats.Value ?? 0,
                Weight = w.Stats.Weight ?? 0,
                TotalRarityWeight = w.RarityWeight,
            })
            .ToList();

        if (randomize) matched = [.. matched.OrderBy(_ => _random.Next())];
        if (maxItems > 0 && matched.Count > maxItems) matched = matched.Take(maxItems).ToList();
        return matched;
    }

    private async Task<List<Item>> LoadArmor(ContentDbContext db, List<string> proficiencies, int maxItems, bool randomize, CancellationToken ct)
    {
        var armors = await db.Armors.Where(a => a.IsActive).ToListAsync(ct);

        var matched = armors
            .Where(a => ArmorCategoryToProficiencies.TryGetValue(a.ArmorType, out var profs) &&
                        profs.Any(p => proficiencies.Contains(p, StringComparer.OrdinalIgnoreCase)))
            .Select(a => new Item
            {
                Id = $"armor:{a.TypeKey}:{a.Slug}",
                Slug = a.Slug,
                Name = a.DisplayName ?? a.TypeKey,
                BaseName = a.DisplayName ?? a.TypeKey,
                Type = a.ArmorType.Equals("shield", StringComparison.OrdinalIgnoreCase) ? ItemType.Shield : ItemType.Chest,
                ArmorType = a.ArmorType,
                ArmorClass = a.ArmorType,
                Price = a.Stats.Value ?? 0,
                Weight = a.Stats.Weight ?? 0,
                TotalRarityWeight = a.RarityWeight,
            })
            .ToList();

        if (randomize) matched = [.. matched.OrderBy(_ => _random.Next())];
        if (maxItems > 0 && matched.Count > maxItems) matched = matched.Take(maxItems).ToList();
        return matched;
    }
}
