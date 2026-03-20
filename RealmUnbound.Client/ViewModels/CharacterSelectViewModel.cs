using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive;
using RealmUnbound.Client.Services;
using RealmUnbound.Contracts.Characters;

namespace RealmUnbound.Client.ViewModels;

public class CharacterSelectViewModel : ViewModelBase
{
    private readonly ICharacterService _characters;
    private readonly IServerConnectionService _connection;
    private readonly INavigationService _navigation;
    private readonly GameViewModel _gameVm;
    private readonly ContentCache _contentCache;
    private readonly ClientSettings _settings;

    private bool _isCreating;
    private string _newCharacterName = string.Empty;
    private string _selectedClass = string.Empty;
    private IReadOnlyList<string> _availableClasses = ["Warrior"];

    // Hub subscriptions — stored so they can be disposed before re-subscribing on retry
    private IDisposable? _zoneEnteredSub;
    private IDisposable? _playerEnteredSub;
    private IDisposable? _playerLeftSub;
    private IDisposable? _hubErrorSub;
    private IDisposable? _characterStatusSub;
    private IDisposable? _characterAlreadyActiveSub;
    private IDisposable? _attrAllocatedSub;
    private IDisposable? _characterRestedSub;
    private IDisposable? _abilityUsedSub;
    private IDisposable? _skillXpGainedSub;
    private IDisposable? _itemEquippedSub;
    private IDisposable? _goldChangedSub;
    private IDisposable? _damageTakenSub;
    private IDisposable? _experienceGainedSub;

    public ObservableCollection<CharacterEntryViewModel> Characters { get; } = [];

    public bool IsCreating
    {
        get => _isCreating;
        set
        {
            this.RaiseAndSetIfChanged(ref _isCreating, value);
            this.RaisePropertyChanged(nameof(PanelTitle));
        }
    }

    public string NewCharacterName
    {
        get => _newCharacterName;
        set => this.RaiseAndSetIfChanged(ref _newCharacterName, value);
    }

    public string SelectedClass
    {
        get => _selectedClass;
        set => this.RaiseAndSetIfChanged(ref _selectedClass, value);
    }

    /// <summary>Gets the list of available character classes for the creation dropdown.
    /// Loaded from the content catalog on startup; falls back to the built-in list when the server is unavailable.</summary>
    public IReadOnlyList<string> AvailableClasses
    {
        get => _availableClasses;
        private set => this.RaiseAndSetIfChanged(ref _availableClasses, value);
    }

    /// <summary>Drives the top bar title — changes when switching between list and create panels.</summary>
    public string PanelTitle => IsCreating ? "New Character" : "Select Your Character";

    /// <summary>Gets or sets the base URL of the game server. Delegates to <see cref="ClientSettings.ServerBaseUrl"/>.</summary>
    public string ServerUrl
    {
        get => _settings.ServerBaseUrl;
        set => _settings.ServerBaseUrl = value;
    }

    public ReactiveCommand<CharacterEntryViewModel, Unit> SelectCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowCreateCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCreateCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateCommand { get; }
    public ReactiveCommand<CharacterEntryViewModel, Unit> DeleteCommand { get; }
    public ReactiveCommand<Unit, Unit> LogoutCommand { get; }

