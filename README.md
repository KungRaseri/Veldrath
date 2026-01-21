# RealmEngine

A powerful RPG framework built with .NET 9, showcasing modern C# architecture patterns and comprehensive game systems. RealmEngine provides the foundation for creating fantasy adventures, with **RealmForge** as the companion data editor tool.

## 🏗️ Architecture

**Vertical Slice Architecture + CQRS Pattern**

**RealmEngine** consists of multiple libraries working together:
- **RealmEngine.Core** - Core game mechanics, combat, inventory, character systems
- **RealmEngine.Data** - JSON data management and persistence layer  
- **RealmEngine.Shared** - Common utilities, models, and services
- **RealmForge** - WPF desktop application for editing game data (JSON files)

This project uses **Vertical Slice Architecture** with **CQRS** (Command Query Responsibility Segregation) using **MediatR** for a clean, maintainable codebase organized by business features.

- 📐 **[Vertical Slice Migration Summary](./docs/VERTICAL_SLICE_MIGRATION_SUMMARY.md)** - Complete migration details
- 🚀 **[Developer Quick Reference](./docs/VERTICAL_SLICE_QUICK_REFERENCE.md)** - How to add new features

**Key Benefits:**
- ✅ Code organized by **business capability** (Features/Combat, Features/Inventory, etc.)
- ✅ Clear separation: **Commands** (write) vs **Queries** (read)
- ✅ Automatic **validation** and **logging** via MediatR pipeline behaviors
- ✅ **CQRS handlers** across multiple game features (Combat, Inventory, CharacterCreation, SaveLoad, Exploration)

## 📚 Documentation

**Complete documentation is available in the [docs/](./docs/) folder:**

- 🎮 **[Game Design Document (GDD-Main.md)](./docs/GDD-Main.md)** - **START HERE!** Complete game overview, systems, and roadmap
- � **[Vertical Slice Migration Summary](./docs/VERTICAL_SLICE_MIGRATION_SUMMARY.md)** - Complete migration details
- 🚀 **[Developer Quick Reference](./docs/VERTICAL_SLICE_QUICK_REFERENCE.md)** - How to add new features
- 📖 **[Documentation Index](./docs/README.md)** - All guides and implementation notes

**Quick Guide Links:**
- [Game Loop Guide](./docs/guides/GAME_LOOP_GUIDE.md) - Understanding the GameEngine architecture
- [Inventory System Guide](./docs/guides/INVENTORY_GUIDE.md) - Complete item management system
- [Settings Guide](./docs/guides/SETTINGS_GUIDE.md) - Configuration management
- [Save/Load Guide](./docs/guides/SAVE_LOAD_GUIDE.md) - Game persistence system
- [Test Coverage Report](./docs/testing/TEST_COVERAGE_REPORT.md) - Comprehensive test suite with high pass rate

## Solution Files

Three solution files are provided for different development scenarios:

- **RealmEngine.sln** - Core game engine only (no MAUI dependencies)
  - RealmEngine.Shared, RealmEngine.Core, RealmEngine.Data
  - Test projects for all engine libraries
  - ✅ Builds without MAUI workloads
  - 🎯 Used by CI/CD for engine builds and NuGet publishing

- **RealmForge.sln** - MAUI tool + dependencies
  - RealmForge (MAUI app), RealmForge.Tests
  - Required engine libraries
  - ⚠️ Requires MAUI workloads (`dotnet workload install maui-windows`)
  - 🎯 Used by MAUI CI workflow

- **RealmEngine.Full.sln** - All projects (complete workspace)
  - All engine and tool projects
  - 🎯 Used for local development when working across both

## Quick Start

```powershell
# Build the RealmEngine framework (no MAUI required)
dotnet build RealmEngine.sln

# Run the test suite (8,286 tests)
dotnet test RealmEngine.sln

# Build RealmForge tool (requires MAUI workloads)
dotnet build RealmForge.sln --framework net9.0-windows10.0.19041.0

# Launch RealmForge data editor
dotnet run --project RealmForge

# Debug in VS Code
Press F5
```

## ⚡ Features

### Game Engine & Architecture
- **State Machine**: GameEngine with event-driven architecture (MediatR)
- **Error Handling**: Retry logic and resilience patterns (Polly)
- **Settings System**: Microsoft.Extensions.Configuration with validation
- **Logging**: Structured logging (Serilog) to console and files

### Gameplay Features
- **D20 System**: Full attribute system (STR, DEX, CON, INT, WIS, CHA) with derived stats
- **Character Classes**: 6 classes (Warrior, Rogue, Mage, Cleric, Ranger, Paladin) with unique bonuses
- **Turn-Based Combat**: Attack, defend, use items - with dodge, crit, and blocking mechanics
- **Level-Up System**: Interactive attribute allocation and skill learning
- **Skills**: 8 learnable skills that enhance combat and character abilities
- **Inventory System**: Full item management with equipment slots, consumables, and sorting
- **Item Generation**: Random loot drops with 5 rarity tiers (Common to Legendary)
- **Save/Load**: Persistent game state with auto-save and multiple character support
- **Enemy AI**: Procedurally generated enemies with difficulty scaling

