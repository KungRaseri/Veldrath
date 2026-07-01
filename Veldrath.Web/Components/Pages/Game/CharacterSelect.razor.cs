using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Veldrath.Contracts.Characters;
using Veldrath.Contracts.Connection;
using Veldrath.GameClient.Core.Abstractions;
using Veldrath.GameClient.Core.Payloads;
using Veldrath.GameClient.Core.Services;
using Veldrath.Web.Services;

namespace Veldrath.Web.Components.Pages.Game;

/// <summary>
/// Character selection screen — shows the player's characters and allows selecting one
/// to enter the game world.  Redirects to <c>/Login</c> if not authenticated.
/// </summary>
public sealed partial class CharacterSelect : IAsyncDisposable
{
    [Inject] private IGameHubConnectionService Hub { get; set; } = null!;
    [Inject] private IGameStateService GameState { get; set; } = null!;
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
                if (GameState is GameStateService gs)
                {
                    gs.ApplyServerInfo(payload.ConnectionId);
                }
                await InvokeAsync(StateHasChanged);
            }));

            // Register handler for CharacterSelected event BEFORE connecting.
            _hubSubscriptions.Add(Hub.On<CharacterSelectedPayload>("CharacterSelected", async payload =>
            {
                GameState.ApplyCharacterSelected(payload);
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
}
