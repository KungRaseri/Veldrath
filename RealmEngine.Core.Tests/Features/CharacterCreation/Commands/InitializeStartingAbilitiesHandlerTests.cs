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
/// Tests for InitializeStartingAbilitiesHandler.
/// </summary>
public class InitializeStartingAbilitiesHandlerTests
{
    private static Mock<ICharacterClassRepository> BuildClassRepo(string className, List<string> abilityIds)
    {
        var mock = new Mock<ICharacterClassRepository>();
        mock.Setup(r => r.GetByName(className))
            .Returns(new CharacterClass { Name = className, StartingAbilityIds = abilityIds });
        return mock;
    }

    [Theory]
    [InlineData("Warrior", 3)]
    [InlineData("Rogue", 3)]
    [InlineData("Mage", 3)]
    [InlineData("Cleric", 3)]
    [InlineData("Ranger", 2)]
    [InlineData("Paladin", 2)]
    public async Task Handle_Should_Learn_Correct_Number_Of_Starting_Abilities(string className, int expectedCount)
    {
        // Arrange
        var mockMediator = new Mock<IMediator>();
        
        // Mock successful ability learning
        mockMediator
            .Setup(m => m.Send(It.IsAny<LearnAbilityCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LearnAbilityResult { Success = true, Message = "Learned" });
        
        var abilityIds = Enumerable.Range(1, expectedCount).Select(i => $"ability-{i}").ToList();
        var classRepo = BuildClassRepo(className, abilityIds);
        var handler = new InitializeStartingAbilitiesHandler(mockMediator.Object, classRepo.Object, NullLogger<InitializeStartingAbilitiesHandler>.Instance);
        var character = new Character { Name = "TestHero", ClassName = className };
        var command = new InitializeStartingAbilitiesCommand
        {
            Character = character,
            ClassName = className
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.AbilitiesLearned.Should().Be(expectedCount);
        result.AbilityIds.Should().HaveCount(expectedCount);
        
        // Verify LearnAbilityCommand was called the correct number of times
        mockMediator.Verify(
            m => m.Send(It.IsAny<LearnAbilityCommand>(), It.IsAny<CancellationToken>()), 
            Times.Exactly(expectedCount));
    }

    [Fact]
    public async Task Handle_Should_Return_Success_For_Unknown_Class()
    {
        // Arrange
        var mockMediator = new Mock<IMediator>();
        var classRepo = new Mock<ICharacterClassRepository>();
        classRepo.Setup(r => r.GetByName(It.IsAny<string>())).Returns((CharacterClass?)null);
        var handler = new InitializeStartingAbilitiesHandler(mockMediator.Object, classRepo.Object, NullLogger<InitializeStartingAbilitiesHandler>.Instance);
        var character = new Character { Name = "TestHero", ClassName = "UnknownClass" };
        var command = new InitializeStartingAbilitiesCommand
        {
            Character = character,
            ClassName = "UnknownClass"
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.AbilitiesLearned.Should().Be(0);
        result.AbilityIds.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_Include_Correct_Ability_IDs_For_Warrior()
    {
        // Arrange
        var mockMediator = new Mock<IMediator>();
        mockMediator
            .Setup(m => m.Send(It.IsAny<LearnAbilityCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LearnAbilityResult { Success = true, Message = "Learned" });
        
        var warriorAbilities = new List<string>
        {
            "active/offensive:shield-bash",
            "active/support:second-wind",
            "active/support:battle-cry"
        };
        var classRepo = BuildClassRepo("Warrior", warriorAbilities);
        var handler = new InitializeStartingAbilitiesHandler(mockMediator.Object, classRepo.Object, NullLogger<InitializeStartingAbilitiesHandler>.Instance);
        var character = new Character { Name = "TestWarrior", ClassName = "Warrior" };
        var command = new InitializeStartingAbilitiesCommand
        {
            Character = character,
            ClassName = "Warrior"
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.AbilityIds.Should().Contain("active/offensive:shield-bash");
        result.AbilityIds.Should().Contain("active/support:second-wind");
        result.AbilityIds.Should().Contain("active/support:battle-cry");
    }

    [Fact]
    public async Task Handle_Should_Continue_If_One_Ability_Fails()
    {
        // Arrange
        var mockMediator = new Mock<IMediator>();
        int callCount = 0;
        
        mockMediator
            .Setup(m => m.Send(It.IsAny<LearnAbilityCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                // First call fails, others succeed
                return new LearnAbilityResult 
                { 
                    Success = callCount > 1, 
                    Message = callCount > 1 ? "Learned" : "Failed" 
                };
            });
        
        var classRepo = BuildClassRepo("Warrior", ["ability-1", "ability-2", "ability-3"]);
        var handler = new InitializeStartingAbilitiesHandler(mockMediator.Object, classRepo.Object, NullLogger<InitializeStartingAbilitiesHandler>.Instance);
        var character = new Character { Name = "TestHero", ClassName = "Warrior" };
        var command = new InitializeStartingAbilitiesCommand
        {
            Character = character,
            ClassName = "Warrior"
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.AbilitiesLearned.Should().Be(2); // Only 2 succeeded
        mockMediator.Verify(
            m => m.Send(It.IsAny<LearnAbilityCommand>(), It.IsAny<CancellationToken>()), 
            Times.Exactly(3)); // All 3 were attempted
    }
}
