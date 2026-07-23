using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;
using MudBlazor;
using Veldrath.Auth.Blazor;
using Veldrath.Contracts.Characters;
using Veldrath.Contracts.Content;
using Veldrath.GameClient.Components.Models;
using Veldrath.GameClient.Core.Abstractions;

namespace Veldrath.GameClient.Components.Components.Pages;

public partial class CreateCharacter : IAsyncDisposable
{

    private List<ActorClassDto> _availableClasses = [];
    private ActorClassDto? _selectedClass;
    private List<SpeciesDto> _availableSpecies = [];
    private SpeciesDto? _selectedSpecies;
    private List<BackgroundDto> _availableBackgrounds = [];
    private BackgroundDto? _selectedBackground;
    private string _characterName = string.Empty;
    private string? _nameError;
    private bool _nameChecking;
    private bool? _nameAvailable;
    private bool _isInitializing = true;
    private bool _isCreating;
    private string? _loadError;
    private bool _isWaitingForAuth;
    private int _currentStep = 1;
    private Guid? _sessionId;
    private DateTime _sessionCreatedAt;
    private int _sessionTimeoutMinutes = 30;
    private bool _sessionExpired;
    private bool _isSavingAttributes;
    private bool _isSavingEquipment;

    // Per-step message display (M4)
    private List<StepMessage> _stepMessages = [];

    /// <summary>Clears all step messages.</summary>
    private void ClearStepMessages() => _stepMessages.Clear();

    /// <summary>Sets a single error step message, clearing any prior messages.</summary>
    /// <param name="message">The error message to display.</param>
    private void SetStepError(string message)
    {
        _stepMessages.Clear();
        _stepMessages.Add(new StepMessage(StepMessageStatus.Error, message));
    }

    /// <summary>Sets a single success step message, clearing any prior messages.</summary>
    /// <param name="message">The success message to display.</param>
    private void SetStepSuccess(string message)
    {
        _stepMessages.Clear();
        _stepMessages.Add(new StepMessage(StepMessageStatus.Success, message));
    }

    /// <summary>Sets a single warning step message, clearing any prior messages.</summary>
    /// <param name="message">The warning message to display.</param>
    private void SetStepWarning(string message)
    {
        _stepMessages.Clear();
        _stepMessages.Add(new StepMessage(StepMessageStatus.Warning, message));
    }

    // Character preview
    private CharacterPreviewDto? _characterPreview;
    private bool _isLoadingPreview;

    // Screen reader announcement text
    private string _srAnnouncement = string.Empty;

    // Focus management
    private ElementReference _stepHeadingRef;

    // Name check debounce (C6)
    private CancellationTokenSource? _nameCheckCts;

    // Auth readiness timeout — cancelled when auth becomes ready or component disposes.
    private CancellationTokenSource? _authTimeoutCts;

    // Point-buy configuration — populated from server session response.
    // Fallback defaults match the engine's PointBuyConfig until the API responds.
    private PointBuyConfigDto? _pointBuyConfig;
    private IReadOnlyDictionary<int, int> StatCosts => _pointBuyConfig?.CostTable ?? s_defaultStatCosts;
    private int MinStat => _pointBuyConfig?.MinStatValue ?? 8;
    private int MaxStat => _pointBuyConfig?.MaxStatValue ?? 15;
    private int TotalBudget => _pointBuyConfig?.TotalPoints ?? 27;

    private static readonly Dictionary<int, int> s_defaultStatCosts = new()
    {
        { 8, 0 }, { 9, 1 }, { 10, 2 }, { 11, 3 }, { 12, 4 }, { 13, 5 }, { 14, 7 }, { 15, 9 }
    };

    // C5: Stat keys use PascalCase to match desktop
    private Dictionary<string, int> _statAllocations = new()
    {
        { "Strength", 8 }, { "Dexterity", 8 }, { "Constitution", 8 },
        { "Intelligence", 8 }, { "Wisdom", 8 }, { "Charisma", 8 }
    };

    private int PointsSpent => _statAllocations.Sum(kvp => StatCosts[kvp.Value]);
    private int RemainingPoints => TotalBudget - PointsSpent;

