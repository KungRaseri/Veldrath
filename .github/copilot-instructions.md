# RealmEngine - Game Engine Backend (.NET 9.0)

This is a .NET Core backend game engine designed to be integrated with Godot for UI. This repository contains **ONLY** the game logic, data models, and API layer. All UI/UX is handled by a separate Godot project that consumes this engine via commands and queries.

## Project Type

- **Language**: C#
- **Framework**: .NET Core (.NET 9.0)
- **Type**: Game Engine Backend / API Library
- **Architecture**: MediatR CQRS (Commands/Queries for Godot integration)

## Project Structure

```
RealmEngine/ (Backend API for Godot)
├── RealmEngine.Core/          # Game logic, commands, handlers
├── RealmEngine.Shared/        # Models, abstractions, interfaces
├── RealmEngine.Data/          # Data access, repositories, JSON loading
├── RealmEngine.*.Tests/       # Unit and integration tests
├── .vscode/
│   ├── launch.json           # Debug configuration
│   └── tasks.json            # Build tasks
├── .github/
│   └── copilot-instructions.md
├── docs/
│   ├── standards/json/       # JSON v4.0+ standards
│   ├── features/             # Feature documentation
│   └── GDD-Main.md          # Game design document
└── package/                  # Deployment package for Godot integration
```

## Architecture: Backend Engine for Godot UI

**IMPORTANT**: This repository contains **ZERO** production UI code. All UI is built in Godot.

### What This Repository Contains:
- ✅ **Game Logic**: Combat, inventory, progression, quests
- ✅ **Data Models**: Character, Item, Enemy, NPC, Quest, SaveGame
- ✅ **MediatR Commands**: CreateCharacter, AttackEnemy, BuyFromShop, CastSpell
- ✅ **MediatR Queries**: GetPlayerInventory, GetCombatState, GetActiveQuests
- ✅ **JSON Data**: 211 data files (enemies, items, abilities, spells, etc.)
- ✅ **Generators**: Procedural content (items, enemies, NPCs, locations)
- ✅ **Persistence**: LiteDB save/load system

### What This Repository Does NOT Contain:
- ❌ **Production UI**: No menus, HUD, or visual elements
- ❌ **Input Handling**: Godot handles all player input
- ❌ **Rendering**: Godot handles all graphics
- ❌ **Audio Playback**: Godot handles all sound (NAudio unused in production)

### Integration Pattern:
```
Godot UI → IMediator.Send(Command/Query) → RealmEngine Backend → Response DTO → Godot UI
```

**Example Integration:**
```csharp
// Godot calls this via DI-injected IMediator
var result = await mediator.Send(new AttackEnemyCommand 
{ 
    CharacterName = "Player1",
    Action = CombatActionType.Attack 
});

// Godot receives CombatResult and updates UI
if (result.Success) {
    UpdateHealthBar(result.PlayerHealth);
    ShowDamageNumber(result.Damage);
}
```

## Completed Setup

- ✅ Created project structure with RealmEngine.Core/Shared/Data
- ✅ Added MediatR v14.0.0 for CQRS command/query pattern
- ✅ Added LiteDB v5.0.21 for save game persistence
- ✅ Added Newtonsoft.Json v13.0.4 for JSON data loading
- ✅ Added FluentValidation v12.1.1 for input validation
- ✅ Added Bogus v35.6.5 for procedural content generation
- ✅ Added Humanizer v3.0.1 for natural language formatting
- ✅ Added Polly v8.6.5 for resilience patterns
- ✅ Added Serilog v4.3.0 for structured logging
- ✅ Added xUnit v2.9.3 and FluentAssertions v8.8.0 for testing
- ✅ Compiled successfully with `dotnet build`
- ✅ Created VS Code build and debug tasks
- ✅ 7,564 tests passing (100% pass rate)
- ✅ Established JSON v4.0+ standards for all game data files
- ✅ Created RealmForge WPF application for JSON editing (separate tool, on hold)
- ✅ Integrated JSON v4.1 reference system
- ✅ All JSON compliance tests passing
- ✅ Migrated 38 catalogs to JSON v5.1 (attributes, formulas, combat structure)

## JSON Data Standards (v4.0 + v4.1 References)

**All game data files follow strict standards documented in `docs/standards/json/`:**

### JSON Reference System v4.1

**Purpose**: Unified system for linking game data across domains to eliminate duplication

**Reference Syntax**: `@domain/path/category:item-name[filters]?.property.nested`

**Common Reference Patterns**:
- Abilities: `@abilities/active/offensive:basic-attack`
- Classes: `@classes/warriors:fighter`
- Items: `@items/weapons/swords:iron-longsword`
- Enemies: `@enemies/humanoid:goblin-warrior`
- NPCs: `@npcs/merchants:blacksmith`
- Quests: `@quests/main-story:chapter-1`

