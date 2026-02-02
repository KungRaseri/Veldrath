# Character Creation Expansion - Implementation Plan

**Date:** February 2, 2026  
**Status:** Planning Phase  
**Target:** Character creation system with backgrounds, equipment selection, location choice, and difficulty integration

---

## Overview

Expanding the character creation system to include:
1. **Backgrounds** - Origin stories with +2/+1 attribute bonuses
2. **Equipment Selection** - Player choice of armor/weapon types by class proficiencies
3. **Starting Location** - Background-recommended locations with override option
4. **Difficulty Scaling** - Budget and modifier adjustments based on difficulty
5. **Enhanced CreateCharacterCommand** - Orchestrates all systems

---

## Phase 1: Backgrounds System

### 1.1 Create Background Data Files

**File:** `RealmEngine.Data/Data/Json/backgrounds/catalog.json`

```json
{
  "metadata": {
    "description": "Character backgrounds defining origin stories and attribute bonuses",
    "version": "5.1",
    "lastUpdated": "2026-02-02",
    "type": "background_catalog",
    "totalBackgroundTypes": 6,
    "totalBackgrounds": 12,
    "notes": [
      "Backgrounds provide attribute bonuses and recommended starting locations",
      "Primary attribute gets +2, secondary attribute gets +1",
      "Location types reference @locations/towns:*, @locations/wilderness:*, @locations/dungeons:*"
    ]
  },
  "background_types": {
    "strength": {
      "items": [
        {
          "slug": "soldier",
          "name": "Soldier",
          "rarityWeight": 50,
          "description": "Trained warrior who served in an army or mercenary company.",
          "primaryAttribute": "strength",
          "primaryBonus": 2,
          "secondaryAttribute": "constitution",
          "secondaryBonus": 1,
          "recommendedLocationTypes": ["settlement", "wilderness", "dungeon"]
        },
        {
          "slug": "laborer",
          "name": "Laborer",
          "rarityWeight": 50,
          "description": "Years of hard physical work built your strength and endurance.",
          "primaryAttribute": "strength",
          "primaryBonus": 2,
          "secondaryAttribute": "constitution",
          "secondaryBonus": 1,
          "recommendedLocationTypes": ["settlement", "wilderness", "settlement"]
        }
      ]
    },
    "dexterity": {
      "items": [
        {
          "slug": "criminal",
          "name": "Criminal",
          "rarityWeight": 50,
          "description": "Thief, smuggler, or street enforcer who survived in the shadows.",
          "primaryAttribute": "dexterity",
          "primaryBonus": 2,
          "secondaryAttribute": "charisma",
          "secondaryBonus": 1,
          "recommendedLocationTypes": ["settlement", "dungeon", "wilderness"]
        },
        {
          "slug": "entertainer",
          "name": "Entertainer",
          "rarityWeight": 50,
          "description": "Acrobat, dancer, or performer who captivates audiences.",
          "primaryAttribute": "dexterity",
          "primaryBonus": 2,
          "secondaryAttribute": "charisma",
          "secondaryBonus": 1,
          "recommendedLocationTypes": ["settlement", "settlement", "wilderness"]
        }
      ]
    },
    "constitution": {
      "items": [
        {
          "slug": "folk-hero",
          "name": "Folk Hero",
          "rarityWeight": 50,
          "description": "You stood up to oppression and became a local legend.",
          "primaryAttribute": "constitution",
          "primaryBonus": 2,
          "secondaryAttribute": "strength",
          "secondaryBonus": 1,
          "recommendedLocationTypes": ["settlement", "wilderness", "wilderness"]
        },
        {
          "slug": "outlander",
          "name": "Outlander",
          "rarityWeight": 50,
          "description": "You grew up in the wilderness, far from civilization.",
          "primaryAttribute": "constitution",
          "primaryBonus": 2,
          "secondaryAttribute": "wisdom",
          "secondaryBonus": 1,
          "recommendedLocationTypes": ["wilderness", "wilderness", "settlement"]
        }
      ]
    },
    "intelligence": {
      "items": [
        {
          "slug": "scholar",
          "name": "Scholar",
          "rarityWeight": 50,
          "description": "You spent years studying ancient texts and arcane lore.",
          "primaryAttribute": "intelligence",
          "primaryBonus": 2,
          "secondaryAttribute": "wisdom",
          "secondaryBonus": 1,
          "recommendedLocationTypes": ["settlement", "dungeon", "wilderness"]
        },
        {
          "slug": "sage",
          "name": "Sage",
          "rarityWeight": 50,
          "description": "Researcher, librarian, or academic devoted to knowledge.",
          "primaryAttribute": "intelligence",
          "primaryBonus": 2,
          "secondaryAttribute": "wisdom",
          "secondaryBonus": 1,
          "recommendedLocationTypes": ["settlement", "dungeon", "settlement"]
        }
      ]
    },
    "wisdom": {
      "items": [
        {
          "slug": "acolyte",
          "name": "Acolyte",
          "rarityWeight": 50,
          "description": "Raised in a temple, trained in religious rites and healing.",
          "primaryAttribute": "wisdom",
          "primaryBonus": 2,
          "secondaryAttribute": "charisma",
          "secondaryBonus": 1,
          "recommendedLocationTypes": ["settlement", "wilderness", "dungeon"]
        },
        {
          "slug": "hermit",
          "name": "Hermit",
          "rarityWeight": 50,
          "description": "You lived in isolation, seeking enlightenment and inner truth.",
          "primaryAttribute": "wisdom",
          "primaryBonus": 2,
          "secondaryAttribute": "intelligence",
          "secondaryBonus": 1,
          "recommendedLocationTypes": ["wilderness", "dungeon", "wilderness"]
        }
      ]
    },
    "charisma": {
      "items": [
        {
          "slug": "noble",
          "name": "Noble",
          "rarityWeight": 50,
          "description": "Born to wealth and privilege, trained in etiquette and politics.",
          "primaryAttribute": "charisma",
          "primaryBonus": 2,
          "secondaryAttribute": "intelligence",
          "secondaryBonus": 1,
          "recommendedLocationTypes": ["settlement", "settlement", "wilderness"]
        },
        {
          "slug": "charlatan",
          "name": "Charlatan",
          "rarityWeight": 50,
          "description": "Con artist, swindler, or smooth-talker who lives by wit.",
          "primaryAttribute": "charisma",
          "primaryBonus": 2,
          "secondaryAttribute": "dexterity",
          "secondaryBonus": 1,
          "recommendedLocationTypes": ["settlement", "wilderness", "dungeon"]
        }
      ]
    }
  }
}
```

