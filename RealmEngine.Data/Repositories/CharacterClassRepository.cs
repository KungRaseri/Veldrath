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
    /// Gets a character class by name (case-insensitive).
    /// </summary>
    public CharacterClass? GetClassByName(string name)
    {
        return GetAllClasses().FirstOrDefault(c => 
            c.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
            c.DisplayName.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public CharacterClass? GetById(string id) => GetByName(id); // ID is the name for character classes
    
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
                    classes.Add(characterClass);
                }
            }

            _logger.LogInformation("Loaded {Count} character classes from catalog", classes.Count);
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
}
