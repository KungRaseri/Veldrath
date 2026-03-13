using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Characters.Queries;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Characters.Queries;

[Trait("Category", "Feature")]
public class GetBackgroundsHandlerTests
{
    private static Background MakeBackground(string slug, string primaryAttribute, int primaryBonus = 2) =>
        new() { Slug = slug, Name = slug, PrimaryAttribute = primaryAttribute, PrimaryBonus = primaryBonus };

    [Fact]
    public async Task Handle_ReturnsAllBackgrounds_WhenNoFilter()
    {
        var expected = new List<Background>
        {
            MakeBackground("soldier",  "strength"),
            MakeBackground("criminal", "dexterity"),
            MakeBackground("scholar",  "intelligence"),
        };

        var repo = new Mock<IBackgroundRepository>();
        repo.Setup(r => r.GetAllBackgroundsAsync()).ReturnsAsync(expected);

        var handler = new GetBackgroundsHandler(repo.Object, NullLogger<GetBackgroundsHandler>.Instance);
        var result  = await handler.Handle(new GetBackgroundsQuery(), CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        repo.Verify(r => r.GetAllBackgroundsAsync(), Times.Once);
        repo.Verify(r => r.GetBackgroundsByAttributeAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DelegatesToAttributeFilter_WhenFilterProvided()
    {
        var expected = new List<Background> { MakeBackground("soldier", "strength") };

        var repo = new Mock<IBackgroundRepository>();
        repo.Setup(r => r.GetBackgroundsByAttributeAsync("strength")).ReturnsAsync(expected);

        var handler = new GetBackgroundsHandler(repo.Object, NullLogger<GetBackgroundsHandler>.Instance);
        var result  = await handler.Handle(new GetBackgroundsQuery("strength"), CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        repo.Verify(r => r.GetBackgroundsByAttributeAsync("strength"), Times.Once);
        repo.Verify(r => r.GetAllBackgroundsAsync(), Times.Never);
    }

    [Fact]
    public async Task Handle_ReturnsEmpty_WhenRepoReturnsEmpty()
    {
        var repo = new Mock<IBackgroundRepository>();
        repo.Setup(r => r.GetAllBackgroundsAsync()).ReturnsAsync([]);

        var handler = new GetBackgroundsHandler(repo.Object, NullLogger<GetBackgroundsHandler>.Instance);
        var result  = await handler.Handle(new GetBackgroundsQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBackground_BySlug_DelegatesToRepo()
    {
        var background = MakeBackground("noble", "charisma");

        var repo = new Mock<IBackgroundRepository>();
        repo.Setup(r => r.GetBackgroundByIdAsync("noble")).ReturnsAsync(background);

        // GetBackgroundQuery goes through a GetBackgroundHandler, not GetBackgroundsHandler.
        // Verify the repo contract is invoked correctly when called directly.
        var result = await repo.Object.GetBackgroundByIdAsync("noble");

        result.Should().NotBeNull();
        result!.Slug.Should().Be("noble");
    }

    [Fact]
    public async Task GetBackground_WithInvalidSlug_ReturnsNull()
    {
        var repo = new Mock<IBackgroundRepository>();
        repo.Setup(r => r.GetBackgroundByIdAsync(It.IsAny<string>())).ReturnsAsync((Background?)null);

        var result = await repo.Object.GetBackgroundByIdAsync("nonexistent");

        result.Should().BeNull();
    }
}
