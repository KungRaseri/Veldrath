using MediatR;
using RealmEngine.Shared.Models;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Core.Features.Equipment.Queries;
using RealmEngine.Core.Features.Exploration.Queries;
using RealmEngine.Core.Features.Progression.Services;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.CharacterCreation.Commands;

/// <summary>
/// Handles creating a new character with full initialization (abilities, spells, equipment).
/// </summary>
public class CreateCharacterHandler : IRequestHandler<CreateCharacterCommand, CreateCharacterResult>
{
    private readonly IMediator _mediator;
    private readonly IBackgroundRepository _backgroundRepository;
    private readonly ISpeciesRepository _speciesRepository;
    private readonly SkillProgressionService _skillProgressionService;
    private readonly ILogger<CreateCharacterHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateCharacterHandler"/> class.
    /// </summary>
    /// <param name="mediator">The mediator for sending commands.</param>
    /// <param name="backgroundRepository">The repository for loading backgrounds.</param>
    /// <param name="speciesRepository">The repository for loading species.</param>
    /// <param name="skillProgressionService">The service for initializing character skills.</param>
    /// <param name="logger">The logger.</param>
    public CreateCharacterHandler(
        IMediator mediator,
        IBackgroundRepository backgroundRepository,
        ISpeciesRepository speciesRepository,
        SkillProgressionService skillProgressionService,
        ILogger<CreateCharacterHandler> logger)
    {
        _mediator                = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _backgroundRepository    = backgroundRepository ?? throw new ArgumentNullException(nameof(backgroundRepository));
        _speciesRepository       = speciesRepository ?? throw new ArgumentNullException(nameof(speciesRepository));
        _skillProgressionService = skillProgressionService ?? throw new ArgumentNullException(nameof(skillProgressionService));
        _logger                  = logger;
    }

