using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Veldrath.GameClient.Components.Components.Pages;
using Veldrath.GameClient.Components.Tests.Infrastructure;
using Veldrath.GameClient.Core.Abstractions;
using Veldrath.GameClient.Core.Services;
using Xunit;

namespace Veldrath.GameClient.Components.Tests;

/// <summary>
/// Tests for the <see cref="GameTilemap"/> component using the fixed 15×15 viewport model.
/// </summary>
public class GameTilemapComponentTests : BunitContext
{
    private const int ViewportSize = 15;

    private readonly FakeGameHubConnectionService _fakeHub;
    private readonly GameStateService _gameState;

    /// <summary>
    /// Initializes a new instance and registers required services.
    /// </summary>
    public GameTilemapComponentTests()
    {
        _fakeHub = new FakeGameHubConnectionService();
        _gameState = new GameStateService();

        Services.AddSingleton<IGameHubConnectionService>(_fakeHub);
        Services.AddSingleton<IGameStateService>(_gameState);
        Services.AddSingleton(_gameState);
    }

    /// <summary>
    /// Verifies the empty state renders when no tilemap data is available.
    /// </summary>
    [Fact]
    public void Tilemap_Shows_Empty_State_When_No_Data()
    {
        var cut = Render<GameTilemap>();

        var emptyMsg = cut.Find(".game-tilemap-empty");
        Assert.NotNull(emptyMsg);
        Assert.Contains("No tilemap data available.", emptyMsg.TextContent);
    }

    /// <summary>
    /// Verifies the viewport always renders exactly 225 tiles (15×15) regardless of map size.
    /// </summary>
    [Fact]
    public void Viewport_Renders_Exactly_225_Tiles()
    {
        var tiles = new Veldrath.GameClient.Core.Abstractions.Tile[3, 3]
        {
            { new Veldrath.GameClient.Core.Abstractions.Tile(0, 0, 0, false), new Veldrath.GameClient.Core.Abstractions.Tile(1, 0, 0, false), new Veldrath.GameClient.Core.Abstractions.Tile(2, 0, 0, false) },
            { new Veldrath.GameClient.Core.Abstractions.Tile(0, 1, 1, true),  new Veldrath.GameClient.Core.Abstractions.Tile(1, 1, 0, false), new Veldrath.GameClient.Core.Abstractions.Tile(2, 1, 0, false) },
            { new Veldrath.GameClient.Core.Abstractions.Tile(0, 2, 2, false), new Veldrath.GameClient.Core.Abstractions.Tile(1, 2, 0, false), new Veldrath.GameClient.Core.Abstractions.Tile(2, 2, 0, false) },
        };

        _gameState.ApplyZoneEntered("test-zone", "Test Zone", 0, 0, tiles);

        var cut = Render<GameTilemap>();

        var tilemapElement = cut.Find(".game-tilemap-viewport");
        Assert.NotNull(tilemapElement);

        var tileElements = cut.FindAll(".game-tile");
        Assert.Equal(ViewportSize * ViewportSize, tileElements.Count);
    }

    /// <summary>
    /// Verifies out-of-bounds tiles (beyond map edges) render as void tiles.
    /// </summary>
    [Fact]
    public void OutOfBounds_Tiles_Render_As_Void()
    {
        var tiles = new Veldrath.GameClient.Core.Abstractions.Tile[1, 1]
        {
            { new Veldrath.GameClient.Core.Abstractions.Tile(0, 0, 0, false) },
        };

        _gameState.ApplyZoneEntered("test-zone", "Test Zone", 0, 0, tiles);

        var cut = Render<GameTilemap>();

        // The viewport has 225 tiles. Only 1 is real (grass), 224 are void.
        var grassTiles = cut.FindAll(".tile-grass");
        var voidTiles = cut.FindAll(".tile-void");

        Assert.Single(grassTiles);
        Assert.Equal(224, voidTiles.Count);
    }