**Features**:
- Direct references: Link to specific items
- Property access: Use dot notation (`.property.nested`)
- Wildcard selection: `:*` for random item respecting rarityWeight
- Optional references: `?` suffix returns null instead of error
- Filtering: Support for operators (=, !=, <, <=, >, >=, EXISTS, NOT EXISTS, MATCHES)

**Documentation**: `docs/standards/json/JSON_REFERENCE_STANDARDS.md`

### names.json Standard (Pattern Generation)

**Required Fields:**
- `version`: "4.0"
- `type`: "pattern_generation"
- `supportsTraits`: true or false
- `lastUpdated`: ISO date string
- `description`: Purpose of the file
- `patterns[]`: Array with `rarityWeight` (NOT "weight")
- `components{}`: Component arrays (prefix, suffix, etc.)

**Pattern Syntax:**
- Component tokens: `{base}`, `{prefix}`, `{suffix}`, `{quality}`
- External references: Use v4.1 syntax `@items/materials/metals` instead of old `[@materialRef/weapon]`
- NO "example" fields allowed

### catalog.json Standard (Item/Enemy Definitions)

**Required Metadata:**
- `description`, `version`, `lastUpdated`, `type` (ends with "_catalog")

**Structure:**
- All items MUST have `name` and `rarityWeight`
- Physical "weight" allowed (item weight in lbs)
- Use references instead of hardcoded names (e.g., `@abilities/...` not "Basic Attack")

### .cbconfig.json Standard (ContentBuilder UI)

**Required Fields:**
- `icon`: MaterialDesign icon name (NOT emojis)
- `sortOrder`: Integer for tree position

### Compliance Status

✅ **JSON v4.1 Reference System (December 28, 2025)**
- **classes/catalog.json**: ✅ All abilities and parentClass use references
- **classes/progression.json**: ✅ Merged into catalog.json (deleted)

✅ **JSON v4.0 Standards Compliance (December 29, 2025)**
- **Phase 5 Comprehensive Testing**: 857 automated tests created
- **Total Files**: 164 (61 catalogs + 38 names + 65 configs)
- **Overall Compliance**: 164/164 files (100%) ✅
  - ✅ **.cbconfig.json**: 65/65 (100% compliant)
  - ✅ **names.json**: 38/38 (100% compliant)
  - ✅ **catalog.json**: 61/61 (100% compliant)

**All JSON data files are now fully compliant with v4.0 standards!**

**See**: [JSON_DATA_COMPLIANCE_REPORT.md](../docs/JSON_DATA_COMPLIANCE_REPORT.md)

**Standards Documentation:**
- `docs/standards/json/JSON_REFERENCE_STANDARDS.md` - **NEW v4.1**
- `docs/standards/json/NAMES_JSON_STANDARD.md`
- `docs/standards/json/CATALOG_JSON_STANDARD.md`
- `docs/standards/json/CBCONFIG_STANDARD.md`
- `docs/standards/json/README.md`

### Budget System Field Standardization (January 17, 2026)

**Unified Field System**: All game data now uses `rarityWeight` exclusively for both selection probability and cost calculation.

**Key Changes:**
- ✅ **Removed** `budgetCost` field from all materials (14 files)
- ✅ **Removed** `rarity` field from all catalogs (76 files)
- ✅ **Removed** `selectionWeight` from material pools (replaced with `rarityWeight`)
- ✅ **Material Pools**: Converted from array to dictionary structure in `material-pools.json`

**Cost Calculation**: Costs are now calculated dynamically from `rarityWeight` using inverse formulas:
- **Materials**: `cost = (6000 / rarityWeight) × costScale`
- **Components**: `cost = 100 / rarityWeight`
- **Enchantments**: `cost = 130 / rarityWeight`

**Rarity Tiers**: The `rarity` field is no longer stored in JSON. Instead, display tiers are calculated on-demand from `rarityWeight`:
- **Common**: rarityWeight 50-100
- **Uncommon**: rarityWeight 30-49
- **Rare**: rarityWeight 15-29
- **Epic**: rarityWeight 5-14
- **Legendary**: rarityWeight 1-4

**Material Pools Dictionary Structure**: Material pools are now keyed by pool name for efficient lookup:
```json
{
  "pools": {
    "humanoid_low": {
      "metals": [
        {
          "materialRef": "@materials/properties/metals:iron",
          "rarityWeight": 60
        }
      ]
    }
  }
}
```

**See**: [BUDGET_SYSTEM_FIELD_STANDARDIZATION.md](../docs/proposals/BUDGET_SYSTEM_FIELD_STANDARDIZATION.md)

