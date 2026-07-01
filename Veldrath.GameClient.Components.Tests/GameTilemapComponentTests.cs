using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Veldrath.GameClient.Components.Components.Pages;
using Veldrath.GameClient.Components.Tests.Infrastructure;
using Veldrath.GameClient.Core.Abstractions;
using Veldrath.GameClient.Core.Services;
using Xunit;

namespace Veldrath.GameClient.Components.Tests;

/// <summary>
/// Tests for the <see cref="GameTilemap"/> component, verifying tile grid
/// rendering, tile click behaviour, and CSS class application.
/// </summary>
public class GameTilemapComponentTests : TestContext
{
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
    /// Verifies the tile grid renders from zone tilemap data.
    /// </summary>
    [Fact]
    public void Tilemap_Renders_Grid_From_Zone_Data()
    {
        // Create a simple 3x3 tilemap.
        var tiles = new Tile[3, 3]
        {
            { new Tile(0, 0, 0, false), new Tile(1, 0, 0, false), new Tile(2, 0, 0, false) },
            { new Tile(0, 1, 1, true),  new Tile(1, 1, 0, false), new Tile(2, 1, 0, false) },
            { new Tile(0, 2, 2, false), new Tile(1, 2, 0, false), new Tile(2, 2, 0, false) },
        };

        _gameState.ApplyZoneEntered("test-zone", "Test Zone", 0, 0, tiles);

        var cut = Render<GameTilemap>();

        // The tile grid should be rendered.
        var tilemapElement = cut.Find(".game-tilemap");
        Assert.NotNull(tilemapElement);

        // Each tile should be a div with the game-tile class.
        var tileElements = cut.FindAll(".game-tile");
        Assert.Equal(9, tileElements.Count);
    }

    /// <summary>
    /// Verifies tile types render with correct CSS classes.
    /// </summary>
    [Fact]
    public void TileTypes_Render_With_Correct_Css_Classes()
    {
        var tiles = new Tile[1, 5]
        {
            {
                new Tile(0, 0, 0, false),  // grass
                new Tile(1, 0, 1, true),   // wall
                new Tile(2, 0, 2, false),  // water
                new Tile(3, 0, 3, false),  // door
                new Tile(4, 0, -1, false)  // void
            },
        };

        _gameState.ApplyZoneEntered("test-zone", "Test Zone", 0, 0, tiles);

        var cut = Render<GameTilemap>();

        var tileElements = cut.FindAll(".game-tile");
        Assert.Equal(5, tileElements.Count);

        // Check each tile type class is present.
        Assert.Single(cut.FindAll(".tile-grass"));
        Assert.Single(cut.FindAll(".tile-wall"));
        Assert.Single(cut.FindAll(".tile-water"));
        Assert.Single(cut.FindAll(".tile-door"));
        Assert.Single(cut.FindAll(".tile-void"));
    }

    /// <summary>
    /// Verifies clicking a tile sends the MoveCharacter command when not in combat.
    /// </summary>
    [Fact]
    public void TileClick_Sends_MoveCommand_When_Not_In_Combat()
    {
        var tiles = new Tile[2, 2]
        {
            { new Tile(0, 0, 0, false), new Tile(1, 0, 0, false) },
            { new Tile(0, 1, 0, false), new Tile(1, 1, 0, false) },
        };

        _gameState.ApplyZoneEntered("test-zone", "Test Zone", 0, 0, tiles);

        var cut = Render<GameTilemap>();

        // Click the first tile.
        var tileElements = cut.FindAll(".game-tile");
        Assert.NotEmpty(tileElements);

        tileElements[1].Click(); // Click tile (1, 0)

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
        var tiles = new Tile[2, 2]
        {
            { new Tile(0, 0, 0, false), new Tile(1, 0, 0, false) },
            { new Tile(0, 1, 0, false), new Tile(1, 1, 0, false) },
        };

        _gameState.ApplyZoneEntered("test-zone", "Test Zone", 0, 0, tiles);

        // Start combat.
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
        var tiles = new Tile[2, 2]
        {
            { new Tile(0, 0, 0, false), new Tile(1, 0, 0, false) },
            { new Tile(0, 1, 0, false), new Tile(1, 1, 0, false) },
        };

        _gameState.ApplyZoneEntered("test-zone", "Test Zone", 0, 0, tiles);

        var cut = Render<GameTilemap>();

        var playerIndicators = cut.FindAll(".tile-indicator-player");
        Assert.Single(playerIndicators);
    }
}
