using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Veldrath.Auth.Blazor;
using Veldrath.GameClient.Components.Components.Layout;
using Veldrath.GameClient.Components.Tests.Infrastructure;
using Veldrath.GameClient.Core.Abstractions;
using Veldrath.GameClient.Core.Services;
using Xunit;

namespace Veldrath.GameClient.Components.Tests;

/// <summary>
/// Tests for the <see cref="GameLayout"/> component, ensuring it renders with
/// the correct CSS classes and layout structure.
/// </summary>
public class GameLayoutRenderTests : BunitContext
{
    private readonly FakeGameHubConnectionService _fakeHub;
    private readonly GameStateService _gameState;

    /// <summary>
    /// Initializes a new instance and registers required services.
    /// </summary>
    public GameLayoutRenderTests()
    {
        _fakeHub = new FakeGameHubConnectionService();
        _gameState = new GameStateService();

        Services.AddSingleton<IGameHubConnectionService>(_fakeHub);
        Services.AddSingleton<IGameStateService>(_gameState);
        Services.AddSingleton(_gameState);
        Services.AddMudServices();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Veldrath:ServerUrl"] = "http://localhost:5000",
            })
            .Build();
        Services.AddSingleton<IConfiguration>(config);

        var fakeAuthApi = new FakeVeldrathAuthApiClient();
        Services.AddSingleton(fakeAuthApi);
        Services.AddSingleton<AuthStateServiceBase>(sp =>
            new FakeAuthStateService(sp.GetRequiredService<FakeVeldrathAuthApiClient>()));
    }

    /// <summary>
    /// Verifies GameLayout applies the <c>in-game</c> CSS class when connected.
    /// </summary>
    [Fact]
    public void GameLayout_Renders_With_Correct_Css_Class_When_Connected()
    {
        _gameState.ApplyServerInfo("test-connection-id");

        var cut = Render<GameLayout>();

        Assert.Contains("in-game", cut.Markup);
    }

    /// <summary>
    /// Verifies GameLayout renders without the <c>in-game</c> CSS class
    /// when the connection state is disconnected.
    /// </summary>
    [Fact]
    public void GameLayout_Renders_Without_InGame_Css_When_Disconnected()
    {
        var cut = Render<GameLayout>();

        // When disconnected, the "in-game" class should not be present.
        Assert.DoesNotContain("in-game", cut.Markup);
    }
}