**File:** `RealmEngine.Data/Data/Json/backgrounds/.cbconfig.json`

```json
{
  "icon": "AccountOutline",
  "sortOrder": 50,
  "category": "Character",
  "displayName": "Backgrounds"
}
```

### 1.2 Create Background Model

**File:** `RealmEngine.Shared/Models/Background.cs`

```csharp
using Newtonsoft.Json;

namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents a character background providing origin story and attribute bonuses
/// </summary>
public class Background
{
    [JsonProperty("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("rarityWeight")]
    public int RarityWeight { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("primaryAttribute")]
    public string PrimaryAttribute { get; set; } = string.Empty;

    [JsonProperty("primaryBonus")]
    public int PrimaryBonus { get; set; }

    [JsonProperty("secondaryAttribute")]
    public string SecondaryAttribute { get; set; } = string.Empty;

    [JsonProperty("secondaryBonus")]
    public int SecondaryBonus { get; set; }

    [JsonProperty("recommendedLocationTypes")]
    public List<string> RecommendedLocationTypes { get; set; } = new();

    /// <summary>
    /// Gets the full background ID in format "backgrounds/[type]:[slug]"
    /// </summary>
    public string GetBackgroundId()
    {
        var category = PrimaryAttribute.ToLowerInvariant();
        return $"backgrounds/{category}:{Slug}";
    }

    /// <summary>
    /// Apply attribute bonuses to a character's base stats
    /// </summary>
    public void ApplyBonuses(Character character)
    {
        ApplyAttributeBonus(character, PrimaryAttribute, PrimaryBonus);
        ApplyAttributeBonus(character, SecondaryAttribute, SecondaryBonus);
    }

    private void ApplyAttributeBonus(Character character, string attribute, int bonus)
    {
        switch (attribute.ToLowerInvariant())
        {
            case "strength":
                character.Strength += bonus;
                break;
            case "dexterity":
                character.Dexterity += bonus;
                break;
            case "constitution":
                character.Constitution += bonus;
                break;
            case "intelligence":
                character.Intelligence += bonus;
                break;
            case "wisdom":
                character.Wisdom += bonus;
                break;
            case "charisma":
                character.Charisma += bonus;
                break;
        }
    }
}
```

### 1.3 Create Background Repository

**File:** `RealmEngine.Data/Repositories/BackgroundRepository.cs`

