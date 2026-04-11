using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models.Tiled;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Zones;

namespace Veldrath.Server.Tests.Features;

/// <summary>Unit tests for <see cref="ExitZoneHubCommandHandler"/>.</summary>
public class ExitZoneHubCommandHandlerTests
{
    private static readonly Guid CharId = Guid.NewGuid();

    private static ExitZoneHubCommandHandler MakeHandler(
        ITileMapRepository? tilemapRepo = null,
        IPlayerSessionRepository? sessionRepo = null) =>
        new(
            tilemapRepo  ?? new Mock<ITileMapRepository>().Object,
            sessionRepo  ?? new Mock<IPlayerSessionRepository>().Object,
            NullLogger<ExitZoneHubCommandHandler>.Instance);

    private static TiledMap MakeRegionMap(int zoneEntryTileX = 5, int zoneEntryTileY = 8, string zoneSlug = "fenwick-crossing") =>
        new()
        {
            Width = 30, Height = 20, TileWidth = 16, TileHeight = 16,
            Properties =
            [
                new TiledProperty { Name = "regionId",   Type = "string", Value = JsonSerializer.SerializeToElement("thornveil") },
                new TiledProperty { Name = "tilesetKey", Type = "string", Value = JsonSerializer.SerializeToElement("overworld") },
            ],
            Tilesets = [new TiledTileset { FirstGid = 1, Columns = 49, TileWidth = 16, TileHeight = 16 }],
            Layers =
            [
                new TiledLayer
                {
                    Id = 1, Type = "tilelayer", Name = "base",
                    Width = 30, Height = 20,
                    Data  = [..Enumerable.Repeat(1, 600)],
                },
                new TiledLayer
                {
                    Id = 2, Type = "objectgroup", Name = "zones",
                    Objects =
                    [
                        new TiledObject
                        {
                            Id = 10, Name = zoneSlug,
                            X  = zoneEntryTileX * 16, Y = zoneEntryTileY * 16,
                            Width = 16, Height = 16,
                            Properties =
                            [
                                new TiledProperty { Name = "displayName", Type = "string", Value = JsonSerializer.SerializeToElement("Fenwick Crossing") },
                                new TiledProperty { Name = "minLevel",    Type = "int",    Value = JsonSerializer.SerializeToElement(1) },
                                new TiledProperty { Name = "maxLevel",    Type = "int",    Value = JsonSerializer.SerializeToElement(5) },
                            ],
                        },
                    ],
                },
            ],
        };

    // ── Success — known zone ───────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Returns_Zone_Entry_Tile_When_Zone_Found_On_Map()
    {
        var tilemapRepo = new Mock<ITileMapRepository>();
        tilemapRepo.Setup(r => r.GetByRegionIdAsync("thornveil"))
            .ReturnsAsync(MakeRegionMap(zoneEntryTileX: 5, zoneEntryTileY: 8, zoneSlug: "fenwick-crossing"));

        var sessionRepo = new Mock<IPlayerSessionRepository>();

        var result = await MakeHandler(tilemapRepo.Object, sessionRepo.Object)
            .Handle(new ExitZoneHubCommand(CharId, "thornveil", "fenwick-crossing"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.TileX.Should().Be(5);
        result.TileY.Should().Be(8);
    }

    [Fact]
    public async Task Handle_Returns_Success_And_Clears_ZoneId()
    {
        var tilemapRepo = new Mock<ITileMapRepository>();
        tilemapRepo.Setup(r => r.GetByRegionIdAsync("thornveil"))
            .ReturnsAsync(MakeRegionMap(zoneSlug: "fenwick-crossing"));

        var sessionRepo = new Mock<IPlayerSessionRepository>();

        var result = await MakeHandler(tilemapRepo.Object, sessionRepo.Object)
            .Handle(new ExitZoneHubCommand(CharId, "thornveil", "fenwick-crossing"), CancellationToken.None);

        result.Success.Should().BeTrue();
        sessionRepo.Verify(r => r.SetZoneAsync(CharId, null), Times.Once);
    }

    [Fact]
    public async Task Handle_Persists_Position_On_Success()
    {
        var tilemapRepo = new Mock<ITileMapRepository>();
        tilemapRepo.Setup(r => r.GetByRegionIdAsync("thornveil"))
            .ReturnsAsync(MakeRegionMap(zoneEntryTileX: 5, zoneEntryTileY: 8, zoneSlug: "fenwick-crossing"));

        var sessionRepo = new Mock<IPlayerSessionRepository>();

        await MakeHandler(tilemapRepo.Object, sessionRepo.Object)
            .Handle(new ExitZoneHubCommand(CharId, "thornveil", "fenwick-crossing"), CancellationToken.None);

        sessionRepo.Verify(r => r.UpdatePositionAsync(CharId, 5, 8), Times.Once);
    }

    // ── Fallback — zone not found on map ──────────────────────────────────────

    [Fact]
    public async Task Handle_Falls_Back_To_1_1_When_Zone_Not_Found_On_Map()
    {
        var tilemapRepo = new Mock<ITileMapRepository>();
        tilemapRepo.Setup(r => r.GetByRegionIdAsync("thornveil"))
            .ReturnsAsync(MakeRegionMap(zoneSlug: "other-zone")); // wrong slug

        var sessionRepo = new Mock<IPlayerSessionRepository>();

        var result = await MakeHandler(tilemapRepo.Object, sessionRepo.Object)
            .Handle(new ExitZoneHubCommand(CharId, "thornveil", "fenwick-crossing"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.TileX.Should().Be(1);
        result.TileY.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Falls_Back_To_1_1_When_Region_Map_Is_Null()
    {
        var tilemapRepo = new Mock<ITileMapRepository>();
        tilemapRepo.Setup(r => r.GetByRegionIdAsync("thornveil")).ReturnsAsync((TiledMap?)null);

        var sessionRepo = new Mock<IPlayerSessionRepository>();

        var result = await MakeHandler(tilemapRepo.Object, sessionRepo.Object)
            .Handle(new ExitZoneHubCommand(CharId, "thornveil", "fenwick-crossing"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.TileX.Should().Be(1);
        result.TileY.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Still_Clears_Zone_Even_When_Map_Is_Null()
    {
        var tilemapRepo = new Mock<ITileMapRepository>();
        tilemapRepo.Setup(r => r.GetByRegionIdAsync("thornveil")).ReturnsAsync((TiledMap?)null);

        var sessionRepo = new Mock<IPlayerSessionRepository>();

        await MakeHandler(tilemapRepo.Object, sessionRepo.Object)
            .Handle(new ExitZoneHubCommand(CharId, "thornveil", "fenwick-crossing"), CancellationToken.None);

        sessionRepo.Verify(r => r.SetZoneAsync(CharId, null), Times.Once);
    }

    // ── Case-insensitive zone match ────────────────────────────────────────────

    [Fact]
    public async Task Handle_Matches_Zone_Slug_Case_Insensitively()
    {
        var tilemapRepo = new Mock<ITileMapRepository>();
        tilemapRepo.Setup(r => r.GetByRegionIdAsync("thornveil"))
            .ReturnsAsync(MakeRegionMap(zoneEntryTileX: 5, zoneEntryTileY: 8, zoneSlug: "Fenwick-Crossing"));

        var sessionRepo = new Mock<IPlayerSessionRepository>();

        var result = await MakeHandler(tilemapRepo.Object, sessionRepo.Object)
            .Handle(new ExitZoneHubCommand(CharId, "thornveil", "fenwick-crossing"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.TileX.Should().Be(5);
        result.TileY.Should().Be(8);
    }
}
