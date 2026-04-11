using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models.Tiled;
using Veldrath.Server.Features.Zones;

namespace Veldrath.Server.Tests.Features;

/// <summary>Unit tests for <see cref="GetZoneTileMapHubCommandHandler"/>.</summary>
public class GetZoneTileMapHubCommandHandlerTests
{
    private static GetZoneTileMapHubCommandHandler MakeHandler(ITileMapRepository repo) =>
        new(repo, NullLogger<GetZoneTileMapHubCommandHandler>.Instance);

    private static TiledMap MakeDefinition(string zoneId, int width = 40, int height = 30)
    {
        var size = width * height;
        return new TiledMap
        {
            Width      = width,
            Height     = height,
            TileWidth  = 16,
            TileHeight = 16,
            Properties =
            [
                new TiledProperty { Name = "zoneId",     Type = "string", Value = JsonSerializer.SerializeToElement(zoneId) },
                new TiledProperty { Name = "tilesetKey", Type = "string", Value = JsonSerializer.SerializeToElement("onebit_packed") },
            ],
            Tilesets = [new TiledTileset { FirstGid = 1, Name = "onebit", TileWidth = 16, TileHeight = 16, Columns = 49 }],
            Layers =
            [
                new TiledLayer
                {
                    Id = 1, Type = "tilelayer", Name = "base",
                    Width = width, Height = height,
                    Data = [..Enumerable.Repeat(1, size)],
                },
                new TiledLayer
                {
                    Id = 2, Type = "objectgroup", Name = "spawns",
                    Objects = [new TiledObject { Id = 1, Type = "spawn", X = 5 * 16, Y = 5 * 16, Point = true }],
                },
                new TiledLayer
                {
                    Id = 3, Type = "objectgroup", Name = "exits",
                    Objects =
                    [
                        new TiledObject
                        {
                            Id = 2, Type = "exit",
                            X = 20 * 16, Y = 29 * 16, Width = 16, Height = 16,
                            Properties = [new TiledProperty { Name = "toZoneId", Type = "string", Value = JsonSerializer.SerializeToElement("other-zone") }],
                        },
                    ],
                },
            ],
        };
    }

    private static TiledProperty StringProp(string name, string value) =>
        new() { Name = name, Type = "string", Value = JsonSerializer.SerializeToElement(value) };

    // ── Not-found tests ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsError_WhenRepoReturnsNull()
    {
        var repo = new Mock<ITileMapRepository>();
        repo.Setup(r => r.GetByZoneIdAsync("unknown-zone")).ReturnsAsync((TiledMap?)null);

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
        // Replace the default exits layer with a specific exit
        definition.Layers.RemoveAll(l => l.Name == "exits");
        definition.Layers.Add(new TiledLayer
        {
            Id = 4, Type = "objectgroup", Name = "exits",
            Objects =
            [
                new TiledObject
                {
                    Id = 5, Type = "exit",
                    X = 15 * 16, Y = 21 * 16, Width = 16, Height = 16,
                    Properties = [StringProp("toZoneId", "greenveil-paths")],
                },
            ],
        });

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