```csharp
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

public interface IBackgroundRepository
{
    Task<List<Background>> GetAllBackgroundsAsync();
    Task<Background?> GetBackgroundByIdAsync(string backgroundId);
    Task<List<Background>> GetBackgroundsByAttributeAsync(string attribute);
}

public class BackgroundRepository : IBackgroundRepository
{
    private readonly IGameDataCache _cache;
    private readonly ILogger<BackgroundRepository> _logger;
    private List<Background>? _cachedBackgrounds;

    public BackgroundRepository(IGameDataCache cache, ILogger<BackgroundRepository> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<Background>> GetAllBackgroundsAsync()
    {
        if (_cachedBackgrounds != null)
            return _cachedBackgrounds;

        _cachedBackgrounds = await LoadBackgroundsFromCatalogAsync();
        return _cachedBackgrounds;
    }

    public async Task<Background?> GetBackgroundByIdAsync(string backgroundId)
    {
        var backgrounds = await GetAllBackgroundsAsync();
        return backgrounds.FirstOrDefault(b => 
            b.GetBackgroundId().Equals(backgroundId, StringComparison.OrdinalIgnoreCase) ||
            b.Slug.Equals(backgroundId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<List<Background>> GetBackgroundsByAttributeAsync(string attribute)
    {
        var backgrounds = await GetAllBackgroundsAsync();
        return backgrounds
            .Where(b => b.PrimaryAttribute.Equals(attribute, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private async Task<List<Background>> LoadBackgroundsFromCatalogAsync()
    {
        var backgrounds = new List<Background>();

        try
        {
            var catalogPath = "backgrounds/catalog.json";
            var catalogData = await _cache.GetJsonDataAsync(catalogPath);
            
            if (catalogData == null)
            {
                _logger.LogWarning("Background catalog not found at {Path}", catalogPath);
                return backgrounds;
            }

            var backgroundTypes = catalogData["background_types"] as JObject;
            if (backgroundTypes == null)
            {
                _logger.LogWarning("No background_types found in catalog");
                return backgrounds;
            }

            foreach (var typeProperty in backgroundTypes.Properties())
            {
                var items = typeProperty.Value["items"] as JArray;
                if (items == null) continue;

                foreach (var item in items)
                {
                    var background = item.ToObject<Background>();
                    if (background != null)
                    {
                        backgrounds.Add(background);
                    }
                }
            }

            _logger.LogInformation("Loaded {Count} backgrounds from catalog", backgrounds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading backgrounds from catalog");
        }

        return backgrounds;
    }
}
```

### 1.4 Create Background Queries

**File:** `RealmEngine.Core/Features/Characters/Queries/GetBackgroundsQuery.cs`

```csharp
using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Characters.Queries;

/// <summary>
/// Query to retrieve all available character backgrounds
/// </summary>
public record GetBackgroundsQuery(string? FilterByAttribute = null) : IRequest<List<Background>>;
```

**File:** `RealmEngine.Core/Features/Characters/Queries/GetBackgroundsHandler.cs`

```csharp
using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Repositories;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Characters.Queries;

public class GetBackgroundsHandler : IRequestHandler<GetBackgroundsQuery, List<Background>>
{
    private readonly IBackgroundRepository _repository;
    private readonly ILogger<GetBackgroundsHandler> _logger;

    public GetBackgroundsHandler(IBackgroundRepository repository, ILogger<GetBackgroundsHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<List<Background>> Handle(GetBackgroundsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving backgrounds (Filter: {Filter})", request.FilterByAttribute ?? "None");

        if (!string.IsNullOrWhiteSpace(request.FilterByAttribute))
        {
            return await _repository.GetBackgroundsByAttributeAsync(request.FilterByAttribute);
        }

        return await _repository.GetAllBackgroundsAsync();
    }
}
```

**File:** `RealmEngine.Core/Features/Characters/Queries/GetBackgroundQuery.cs`

```csharp
using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Characters.Queries;

/// <summary>
/// Query to retrieve a specific background by ID or slug
/// </summary>
public record GetBackgroundQuery(string BackgroundId) : IRequest<Background?>;
```

**File:** `RealmEngine.Core/Features/Characters/Queries/GetBackgroundHandler.cs`

```csharp
using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Repositories;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Characters.Queries;

public class GetBackgroundHandler : IRequestHandler<GetBackgroundQuery, Background?>
{
    private readonly IBackgroundRepository _repository;
    private readonly ILogger<GetBackgroundHandler> _logger;

    public GetBackgroundHandler(IBackgroundRepository repository, ILogger<GetBackgroundHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Background?> Handle(GetBackgroundQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving background: {BackgroundId}", request.BackgroundId);
        return await _repository.GetBackgroundByIdAsync(request.BackgroundId);
    }
}
```

