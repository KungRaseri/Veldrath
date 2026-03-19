using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Characters.Queries;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Characters.Queries;

[Trait("Category", "Feature")]
public class GetBackgroundHandlerTests
{
    private static GetBackgroundHandler CreateHandler(Mock<IBackgroundRepository>? repo = null) =>
        new((repo ?? new Mock<IBackgroundRepository>()).Object, NullLogger<GetBackgroundHandler>.Instance);

    [Fact]
    public async Task Handle_ReturnsBg_WhenFoundInRepository()
    {
        var background = new Background { Slug = "noble", Name = "Noble" };
        var repo = new Mock<IBackgroundRepository>();
        repo.Setup(r => r.GetBackgroundByIdAsync("noble")).ReturnsAsync(background);

        var result = await CreateHandler(repo).Handle(new GetBackgroundQuery("noble"), default);

        result.Should().BeSameAs(background);
        repo.Verify(r => r.GetBackgroundByIdAsync("noble"), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenNotFound()
    {
        var repo = new Mock<IBackgroundRepository>();
        repo.Setup(r => r.GetBackgroundByIdAsync(It.IsAny<string>())).ReturnsAsync((Background?)null);

        var result = await CreateHandler(repo).Handle(new GetBackgroundQuery("nonexistent"), default);

        result.Should().BeNull();
    }
}