## Dependencies

### Data & Persistence
- **LiteDB v5.0.21** - Lightweight NoSQL database for save files and game data
- **Newtonsoft.Json v13.0.4** - JSON serialization for configuration and data storage

### Audio
- **NAudio v2.2.1** - Sound effects and background music playback

### Validation & Data Generation
- **FluentValidation v12.1.1** - Robust input validation with custom rules
- **Bogus v35.6.5** - Realistic fake data generation for NPCs, items, and content

### Utilities
- **Humanizer v3.0.1** - Natural language text formatting (numbers to words, pluralization)
- **MediatR v14.0.0** - Event-driven architecture and command/query separation
- **Polly v8.6.5** - Resilience patterns (retry logic, circuit breakers)

### Logging
- **Serilog v4.3.0** - Structured logging framework
- **Serilog.Sinks.Console v6.1.1** - Console output for logs
- **Serilog.Sinks.File v7.0.0** - File-based logging

### Testing
- **xUnit v2.9.3** - Unit testing framework
- **xUnit.runner.visualstudio v3.1.5** - Visual Studio test runner integration
- **FluentAssertions v8.8.0** - Readable and expressive test assertions

## How to Use

- **Build**: Press `Ctrl+Shift+B` or run `dotnet build`
- **Run**: Use `dotnet run --project Game`
- **Debug**: Press `F5` in VS Code (uses integrated terminal for color support)
- **Watch Mode**: Run the "watch" task for auto-reload during development
- **Run Tests**: Use `dotnet test` to run all 38 unit tests

## Testing

### Test Project Structure

```
Game.Tests/
├── Models/                      # Model tests
│   └── CharacterTests.cs       # 7 tests
├── Validators/                  # Validation tests
│   └── CharacterValidatorTests.cs  # 6 tests
├── Generators/                  # Generator tests
│   ├── ItemGeneratorTests.cs   # 6 tests
│   └── NpcGeneratorTests.cs    # 5 tests
└── Game.Tests.csproj
```

### Running Tests

```powershell
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~CharacterTests"

# Run tests with verbose output
dotnet test --logger "console;verbosity=detailed"
```

### Test Coverage (38 tests ✅)

- **CharacterTests**: Model behavior, XP gain, leveling, stat increases
- **CharacterValidatorTests**: Name, level, health, mana validation
- **ItemGeneratorTests**: Item creation, type filtering, uniqueness
- **NpcGeneratorTests**: NPC generation, data variety, realism

### Writing Tests

Example test with FluentAssertions:

```csharp
[Fact]
public void Character_Should_Level_Up_When_Gaining_100_XP()
{
    // Arrange
    var character = new Character { Level = 1, Experience = 0 };
    
    // Act
    character.GainExperience(100);
    
    // Assert
    character.Level.Should().Be(2);
    character.Experience.Should().Be(0);
}
```

Example validation test using FluentValidation.TestHelper:

```csharp
[Fact]
public void Should_Have_Error_When_Name_Is_Empty()
{
    // Arrange
    var validator = new CharacterValidator();
    
    // Act & Assert
    validator.ShouldHaveValidationErrorFor(c => c.Name, new Character { Name = "" });
}
```

### LiteDB - Game Data Persistence
```csharp
// Save game data
using var db = new LiteDatabase("game.db");
var saves = db.GetCollection<SaveGame>("saves");
saves.Insert(new SaveGame { PlayerName = "Hero", Level = 5 });

// Query saved games
var allSaves = saves.FindAll().ToList();
```

### NAudio - Sound Effects & Music
```csharp
// Play background music
using var audioFile = new AudioFileReader("music.mp3");
using var outputDevice = new WaveOutEvent();
outputDevice.Init(audioFile);
outputDevice.Play();

// Play sound effect
using var sfx = new AudioFileReader("sword.wav");
using var output = new WaveOutEvent();
output.Init(sfx);
output.Play();
```

### FluentValidation - Input Validation
```csharp
// Define validation rules
public class CharacterValidator : AbstractValidator<Character>
{
    public CharacterValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty()
            .Length(3, 20)
            .Matches("^[a-zA-Z]+$");
        
        RuleFor(c => c.Level)
            .GreaterThan(0)
            .LessThanOrEqualTo(100);
    }
}

// Validate
var validator = new CharacterValidator();
var result = validator.Validate(character);
if (!result.IsValid)
{
    foreach (var error in result.Errors)
        ConsoleUI.ShowError(error.ErrorMessage);
}
```

