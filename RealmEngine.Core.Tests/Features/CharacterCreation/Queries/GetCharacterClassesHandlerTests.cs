using FluentAssertions;
using Moq;
using RealmEngine.Core.Features.CharacterCreation.Queries;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.CharacterCreation.Queries;

[Trait("Category", "Feature")]
public class GetCharacterClassesHandlerTests
{
    private static CharacterClass MakeClass(
        string name, bool isSubclass = false, string? parentClassId = null) =>
        new()
        {
            Id          = $"warrior:{name}",
            Name        = name,
            DisplayName = name,
            Description = $"The {name} class",
            IsSubclass  = isSubclass,
            ParentClassId = parentClassId,
        };

    [Fact]
    public async Task Handle_ReturnsAllClasses_FromRepo()
    {
        var classes = new List<CharacterClass>
        {
            MakeClass("Fighter"),
            MakeClass("Priest"),
            MakeClass("Wizard"),
        };

        var repo = new Mock<ICharacterClassRepository>();
        repo.Setup(r => r.GetAllClasses()).Returns(classes);

        var result = await new GetCharacterClassesHandler(repo.Object)
            .Handle(new GetCharacterClassesQuery(), CancellationToken.None);

        result.Classes.Should().BeEquivalentTo(classes);
        repo.Verify(r => r.GetAllClasses(), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenRepoIsEmpty()
    {
        var repo = new Mock<ICharacterClassRepository>();
        repo.Setup(r => r.GetAllClasses()).Returns([]);

        var result = await new GetCharacterClassesHandler(repo.Object)
            .Handle(new GetCharacterClassesQuery(), CancellationToken.None);

        result.Should().NotBeNull();
        result.Classes.Should().NotBeNull();
        result.Classes.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsResult_WithSubclassInfo()
    {
        var paladin = MakeClass("Paladin", isSubclass: true, parentClassId: "warrior:Fighter");
        var repo = new Mock<ICharacterClassRepository>();
        repo.Setup(r => r.GetAllClasses()).Returns([paladin]);

        var result = await new GetCharacterClassesHandler(repo.Object)
            .Handle(new GetCharacterClassesQuery(), CancellationToken.None);

        var returnedPaladin = result.Classes.Single();
        returnedPaladin.IsSubclass.Should().BeTrue();
        returnedPaladin.ParentClassId.Should().Be("warrior:Fighter");
    }

    [Fact]
    public async Task Handle_ReturnsClasses_WithValidNameAndDescription()
    {
        var classes = new List<CharacterClass>
        {
            MakeClass("Fighter"),
            MakeClass("Wizard"),
        };
        var repo = new Mock<ICharacterClassRepository>();
        repo.Setup(r => r.GetAllClasses()).Returns(classes);

        var result = await new GetCharacterClassesHandler(repo.Object)
            .Handle(new GetCharacterClassesQuery(), CancellationToken.None);

        result.Classes.Should().AllSatisfy(c =>
        {
            c.Name.Should().NotBeNullOrEmpty();
            c.Description.Should().NotBeNullOrEmpty();
        });
    }
}