### 1.5 Register Services

**File:** `RealmEngine.Data/ServiceCollectionExtensions.cs` (UPDATE)

Add to `AddRealmEngineData` method:
```csharp
services.AddSingleton<IBackgroundRepository, BackgroundRepository>();
```

### 1.6 Create Unit Tests

**File:** `RealmEngine.Core.Tests/Features/Characters/Queries/GetBackgroundsHandlerTests.cs`

```csharp
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using RealmEngine.Core.Features.Characters.Queries;
using RealmEngine.Core.Tests.Fixtures;
using Xunit;

namespace RealmEngine.Core.Tests.Features.Characters.Queries;

public class GetBackgroundsHandlerTests : IClassFixture<ServiceProviderFixture>
{
    private readonly IMediator _mediator;

    public GetBackgroundsHandlerTests(ServiceProviderFixture fixture)
    {
        _mediator = fixture.ServiceProvider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Handle_ShouldReturnAllBackgrounds()
    {
        // Arrange
        var query = new GetBackgroundsQuery();

        // Act
        var result = await _mediator.Send(query);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(12);
    }

    [Theory]
    [InlineData("strength", 2)]
    [InlineData("dexterity", 2)]
    [InlineData("constitution", 2)]
    [InlineData("intelligence", 2)]
    [InlineData("wisdom", 2)]
    [InlineData("charisma", 2)]
    public async Task Handle_WithAttributeFilter_ShouldReturnFilteredBackgrounds(string attribute, int expectedCount)
    {
        // Arrange
        var query = new GetBackgroundsQuery(attribute);

        // Act
        var result = await _mediator.Send(query);

        // Assert
        result.Should().HaveCount(expectedCount);
        result.Should().AllSatisfy(b => b.PrimaryAttribute.Should().Be(attribute));
    }

    [Fact]
    public async Task Handle_ShouldReturnBackgroundsWithCorrectBonuses()
    {
        // Arrange
        var query = new GetBackgroundsQuery();

        // Act
        var result = await _mediator.Send(query);

        // Assert
        result.Should().AllSatisfy(b =>
        {
            b.PrimaryBonus.Should().Be(2);
            b.SecondaryBonus.Should().Be(1);
            b.PrimaryAttribute.Should().NotBeNullOrEmpty();
            b.SecondaryAttribute.Should().NotBeNullOrEmpty();
        });
    }

    [Fact]
    public async Task Handle_ShouldReturnBackgroundsWithLocationRecommendations()
    {
        // Arrange
        var query = new GetBackgroundsQuery();

        // Act
        var result = await _mediator.Send(query);

        // Assert
        result.Should().AllSatisfy(b =>
        {
            b.RecommendedLocationTypes.Should().HaveCount(3);
            b.RecommendedLocationTypes.Should().Contain(type => 
                type == "settlement" || type == "wilderness" || type == "dungeon");
        });
    }

    [Theory]
    [InlineData("soldier")]
    [InlineData("criminal")]
    [InlineData("scholar")]
    [InlineData("noble")]
    public async Task GetBackground_BySlug_ShouldReturnCorrectBackground(string slug)
    {
        // Arrange
        var query = new GetBackgroundQuery(slug);

        // Act
        var result = await _mediator.Send(query);

        // Assert
        result.Should().NotBeNull();
        result!.Slug.Should().Be(slug);
    }
}
```

---

## Phase 2: Starting Location Selection

### 2.1 Update Location Model

**File:** `RealmEngine.Shared/Models/Location.cs` (UPDATE)

Add property:
```csharp
[JsonProperty("isStartingZone")]
public bool IsStartingZone { get; set; } = false;
```

### 2.2 Create Starting Locations Query

**File:** `RealmEngine.Core/Features/Exploration/Queries/GetStartingLocationsQuery.cs`

```csharp
using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Exploration.Queries;

/// <summary>
/// Query to retrieve starting locations, optionally filtered by background recommendations
/// </summary>
public record GetStartingLocationsQuery(
    string? BackgroundId = null,
    bool FilterByRecommended = true
) : IRequest<List<Location>>;
```

**File:** `RealmEngine.Core/Features/Exploration/Queries/GetStartingLocationsHandler.cs`

