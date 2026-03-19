using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.CharacterCreation.Queries;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.CharacterCreation.Queries;

public class GetAvailableClassesQueryHandlerTests
{
    private static CharacterClass MakeClass(string name, bool isSubclass = false) =>
        new() { Id = $"class:{name}", Name = name, DisplayName = name, Description = $"The {name}", IsSubclass = isSubclass };

    private static GetAvailableClassesQueryHandler CreateHandler(
        IEnumerable<CharacterClass>? allClasses = null,
        Dictionary<string, List<CharacterClass>>? byCategory = null)
    {
        var repo = new Mock<ICharacterClassRepository>();
        repo.Setup(r => r.GetAll()).Returns(allClasses?.ToList() ?? []);
        if (byCategory != null)
        {
            foreach (var kvp in byCategory)
                repo.Setup(r => r.GetClassesByType(kvp.Key)).Returns(kvp.Value);
        }

        var generator = new CharacterClassGenerator(repo.Object, NullLogger<CharacterClassGenerator>.Instance);
        return new GetAvailableClassesQueryHandler(generator, NullLogger<GetAvailableClassesQueryHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ReturnsAllClasses_WhenNoCategoryFilter()
    {
        var classes = new[] { MakeClass("Warrior"), MakeClass("Wizard"), MakeClass("Rogue") };
        var handler = CreateHandler(classes);

        var result = await handler.Handle(new GetAvailableClassesQuery(), default);

        result.Success.Should().BeTrue();
        result.Classes.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_FiltersByCategory_WhenCategoryProvided()
    {
        var warriors = new List<CharacterClass> { MakeClass("Fighter"), MakeClass("Paladin") };
        var handler = CreateHandler(byCategory: new Dictionary<string, List<CharacterClass>>
        {
            ["warriors"] = warriors
        });

        var result = await handler.Handle(new GetAvailableClassesQuery { Category = "warriors" }, default);

        result.Success.Should().BeTrue();
        result.Classes.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoCategoryClassesFound()
    {
        var repo = new Mock<ICharacterClassRepository>();
        repo.Setup(r => r.GetClassesByType("invalid")).Returns([]);
        var generator = new CharacterClassGenerator(repo.Object, NullLogger<CharacterClassGenerator>.Instance);
        var handler = new GetAvailableClassesQueryHandler(generator, NullLogger<GetAvailableClassesQueryHandler>.Instance);

        var result = await handler.Handle(new GetAvailableClassesQuery { Category = "invalid" }, default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("invalid");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoClassesExist()
    {
        var handler = CreateHandler(Enumerable.Empty<CharacterClass>());

        var result = await handler.Handle(new GetAvailableClassesQuery(), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }
}

public class GetBaseClassesHandlerTests
{
    private static CharacterClass MakeClass(bool isSubclass = false) =>
        new() { Id = Guid.NewGuid().ToString(), Name = "TestClass", DisplayName = "Test", Description = "Desc", IsSubclass = isSubclass };

    [Fact]
    public async Task Handle_ReturnsBaseClasses_FromRepo()
    {
        var baseClasses = new List<CharacterClass> { MakeClass(), MakeClass() };
        var repo = new Mock<ICharacterClassRepository>();
        repo.Setup(r => r.GetBaseClasses()).Returns(baseClasses);

        var result = await new GetBaseClassesHandler(repo.Object)
            .Handle(new GetBaseClassesQuery(), default);

        result.BaseClasses.Should().HaveCount(2);
        repo.Verify(r => r.GetBaseClasses(), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenNoBaseClassesExist()
    {
        var repo = new Mock<ICharacterClassRepository>();
        repo.Setup(r => r.GetBaseClasses()).Returns([]);

        var result = await new GetBaseClassesHandler(repo.Object)
            .Handle(new GetBaseClassesQuery(), default);

        result.BaseClasses.Should().BeEmpty();
    }
}

public class GetClassDetailsQueryHandlerTests
{
    private static CharacterClass MakeClass(string name) =>
        new() { Id = $"class:{name}", Name = name, DisplayName = name, Description = $"The {name}" };

    [Fact]
    public async Task Handle_ReturnsClassDetails_WhenClassExists()
    {
        var clazz = MakeClass("Wizard");
        var repo = new Mock<ICharacterClassRepository>();
        repo.Setup(r => r.GetByName("Wizard")).Returns(clazz);

        var generator = new CharacterClassGenerator(repo.Object, NullLogger<CharacterClassGenerator>.Instance);
        var handler = new GetClassDetailsQueryHandler(generator, NullLogger<GetClassDetailsQueryHandler>.Instance);

        var result = await handler.Handle(new GetClassDetailsQuery { ClassName = "Wizard" }, default);

        result.Success.Should().BeTrue();
        result.Class.Should().NotBeNull();
        result.Class!.Name.Should().Be("Wizard");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenClassNotFound()
    {
        var repo = new Mock<ICharacterClassRepository>();
        repo.Setup(r => r.GetByName("UnknownClass")).Returns((CharacterClass?)null);

        var generator = new CharacterClassGenerator(repo.Object, NullLogger<CharacterClassGenerator>.Instance);
        var handler = new GetClassDetailsQueryHandler(generator, NullLogger<GetClassDetailsQueryHandler>.Instance);

        var result = await handler.Handle(new GetClassDetailsQuery { ClassName = "UnknownClass" }, default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }
}

public class GetClassesByTypeHandlerTests
{
    private static CharacterClass MakeClass(string type) =>
        new() { Id = $"{type}:1", Name = $"Test{type}", DisplayName = type, Description = type };

    [Fact]
    public async Task Handle_ReturnsClassesOfSpecifiedType()
    {
        var mages = new List<CharacterClass> { MakeClass("mage"), MakeClass("mage") };
        var repo = new Mock<ICharacterClassRepository>();
        repo.Setup(r => r.GetClassesByType("mage")).Returns(mages);

        var result = await new GetClassesByTypeHandler(repo.Object)
            .Handle(new GetClassesByTypeQuery { ClassType = "mage" }, default);

        result.Classes.Should().HaveCount(2);
        result.ClassType.Should().Be("mage");
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenTypeNotFound()
    {
        var repo = new Mock<ICharacterClassRepository>();
        repo.Setup(r => r.GetClassesByType("unknown")).Returns([]);

        var result = await new GetClassesByTypeHandler(repo.Object)
            .Handle(new GetClassesByTypeQuery { ClassType = "unknown" }, default);

        result.Classes.Should().BeEmpty();
        result.ClassType.Should().Be("unknown");
    }
}

public class GetSubclassesHandlerTests
{
    private static CharacterClass MakeSubclass(string id, string parentId) =>
        new() { Id = id, Name = id, DisplayName = id, Description = id, IsSubclass = true, ParentClassId = parentId };

    [Fact]
    public async Task Handle_ReturnsAllSubclasses_WhenNoParentFilter()
    {
        var subclasses = new List<CharacterClass>
        {
            MakeSubclass("warrior:berserker", "warrior"),
            MakeSubclass("mage:arcanist", "mage"),
        };

        var repo = new Mock<ICharacterClassRepository>();
        repo.Setup(r => r.GetSubclasses()).Returns(subclasses);

        var result = await new GetSubclassesHandler(repo.Object)
            .Handle(new GetSubclassesQuery(), default);

        result.Subclasses.Should().HaveCount(2);
        result.ParentClassId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_FiltersSubclasses_ByParentClassId()
    {
        var warriorSubs = new List<CharacterClass>
        {
            MakeSubclass("warrior:berserker", "warrior"),
            MakeSubclass("warrior:paladin", "warrior"),
        };

        var repo = new Mock<ICharacterClassRepository>();
        repo.Setup(r => r.GetSubclassesForParent("warrior")).Returns(warriorSubs);

        var result = await new GetSubclassesHandler(repo.Object)
            .Handle(new GetSubclassesQuery { ParentClassId = "warrior" }, default);

        result.Subclasses.Should().HaveCount(2);
        result.ParentClassId.Should().Be("warrior");
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenParentHasNoSubclasses()
    {
        var repo = new Mock<ICharacterClassRepository>();
        repo.Setup(r => r.GetSubclassesForParent("rogue")).Returns([]);

        var result = await new GetSubclassesHandler(repo.Object)
            .Handle(new GetSubclassesQuery { ParentClassId = "rogue" }, default);

        result.Subclasses.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_UsesGetSubclasses_WhenParentIdIsEmptyString()
    {
        var repo = new Mock<ICharacterClassRepository>();
        repo.Setup(r => r.GetSubclasses()).Returns([]);

        // Empty string is treated the same as null — delegates to GetSubclasses().
        await new GetSubclassesHandler(repo.Object)
            .Handle(new GetSubclassesQuery { ParentClassId = string.Empty }, default);

        repo.Verify(r => r.GetSubclasses(), Times.Once);
        repo.Verify(r => r.GetSubclassesForParent(It.IsAny<string>()), Times.Never);
    }
}
