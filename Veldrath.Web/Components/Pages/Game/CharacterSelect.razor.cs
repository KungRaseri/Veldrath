using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Veldrath.Web.Services;
using Veldrath.Contracts.Characters;
using Veldrath.Contracts.Connection;

namespace Veldrath.Web.Components.Pages.Game;

/// <summary>
/// Character selection screen — shows the player's characters and allows selecting one
/// to enter the game world.  Redirects to <c>/Login</c> if not authenticated.
/// </summary>
public sealed partial class CharacterSelect : IAsyncDisposable
{
    [Inject] private GameHubConnectionService Hub { get; set; } = null!;
    [Inject] private GameStateService GameState { get; set; } = null!;
    [Inject] private AuthStateService Auth { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    [Inject] private IConfiguration Configuration { get; set; } = null!;
    [Inject] private ILogger<CharacterSelect> Logger { get; set; } = null!;
    [Inject] private VeldrathApiClient Api { get; set; } = null!;

    private List<CharacterDto> _characters = [];
    private bool _isLoading = true;
    private string? _errorMessage;
    private string? _selectedCharacterId;
    private readonly List<IDisposable> _hubSubscriptions = [];

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        if (!Auth.IsLoggedIn)
        {
            Navigation.NavigateTo("/Login");
            return;
        }

        await ConnectAndLoadCharacters();
    }

    private async Task ConnectAndLoadCharacters()
    {
        _isLoading = true;
        _errorMessage = null;
        StateHasChanged();

        try
        {
            // Ensure the JWT is still fresh before connecting.
            var tokenValid = await Auth.TryRefreshAsync();
            if (!tokenValid || Auth.AccessToken is null)
            {
                Navigation.NavigateTo("/Login");
                return;
            }

            var serverUrl = Configuration["Veldrath:ServerUrl"];
            if (string.IsNullOrWhiteSpace(serverUrl))
                throw new InvalidOperationException(
                    "Veldrath:ServerUrl is not configured. Set it in appsettings.json, " +
                    "appsettings.{Environment}.json, or the Veldrath__ServerUrl environment variable.");

            // Register handler for ServerInfo — sets IsConnected = true on the GameState.
            _hubSubscriptions.Add(Hub.On<ServerInfoPayload>("ServerInfo", async payload =>
            {
                GameState.ApplyServerInfo(payload.ConnectionId);
                await InvokeAsync(StateHasChanged);
            }));

            // Register handler for CharacterSelected event BEFORE connecting.
            _hubSubscriptions.Add(Hub.On<CharacterSelectedPayload>("CharacterSelected", async payload =>
            {
                var characterInfo = new Services.CharacterBasicInfo(
                    payload.Id,
                    payload.Name,
                    payload.ClassName,
                    payload.Level,
                    payload.Experience,
                    payload.CurrentHealth,
                    payload.MaxHealth,
                    payload.CurrentMana,
                    payload.MaxMana,
                    payload.Gold);

                // Store the zone ID so Game.razor knows which zone to enter.
                GameState.ApplyCharacterSelected(characterInfo, payload.CurrentZoneId);
                await InvokeAsync(() => Navigation.NavigateTo("/Game/Play"));
            }));

            // Register handler for errors.
            _hubSubscriptions.Add(Hub.On<string>("Error", async error =>
            {
                Logger.LogWarning("Hub error: {Error}", error);
                await InvokeAsync(() =>
                {
                    _errorMessage = error;
                    StateHasChanged();
                });
            }));

            await Hub.ConnectAsync(serverUrl, Auth.AccessToken);

            // Load characters from REST API.
            _characters = await Api.GetCharactersAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to connect or load characters: {ExceptionType}: {ExceptionMessage}",
                ex.GetType().Name, ex.Message);
            _errorMessage = "Failed to connect to the game server. Please try again.";
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task SelectCharacter(Guid characterId)
    {
        _selectedCharacterId = characterId.ToString();
        StateHasChanged();

        try
        {
            await Hub.SendAsync("SelectCharacter", characterId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to select character {CharacterId}.", characterId);
            _errorMessage = "Failed to select character. Please try again.";
            _selectedCharacterId = null;
            StateHasChanged();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var sub in _hubSubscriptions)
        {
            sub.Dispose();
        }
        _hubSubscriptions.Clear();

        // Don't disconnect the hub here — Game.razor reuses the same scoped
        // GameHubConnectionService instance. Disconnecting would cause the
        // server to release the character claim and lose the connection context.
    }

    // ── Hub Event Payload DTOs ──────────────────────────────────────────────

    /// <summary>Payload received when a character is successfully selected on the server.</summary>
    /// <param name="Id">The character's unique identifier.</param>
    /// <param name="Name">The character's display name.</param>
    /// <param name="ClassName">The character's class name.</param>
    /// <param name="Level">The character's level.</param>
    /// <param name="Experience">Total experience points earned.</param>
    /// <param name="CurrentZoneId">The zone the character is currently in.</param>
    /// <param name="RegionId">The region the character belongs to.</param>
    /// <param name="CurrentHealth">Current health points.</param>
    /// <param name="MaxHealth">Maximum health points.</param>
    /// <param name="CurrentMana">Current mana points.</param>
    /// <param name="MaxMana">Maximum mana points.</param>
    /// <param name="Gold">Gold coins in possession.</param>
    /// <param name="UnspentAttributePoints">Unspent attribute points.</param>
    /// <param name="Strength">Strength attribute value.</param>
    /// <param name="Dexterity">Dexterity attribute value.</param>
    /// <param name="Constitution">Constitution attribute value.</param>
    /// <param name="Intelligence">Intelligence attribute value.</param>
    /// <param name="Wisdom">Wisdom attribute value.</param>
    /// <param name="Charisma">Charisma attribute value.</param>
    /// <param name="LearnedAbilities">List of ability slugs the character has learned.</param>
    /// <param name="SelectedAt">When the selection was confirmed on the server.</param>
    private sealed record CharacterSelectedPayload(
        Guid Id,
        string Name,
        string ClassName,
        int Level,
        long Experience,
        string? CurrentZoneId,
        string RegionId,
        int CurrentHealth,
        int MaxHealth,
        int CurrentMana,
        int MaxMana,
        int Gold,
        int UnspentAttributePoints,
        int Strength,
        int Dexterity,
        int Constitution,
        int Intelligence,
        int Wisdom,
        int Charisma,
        List<string> LearnedAbilities,
        DateTimeOffset SelectedAt);
}
