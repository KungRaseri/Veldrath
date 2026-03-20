using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive;
using RealmUnbound.Client.Services;

namespace RealmUnbound.Client.ViewModels;

/// <summary>In-game view model. Active after a character has entered a zone.</summary>
public class GameViewModel : ViewModelBase
{
    private readonly IServerConnectionService _connection;
    private readonly IZoneService _zoneService;
    private readonly TokenStore _tokens;
    private readonly INavigationService _navigation;

    // ── Zone state ────────────────────────────────────────────────────────────
    private string _zoneName = string.Empty;
    private string _zoneDescription = string.Empty;
    private string _characterName = string.Empty;
    private string _statusMessage = string.Empty;
    private string _currentZoneId = string.Empty;

    public string ZoneName
    {
        get => _zoneName;
        set => this.RaiseAndSetIfChanged(ref _zoneName, value);
    }

    public string ZoneDescription
    {
        get => _zoneDescription;
        set => this.RaiseAndSetIfChanged(ref _zoneDescription, value);
    }

    public string CharacterName
    {
        get => _characterName;
        set => this.RaiseAndSetIfChanged(ref _characterName, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    // ── Character stats ───────────────────────────────────────────────────────
    private int _unspentAttributePoints;
    private int _currentHealth;
    private int _maxHealth;
    private int _currentMana;
    private int _maxMana;
    private int _gold;

    /// <summary>Attribute points the character has earned but not yet spent.</summary>
    public int UnspentAttributePoints
    {
        get => _unspentAttributePoints;
        set => this.RaiseAndSetIfChanged(ref _unspentAttributePoints, value);
    }

    /// <summary>Current hit points of the active character.</summary>
    public int CurrentHealth
    {
        get => _currentHealth;
        set => this.RaiseAndSetIfChanged(ref _currentHealth, value);
    }

    /// <summary>Maximum hit points of the active character.</summary>
    public int MaxHealth
    {
        get => _maxHealth;
        set => this.RaiseAndSetIfChanged(ref _maxHealth, value);
    }

    /// <summary>Current mana points of the active character.</summary>
    public int CurrentMana
    {
        get => _currentMana;
        set => this.RaiseAndSetIfChanged(ref _currentMana, value);
    }

    /// <summary>Maximum mana points of the active character.</summary>
    public int MaxMana
    {
        get => _maxMana;
        set => this.RaiseAndSetIfChanged(ref _maxMana, value);
    }

    /// <summary>Gold currently held by the active character.</summary>
    public int Gold
    {
        get => _gold;
        set => this.RaiseAndSetIfChanged(ref _gold, value);
    }

    /// <summary>Players currently online in the same zone.</summary>
    public ObservableCollection<string> OnlinePlayers { get; } = [];

    /// <summary>Scrolling action log (last 100 entries).</summary>
    public ObservableCollection<string> ActionLog { get; } = [];

    /// <summary>Logs out the character, leaves the zone, and returns to the main menu.</summary>
    public ReactiveCommand<Unit, Unit> LogoutCommand { get; }

    /// <summary>Rest at the current zone's inn, restoring HP and MP at a cost of 10 gold.</summary>
    public ReactiveCommand<Unit, Unit> RestAtLocationCommand { get; }

    /// <summary>Spend unallocated attribute points by sending an allocation map to the server.</summary>
    public ReactiveCommand<Dictionary<string, int>, Unit> AllocateAttributePointsCommand { get; }

    /// <summary>Initializes a new instance of <see cref="GameViewModel"/>.</summary>
    public GameViewModel(
        IServerConnectionService connection,
        IZoneService zoneService,
        TokenStore tokens,
        INavigationService navigation)
    {
        _connection = connection;
        _zoneService = zoneService;
        _tokens = tokens;
        _navigation = navigation;

        CharacterName = tokens.Username ?? "Adventurer";
        LogoutCommand = ReactiveCommand.CreateFromTask(DoLogoutAsync);
        RestAtLocationCommand = ReactiveCommand.CreateFromTask(DoRestAtLocationAsync);
        AllocateAttributePointsCommand = ReactiveCommand.CreateFromTask<Dictionary<string, int>>(DoAllocateAttributePointsAsync);
    }

    /// <summary>Called by <see cref="CharacterSelectViewModel"/> after SelectCharacter + EnterZone succeeds.</summary>
    public async Task InitializeAsync(string characterName, string zoneId)
    {
        CharacterName = characterName;
        _currentZoneId = zoneId;

        var zone = await _zoneService.GetZoneAsync(zoneId);
        if (zone is not null)
        {
            ZoneName        = zone.Name;
            ZoneDescription = zone.Description;
        }

        AppendLog($"Welcome to {ZoneName}, {CharacterName}!");
    }

    /// <summary>Called from hub when another player enters the zone.</summary>
    public void OnPlayerEntered(string playerName)
    {
        if (!OnlinePlayers.Contains(playerName))
            OnlinePlayers.Add(playerName);
        AppendLog($"{playerName} entered the zone.");
    }

    /// <summary>Called from hub when another player leaves the zone.</summary>
    public void OnPlayerLeft(string playerName)
    {
        OnlinePlayers.Remove(playerName);
        AppendLog($"{playerName} left the zone.");
    }

    /// <summary>Called from hub when zone state is received with initial occupant list.</summary>
    public void SetOccupants(IEnumerable<string> playerNames)
    {
        OnlinePlayers.Clear();
        foreach (var name in playerNames)
            if (name != CharacterName)
                OnlinePlayers.Add(name);
    }

    /// <summary>Called from hub when the active character's attribute points have been allocated.</summary>
    public void OnAttributePointsAllocated(int remainingPoints, Dictionary<string, int> newAttributes)
    {
        UnspentAttributePoints = remainingPoints;
        AppendLog($"Attribute points allocated. Remaining unspent: {remainingPoints}.");
    }

    /// <summary>Called from hub when the active character has rested at a location.</summary>
    public void OnCharacterRested(int currentHealth, int maxHealth, int currentMana, int maxMana, int goldRemaining)
    {
        CurrentHealth = currentHealth;
        MaxHealth     = maxHealth;
        CurrentMana   = currentMana;
        MaxMana       = maxMana;
        Gold          = goldRemaining;
        AppendLog($"Rested. HP: {currentHealth}/{maxHealth}  MP: {currentMana}/{maxMana}  Gold: {goldRemaining}");
    }

    private async Task DoLogoutAsync()
    {
        // Leave zone, disconnect hub, then go back to main menu
        try { await _connection.SendCommandAsync<object>("LeaveZone", new { }); } catch { /* ignore */ }
        await _connection.DisconnectAsync();
        _navigation.NavigateTo<MainMenuViewModel>();
    }

    private async Task DoRestAtLocationAsync()
    {
        try
        {
            await _connection.SendCommandAsync<object>("RestAtLocation", _currentZoneId);
        }
        catch (Exception ex)
        {
            AppendLog($"Rest failed: {ex.Message}");
        }
    }

    private async Task DoAllocateAttributePointsAsync(Dictionary<string, int> allocations)
    {
        try
        {
            await _connection.SendCommandAsync<object>("AllocateAttributePoints", allocations);
        }
        catch (Exception ex)
        {
            AppendLog($"Attribute allocation failed: {ex.Message}");
        }
    }

    private void AppendLog(string message)
    {
        ActionLog.Add($"[{DateTime.Now:HH:mm}] {message}");
        while (ActionLog.Count > 100)
            ActionLog.RemoveAt(0);
    }
}