    /// <summary>
    /// Handles the create character command and returns the result.
    /// </summary>
    /// <param name="request">The create character command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the creation result.</returns>
    public async Task<CreateCharacterResult> Handle(CreateCharacterCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Create the character with base stats from class
            var character = CreateCharacterFromClass(request.CharacterName, request.CharacterClass, request.AttributeAllocations);
            
            _logger.LogInformation("Created new character: {CharacterName} ({ClassName})", 
                request.CharacterName, request.CharacterClass.Name);

            // Apply background bonuses if provided
            Background? background = null;
            if (!string.IsNullOrWhiteSpace(request.BackgroundId))
            {
                background = await ApplyBackgroundBonuses(character, request.BackgroundId);
            }

            // Apply species bonuses if provided
            if (!string.IsNullOrWhiteSpace(request.SpeciesSlug))
            {
                await ApplySpeciesBonuses(character, request.SpeciesSlug);
            }

            // Initialize starting abilities
            var abilitiesCommand = new InitializeStartingAbilitiesCommand
            {
                Character = character,
                ClassName = request.CharacterClass.Name
            };
            
            var abilitiesResult = await _mediator.Send(abilitiesCommand, cancellationToken);
            
            if (!abilitiesResult.Success)
            {
                _logger.LogWarning("Failed to initialize starting abilities for {CharacterName}: {Message}",
                    request.CharacterName, abilitiesResult.Message);
            }

            // Initialize starting spells
            var spellsCommand = new InitializeStartingPowersCommand
            {
                Character = character,
                ClassName = request.CharacterClass.Name
            };
            
            var spellsResult = await _mediator.Send(spellsCommand, cancellationToken);
            
            if (!spellsResult.Success)
            {
                _logger.LogWarning("Failed to initialize starting spells for {CharacterName}: {Message}",
                    request.CharacterName, spellsResult.Message);
            }

            // Select and equip starting equipment
            var equipment = await SelectStartingEquipment(
                character, 
                request.CharacterClass.Slug,
                request.PreferredArmorType,
                request.PreferredWeaponType,
                request.IncludeShield,
                cancellationToken);

            // Initialize all skills at rank 0
            _skillProgressionService.InitializeAllSkills(character);

            // Assign starting location if provided
            Location? location = null;
            if (!string.IsNullOrWhiteSpace(request.StartingLocationId))
            {
                location = await AssignStartingLocation(character, request.StartingLocationId, cancellationToken);
            }

            _logger.LogInformation("Character creation complete: {CharacterName} with {AbilityCount} abilities, {SpellCount} spells, {EquipmentCount} equipment items",
                request.CharacterName, abilitiesResult.AbilitiesLearned, spellsResult.PowersLearned, equipment.Count);

            return new CreateCharacterResult
            {
                Character = character,
                Success = true,
                Message = $"Character {request.CharacterName} created successfully",
                AbilitiesLearned = abilitiesResult.AbilitiesLearned,
                SpellsLearned = spellsResult.PowersLearned,
                EquipmentSelected = equipment,
                StartingLocation = location,
                BackgroundApplied = background
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating character {CharacterName}", request.CharacterName);
            return new CreateCharacterResult
            {
                Character = null,
                Success = false,
                Message = $"Failed to create character: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Creates a new character object with base stats from the character class and optional point-buy allocations.
    /// </summary>
    private Character CreateCharacterFromClass(string name, CharacterClass characterClass, Dictionary<string, int>? allocations = null)
    {
        int Base(string stat) => allocations?.GetValueOrDefault(stat, 10) ?? 10;

        var character = new Character
        {
            Name = name,
            ClassName = characterClass.Name,
            Level = 1,
            Experience = 0,

            // Point-buy base + class bonus
            Strength     = Base("Strength")     + characterClass.BonusStrength,
            Dexterity    = Base("Dexterity")    + characterClass.BonusDexterity,
            Constitution = Base("Constitution") + characterClass.BonusConstitution,
            Intelligence = Base("Intelligence") + characterClass.BonusIntelligence,
            Wisdom       = Base("Wisdom")       + characterClass.BonusWisdom,
            Charisma     = Base("Charisma")     + characterClass.BonusCharisma,

            // Set starting resources from class
            Health    = characterClass.StartingHealth,
            MaxHealth = characterClass.StartingHealth,
            Mana      = characterClass.StartingMana,
            MaxMana   = characterClass.StartingMana,

            // Initialize collections
            Inventory        = new List<Item>(),
            LearnedAbilities = new Dictionary<string, CharacterAbility>(),
            LearnedSpells    = new Dictionary<string, CharacterSpell>(),
            Skills           = new Dictionary<string, CharacterSkill>(),
            PendingLevelUps  = new List<LevelUpInfo>()
        };

        return character;
    }

    /// <summary>
    /// Applies species stat bonuses to the character.
    /// </summary>
    private async Task ApplySpeciesBonuses(Character character, string speciesSlug)
    {
        try
        {
            var species = await _speciesRepository.GetSpeciesBySlugAsync(speciesSlug);
            if (species is null)
            {
                _logger.LogWarning("Species not found: {SpeciesSlug}", speciesSlug);
                return;
            }

            species.ApplyBonuses(character);
            character.SpeciesSlug = species.Slug;
            _logger.LogInformation("Applied species {SpeciesName} bonuses to {CharacterName}",
                species.DisplayName, character.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying species {SpeciesSlug} to {CharacterName}", speciesSlug, character.Name);
        }
    }

    /// <summary>
    /// Applies background bonuses to the character's attributes.
    /// </summary>
    private async Task<Background?> ApplyBackgroundBonuses(Character character, string backgroundId)
    {
        try
        {
            var background = await _backgroundRepository.GetBackgroundByIdAsync(backgroundId);
            if (background == null)
            {
                _logger.LogWarning("Background not found: {BackgroundId}", backgroundId);
                return null;
            }

            background.ApplyBonuses(character);
            character.BackgroundId = background.GetBackgroundId();
            
            _logger.LogInformation("Applied background {BackgroundName} to {CharacterName} ({PrimaryAttr}+{PrimaryBonus}, {SecondaryAttr}+{SecondaryBonus})",
                background.Name, character.Name, 
                background.PrimaryAttribute, background.PrimaryBonus,
                background.SecondaryAttribute, background.SecondaryBonus);

            return background;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying background {BackgroundId} to {CharacterName}", backgroundId, character.Name);
            return null;
        }
    }

    /// <summary>
    /// Selects and equips starting equipment based on class proficiencies and preferences.
    /// </summary>
    private async Task<List<Item>> SelectStartingEquipment(
        Character character,
        string classSlug,
        string? preferredArmorType,
        string? preferredWeaponType,
        bool includeShield,
        CancellationToken cancellationToken)
    {
        var selectedEquipment = new List<Item>();

        try
        {
            // Query equipment for this class using its slug directly
            var equipmentQuery = new GetEquipmentForClassQuery
            {
                ClassId = classSlug,
                MaxItemsPerCategory = 5,
                RandomizeSelection = true
            };

            var equipmentResult = await _mediator.Send(equipmentQuery, cancellationToken);

            if (!equipmentResult.Success || equipmentResult.Weapons.Count == 0)
            {
                _logger.LogWarning("No equipment found for class {ClassSlug}", classSlug);
                return selectedEquipment;
            }

            // Select one weapon (prefer specified type if provided)
            var weapon = preferredWeaponType != null
                ? equipmentResult.Weapons.FirstOrDefault(w => 
                    w.WeaponType?.Contains(preferredWeaponType, StringComparison.OrdinalIgnoreCase) == true)
                  ?? equipmentResult.Weapons.First()
                : equipmentResult.Weapons.First();

            character.EquippedMainHand = weapon;
            character.Inventory.Add(weapon);
            selectedEquipment.Add(weapon);
            _logger.LogInformation("Equipped weapon: {WeaponName} ({WeaponType})", weapon.Name, weapon.WeaponType);

            // Select armor if available (prefer specified type if provided)
            if (equipmentResult.Armor.Count > 0)
            {
                var armor = preferredArmorType != null
                    ? equipmentResult.Armor.FirstOrDefault(a => 
                        a.ArmorType?.Contains(preferredArmorType, StringComparison.OrdinalIgnoreCase) == true)
                      ?? equipmentResult.Armor.First()
                    : equipmentResult.Armor.First();

                // Equip armor to appropriate slot based on armor type
                EquipArmorToSlot(character, armor);
                character.Inventory.Add(armor);
                selectedEquipment.Add(armor);
                _logger.LogInformation("Equipped armor: {ArmorName} ({ArmorType})", armor.Name, armor.ArmorType);
            }

            // Optionally equip a shield if requested
            if (includeShield)
            {
                var shield = equipmentResult.Armor.FirstOrDefault(a => 
                    a.ArmorType?.Contains("shield", StringComparison.OrdinalIgnoreCase) == true);
                
                if (shield != null && character.EquippedOffHand == null)
                {
                    character.EquippedOffHand = shield;
                    character.Inventory.Add(shield);
                    selectedEquipment.Add(shield);
                    _logger.LogInformation("Equipped shield: {ShieldName}", shield.Name);
                }
            }

            return selectedEquipment;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error selecting starting equipment for {CharacterName}", character.Name);
            return selectedEquipment;
        }
    }

    /// <summary>
    /// Assigns a starting location to the character.
    /// </summary>
    private async Task<Location?> AssignStartingLocation(
        Character character,
        string locationId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Query all starting locations
            var locationsQuery = new GetStartingLocationsQuery(BackgroundId: null, FilterByRecommended: false);
            var locations = await _mediator.Send(locationsQuery, cancellationToken);

            // Find the requested location
            var location = locations.FirstOrDefault(l => 
                l.Id?.Equals(locationId, StringComparison.OrdinalIgnoreCase) == true ||
                l.Name?.Equals(locationId, StringComparison.OrdinalIgnoreCase) == true);

            if (location == null)
            {
                _logger.LogWarning("Starting location not found: {LocationId}", locationId);
                return null;
            }

            character.CurrentLocationId = location.Id;
            character.CurrentZone = location.Name;
            
            _logger.LogInformation("Assigned starting location to {CharacterName}: {LocationName}", 
                character.Name, location.Name);

            return location;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning starting location {LocationId} to {CharacterName}", 
                locationId, character.Name);
            return null;
        }
    }

    /// <summary>
    /// Equips armor to the appropriate equipment slot based on its armor type.
    /// </summary>
    private void EquipArmorToSlot(Character character, Item armor)
    {
        var armorType = armor.ArmorType?.ToLowerInvariant() ?? "";

        if (armorType.Contains("helm") || armorType.Contains("hat") || armorType.Contains("hood"))
            character.EquippedHelmet = armor;
        else if (armorType.Contains("chest") || armorType.Contains("robe") || armorType.Contains("breastplate"))
            character.EquippedChest = armor;
        else if (armorType.Contains("legs") || armorType.Contains("pants") || armorType.Contains("greaves"))
            character.EquippedLegs = armor;
        else if (armorType.Contains("boots") || armorType.Contains("shoes"))
            character.EquippedBoots = armor;
        else if (armorType.Contains("gloves") || armorType.Contains("gauntlets"))
            character.EquippedGloves = armor;
        else if (armorType.Contains("shoulder") || armorType.Contains("pauldrons"))
            character.EquippedShoulders = armor;
        else if (armorType.Contains("belt") || armorType.Contains("girdle"))
            character.EquippedBelt = armor;
        else if (armorType.Contains("bracers") || armorType.Contains("wrist"))
            character.EquippedBracers = armor;
        else
            character.EquippedChest = armor; // Default to chest if unknown
    }

    /// <summary>
    /// Converts a class name to a class ID format.
    /// </summary>
    private string GetClassIdFromName(string className)
    {
        // Map common class names to their IDs
        // This is a simplified approach - in production you'd query the class catalog
        var classNameLower = className.ToLowerInvariant();
        
        return classNameLower switch
        {
            "fighter" => "warriors:fighter",
            "barbarian" => "warriors:barbarian",
            "paladin" => "warriors:paladin",
            "ranger" => "warriors:ranger",
            "rogue" => "rogues:rogue",
            "monk" => "rogues:monk",
            "bard" => "rogues:bard",
            "cleric" => "clerics:cleric",
            "druid" => "clerics:druid",
            "priest" => "clerics:priest",
            "wizard" => "mages:wizard",
            "sorcerer" => "mages:sorcerer",
            "warlock" => "mages:warlock",
            _ => $"warriors:{classNameLower}" // Default fallback
        };
    }
}