### Backend API & Integration
- **MediatR Commands/Queries**: Complete API for Godot UI integration
- **Data Persistence**: Save/load game state (LiteDB)
- **Audio Support**: Background music and sound effects (NAudio - planned for Godot)

### Development & Testing  
- **Validation**: Robust input checking (FluentValidation)
- **Procedural Generation**: Random NPCs and items (Bogus)
- **Natural Language**: Number formatting and pluralization (Humanizer)
- **Test Coverage**: Comprehensive test suite with xUnit and FluentAssertions

See the [docs/](./docs/) folder for detailed feature documentation.

## 🔧 RealmForge - Data Editor

**RealmForge** is the companion WPF desktop application for editing RealmEngine's game data:

- **JSON Editor** - Visual editor for all game data files (164+ files)
- **Schema Validation** - Real-time validation against JSON v4.0 standards  
- **Reference System** - Support for v4.1 cross-references between data files
- **Live Preview** - See changes instantly without restarting
- **Data Compliance** - Ensures all JSON follows established patterns

**Launch RealmForge:**
```powershell
dotnet run --project RealmForge
```

**Key Features:**
- Edit abilities, classes, enemies, items, NPCs, and quests
- Material Design UI with dark/light themes
- Drag-and-drop pattern building for names generation
- Automatic backup and version control integration
- Hot reload support for rapid iteration

## Architecture Highlights

🏗️ **Modern Design Patterns**
- **Vertical Slice Architecture** - Features organized by business capability
- **CQRS with MediatR** - Clean separation of commands and queries
- **Event-Driven Architecture** - Loosely coupled components
- **Domain-Driven Design** - Rich domain models and services

🧪 **Quality & Testing**
- **8,286 Tests Passing** - 100% pass rate across all projects
  - 6,378 Data Tests (JSON compliance, reference integrity)
  - 1,218 Core Tests (combat, crafting, inventory, quests)
  - 690 Shared Tests (models, services, utilities)
- **High Test Coverage** - Extensive validation of game mechanics
- **Automated CI/CD** - Quality gates and continuous integration
- **JSON Schema Validation** - Data integrity across 164+ game data files

🎯 **Enterprise Patterns**
- **Dependency Injection** - Microsoft.Extensions.DI
- **Configuration Management** - Strongly-typed settings
- **Structured Logging** - Serilog with multiple sinks
- **Resilience Patterns** - Polly for retry logic

## Building the Project

```powershell
# Build the entire RealmEngine solution
dotnet build

# Build specific components
dotnet build RealmEngine.Core        # Core engine
dotnet build RealmEngine.Data        # Data layer
dotnet build RealmEngine.Shared      # Shared utilities
dotnet build RealmForge              # Data editor tool

# Run all tests
dotnet test

# Launch RealmForge
dotnet run --project RealmForge
```

## Development

### Adding New Models
Create classes in the `Models/` folder:
```csharp
namespace Game.Models;

public class Enemy
{
    public string Name { get; set; }
    public int Health { get; set; }
}
```

### Creating Validators
Use FluentValidation in the `Validators/` folder:
```csharp
using FluentValidation;
using Game.Models;

namespace Game.Validators;

public class EnemyValidator : AbstractValidator<Enemy>
{
    public EnemyValidator()
    {
        RuleFor(e => e.Name).NotEmpty();
        RuleFor(e => e.Health).GreaterThan(0);
    }
}
```

### Generating Random Data
Use Bogus generators in the `Generators/` folder:
```csharp
using Bogus;
using Game.Models;

namespace Game.Generators;

public static class EnemyGenerator
{
    private static readonly Faker<Enemy> EnemyFaker = new Faker<Enemy>()
        .RuleFor(e => e.Name, f => f.Name.FirstName())
        .RuleFor(e => e.Health, f => f.Random.Int(50, 200));

    public static Enemy Generate() => EnemyFaker.Generate();
}
```

### Using Events
Define events in `Handlers/GameEvents.cs`:
```csharp
public record EnemyDefeated(string EnemyName, int XpGained) : INotification;
```

Create handlers in `Handlers/EventHandlers.cs`:
```csharp
public class EnemyDefeatedHandler : INotificationHandler<EnemyDefeated>
{
    public Task Handle(EnemyDefeated notification, CancellationToken ct)
    {
        ConsoleUI.ShowSuccess($"Defeated {notification.EnemyName}!");
        return Task.CompletedTask;
    }
}
```