```csharp
using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Repositories;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Exploration.Queries;

public class GetStartingLocationsHandler : IRequestHandler<GetStartingLocationsQuery, List<Location>>
{
    private readonly ILocationRepository _locationRepository;
    private readonly IBackgroundRepository _backgroundRepository;
    private readonly ILogger<GetStartingLocationsHandler> _logger;

    public GetStartingLocationsHandler(
        ILocationRepository locationRepository,
        IBackgroundRepository backgroundRepository,
        ILogger<GetStartingLocationsHandler> logger)
    {
        _locationRepository = locationRepository;
        _backgroundRepository = backgroundRepository;
        _logger = logger;
    }

    public async Task<List<Location>> Handle(GetStartingLocationsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving starting locations (Background: {BgId}, Filter: {Filter})", 
            request.BackgroundId ?? "None", request.FilterByRecommended);

        // Get all locations marked as starting zones
        var allLocations = await _locationRepository.GetAllLocationsAsync();
        var startingLocations = allLocations
            .Where(l => l.IsStartingZone || l.Danger == "Low" || l.Difficulty == "Easy")
            .ToList();

        // If no background or filtering disabled, return all safe locations
        if (string.IsNullOrWhiteSpace(request.BackgroundId) || !request.FilterByRecommended)
        {
            return startingLocations;
        }

        // Filter by background recommendations
        var background = await _backgroundRepository.GetBackgroundByIdAsync(request.BackgroundId);
        if (background == null)
        {
            _logger.LogWarning("Background not found: {BackgroundId}", request.BackgroundId);
            return startingLocations;
        }

        var recommendedTypes = background.RecommendedLocationTypes;
        var filteredLocations = startingLocations
            .Where(l => recommendedTypes.Contains(l.LocationType, StringComparer.OrdinalIgnoreCase))
            .ToList();

        _logger.LogInformation("Filtered to {Count} recommended locations for background {Background}", 
            filteredLocations.Count, background.Name);

        return filteredLocations;
    }
}
```

### 2.3 Update Location Repository

**File:** `RealmEngine.Data/Repositories/LocationRepository.cs` (UPDATE)

Add method:
```csharp
public async Task<List<Location>> GetStartingLocationsAsync()
{
    var allLocations = await GetAllLocationsAsync();
    return allLocations
        .Where(l => l.IsStartingZone || l.Danger == "Low" || l.Difficulty == "Easy")
        .ToList();
}
```

### 2.4 Update Location Catalog Data

**Files to Update:**
- `world/locations/towns/catalog.json`
- `world/locations/wilderness/catalog.json`
- `world/locations/dungeons/catalog.json`

Add `"isStartingZone": true` to appropriate low-danger locations:
- Outpost "Crossroads"
- Villages "Riverside", "Oakshire"
- Towns (2-3 entries)
- Wilderness low-danger (3-5 entries)

### 2.5 Create Unit Tests

**File:** `RealmEngine.Core.Tests/Features/Exploration/Queries/GetStartingLocationsHandlerTests.cs`

---

## Phase 3: Equipment Selection System

### 3.1 Update Class Model with Proficiencies

**File:** `RealmEngine.Shared/Models/CharacterClass.cs` (UPDATE)

Add properties (map from catalog metadata):
```csharp
[JsonProperty("armorProficiency")]
public List<string> ArmorProficiency { get; set; } = new();

[JsonProperty("weaponProficiency")]
public List<string> WeaponProficiency { get; set; } = new();
```

### 3.2 Update CharacterClassRepository

**File:** `RealmEngine.Data/Repositories/CharacterClassRepository.cs` (UPDATE)

In `MapToCharacterClass` method, add:
```csharp
// Map proficiencies from metadata
var metadata = typeValue["metadata"];
if (metadata != null)
{
    characterClass.ArmorProficiency = metadata["armorProficiency"]?.ToObject<List<string>>() ?? new();
    characterClass.WeaponProficiency = metadata["weaponProficiency"]?.ToObject<List<string>>() ?? new();
}
```

### 3.3 Add Weapon/Armor Type Fields to Item Models

**File:** `RealmEngine.Shared/Models/Item.cs` (UPDATE)

Add properties:
```csharp
[JsonProperty("weaponType")]
public string? WeaponType { get; set; }

[JsonProperty("armorType")]
public string? ArmorType { get; set; }
```

### 3.4 Create Equipment Selection Queries

**File:** `RealmEngine.Core/Features/Items/Queries/GetWeaponsByTypeQuery.cs`