### Bogus - Procedural Content Generation
```csharp
// Generate random NPCs
var npcFaker = new Faker<NPC>()
    .RuleFor(n => n.Name, f => f.Name.FullName())
    .RuleFor(n => n.Age, f => f.Random.Int(18, 80))
    .RuleFor(n => n.Occupation, f => f.Name.JobTitle())
    .RuleFor(n => n.Gold, f => f.Random.Int(10, 500));

var npcs = npcFaker.Generate(10);

// Generate random items
var itemFaker = new Faker<Item>()
    .RuleFor(i => i.Name, f => f.Commerce.ProductName())
    .RuleFor(i => i.Price, f => f.Random.Int(5, 100))
    .RuleFor(i => i.Rarity, f => f.PickRandom<Rarity>());

var loot = itemFaker.Generate(50);
```

### Humanizer - Natural Language Formatting
```csharp
// Convert numbers to words
int gold = 1234;
ConsoleUI.WriteText($"You have {gold.ToWords()} gold coins");
// Output: "You have one thousand two hundred and thirty-four gold coins"

// Pluralization
int enemies = 5;
ConsoleUI.WriteText($"{enemies} {"enemy".ToQuantity(enemies)}");
// Output: "5 enemies"

// Time formatting
TimeSpan elapsed = TimeSpan.FromMinutes(75);
ConsoleUI.WriteText($"Play time: {elapsed.Humanize()}");
// Output: "Play time: 1 hour, 15 minutes"

// Ordinal numbers
int position = 3;
ConsoleUI.WriteText($"You finished in {position.Ordinalize()} place");
// Output: "You finished in 3rd place"
```

### MediatR - Event-Driven Architecture
```csharp
// Define events
public record PlayerLeveledUp(int NewLevel) : INotification;

// Handle events
public class LevelUpHandler : INotificationHandler<PlayerLeveledUp>
{
    public Task Handle(PlayerLeveledUp notification, CancellationToken ct)
    {
        ConsoleUI.ShowSuccess($"Level up! You are now level {notification.NewLevel}!");
        return Task.CompletedTask;
    }
}

// Publish events
await mediator.Publish(new PlayerLeveledUp(6));
```

### Polly - Resilience & Retry Logic
```csharp
// Retry file operations
var retryPolicy = Policy
    .Handle<IOException>()
    .WaitAndRetry(3, retryAttempt => 
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

retryPolicy.Execute(() =>
{
    File.WriteAllText("save.json", gameData);
});

// Circuit breaker for external services
var circuitBreaker = Policy
    .Handle<HttpRequestException>()
    .CircuitBreaker(2, TimeSpan.FromMinutes(1));
```

### Serilog - Structured Logging
```csharp
// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/game-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

// Use structured logging
Log.Information("Player {PlayerName} started new game", playerName);
Log.Warning("Low health: {Health}/{MaxHealth}", health, maxHealth);
Log.Error(ex, "Failed to save game for player {PlayerId}", playerId);

// Context enrichment
using (LogContext.PushProperty("SessionId", sessionId))
{
    Log.Information("Battle started");
}
```

### xUnit & FluentAssertions - Testing
```csharp
// Test with FluentAssertions
public class CharacterTests
{
    [Fact]
    public void Character_Should_Level_Up_Correctly()
    {
        // Arrange
        var character = new Character { Level = 1, Experience = 0 };
        
        // Act
        character.GainExperience(100);
        
        // Assert
        character.Level.Should().Be(2);
        character.Experience.Should().Be(0);
    }
    
    [Theory]
    [InlineData(10, 20, 30)]
    [InlineData(5, 5, 10)]
    public void Inventory_Should_Stack_Items(int count1, int count2, int expected)
    {
        // Arrange
        var inventory = new Inventory();
        
        // Act
        inventory.AddItem("Potion", count1);
        inventory.AddItem("Potion", count2);
        
        // Assert
        inventory.GetItemCount("Potion").Should().Be(expected);
    }
}
```

## Architecture Recommendations

### Project Organization
```
Game/
├── Commands/          # CLI commands
├── Models/            # Game entities (Character, Item, Enemy, etc.)
├── Services/          # Game logic services
├── Handlers/          # MediatR event handlers
├── Validators/        # FluentValidation validators
├── Data/              # LiteDB repositories
├── Audio/             # NAudio sound management
├── Generators/        # Bogus data generators
```

### Best Practices
1. **Separation of Concerns**: Keep game logic separate from UI code
2. **Event-Driven**: Use MediatR for decoupled game events
3. **Validation**: Validate all user input with FluentValidation
4. **Logging**: Log important game events with Serilog
5. **Testing**: Write unit tests for game mechanics with xUnit
6. **Persistence**: Use LiteDB for simple data, consider adding indexes
7. **Audio**: Dispose of audio resources properly to prevent memory leaks
8. **Error Handling**: Use Polly for retry logic on I/O operations