### Saving Data
Use LiteDB repositories in the `Data/` folder:
```csharp
using (var repo = new SaveGameRepository())
{
    var save = new SaveGame { PlayerName = "Hero" };
    repo.Save(save);
}
```

## Libraries Used

- **MediatR** - CQRS command/query pattern for Godot integration
- **LiteDB** - NoSQL database for save games
- **Newtonsoft.Json** - JSON serialization for game data
- **NAudio** - Audio playback (for future Godot integration)
- **FluentValidation** - Input validation
- **Bogus** - Procedural content generation
- **Humanizer** - Natural language formatting
- **Polly** - Resilience patterns
- **Serilog** - Structured logging
- **xUnit** - Unit testing
- **FluentAssertions** - Test assertions

## Testing

The project includes a comprehensive test suite using xUnit and FluentAssertions.

### Running Tests

To run all tests:

```powershell
dotnet test
```

Or run specific test files:

```powershell
# Run specific test class
dotnet test --filter "FullyQualifiedName~CharacterTests"

# Run all tests (8,286 total)
dotnet test RealmEngine.sln

# Run specific test projects
dotnet test RealmEngine.Data.Tests     # 6,378 JSON compliance tests
dotnet test RealmEngine.Core.Tests     # 1,218 game logic tests  
dotnet test RealmEngine.Shared.Tests   # 690 utility tests
```

### Test Structure

```
RealmEngine.Core.Tests/
├── Features/                    # Feature slice tests
│   ├── Combat/                 # Combat system tests
│   ├── Crafting/               # Crafting system tests
│   ├── Inventory/              # Inventory tests
│   └── Quest/                  # Quest system tests
├── Generators/                  # Generator tests
└── Services/                    # Service layer tests

RealmEngine.Data.Tests/
├── CatalogJsonComplianceTests.cs    # 857 catalog validation tests
├── NamesJsonComplianceTests.cs      # Names.json compliance
├── ReferenceIntegrityTests.cs       # Cross-reference validation
└── Services/                        # Data service tests

RealmEngine.Shared.Tests/
├── Models/                      # Shared model tests
└── Services/                    # Utility service tests
```

### Test Coverage

**Test Results:**
- **RealmEngine.Data.Tests**: 6,378/6,378 passing ✅
- **RealmEngine.Core.Tests**: 1,218/1,225 passing (7 intentionally skipped) ✅
- **RealmEngine.Shared.Tests**: 690/690 passing ✅
- **Total**: 8,286/8,293 passing (99.9%) ✅

Current test coverage includes:
- **JSON Compliance**: 857 tests for v4.0+ standards (catalogs, names, configs)
- **Reference Integrity**: Validates all `@domain/path:item` references
- **Combat System**: Turn-based combat, abilities, spells, status effects
- **Crafting System**: Tiered failure, quality bonuses, material refunds
- **Item Generation**: Budget-fitting algorithm, component selection
- **Character Progression**: XP gain, leveling, skill advancement
- **Inventory Management**: Equipment, stacking, weight limits

**All critical systems tested** ✅

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

Example validation test:

```csharp
[Fact]
public void Should_Have_Error_When_Name_Is_Empty()
{
    // Arrange
    var validator = new CharacterValidator();
    var character = new Character { Name = "" };
    
    // Act & Assert
    validator.ShouldHaveValidationErrorFor(c => c.Name, character);
}
```

## Development Roadmap

The project follows a feature-driven development approach using vertical slices:

**Core Engine Features**
- Quest system with objectives and rewards
- Magic spell system with mana management
- Shop/economy system with dynamic pricing
- Status effects system (poison, stun, burning, etc.)

**Content & World Building**
- Dungeon zones with procedural generation
- Achievement and statistics tracking
- Equipment enchantment system
- NPC dialogue trees

**Quality of Life**
- Hot reload for JSON data files
- Console game controller support
- Audio system enhancements
- Performance optimizations

## Architecture Resources

**Documentation**
- **[Game Design Document](./docs/GDD-Main.md)** - Complete game specification
- **[Vertical Slice Quick Reference](./docs/VERTICAL_SLICE_QUICK_REFERENCE.md)** - Adding new features
- **[Architecture Documentation](./docs/)** - All implementation guides

**External Resources**
- [MediatR Documentation](https://github.com/jbogard/MediatR)
- [Godot Engine Documentation](https://docs.godotengine.org/)
- [.NET 9 Documentation](https://docs.microsoft.com/en-us/dotnet/)

---

**Framework**: .NET 9.0 with C# 13  
**Architecture**: MediatR CQRS Pattern (Backend API for Godot)  
**UI Framework**: Godot Engine (separate project)  
**Database**: LiteDB for persistence  
**Testing**: xUnit with FluentAssertions
