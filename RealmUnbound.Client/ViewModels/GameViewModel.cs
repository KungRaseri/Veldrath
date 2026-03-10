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

    /// <summary>Players currently online in the same zone.</summary>
    public ObservableCollection<string> OnlinePlayers { get; } = [];

    /// <summary>Scrolling action log (last 100 entries).</summary>
    public ObservableCollection<string> ActionLog { get; } = [];

    public ReactiveCommand<Unit, Unit> LogoutCommand { get; }

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
    }

    /// <summary>Called by <see cref="CharacterSelectViewModel"/> after SelectCharacter + EnterZone succeeds.</summary>
    public async Task InitializeAsync(string characterName, string zoneId)
    {
        CharacterName = characterName;

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

    private async Task DoLogoutAsync()
    {
        // Leave zone, disconnect hub, then go back to main menu
        try { await _connection.SendCommandAsync<object>("LeaveZone", new { }); } catch { /* ignore */ }
        await _connection.DisconnectAsync();
        _navigation.NavigateTo<MainMenuViewModel>();
    }

    private void AppendLog(string message)
    {
        ActionLog.Add($"[{DateTime.Now:HH:mm}] {message}");
        while (ActionLog.Count > 100)
            ActionLog.RemoveAt(0);
    }
}