    // C5: StatDisplayNames use PascalCase keys
    private static readonly Dictionary<string, string> StatDisplayNames = new()
    {
        { "Strength", "Strength (STR)" },
        { "Dexterity", "Dexterity (DEX)" },
        { "Constitution", "Constitution (CON)" },
        { "Intelligence", "Intelligence (INT)" },
        { "Wisdom", "Wisdom (WIS)" },
        { "Charisma", "Charisma (CHA)" }
    };

    // Equipment preferences — catalog populated from server session response.
    private EquipmentTypeCatalogDto? _equipmentCatalog;
    private IReadOnlyList<EquipmentTypeOptionDto> EquipmentArmorTypes =>
        _equipmentCatalog?.ArmorTypes ?? [];
    private IReadOnlyList<EquipmentTypeOptionDto> EquipmentWeaponTypes =>
        _equipmentCatalog?.WeaponTypes ?? [];
    private string _selectedArmorType = "";
    private string _selectedWeaponType = "";
    private bool _includeShield = false;

    // Difficulty mode
    private string _difficultyMode = "normal";
    private bool IsHardcore => _difficultyMode == "hardcore";

    /// <summary>Gets the number of minutes remaining before the creation session expires.
    /// Computed from <see cref="_sessionCreatedAt"/> and the server-provided session timeout.</summary>
    private int SessionMinutesRemaining => _sessionCreatedAt == default
        ? _sessionTimeoutMinutes
        : _sessionTimeoutMinutes - (int)(DateTime.UtcNow - _sessionCreatedAt).TotalMinutes;

    /// <summary>Gets a human-readable summary of the selected equipment preferences.</summary>
    private string EquipmentSummary
    {
        get
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(_selectedArmorType))
            {
                var display = EquipmentArmorTypes.FirstOrDefault(a =>
                    a.Slug == _selectedArmorType)?.DisplayName ?? _selectedArmorType;
                parts.Add(display);
            }

            if (!string.IsNullOrEmpty(_selectedWeaponType))
            {
                var display = EquipmentWeaponTypes.FirstOrDefault(w =>
                    w.Slug == _selectedWeaponType)?.DisplayName ?? _selectedWeaponType;
                parts.Add(display);
            }

            if (_includeShield)
                parts.Add("Shield");

