using FluentAssertions;
using MediatR;
using Moq;
using RealmEngine.Core.Features.CharacterCreation.Commands;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.CharacterCreation.Commands;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for CreateCharacterHandler.
/// </summary>
public class CreateCharacterHandlerTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<IBackgroundRepository> _backgroundRepositoryMock;
    private readonly CreateCharacterHandler _handler;

    public CreateCharacterHandlerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _backgroundRepositoryMock = new Mock<IBackgroundRepository>();
        _handler = new CreateCharacterHandler(_mediatorMock.Object, _backgroundRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_Should_Create_Character_With_Warrior_Class()
    {
        // Arrange
        var warriorClass = CreateWarriorClass();
        var command = new CreateCharacterCommand
        {
            CharacterName = "TestWarrior",
            CharacterClass = warriorClass
        };

        // Setup mediator to return successful ability initialization
        _mediatorMock.Setup(m => m.Send(It.IsAny<InitializeStartingAbilitiesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitializeStartingAbilitiesResult
            {
                Success = true,
                AbilitiesLearned = 3,
                AbilityIds = new List<string> { "ability1", "ability2", "ability3" },
                Message = "Learned 3 starting abilities"
            });

        // Setup mediator to return successful spell initialization (warriors don't get spells)
        _mediatorMock.Setup(m => m.Send(It.IsAny<InitializeStartingSpellsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitializeStartingSpellsResult
            {
                Success = true,
                SpellsLearned = 0,
                SpellIds = new List<string>(),
                Message = "No starting spells for Warrior"
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Character.Should().NotBeNull();
        result.Character!.Name.Should().Be("TestWarrior");
        result.Character.ClassName.Should().Be("Warrior");
        result.Character.Level.Should().Be(1);
        result.Character.Experience.Should().Be(0);
        result.Character.Strength.Should().Be(13); // 10 base + 3 bonus
        result.Character.Health.Should().Be(12);
        result.Character.MaxHealth.Should().Be(12);
        result.Character.Mana.Should().Be(4);
        result.Character.MaxMana.Should().Be(4);
        result.AbilitiesLearned.Should().Be(3);
        result.SpellsLearned.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Should_Create_Character_With_Mage_Class()
    {
        // Arrange
        var mageClass = CreateMageClass();
        var command = new CreateCharacterCommand
        {
            CharacterName = "TestMage",
            CharacterClass = mageClass
        };

        // Setup mediator to return successful ability initialization
        _mediatorMock.Setup(m => m.Send(It.IsAny<InitializeStartingAbilitiesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitializeStartingAbilitiesResult
            {
                Success = true,
                AbilitiesLearned = 2,
                AbilityIds = new List<string> { "ability1", "ability2" },
                Message = "Learned 2 starting abilities"
            });

        // Setup mediator to return successful spell initialization (mages get spells)
        _mediatorMock.Setup(m => m.Send(It.IsAny<InitializeStartingSpellsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitializeStartingSpellsResult
            {
                Success = true,
                SpellsLearned = 5,
                SpellIds = new List<string> { "spell1", "spell2", "spell3", "spell4", "spell5" },
                Message = "Learned 5 starting spells"
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Character.Should().NotBeNull();
        result.Character!.Name.Should().Be("TestMage");
        result.Character.ClassName.Should().Be("Mage");
        result.Character.Level.Should().Be(1);
        result.Character.Intelligence.Should().Be(13); // 10 base + 3 bonus
        result.Character.Health.Should().Be(6);
        result.Character.MaxHealth.Should().Be(6);
        result.Character.Mana.Should().Be(20);
        result.Character.MaxMana.Should().Be(20);
        result.AbilitiesLearned.Should().Be(2);
        result.SpellsLearned.Should().Be(5);
    }

    [Fact]
    public async Task Handle_Should_Initialize_Empty_Collections()
    {
        // Arrange
        var command = new CreateCharacterCommand
        {
            CharacterName = "TestChar",
            CharacterClass = CreateWarriorClass()
        };

        _mediatorMock.Setup(m => m.Send(It.IsAny<InitializeStartingAbilitiesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitializeStartingAbilitiesResult { Success = true, AbilitiesLearned = 0 });

        _mediatorMock.Setup(m => m.Send(It.IsAny<InitializeStartingSpellsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitializeStartingSpellsResult { Success = true, SpellsLearned = 0 });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Character!.Inventory.Should().NotBeNull().And.BeEmpty();
        result.Character.LearnedAbilities.Should().NotBeNull().And.BeEmpty();
        result.Character.LearnedSpells.Should().NotBeNull().And.BeEmpty();
        result.Character.Skills.Should().NotBeNull().And.BeEmpty();
        result.Character.PendingLevelUps.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_Call_InitializeStartingAbilitiesCommand()
    {
        // Arrange
        var command = new CreateCharacterCommand
        {
            CharacterName = "TestChar",
            CharacterClass = CreateWarriorClass()
        };

        _mediatorMock.Setup(m => m.Send(It.IsAny<InitializeStartingAbilitiesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitializeStartingAbilitiesResult { Success = true, AbilitiesLearned = 3 });

        _mediatorMock.Setup(m => m.Send(It.IsAny<InitializeStartingSpellsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitializeStartingSpellsResult { Success = true, SpellsLearned = 0 });

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<InitializeStartingAbilitiesCommand>(c => 
                c.Character.Name == "TestChar" && 
                c.ClassName == "Warrior"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Call_InitializeStartingSpellsCommand()
    {
        // Arrange
        var command = new CreateCharacterCommand
        {
            CharacterName = "TestMage",
            CharacterClass = CreateMageClass()
        };

        _mediatorMock.Setup(m => m.Send(It.IsAny<InitializeStartingAbilitiesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitializeStartingAbilitiesResult { Success = true, AbilitiesLearned = 2 });

        _mediatorMock.Setup(m => m.Send(It.IsAny<InitializeStartingSpellsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitializeStartingSpellsResult { Success = true, SpellsLearned = 5 });

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<InitializeStartingSpellsCommand>(c => 
                c.Character.Name == "TestMage" && 
                c.ClassName == "Mage"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Success_Even_If_Abilities_Initialization_Fails()
    {
        // Arrange
        var command = new CreateCharacterCommand
        {
            CharacterName = "TestChar",
            CharacterClass = CreateWarriorClass()
        };

        _mediatorMock.Setup(m => m.Send(It.IsAny<InitializeStartingAbilitiesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitializeStartingAbilitiesResult 
            { 
                Success = false, 
                AbilitiesLearned = 0,
                Message = "Failed to initialize abilities"
            });

        _mediatorMock.Setup(m => m.Send(It.IsAny<InitializeStartingSpellsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitializeStartingSpellsResult { Success = true, SpellsLearned = 0 });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue(); // Character created successfully, abilities just weren't learned
        result.Character.Should().NotBeNull();
        result.AbilitiesLearned.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Should_Apply_All_Class_Attribute_Bonuses()
    {
        // Arrange
        var customClass = new CharacterClass
        {
            Name = "CustomClass",
            Description = "Test class with all bonuses",
            BonusStrength = 1,
            BonusDexterity = 2,
            BonusConstitution = 1,
            BonusIntelligence = 3,
            BonusWisdom = 2,
            BonusCharisma = 1,
            StartingHealth = 15,
            StartingMana = 18,
            PrimaryAttributes = new List<string> { "Intelligence" },
            StartingEquipmentIds = new List<string>()
        };

        var command = new CreateCharacterCommand
        {
            CharacterName = "TestChar",
            CharacterClass = customClass
        };

        _mediatorMock.Setup(m => m.Send(It.IsAny<InitializeStartingAbilitiesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitializeStartingAbilitiesResult { Success = true, AbilitiesLearned = 0 });

        _mediatorMock.Setup(m => m.Send(It.IsAny<InitializeStartingSpellsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitializeStartingSpellsResult { Success = true, SpellsLearned = 0 });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Character!.Strength.Should().Be(11); // 10 + 1
        result.Character.Dexterity.Should().Be(12); // 10 + 2
        result.Character.Constitution.Should().Be(11); // 10 + 1
        result.Character.Intelligence.Should().Be(13); // 10 + 3
        result.Character.Wisdom.Should().Be(12); // 10 + 2
        result.Character.Charisma.Should().Be(11); // 10 + 1
    }

    // Helper methods to create test character classes
    private CharacterClass CreateWarriorClass()
    {
        return new CharacterClass
        {
            Name = "Warrior",
            Description = "A mighty warrior",
            BonusStrength = 3,
            BonusDexterity = 0,
            BonusConstitution = 2,
            BonusIntelligence = 0,
            BonusWisdom = 0,
            BonusCharisma = 0,
            StartingHealth = 12,
            StartingMana = 4,
            PrimaryAttributes = new List<string> { "Strength", "Constitution" },
            StartingEquipmentIds = new List<string>()
        };
    }

    private CharacterClass CreateMageClass()
    {
        return new CharacterClass
        {
            Name = "Mage",
            Description = "A powerful mage",
            BonusStrength = 0,
            BonusDexterity = 0,
            BonusConstitution = 0,
            BonusIntelligence = 3,
            BonusWisdom = 2,
            BonusCharisma = 0,
            StartingHealth = 6,
            StartingMana = 20,
            PrimaryAttributes = new List<string> { "Intelligence", "Wisdom" },
            StartingEquipmentIds = new List<string>()
        };
    }
}
