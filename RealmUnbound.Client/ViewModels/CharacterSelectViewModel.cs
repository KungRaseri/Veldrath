using Avalonia.Media.Imaging;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using RealmUnbound.Assets;
using RealmUnbound.Assets.Manifest;
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
    private readonly IAssetStore? _assetStore;
    private readonly IAuthService _auth;
    private readonly TokenStore _tokens;

    private bool _isCreating;
    private string _newCharacterName = string.Empty;
    private string _selectedClass = string.Empty;
    private IReadOnlyList<string> _availableClasses = ["Warrior"];
    private Bitmap? _selectedClassIcon;

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
    private IDisposable? _characterSelectedSub;
    private IDisposable? _itemCraftedSub;
    private IDisposable? _dungeonEnteredSub;
    private IDisposable? _shopVisitedSub;
    private IDisposable? _zoneLeftSub;
    private IDisposable? _inventoryLoadedSub;
    private IDisposable? _locationEnteredSub;
    private IDisposable? _zoneLocationUnlockedSub;
    private IDisposable? _areaSearchedSub;
    private IDisposable? _connectionTraversedSub;
    private IDisposable? _tokenRefreshTimer;

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

    /// <summary>Class badge icon for the currently selected class in the create form, or <see langword="null"/> when no class is selected or assets are unavailable.</summary>
    public Bitmap? SelectedClassIcon
    {
        get => _selectedClassIcon;
        private set => this.RaiseAndSetIfChanged(ref _selectedClassIcon, value);
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
        TokenStore tokens,
        ContentCache contentCache,
        ClientSettings settings,
        IAssetStore? assetStore = null)
    {
        _characters = characters;
        _connection = connection;
        _navigation = navigation;
        _gameVm = gameVm;
        _contentCache = contentCache;
        _settings = settings;
        _assetStore = assetStore;
        _auth = auth;
        _tokens = tokens;

        if (assetStore is not null)
            this.WhenAnyValue(x => x.SelectedClass)
                .Subscribe(cls => _ = LoadSelectedClassIconAsync(cls));

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
            // If the access token is expired or expiring shortly, refresh before attempting any
            // server calls. This handles restarts where the stored token has aged past its expiry.
            if (_tokens.IsExpiringSoon)
            {
                var refreshed = await _auth.RefreshAsync();
                if (!refreshed)
                {
                    // Refresh token also invalid — redirect so the user can log in again.
                    _navigation.NavigateTo<MainMenuViewModel>();
                    return;
                }
            }

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
            PopulateCharacters(list);

            // Query which characters are currently active on the server and mark them online.
            try
            {
                var activeIds = await _connection.SendCommandAsync<IEnumerable<Guid>>("GetActiveCharacters");
                if (activeIds is not null)
                    foreach (var entry in Characters)
                        entry.IsOnline = activeIds.Contains(entry.Character.Id);
            }
            catch { /* best-effort: IsOnline stays false if query fails */ }
        }
        catch
        {
            // Hub unavailable during load is non-fatal — characters still load via HTTP
            var list = await _characters.GetCharactersAsync();
            PopulateCharacters(list);
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
            var zoneId = character.CurrentZoneId.Length > 0 ? character.CurrentZoneId : "fenwick-crossing";

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
            _characterSelectedSub?.Dispose();
            _itemCraftedSub?.Dispose();
            _dungeonEnteredSub?.Dispose();
            _shopVisitedSub?.Dispose();
            _zoneLeftSub?.Dispose();
            _inventoryLoadedSub?.Dispose();
            _locationEnteredSub?.Dispose();
            _zoneLocationUnlockedSub?.Dispose();
            _areaSearchedSub?.Dispose();
            _connectionTraversedSub?.Dispose();
            _tokenRefreshTimer?.Dispose();

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
                _gameVm.OnItemEquipped(payload.Slot, payload.ItemRef, payload.AllEquippedItems));

            _goldChangedSub = _connection.On<GoldChangedPayload>("GoldChanged", payload =>
                _gameVm.OnGoldChanged(payload.GoldAdded, payload.NewGoldTotal));

            _damageTakenSub = _connection.On<DamageTakenPayload>("DamageTaken", payload =>
                _gameVm.OnDamageTaken(payload.DamageAmount, payload.CurrentHealth, payload.MaxHealth, payload.IsDead));

            _experienceGainedSub = _connection.On<ExperienceGainedPayload>("ExperienceGained", payload =>
                _gameVm.OnExperienceGained(payload.NewLevel, payload.NewExperience, payload.LeveledUp, payload.LeveledUpTo));

            _characterSelectedSub = _connection.On<CharacterSelectedPayload>("CharacterSelected", payload =>
                _gameVm.SeedInitialStats(
                    payload.Level, payload.Experience,
                    payload.CurrentHealth, payload.MaxHealth,
                    payload.CurrentMana, payload.MaxMana,
                    payload.Gold, payload.UnspentAttributePoints,
                    payload.Id));

            _itemCraftedSub = _connection.On<ItemCraftedPayload>("ItemCrafted", payload =>
                _gameVm.OnItemCrafted(payload.RecipeSlug, payload.GoldSpent, payload.RemainingGold));

            _dungeonEnteredSub = _connection.On<DungeonEnteredPayload>("DungeonEntered", payload =>
                _gameVm.OnDungeonEntered(payload.DungeonId, payload.DungeonSlug));

            _shopVisitedSub = _connection.On<ShopVisitedPayload>("ShopVisited", payload =>
                _gameVm.OnShopVisited(payload.ZoneId, payload.ZoneName));

            _zoneLeftSub = _connection.On("ZoneLeft", () => _gameVm.OnZoneLeft());

            _inventoryLoadedSub = _connection.On<InventoryLoadedPayload>("InventoryLoaded", payload =>
                _gameVm.OnInventoryLoaded(payload.Items));
            _locationEnteredSub = _connection.On<LocationEnteredPayload>("LocationEntered", payload =>
                _gameVm.OnLocationEntered(payload.LocationSlug, payload.LocationDisplayName, payload.LocationType));
            _zoneLocationUnlockedSub = _connection.On<ZoneLocationUnlockedPayload>("ZoneLocationUnlocked", payload =>
                _gameVm.OnZoneLocationUnlocked(payload.LocationSlug, payload.LocationDisplayName, payload.LocationType, payload.UnlockSource));
            _areaSearchedSub = _connection.On<AreaSearchedPayload>("AreaSearched", payload =>
                _gameVm.OnAreaSearched(payload.RollValue, payload.AnyFound));
            _connectionTraversedSub = _connection.On<ConnectionTraversedPayload>("ConnectionTraversed", payload =>
                _gameVm.OnConnectionTraversed(payload.ToLocationSlug, payload.ToZoneId, payload.IsCrossZone));

            // Proactively refresh the access token every 5 minutes during gameplay so it
            // never silently expires mid-session and cause hub reconnects to fail with 401.
            _tokenRefreshTimer?.Dispose();
            _tokenRefreshTimer = Observable.Interval(TimeSpan.FromMinutes(5))
                .Subscribe(tick => { _ = DoProactiveTokenRefreshAsync(); });

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

    private async Task DoProactiveTokenRefreshAsync()
    {
        if (!_tokens.IsExpiringSoon) return;
        var refreshed = await _auth.RefreshAsync();
        if (!refreshed)
            _navigation.NavigateTo<MainMenuViewModel>();
    }

    // Payload shapes (matching server hub broadcasts)
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
    internal record CharacterSelectedPayload(Guid Id, string Name, string ClassName, int Level, long Experience, string CurrentZoneId, int CurrentHealth, int MaxHealth, int CurrentMana, int MaxMana, int Gold, int UnspentAttributePoints, DateTimeOffset SelectedAt);
    internal record ItemCraftedPayload(Guid CharacterId, string RecipeSlug, int GoldSpent, int RemainingGold);
    internal record DungeonEnteredPayload(Guid CharacterId, string DungeonId, string DungeonSlug);
    internal record ShopVisitedPayload(Guid CharacterId, string ZoneId, string ZoneName);
    internal record InventoryLoadedPayload(Guid CharacterId, IReadOnlyList<InventoryItemEntry> Items);
    internal record LocationEnteredPayload(Guid CharacterId, string LocationSlug, string LocationDisplayName, string LocationType);
    internal record ZoneLocationUnlockedPayload(Guid CharacterId, string LocationSlug, string LocationDisplayName, string LocationType, string UnlockSource);
    internal record AreaSearchedPayload(Guid CharacterId, int RollValue, bool AnyFound, IReadOnlyList<object> Discovered);
    internal record ConnectionTraversedPayload(Guid CharacterId, string FromLocation, string? ToLocationSlug, string? ToZoneId, bool IsCrossZone, string? ConnectionType);

    private void PopulateCharacters(IEnumerable<CharacterDto> characters)
    {
        var entries = characters.OrderBy(x => x.SlotIndex).Select(c => new CharacterEntryViewModel(c)).ToList();
        Characters.Clear();
        foreach (var entry in entries)
            Characters.Add(entry);
        if (_assetStore is not null)
            _ = LoadEntryIconsAsync(entries);
    }

    private async Task LoadEntryIconsAsync(IReadOnlyList<CharacterEntryViewModel> entries)
    {
        foreach (var entry in entries)
        {
            var path = ClassAssets.GetPath(entry.Character.ClassName);
            if (path is null) continue;
            var bytes = await _assetStore!.LoadImageAsync(path);
            if (bytes is not null)
                entry.ClassIcon = new Bitmap(new MemoryStream(bytes));
        }
    }

    private async Task LoadSelectedClassIconAsync(string className)
    {
        var path = ClassAssets.GetPath(className);
        if (path is null) { SelectedClassIcon = null; return; }
        var bytes = await _assetStore!.LoadImageAsync(path);
        SelectedClassIcon = bytes is null ? null : new Bitmap(new MemoryStream(bytes));
    }

    private async Task DoCreateAsync()
    {
        IsBusy = true;
        ClearError();
        try
        {
            var (character, error) = await _characters.CreateCharacterAsync(new CreateCharacterRequest(NewCharacterName!, SelectedClass!));
            if (character is not null)
            {
                var entry = new CharacterEntryViewModel(character);
                Characters.Add(entry);
                if (_assetStore is not null)
                    _ = LoadEntryIconsAsync([entry]);
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

