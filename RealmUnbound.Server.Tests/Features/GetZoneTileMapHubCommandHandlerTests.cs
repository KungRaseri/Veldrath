using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using RealmUnbound.Server.Features.Zones;

namespace RealmUnbound.Server.Tests.Features;

/// <summary>Unit tests for <see cref="GetZoneTileMapHubCommandHandler"/>.</summary>
public class GetZoneTileMapHubCommandHandlerTests
{
    private static GetZoneTileMapHubCommandHandler MakeHandler(ITileMapRepository repo) =>
        new(repo, NullLogger<GetZoneTileMapHubCommandHandler>.Instance);

    private static TileMapDefinition MakeDefinition(string zoneId, int width = 40, int height = 30) =>
        new()
        {
            ZoneId        = zoneId,
            TilesetKey    = "onebit_packed",
            Width         = width,
            Height        = height,
            TileSize      = 16,
            Layers        = [new TileLayerDefinition { Name = "base", Data = new int[width * height] }],
            CollisionMask = new bool[width * height],
            FogMask       = new bool[width * height],
            SpawnPoints   = [new SpawnPointDefinition { TileX = 5, TileY = 5 }],
            ExitTiles     = [new ExitTileDefinition { TileX = 20, TileY = 29, ToZoneId = "other-zone" }],
        };

    // ── Not-found tests ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsError_WhenRepoReturnsNull()
    {
        var repo = new Mock<ITileMapRepository>();
        repo.Setup(r => r.GetByZoneIdAsync("unknown-zone")).ReturnsAsync((TileMapDefinition?)null);

        var result = await MakeHandler(repo.Object)
            .Handle(new GetZoneTileMapHubCommand("unknown-zone"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.TileMap.Should().BeNull();
        result.ErrorMessage.Should().Contain("unknown-zone");
    }

    // ── Success tests ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsSuccess_WithCorrectZoneId()
    {
        var repo = new Mock<ITileMapRepository>();
        repo.Setup(r => r.GetByZoneIdAsync("fenwick-crossing"))
            .ReturnsAsync(MakeDefinition("fenwick-crossing"));

        var result = await MakeHandler(repo.Object)
            .Handle(new GetZoneTileMapHubCommand("fenwick-crossing"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.TileMap.Should().NotBeNull();
        result.TileMap!.ZoneId.Should().Be("fenwick-crossing");
    }

    [Fact]
    public async Task Handle_MapsWidthHeightTileSize_Correctly()
    {
        var repo = new Mock<ITileMapRepository>();
        repo.Setup(r => r.GetByZoneIdAsync("soddenfen"))
            .ReturnsAsync(MakeDefinition("soddenfen", width: 50, height: 38));

        var result = await MakeHandler(repo.Object)
            .Handle(new GetZoneTileMapHubCommand("soddenfen"), CancellationToken.None);

        result.TileMap!.Width.Should().Be(50);
        result.TileMap.Height.Should().Be(38);
        result.TileMap.TileSize.Should().Be(16);
    }

    [Fact]
    public async Task Handle_MapsLayers_IntoDto()
    {
        var definition = MakeDefinition("fenwick-crossing");
        var repo = new Mock<ITileMapRepository>();
        repo.Setup(r => r.GetByZoneIdAsync(It.IsAny<string>())).ReturnsAsync(definition);

        var result = await MakeHandler(repo.Object)
            .Handle(new GetZoneTileMapHubCommand("fenwick-crossing"), CancellationToken.None);

        result.TileMap!.Layers.Should().HaveCount(1);
        result.TileMap.Layers[0].Name.Should().Be("base");
    }

    [Fact]
    public async Task Handle_MapsExitTiles_IntoDto()
    {
        var definition = MakeDefinition("fenwick-crossing");
        definition.ExitTiles = [new ExitTileDefinition { TileX = 15, TileY = 21, ToZoneId = "greenveil-paths" }];
        var repo = new Mock<ITileMapRepository>();
        repo.Setup(r => r.GetByZoneIdAsync(It.IsAny<string>())).ReturnsAsync(definition);

        var result = await MakeHandler(repo.Object)
            .Handle(new GetZoneTileMapHubCommand("fenwick-crossing"), CancellationToken.None);

        result.TileMap!.ExitTiles.Should().HaveCount(1);
        result.TileMap.ExitTiles[0].ToZoneId.Should().Be("greenveil-paths");
        result.TileMap.ExitTiles[0].TileX.Should().Be(15);
    }

    [Fact]
    public async Task Handle_MapsSpawnPoints_IntoDto()
    {
        var definition = MakeDefinition("fenwick-crossing");
        var repo = new Mock<ITileMapRepository>();
        repo.Setup(r => r.GetByZoneIdAsync(It.IsAny<string>())).ReturnsAsync(definition);

        var result = await MakeHandler(repo.Object)
            .Handle(new GetZoneTileMapHubCommand("fenwick-crossing"), CancellationToken.None);

        result.TileMap!.SpawnPoints.Should().HaveCount(1);
        result.TileMap.SpawnPoints[0].TileX.Should().Be(5);
        result.TileMap.SpawnPoints[0].TileY.Should().Be(5);
    }

    [Fact]
    public async Task Handle_MapsTilesetKey_IntoDto()
    {
        var repo = new Mock<ITileMapRepository>();
        repo.Setup(r => r.GetByZoneIdAsync(It.IsAny<string>()))
            .ReturnsAsync(MakeDefinition("fenwick-crossing"));

        var result = await MakeHandler(repo.Object)
            .Handle(new GetZoneTileMapHubCommand("fenwick-crossing"), CancellationToken.None);

        result.TileMap!.TilesetKey.Should().Be("onebit_packed");
    }
}
