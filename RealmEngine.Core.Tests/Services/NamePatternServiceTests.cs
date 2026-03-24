using FluentAssertions;
using Moq;
using RealmEngine.Core.Abstractions;
using RealmEngine.Core.Services;
using RealmEngine.Data.Entities;

namespace RealmEngine.Core.Tests.Services;

[Trait("Category", "Service")]
public class NamePatternServiceTests
{
    private static NamePatternSet MakeSet(string entityPath) =>
        new() { EntityPath = entityPath, DisplayName = entityPath };

    private static Mock<INamePatternRepository> MockRepoWith(params NamePatternSet[] sets)
    {
        var mock = new Mock<INamePatternRepository>();
        mock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(sets.AsEnumerable());
        return mock;
    }

    // Before initialization
    [Fact]
    public void GetPatternSet_ReturnsNull_BeforeInitialization()
    {
        var svc = new NamePatternService(MockRepoWith().Object);

        svc.GetPatternSet("enemies/wolves").Should().BeNull();
    }

    [Fact]
    public void HasPatternSet_ReturnsFalse_BeforeInitialization()
    {
        var svc = new NamePatternService(MockRepoWith().Object);

        svc.HasPatternSet("enemies/wolves").Should().BeFalse();
    }

    // After initialization
    [Fact]
    public async Task InitializeAsync_LoadsPatternSetsFromRepository()
    {
        var set = MakeSet("enemies/wolves");
        var svc = new NamePatternService(MockRepoWith(set).Object);

        await svc.InitializeAsync();

        svc.GetPatternSet("enemies/wolves").Should().BeSameAs(set);
    }

    [Fact]
    public async Task HasPatternSet_ReturnsTrue_ForLoadedPath()
    {
        var svc = new NamePatternService(MockRepoWith(MakeSet("items/weapons")).Object);

        await svc.InitializeAsync();

        svc.HasPatternSet("items/weapons").Should().BeTrue();
    }

    [Fact]
    public async Task HasPatternSet_ReturnsFalse_ForUnknownPath()
    {
        var svc = new NamePatternService(MockRepoWith(MakeSet("items/weapons")).Object);

        await svc.InitializeAsync();

        svc.HasPatternSet("npcs/merchants").Should().BeFalse();
    }

    [Fact]
    public async Task GetPatternSet_ReturnsNull_ForUnknownPath()
    {
        var svc = new NamePatternService(MockRepoWith(MakeSet("items/weapons")).Object);

        await svc.InitializeAsync();

        svc.GetPatternSet("does/not/exist").Should().BeNull();
    }

    [Fact]
    public async Task GetPatternSet_IsCaseInsensitive()
    {
        var set = MakeSet("Enemies/Wolves");
        var svc = new NamePatternService(MockRepoWith(set).Object);

        await svc.InitializeAsync();

        svc.GetPatternSet("enemies/wolves").Should().BeSameAs(set);
        svc.GetPatternSet("ENEMIES/WOLVES").Should().BeSameAs(set);
    }

    [Fact]
    public async Task InitializeAsync_LoadsMultipleSets()
    {
        var sets = new[] { MakeSet("a"), MakeSet("b"), MakeSet("c") };
        var svc = new NamePatternService(MockRepoWith(sets).Object);

        await svc.InitializeAsync();

        svc.HasPatternSet("a").Should().BeTrue();
        svc.HasPatternSet("b").Should().BeTrue();
        svc.HasPatternSet("c").Should().BeTrue();
    }

    // Double-init guard
    [Fact]
    public async Task InitializeAsync_SecondCall_IsNoOp_RepositoryCalledOnce()
    {
        var mock = MockRepoWith(MakeSet("a"));
        var svc = new NamePatternService(mock.Object);

        await svc.InitializeAsync();
        await svc.InitializeAsync();

        mock.Verify(r => r.GetAllAsync(), Times.Once);
    }

    // Exception propagation
    [Fact]
    public async Task InitializeAsync_Throws_WhenRepositoryFails()
    {
        var mock = new Mock<INamePatternRepository>();
        mock.Setup(r => r.GetAllAsync())
            .ThrowsAsync(new InvalidOperationException("DB unavailable"));
        var svc = new NamePatternService(mock.Object);

        await svc.Invoking(s => s.InitializeAsync())
            .Should().ThrowAsync<InvalidOperationException>();
    }
}
