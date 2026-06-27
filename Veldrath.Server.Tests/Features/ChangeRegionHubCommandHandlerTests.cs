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

/// <summary>Unit tests for <see cref="ChangeRegionHubCommandHandler"/>.</summary>
public class ChangeRegionHubCommandHandlerTests
{
    private static readonly Guid CharId = Guid.NewGuid();
    private const string CurrentRegionId = "varenmark";
    private const string TargetRegionId = "greymoor";

    private static ChangeRegionHubCommandHandler MakeHandler(
        ITileMapRepository? tilemapRepo = null,
        IPlayerSessionRepository? sessionRepo = null) =>
        new(
            tilemapRepo ?? new Mock<ITileMapRepository>().Object,
            sessionRepo ?? new Mock<IPlayerSessionRepository>().Object,
            NullLogger<ChangeRegionHubCommandHandler>.Instance);

    /// <summary>
    /// Builds a <see cref="TiledMap"/> for the target region, optionally including a
    /// <c>region_exits</c> objectgroup layer with a single exit object.
    /// </summary>
    /// <param name="width">Map width in tiles.</param>
    /// <param name="height">Map height in tiles.</param>
    /// <param name="exitTileX">Tile column of the exit object when <paramref name="exitTargetRegion"/> is set.</param>
    /// <param name="exitTileY">Tile row of the exit object when <paramref name="exitTargetRegion"/> is set.</param>
    /// <param name="exitTargetRegion">
    /// When non-<see langword="null"/>, a <c>region_exits</c> object is added whose <c>Name</c>
    /// equals this value. This represents the "door back" from the target region.
    /// </param>
    private static TiledMap MakeRegionMap(
        int width = 30, int height = 20,
        int exitTileX = 0, int exitTileY = 10,
        string? exitTargetRegion = null)
    {
        var layers = new List<TiledLayer>
        {
            new()
            {
                Name = "ground",
                Type = "tilelayer",
                Width = width,
                Height = height,
                Data = Enumerable.Repeat(1, width * height).ToList(),
            },
        };

        if (exitTargetRegion is not null)
        {
            layers.Add(new TiledLayer
            {
                Name = "region_exits",
                Type = "objectgroup",
                Objects =
                [
                    new TiledObject
                    {
                        Name = exitTargetRegion,
                        X = exitTileX * 16,
                        Y = exitTileY * 16,
                        Width = 16,
                        Height = 16,
                    },
                ],
            });
        }

        return new TiledMap
        {
            Width = width,
            Height = height,
            TileWidth = 16,
            TileHeight = 16,
            Properties =
            [
                new TiledProperty { Name = "regionId",   Type = "string", Value = JsonSerializer.SerializeToElement(TargetRegionId) },
                new TiledProperty { Name = "tilesetKey", Type = "string", Value = JsonSerializer.SerializeToElement("roguelike_base") },
            ],
            Tilesets = [new TiledTileset { FirstGid = 1, Columns = 49 }],
            Layers = [.. layers],
        };
    }

    // ── Validation / failure paths ─────────────────────────────────────────────

    [Fact]
    public async Task Handle_Returns_Error_When_TargetRegionId_Is_Empty()
    {
        var handler = MakeHandler();
        var command = new ChangeRegionHubCommand(CharId, CurrentRegionId, string.Empty);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty");
    }

    [Fact]
    public async Task Handle_Returns_Error_When_TargetRegionMap_Not_Found()
    {
        var tilemapRepo = new Mock<ITileMapRepository>();
        tilemapRepo.Setup(r => r.GetByRegionIdAsync(TargetRegionId))
            .ReturnsAsync((TiledMap?)null);
        var handler = MakeHandler(tilemapRepo: tilemapRepo.Object);
        var command = new ChangeRegionHubCommand(CharId, CurrentRegionId, TargetRegionId);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
    }

    // ── Success — spawn inward from edges ──────────────────────────────────────

    [Fact]
    public async Task Handle_Success_Finds_Exit_And_Spawns_Inward_Left_Edge()
    {
        const int exitY = 10;
        var map = MakeRegionMap(exitTileX: 0, exitTileY: exitY, exitTargetRegion: CurrentRegionId);
        var tilemapRepo = new Mock<ITileMapRepository>();
        tilemapRepo.Setup(r => r.GetByRegionIdAsync(TargetRegionId)).ReturnsAsync(map);
        var sessionRepo = new Mock<IPlayerSessionRepository>();
        var handler = MakeHandler(tilemapRepo.Object, sessionRepo.Object);
        var command = new ChangeRegionHubCommand(CharId, CurrentRegionId, TargetRegionId);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.TileX.Should().Be(1);        // pushed right by 1
        result.TileY.Should().Be(exitY);    // same Y
        result.NewRegionId.Should().Be(TargetRegionId);
        sessionRepo.Verify(r => r.SetRegionAsync(CharId, TargetRegionId), Times.Once);
        sessionRepo.Verify(r => r.UpdatePositionAsync(CharId, 1, exitY), Times.Once);
    }

