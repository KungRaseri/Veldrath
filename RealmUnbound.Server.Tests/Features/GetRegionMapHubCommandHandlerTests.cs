using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models.Tiled;
using RealmUnbound.Server.Features.Zones;

namespace RealmUnbound.Server.Tests.Features;

/// <summary>Unit tests for <see cref="GetRegionMapHubCommandHandler"/>.</summary>
public class GetRegionMapHubCommandHandlerTests
{
    private static GetRegionMapHubCommandHandler MakeHandler(ITileMapRepository repo) =>
        new(repo, NullLogger<GetRegionMapHubCommandHandler>.Instance);

    private static TiledMap MakeRegionMap(string regionId, int width = 30, int height = 20)
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
                new TiledProperty { Name = "regionId",   Type = "string", Value = JsonSerializer.SerializeToElement(regionId) },
                new TiledProperty { Name = "tilesetKey", Type = "string", Value = JsonSerializer.SerializeToElement("overworld") },
            ],
            Tilesets = [new TiledTileset { FirstGid = 1, Name = "overworld", TileWidth = 16, TileHeight = 16, Columns = 49 }],
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
                    Id = 2, Type = "objectgroup", Name = "zones",
                    Objects =
                    [
                        new TiledObject
                        {
                            Id = 1, Name = "fenwick-crossing",
                            X = 5 * 16, Y = 8 * 16, Width = 16, Height = 16,
                            Properties =
                            [
                                new TiledProperty { Name = "displayName", Type = "string", Value = JsonSerializer.SerializeToElement("Fenwick Crossing") },
                                new TiledProperty { Name = "minLevel",    Type = "int",    Value = JsonSerializer.SerializeToElement(1) },
                                new TiledProperty { Name = "maxLevel",    Type = "int",    Value = JsonSerializer.SerializeToElement(5) },
                            ],
                        },
                    ],
                },
                new TiledLayer
                {
                    Id = 3, Type = "objectgroup", Name = "region_exits",
                    Objects =
                    [
                        new TiledObject
                        {
                            Id = 2, Name = "varenmark",
                            X = 29 * 16, Y = 10 * 16, Width = 16, Height = 16,
                        },
                    ],
                },
            ],
        };
    }

    // ── Validation ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Returns_Error_When_RegionId_Is_Empty()
    {
        var repo = new Mock<ITileMapRepository>();
        var result = await MakeHandler(repo.Object)
            .Handle(new GetRegionMapHubCommand(string.Empty), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.RegionMap.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Returns_Error_When_Repo_Returns_Null()
    {
        var repo = new Mock<ITileMapRepository>();
        repo.Setup(r => r.GetByRegionIdAsync("thornveil")).ReturnsAsync((TiledMap?)null);

        var result = await MakeHandler(repo.Object)
            .Handle(new GetRegionMapHubCommand("thornveil"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("thornveil");
    }

    // ── Success projection ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Returns_Success_With_Correct_RegionId()
    {
        var repo = new Mock<ITileMapRepository>();
        repo.Setup(r => r.GetByRegionIdAsync("thornveil"))
            .ReturnsAsync(MakeRegionMap("thornveil"));

        var result = await MakeHandler(repo.Object)
            .Handle(new GetRegionMapHubCommand("thornveil"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.RegionMap.Should().NotBeNull();
        result.RegionMap!.RegionId.Should().Be("thornveil");
    }

    [Fact]
    public async Task Handle_Projects_Width_Height_TileSize()
    {
        var repo = new Mock<ITileMapRepository>();
        repo.Setup(r => r.GetByRegionIdAsync("thornveil"))
            .ReturnsAsync(MakeRegionMap("thornveil", width: 30, height: 20));

        var result = await MakeHandler(repo.Object)
            .Handle(new GetRegionMapHubCommand("thornveil"), CancellationToken.None);

        result.RegionMap!.Width.Should().Be(30);
        result.RegionMap.Height.Should().Be(20);
        result.RegionMap.TileSize.Should().Be(16);
    }

    [Fact]
    public async Task Handle_Projects_Zone_Entries()
    {
        var repo = new Mock<ITileMapRepository>();
        repo.Setup(r => r.GetByRegionIdAsync("thornveil"))
            .ReturnsAsync(MakeRegionMap("thornveil"));

        var result = await MakeHandler(repo.Object)
            .Handle(new GetRegionMapHubCommand("thornveil"), CancellationToken.None);

        result.RegionMap!.ZoneEntries.Should().ContainSingle(e =>
            e.ZoneSlug == "fenwick-crossing" &&
            e.DisplayName == "Fenwick Crossing" &&
            e.MinLevel == 1 &&
            e.MaxLevel == 5);
    }

    [Fact]
    public async Task Handle_Projects_Region_Exits()
    {
        var repo = new Mock<ITileMapRepository>();
        repo.Setup(r => r.GetByRegionIdAsync("thornveil"))
            .ReturnsAsync(MakeRegionMap("thornveil"));

        var result = await MakeHandler(repo.Object)
            .Handle(new GetRegionMapHubCommand("thornveil"), CancellationToken.None);

        result.RegionMap!.RegionExits.Should().ContainSingle(e => e.TargetRegionId == "varenmark");
    }

    [Fact]
    public async Task Handle_Falls_Back_To_Request_RegionId_When_Map_Property_Missing()
    {
        // A map with no "regionId" property should fall back to the request value
        var map = new TiledMap
        {
            Width = 10, Height = 10, TileWidth = 16, TileHeight = 16,
            Properties = [],
            Tilesets   = [new TiledTileset { FirstGid = 1, Columns = 49 }],
            Layers     = [new TiledLayer { Id = 1, Type = "tilelayer", Width = 10, Height = 10, Data = [..Enumerable.Repeat(1, 100)] }],
        };

        var repo = new Mock<ITileMapRepository>();
        repo.Setup(r => r.GetByRegionIdAsync("fallback-region")).ReturnsAsync(map);

        var result = await MakeHandler(repo.Object)
            .Handle(new GetRegionMapHubCommand("fallback-region"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.RegionMap!.RegionId.Should().Be("fallback-region");
    }
}
