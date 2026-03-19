using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Exploration.Queries;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using SharedBackground = RealmEngine.Shared.Models.Background;

namespace RealmEngine.Core.Tests.Features.Exploration.Queries;

[Trait("Category", "Feature")]
public class GetStartingLocationsHandlerTests : IDisposable
{
    // Each test gets its own named InMemory database so there's no cross-test pollution.
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly Mock<IBackgroundRepository> _bgRepo = new();
    private readonly Mock<IDbContextFactory<ContentDbContext>> _dbFactory = new();

    public GetStartingLocationsHandlerTests()
    {
        _dbFactory.Setup(f => f.CreateDbContext())
                  .Returns(() => CreateDb());
    }

    private ContentDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options);

    private GetStartingLocationsHandler CreateHandler() =>
        new(_bgRepo.Object, _dbFactory.Object, NullLogger<GetStartingLocationsHandler>.Instance);

    private async Task SeedAsync(params WorldLocation[] locations)
    {
        await using var db = CreateDb();
        db.WorldLocations.AddRange(locations);
        await db.SaveChangesAsync();
    }

    private static WorldLocation MakeTown(string slug, string type = "towns") =>
        new()
        {
            Slug = slug,
            TypeKey = type,
            DisplayName = slug,
            LocationType = "settlement",
            IsActive = true,
            Traits = new WorldLocationTraits { IsTown = true, HasMerchant = true },
            Stats = new WorldLocationStats { MinLevel = 1, DangerLevel = 0 }
        };

    private static WorldLocation MakeWilderness(string slug, int minLevel = 5) =>
        new()
        {
            Slug = slug,
            TypeKey = "wilderness",
            DisplayName = slug,
            LocationType = "wilderness",
            IsActive = true,
            Traits = new WorldLocationTraits { IsTown = false },
            Stats = new WorldLocationStats { MinLevel = minLevel, DangerLevel = 3 }
        };

    [Fact]
    public async Task Handle_ReturnsTownLocations_WhenTownsExistInDb()
    {
        await SeedAsync(MakeTown("riverside"), MakeTown("ironpeak"), MakeWilderness("darkwood"));

        var result = await CreateHandler()
            .Handle(new GetStartingLocationsQuery(), default);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(l => l.IsSafeZone);
    }

    [Fact]
    public async Task Handle_FallsBackToAllActiveLocations_WhenNoTownsExist()
    {
        await SeedAsync(MakeWilderness("forest"), MakeWilderness("mountains", minLevel: 10));

        var result = await CreateHandler()
            .Handle(new GetStartingLocationsQuery(), default);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenNoActiveLocationsExist()
    {
        var result = await CreateHandler()
            .Handle(new GetStartingLocationsQuery(), default);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_SetsIsStartingZoneTrue_OnAllResults()
    {
        await SeedAsync(MakeTown("haven"));

        var result = await CreateHandler()
            .Handle(new GetStartingLocationsQuery(), default);

        result.Should().OnlyContain(l => l.IsStartingZone);
    }

    [Fact]
    public async Task Handle_MapsLocationIdCorrectly()
    {
        await SeedAsync(MakeTown("oakvale"));

        var result = await CreateHandler()
            .Handle(new GetStartingLocationsQuery(), default);

        result.Should().ContainSingle().Which.Id.Should().Be("towns:oakvale");
    }

    [Fact]
    public async Task Handle_ReturnsAllLocations_WhenFilterByRecommendedIsFalse()
    {
        await SeedAsync(MakeTown("riverside"), MakeWilderness("darkwood"));
        _bgRepo.Setup(r => r.GetBackgroundByIdAsync(It.IsAny<string>()))
               .ReturnsAsync(new SharedBackground { Name = "Wanderer", RecommendedLocationTypes = ["settlement"] });

        // FilterByRecommended = false → skip background filtering, but towns-first logic still applies.
        // Only towns are returned since towns exist (wilderness is fallback-only).
        var result = await CreateHandler()
            .Handle(new GetStartingLocationsQuery("wanderer", FilterByRecommended: false), default);

        result.Should().ContainSingle(l => l.LocationType == "settlement");
        _bgRepo.Verify(r => r.GetBackgroundByIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_FiltersToRecommendedTypes_WhenBackgroundProvided()
    {
        await SeedAsync(MakeTown("riverside"), MakeWilderness("darkwood"));
        var bg = new SharedBackground
        {
            Name = "Farmer",
            RecommendedLocationTypes = ["settlement"]
        };
        _bgRepo.Setup(r => r.GetBackgroundByIdAsync("farmer")).ReturnsAsync(bg);

        var result = await CreateHandler()
            .Handle(new GetStartingLocationsQuery("farmer"), default);

        // Only "settlement" type locations (towns) should be returned
        result.Should().ContainSingle().Which.LocationType.Should().Be("settlement");
    }

    [Fact]
    public async Task Handle_ReturnsUnfilteredLocations_WhenBackgroundNotFound()
    {
        await SeedAsync(MakeTown("riverside"));
        _bgRepo.Setup(r => r.GetBackgroundByIdAsync(It.IsAny<string>()))
               .ReturnsAsync((SharedBackground?)null);

        // Background not found → falls back to returning the town-filtered list
        var result = await CreateHandler()
            .Handle(new GetStartingLocationsQuery("missing-bg"), default);

        result.Should().ContainSingle().Which.Id.Should().Be("towns:riverside");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Nothing to dispose — InMemory databases are automatically cleaned up when the process ends.
    }
}
