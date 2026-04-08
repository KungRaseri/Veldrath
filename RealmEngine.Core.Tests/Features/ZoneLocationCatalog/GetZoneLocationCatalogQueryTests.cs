using Moq;
using RealmEngine.Core.Features.ZoneLocationCatalog.Queries;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.ZoneLocationCatalog;

public class GetZoneLocationCatalogQueryHandlerTests
{
    private static ZoneLocationEntry MakeLoc() =>
        new("slug", "Display", "zone", "fenwick-crossing", 1, null, null);

    private static IZoneLocationRepository BuildRepo(IEnumerable<ZoneLocationEntry> data)
    {
        var mock = new Mock<IZoneLocationRepository>();
        mock.Setup(r => r.GetAllAsync()).ReturnsAsync(data.ToList());
        mock.Setup(r => r.GetByTypeKeyAsync(It.IsAny<string>())).ReturnsAsync(data.ToList());
        return mock.Object;
    }

    [Fact]
    public async Task Handle_NoFilter_ReturnsAllFromRepository()
    {
        var data = new List<ZoneLocationEntry> { MakeLoc(), MakeLoc(), MakeLoc() };
        var handler = new GetZoneLocationCatalogQueryHandler(BuildRepo(data));

        var result = await handler.Handle(new GetZoneLocationCatalogQuery(), CancellationToken.None);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_WithFilter_CallsFilteredMethod()
    {
        var mock = new Mock<IZoneLocationRepository>();
        mock.Setup(r => r.GetByTypeKeyAsync(It.IsAny<string>())).ReturnsAsync([MakeLoc()]);
        var handler = new GetZoneLocationCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetZoneLocationCatalogQuery("dungeon"), CancellationToken.None);

        mock.Verify(r => r.GetByTypeKeyAsync(It.IsAny<string>()), Times.Once);
        mock.Verify(r => r.GetAllAsync(), Times.Never);
    }

    [Fact]
    public async Task Handle_WithFilter_PassesFilterValueToRepository()
    {
        var mock = new Mock<IZoneLocationRepository>();
        mock.Setup(r => r.GetByTypeKeyAsync(It.IsAny<string>())).ReturnsAsync([]);
        var handler = new GetZoneLocationCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetZoneLocationCatalogQuery("town"), CancellationToken.None);

        mock.Verify(r => r.GetByTypeKeyAsync("town"), Times.Once);
    }

    [Fact]
    public void Validator_NullFilter_IsValid()
    {
        var result = new GetZoneLocationCatalogQueryValidator().Validate(new GetZoneLocationCatalogQuery(null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_ValidFilter_IsValid()
    {
        var result = new GetZoneLocationCatalogQueryValidator().Validate(new GetZoneLocationCatalogQuery("dungeon"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_EmptyStringFilter_IsInvalid()
    {
        var result = new GetZoneLocationCatalogQueryValidator().Validate(new GetZoneLocationCatalogQuery(""));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_TooLongFilter_IsInvalid()
    {
        var result = new GetZoneLocationCatalogQueryValidator().Validate(new GetZoneLocationCatalogQuery(new string('x', 101)));
        result.IsValid.Should().BeFalse();
    }
}
