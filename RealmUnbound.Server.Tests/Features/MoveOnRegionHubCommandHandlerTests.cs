using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models.Tiled;
using RealmUnbound.Server.Data.Entities;
using RealmUnbound.Server.Data.Repositories;
using RealmUnbound.Server.Features.Zones;

namespace RealmUnbound.Server.Tests.Features;

/// <summary>Unit tests for <see cref="MoveOnRegionHubCommandHandler"/>.</summary>
public class MoveOnRegionHubCommandHandlerTests
{
    private static readonly Guid CharId = Guid.NewGuid();

    private static MoveOnRegionHubCommandHandler MakeHandler(
        ITileMapRepository? tilemapRepo = null,
        IPlayerSessionRepository? sessionRepo = null) =>
        new(
            tilemapRepo  ?? new Mock<ITileMapRepository>().Object,
            sessionRepo  ?? new Mock<IPlayerSessionRepository>().Object,
            NullLogger<MoveOnRegionHubCommandHandler>.Instance);

    /// <summary>Builds a flat walkable region map for test use.</summary>
    private static TiledMap MakeRegionMap(int width = 30, int height = 20, bool addZoneEntry = false, bool addRegionExit = false)
    {
        var size    = width * height;
        var layers  = new List<TiledLayer>
        {
            new()
            {
                Id = 1, Type = "tilelayer", Name = "base",
                Width = width, Height = height,
                Data  = [..Enumerable.Repeat(1, size)],
            },
        };

        if (addZoneEntry)
        {
            layers.Add(new TiledLayer
            {
                Id = 2, Type = "objectgroup", Name = "zones",
                Objects =
                [
                    new TiledObject
                    {
                        Id = 10, Name = "fenwick-crossing",
                        X  = 6 * 16, Y = 5 * 16, Width = 16, Height = 16,
                        Properties =
                        [
                            new TiledProperty { Name = "displayName", Type = "string", Value = JsonSerializer.SerializeToElement("Fenwick Crossing") },
                            new TiledProperty { Name = "minLevel",    Type = "int",    Value = JsonSerializer.SerializeToElement(1) },
                            new TiledProperty { Name = "maxLevel",    Type = "int",    Value = JsonSerializer.SerializeToElement(5) },
                        ],
                    },
                ],
            });
        }

        if (addRegionExit)
        {
            layers.Add(new TiledLayer
            {
                Id = 3, Type = "objectgroup", Name = "region_exits",
                Objects =
                [
                    new TiledObject
                    {
                        Id = 11, Name = "varenmark",
                        X  = 7 * 16, Y = 5 * 16, Width = 16, Height = 16,
                    },
                ],
            });
        }

        return new TiledMap
        {
            Width = width, Height = height, TileWidth = 16, TileHeight = 16,
            Properties =
            [
                new TiledProperty { Name = "regionId",   Type = "string", Value = JsonSerializer.SerializeToElement("thornveil") },
                new TiledProperty { Name = "tilesetKey", Type = "string", Value = JsonSerializer.SerializeToElement("overworld") },
            ],
            Tilesets = [new TiledTileset { FirstGid = 1, Columns = 49, TileWidth = 16, TileHeight = 16 }],
            Layers   = [..layers],
        };
    }

    private static PlayerSession MakeSession(int tileX = 5, int tileY = 5, DateTimeOffset? lastMoved = null) =>
        new()
        {
            CharacterId  = CharId,
            CharacterName = "Aria",
            ConnectionId = "conn-1",
            RegionId     = "thornveil",
            TileX        = tileX,
            TileY        = tileY,
            LastMovedAt  = lastMoved ?? DateTimeOffset.UtcNow.AddSeconds(-1),
        };

