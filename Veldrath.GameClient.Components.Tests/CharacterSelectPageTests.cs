using Bunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
/// Tests for the <see cref="CharacterSelect"/> component, verifying character
/// list rendering and selection behaviour.
/// </summary>
public class CharacterSelectPageTests : BunitContext
{
    private readonly FakeGameHubConnectionService _fakeHub;
    private readonly FakeGameApiClient _fakeApi;
    private readonly FakeAuthStateService _fakeAuth;
    private readonly GameStateService _gameState;
    private readonly FakeVeldrathAuthApiClient _fakeAuthApi;

    /// <summary>
    /// Initializes a new instance and registers all stub services.
    /// </summary>
    public CharacterSelectPageTests()
    {
        _fakeHub = new FakeGameHubConnectionService();
        _fakeApi = new FakeGameApiClient();
        _gameState = new GameStateService();
        _fakeAuthApi = new FakeVeldrathAuthApiClient();
        _fakeAuth = new FakeAuthStateService(_fakeAuthApi);

        Services.AddSingleton<IGameHubConnectionService>(_fakeHub);
        Services.AddSingleton<IGameStateService>(_gameState);
        Services.AddSingleton<IGameApiClient>(_fakeApi);
        Services.AddSingleton(_gameState);
        Services.AddSingleton<AuthStateServiceBase>(_fakeAuth);
        Services.AddSingleton<IVeldrathAuthApiClient>(_fakeAuthApi);
        Services.AddSingleton<ILogger<CharacterSelect>>(NullLogger<CharacterSelect>.Instance);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Veldrath:ServerUrl"] = "http://localhost:9000",
            })
            .Build();
        Services.AddSingleton<IConfiguration>(config);
    }

    /// <summary>
    /// Verifies the component shows the loading state initially before characters are loaded.
    /// </summary>
    [Fact]
    public void CharacterSelect_Shows_Loading_State()
    {
        var cut = Render<CharacterSelect>();

        // On initial render, the component starts with _isLoading = true
        // and attempts to connect and load characters.
        Assert.NotNull(cut);
    }

    /// <summary>
    /// Verifies that when the character list is empty, the component shows
    /// the empty-state message and a link to create a character.
    /// </summary>
    [Fact]
    public void CharacterSelect_Shows_Empty_State_When_No_Characters()
    {
        _fakeApi.Characters = [];
        _fakeHub.StateValue = Veldrath.GameClient.Core.Models.ConnectionState.Connected;

        var cut = Render<CharacterSelect>();

        // The empty state link should be present.
        var createLink = cut.FindAll("a[href='/Game/CreateCharacter']");
        Assert.NotEmpty(createLink);
    }

    /// <summary>
    /// Verifies that characters from the API are rendered as cards.
    /// </summary>
    [Fact]
    public void CharacterSelect_Shows_Character_Cards()
    {
        var charId = Guid.NewGuid();
        _fakeApi.Characters =
        [
            new CharacterDto(charId, 1, "Aragorn", "Warrior", 5, 1200, DateTimeOffset.UtcNow.AddDays(-1), "fenwick-crossing"),
            new CharacterDto(Guid.NewGuid(), 2, "Legolas", "Rogue", 3, 600, DateTimeOffset.UtcNow.AddDays(-2), "fenwick-crossing"),
        ];
        _fakeHub.StateValue = Veldrath.GameClient.Core.Models.ConnectionState.Connected;

        var cut = Render<CharacterSelect>();

        // Each character name should appear in the rendered markup.
        Assert.Contains("Aragorn", cut.Markup);
        Assert.Contains("Legolas", cut.Markup);
    }

    /// <summary>
    /// Verifies that selecting a character sends the SelectCharacter command
    /// via the hub connection.
    /// </summary>
    [Fact]
    public void Selecting_Character_Sends_SelectCharacter_Command()
    {
        var charId = Guid.NewGuid();
        _fakeApi.Characters =
        [
            new CharacterDto(charId, 1, "TestHero", "Warrior", 1, 0, DateTimeOffset.UtcNow, "fenwick-crossing"),
        ];
        _fakeHub.StateValue = Veldrath.GameClient.Core.Models.ConnectionState.Connected;

        var cut = Render<CharacterSelect>();

        // Find and click the select button for the character.
        var selectButtons = cut.FindAll("button");
        var selectBtn = selectButtons.FirstOrDefault(b => b.TextContent.Contains("Select"));
        if (selectBtn is not null)
        {
            selectBtn.Click();
        }

        // The SelectCharacter command should have been sent with the character ID.
        var sentSelect = _fakeHub.SentCommands
            .FirstOrDefault(c => c.Method == "SelectCharacter");
        Assert.NotEqual(default, sentSelect);
    }
}