```csharp
using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Items.Queries;

/// <summary>
/// Query to retrieve weapons of a specific type
/// </summary>
public record GetWeaponsByTypeQuery(
    string WeaponType,
    string? ClassId = null,
    bool RandomizeSelection = false
) : IRequest<List<Item>>;
```

**File:** `RealmEngine.Core/Features/Items/Queries/GetArmorByTypeQuery.cs`

```csharp
using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Items.Queries;

/// <summary>
/// Query to retrieve armor of a specific type
/// </summary>
public record GetArmorByTypeQuery(
    string ArmorType,
    string? ClassId = null,
    bool RandomizeSelection = false
) : IRequest<List<Item>>;
```

### 3.5 Create Handlers

Implement handlers that:
1. Load items from catalog by type (using key structure: `weapons/swords`, `armor/heavy`)
2. Filter by class proficiencies if ClassId provided
3. Return random item from type if RandomizeSelection = true

### 3.6 Update Item Catalog Data

**Files to Update:**
- Verify weapon types exist in keys: `swords`, `axes`, `maces`, `polearms`, `daggers`, `bows`
- Verify armor types exist in keys: `heavy`, `medium`, `light`, `shields`
- No JSON changes needed (types already inferred from keys)

---

## Phase 4: Difficulty Integration

### 4.1 Create Difficulty Preset Catalog

**File:** `RealmEngine.Data/Data/Json/difficulty/catalog.json`

```json
{
  "metadata": {
    "description": "Difficulty presets affecting combat, economy, progression, and death penalties",
    "version": "5.1",
    "lastUpdated": "2026-02-02",
    "type": "difficulty_catalog",
    "totalDifficulties": 4
  },
  "difficulty_types": {
    "standard": {
      "items": [
        {
          "slug": "story",
          "name": "Story Mode",
          "rarityWeight": 50,
          "description": "Relaxed difficulty for narrative focus",
          "combatModifiers": {
            "playerDamageMultiplier": 1.5,
            "enemyDamageMultiplier": 0.75,
            "enemyHealthMultiplier": 0.8
          },
          "economicModifiers": {
            "shopPriceMultiplier": 0.8,
            "lootGoldMultiplier": 1.5,
            "startingBonusBudgetMultiplier": 2.0
          },
          "progressionModifiers": {
            "experienceMultiplier": 1.25,
            "skillPointMultiplier": 1.0
          },
          "deathPenalties": {
            "loseGoldPercentage": 0,
            "loseExperiencePercentage": 0,
            "respawnAtCheckpoint": true,
            "permadeath": false
          }
        },
        {
          "slug": "normal",
          "name": "Normal",
          "rarityWeight": 50,
          "description": "Balanced difficulty for standard play",
          "combatModifiers": {
            "playerDamageMultiplier": 1.0,
            "enemyDamageMultiplier": 1.0,
            "enemyHealthMultiplier": 1.0
          },
          "economicModifiers": {
            "shopPriceMultiplier": 1.0,
            "lootGoldMultiplier": 1.0,
            "startingBonusBudgetMultiplier": 1.0
          },
          "progressionModifiers": {
            "experienceMultiplier": 1.0,
            "skillPointMultiplier": 1.0
          },
          "deathPenalties": {
            "loseGoldPercentage": 10,
            "loseExperiencePercentage": 0,
            "respawnAtCheckpoint": true,
            "permadeath": false
          }
        },
        {
          "slug": "hard",
          "name": "Hard",
          "rarityWeight": 30,
          "description": "Challenging difficulty for experienced players",
          "combatModifiers": {
            "playerDamageMultiplier": 0.9,
            "enemyDamageMultiplier": 1.25,
            "enemyHealthMultiplier": 1.3
          },
          "economicModifiers": {
            "shopPriceMultiplier": 1.25,
            "lootGoldMultiplier": 0.75,
            "startingBonusBudgetMultiplier": 0.5
          },
          "progressionModifiers": {
            "experienceMultiplier": 1.0,
            "skillPointMultiplier": 0.9
          },
          "deathPenalties": {
            "loseGoldPercentage": 25,
            "loseExperiencePercentage": 10,
            "respawnAtCheckpoint": true,
            "permadeath": false
          }
        },
        {
          "slug": "ironman",
          "name": "Ironman",
          "rarityWeight": 10,
          "description": "Extreme difficulty with permadeath",
          "combatModifiers": {
            "playerDamageMultiplier": 0.8,
            "enemyDamageMultiplier": 1.5,
            "enemyHealthMultiplier": 1.5
          },
          "economicModifiers": {
            "shopPriceMultiplier": 1.5,
            "lootGoldMultiplier": 0.5,
            "startingBonusBudgetMultiplier": 0.25
          },
          "progressionModifiers": {
            "experienceMultiplier": 0.9,
            "skillPointMultiplier": 0.8
          },
          "deathPenalties": {
            "loseGoldPercentage": 100,
            "loseExperiencePercentage": 100,
            "respawnAtCheckpoint": false,
            "permadeath": true
          }
        }
      ]
    }
  }
}
```