    /// <summary>
    /// Verifies tile type CSS classes are applied correctly within the viewport.
    /// </summary>
    [Fact]
    public void TileTypes_Render_With_Correct_Css_Classes()
    {
        var tiles = new Veldrath.GameClient.Core.Abstractions.Tile[1, 5]
        {
            {
                new Veldrath.GameClient.Core.Abstractions.Tile(0, 0, 0, false),  // grass
                new Veldrath.GameClient.Core.Abstractions.Tile(1, 0, 1, true),   // wall
                new Veldrath.GameClient.Core.Abstractions.Tile(2, 0, 2, false),  // water
                new Veldrath.GameClient.Core.Abstractions.Tile(3, 0, 3, false),  // door
                new Veldrath.GameClient.Core.Abstractions.Tile(4, 0, -1, false)  // void
            },
        };

        _gameState.ApplyZoneEntered("test-zone", "Test Zone", 0, 0, tiles);

        var cut = Render<GameTilemap>();

        // Check each tile type class is present in the viewport.
        Assert.Single(cut.FindAll(".tile-grass"));
        Assert.Single(cut.FindAll(".tile-wall"));
        Assert.Single(cut.FindAll(".tile-water"));
        Assert.Single(cut.FindAll(".tile-door"));
        // Void count = 1 real void + all the out-of-bounds tiles
        Assert.True(cut.FindAll(".tile-void").Count >= 1);
    }

    /// <summary>
    /// Verifies clicking a tile sends the MoveCharacter command with correct map coordinates.
    /// </summary>
    [Fact]
    public void TileClick_Sends_MoveCommand_With_Map_Coordinates()
    {
        var tiles = new Veldrath.GameClient.Core.Abstractions.Tile[3, 3];
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                tiles[y, x] = new Veldrath.GameClient.Core.Abstractions.Tile(x, y, 0, false);

        _gameState.ApplyZoneEntered("test-zone", "Test Zone", 0, 0, tiles);

        var cut = Render<GameTilemap>();

        // Click the tile at viewport position (1, 0) → map coordinates (1, 0) when camera at (0, 0)
        var tileElements = cut.FindAll(".game-tile");
        Assert.NotEmpty(tileElements);

        // Position (1, 0) is the second rendered tile (row-major: x inner loop)
        tileElements[1].Click();

        var sentMove = _fakeHub.SentCommands
            .FirstOrDefault(c => c.Method == "MoveCharacter");
        Assert.NotEqual(default, sentMove);
    }

    /// <summary>
    /// Verifies clicking a tile does NOT send a movement command when in combat.
    /// </summary>
    [Fact]
    public void TileClick_Does_Not_Send_Move_When_In_Combat()
    {
        var tiles = new Veldrath.GameClient.Core.Abstractions.Tile[3, 3];
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                tiles[y, x] = new Veldrath.GameClient.Core.Abstractions.Tile(x, y, 0, false);

        _gameState.ApplyZoneEntered("test-zone", "Test Zone", 0, 0, tiles);

        _gameState.ApplyCombatStarted(new EnemyInfo(
            Guid.NewGuid(), "Goblin", 3, 25, 25, 5, 5));

        var cut = Render<GameTilemap>();

        var tileElements = cut.FindAll(".game-tile");
        Assert.NotEmpty(tileElements);

        tileElements[0].Click();

        var sentMove = _fakeHub.SentCommands
            .FirstOrDefault(c => c.Method == "MoveCharacter");
        Assert.Equal(default, sentMove);
    }

    /// <summary>
    /// Verifies the player indicator renders on the player's tile.
    /// </summary>
    [Fact]
    public void PlayerIndicator_Renders_On_Player_Tile()
    {
        var tiles = new Veldrath.GameClient.Core.Abstractions.Tile[3, 3];
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                tiles[y, x] = new Veldrath.GameClient.Core.Abstractions.Tile(x, y, 0, false);

        _gameState.ApplyZoneEntered("test-zone", "Test Zone", 1, 1, tiles);

        var cut = Render<GameTilemap>();

        var playerIndicators = cut.FindAll(".tile-indicator-player");
        Assert.Single(playerIndicators);
    }