    [Fact]
    public async Task Handle_Success_Finds_Exit_And_Spawns_Inward_Right_Edge()
    {
        const int mapWidth = 30;
        const int exitY = 10;
        var map = MakeRegionMap(width: mapWidth, exitTileX: mapWidth - 1, exitTileY: exitY, exitTargetRegion: CurrentRegionId);
        var tilemapRepo = new Mock<ITileMapRepository>();
        tilemapRepo.Setup(r => r.GetByRegionIdAsync(TargetRegionId)).ReturnsAsync(map);
        var sessionRepo = new Mock<IPlayerSessionRepository>();
        var handler = MakeHandler(tilemapRepo.Object, sessionRepo.Object);
        var command = new ChangeRegionHubCommand(CharId, CurrentRegionId, TargetRegionId);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.TileX.Should().Be(mapWidth - 2); // pushed left by 1
        result.TileY.Should().Be(exitY);
    }

    [Fact]
    public async Task Handle_Success_Finds_Exit_And_Spawns_Inward_Top_Edge()
    {
        const int exitX = 15;
        var map = MakeRegionMap(exitTileX: exitX, exitTileY: 0, exitTargetRegion: CurrentRegionId);
        var tilemapRepo = new Mock<ITileMapRepository>();
        tilemapRepo.Setup(r => r.GetByRegionIdAsync(TargetRegionId)).ReturnsAsync(map);
        var sessionRepo = new Mock<IPlayerSessionRepository>();
        var handler = MakeHandler(tilemapRepo.Object, sessionRepo.Object);
        var command = new ChangeRegionHubCommand(CharId, CurrentRegionId, TargetRegionId);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.TileX.Should().Be(exitX);
        result.TileY.Should().Be(1); // pushed down by 1
    }

    [Fact]
    public async Task Handle_Success_Finds_Exit_And_Spawns_Inward_Bottom_Edge()
    {
        const int mapHeight = 20;
        const int exitX = 15;
        var map = MakeRegionMap(height: mapHeight, exitTileX: exitX, exitTileY: mapHeight - 1, exitTargetRegion: CurrentRegionId);
        var tilemapRepo = new Mock<ITileMapRepository>();
        tilemapRepo.Setup(r => r.GetByRegionIdAsync(TargetRegionId)).ReturnsAsync(map);
        var sessionRepo = new Mock<IPlayerSessionRepository>();
        var handler = MakeHandler(tilemapRepo.Object, sessionRepo.Object);
        var command = new ChangeRegionHubCommand(CharId, CurrentRegionId, TargetRegionId);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.TileX.Should().Be(exitX);
        result.TileY.Should().Be(mapHeight - 2); // pushed up by 1
    }

    // ── Fallback ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Falls_Back_To_1_1_When_No_Matching_Exit_Found()
    {
        // region_exits exists but points to a different region, not CurrentRegionId
        var map = MakeRegionMap(exitTileX: 5, exitTileY: 5, exitTargetRegion: "saltcliff");
        var tilemapRepo = new Mock<ITileMapRepository>();
        tilemapRepo.Setup(r => r.GetByRegionIdAsync(TargetRegionId)).ReturnsAsync(map);
        var sessionRepo = new Mock<IPlayerSessionRepository>();
        var handler = MakeHandler(tilemapRepo.Object, sessionRepo.Object);
        var command = new ChangeRegionHubCommand(CharId, CurrentRegionId, TargetRegionId);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.TileX.Should().Be(1);
        result.TileY.Should().Be(1);
    }

    // ── Persistence ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Calls_SetRegionAsync_And_UpdatePositionAsync_On_Success()
    {
        var map = MakeRegionMap(exitTileX: 0, exitTileY: 10, exitTargetRegion: CurrentRegionId);
        var tilemapRepo = new Mock<ITileMapRepository>();
        tilemapRepo.Setup(r => r.GetByRegionIdAsync(TargetRegionId)).ReturnsAsync(map);
        var sessionRepo = new Mock<IPlayerSessionRepository>();
        var handler = MakeHandler(tilemapRepo.Object, sessionRepo.Object);
        var command = new ChangeRegionHubCommand(CharId, CurrentRegionId, TargetRegionId);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        sessionRepo.Verify(r => r.SetRegionAsync(CharId, TargetRegionId), Times.Once);
        sessionRepo.Verify(r => r.UpdatePositionAsync(CharId, 1, 10), Times.Once);
    }
}
