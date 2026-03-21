using Moq;
using RealmEngine.Core.Features.WorldLocationCatalog.Queries;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.WorldLocationCatalog;

public class GetWorldLocationCatalogQueryHandlerTests
{
    private static WorldLocationEntry MakeLoc() =>
        new("slug", "Display", "zone", "dungeon", 1, null, null);

    private static IWorldLocationRepository BuildRepo(IEnumerable<WorldLocationEntry> data)
    {
        var mock = new Mock<IWorldLocationRepository>();
        mock.Setup(r => r.GetAllAsync()).ReturnsAsync(data.ToList());
        mock.Setup(r => r.GetByLocationTypeAsync(It.IsAny<string>())).ReturnsAsync(data.ToList());
        return mock.Object;
    }

    [Fact]
    public async Task Handle_NoFilter_ReturnsAllFromRepository()
    {
        var data = new List<WorldLocationEntry> { MakeLoc(), MakeLoc(), MakeLoc() };
        var handler = new GetWorldLocationCatalogQueryHandler(BuildRepo(data));

        var result = await handler.Handle(new GetWorldLocationCatalogQuery(), CancellationToken.None);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_WithFilter_CallsFilteredMethod()
    {
        var mock = new Mock<IWorldLocationRepository>();
        mock.Setup(r => r.GetByLocationTypeAsync(It.IsAny<string>())).ReturnsAsync([MakeLoc()]);
        var handler = new GetWorldLocationCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetWorldLocationCatalogQuery("dungeon"), CancellationToken.None);

        mock.Verify(r => r.GetByLocationTypeAsync(It.IsAny<string>()), Times.Once);
        mock.Verify(r => r.GetAllAsync(), Times.Never);
    }

    [Fact]
    public async Task Handle_WithFilter_PassesFilterValueToRepository()
    {
        var mock = new Mock<IWorldLocationRepository>();
        mock.Setup(r => r.GetByLocationTypeAsync(It.IsAny<string>())).ReturnsAsync([]);
        var handler = new GetWorldLocationCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetWorldLocationCatalogQuery("town"), CancellationToken.None);

        mock.Verify(r => r.GetByLocationTypeAsync("town"), Times.Once);
    }

    [Fact]
    public void Validator_NullFilter_IsValid()
    {
        var result = new GetWorldLocationCatalogQueryValidator().Validate(new GetWorldLocationCatalogQuery(null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_ValidFilter_IsValid()
    {
        var result = new GetWorldLocationCatalogQueryValidator().Validate(new GetWorldLocationCatalogQuery("dungeon"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_EmptyStringFilter_IsInvalid()
    {
        var result = new GetWorldLocationCatalogQueryValidator().Validate(new GetWorldLocationCatalogQuery(""));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_TooLongFilter_IsInvalid()
    {
        var result = new GetWorldLocationCatalogQueryValidator().Validate(new GetWorldLocationCatalogQuery(new string('x', 101)));
        result.IsValid.Should().BeFalse();
    }
}