    // ── Validation ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("diagonal")]
    [InlineData("")]
    [InlineData("north")]
    public async Task Handle_Returns_Error_For_Invalid_Direction(string direction)
    {
        var result = await MakeHandler()
            .Handle(new MoveOnRegionHubCommand(CharId, 6, 5, direction, "thornveil"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("direction");
    }

    [Fact]
    public async Task Handle_Returns_Error_When_Session_Is_Null()
    {
        var sessionRepo = new Mock<IPlayerSessionRepository>();
        sessionRepo.Setup(r => r.GetByCharacterIdAsync(CharId)).ReturnsAsync((PlayerSession?)null);

        var result = await MakeHandler(sessionRepo: sessionRepo.Object)
            .Handle(new MoveOnRegionHubCommand(CharId, 6, 5, "right", "thornveil"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("session");
    }

    [Theory]
    [InlineData(5, 5, 7, 5)]   // dx=2
    [InlineData(5, 5, 5, 7)]   // dy=2
    [InlineData(5, 5, 6, 6)]   // diagonal
    public async Task Handle_Returns_Error_For_Non_One_Tile_Step(int fromX, int fromY, int toX, int toY)
    {
        var sessionRepo = new Mock<IPlayerSessionRepository>();
        sessionRepo.Setup(r => r.GetByCharacterIdAsync(CharId)).ReturnsAsync(MakeSession(fromX, fromY));

        var result = await MakeHandler(sessionRepo: sessionRepo.Object)
            .Handle(new MoveOnRegionHubCommand(CharId, toX, toY, "right", "thornveil"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("one tile");
    }

    [Fact]
    public async Task Handle_Returns_Error_When_Move_Cooldown_Not_Elapsed()
    {
        var sessionRepo = new Mock<IPlayerSessionRepository>();
        sessionRepo.Setup(r => r.GetByCharacterIdAsync(CharId))
            .ReturnsAsync(MakeSession(lastMoved: DateTimeOffset.UtcNow)); // just moved

        var result = await MakeHandler(sessionRepo: sessionRepo.Object)
            .Handle(new MoveOnRegionHubCommand(CharId, 6, 5, "right", "thornveil"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("fast");
    }

    [Fact]
    public async Task Handle_Returns_Error_When_Target_Tile_Is_Blocked()
    {
        // Build a map where tile local-id 0 (GID 1) has a collision shape.
        // Place that GID at (6,5) so the destination is solid.
        var map = MakeRegionMap();

        // Add a tile definition with a collision objectgroup for local tile id 0
        var solidTileId = 0; // local id
        map.Tilesets[0].Tiles =
        [
            new TiledTileDefinition
            {
                Id = solidTileId,
                ObjectGroup = new TiledLayer
                {
                    Type    = "objectgroup",
                    Objects = [new TiledObject { Id = 1, X = 0, Y = 0, Width = 16, Height = 16 }],
                },
            },
        ];

        // Override the base layer so that tile at index (5*30+6 = 156) uses GID 1 (= firstGid + solidTileId)
        var data = Enumerable.Repeat(2, 30 * 20).ToList(); // GID 2 = open tile
        data[5 * 30 + 6] = 1; // GID 1 = solid tile
        map.Layers[0] = new TiledLayer
        {
            Id = 1, Type = "tilelayer", Name = "base",
            Width = 30, Height = 20, Data = data,
        };

        var tilemapRepo = new Mock<ITileMapRepository>();
        tilemapRepo.Setup(r => r.GetByRegionIdAsync("thornveil")).ReturnsAsync(map);

        var sessionRepo = new Mock<IPlayerSessionRepository>();
        sessionRepo.Setup(r => r.GetByCharacterIdAsync(CharId)).ReturnsAsync(MakeSession(5, 5));

        var result = await MakeHandler(tilemapRepo.Object, sessionRepo.Object)
            .Handle(new MoveOnRegionHubCommand(CharId, 6, 5, "right", "thornveil"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("blocked");
    }

    // ── Success path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Success_Persists_New_Position()
    {
        var tilemapRepo = new Mock<ITileMapRepository>();
        tilemapRepo.Setup(r => r.GetByRegionIdAsync("thornveil")).ReturnsAsync(MakeRegionMap());

        var sessionRepo = new Mock<IPlayerSessionRepository>();
        sessionRepo.Setup(r => r.GetByCharacterIdAsync(CharId)).ReturnsAsync(MakeSession(5, 5));

        await MakeHandler(tilemapRepo.Object, sessionRepo.Object)
            .Handle(new MoveOnRegionHubCommand(CharId, 6, 5, "right", "thornveil"), CancellationToken.None);

        sessionRepo.Verify(r => r.UpdatePositionAsync(CharId, 6, 5), Times.Once);
        sessionRepo.Verify(r => r.UpdateLastMovedAtAsync(CharId, It.IsAny<DateTimeOffset>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Success_Returns_New_TileXY_And_Direction()
    {
        var tilemapRepo = new Mock<ITileMapRepository>();
        tilemapRepo.Setup(r => r.GetByRegionIdAsync("thornveil")).ReturnsAsync(MakeRegionMap());

        var sessionRepo = new Mock<IPlayerSessionRepository>();
        sessionRepo.Setup(r => r.GetByCharacterIdAsync(CharId)).ReturnsAsync(MakeSession(5, 5));

        var result = await MakeHandler(tilemapRepo.Object, sessionRepo.Object)
            .Handle(new MoveOnRegionHubCommand(CharId, 6, 5, "right", "thornveil"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.TileX.Should().Be(6);
        result.TileY.Should().Be(5);
        result.Direction.Should().Be("right");
    }

    [Fact]
    public async Task Handle_Detects_Zone_Entry_At_Destination()
    {
        var map = MakeRegionMap(addZoneEntry: true); // zone at (6, 5)

        var tilemapRepo = new Mock<ITileMapRepository>();
        tilemapRepo.Setup(r => r.GetByRegionIdAsync("thornveil")).ReturnsAsync(map);

        var sessionRepo = new Mock<IPlayerSessionRepository>();
        sessionRepo.Setup(r => r.GetByCharacterIdAsync(CharId)).ReturnsAsync(MakeSession(5, 5));

        var result = await MakeHandler(tilemapRepo.Object, sessionRepo.Object)
            .Handle(new MoveOnRegionHubCommand(CharId, 6, 5, "right", "thornveil"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ZoneEntryTriggered.Should().NotBeNull();
        result.ZoneEntryTriggered!.ZoneSlug.Should().Be("fenwick-crossing");
        result.RegionExitTriggered.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Detects_Region_Exit_At_Destination()
    {
        var map = MakeRegionMap(addRegionExit: true); // exit at (7, 5)

        var tilemapRepo = new Mock<ITileMapRepository>();
        tilemapRepo.Setup(r => r.GetByRegionIdAsync("thornveil")).ReturnsAsync(map);

        var sessionRepo = new Mock<IPlayerSessionRepository>();
        sessionRepo.Setup(r => r.GetByCharacterIdAsync(CharId)).ReturnsAsync(MakeSession(6, 5));

        var result = await MakeHandler(tilemapRepo.Object, sessionRepo.Object)
            .Handle(new MoveOnRegionHubCommand(CharId, 7, 5, "right", "thornveil"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.RegionExitTriggered.Should().NotBeNull();
        result.RegionExitTriggered!.TargetRegionId.Should().Be("varenmark");
        result.ZoneEntryTriggered.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Success_When_Tilemap_Is_Null_No_Zone_Checks()
    {
        // No tilemap → no collision check, no zone/exit checks — should still succeed
        var tilemapRepo = new Mock<ITileMapRepository>();
        tilemapRepo.Setup(r => r.GetByRegionIdAsync("thornveil")).ReturnsAsync((TiledMap?)null);

        var sessionRepo = new Mock<IPlayerSessionRepository>();
        sessionRepo.Setup(r => r.GetByCharacterIdAsync(CharId)).ReturnsAsync(MakeSession(5, 5));

        var result = await MakeHandler(tilemapRepo.Object, sessionRepo.Object)
            .Handle(new MoveOnRegionHubCommand(CharId, 6, 5, "right", "thornveil"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ZoneEntryTriggered.Should().BeNull();
        result.RegionExitTriggered.Should().BeNull();
    }
}
