using Bunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Veldrath.Auth;
using Veldrath.Auth.Blazor;
using Veldrath.GameClient.Components.Components.Pages;
using Veldrath.GameClient.Components.Tests.Infrastructure;
using Veldrath.GameClient.Core.Abstractions;
using Veldrath.GameClient.Core.Services;
using Xunit;

namespace Veldrath.GameClient.Components.Tests;

/// <summary>
/// Verifies that each major RCL component renders without throwing exceptions
/// when provided with minimal mock services.
/// </summary>
public class RclComponentRenderTests : TestContext
{
    private readonly FakeGameHubConnectionService _fakeHub;
    private readonly FakeGameApiClient _fakeApi;
    private readonly FakeAuthStateService _fakeAuth;
    private readonly GameStateService _gameState;

    /// <summary>
    /// Initializes a new instance and registers all stub services into the bUnit
    /// <see cref="TestContext"/> DI container.
    /// </summary>
    public RclComponentRenderTests()
    {
        _fakeHub = new FakeGameHubConnectionService();
        _fakeApi = new FakeGameApiClient();
        _gameState = new GameStateService();

        var fakeAuthApi = new FakeVeldrathAuthApiClient();
        _fakeAuth = new FakeAuthStateService(fakeAuthApi);

        // Register stubs for interface-injected services.
        Services.AddSingleton<IGameHubConnectionService>(_fakeHub);
        Services.AddSingleton<IGameStateService>(_gameState);
        Services.AddSingleton<IGameApiClient>(_fakeApi);
        Services.AddSingleton<AuthStateServiceBase>(_fakeAuth);
        Services.AddSingleton<IVeldrathAuthApiClient>(fakeAuthApi);
        Services.AddSingleton<ILogger<CharacterSelect>>(NullLogger<CharacterSelect>.Instance);
        Services.AddSingleton<ILogger<CreateCharacter>>(NullLogger<CreateCharacter>.Instance);
        Services.AddSingleton<ILogger<Game>>(NullLogger<Game>.Instance);

        // Register the concrete GameStateService for components that inject it directly.
        Services.AddSingleton(_gameState);

        // Register a stub IConfiguration with a default server URL.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Veldrath:ServerUrl"] = "http://localhost:9000",
            })
            .Build();
        Services.AddSingleton<IConfiguration>(config);
    }

    /// <summary>
    /// Verifies <see cref="CharacterSelect"/> renders without exceptions.
    /// </summary>
    [Fact]
    public void CharacterSelect_Renders_Successfully()
    {
        _fakeHub.StateValue = Veldrath.GameClient.Core.Models.ConnectionState.Connected;
        var cut = Render<CharacterSelect>();
        Assert.NotNull(cut);
    }

    /// <summary>
    /// Verifies <see cref="CreateCharacter"/> renders without exceptions.
    /// </summary>
    [Fact]
    public void CreateCharacter_Renders_Successfully()
    {
        var cut = Render<CreateCharacter>();
        Assert.NotNull(cut);
    }

    /// <summary>
    /// Verifies <see cref="GameChat"/> renders without exceptions.
    /// </summary>
    [Fact]
    public void GameChat_Renders_Successfully()
    {
        var cut = Render<GameChat>();
        Assert.NotNull(cut);
    }

    /// <summary>
    /// Verifies <see cref="GameCombat"/> renders without exceptions.
    /// </summary>
    [Fact]
    public void GameCombat_Renders_Successfully()
    {
        var cut = Render<GameCombat>();
        Assert.NotNull(cut);
    }

    /// <summary>
    /// Verifies <see cref="GameFooter"/> renders without exceptions.
    /// </summary>
    [Fact]
    public void GameFooter_Renders_Successfully()
    {
        var cut = Render<GameFooter>();
        Assert.NotNull(cut);
    }

    /// <summary>
    /// Verifies <see cref="GameHeader"/> renders without exceptions.
    /// </summary>
    [Fact]
    public void GameHeader_Renders_Successfully()
    {
        var cut = Render<GameHeader>();
        Assert.NotNull(cut);
    }

    /// <summary>
    /// Verifies <see cref="GameOverlay"/> renders without exceptions.
    /// </summary>
    [Fact]
    public void GameOverlay_Renders_Successfully()
    {
        var cut = Render<GameOverlay>();
        Assert.NotNull(cut);
    }

    /// <summary>
    /// Verifies <see cref="GameSidebar"/> renders without exceptions.
    /// </summary>
    [Fact]
    public void GameSidebar_Renders_Successfully()
    {
        var cut = Render<GameSidebar>();
        Assert.NotNull(cut);
    }

    /// <summary>
    /// Verifies <see cref="GameTilemap"/> renders without exceptions.
    /// </summary>
    [Fact]
    public void GameTilemap_Renders_Successfully()
    {
        var cut = Render<GameTilemap>();
        Assert.NotNull(cut);
    }

    /// <summary>
    /// Verifies <see cref="GameMap"/> renders without exceptions.
    /// </summary>
    [Fact]
    public void GameMap_Renders_Successfully()
    {
        var cut = Render<GameMap>();
        Assert.NotNull(cut);
    }
}
