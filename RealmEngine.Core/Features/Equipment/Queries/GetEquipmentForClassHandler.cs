using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Equipment.Queries;

/// <summary>
/// Handles <see cref="GetEquipmentForClassQuery"/>.
/// Loads weapon and armor items via <see cref="IWeaponRepository"/> and <see cref="IArmorRepository"/>,
/// then filters by the class's proficiency lists. No direct DB context dependency.
/// </summary>
public class GetEquipmentForClassHandler : IRequestHandler<GetEquipmentForClassQuery, GetEquipmentForClassResult>
{
    private readonly ICharacterClassRepository _classRepository;
    private readonly IWeaponRepository _weaponRepository;
    private readonly IArmorRepository _armorRepository;
    private readonly ILogger<GetEquipmentForClassHandler> _logger;
    private readonly Random _random = new();

    /// <param name="classRepository">Provides class proficiency lists.</param>
    /// <param name="weaponRepository">Source of all active weapon items.</param>
    /// <param name="armorRepository">Source of all active armor items.</param>
    /// <param name="logger">Logger.</param>
    public GetEquipmentForClassHandler(
        ICharacterClassRepository classRepository,
        IWeaponRepository weaponRepository,
        IArmorRepository armorRepository,
        ILogger<GetEquipmentForClassHandler> logger)
    {
        _classRepository = classRepository ?? throw new ArgumentNullException(nameof(classRepository));
        _weaponRepository = weaponRepository ?? throw new ArgumentNullException(nameof(weaponRepository));
        _armorRepository = armorRepository ?? throw new ArgumentNullException(nameof(armorRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
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

            if (request.EquipmentType == null || request.EquipmentType.Equals("weapons", StringComparison.OrdinalIgnoreCase))
                result.Weapons = await LoadWeapons(weaponProfs, request.MaxItemsPerCategory, request.RandomizeSelection, cancellationToken);

            if (request.EquipmentType == null || request.EquipmentType.Equals("armor", StringComparison.OrdinalIgnoreCase))
                result.Armor = await LoadArmor(armorProfs, request.MaxItemsPerCategory, request.RandomizeSelection, cancellationToken);

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

    // TypeKey (catalog category) → proficiency tokens recognised by class definitions
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

    private async Task<List<Item>> LoadWeapons(List<string> proficiencies, int maxItems, bool randomize, CancellationToken ct)
    {
        var all = await _weaponRepository.GetAllAsync();

        var matched = all
            .Where(w => w.TypeKey != null &&
                        WeaponCategoryToProficiencies.TryGetValue(w.TypeKey, out var profs) &&
                        profs.Any(p => proficiencies.Contains(p, StringComparer.OrdinalIgnoreCase)))
            .ToList();

        if (randomize) matched = [.. matched.OrderBy(_ => _random.Next())];
        if (maxItems > 0 && matched.Count > maxItems) matched = matched.Take(maxItems).ToList();
        return matched;
    }

    private async Task<List<Item>> LoadArmor(List<string> proficiencies, int maxItems, bool randomize, CancellationToken ct)
    {
        var all = await _armorRepository.GetAllAsync();

        var matched = all
            .Where(a => a.TypeKey != null &&
                        ArmorCategoryToProficiencies.TryGetValue(a.TypeKey, out var profs) &&
                        profs.Any(p => proficiencies.Contains(p, StringComparer.OrdinalIgnoreCase)))
            .ToList();

        if (randomize) matched = [.. matched.OrderBy(_ => _random.Next())];
        if (maxItems > 0 && matched.Count > maxItems) matched = matched.Take(maxItems).ToList();
        return matched;
    }
}
