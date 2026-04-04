using FluentAssertions;
using FluentValidation.TestHelper;
using Moq;
using RealmEngine.Core.Features.Tilemap.Queries;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Tilemap;

/// <summary>
/// Unit tests for <see cref="GetTileMapQueryHandler"/> and <see cref="GetTileMapQueryValidator"/>.
/// </summary>
[Trait("Category", "Feature")]
public class GetTileMapQueryHandlerTests
{
    private static GetTileMapQueryHandler CreateHandler(ITileMapRepository repository) =>
        new(repository);

    // ── Handler tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsTileMap_WhenRepositoryReturnsDefinition()
    {
        var expected = new TileMapDefinition
        {
            ZoneId = "fenwick-crossing",
            TilesetKey = "roguelike_base",
            Width = 40,
            Height = 30
        };
        var repo = new Mock<ITileMapRepository>();
        repo.Setup(r => r.GetByZoneIdAsync("fenwick-crossing")).ReturnsAsync(expected);

        var result = await CreateHandler(repo.Object).Handle(
            new GetTileMapQuery("fenwick-crossing"), CancellationToken.None);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenRepositoryReturnsNull()
    {
        var repo = new Mock<ITileMapRepository>();
        repo.Setup(r => r.GetByZoneIdAsync(It.IsAny<string>())).ReturnsAsync((TileMapDefinition?)null);

        var result = await CreateHandler(repo.Object).Handle(
            new GetTileMapQuery("unknown-zone"), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_PassesZoneIdToRepository()
    {
        const string zoneId = "barrow-deeps";
        var repo = new Mock<ITileMapRepository>();
        repo.Setup(r => r.GetByZoneIdAsync(zoneId)).ReturnsAsync((TileMapDefinition?)null);

        await CreateHandler(repo.Object).Handle(new GetTileMapQuery(zoneId), CancellationToken.None);

        repo.Verify(r => r.GetByZoneIdAsync(zoneId), Times.Once);
    }

    [Theory]
    [InlineData("fenwick-crossing")]
    [InlineData("barrow-deeps")]
    [InlineData("kaldrek-maw")]
    [InlineData("tidewrack-flats")]
    public async Task Handle_ForwardsCorrectZoneId_ForVariousZones(string zoneId)
    {
        var map = new TileMapDefinition { ZoneId = zoneId };
        var repo = new Mock<ITileMapRepository>();
        repo.Setup(r => r.GetByZoneIdAsync(zoneId)).ReturnsAsync(map);

        var result = await CreateHandler(repo.Object).Handle(
            new GetTileMapQuery(zoneId), CancellationToken.None);

        result.Should().NotBeNull();
        result!.ZoneId.Should().Be(zoneId);
    }
}

/// <summary>
/// Unit tests for <see cref="GetTileMapQueryValidator"/>.
/// </summary>
[Trait("Category", "Feature")]
public class GetTileMapQueryValidatorTests
{
    private readonly GetTileMapQueryValidator _validator = new();

    [Fact]
    public void Validate_HasNoErrors_WhenZoneIdIsValid()
    {
        var result = _validator.TestValidate(new GetTileMapQuery("fenwick-crossing"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_HasError_WhenZoneIdIsEmpty()
    {
        var result = _validator.TestValidate(new GetTileMapQuery(""));
        result.ShouldHaveValidationErrorFor(q => q.ZoneId);
    }

    [Fact]
    public void Validate_HasError_WhenZoneIdExceeds128Characters()
    {
        var longId = new string('x', 129);
        var result = _validator.TestValidate(new GetTileMapQuery(longId));
        result.ShouldHaveValidationErrorFor(q => q.ZoneId);
    }

    [Fact]
    public void Validate_HasNoErrors_WhenZoneIdIsExactly128Characters()
    {
        var maxId = new string('x', 128);
        var result = _validator.TestValidate(new GetTileMapQuery(maxId));
        result.ShouldNotHaveValidationErrorFor(q => q.ZoneId);
    }
}
