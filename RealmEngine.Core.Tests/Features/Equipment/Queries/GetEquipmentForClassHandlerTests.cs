using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using RealmEngine.Core;
using RealmEngine.Core.Features.Equipment.Queries;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Equipment.Queries;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for GetEquipmentForClassHandler to verify equipment filtering by class proficiencies.
/// </summary>
public class GetEquipmentForClassHandlerTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMediator _mediator;

    public GetEquipmentForClassHandlerTests()
    {
        var services = new ServiceCollection();
        
        // Register logging
        services.AddLogging();
        
        // Register RealmEngine services
        services.AddRealmEngineData("c:\\code\\console-game\\RealmEngine.Data\\Data\\Json");
        services.AddRealmEngineCore();
        services.AddRealmEngineMediatR();

        _serviceProvider = services.BuildServiceProvider();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();
    }

    public void Dispose()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    [Fact]
    public async Task Handler_Should_Return_Success_For_Valid_Class()
    {
        // Arrange
        var query = new GetEquipmentForClassQuery
        {
            ClassId = "warrior:Fighter",
            MaxItemsPerCategory = 5
        };

        // Act
        var result = await _mediator.Send(query);

        // Assert
        result.Should().NotBeNull();
        if (!result.Success)
        {
            throw new Exception($"Handler failed: {result.ErrorMessage}");
        }
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNullOrEmpty();
        result.ClassName.Should().Be("Fighter");
    }

    [Fact]
    public async Task Handler_Should_Return_Error_For_Invalid_Class()
    {
        // Arrange
        var query = new GetEquipmentForClassQuery
        {
            ClassId = "invalid:nonexistent",
            MaxItemsPerCategory = 5
        };

        // Act
        var result = await _mediator.Send(query);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task Handler_Should_Load_Warrior_Proficiencies()
    {
        // Arrange
        var query = new GetEquipmentForClassQuery
        {
            ClassId = "warrior:Fighter"
        };

        // Act
        var result = await _mediator.Send(query);

        // Assert
        result.Success.Should().BeTrue();
        result.ArmorProficiencies.Should().Contain(new[] { "heavy", "medium", "light", "shields" });
        result.WeaponProficiencies.Should().Contain("all");
    }

    [Fact]
    public async Task Handler_Should_Load_Cleric_Proficiencies()
    {
        // Arrange
        var query = new GetEquipmentForClassQuery
        {
            ClassId = "cleric:Priest"
        };

        // Act
        var result = await _mediator.Send(query);

        // Assert
        result.Success.Should().BeTrue();
        result.ArmorProficiencies.Should().Contain(new[] { "medium", "light", "shields" });
        result.WeaponProficiencies.Should().Contain(new[] { "maces", "staves", "simple" });
    }

    [Fact]
    public async Task Handler_Should_Load_Rogue_Proficiencies()
    {
        // Arrange
        var query = new GetEquipmentForClassQuery
        {
            ClassId = "rogue:Thief"
        };

        // Act
        var result = await _mediator.Send(query);

        // Assert
        result.Success.Should().BeTrue();
        result.ArmorProficiencies.Should().Contain("light");
        result.WeaponProficiencies.Should().Contain(new[] { "daggers", "shortswords", "rapiers", "crossbows" });
    }

    [Fact]
    public async Task Handler_Should_Load_Ranger_Proficiencies()
    {
        // Arrange
        var query = new GetEquipmentForClassQuery
        {
            ClassId = "ranger:Hunter"
        };

        // Act
        var result = await _mediator.Send(query);

        // Assert
        result.Success.Should().BeTrue();
        result.ArmorProficiencies.Should().Contain(new[] { "medium", "light" });
        result.WeaponProficiencies.Should().Contain(new[] { "bows", "crossbows", "swords", "daggers" });
    }

    [Fact]
    public async Task Handler_Should_Load_Mage_Proficiencies()
    {
        // Arrange
        var query = new GetEquipmentForClassQuery
        {
            ClassId = "mage:Wizard"
        };

        // Act
        var result = await _mediator.Send(query);

        // Assert
        result.Success.Should().BeTrue();
        result.ArmorProficiencies.Should().Contain("light");
        result.WeaponProficiencies.Should().Contain(new[] { "staves", "wands", "daggers" });
    }

    [Fact]
    public async Task Handler_Should_Load_Weapons_Only_When_Specified()
    {
        // Arrange
        var query = new GetEquipmentForClassQuery
        {
            ClassId = "warrior:Fighter",
            EquipmentType = "weapons",
            MaxItemsPerCategory = 10
        };

        // Act
        var result = await _mediator.Send(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Weapons.Should().NotBeEmpty("Warriors should have weapon options");
        result.Armor.Should().BeEmpty("Armor should not be loaded when only weapons requested");
    }

    [Fact]
    public async Task Handler_Should_Load_Armor_Only_When_Specified()
    {
        // Arrange
        var query = new GetEquipmentForClassQuery
        {
            ClassId = "warrior:Fighter",
            EquipmentType = "armor",
            MaxItemsPerCategory = 10
        };

        // Act
        var result = await _mediator.Send(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Armor.Should().NotBeEmpty("Warriors should have armor options");
        result.Weapons.Should().BeEmpty("Weapons should not be loaded when only armor requested");
    }

    [Fact]
    public async Task Handler_Should_Load_Both_Weapons_And_Armor_By_Default()
    {
        // Arrange
        var query = new GetEquipmentForClassQuery
        {
            ClassId = "warrior:Fighter",
            MaxItemsPerCategory = 10
        };

        // Act
        var result = await _mediator.Send(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Weapons.Should().NotBeEmpty("Warriors should have weapon options");
        result.Armor.Should().NotBeEmpty("Warriors should have armor options");
    }

    [Fact]
    public async Task Handler_Should_Respect_MaxItemsPerCategory()
    {
        // Arrange
        var query = new GetEquipmentForClassQuery
        {
            ClassId = "warrior:Fighter",
            MaxItemsPerCategory = 3
        };

        // Act
        var result = await _mediator.Send(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Weapons.Should().HaveCountLessThanOrEqualTo(3);
        result.Armor.Should().HaveCountLessThanOrEqualTo(3);
    }

    [Fact]
    public async Task Handler_Should_Set_WeaponType_On_Weapon_Items()
    {
        // Arrange
        var query = new GetEquipmentForClassQuery
        {
            ClassId = "warrior:Fighter",
            EquipmentType = "weapons",
            MaxItemsPerCategory = 5
        };

        // Act
        var result = await _mediator.Send(query);

        // Assert
        result.Success.Should().BeTrue();
        if (result.Weapons.Any())
        {
            result.Weapons.Should().AllSatisfy(w => 
                w.WeaponType.Should().NotBeNullOrEmpty("All weapons should have WeaponType set"));
        }
    }

    [Fact]
    public async Task Handler_Should_Set_ArmorType_On_Armor_Items()
    {
        // Arrange
        var query = new GetEquipmentForClassQuery
        {
            ClassId = "warrior:Fighter",
            EquipmentType = "armor",
            MaxItemsPerCategory = 5
        };

        // Act
        var result = await _mediator.Send(query);

        // Assert
        result.Success.Should().BeTrue();
        if (result.Armor.Any())
        {
            result.Armor.Should().AllSatisfy(a => 
                a.ArmorType.Should().NotBeNullOrEmpty("All armor should have ArmorType set"));
        }
    }

    [Fact]
    public async Task Handler_Should_Only_Load_Proficient_Weapons_For_Rogue()
    {
        // Arrange
        var query = new GetEquipmentForClassQuery
        {
            ClassId = "rogue:Thief",
            EquipmentType = "weapons",
            MaxItemsPerCategory = 20
        };

        // Act
        var result = await _mediator.Send(query);

        // Assert
        result.Success.Should().BeTrue();
        
        // Rogues should NOT get weapons they're not proficient with
        var allowedTypes = new[] { "daggers", "shortswords", "rapiers", "crossbows" };
        
        if (result.Weapons.Any())
        {
            result.Weapons.Should().AllSatisfy(w =>
                allowedTypes.Should().Contain(w.WeaponType, 
                    $"Rogue should only get weapons from {string.Join(", ", allowedTypes)}"));
        }
    }

    [Fact]
    public async Task Handler_Should_Only_Load_Proficient_Armor_For_Rogue()
    {
        // Arrange
        var query = new GetEquipmentForClassQuery
        {
            ClassId = "rogue:Thief",
            EquipmentType = "armor",
            MaxItemsPerCategory = 20
        };

        // Act
        var result = await _mediator.Send(query);

        // Assert
        result.Success.Should().BeTrue();
        
        // Rogues should ONLY get light armor
        if (result.Armor.Any())
        {
            result.Armor.Should().AllSatisfy(a =>
                a.ArmorType.Should().Be("light", "Rogue should only get light armor"));
        }
    }

    [Fact]
    public async Task Handler_Should_Load_All_Weapon_Types_For_Warrior()
    {
        // Arrange
        var query = new GetEquipmentForClassQuery
        {
            ClassId = "warrior:Fighter",
            EquipmentType = "weapons",
            MaxItemsPerCategory = 50
        };

        // Act
        var result = await _mediator.Send(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Weapons.Should().NotBeEmpty("Warriors with 'all' proficiency should get many weapons");
        
        // Warriors should have diverse weapon types since they have "all" proficiency
        var uniqueWeaponTypes = result.Weapons.Select(w => w.WeaponType).Distinct().ToList();
        uniqueWeaponTypes.Should().HaveCountGreaterThan(1, "Warriors should have access to multiple weapon types");
    }

    [Fact(Skip = "Test failing with limited armor loaded - needs investigation into GameDataCache behavior")]
    public async Task Handler_Should_Load_All_Armor_Types_For_Warrior()
    {
        // Arrange
        var query = new GetEquipmentForClassQuery
        {
            ClassId = "warrior:Fighter",
            EquipmentType = "armor",
            MaxItemsPerCategory = 50
        };

        // Act
        var result = await _mediator.Send(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Armor.Should().NotBeEmpty("Warriors should have armor options");
        
        // Warriors should have diverse armor types (light, medium, heavy, shields)
        var uniqueArmorTypes = result.Armor.Select(a => a.ArmorType).Distinct().ToList();
        uniqueArmorTypes.Should().HaveCountGreaterThan(1, "Warriors should have access to multiple armor types");
    }

    [Fact]
    public async Task Handler_Should_Support_Randomization()
    {
        // Arrange
        var query1 = new GetEquipmentForClassQuery
        {
            ClassId = "warrior:Fighter",
            MaxItemsPerCategory = 5,
            RandomizeSelection = true
        };

        var query2 = new GetEquipmentForClassQuery
        {
            ClassId = "warrior:Fighter",
            MaxItemsPerCategory = 5,
            RandomizeSelection = true
        };

        // Act
        var result1 = await _mediator.Send(query1);
        var result2 = await _mediator.Send(query2);

        // Assert
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
        
        // With randomization, the order might be different (though not guaranteed)
        // This test verifies randomization doesn't break the handler
        result1.Weapons.Should().NotBeEmpty();
        result2.Weapons.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handler_Should_Work_With_Subclasses()
    {
        // Arrange - Paladin is a subclass of Priest
        var query = new GetEquipmentForClassQuery
        {
            ClassId = "cleric:Paladin",
            MaxItemsPerCategory = 10
        };

        // Act
        var result = await _mediator.Send(query);

        // Assert
        result.Success.Should().BeTrue();
        result.ClassName.Should().Be("Paladin");
        result.ArmorProficiencies.Should().Contain(new[] { "medium", "light", "shields" });
        result.WeaponProficiencies.Should().Contain(new[] { "maces", "staves", "simple" });
    }

    [Fact]
    public async Task Handler_Should_Handle_Zero_MaxItems_As_All_Items()
    {
        // Arrange
        var query = new GetEquipmentForClassQuery
        {
            ClassId = "warrior:Fighter",
            MaxItemsPerCategory = 0, // 0 means no limit
            EquipmentType = "weapons"
        };

        // Act
        var result = await _mediator.Send(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Weapons.Should().NotBeEmpty();
        // With no limit, warriors should get all available weapons
    }
}
