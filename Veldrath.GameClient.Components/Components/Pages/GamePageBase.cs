using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Veldrath.Auth.Blazor;

namespace Veldrath.GameClient.Components.Components.Pages;

/// <summary>
/// Abstract base class for all game pages that need authentication.
/// Handles the auth-check-wait-retry pattern consistently:
/// <list type="number">
///   <item>Waits for <see cref="AuthStateServiceBase.IsAuthReady"/> if auth is not yet available.</item>
///   <item>Redirects to <c>/login</c> if the user is not logged in.</item>
///   <item>Calls <see cref="OnGameInitializedAsync"/> when auth is ready and logged in.</item>
///   <item>Retries initialization when auth becomes ready after a delayed start (Blazor Server circuit timing).</item>
/// </list>
/// </summary>
/// <remarks>
/// Derived pages override <see cref="OnGameInitializedAsync"/> instead of
/// <see cref="ComponentBase.OnInitializedAsync"/>. The base class manages auth state
/// subscription, timeout handling, and navigation redirects.
/// </remarks>
public abstract class GamePageBase : ComponentBase, IAsyncDisposable
{
    private bool _initializationAttempted;
    private CancellationTokenSource? _authTimeoutCts;

    /// <summary>Gets or sets the auth state service, injected by DI.</summary>
    [Inject]
    protected AuthStateServiceBase Auth { get; set; } = null!;

    /// <summary>Gets or sets the navigation manager, injected by DI.</summary>
    [Inject]
    protected NavigationManager Navigation { get; set; } = null!;

    /// <summary>Gets or sets the logger, injected by DI.</summary>
    [Inject]
    private ILogger<GamePageBase> Logger { get; set; } = null!;

    /// <summary>
    /// Gets a value indicating whether the user is authenticated and auth is ready.
    /// Derived pages can use this to gate UI rendering.
    /// </summary>
    protected bool IsAuthReady => Auth.IsAuthReady && Auth.IsLoggedIn;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Auth.OnChange += OnAuthStateChanged;
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await TryInitializeAsync();
    }

    /// <inheritdoc />
    /// <remarks>
    /// After a Blazor Server circuit reconnect, <see cref="OnInitializedAsync"/> may
    /// not re-execute. This override recovers from a stuck waiting state when auth
    /// is now ready.
    /// </remarks>
    protected override async Task OnParametersSetAsync()
    {
        if (!_initializationAttempted && IsAuthReady)
        {
            _authTimeoutCts?.Cancel();
            _initializationAttempted = true;
            await OnGameInitializedAsync();
        }
    }

    /// <summary>
    /// Called when the auth state changes. Retries initialization if auth became
    /// ready after the initial attempt failed.
    /// </summary>
    private void OnAuthStateChanged()
    {
        if (!Auth.IsAuthReady)
            return;

        _authTimeoutCts?.Cancel();

        if (!Auth.IsLoggedIn)
        {
            SafeNavigate("/login");
            return;
        }

        if (!_initializationAttempted)
        {
            _initializationAttempted = true;
            InvokeAsync(async () =>
            {
                await OnGameInitializedAsync();
                StateHasChanged();
            });
            return;
        }

        InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Attempts to initialize. If auth is not ready, starts the auth timeout and
    /// waits for <see cref="OnAuthStateChanged"/> to trigger a retry.
    /// If auth is ready but the user is not logged in, redirects to <c>/login</c>.
    /// </summary>
    private async Task TryInitializeAsync()
    {
        if (!Auth.IsAuthReady)
        {
            _initializationAttempted = false;
            StartAuthTimeout();
            return;
        }

        if (!Auth.IsLoggedIn)
        {
            SafeNavigate("/login");
            return;
        }

        _initializationAttempted = true;
        await OnGameInitializedAsync();
    }

    /// <summary>
    /// Override in derived pages to perform page-specific initialization
    /// after auth is confirmed ready. This is called in place of
    /// <see cref="OnInitializedAsync"/>.
    /// </summary>
    protected abstract Task OnGameInitializedAsync();

    /// <summary>
    /// Starts a 10-second countdown. If auth is still not ready when it fires,
    /// logs a warning. The page remains in its waiting state until auth becomes
    /// ready via <see cref="OnAuthStateChanged"/> or a manual retry.
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
                Logger.LogWarning(
                    "Auth not ready after 10-second timeout — waiting for auth state change");
            }
        }
        catch (TaskCanceledException)
        {
            // Timeout cancelled — auth became ready or component disposed.
        }
    }

    /// <summary>
    /// Navigates to the specified URI, catching any exceptions that may occur
    /// if the circuit is terminating or the component has been disposed.
    /// </summary>
    /// <param name="uri">The target URI to navigate to.</param>
    protected void SafeNavigate(string uri)
    {
        try { Navigation.NavigateTo(uri); }
        catch (Exception ex) { Logger.LogWarning(ex, "Navigation to {Uri} failed", uri); }
    }

    /// <inheritdoc />
    public virtual ValueTask DisposeAsync()
    {
        Auth.OnChange -= OnAuthStateChanged;
        _authTimeoutCts?.Cancel();
        _authTimeoutCts?.Dispose();
        return ValueTask.CompletedTask;
    }
}
