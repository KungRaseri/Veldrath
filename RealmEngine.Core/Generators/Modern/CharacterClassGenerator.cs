using RealmEngine.Data.Services;
using RealmEngine.Shared.Data.Models;
using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace RealmEngine.Core.Generators.Modern;

/// <summary>
/// Generates CharacterClass instances from classes/catalog.json using GameDataCache.
/// </summary>
public class CharacterClassGenerator
{
    private readonly GameDataCache _dataCache;
    private readonly ReferenceResolverService _referenceResolver;
    private readonly ILogger<CharacterClassGenerator> _logger;
    private ClassCatalogData? _classCatalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="CharacterClassGenerator"/> class.
    /// </summary>
    /// <param name="dataCache">The game data cache.</param>
    /// <param name="referenceResolver">The reference resolver service.</param>
    /// <param name="logger">The logger.</param>
    public CharacterClassGenerator(GameDataCache dataCache, ReferenceResolverService referenceResolver, ILogger<CharacterClassGenerator> logger)
    {
        _dataCache = dataCache ?? throw new ArgumentNullException(nameof(dataCache));
        _referenceResolver = referenceResolver ?? throw new ArgumentNullException(nameof(referenceResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Loads the classes catalog from JSON.
    /// </summary>
    private ClassCatalogData LoadCatalog()
    {
        if (_classCatalog != null)
            return _classCatalog;

        var cachedFile = _dataCache.GetFile("classes/catalog.json");
        if (cachedFile == null)
        {
            _logger.LogError("Failed to load classes/catalog.json from cache");
            throw new InvalidOperationException("Classes catalog not found in cache");
        }

        try
        {
            var catalogData = JsonSerializer.Deserialize<ClassCatalogData>(cachedFile.JsonData.ToString());
            if (catalogData == null)
            {
                _logger.LogError("Failed to deserialize classes/catalog.json");
                throw new InvalidOperationException("Failed to deserialize classes catalog");
            }

            _classCatalog = catalogData;
            _logger.LogInformation("Loaded classes catalog with {Count} categories", catalogData.ClassTypes.Count);
            return catalogData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deserializing classes/catalog.json");
            throw;
        }
    }

    /// <summary>
    /// Gets all classes from all categories.
    /// </summary>
    /// <param name="hydrate">If true, populates resolved StartingAbilities and StartingEquipment properties (default: true).</param>
    public async Task<List<CharacterClass>> GetAllClassesAsync(bool hydrate = true)
    {
        var catalog = LoadCatalog();
        var classes = new List<CharacterClass>();

        foreach (var (categoryKey, category) in catalog.ClassTypes)
        {
            foreach (var classData in category.Items)
            {
                var characterClass = MapToCharacterClass(classData, categoryKey, category.Metadata);
                if (hydrate)
                {
                    await HydrateCharacterClassAsync(characterClass);
                }
                classes.Add(characterClass);
            }
        }

        _logger.LogInformation("Generated {Count} character classes", classes.Count);
        return classes;
    }

    /// <summary>
    /// Gets a specific class by name (searches all categories).
    /// </summary>
    /// <param name="name">The name or display name of the class to find.</param>
    /// <param name="hydrate">If true, populates resolved StartingAbilities and StartingEquipment properties (default: true).</param>
    public async Task<CharacterClass?> GetClassByNameAsync(string name, bool hydrate = true)
    {
        var catalog = LoadCatalog();

        foreach (var (categoryKey, category) in catalog.ClassTypes)
        {
            var classData = category.Items.FirstOrDefault(c => 
                c.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                c.DisplayName.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (classData != null)
            {
                var characterClass = MapToCharacterClass(classData, categoryKey, category.Metadata);
                if (hydrate)
                {
                    await HydrateCharacterClassAsync(characterClass);
                }
                return characterClass;
            }
        }

        _logger.LogWarning("Class not found: {Name}", name);
        return null;
    }

    /// <summary>
    /// Gets all classes in a specific category (e.g., "warrior", "mage").
    /// </summary>
    /// <param name="categoryName">The category name to search.</param>
    /// <param name="hydrate">If true, populates resolved StartingAbilities and StartingEquipment properties (default: true).</param>
    public async Task<List<CharacterClass>> GetClassesByCategoryAsync(string categoryName, bool hydrate = true)
    {
        var catalog = LoadCatalog();

        if (!catalog.ClassTypes.TryGetValue(categoryName, out var category))
        {
            _logger.LogWarning("Class category not found: {Category}", categoryName);
            return new List<CharacterClass>();
        }

        var classes = new List<CharacterClass>();
        foreach (var classData in category.Items)
        {
            var characterClass = MapToCharacterClass(classData, categoryName, category.Metadata);
            if (hydrate)
            {
                await HydrateCharacterClassAsync(characterClass);
            }
            classes.Add(characterClass);
        }

        _logger.LogInformation("Found {Count} classes in category {Category}", classes.Count, categoryName);
        return classes;
    }

    /// <summary>
    /// Maps ClassItemData to CharacterClass model.
    /// </summary>
    private CharacterClass MapToCharacterClass(ClassItemData data, string categoryKey, ClassCategoryMetadata? categoryMetadata)
    {
        var characterClass = new CharacterClass
        {
            Id = $"{categoryKey}:{data.Name}",
            Slug = data.Slug,
            Name = data.Name,
            DisplayName = data.DisplayName,
            Description = data.Description,
            RarityWeight = data.RarityWeight,
            IsSubclass = data.IsSubclass,
            ParentClassId = ParseParentClassReference(data.ParentClass),
            StartingAbilityIds = ParseAbilityReferences(data.StartingAbilityIds),
            Traits = data.Traits ?? new Dictionary<string, object>()
        };

        // Map starting stats
        if (data.StartingStats != null)
        {
            characterClass.StartingHealth = data.StartingStats.Health;
            characterClass.StartingMana = data.StartingStats.Mana;
            characterClass.BonusStrength = data.StartingStats.Strength;
            characterClass.BonusDexterity = data.StartingStats.Dexterity;
            characterClass.BonusConstitution = data.StartingStats.Constitution;
            characterClass.BonusIntelligence = data.StartingStats.Intelligence;
            characterClass.BonusWisdom = data.StartingStats.Wisdom;
            characterClass.BonusCharisma = data.StartingStats.Charisma;
        }

        // Copy primary attributes from category metadata
        if (categoryMetadata != null)
        {
            characterClass.PrimaryAttributes = new List<string>(categoryMetadata.PrimaryStats);
        }

        return characterClass;
    }

    /// <summary>
    /// Parses parent class reference like "@classes/warriors:fighter" to "warriors:fighter".
    /// </summary>
    private string? ParseParentClassReference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return null;

        // Reference format: @classes/warriors:fighter
        // Extract: warriors:fighter
        if (reference.StartsWith("@classes/"))
        {
            return reference.Substring("@classes/".Length);
        }

        _logger.LogWarning("Invalid parent class reference format: {Reference}", reference);
        return reference; // Return as-is if format is unexpected
    }

    /// <summary>
    /// Parses ability reference array like ["@abilities/active/support:heal", "@abilities/active/support:bless"].
    /// Returns list of ability IDs like "active/support:heal", "active/support:bless".
    /// </summary>
    private List<string> ParseAbilityReferences(List<string>? referencesList)
    {
        if (referencesList == null || !referencesList.Any())
            return new List<string>();

        var abilityIds = new List<string>();

        foreach (var reference in referencesList)
        {
            if (reference.StartsWith("@abilities/"))
            {
                // Keep full reference for ReferenceResolverService
                abilityIds.Add(reference);
            }
            else
            {
                _logger.LogWarning("Invalid ability reference format: {Reference}", reference);
                abilityIds.Add(reference); // Add as-is
            }
        }

        return abilityIds;
    }

    /// <summary>
    /// Hydrates a character class by resolving reference IDs to full objects.
    /// Populates StartingAbilities and StartingEquipment properties.
    /// </summary>
    /// <param name="characterClass">The character class to hydrate.</param>
    private async Task HydrateCharacterClassAsync(CharacterClass characterClass)
    {
        // Resolve starting abilities
        if (characterClass.StartingAbilityIds != null && characterClass.StartingAbilityIds.Any())
        {
            var abilities = new List<Ability>();
            foreach (var refId in characterClass.StartingAbilityIds)
            {
                try
                {
                    var abilityJson = await _referenceResolver.ResolveToObjectAsync(refId);
                    if (abilityJson != null)
                    {
                        var ability = abilityJson.ToObject<Ability>();
                        if (ability != null)
                        {
                            abilities.Add(ability);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to deserialize ability from reference '{AbilityId}' for class '{ClassName}'", refId, characterClass.Name);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Could not resolve ability reference '{AbilityId}' for class '{ClassName}' - item may not exist in catalog", refId, characterClass.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception resolving starting ability '{AbilityId}' for class '{ClassName}'", refId, characterClass.Name);
                }
            }
            characterClass.StartingAbilities = abilities;
        }

        // Resolve starting equipment
        if (characterClass.StartingEquipmentIds != null && characterClass.StartingEquipmentIds.Any())
        {
            var equipment = new List<Item>();
            foreach (var refId in characterClass.StartingEquipmentIds)
            {
                try
                {
                    var itemJson = await _referenceResolver.ResolveToObjectAsync(refId);
                    if (itemJson != null)
                    {
                        var item = itemJson.ToObject<Item>();
                        if (item != null)
                        {
                            equipment.Add(item);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to deserialize item from reference '{ItemId}' for class '{ClassName}'", refId, characterClass.Name);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Could not resolve equipment reference '{ItemId}' for class '{ClassName}' - item may not exist in catalog", refId, characterClass.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception resolving starting equipment '{ItemId}' for class '{ClassName}'", refId, characterClass.Name);
                }
            }
            characterClass.StartingEquipment = equipment;
        }
    }
}
