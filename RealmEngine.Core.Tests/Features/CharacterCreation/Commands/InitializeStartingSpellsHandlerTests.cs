using FluentAssertions;
using MediatR;
using Moq;
using RealmEngine.Core.Features.CharacterCreation.Commands;
using RealmEngine.Core.Features.Progression.Commands;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealmEngine.Core.Tests.Features.CharacterCreation.Commands;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for InitializeStartingSpellsHandler.
/// </summary>
public class InitializeStartingSpellsHandlerTests
{
    private static Mock<ICharacterClassRepository> BuildClassRepo(string className, List<string> spellIds)
    {
        var mock = new Mock<ICharacterClassRepository>();
        mock.Setup(r => r.GetByName(className))
            .Returns(new CharacterClass { Name = className, StartingSpellIds = spellIds });
        return mock;
    }

    [Theory]
    [InlineData("Mage", 3)]
    [InlineData("Cleric", 3)]
    [InlineData("Paladin", 2)]
    [InlineData("Warrior", 0)]  // Non-spellcaster
    [InlineData("Rogue", 0)]    // Non-spellcaster
    public async Task Handle_Should_Learn_Correct_Number_Of_Starting_Spells(string className, int expectedCount)
    {
        // Arrange
        var mockMediator = new Mock<IMediator>();
        
        // Mock successful spell learning
        mockMediator
            .Setup(m => m.Send(It.IsAny<LearnSpellCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LearnSpellResult { Success = true, Message = "Learned" });
        
        var spellIds = Enumerable.Range(1, expectedCount).Select(i => $"spell-{i}").ToList();
        var classRepo = BuildClassRepo(className, spellIds);
        var handler = new InitializeStartingSpellsHandler(mockMediator.Object, classRepo.Object, NullLogger<InitializeStartingSpellsHandler>.Instance);
        var character = new Character { Name = "TestHero", ClassName = className };
        var command = new InitializeStartingSpellsCommand
        {
            Character = character,
            ClassName = className
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.SpellsLearned.Should().Be(expectedCount);
        result.SpellIds.Should().HaveCount(expectedCount);
        
        // Verify LearnSpellCommand was called the correct number of times
        mockMediator.Verify(
            m => m.Send(It.IsAny<LearnSpellCommand>(), It.IsAny<CancellationToken>()), 
            Times.Exactly(expectedCount));
    }

    [Fact]
    public async Task Handle_Should_Include_Correct_Spell_IDs_For_Mage()
    {
        // Arrange
        var mockMediator = new Mock<IMediator>();
        mockMediator
            .Setup(m => m.Send(It.IsAny<LearnSpellCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LearnSpellResult { Success = true, Message = "Learned" });
        
        var mageSpells = new List<string>
        {
            "@spells/arcane/offensive:magic-missile",
            "@spells/arcane/offensive:fire-bolt",
            "@spells/arcane/defensive:shield"
        };
        var classRepo = BuildClassRepo("Mage", mageSpells);
        var handler = new InitializeStartingSpellsHandler(mockMediator.Object, classRepo.Object, NullLogger<InitializeStartingSpellsHandler>.Instance);
        var character = new Character { Name = "TestMage", ClassName = "Mage" };
        var command = new InitializeStartingSpellsCommand
        {
            Character = character,
            ClassName = "Mage"
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.SpellIds.Should().Contain("@spells/arcane/offensive:magic-missile");
        result.SpellIds.Should().Contain("@spells/arcane/offensive:fire-bolt");
        result.SpellIds.Should().Contain("@spells/arcane/defensive:shield");
    }

    [Fact]
    public async Task Handle_Should_Return_Success_For_Non_Spellcaster()
    {
        // Arrange
        var mockMediator = new Mock<IMediator>();
        var classRepo = BuildClassRepo("Warrior", []);
        var handler = new InitializeStartingSpellsHandler(mockMediator.Object, classRepo.Object, NullLogger<InitializeStartingSpellsHandler>.Instance);
        var character = new Character { Name = "TestWarrior", ClassName = "Warrior" };
        var command = new InitializeStartingSpellsCommand
        {
            Character = character,
            ClassName = "Warrior"
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.SpellsLearned.Should().Be(0);
        result.SpellIds.Should().BeEmpty();
        result.Message.Should().Contain("No starting spells");
    }
}
