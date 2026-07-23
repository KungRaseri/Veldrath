using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using MudBlazor.Services;
using Veldrath.Auth;
using Veldrath.Auth.Blazor;
using Veldrath.Contracts.Characters;
using Veldrath.GameClient.Components.Components.Pages;
using Veldrath.GameClient.Components.Tests.Infrastructure;
using Veldrath.GameClient.Core.Abstractions;
using Veldrath.GameClient.Core.Services;
using Xunit;

namespace Veldrath.GameClient.Components.Tests;

/// <summary>
/// Tests for the <see cref="GameEntry"/> routing guard component, verifying
/// auth gating, character discovery, session detection, and dialog behaviour.
/// </summary>
public class GameEntryPageTests : BunitContext
{
    private readonly FakeGameApiClient _fakeApi;
    private readonly FakeAuthStateService _fakeAuth;
    private readonly FakeVeldrathAuthApiClient _fakeAuthApi;
    private readonly FakeDialogService _fakeDialog;
    private readonly GameStateService _gameState;

    /// <summary>
    /// Initializes a new instance and registers all stub services into the bUnit
    /// <see cref="TestContext"/> DI container.
    /// </summary>
    public GameEntryPageTests()
    {
        _fakeApi = new FakeGameApiClient();
        _fakeAuthApi = new FakeVeldrathAuthApiClient();
        _fakeAuth = new FakeAuthStateService(_fakeAuthApi);
        _fakeDialog = new FakeDialogService();
        _gameState = new GameStateService();

        Services.AddSingleton<IGameApiClient>(_fakeApi);
        Services.AddSingleton<AuthStateServiceBase>(_fakeAuth);
        Services.AddSingleton<IVeldrathAuthApiClient>(_fakeAuthApi);
        Services.AddSingleton<IDialogService>(_fakeDialog);
        Services.AddSingleton<IGameStateService>(_gameState);
        Services.AddSingleton(_gameState);
        Services.AddSingleton<ILogger<GameEntry>>(NullLogger<GameEntry>.Instance);
        Services.AddMudServices();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Veldrath:ServerUrl"] = "http://localhost:9000",
            })
            .Build();
        Services.AddSingleton<IConfiguration>(config);
    }

    /// <summary>
    /// Helper to create a valid <see cref="CharacterDto"/> for tests.
    /// </summary>
    private static CharacterDto CreateCharacterDto(string name = "TestHero")
        => new(Guid.NewGuid(), 1, name, "Warrior", 5, 1200, DateTimeOffset.UtcNow, "fenwick-crossing");

    // ── Auth Gate Tests ──────────────────────────────────────────────────────

    /// <summary>
    /// Verifies the component shows a waiting-for-auth spinner when
    /// <see cref="AuthStateServiceBase.IsAuthReady"/> is <see langword="false"/>.
    /// </summary>
    [Fact]
    public void Shows_WaitingForAuth_When_Auth_Not_Ready()
    {
        _fakeAuth.IsAuthReady = false;
        _fakeAuth.IsLoggedInOverride = false;

        var cut = Render<GameEntry>();

        Assert.Contains("Waiting for authentication", cut.Markup);
    }

    /// <summary>
    /// Verifies the component redirects to <c>/login</c> when the user
    /// is not logged in but auth is ready.
    /// </summary>
    [Fact]
    public void Redirects_To_Login_When_Not_Logged_In()
    {
        _fakeAuth.IsLoggedInOverride = false;

        Render<GameEntry>();

        var uri = new Uri(Services.GetRequiredService<NavigationManager>().Uri);
        Assert.Contains("/login", uri.AbsolutePath);
    }

    /// <summary>
    /// Verifies the component redirects to <c>/login</c> when token refresh fails
    /// (user was logged in but the refresh token was rejected).
    /// </summary>
    [Fact]
    public void Redirects_To_Login_When_Token_Refresh_Fails()
    {
        _fakeAuth.TryRefreshResult = false;
        _fakeAuth.IsLoggedInOverride = true;

        Render<GameEntry>();

        var uri = new Uri(Services.GetRequiredService<NavigationManager>().Uri);
        Assert.Contains("/login", uri.AbsolutePath);
    }

    // ── Character Discovery Tests ────────────────────────────────────────────

    /// <summary>
    /// Verifies the component redirects to <c>/Game/CreateCharacter</c>
    /// when the account has no characters.
    /// </summary>
    [Fact]
    public void Redirects_To_CreateCharacter_When_No_Characters()
    {
        _fakeApi.Characters = [];

        Render<GameEntry>();

        var uri = new Uri(Services.GetRequiredService<NavigationManager>().Uri);
        Assert.Contains("/Game/CreateCharacter", uri.AbsolutePath);
    }

    /// <summary>
    /// Verifies the component redirects to <c>/Game/CharacterSelect</c>
    /// when the account has characters but no active session.
    /// </summary>
    [Fact]
    public void Redirects_To_CharacterSelect_When_Has_Characters_No_Session()
    {
        _fakeApi.Characters = [CreateCharacterDto()];
        _fakeApi.LastSessionResult = null;

        Render<GameEntry>();

        var uri = new Uri(Services.GetRequiredService<NavigationManager>().Uri);
        Assert.Contains("/Game/CharacterSelect", uri.AbsolutePath);
    }

    /// <summary>
    /// Verifies that <see cref="IGameApiClient.GetLastSessionAsync"/> is not called
    /// when there are no characters (avoids unnecessary API call).
    /// </summary>
    [Fact]
    public void Does_Not_Call_GetLastSession_When_No_Characters()
    {
        _fakeApi.Characters = [];
        _fakeApi.LastSessionResult = new LastSessionDto(
            Guid.NewGuid(), "Hero", "zone-1", "fenwick-crossing", 10, 20);

        Render<GameEntry>();

        Assert.Equal(0, _fakeApi.GetLastSessionCallCount);
    }

    // ── Session Resume Dialog Tests ──────────────────────────────────────────

    /// <summary>
    /// Verifies the resume dialog is shown when the account has characters
    /// and an active session exists.
    /// </summary>
    [Fact]
    public void Shows_Resume_Dialog_When_Session_Exists()
    {
        _fakeApi.Characters = [CreateCharacterDto("Aragorn")];
        _fakeApi.LastSessionResult = new LastSessionDto(
            Guid.NewGuid(), "Aragorn", "darkwood-forest", "fenwick-crossing", 10, 20);

        Render<GameEntry>();

        Assert.NotNull(_fakeDialog.LastDialog);
        Assert.NotNull(_fakeDialog.LastParameters);
        Assert.Equal("Aragorn", _fakeDialog.LastParameters![nameof(Components.Dialogs.ResumeSessionDialog.CharacterName)]);
    }

    /// <summary>
    /// Verifies the component navigates to <c>/Game/Play</c> when the user
    /// clicks "Resume" in the dialog (<see cref="FakeDialogService"/> returns <c>true</c>).
    /// </summary>
    [Fact]
    public void Navigates_To_Play_On_Resume()
    {
        _fakeDialog.DialogResultValue = true;
        _fakeApi.Characters = [CreateCharacterDto()];
        _fakeApi.LastSessionResult = new LastSessionDto(
            Guid.NewGuid(), "Hero", "zone-1", "fenwick-crossing", 10, 20);

        Render<GameEntry>();

        var uri = new Uri(Services.GetRequiredService<NavigationManager>().Uri);
        Assert.Contains("/Game/Play", uri.AbsolutePath);
    }

    /// <summary>
    /// Verifies the component navigates to <c>/Game/CharacterSelect</c> when the user
    /// clicks "Choose Different Character" in the dialog (<see cref="FakeDialogService"/> returns <c>false</c>).
    /// </summary>
    [Fact]
    public void Navigates_To_CharacterSelect_On_Choose_Different()
    {
        _fakeDialog.DialogResultValue = false;
        _fakeApi.Characters = [CreateCharacterDto()];
        _fakeApi.LastSessionResult = new LastSessionDto(
            Guid.NewGuid(), "Hero", "zone-1", "fenwick-crossing", 10, 20);

        Render<GameEntry>();

        var uri = new Uri(Services.GetRequiredService<NavigationManager>().Uri);
        Assert.Contains("/Game/CharacterSelect", uri.AbsolutePath);
    }

    // ── Error Handling Tests ─────────────────────────────────────────────────

    /// <summary>
    /// Verifies the component shows an error state when
    /// <see cref="IGameApiClient.GetCharactersAsync"/> throws.
    /// </summary>
    [Fact]
    public void Shows_Error_When_GetCharacters_Fails()
    {
        _fakeApi.GetCharactersException = new InvalidOperationException("Network error");

        var cut = Render<GameEntry>();

        Assert.Contains("Connection Error", cut.Markup);
        Assert.Contains("Failed to connect to the game server", cut.Markup);
    }

    /// <summary>
    /// Verifies the component shows an error state when
    /// <see cref="IGameApiClient.GetLastSessionAsync"/> throws.
    /// </summary>
    [Fact]
    public void Shows_Error_When_GetLastSession_Fails()
    {
        _fakeApi.Characters = [CreateCharacterDto()];
        _fakeApi.GetLastSessionException = new InvalidOperationException("Network error");

        var cut = Render<GameEntry>();

        Assert.Contains("Connection Error", cut.Markup);
    }

    /// <summary>
    /// Verifies that clicking the Retry button re-attempts the discovery
    /// by incrementing the call count on <see cref="IGameApiClient.GetCharactersAsync"/>.
    /// </summary>
    [Fact]
    public void Retry_Button_Reattempts_Discovery()
    {
        _fakeApi.GetCharactersException = new InvalidOperationException("Network error");

        var cut = Render<GameEntry>();

        var initialCount = _fakeApi.GetCharactersCallCount;
        var retryButton = cut.Find("button");
        retryButton.Click();

        Assert.True(_fakeApi.GetCharactersCallCount > initialCount);
    }

    // ── Auth Reactivity Tests ────────────────────────────────────────────────

    /// <summary>
    /// Verifies that when auth becomes ready after the initial render
    /// (simulating the SSR prerender → circuit handoff), the component
    /// triggers character discovery.
    /// </summary>
    [Fact]
    public void Transitions_From_WaitingForAuth_When_Auth_Ready()
    {
        _fakeAuth.IsAuthReady = false;
        _fakeAuth.IsLoggedInOverride = true;
        _fakeApi.Characters = [CreateCharacterDto()];
        _fakeApi.LastSessionResult = null;

        var cut = Render<GameEntry>();

        Assert.Contains("Waiting for authentication", cut.Markup);

        // Simulate auth becoming ready.
        _fakeAuth.IsAuthReady = true;
        _fakeAuth.NotifyStateChanged();

        // OnAuthStateChanged invokes DiscoverAndRouteAsync via InvokeAsync.
        // Wait for the navigation to occur.
        cut.WaitForAssertion(() =>
        {
            var uri = new Uri(Services.GetRequiredService<NavigationManager>().Uri);
            Assert.Contains("/Game/CharacterSelect", uri.AbsolutePath);
        });
    }

    /// <summary>
    /// Verifies that when auth logs out while the component is visible,
    /// it redirects to <c>/login</c>.
    /// </summary>
    [Fact]
    public void Redirects_To_Login_When_Auth_Logs_Out()
    {
        _fakeAuth.IsLoggedInOverride = true;
        _fakeApi.Characters = [CreateCharacterDto()];
        _fakeApi.LastSessionResult = null;

        Render<GameEntry>();

        // Simulate auth logout.
        _fakeAuth.IsLoggedInOverride = false;
        _fakeAuth.NotifyStateChanged();

        // OnAuthStateChanged invokes Navigation.NavigateTo synchronously
        // (no InvokeAsync wrapping for the logout path), but wait anyway.
        cut.WaitForAssertion(() =>
        {
            var uri = new Uri(Services.GetRequiredService<NavigationManager>().Uri);
            Assert.Contains("/login", uri.AbsolutePath);
        });
    }
}
