using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Veldrath.Contracts.Tilemap;
using Veldrath.GameClient.Components.Components.Pages;
using Veldrath.GameClient.Components.Tests.Infrastructure;
using Veldrath.GameClient.Core.Abstractions;
using Veldrath.GameClient.Core.Services;
using Xunit;

namespace Veldrath.GameClient.Components.Tests;

/// <summary>
/// bUnit tests for the <see cref="GameMap"/> component.
/// Verifies rendering of zone cards, current-zone highlighting,
/// undiscovered zone display, and navigation click handling.
/// </summary>
public class GameMapComponentTests : BunitContext
{
    private readonly FakeGameHubConnectionService _fakeHub;
    private readonly GameStateService _gameState;

    /// <summary>
    /// Initializes a new instance and registers stub services.
    /// </summary>
    public GameMapComponentTests()
    {
        _fakeHub = new FakeGameHubConnectionService();
        _gameState = new GameStateService();

        Services.AddSingleton<IGameHubConnectionService>(_fakeHub);
        Services.AddSingleton<IGameStateService>(_gameState);
        Services.AddSingleton(_gameState);
    }

    /// <summary>
    /// Creates a sample <see cref="RegionMapDto"/> for test use.
    /// </summary>
    private static RegionMapDto CreateSampleRegionMap(params ZoneObjectDto[] zones)
    {
        return new RegionMapDto(
            "thornveil",
            "onebit_packed",
            100, 80, 16,
            [],
            [],
            zones,
            [],
            [],
            []);
    }

    /// <summary>
    /// Verifies the map renders a loading indicator while waiting for hub data.
    /// </summary>
    [Fact]
    public void GameMap_Shows_Loading_Initially()
    {
        var cut = Render<GameMap>();
        Assert.Contains("Loading region map", cut.Markup);
    }

    /// <summary>
    /// Verifies the map renders zone cards when the RegionMap hub event fires.
    /// </summary>
    [Fact]
    public void GameMap_Renders_Zone_List_After_RegionMap_Received()
    {
        var cut = Render<GameMap>();

        var dto = CreateSampleRegionMap(
            new ZoneObjectDto(10, 10, "fenwick-crossing", "Fenwick Crossing", 1, 5),
            new ZoneObjectDto(30, 10, "greenveil-paths", "Greenveil Paths", 3, 8));

        // Fire the hub event on the Blazor dispatcher thread.
        cut.InvokeAsync(() => _fakeHub.FireEvent("RegionMap", dto));
        cut.Render();

        Assert.Contains("Fenwick Crossing", cut.Markup);
        Assert.Contains("Greenveil Paths", cut.Markup);
    }

    /// <summary>
    /// Verifies the current zone is highlighted with a "You are here" badge.
    /// </summary>
    [Fact]
    public void GameMap_Current_Zone_Highlighted()
    {
        // Set up game state with a current zone.
        _gameState.ApplyCharacterSelected(
            new CharacterBasicInfo(
                Guid.NewGuid(), "TestChar", "Warrior", 1, 0, 100, 100, 50, 50, 10),
            "fenwick-crossing");

        var cut = Render<GameMap>();

        var dto = CreateSampleRegionMap(
            new ZoneObjectDto(10, 10, "fenwick-crossing", "Fenwick Crossing", 1, 5),
            new ZoneObjectDto(30, 10, "greenveil-paths", "Greenveil Paths", 3, 8));

        cut.InvokeAsync(() => _fakeHub.FireEvent("RegionMap", dto));
        cut.Render();

        // The current zone should have the "You are here" badge.
        Assert.Contains("You are here", cut.Markup);
    }

    /// <summary>
    /// Verifies clicking a zone card sends the MoveOnRegion hub command.
    /// </summary>
    [Fact]
    public void GameMap_Clicking_Zone_Sends_MoveOnRegion()
    {
        // Set fenwick-crossing as the current zone so the other zone is the only clickable one.
        _gameState.ApplyCharacterSelected(
            new CharacterBasicInfo(
                Guid.NewGuid(), "TestChar", "Warrior", 1, 0, 100, 100, 50, 50, 10),
            "fenwick-crossing");

        var cut = Render<GameMap>();

        var dto = CreateSampleRegionMap(
            new ZoneObjectDto(10, 10, "fenwick-crossing", "Fenwick Crossing", 1, 5),
            new ZoneObjectDto(30, 10, "greenveil-paths", "Greenveil Paths", 3, 8));

        cut.InvokeAsync(() => _fakeHub.FireEvent("RegionMap", dto));
        cut.Render();

        // Find and click the available zone card (greenveil-paths since fenwick-crossing is current).
        var zoneCard = cut.Find(".zone-card-available");
        zoneCard.Click();

        // Verify the hub received the MoveOnRegion command.
        Assert.Contains(_fakeHub.SentCommands, cmd =>
            cmd.Method == "MoveOnRegion" &&
            cmd.Arg?.ToString() == "greenveil-paths");
    }

    /// <summary>
    /// Verifies the map shows an error message when the hub is disconnected.
    /// </summary>
    [Fact]
    public void GameMap_Shows_Error_When_Hub_Disconnected()
    {
        _fakeHub.StateValue = Veldrath.GameClient.Core.Models.ConnectionState.Disconnected;

        var cut = Render<GameMap>();
        cut.Render();

        // Should show error about not being connected.
        Assert.Contains("Not connected to game server", cut.Markup);
    }

    /// <summary>
    /// Verifies the map shows a fallback zone from game state when hub sends empty zone list.
    /// </summary>
    [Fact]
    public void GameMap_Shows_Fallback_When_Empty_Zone_List()
    {
        // Set up game state with a current zone.
        _gameState.ApplyCharacterSelected(
            new CharacterBasicInfo(
                Guid.NewGuid(), "TestChar", "Warrior", 1, 0, 100, 100, 50, 50, 10),
            "fenwick-crossing");
        _gameState.ApplyZoneEntered("fenwick-crossing", "Fenwick Crossing", 0, 0, null);

        var cut = Render<GameMap>();

        // Send an empty RegionMap DTO (no zone entries).
        var dto = CreateSampleRegionMap();

        cut.InvokeAsync(() => _fakeHub.FireEvent("RegionMap", dto));
        cut.Render();

        // Fallback zone from GameState should appear.
        Assert.Contains("Fenwick Crossing", cut.Markup);
    }

    /// <summary>
    /// Verifies the map renders the "Back" button which navigates to /Game/Play.
    /// </summary>
    [Fact]
    public void GameMap_Back_Button_Navigates_To_Game()
    {
        var cut = Render<GameMap>();

        // Should have a Back button.
        Assert.Contains("Back", cut.Markup);
    }
}
