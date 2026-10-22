using Bunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using MudBlazor.Services;
using Veldrath.Auth;
using Veldrath.Auth.Blazor;
using Veldrath.Contracts.Connection;
using Veldrath.GameClient.Components.Components.Layout;
using Veldrath.GameClient.Components.Components.Pages;
using Veldrath.GameClient.Components.Components.Shared;
using Veldrath.GameClient.Components.Tests.Infrastructure;
using Veldrath.GameClient.Core.Abstractions;
using Veldrath.GameClient.Core.Payloads;
using Veldrath.GameClient.Core.Services;
using Xunit;

namespace Veldrath.GameClient.Components.Tests;

/// <summary>
/// Verifies that each major RCL component renders without throwing exceptions
/// when provided with minimal mock services.
/// </summary>
public class RclComponentRenderTests : BunitContext
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

        // Set up JSInterop for components that call JavaScript functions.
        JSInterop.SetupVoid("characterCreation.enableBeforeUnload");
        JSInterop.SetupVoid("characterCreation.disableBeforeUnload");

        // MudBlazor components (text inputs, tabs, resize observers) invoke JS interop;
        // loose mode lets unconfigured calls succeed instead of failing the render.
        JSInterop.Mode = JSRuntimeMode.Loose;

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

        Services.AddSingleton<IDialogService>(new FakeDialogService());

        // MudBlazor's production key-interceptor and resize-observer services are
        // async-dispose-only, while bUnit disposes the test container synchronously.
        // Register sync-disposable no-op doubles first so AddMudServices' TryAdd
        // registrations do not override them.
        Services.AddSingleton<IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton<IResizeObserverFactory, NoopResizeObserverFactory>();
        Services.AddMudServices();

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
        var cut = Render<GameHeader>(parameters =>
            parameters.AddCascadingValue<IGameStateService>(_gameState));
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
    /// Verifies <see cref="RightSidebar"/> renders without exceptions.
    /// </summary>
    [Fact]
    public void GameSidebar_Renders_Successfully()
    {
        var cut = Render<RightSidebar>(parameters =>
            parameters.AddCascadingValue<IGameStateService>(_gameState));
        Assert.NotNull(cut);
    }

    // NOTE: GameSettings has no bUnit render test because it resolves MudBlazor's
    // internal async-dispose-only PointerEventsNoneService, which bUnit's synchronous
    // container disposal cannot handle.

    /// <summary>Verifies <see cref="GamePanel"/> renders without exceptions.</summary>
    [Fact]
    public void GamePanel_Renders_Successfully()
    {
        var cut = Render<GamePanel>();
        Assert.NotNull(cut);
    }

    /// <summary>Verifies <see cref="ReconnectOverlay"/> renders without exceptions.</summary>
    [Fact]
    public void ReconnectOverlay_Renders_Successfully()
    {
        var cut = Render<ReconnectOverlay>(parameters =>
            parameters.AddCascadingValue<IGameStateService>(_gameState));
        Assert.NotNull(cut);
    }

    /// <summary>Verifies the game shell handles social and moderation hub events.</summary>
    [Fact]
    public void Game_Registers_Social_And_Moderation_Handlers()
    {
        var cut = Render<Game>();

        _fakeHub.FireEvent("OnWhisper", new WhisperPayload(Guid.NewGuid(), "Aria", "hello there"));
        _fakeHub.FireEvent("OnAnnouncement", new AnnouncementPayload("Server restart in 5 minutes", "warning"));
        _fakeHub.FireEvent("OnWarned", new WarnedPayload("Spam", 2));
        _fakeHub.FireEvent("OnMuted", new MutedPayload("Spam", null));
        _fakeHub.FireEvent("OnEmote", new EmotePayload(Guid.NewGuid(), "Aria", "waves hello"));

        _gameState.ChatMessages.Should().Contain(m => m.Channel == "whisper" && m.Message == "hello there");
        _gameState.ChatMessages.Should().Contain(m => m.Message.Contains("Server restart in 5 minutes"));
        _gameState.ChatMessages.Should().Contain(m => m.Message.Contains("total warnings: 2"));
        _gameState.ChatMessages.Should().Contain(m => m.Message.Contains("muted indefinitely"));
        _gameState.ChatMessages.Should().Contain(m => m.Message.Contains("waves hello"));
        Assert.NotNull(cut);
    }

    /// <summary>Verifies the game shell handles enemy and staff-action hub events.</summary>
    [Fact]
    public void Game_Registers_Enemy_And_Staff_Action_Handlers()
    {
        var cut = Render<Game>();

        _fakeHub.FireEvent("EnemySpawned", new EnemySpawnedPayload(Guid.NewGuid(), "Bog Lurker", 3, 40, 40));
        _fakeHub.FireEvent("OnItemReceived", new ItemReceivedPayload("iron-sword", 2, "GM"));
        _fakeHub.FireEvent("OnSummoned", new SummonedPayload("GM", "crestfall"));
        _fakeHub.FireEvent("OnTeleported", new TeleportedPayload("crestfall", "Crestfall"));

        _gameState.ChatMessages.Should().Contain(m => m.Message.Contains("Bog Lurker"));
        _gameState.ChatMessages.Should().Contain(m => m.Message.Contains("2× iron-sword"));
        _gameState.ChatMessages.Should().Contain(m => m.Message.Contains("summoned you"));
        _gameState.ChatMessages.Should().Contain(m => m.Message.Contains("Teleported to Crestfall"));
        _fakeHub.SentMethods.Should().Contain("GetMapGraph");
        Assert.NotNull(cut);
    }
}
