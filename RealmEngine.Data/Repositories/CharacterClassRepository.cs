using RealmEngine.Shared.Models;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Data.Models;
using RealmEngine.Data.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// Repository that loads character classes from classes/catalog.json via GameDataCache.
/// </summary>
public class CharacterClassRepository : ICharacterClassRepository
{
    private readonly GameDataCache _dataCache;
    private readonly ReferenceResolverService _referenceResolver;
    private readonly ILogger<CharacterClassRepository> _logger;
    private List<CharacterClass>? _cachedClasses;
    private readonly object _cacheLock = new object();

    /// <summary>
    /// Initializes a new instance of the <see cref="CharacterClassRepository"/> class.
    /// </summary>
    /// <param name="dataCache">The game data cache.</param>
    /// <param name="referenceResolver">The reference resolver service.</param>
    /// <param name="logger">The logger.</param>
    public CharacterClassRepository(
        GameDataCache dataCache, 
        ReferenceResolverService referenceResolver,
        ILogger<CharacterClassRepository> logger)
    {
        _dataCache = dataCache ?? throw new ArgumentNullException(nameof(dataCache));
        _referenceResolver = referenceResolver ?? throw new ArgumentNullException(nameof(referenceResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets all available character classes from catalog.
    /// </summary>
    public List<CharacterClass> GetAllClasses()
    {
        lock (_cacheLock)
        {
            if (_cachedClasses != null)
                return _cachedClasses;

            _cachedClasses = LoadClassesFromCatalog();
            return _cachedClasses;
        }
    }

    /// <summary>
    /// Gets all classes of a specific type/category (e.g., "warrior", "mage", "cleric").
    /// </summary>
    public List<CharacterClass> GetClassesByType(string classType)
    {
        return GetAllClasses()
            .Where(c => c.Id.StartsWith($"{classType}:", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Gets only base classes (excluding subclasses).
    /// </summary>
    public List<CharacterClass> GetBaseClasses()
    {
        return GetAllClasses().Where(c => !c.IsSubclass).ToList();
    }

    /// <summary>
    /// Gets only subclasses.
    /// </summary>
    public List<CharacterClass> GetSubclasses()
    {
        return GetAllClasses().Where(c => c.IsSubclass).ToList();
    }

    /// <summary>
    /// Gets subclasses for a specific parent class.
    /// </summary>
    public List<CharacterClass> GetSubclassesForParent(string parentClassId)
    {
        return GetAllClasses()
            .Where(c => c.IsSubclass && 
                       c.ParentClassId != null && 
                       c.ParentClassId.Equals(parentClassId, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Gets a character class by name (case-insensitive).
    /// </summary>
    public CharacterClass? GetClassByName(string name)
    {
        return GetAllClasses().FirstOrDefault(c => 
            c.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
            c.DisplayName.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public CharacterClass? GetById(string id)
    {
        // First try exact ID match (e.g., "warrior:Fighter")
        var byId = GetAllClasses().FirstOrDefault(c => c.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (byId != null)
            return byId;
        
        // Fallback to name search for backwards compatibility
        return GetByName(id);
    }
    
    /// <inheritdoc />
    public CharacterClass? GetByName(string name) => GetClassByName(name);
    
    /// <inheritdoc />
    public List<CharacterClass> GetAll() => GetAllClasses();
    
    /// <inheritdoc />
    public void Add(CharacterClass entity) => throw new NotSupportedException("Character classes are predefined");
    
    /// <inheritdoc />
    public void Update(CharacterClass entity) => throw new NotSupportedException("Character classes are predefined");
    
    /// <inheritdoc />
    public void Delete(string id) => throw new NotSupportedException("Character classes are predefined");
    
    /// <inheritdoc />
    public void Dispose() 
    {
        _cachedClasses = null;
    }

    private List<CharacterClass> LoadClassesFromCatalog()
    {
        try
        {
            var cachedFile = _dataCache.GetFile("classes/catalog.json");
            if (cachedFile == null)
            {
                _logger.LogError("Failed to load classes/catalog.json from cache");
                return new List<CharacterClass>();
            }

            var catalogData = JsonSerializer.Deserialize<ClassCatalogData>(cachedFile.JsonData.ToString());
            if (catalogData == null)
            {
                _logger.LogError("Failed to deserialize classes/catalog.json");
                return new List<CharacterClass>();
            }

            var classes = new List<CharacterClass>();

            foreach (var (categoryKey, category) in catalogData.ClassTypes)
            {
                foreach (var classData in category.Items)
                {
                    var characterClass = MapToCharacterClass(classData, categoryKey, category.Metadata);
                    HydrateCharacterClass(characterClass); // Hydrate abilities and equipment
                    classes.Add(characterClass);
                }
            }

            _logger.LogInformation("Loaded and hydrated {Count} character classes from catalog", classes.Count);
            return classes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading classes from catalog");
            return new List<CharacterClass>();
        }
    }

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
            StartingAbilityIds = data.StartingAbilityIds ?? new List<string>(),
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
            characterClass.ArmorProficiency = categoryMetadata.ArmorProficiency != null 
                ? new List<string>(categoryMetadata.ArmorProficiency) 
                : new List<string>();
            characterClass.WeaponProficiency = categoryMetadata.WeaponProficiency != null 
                ? new List<string>(categoryMetadata.WeaponProficiency) 
                : new List<string>();
        }

        return characterClass;
    }

    private string? ParseParentClassReference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return null;

        // Parse reference like "@classes/cleric:Priest" to "cleric:Priest"
        if (reference.StartsWith("@classes/"))
        {
            return reference.Substring("@classes/".Length);
        }

        return reference;
    }

    /// <summary>
    /// Hydrates a character class by resolving ability and equipment references.
    /// </summary>
    private void HydrateCharacterClass(CharacterClass characterClass)
    {
        // Resolve starting abilities
        if (characterClass.StartingAbilityIds != null && characterClass.StartingAbilityIds.Any())
        {
            var abilities = new List<Ability>();
            foreach (var refId in characterClass.StartingAbilityIds)
            {
                try
                {
                    var abilityJson = _referenceResolver.ResolveToObjectAsync(refId).GetAwaiter().GetResult();
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
                        _logger.LogWarning("Could not resolve ability reference '{AbilityId}' for class '{ClassName}'", refId, characterClass.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception resolving starting ability '{AbilityId}' for class '{ClassName}'", refId, characterClass.Name);
                }
            }
            characterClass.StartingAbilities = abilities;
            
            _logger.LogDebug("Hydrated {Count} abilities for class '{ClassName}'", abilities.Count, characterClass.Name);
        }

        // Resolve starting equipment (if StartingEquipmentIds exists)
        // Note: Current catalog.json doesn't have StartingEquipmentIds, but we'll support it for future use
        if (characterClass.StartingEquipmentIds != null && characterClass.StartingEquipmentIds.Any())
        {
            var equipment = new List<Item>();
            foreach (var refId in characterClass.StartingEquipmentIds)
            {
                try
                {
                    var itemJson = _referenceResolver.ResolveToObjectAsync(refId).GetAwaiter().GetResult();
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
                        _logger.LogWarning("Could not resolve equipment reference '{ItemId}' for class '{ClassName}'", refId, characterClass.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception resolving starting equipment '{ItemId}' for class '{ClassName}'", refId, characterClass.Name);
                }
            }
            characterClass.StartingEquipment = equipment;
            
            _logger.LogDebug("Hydrated {Count} equipment items for class '{ClassName}'", equipment.Count, characterClass.Name);
        }
    }
}