            return parts.Count > 0 ? string.Join(", ", parts) : "Class Defaults";
        }
    }

    private bool _hasLoadedData;

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
            _isInitializing = false;
            _isWaitingForAuth = true;
            StartAuthTimeout();
            return;
        }

        if (!Auth.IsLoggedIn)
        {
            try { Navigation.NavigateTo("/login"); } catch (InvalidOperationException) { }
            return;
        }

        Navigation.LocationChanged += OnLocationChanged;

        await LoadDataAsync();
    }

    /// <summary>Starts a 10-second countdown; if auth is still not ready when it fires,
    /// sets <see cref="_loadError"/> so the error UI with Retry button appears.</summary>
    private async void StartAuthTimeout()
    {
        _authTimeoutCts?.Cancel();
        _authTimeoutCts = new CancellationTokenSource();
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), _authTimeoutCts.Token);
            if (!Auth.IsAuthReady)
            {
                _isWaitingForAuth = false;
                _loadError = "Authentication is taking longer than expected. The server may be starting up.";
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (TaskCanceledException)
        {
            // Timeout was cancelled — auth became ready or component disposed.
        }
    }

    /// <summary>Reacts to auth state changes. Cancels any pending auth timeout and triggers
    /// data load when auth becomes ready after prerendering, or refreshes the UI otherwise.
    /// Redirects to the login page when the auth state indicates the user is no longer logged in
    /// (e.g. after the <see cref="AuthDelegatingHandler"/> cleared stale tokens on a 401).</summary>
    private void OnAuthStateChanged()
    {
        if (!Auth.IsAuthReady)
            return;

        // Cancel any pending auth timeout since auth is now ready.
        _authTimeoutCts?.Cancel();

        // If the auth state was cleared (e.g. by the delegating handler after a renewal
        // failure), redirect to login so the user can re-authenticate.
        if (!Auth.IsLoggedIn)
        {
            try { Navigation.NavigateTo("/login"); } catch (InvalidOperationException) { }
            return;
        }

        if (!_hasLoadedData && Auth.IsLoggedIn)
        {
            _isWaitingForAuth = false;
            _hasLoadedData = true;
            InvokeAsync(async () =>
            {
                Navigation.LocationChanged += OnLocationChanged;
                await LoadDataAsync();
                StateHasChanged();
            });
            return;
        }

        InvokeAsync(StateHasChanged);
    }

    /// <summary>Fetches all reference data (classes, species, backgrounds) in parallel and begins a creation
    /// session.</summary>
    private async Task LoadDataAsync()
    {
        _isInitializing = true;
        _isWaitingForAuth = false;
        _loadError = null;
        StateHasChanged();

        try
        {
            // Ensure the JWT is fresh before making API calls.
            await Auth.TryRefreshAsync();

            // M5: Parallelize content loading with Task.WhenAll
            var classesTask = Api.GetClassesAsync();
            var speciesTask = Api.GetSpeciesAsync();
            var backgroundsTask = Api.GetBackgroundsAsync();
            var sessionTask = Api.BeginCreationSessionAsync();

            await Task.WhenAll(classesTask, speciesTask, backgroundsTask, sessionTask);

            // Process results individually with error handling per task
            List<ActorClassDto>? classes = null;
            try
            {
                classes = await classesTask;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to load available classes");
                _loadError = "Could not load available classes. Please check your connection and try again.";
                return;
            }

            List<SpeciesDto>? speciesList = null;
            try
            {
                speciesList = await speciesTask;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to load available species");
                _loadError = "Could not load available species. Please check your connection and try again.";
                return;
            }

            List<BackgroundDto>? backgrounds = null;
            try
            {
                backgrounds = await backgroundsTask;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to load available backgrounds");
                _loadError = "Could not load available backgrounds. Please check your connection and try again.";
                return;
            }

            _availableClasses = classes!;
            _availableSpecies = speciesList!;
            _availableBackgrounds = backgrounds!;

            // M1: Auto-select first item in each list
            if (_availableClasses.Count > 0)
            {
                _selectedClass = _availableClasses[0];
            }

            if (_availableSpecies.Count > 0)
            {
                _selectedSpecies = _availableSpecies[0];
            }

            if (_availableBackgrounds.Count > 0)
            {
                _selectedBackground = _availableBackgrounds[0];
            }

            // Process session creation result — extract config for point-buy and equipment catalogs.
            try
            {
                var sessionResult = await sessionTask;
                if (sessionResult is { Success: true })
                {
                    _sessionId = sessionResult.SessionId;
                    _sessionCreatedAt = DateTime.UtcNow;
                    _pointBuyConfig = sessionResult.PointBuyConfig;
                    _equipmentCatalog = sessionResult.EquipmentTypeCatalog;
                    _sessionTimeoutMinutes = sessionResult.SessionTimeoutMinutes;
                }
                else
                {
                    Logger.LogWarning("BeginCreationSessionAsync returned null or failed; proceeding without session tracking.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to begin creation session; proceeding without session tracking.");
            }

            _hasLoadedData = true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error during character creation initialization");
            _loadError = "An unexpected error occurred while loading character creation. Please try again.";
        }
        finally
        {
            _isInitializing = false;
            StateHasChanged();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Auth.OnChange -= OnAuthStateChanged;
        Navigation.LocationChanged -= OnLocationChanged;
        _nameCheckCts?.Cancel();
        _nameCheckCts?.Dispose();
        _authTimeoutCts?.Cancel();
        _authTimeoutCts?.Dispose();

        await DisableBeforeUnloadAsync();

        if (_sessionId.HasValue)
        {
            try
            {
                await Api.AbandonCreationSessionAsync(_sessionId.Value);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to abandon creation session {SessionId}", _sessionId);
            }
        }
    }

    /// <summary>Handles in-app navigation away from the wizard by abandoning the creation session.</summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">The location changed event arguments.</param>
    private async void OnLocationChanged(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
    {
        if (!e.Location.Contains("/Game/CreateCharacter", StringComparison.OrdinalIgnoreCase))
        {
            await AbandonSessionIfNeeded();
        }
    }

    /// <summary>Best-effort session abandonment for circuit disconnect or navigation away scenarios.
    /// Does not throw — failures are silently logged.</summary>
    private async Task AbandonSessionIfNeeded()
    {
        if (_sessionId.HasValue)
        {
            try
            {
                await Api.AbandonCreationSessionAsync(_sessionId.Value);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to abandon creation session {SessionId} on navigation", _sessionId);
            }
        }
    }

    /// <inheritdoc />
    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && _hasLoadedData)
        {
            try
            {
                await JsRuntime.InvokeVoidAsync("characterCreation.enableBeforeUnload");
            }
            catch (JSException)
            {
                // JS module not loaded yet (e.g., after circuit reconnect on page refresh).
                // Safe to ignore — the beforeunload warning isn't critical,
                // and the module will be available on subsequent renders.
            }
        }
    }

    /// <summary>Removes the browser beforeunload listener that warns users about unsaved progress.
    /// Swallows JS interop failures from prerendering or disconnected circuits.</summary>
    private async Task DisableBeforeUnloadAsync()
    {
        try
        {
            await JsRuntime.InvokeVoidAsync("characterCreation.disableBeforeUnload");
        }
        catch (JSDisconnectedException)
        {
            // Circuit already disconnected — nothing to clean up.
        }
        catch (InvalidOperationException)
        {
            // Still in static prerendering — JS interop not available.
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to disable beforeunload listener");
        }
    }

    /// <summary>Moves focus to the current step heading for screen reader accessibility.</summary>
    private async Task FocusStepHeadingAsync()
    {
        await Task.Delay(50); // Allow DOM to update after step change
        if (_stepHeadingRef.Context != null)
        {
            await _stepHeadingRef.FocusAsync();
        }
    }

    /// <summary>Navigates to the specified step, clears any step error, and moves focus to its heading. (M4)</summary>
    /// <param name="step">The step number (1–7) to navigate to.</param>
    private async Task NavigateToStepAsync(int step)
    {
        ClearStepMessages();
        _currentStep = step;
        await FocusStepHeadingAsync();
    }

    /// <summary>Handles keyboard navigation on selection cards (Enter or Space to select).</summary>
    /// <param name="e">The keyboard event arguments.</param>
    /// <param name="action">The selection action to invoke.</param>
    private async Task OnCardKeyDownAsync(KeyboardEventArgs e, Func<Task> action)
    {
        if (e.Key is "Enter" or " ")
        {
            await action();
        }
    }

    /// <summary>Selects a class and eagerly persists the choice to the creation session.</summary>
    /// <param name="cls">The class to select.</param>
    private async Task SelectClass(ActorClassDto cls)
    {
        _selectedClass = cls;

        if (_sessionId.HasValue)
        {
            try
            {
                await Auth.TryRefreshAsync();
                await Api.SetCreationClassAsync(_sessionId.Value, cls.DisplayName);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to persist class choice to session {SessionId}", _sessionId);
                SetStepWarning("Failed to save class choice. You can continue, but it may not be recorded.");
            }
        }
    }

    /// <summary>Selects a species and eagerly persists the choice to the creation session.</summary>
    /// <param name="species">The species to select.</param>
    private async Task SelectSpecies(SpeciesDto species)
    {
        _selectedSpecies = species;

        if (_sessionId.HasValue)
        {
            try
            {
                await Auth.TryRefreshAsync();
                await Api.SetCreationSpeciesAsync(_sessionId.Value, species.Slug);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to persist species choice to session {SessionId}", _sessionId);
                SetStepWarning("Failed to save species choice. You can continue, but it may not be recorded.");
            }
        }
    }

    /// <summary>Selects a background and eagerly persists the choice to the creation session.</summary>
    /// <param name="background">The background to select.</param>
    private async Task SelectBackground(BackgroundDto background)
    {
        _selectedBackground = background;

        if (_sessionId.HasValue)
        {
            try
            {
                await Auth.TryRefreshAsync();
                await Api.SetCreationBackgroundAsync(_sessionId.Value, background.Slug);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to persist background choice to session {SessionId}", _sessionId);
                SetStepWarning("Failed to save background choice. You can continue, but it may not be recorded.");
            }
        }
    }

    // Step navigation methods. Steps are reordered to match desktop:
    // 1=Name, 2=Class, 3=Species, 4=Background, 5=Attributes, 6=Equipment, 7=Review
    private async Task GoToStep1Async() => await NavigateToStepAsync(1);
    private async Task GoToStep3Async() => await NavigateToStepAsync(3);
    private async Task GoToStep4Async() => await NavigateToStepAsync(4);
    private async Task GoToStep5Async() => await NavigateToStepAsync(5);
    private async Task GoToStep6Async() => await NavigateToStepAsync(6);

    /// <summary>Saves the character name to the session and advances from step 1 (Name)
    /// to step 2 (Class). (C3+M8)</summary>
    private async Task GoToStep2Async()
    {
        ClearStepMessages();

        var trimmedName = _characterName.Trim();

        // Save name to session before advancing (matching desktop behavior)
        if (_sessionId.HasValue && !string.IsNullOrWhiteSpace(trimmedName))
        {
            try
            {
                await Auth.TryRefreshAsync();
                var result = await Api.SetCreationNameAsync(_sessionId.Value, trimmedName);
                if (result is not { Success: true })
                {
                    SetStepError("Failed to save character name to session. Please try again.");
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to persist character name to session {SessionId}", _sessionId);
                SetStepError("Failed to save character name. Please try again.");
                return;
            }
        }

        await NavigateToStepAsync(2);
    }

    /// <summary>Saves equipment preferences and advances from step 6 (Equipment) to step 7 (Review).</summary>
    private async Task GoToStep7SaveEquipmentAsync()
    {
        ClearStepMessages();

        // Only call the API if at least one equipment preference is set.
        if (_sessionId.HasValue &&
            (!string.IsNullOrEmpty(_selectedArmorType) || !string.IsNullOrEmpty(_selectedWeaponType) || _includeShield))
        {
            _isSavingEquipment = true;
            try
            {
                await Auth.TryRefreshAsync();
                var armorSlug = string.IsNullOrEmpty(_selectedArmorType) ? null : _selectedArmorType;
                var weaponSlug = string.IsNullOrEmpty(_selectedWeaponType) ? null : _selectedWeaponType;
                await Api.SetCreationEquipmentPreferencesAsync(_sessionId.Value, armorSlug, weaponSlug, _includeShield);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to persist equipment preferences to session {SessionId}", _sessionId);
                SetStepWarning("Failed to save equipment preferences. You can continue, but defaults will be used.");
            }
            finally
            {
                _isSavingEquipment = false;
            }
        }

        _currentStep = 7;
        await FocusStepHeadingAsync();
        await LoadPreviewAsync();
    }

    /// <summary>Loads a non-persisted character preview from the server.
    /// Failures are silently ignored — the preview is non-critical.</summary>
    private async Task LoadPreviewAsync()
    {
        if (_sessionId == Guid.Empty || _sessionId is null)
            return;

        _isLoadingPreview = true;
        StateHasChanged();

        try
        {
            await Auth.TryRefreshAsync();
            _characterPreview = await Api.GetCreationPreviewAsync(_sessionId.Value);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to load character preview for session {SessionId}", _sessionId);
            _characterPreview = null;
        }
        finally
        {
            _isLoadingPreview = false;
            StateHasChanged();
        }
    }

    /// <summary>Sets difficulty mode to normal.</summary>
    private void SetNormalDifficulty() => _difficultyMode = "normal";

    /// <summary>Sets difficulty mode to hardcore.</summary>
    private void SetHardcoreDifficulty() => _difficultyMode = "hardcore";

    /// <summary>Increases the specified stat by 1 if within budget and cap.</summary>
    /// <param name="statKey">The PascalCase stat key (e.g. <c>"Strength"</c>).</param>
    private async Task IncreaseStatAsync(string statKey)
    {
        if (!_statAllocations.TryGetValue(statKey, out var currentValue))
            return;

        var nextValue = currentValue + 1;
        if (nextValue > MaxStat)
            return;

        if (!StatCosts.TryGetValue(nextValue, out var nextCost))
            return;

        var currentCost = StatCosts[currentValue];
        if (PointsSpent - currentCost + nextCost > TotalBudget)
            return;

        _statAllocations[statKey] = nextValue;
        await SaveAttributesAsync();

        if (RemainingPoints == 0)
        {
            _srAnnouncement = "All points allocated";
        }
    }

    /// <summary>Decreases the specified stat by 1 if above the minimum.</summary>
    /// <param name="statKey">The PascalCase stat key (e.g. <c>"Strength"</c>).</param>
    private async Task DecreaseStatAsync(string statKey)
    {
        if (!_statAllocations.TryGetValue(statKey, out var currentValue))
            return;

        if (currentValue <= MinStat)
            return;

        _statAllocations[statKey] = currentValue - 1;
        await SaveAttributesAsync();
    }

    /// <summary>Eagerly persists the current stat allocations to the creation session.</summary>
    private async Task SaveAttributesAsync()
    {
        if (!_sessionId.HasValue)
            return;

        _isSavingAttributes = true;
        try
        {
            await Auth.TryRefreshAsync();
            await Api.SetCreationAttributesAsync(_sessionId.Value, new Dictionary<string, int>(_statAllocations));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to persist attribute allocation to session {SessionId}", _sessionId);
        }
        finally
        {
            _isSavingAttributes = false;
            StateHasChanged();
        }
    }

    /// <summary>Handles name input changes with client-side validation
    /// and a 400 ms debounced server availability check. (C2, C6)</summary>
    private async Task OnNameChanged()
    {
        _nameCheckCts?.Cancel();
        _nameCheckCts = new CancellationTokenSource();
        var ct = _nameCheckCts.Token;

        _nameError = null;
        _nameAvailable = null;

        var trimmed = _characterName?.Trim() ?? string.Empty;

        // C2: Letters-only validation (no spaces, matching desktop and server)
        if (trimmed.Length < 2)
        {
            _nameError = "Name must be at least 2 characters.";
            SetStepError("Name must be at least 2 characters");
            return;
        }
        if (trimmed.Length > 20)
        {
            _nameError = "Name must be at most 20 characters.";
            SetStepError("Name must be at most 20 characters");
            return;
        }
        if (!System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[a-zA-Z ]+$"))
        {
            _nameError = "Name may only contain letters and spaces.";
            SetStepError("Name may only contain letters and spaces");
            return;
        }

        _nameChecking = true;

        try
        {
            // Ensure the JWT is fresh before the server check.
            var tokenValid = await Auth.TryRefreshAsync();
            if (!tokenValid || Auth.AccessToken is null)
            {
                _nameError = "Your session has expired. Please refresh the page.";
                SetStepError("Session expired — please refresh the page");
                _nameChecking = false;
                return;
            }

            // C6: 400 ms debounce before server check, matching desktop behavior
            await Task.Delay(400, ct);
            if (ct.IsCancellationRequested)
                return;

            var result = await Api.CheckCharacterNameAsync(trimmed);
            if (ct.IsCancellationRequested)
                return;

            if (result is null)
            {
                _nameError = "Unexpected empty response from server. Please try again.";
                SetStepError("Error checking name availability");
            }
            else if (!result.Available)
            {
                _nameError = result.Error ?? "Name is not available.";
                _nameAvailable = false;
                SetStepError("Name is not available");
            }
            else
            {
                _nameAvailable = true;
                _characterName = trimmed;
                SetStepSuccess("Name is available");
            }
        }
        catch (TaskCanceledException)
        {
            // Debounced — a newer keystroke cancelled this check
        }
        catch (HttpRequestException ex) when (ex.StatusCode is not null && (int)ex.StatusCode == 401)
        {
            Logger.LogWarning(ex, "Session expired while checking character name availability.");
            _nameError = "Your session has expired. Please refresh the page.";
            SetStepError("Session expired — please refresh the page");
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "HTTP error while checking character name availability.");
            _nameError = "Server error while checking name. Please try again later.";
            SetStepError("Server error while checking name");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to check character name availability.");
            _nameError = "Could not verify name availability. Please try again.";
            SetStepError("Could not verify name availability");
        }
        finally
        {
            if (!ct.IsCancellationRequested)
            {
                _nameChecking = false;
            }
        }
    }

    /// <summary>Finalizes character creation. Passes <c>null</c> for the character name
    /// since it was already saved via <see cref="SetCreationNameAsync"/>. (C3+M8)</summary>
    private async Task ConfirmCreate()
    {
        if (_selectedClass is null || string.IsNullOrWhiteSpace(_characterName))
            return;

        _isCreating = true;
        _loadError = null;
        ClearStepMessages();

        try
        {
            if (!_sessionId.HasValue)
            {
                SetStepError("No active creation session. Please restart the wizard.");
                return;
            }

            // Ensure the JWT is fresh before the final creation call.
            await Auth.TryRefreshAsync();

            // C3+M8: Pass null for CharacterName — the server reads it from session state
            // (set earlier via SetCreationNameAsync in GoToStep2Async).
            var result = await Api.FinalizeCreationSessionAsync(
                _sessionId.Value,
                new FinalizeCreationSessionRequest(null, _difficultyMode));

            if (result is null)
            {
                SetStepError("Failed to create character. The name may already be taken.");
            }
            else
            {
                Navigation.NavigateTo("/Game/CharacterSelect");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to create character.");
            SetStepError("Failed to create character. Please try again.");
        }
        finally
        {
            _isCreating = false;
        }
    }

    /// <summary>Abandons the current creation session and returns to character select.</summary>
    private async Task CancelWizard()
    {
        if (_sessionId.HasValue)
        {
            try
            {
                await Api.AbandonCreationSessionAsync(_sessionId.Value);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to abandon creation session {SessionId} during cancel", _sessionId);
            }
        }

        Navigation.NavigateTo("/Game/CharacterSelect");
    }

    private async Task ResetWizard()
    {
        _loadError = null;
        ClearStepMessages();
        _sessionExpired = false;
        _currentStep = 1;

        // Abandon the old session and start a fresh one.
        if (_sessionId.HasValue)
        {
            try { await Api.AbandonCreationSessionAsync(_sessionId.Value); }
            catch (Exception ex) { Logger.LogWarning(ex, "Failed to abandon session during reset"); }
        }

        _sessionId = null;
        _sessionCreatedAt = default;
        _sessionTimeoutMinutes = 30;
        _pointBuyConfig = null;
        _equipmentCatalog = null;
        _selectedClass = null;
        _selectedSpecies = null;
        _selectedBackground = null;
        _characterName = string.Empty;
        _selectedArmorType = "";
        _selectedWeaponType = "";
        _includeShield = false;
        _difficultyMode = "normal";
        _statAllocations = new Dictionary<string, int>
        {
            { "Strength", 8 }, { "Dexterity", 8 }, { "Constitution", 8 },
            { "Intelligence", 8 }, { "Wisdom", 8 }, { "Charisma", 8 }
        };

        try
        {
            var sessionResult = await Api.BeginCreationSessionAsync();
            if (sessionResult is { Success: true })
            {
                _sessionId = sessionResult.SessionId;
                _sessionCreatedAt = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to begin new session during reset");
        }

        await FocusStepHeadingAsync();
    }

    /// <summary>Returns the appropriate helper text for the character name field based on validation state.</summary>
    private string GetNameHelperText()
    {
        if (_nameError is not null) return _nameError;
        if (_nameChecking) return "Checking availability...";
        if (_nameAvailable == true) return "Name is available!";
        return string.Empty;
    }

    /// <summary>Returns the adornment position for the character name field.
    /// Shows an icon when there is validation state.</summary>
    private Adornment GetNameAdornment()
    {
        if (_nameError is not null || _nameChecking || _nameAvailable == true)
            return Adornment.End;
        return Adornment.None;
    }

    /// <summary>Returns the appropriate icon for the character name field adornment based on validation state.</summary>
    private string GetNameAdornmentIcon()
    {
        if (_nameError is not null) return Icons.Material.Filled.Error;
        if (_nameChecking) return Icons.Material.Filled.HourglassEmpty;
        if (_nameAvailable == true) return Icons.Material.Filled.CheckCircle;
        return string.Empty;
    }

    /// <summary>Returns the appropriate color for the character name field adornment icon
    /// based on validation state.</summary>
    private Color GetNameAdornmentColor()
    {
        if (_nameError is not null) return Color.Error;
        if (_nameChecking) return Color.Warning;
        if (_nameAvailable == true) return Color.Success;
        return Color.Default;
    }

    /// <summary>Thin wrapper around <see cref="OnNameChanged"/> for the MudTextField OnBlur event callback.</summary>
    private async Task OnNameChangedAsync() => await OnNameChanged();
}
