using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;
using Veldrath.Auth.Blazor;
using Veldrath.Contracts.Characters;
using Veldrath.GameClient.Components.Components.Dialogs;
using Veldrath.GameClient.Core.Abstractions;

namespace Veldrath.GameClient.Components.Components.Pages;

/// <summary>
/// Routing guard component at <c>/Game</c> that discovers the player's characters
/// and active session, then routes them to the appropriate destination:
/// <c>/Game/CreateCharacter</c> (no characters),
/// <c>/Game/CharacterSelect</c> (has characters, no active session), or
/// shows a resume dialog (has active session).
/// </summary>
/// <remarks>
/// Follows the same auth guard pattern used by <see cref="CharacterSelect"/>,
/// <see cref="Game"/>, and <see cref="CreateCharacter"/>: subscribes to
/// <see cref="AuthStateServiceBase.OnChange"/>, gates on
/// <see cref="AuthStateServiceBase.IsAuthReady"/> and
/// <see cref="AuthStateServiceBase.IsLoggedIn"/>, and refreshes the token
/// via <see cref="AuthStateServiceBase.TryRefreshAsync"/> before making API calls.
/// </remarks>
public sealed partial class GameEntry : ComponentBase, IAsyncDisposable
{
    private enum EntryPageState { Loading, WaitingForAuth, Error, Ready }

    private EntryPageState _state = EntryPageState.Loading;
    private string? _errorMessage;
    private CancellationTokenSource? _authTimeoutCts;

    /// <summary>Gets or sets the auth state service, injected by DI.</summary>
    [Inject]
    private AuthStateServiceBase Auth { get; set; } = null!;

    /// <summary>Gets or sets the game API client, injected by DI.</summary>
    [Inject]
    private IGameApiClient Api { get; set; } = null!;

    /// <summary>Gets or sets the navigation manager, injected by DI.</summary>
    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    /// <summary>Gets or sets the MudBlazor dialog service, injected by DI.</summary>
    [Inject]
    private IDialogService DialogService { get; set; } = null!;

    /// <summary>Gets or sets the logger, injected by DI.</summary>
    [Inject]
    private ILogger<GameEntry> Logger { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Auth.OnChange += OnAuthStateChanged;
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        if (!Auth.IsAuthReady)
        {
            _state = EntryPageState.WaitingForAuth;
            StartAuthTimeout();
            return;
        }

        if (!Auth.IsLoggedIn)
        {
            try { Navigation.NavigateTo("/login"); } catch (InvalidOperationException) { }
            return;
        }

        await DiscoverAndRouteAsync();
    }

    /// <summary>
    /// Reacts to auth state changes. Cancels any pending auth timeout and triggers
    /// discovery when auth becomes ready after prerendering. Redirects to the login
    /// page when the auth state indicates the user is no longer logged in.
    /// </summary>
    private void OnAuthStateChanged()
    {
        if (!Auth.IsAuthReady)
            return;

        _authTimeoutCts?.Cancel();

        if (!Auth.IsLoggedIn)
        {
            try { Navigation.NavigateTo("/login"); } catch (InvalidOperationException) { }
            return;
        }

        if (_state == EntryPageState.WaitingForAuth)
        {
            _state = EntryPageState.Loading;
            InvokeAsync(async () =>
            {
                await DiscoverAndRouteAsync();
                StateHasChanged();
            });
        }
    }

    /// <summary>
    /// Core routing logic: refreshes the token, checks character list,
    /// checks last session, and routes the user accordingly.
    /// </summary>
    private async Task DiscoverAndRouteAsync()
    {
        _state = EntryPageState.Loading;
        _errorMessage = null;
        StateHasChanged();

        try
        {
            var tokenValid = await Auth.TryRefreshAsync();
            if (!tokenValid || Auth.AccessToken is null)
            {
                Navigation.NavigateTo("/login");
                return;
            }

            // Step 1: Check character list.
            var characters = await Api.GetCharactersAsync();
            if (characters.Count == 0)
            {
                Navigation.NavigateTo("/Game/CreateCharacter");
                return;
            }

            // Step 2: Check last session.
            var session = await Api.GetLastSessionAsync();
            if (session is null)
            {
                Navigation.NavigateTo("/Game/CharacterSelect");
                return;
            }

            _state = EntryPageState.Ready;

            // Step 3: Show resume dialog.
            await ShowResumeDialogAsync(session);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to discover game session");
            _state = EntryPageState.Error;
            _errorMessage = "Failed to connect to the game server. Please try again.";
            StateHasChanged();
        }
    }

    /// <summary>
    /// Shows the resume session dialog and navigates based on the user's choice.
    /// </summary>
    /// <param name="session">The last session data returned by the server.</param>
    private async Task ShowResumeDialogAsync(LastSessionDto session)
    {
        var parameters = new DialogParameters
        {
            [nameof(ResumeSessionDialog.CharacterName)] = session.CharacterName,
            [nameof(ResumeSessionDialog.ZoneId)] = session.ZoneId,
        };

        var options = new DialogOptions
        {
            CloseOnEscapeKey = false,
            BackdropClick = false,
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
        };

        var dialog = await DialogService.ShowAsync<ResumeSessionDialog>(
            "Resume Session", parameters, options);

        var result = await dialog.Result;

        if (result?.Data is bool shouldResume && shouldResume)
        {
            Navigation.NavigateTo("/Game/Play");
        }
        else
        {
            Navigation.NavigateTo("/Game/CharacterSelect");
        }
    }

    /// <summary>
    /// Starts a 10-second countdown; if auth is still not ready when it fires,
    /// sets the error state so the error UI with Retry button appears.
    /// </summary>
    private async void StartAuthTimeout()
    {
        _authTimeoutCts?.Cancel();
        _authTimeoutCts = new CancellationTokenSource();
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), _authTimeoutCts.Token);
            if (!Auth.IsAuthReady)
            {
                _state = EntryPageState.Error;
                _errorMessage = "Authentication is taking longer than expected. The server may be starting up.";
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (TaskCanceledException)
        {
            // Timeout was cancelled — auth became ready or component disposed.
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Auth.OnChange -= OnAuthStateChanged;
        _authTimeoutCts?.Cancel();
        _authTimeoutCts?.Dispose();
    }
}
