using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using MudBlazor.Services;
using Veldrath.GameClient.Components.Components.Layout;
using Veldrath.GameClient.Components.Components.Pages;
using Veldrath.GameClient.Components.Tests.Infrastructure;
using Veldrath.GameClient.Core.Abstractions;
using Veldrath.GameClient.Core.Payloads;
using Veldrath.GameClient.Core.Services;
using Xunit;

namespace Veldrath.GameClient.Components.Tests;

/// <summary>
/// Web-first action wiring tests. These cover hub calls made from the RCL sidebars/footer
/// that previously sent missing or malformed arguments (or invoked removed hub methods).
/// </summary>
public class WebGameActionTests : BunitContext
{
    private readonly FakeGameHubConnectionService _fakeHub;
    private readonly GameStateService _gameState;

    /// <summary>Initializes the test container with the hub/state doubles used by the RCL.</summary>
    public WebGameActionTests()
    {
        _fakeHub = new FakeGameHubConnectionService();
        _gameState = new GameStateService();

        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddSingleton<IGameHubConnectionService>(_fakeHub);
        Services.AddSingleton<IGameStateService>(_gameState);
        Services.AddSingleton(_gameState);
        Services.AddSingleton<IGameApiClient>(new FakeGameApiClient());
        Services.AddSingleton<ILogger<RightSidebar>>(NullLogger<RightSidebar>.Instance);
        Services.AddSingleton<IDialogService>(new FakeDialogService());
        Services.AddSingleton<IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton<IResizeObserverFactory, NoopResizeObserverFactory>();
        Services.AddMudServices();
    }

    private void SeedZone(bool hasInn = true, bool hasMerchant = true)
    {
        _gameState.ApplyCharacterSelected(
            new CharacterBasicInfo(
                Guid.NewGuid(), "WebHero", "Warrior", 3, 0,
                30, 30, 10, 10, 100),
            currentZoneId: "fenwick-crossing");

        _gameState.ApplyZoneEntered(new ZoneEnteredPayload(
            "fenwick-crossing",
            "Fenwick's Crossing",
            "A quiet town.",
            "town",
            [],
            HasInn: hasInn,
            HasMerchant: hasMerchant));
    }

    /// <summary>Visiting the shop must send the current zone ID with the hub command.</summary>
    [Fact]
    public void VisitShop_Sends_ZoneId_Payload()
    {
        SeedZone();
        var cut = Render<LeftSidebar>();

        var button = cut.FindAll("button").First(b => b.TextContent.Contains("Visit Shop"));
        button.Click();

        cut.WaitForAssertion(() =>
        {
            var command = Assert.Single(_fakeHub.SentCommands, c => c.Method == "VisitShop");
            Assert.Equal("fenwick-crossing", ReadString(command.Arg, "ZoneId"));
        });
    }

    /// <summary>Resting must send the location ID and gold cost with the hub command.</summary>
    [Fact]
    public void Rest_Sends_Location_And_Cost_Payload()
    {
        SeedZone();
        var cut = Render<LeftSidebar>();

        var button = cut.FindAll("button").First(b => b.TextContent.Contains("Rest"));
        button.Click();

        cut.WaitForAssertion(() =>
        {
            var command = Assert.Single(_fakeHub.SentCommands, c => c.Method == "RestAtLocation");
            Assert.Equal("fenwick-crossing", ReadString(command.Arg, "LocationId"));
            Assert.Equal("10", ReadString(command.Arg, "CostInGold"));
        });
    }

    /// <summary>Navigating to a location must send the request object, not a bare string.</summary>
    [Fact]
    public void NavigateToLocation_Sends_Request_Object()
    {
        SeedZone();
        _gameState.ApplyZoneLocationUnlocked("market", "Fenwick Market", "market");

        var cut = Render<LeftSidebar>();

        var button = cut.Find("[data-testid='entity-market']");
        button.Click();

        cut.WaitForAssertion(() =>
        {
            var command = Assert.Single(_fakeHub.SentCommands, c => c.Method == "NavigateToLocation");
            Assert.Equal("market", ReadString(command.Arg, "LocationSlug"));
        });
    }

    /// <summary>Leaving a zone must use the existing LeaveZone hub method and refresh the map graph.</summary>
    [Fact]
    public void LeaveZone_Uses_LeaveZone_And_Refreshes_Map()
    {
        SeedZone();
        var cut = Render<LeftSidebar>();

        var button = cut.FindAll("button").First(b => b.TextContent.Contains("Leave Zone"));
        button.Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("LeaveZone", _fakeHub.SentMethods);
            Assert.Contains(_fakeHub.SentCommands, c => c.Method == "GetMapGraph");
        });
    }

    /// <summary>The removed /Game/Map route must not remain as a dead navigation link.</summary>
    [Fact]
    public void LeftSidebar_Does_Not_Link_To_Removed_Map_Route()
    {
        SeedZone();
        var cut = Render<LeftSidebar>();

        Assert.DoesNotContain(
            cut.FindAll("a"),
            anchor => anchor.GetAttribute("href")?.Contains("/Game/Map", StringComparison.OrdinalIgnoreCase) == true);
    }

    /// <summary>The footer must not render combat buttons without command bindings; GameCombat owns them.</summary>
    [Fact]
    public void Footer_Does_Not_Render_Dead_Combat_Actions()
    {
        _gameState.ApplyCombatStarted(new EnemyInfo(Guid.NewGuid(), "Goblin", 1, 10, 10));

        var cut = Render<GameFooter>();

        Assert.Empty(cut.FindAll("button.action-btn-attack"));
        Assert.Empty(cut.FindAll("button.action-btn-defend"));
        Assert.Empty(cut.FindAll("button.action-btn-flee"));
    }

    private static string? ReadString(object? payload, string propertyName) =>
        payload?.GetType().GetProperty(propertyName)?.GetValue(payload)?.ToString();
}
