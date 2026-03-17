using FluentAssertions;
using MediatR;
using Moq;
using RealmEngine.Core.Features.CharacterCreation.Commands;
using RealmEngine.Core.Features.Progression.Commands;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.CharacterCreation.Commands;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for InitializeStartingSpellsHandler.
/// </summary>
public class InitializeStartingSpellsHandlerTests
{
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
        
        var handler = new InitializeStartingSpellsHandler(mockMediator.Object, NullLogger<InitializeStartingSpellsHandler>.Instance);
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
        
        var handler = new InitializeStartingSpellsHandler(mockMediator.Object, NullLogger<InitializeStartingSpellsHandler>.Instance);
        var character = new Character { Name = "TestMage", ClassName = "Mage" };
        var command = new InitializeStartingSpellsCommand
        {
            Character = character,
            ClassName = "Mage"
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.SpellIds.Should().Contain("magic-missile");
        result.SpellIds.Should().Contain("ray-of-frost");
        result.SpellIds.Should().Contain("shield");
    }

    [Fact]
    public async Task Handle_Should_Return_Success_For_Non_Spellcaster()
    {
        // Arrange
        var mockMediator = new Mock<IMediator>();
        var handler = new InitializeStartingSpellsHandler(mockMediator.Object, NullLogger<InitializeStartingSpellsHandler>.Instance);
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
