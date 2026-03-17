using FluentAssertions;
using Moq;
using RealmEngine.Core.Features.CharacterCreation.Queries;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.CharacterCreation.Queries;

[Trait("Category", "Feature")]
public class GetCharacterClassHandlerTests
{
    private static CharacterClass MakeClass(string name, string description = "Test class") =>
        new() { Id = $"warrior:{name}", Name = name, DisplayName = name, Description = description };

    [Fact]
    public async Task Handle_ReturnsFound_WhenClassExists()
    {
        var fighter = MakeClass("Fighter");
        var repo = new Mock<ICharacterClassRepository>();
        repo.Setup(r => r.GetByName("Fighter")).Returns(fighter);

        var result = await new GetCharacterClassHandler(repo.Object)
            .Handle(new GetCharacterClassQuery { ClassName = "Fighter" }, CancellationToken.None);

        result.Found.Should().BeTrue();
        result.CharacterClass.Should().NotBeNull();
        result.CharacterClass!.Name.Should().Be("Fighter");
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenClassMissing()
    {
        var repo = new Mock<ICharacterClassRepository>();
        repo.Setup(r => r.GetByName(It.IsAny<string>())).Returns((CharacterClass?)null);

        var result = await new GetCharacterClassHandler(repo.Object)
            .Handle(new GetCharacterClassQuery { ClassName = "InvalidClass" }, CancellationToken.None);

        result.Found.Should().BeFalse();
        result.CharacterClass.Should().BeNull();
    }

    [Theory]
    [InlineData("Fighter")]
    [InlineData("Wizard")]
    [InlineData("Priest")]
    public async Task Handle_ReturnsValidClassData_ForKnownClasses(string className)
    {
        var characterClass = MakeClass(className, $"The {className} class");
        var repo = new Mock<ICharacterClassRepository>();
        repo.Setup(r => r.GetByName(className)).Returns(characterClass);

        var result = await new GetCharacterClassHandler(repo.Object)
            .Handle(new GetCharacterClassQuery { ClassName = className }, CancellationToken.None);

        result.Found.Should().BeTrue();
        result.CharacterClass!.Name.Should().Be(className);
        result.CharacterClass.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_DelegatesToRepo_WithExactClassName()
    {
        var repo = new Mock<ICharacterClassRepository>();
        repo.Setup(r => r.GetByName(It.IsAny<string>())).Returns((CharacterClass?)null);

        await new GetCharacterClassHandler(repo.Object)
            .Handle(new GetCharacterClassQuery { ClassName = "fighter" }, CancellationToken.None);

        repo.Verify(r => r.GetByName("fighter"), Times.Once);
    }
}