    /// <summary>Initializes a new instance of <see cref="CharacterSelectViewModel"/>.</summary>
    public CharacterSelectViewModel(
        ICharacterService characters,
        IServerConnectionService connection,
        INavigationService navigation,
        GameViewModel gameVm,
        IAuthService auth,
        ContentCache contentCache,
        ClientSettings settings)
    {
        _characters = characters;
        _connection = connection;
        _navigation = navigation;
        _gameVm = gameVm;
        _contentCache = contentCache;
        _settings = settings;

        var canCreate = this.WhenAnyValue(
            x => x.NewCharacterName, x => x.IsBusy, x => x.SelectedClass,
            (name, busy, cls) => !string.IsNullOrWhiteSpace(name) && !busy && !string.IsNullOrWhiteSpace(cls));

        SelectCommand = ReactiveCommand.CreateFromTask<CharacterEntryViewModel>(DoSelectAsync);
        ShowCreateCommand = ReactiveCommand.Create(() => { IsCreating = true; ClearError(); });
        CancelCreateCommand = ReactiveCommand.Create(() => { IsCreating = false; NewCharacterName = string.Empty; SelectedClass = string.Empty; ClearError(); });
        CreateCommand = ReactiveCommand.CreateFromTask(DoCreateAsync, canCreate);
        DeleteCommand = ReactiveCommand.CreateFromTask<CharacterEntryViewModel>(DoDeleteAsync);
        LogoutCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            await auth.LogoutAsync();
            navigation.NavigateTo<MainMenuViewModel>();
        });

        // Load on construction
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsBusy = true;
        ClearError();
        try
        {
            // Establish hub connection early so we can subscribe to real-time status change events.
            // ConnectAsync is idempotent — safe to call if already connected.
            await _connection.ConnectAsync(ServerUrl);

            // Subscribe to live character status updates (e.g. another client logs in/out)
            _characterStatusSub?.Dispose();
            _characterStatusSub = _connection.On<CharacterStatusPayload>("CharacterStatusChanged", payload =>
            {
                var entry = Characters.FirstOrDefault(c => c.Character.Id == payload.CharacterId);
                if (entry is not null)
                    entry.IsOnline = payload.IsOnline;
            });

            // Load class catalog from content service (best-effort; falls back to built-in list on failure).
            try
            {
                var classes = await _contentCache.GetClassesAsync();
                if (classes.Count > 0)
                    AvailableClasses = classes.Select(c => c.DisplayName).ToArray();
            }
            catch { /* keep hardcoded fallback */ }

            var list = await _characters.GetCharactersAsync();
            Characters.Clear();
            foreach (var c in list.OrderBy(x => x.SlotIndex))
                Characters.Add(new CharacterEntryViewModel(c));
        }
        catch
        {
            // Hub unavailable during load is non-fatal — characters still load via HTTP
            var list = await _characters.GetCharactersAsync();
            Characters.Clear();
            foreach (var c in list.OrderBy(x => x.SlotIndex))
                Characters.Add(new CharacterEntryViewModel(c));
        }
        finally { IsBusy = false; }
    }

    private async Task DoSelectAsync(CharacterEntryViewModel entry)
    {
        IsBusy = true;
        ClearError();
        try
        {
            var character = entry.Character;
            var zoneId = character.CurrentZoneId.Length > 0 ? character.CurrentZoneId : "starting-zone";

            await _connection.ConnectAsync(ServerUrl);

            // Dispose previous zone subscriptions to prevent duplicate handlers on retry
            _zoneEnteredSub?.Dispose();
            _playerEnteredSub?.Dispose();
            _playerLeftSub?.Dispose();
            _hubErrorSub?.Dispose();
            _characterAlreadyActiveSub?.Dispose();
            _attrAllocatedSub?.Dispose();
            _characterRestedSub?.Dispose();
            _abilityUsedSub?.Dispose();
            _skillXpGainedSub?.Dispose();
            _itemEquippedSub?.Dispose();
            _goldChangedSub?.Dispose();
            _damageTakenSub?.Dispose();
            _experienceGainedSub?.Dispose();

            // Subscribe to zone hub events before sending commands so no events are missed
            _zoneEnteredSub = _connection.On<ZoneEnteredPayload>("ZoneEntered", payload =>
            {
                _gameVm.SetOccupants(payload.Occupants.Select(o => o.CharacterName));
                _navigation.NavigateTo<GameViewModel>();
            });
            _playerEnteredSub = _connection.On<PlayerEventPayload>("PlayerEntered", payload =>
                _gameVm.OnPlayerEntered(payload.CharacterName));
            _playerLeftSub = _connection.On<PlayerEventPayload>("PlayerLeft", payload =>
                _gameVm.OnPlayerLeft(payload.CharacterName));
            _hubErrorSub = _connection.On<string>("Error", message =>
            {
                ErrorMessage = message;
                IsBusy = false;
            });

            // If the server rejects the selection because another connection already holds this
            // character, show the user a clear message and stay on the character select screen.
            _characterAlreadyActiveSub = _connection.On<Guid>("CharacterAlreadyActive", _ =>
            {
                ErrorMessage = $"{character.Name} is already logged in on another client. Disconnect that client first.";
                IsBusy = false;
            });

            _attrAllocatedSub = _connection.On<AttributePointsAllocatedPayload>("AttributePointsAllocated", payload =>
                _gameVm.OnAttributePointsAllocated(payload.RemainingPoints, payload.NewAttributes));

            _characterRestedSub = _connection.On<CharacterRestedPayload>("CharacterRested", payload =>
                _gameVm.OnCharacterRested(payload.CurrentHealth, payload.MaxHealth,
                    payload.CurrentMana, payload.MaxMana, payload.GoldRemaining));

            _abilityUsedSub = _connection.On<AbilityUsedPayload>("AbilityUsed", payload =>
                _gameVm.OnAbilityUsed(payload.AbilityId, payload.RemainingMana, payload.HealthRestored));

            _skillXpGainedSub = _connection.On<SkillXpGainedPayload>("SkillXpGained", payload =>
                _gameVm.OnSkillXpGained(payload.SkillId, payload.TotalXp, payload.CurrentRank, payload.RankedUp));

            _itemEquippedSub = _connection.On<ItemEquippedPayload>("ItemEquipped", payload =>
                _gameVm.OnItemEquipped(payload.Slot, payload.ItemRef));

            _goldChangedSub = _connection.On<GoldChangedPayload>("GoldChanged", payload =>
                _gameVm.OnGoldChanged(payload.GoldAdded, payload.NewGoldTotal));

            _damageTakenSub = _connection.On<DamageTakenPayload>("DamageTaken", payload =>
                _gameVm.OnDamageTaken(payload.DamageAmount, payload.CurrentHealth, payload.MaxHealth, payload.IsDead));

            _experienceGainedSub = _connection.On<ExperienceGainedPayload>("ExperienceGained", payload =>
                _gameVm.OnExperienceGained(payload.NewLevel, payload.NewExperience, payload.LeveledUp, payload.LeveledUpTo));

            await _connection.SendCommandAsync<object>("SelectCharacter", character.Id);
            await _gameVm.InitializeAsync(character.Name, zoneId);
            await _connection.SendCommandAsync<object>("EnterZone", zoneId);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to connect: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    // ── Payload shapes (matching server hub broadcasts) ────────────────────────
    internal record OccupantInfo(Guid CharacterId, string CharacterName, DateTimeOffset EnteredAt);
    internal record ZoneEnteredPayload(string Id, string Name, string Description, string ZoneType, IEnumerable<OccupantInfo> Occupants);
    internal record PlayerEventPayload(string CharacterName);
    internal record CharacterStatusPayload(Guid CharacterId, bool IsOnline);
    internal record AttributePointsAllocatedPayload(Guid CharacterId, int PointsSpent, int RemainingPoints, Dictionary<string, int> NewAttributes);
    internal record CharacterRestedPayload(Guid CharacterId, string LocationId, int CurrentHealth, int MaxHealth, int CurrentMana, int MaxMana, int GoldRemaining);
    internal record AbilityUsedPayload(Guid CharacterId, string AbilityId, int ManaCost, int RemainingMana, int HealthRestored);
    internal record SkillXpGainedPayload(Guid CharacterId, string SkillId, int TotalXp, int CurrentRank, bool RankedUp);
    internal record ItemEquippedPayload(Guid CharacterId, string Slot, string? ItemRef, Dictionary<string, string> AllEquippedItems);
    internal record GoldChangedPayload(Guid CharacterId, int GoldAdded, int NewGoldTotal, string? Source);
    internal record DamageTakenPayload(Guid CharacterId, int DamageAmount, int CurrentHealth, int MaxHealth, bool IsDead, string? Source);
    internal record ExperienceGainedPayload(Guid CharacterId, int NewLevel, long NewExperience, bool LeveledUp, int? LeveledUpTo, string? Source);

    private async Task DoCreateAsync()
    {
        IsBusy = true;
        ClearError();
        try
        {
            var (character, error) = await _characters.CreateCharacterAsync(new CreateCharacterRequest(NewCharacterName!, SelectedClass!));
            if (character is not null)
            {
                Characters.Add(new CharacterEntryViewModel(character));
                NewCharacterName = string.Empty;
                SelectedClass = string.Empty;
                IsCreating = false;
            }
            else
            {
                ErrorMessage = error?.Message ?? "Failed to create character.";
                ErrorDetails = error?.Details ?? string.Empty;
            }
        }
        finally { IsBusy = false; }
    }

    private async Task DoDeleteAsync(CharacterEntryViewModel entry)
    {
        var error = await _characters.DeleteCharacterAsync(entry.Character.Id);
        if (error is null)
            Characters.Remove(entry);
        else
        {
            ErrorMessage = error.Message;
            ErrorDetails = error.Details ?? string.Empty;
        }
    }
}