**File:** `RealmEngine.Data/Data/Json/difficulty/.cbconfig.json`

```json
{
  "icon": "GaugeOutline",
  "sortOrder": 60,
  "category": "Game",
  "displayName": "Difficulty"
}
```

### 4.2 Create Difficulty Model

**File:** `RealmEngine.Shared/Models/Difficulty.cs`

```csharp
using Newtonsoft.Json;

namespace RealmEngine.Shared.Models;

public class Difficulty
{
    [JsonProperty("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("rarityWeight")]
    public int RarityWeight { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("combatModifiers")]
    public CombatModifiers CombatModifiers { get; set; } = new();

    [JsonProperty("economicModifiers")]
    public EconomicModifiers EconomicModifiers { get; set; } = new();

    [JsonProperty("progressionModifiers")]
    public ProgressionModifiers ProgressionModifiers { get; set; } = new();

    [JsonProperty("deathPenalties")]
    public DeathPenalties DeathPenalties { get; set; } = new();
}

public class CombatModifiers
{
    [JsonProperty("playerDamageMultiplier")]
    public double PlayerDamageMultiplier { get; set; } = 1.0;

    [JsonProperty("enemyDamageMultiplier")]
    public double EnemyDamageMultiplier { get; set; } = 1.0;

    [JsonProperty("enemyHealthMultiplier")]
    public double EnemyHealthMultiplier { get; set; } = 1.0;
}

public class EconomicModifiers
{
    [JsonProperty("shopPriceMultiplier")]
    public double ShopPriceMultiplier { get; set; } = 1.0;

    [JsonProperty("lootGoldMultiplier")]
    public double LootGoldMultiplier { get; set; } = 1.0;

    [JsonProperty("startingBonusBudgetMultiplier")]
    public double StartingBonusBudgetMultiplier { get; set; } = 1.0;
}

public class ProgressionModifiers
{
    [JsonProperty("experienceMultiplier")]
    public double ExperienceMultiplier { get; set; } = 1.0;

    [JsonProperty("skillPointMultiplier")]
    public double SkillPointMultiplier { get; set; } = 1.0;
}

public class DeathPenalties
{
    [JsonProperty("loseGoldPercentage")]
    public int LoseGoldPercentage { get; set; }

    [JsonProperty("loseExperiencePercentage")]
    public int LoseExperiencePercentage { get; set; }

    [JsonProperty("respawnAtCheckpoint")]
    public bool RespawnAtCheckpoint { get; set; }

    [JsonProperty("permadeath")]
    public bool Permadeath { get; set; }
}
```

### 4.3 Update SaveGame Model

**File:** `RealmEngine.Shared/Models/SaveGame.cs` (UPDATE)

Add property:
```csharp
[JsonProperty("difficultyId")]
public string DifficultyId { get; set; } = "normal";
```

### 4.4 Create Difficulty Repository and Queries

Similar pattern to Backgrounds (repository, queries, handlers)

---

## Phase 5: Enhanced Character Creation Command

### 5.1 Update CreateCharacterCommand

**File:** `RealmEngine.Core/Features/Characters/Commands/CreateCharacterCommand.cs` (UPDATE)

```csharp
public record CreateCharacterCommand(
    string Name,
    string ClassId,
    string? BackgroundId = null,
    string? StartingLocationId = null,
    string? ArmorType = null,
    string? WeaponType = null,
    bool IncludeShield = false,
    string DifficultyId = "normal"
) : IRequest<Character>;
```

### 5.2 Update CreateCharacterHandler

**File:** `RealmEngine.Core/Features/Characters/Commands/CreateCharacterHandler.cs` (UPDATE)

Major refactor to:
1. Load and validate all selections (class, background, difficulty)
2. Create base character from class
3. Apply background attribute bonuses
4. Resolve equipment selections (armor, weapon, shield)
5. Calculate and apply bonus budget
6. Set starting location
7. Create SaveGame with difficulty

### 5.3 Create Validator

**File:** `RealmEngine.Core/Validators/CreateCharacterCommandValidator.cs`