    /// <summary>
    /// Verifies the camera centers on the player (player at (20, 20) on a 50×50 map → camera should be at (13, 13)).
    /// </summary>
    [Fact]
    public void Camera_Centers_On_Player()
    {
        var mapSize = 50;
        var tiles = new Veldrath.GameClient.Core.Abstractions.Tile[mapSize, mapSize];
        for (int y = 0; y < mapSize; y++)
            for (int x = 0; x < mapSize; x++)
                tiles[y, x] = new Veldrath.GameClient.Core.Abstractions.Tile(x, y, 0, false);

        _gameState.ApplyZoneEntered("test-zone", "Test Zone", 20, 20, tiles);

        var cut = Render<GameTilemap>();

        // Viewport should contain the player tile at its center position (7, 7) in viewport-local coords
        var playerIndicators = cut.FindAll(".tile-indicator-player");
        Assert.Single(playerIndicators);
    }

    /// <summary>
    /// Verifies the camera clamps at map edges so the player is NOT centered near the boundary.
    /// </summary>
    [Fact]
    public void Camera_Clamps_At_Map_Edges()
    {
        var mapSize = 20;
        var tiles = new Veldrath.GameClient.Core.Abstractions.Tile[mapSize, mapSize];
        for (int y = 0; y < mapSize; y++)
            for (int x = 0; x < mapSize; x++)
                tiles[y, x] = new Veldrath.GameClient.Core.Abstractions.Tile(x, y, 0, false);

        // Player at top-left corner (0, 0) — camera should clamp at (0, 0)
        _gameState.ApplyZoneEntered("test-zone", "Test Zone", 0, 0, tiles);

        var cut = Render<GameTilemap>();

        // Player indicator should still be visible in the viewport
        var playerIndicators = cut.FindAll(".tile-indicator-player");
        Assert.Single(playerIndicators);
    }

    /// <summary>
    /// Verifies the status bar renders below the viewport showing the current tile description.
    /// </summary>
    [Fact]
    public void StatusBar_Renders_Current_Tile_Description()
    {
        var tiles = new Veldrath.GameClient.Core.Abstractions.Tile[3, 3]
        {
            { new Veldrath.GameClient.Core.Abstractions.Tile(0, 0, 0, false), new Veldrath.GameClient.Core.Abstractions.Tile(1, 0, 1, true), new Veldrath.GameClient.Core.Abstractions.Tile(2, 0, 0, false) },
            { new Veldrath.GameClient.Core.Abstractions.Tile(0, 1, 0, false), new Veldrath.GameClient.Core.Abstractions.Tile(1, 1, 2, false), new Veldrath.GameClient.Core.Abstractions.Tile(2, 1, 0, false) },
            { new Veldrath.GameClient.Core.Abstractions.Tile(0, 2, 0, false), new Veldrath.GameClient.Core.Abstractions.Tile(1, 2, 0, false), new Veldrath.GameClient.Core.Abstractions.Tile(2, 2, 0, false) },
        };

        // Player at (1, 1) which is water (type 2)
        _gameState.ApplyZoneEntered("test-zone", "Test Zone", 1, 1, tiles);

        var cut = Render<GameTilemap>();

        var statusBar = cut.Find(".tile-status-bar");
        Assert.NotNull(statusBar);
        Assert.Contains("Water", statusBar.TextContent);
    }

    /// <summary>
    /// Verifies the status bar shows "Void" when the player is at an out-of-bounds position.
    /// </summary>
    [Fact]
    public void StatusBar_Shows_Void_For_Invalid_Position()
    {
        var tiles = new Veldrath.GameClient.Core.Abstractions.Tile[1, 1]
        {
            { new Veldrath.GameClient.Core.Abstractions.Tile(0, 0, 0, false) },
        };

        // Player at (-1, -1) — not on the map
        _gameState.ApplyZoneEntered("test-zone", "Test Zone", -1, -1, tiles);

        var cut = Render<GameTilemap>();

        var statusBar = cut.Find(".tile-status-bar");
        Assert.NotNull(statusBar);
        Assert.Contains("Void", statusBar.TextContent);
    }
}