Validate:
- Name (required, 3-20 chars)
- ClassId (valid class exists)
- BackgroundId (optional, valid if provided)
- StartingLocationId (optional, valid starting zone if provided)
- ArmorType (optional, class has proficiency if provided)
- WeaponType (optional, class has proficiency if provided)
- DifficultyId (valid difficulty exists)

---

## Phase 6: Testing & Documentation

### 6.1 Integration Tests

Create comprehensive test covering full character creation flow with all options

### 6.2 Update Documentation

- `COMMANDS_AND_QUERIES_INDEX.md` - Add all new queries
- `API_SPECIFICATION.md` - Document new endpoints
- Create `CHARACTER_CREATION_GUIDE.md` for Godot integration

### 6.3 Package Deployment

Update `scripts/build-game-package.ps1` to include:
- New catalog files (backgrounds, difficulty)
- Updated models

---

## Implementation Checklist

### Phase 1: Backgrounds ✅
- [ ] Create backgrounds/catalog.json (12 backgrounds)
- [ ] Create backgrounds/.cbconfig.json
- [ ] Create Background.cs model
- [ ] Create BackgroundRepository.cs
- [ ] Create GetBackgroundsQuery/Handler
- [ ] Create GetBackgroundQuery/Handler
- [ ] Register services in DI
- [ ] Create unit tests (5+ tests)
- [ ] Build and verify (dotnet build)
- [ ] Run tests (dotnet test)

### Phase 2: Starting Locations
- [ ] Update Location.cs model (add IsStartingZone)
- [ ] Update location catalog data (5-8 locations)
- [ ] Create GetStartingLocationsQuery/Handler
- [ ] Update LocationRepository (add GetStartingLocationsAsync)
- [ ] Create unit tests (5+ tests)
- [ ] Build and verify
- [ ] Run tests

### Phase 3: Equipment Selection
- [ ] Update CharacterClass.cs (add proficiencies)
- [ ] Update CharacterClassRepository (map proficiencies)
- [ ] Update Item.cs (add weaponType/armorType)
- [ ] Create GetWeaponsByTypeQuery/Handler
- [ ] Create GetArmorByTypeQuery/Handler
- [ ] Create unit tests (8+ tests)
- [ ] Build and verify
- [ ] Run tests

### Phase 4: Difficulty Integration
- [ ] Create difficulty/catalog.json (4 presets)
- [ ] Create difficulty/.cbconfig.json
- [ ] Create Difficulty.cs model (+ modifier classes)
- [ ] Update SaveGame.cs (add DifficultyId)
- [ ] Create DifficultyRepository.cs
- [ ] Create GetDifficultiesQuery/Handler
- [ ] Create GetDifficultyQuery/Handler
- [ ] Create unit tests (5+ tests)
- [ ] Build and verify
- [ ] Run tests

### Phase 5: Enhanced Character Creation
- [ ] Update CreateCharacterCommand (add new parameters)
- [ ] Refactor CreateCharacterHandler (orchestrate all systems)
- [ ] Create CreateCharacterCommandValidator
- [ ] Create integration tests (10+ tests)
- [ ] Test all combinations (class × background × difficulty)
- [ ] Verify default behavior
- [ ] Build and verify
- [ ] Run all tests

### Phase 6: Polish & Documentation
- [ ] Create integration test for full flow
- [ ] Update COMMANDS_AND_QUERIES_INDEX.md
- [ ] Update API_SPECIFICATION.md
- [ ] Create CHARACTER_CREATION_GUIDE.md
- [ ] Test package deployment
- [ ] Verify Godot integration works
- [ ] Performance testing (< 500ms character creation)

---

## Success Criteria

- [ ] All 12 backgrounds load correctly from catalog
- [ ] Background attribute bonuses apply to characters
- [ ] Starting locations filter by background recommendations
- [ ] Equipment selection respects class proficiencies
- [ ] Difficulty modifiers stored in SaveGame
- [ ] Enhanced CreateCharacterCommand accepts all parameters
- [ ] All unit tests pass (50+ new tests)
- [ ] Integration tests pass for full flow
- [ ] Build succeeds with 0 errors
- [ ] Package deploys to Godot successfully
- [ ] Documentation complete and accurate

---

## Notes

- **Start Simple**: Implement each phase fully before moving to next
- **Test Continuously**: Run tests after each phase completion
- **Verify Data**: Check JSON loads correctly before writing code
- **Follow Standards**: All JSON must comply with v4.0/v5.1 standards
- **Godot Integration**: Remember this is backend only - UI is in Godot
