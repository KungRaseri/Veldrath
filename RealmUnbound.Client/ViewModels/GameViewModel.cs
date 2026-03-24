using Avalonia.Media.Imaging;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive;
using RealmUnbound.Assets;
using RealmUnbound.Assets.Manifest;
using RealmUnbound.Client.Services;

namespace RealmUnbound.Client.ViewModels;

/// <summary>In-game view model. Active after a character has entered a zone.</summary>
public class GameViewModel : ViewModelBase
{
    private readonly IServerConnectionService _connection;
    private readonly IZoneService _zoneService;
    private readonly TokenStore _tokens;
    private readonly INavigationService _navigation;
    private readonly IAssetStore? _assetStore;
    private readonly IAudioPlayer? _audioPlayer;

    // Zone state
    private string _zoneName = string.Empty;
    private string _zoneDescription = string.Empty;
    private string _characterName = string.Empty;
    private string _statusMessage = string.Empty;
    private string _currentZoneId = string.Empty;
    private string? _currentZoneLocationSlug;

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

    /// <summary>Slug of the zone location the character is currently at, or <see langword="null"/> if not at a specific location.</summary>
    public string? CurrentZoneLocationSlug
    {
        get => _currentZoneLocationSlug;
        private set => this.RaiseAndSetIfChanged(ref _currentZoneLocationSlug, value);
    }

    // Character stats
    private int _unspentAttributePoints;
    private int _currentHealth;
    private int _maxHealth;
    private int _currentMana;
    private int _maxMana;
    private int _gold;
    private int _level;
    private long _experience;

    /// <summary>Attribute points the character has earned but not yet spent.</summary>
    public int UnspentAttributePoints
    {
        get => _unspentAttributePoints;
        set
        {
            this.RaiseAndSetIfChanged(ref _unspentAttributePoints, value);
            this.RaisePropertyChanged(nameof(HasUnspentPoints));
        }
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

    /// <summary>Current level of the active character.</summary>
    public int Level
    {
        get => _level;
        set
        {
            this.RaiseAndSetIfChanged(ref _level, value);
            this.RaisePropertyChanged(nameof(ExperienceToNextLevel));
        }
    }

    /// <summary>Experience points accumulated toward the next level.</summary>
    public long Experience
    {
        get => _experience;
        set => this.RaiseAndSetIfChanged(ref _experience, value);
    }

    /// <summary>Experience required to reach the next level (<c>Level * 100</c>, matching the server formula).</summary>
    public long ExperienceToNextLevel => Math.Max(1L, Level * 100L);

    /// <summary>Whether the active character has unspent attribute points to allocate.</summary>
    public bool HasUnspentPoints => UnspentAttributePoints > 0;

    // Left panel state
    private bool _isLeftPanelOpen = true;

    /// <summary>Whether the collapsible left stats/log panel is expanded.</summary>
    public bool IsLeftPanelOpen
    {
        get => _isLeftPanelOpen;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isLeftPanelOpen, value);
            this.RaisePropertyChanged(nameof(LeftPanelToggleIcon));
        }
    }

    /// <summary>Icon text for the left panel toggle button: <c>◀</c> when open, <c>▶</c> when collapsed.</summary>
    public string LeftPanelToggleIcon => IsLeftPanelOpen ? "◀" : "▶";

    // Overlay panel state
    private bool _isInventoryOpen;
    private bool _isShopOpen;
    private string _shopZoneName = string.Empty;

    /// <summary>Whether the player's inventory panel is currently visible.</summary>
    public bool IsInventoryOpen
    {
        get => _isInventoryOpen;
        private set => this.RaiseAndSetIfChanged(ref _isInventoryOpen, value);
    }

    /// <summary>Whether the town shop panel is currently visible.</summary>
    public bool IsShopOpen
    {
        get => _isShopOpen;
        private set => this.RaiseAndSetIfChanged(ref _isShopOpen, value);
    }

    /// <summary>Display name of the zone whose shop is currently open.</summary>
    public string ShopZoneName
    {
        get => _shopZoneName;
        private set => this.RaiseAndSetIfChanged(ref _shopZoneName, value);
    }

    // Zone context flags
    private bool _hasInn;
    private bool _hasMerchant;
    private string _zoneType = string.Empty;
    private int _zoneMinLevel;
    private string _regionId = string.Empty;

    // Zone view mode (Zone | Region | World)
    private string _zoneViewMode = "Zone";

    // Region state
    private string _regionName = string.Empty;
    private string _regionDescription = string.Empty;
    private string _regionType = string.Empty;
    private int _regionMinLevel;
    private int _regionMaxLevel;

    // World state
    private string _worldName = string.Empty;
    private string _worldEra = string.Empty;

    /// <summary>Whether the current zone has an inn available for resting.</summary>
    public bool HasInn
    {
        get => _hasInn;
        private set => this.RaiseAndSetIfChanged(ref _hasInn, value);
    }

    /// <summary>Whether the current zone has a merchant for buying and selling.</summary>
    public bool HasMerchant
    {
        get => _hasMerchant;
        private set => this.RaiseAndSetIfChanged(ref _hasMerchant, value);
    }

    /// <summary>The type classification of the current zone (e.g. Town, Wilderness, Dungeon).</summary>
    public string ZoneType
    {
        get => _zoneType;
        private set => this.RaiseAndSetIfChanged(ref _zoneType, value);
    }

    /// <summary>Minimum recommended character level for the current zone.</summary>
    public int ZoneMinLevel
    {
        get => _zoneMinLevel;
        private set => this.RaiseAndSetIfChanged(ref _zoneMinLevel, value);
    }

    // Zone view mode
    /// <summary>Active centre-panel view: <c>Zone</c>, <c>Region</c>, or <c>World</c>.</summary>
    public string ZoneViewMode
    {
        get => _zoneViewMode;
        private set
        {
            this.RaiseAndSetIfChanged(ref _zoneViewMode, value);
            this.RaisePropertyChanged(nameof(IsZoneViewActive));
            this.RaisePropertyChanged(nameof(IsRegionViewActive));
            this.RaisePropertyChanged(nameof(IsWorldViewActive));
        }
    }

    /// <summary>Whether the zone detail panel is currently active.</summary>
    public bool IsZoneViewActive => ZoneViewMode == "Zone";

    /// <summary>Whether the region map panel is currently active.</summary>
    public bool IsRegionViewActive => ZoneViewMode == "Region";

    /// <summary>Whether the world overview panel is currently active.</summary>
    public bool IsWorldViewActive => ZoneViewMode == "World";

    // Region state
    /// <summary>Name of the region the current zone belongs to.</summary>
    public string RegionName
    {
        get => _regionName;
        private set => this.RaiseAndSetIfChanged(ref _regionName, value);
    }

    /// <summary>Description text for the current region.</summary>
    public string RegionDescription
    {
        get => _regionDescription;
        private set => this.RaiseAndSetIfChanged(ref _regionDescription, value);
    }

    /// <summary>Type classification of the current region (Forest, Highland, Coastal, Volcanic).</summary>
    public string RegionType
    {
        get => _regionType;
        private set => this.RaiseAndSetIfChanged(ref _regionType, value);
    }

    /// <summary>Minimum character level for zones within the current region.</summary>
    public int RegionMinLevel
    {
        get => _regionMinLevel;
        private set => this.RaiseAndSetIfChanged(ref _regionMinLevel, value);
    }

    /// <summary>Maximum character level for zones within the current region.</summary>
    public int RegionMaxLevel
    {
        get => _regionMaxLevel;
        private set => this.RaiseAndSetIfChanged(ref _regionMaxLevel, value);
    }

    // World state
    /// <summary>Name of the world the current zone belongs to.</summary>
    public string WorldName
    {
        get => _worldName;
        private set => this.RaiseAndSetIfChanged(ref _worldName, value);
    }

    /// <summary>Era label for the current world (e.g. "The Age of Embers").</summary>
    public string WorldEra
    {
        get => _worldEra;
        private set => this.RaiseAndSetIfChanged(ref _worldEra, value);
    }

    /// <summary>Players currently online in the same zone.</summary>
    public ObservableCollection<string> OnlinePlayers { get; } = [];

    /// <summary>Scrolling action log (last 100 entries).</summary>
    public ObservableCollection<string> ActionLog { get; } = [];

    /// <summary>The eight equipment slots for the active character.</summary>
    public IReadOnlyList<EquipmentSlotViewModel> EquipmentSlots { get; }

    /// <summary>Items currently loaded in the character's inventory panel.</summary>
    public ObservableCollection<InventoryItemViewModel> InventoryItems { get; } = [];

    /// <summary>All zones in the current region, ordered by minimum level. Used by the zone and region panels.</summary>
    public ObservableCollection<ZoneNodeViewModel> RegionZones { get; } = [];

    /// <summary>All regions in the current world, used by the world overview panel.</summary>
    public ObservableCollection<RegionCardViewModel> WorldRegions { get; } = [];

    /// <summary>Toggles the collapsible left stats/log panel open or closed.</summary>
    public ReactiveCommand<Unit, Unit> ToggleLeftPanelCommand { get; }

    /// <summary>Toggles the inventory panel: opens it and fetches items from the server when closed; closes it when open.</summary>
    public ReactiveCommand<Unit, Unit> ToggleInventoryCommand { get; }

    /// <summary>Closes the town shop panel.</summary>
    public ReactiveCommand<Unit, Unit> CloseShopCommand { get; }

    /// <summary>Logs out the character, leaves the zone, and returns to the main menu.</summary>
    public ReactiveCommand<Unit, Unit> LogoutCommand { get; }

    /// <summary>Dev helper: gains 100 XP. Used during development for testing progression.</summary>
    public ReactiveCommand<Unit, Unit> DevGainXpCommand { get; }

    /// <summary>Dev helper: adds 50 gold. Used during development for testing economy.</summary>
    public ReactiveCommand<Unit, Unit> DevAddGoldCommand { get; }

    /// <summary>Dev helper: deals 10 damage. Used during development for testing combat.</summary>
    public ReactiveCommand<Unit, Unit> DevTakeDamageCommand { get; }

    /// <summary>Rest at the current zone's inn, restoring HP and MP at a cost of 10 gold.</summary>
    public ReactiveCommand<Unit, Unit> RestAtLocationCommand { get; }

    /// <summary>Spend unallocated attribute points by sending an allocation map to the server.</summary>
    public ReactiveCommand<Dictionary<string, int>, Unit> AllocateAttributePointsCommand { get; }

    /// <summary>Activate an ability by ID, consuming mana and optionally restoring health.</summary>
    public ReactiveCommand<string, Unit> UseAbilityCommand { get; }

    /// <summary>Award XP to a skill by ID and amount. Tuple: (skillId, amount).</summary>
    public ReactiveCommand<(string SkillId, int Amount), Unit> AwardSkillXpCommand { get; }

    /// <summary>Equip or unequip an item in a named slot. Tuple: (slot, itemRef) — pass <see langword="null"/> itemRef to unequip.</summary>
    public ReactiveCommand<(string Slot, string? ItemRef), Unit> EquipItemCommand { get; }

    /// <summary>Add or remove gold from the active character. Tuple: (amount, source) — pass a negative amount to spend.</summary>
    public ReactiveCommand<(int Amount, string? Source), Unit> AddGoldCommand { get; }

    /// <summary>Apply damage to the active character, reducing current health.</summary>
    public ReactiveCommand<(int DamageAmount, string? Source), Unit> TakeDamageCommand { get; }

    /// <summary>Award experience to the active character. Tuple: (amount, source).</summary>
    public ReactiveCommand<(int Amount, string? Source), Unit> GainExperienceCommand { get; }

    /// <summary>Craft an item by recipe slug, deducting gold from the active character.</summary>
    public ReactiveCommand<string, Unit> CraftItemCommand { get; }

    /// <summary>Enter a dungeon by slug, looking up the dungeon via the zone catalog.</summary>
    public ReactiveCommand<string, Unit> EnterDungeonCommand { get; }

    /// <summary>Open the merchant shop available in zones with a merchant.</summary>
    public ReactiveCommand<Unit, Unit> VisitShopCommand { get; }

    /// <summary>Navigate to a specific location within the current zone by slug.</summary>
    public ReactiveCommand<string, Unit> NavigateToLocationCommand { get; }

    /// <summary>Actively search the current zone area for hidden locations.</summary>
    public ReactiveCommand<Unit, Unit> SearchAreaCommand { get; }

    /// <summary>Traverse a connection from the current location. Parameter is the connection type (e.g. "path").</summary>
    public ReactiveCommand<(string FromSlug, string ConnectionType), Unit> TraverseConnectionCommand { get; }

    /// <summary>Switches the centre panel to the zone detail view.</summary>
    public ReactiveCommand<Unit, Unit> ShowZoneViewCommand { get; }

    /// <summary>Switches the centre panel to the region map view, restoring the character's current region.</summary>
    public ReactiveCommand<Unit, Unit> ShowRegionViewCommand { get; }

    /// <summary>Switches the centre panel to the world overview.</summary>
    public ReactiveCommand<Unit, Unit> ShowWorldViewCommand { get; }

    /// <summary>Travel to any zone by slug, sending <c>EnterZone</c> to the server and reinitializing zone state.</summary>
    public ReactiveCommand<string, Unit> TravelToZoneCommand { get; }

    /// <summary>Load a specific region's zone details in the Region panel and switch to that view.</summary>
    public ReactiveCommand<string, Unit> ViewRegionCommand { get; }

    /// <summary>Initializes a new instance of <see cref="GameViewModel"/>.</summary>
    public GameViewModel(
        IServerConnectionService connection,
        IZoneService zoneService,
        TokenStore tokens,
        INavigationService navigation,
        IAssetStore? assetStore = null,
        IAudioPlayer? audioPlayer = null)
    {
        _connection = connection;
        _zoneService = zoneService;
        _tokens = tokens;
        _navigation = navigation;
        _assetStore = assetStore;
        _audioPlayer = audioPlayer;

        CharacterName = tokens.Username ?? "Adventurer";

        EquipmentSlots =
        [
            new("MainHand", "Main Hand"),
            new("OffHand",  "Off Hand"),
            new("Head",     "Head"),
            new("Chest",    "Chest"),
            new("Legs",     "Legs"),
            new("Feet",     "Feet"),
            new("Ring",     "Ring"),
            new("Amulet",   "Amulet"),
        ];
        if (assetStore is not null)
            _ = LoadEquipmentIconsAsync(assetStore);

        ToggleLeftPanelCommand = ReactiveCommand.Create(() => { IsLeftPanelOpen = !IsLeftPanelOpen; });
        ToggleInventoryCommand = ReactiveCommand.CreateFromTask(DoToggleInventoryAsync);
        CloseShopCommand = ReactiveCommand.Create(() => { IsShopOpen = false; });
        LogoutCommand = ReactiveCommand.CreateFromTask(DoLogoutAsync);
        DevGainXpCommand = ReactiveCommand.CreateFromTask(() => DoGainExperienceAsync(100, "dev"));
        DevAddGoldCommand = ReactiveCommand.CreateFromTask(() => DoAddGoldAsync(50, "dev"));
        DevTakeDamageCommand = ReactiveCommand.CreateFromTask(() => DoTakeDamageAsync(10, "dev"));
        RestAtLocationCommand = ReactiveCommand.CreateFromTask(DoRestAtLocationAsync);
        AllocateAttributePointsCommand = ReactiveCommand.CreateFromTask<Dictionary<string, int>>(DoAllocateAttributePointsAsync);
        UseAbilityCommand = ReactiveCommand.CreateFromTask<string>(DoUseAbilityAsync);
        AwardSkillXpCommand = ReactiveCommand.CreateFromTask<(string, int)>(t => DoAwardSkillXpAsync(t.Item1, t.Item2));
        EquipItemCommand = ReactiveCommand.CreateFromTask<(string, string?)>(t => DoEquipItemAsync(t.Item1, t.Item2));
        AddGoldCommand = ReactiveCommand.CreateFromTask<(int, string?)>(t => DoAddGoldAsync(t.Item1, t.Item2));
        TakeDamageCommand = ReactiveCommand.CreateFromTask<(int, string?)>(t => DoTakeDamageAsync(t.Item1, t.Item2));
        GainExperienceCommand = ReactiveCommand.CreateFromTask<(int, string?)>(t => DoGainExperienceAsync(t.Item1, t.Item2));
        CraftItemCommand = ReactiveCommand.CreateFromTask<string>(DoCraftItemAsync);
        EnterDungeonCommand = ReactiveCommand.CreateFromTask<string>(DoEnterDungeonAsync);
        VisitShopCommand = ReactiveCommand.CreateFromTask(DoVisitShopAsync);
        NavigateToLocationCommand = ReactiveCommand.CreateFromTask<string>(DoNavigateToLocationAsync);
        SearchAreaCommand = ReactiveCommand.CreateFromTask(DoSearchAreaAsync);
        TraverseConnectionCommand = ReactiveCommand.CreateFromTask<(string, string)>(t => DoTraverseConnectionAsync(t.Item1, t.Item2));

        ShowZoneViewCommand = ReactiveCommand.Create(() => { ZoneViewMode = "Zone"; });
        ShowRegionViewCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            if (string.IsNullOrEmpty(_regionId))
                ZoneViewMode = "Region";
            else
                await DoShowRegionDetailsAsync(_regionId);
        });
        ShowWorldViewCommand = ReactiveCommand.Create(() => { ZoneViewMode = "World"; });
        TravelToZoneCommand = ReactiveCommand.CreateFromTask<string>(DoTravelToZoneAsync);
        ViewRegionCommand = ReactiveCommand.CreateFromTask<string>(DoShowRegionDetailsAsync);
    }

    /// <summary>Called by <see cref="CharacterSelectViewModel"/> after SelectCharacter + EnterZone succeeds.</summary>
    public async Task InitializeAsync(string characterName, string zoneId)
    {
        CharacterName = characterName;
        _currentZoneId = zoneId;
        ZoneViewMode = "Zone";

        var zone = await _zoneService.GetZoneAsync(zoneId);
        if (zone is not null)
        {
            ZoneName = zone.Name;
            ZoneDescription = zone.Description;
            ZoneType = zone.Type;
            HasInn = zone.HasInn;
            HasMerchant = zone.HasMerchant;
            ZoneMinLevel = zone.MinLevel;
            await LoadWorldContextAsync(zone.RegionId, zoneId);
        }

        if (_assetStore is not null && _audioPlayer is not null)
        {
            var musicPath = ZoneType.Equals("Dungeon", StringComparison.OrdinalIgnoreCase)
                ? _assetStore.ResolveAudioPath(AudioAssets.MusicDungeon)
                : _assetStore.ResolveAudioPath(AudioAssets.MusicExplore);
            if (musicPath is not null)
                await _audioPlayer.PlayMusicAsync(musicPath);
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
        MaxHealth = maxHealth;
        CurrentMana = currentMana;
        MaxMana = maxMana;
        Gold = goldRemaining;
        AppendLog($"Rested. HP: {currentHealth}/{maxHealth}  MP: {currentMana}/{maxMana}  Gold: {goldRemaining}");
    }

    /// <summary>Called from hub when the active character has used an ability.</summary>
    public void OnAbilityUsed(string abilityId, int remainingMana, int healthRestored)
    {
        CurrentMana = remainingMana;
        if (healthRestored > 0)
            CurrentHealth = Math.Min(CurrentHealth + healthRestored, MaxHealth);
        AppendLog(healthRestored > 0
            ? $"Used {abilityId}. MP: {remainingMana}  +{healthRestored} HP"
            : $"Used {abilityId}. MP: {remainingMana}");
    }

    /// <summary>Called from hub when the active character gains XP in a skill.</summary>
    public void OnSkillXpGained(string skillId, int totalXp, int currentRank, bool rankedUp)
    {
        AppendLog(rankedUp
            ? $"Skill {skillId} ranked up to {currentRank}! (Total XP: {totalXp})"
            : $"Skill {skillId}: +XP \u2192 {totalXp} XP (Rank {currentRank})");
    }

    /// <summary>Called from hub when an item is equipped or unequipped in a slot.</summary>
    public void OnItemEquipped(string slot, string? itemRef, IReadOnlyDictionary<string, string>? allEquippedItems = null)
    {
        if (allEquippedItems is not null)
        {
            foreach (var equipSlot in EquipmentSlots)
            {
                allEquippedItems.TryGetValue(equipSlot.SlotName, out var currentRef);
                equipSlot.ItemRef  = currentRef;
                if (currentRef is null)
                    equipSlot.ItemIcon = null;
            }
            if (_assetStore is not null)
                _ = RefreshAllEquippedIconsAsync(_assetStore);
        }
        AppendLog(itemRef is not null
            ? $"Equipped '{itemRef}' in {slot} slot."
            : $"Unequipped {slot} slot.");
    }

    /// <summary>Called from hub when the active character's gold total has changed.</summary>
    public void OnGoldChanged(int goldAdded, int newGoldTotal)
    {
        Gold = newGoldTotal;
        AppendLog(goldAdded >= 0
            ? $"Gained {goldAdded} gold. Total: {newGoldTotal}"
            : $"Spent {-goldAdded} gold. Total: {newGoldTotal}");
    }

    /// <summary>Called from hub when the active character has taken damage.</summary>
    public void OnDamageTaken(int damageAmount, int currentHealth, int maxHealth, bool isDead)
    {
        CurrentHealth = currentHealth;
        if (_assetStore is not null && _audioPlayer is not null)
        {
            var sfxPath = _assetStore.ResolveAudioPath(AudioAssets.ImpactMetalHeavy1);
            if (sfxPath is not null)
                _audioPlayer.PlaySfx(sfxPath);
        }
        AppendLog(isDead
            ? $"Took {damageAmount} damage and died. HP: 0/{maxHealth}"
            : $"Took {damageAmount} damage. HP: {currentHealth}/{maxHealth}");
    }

    /// <summary>Called from hub when the active character gains experience and possibly levels up.</summary>
    public void OnExperienceGained(int newLevel, long newExperience, bool leveledUp, int? leveledUpTo)
    {
        Level = newLevel;
        Experience = newExperience;
        AppendLog(leveledUp
            ? $"Leveled up to {leveledUpTo}! XP toward next level: {newExperience}"
            : $"Gained experience. Level: {newLevel}  XP: {newExperience}");
    }

    /// <summary>Called from hub when an item has been successfully crafted by the character.</summary>
    public void OnItemCrafted(string recipeSlug, int goldSpent, int remainingGold)
    {
        Gold = remainingGold;
        AppendLog($"Crafted '{recipeSlug}'. Gold spent: {goldSpent}. Remaining: {remainingGold}");
    }

    /// <summary>Called from hub when the character has entered a dungeon.</summary>
    public void OnDungeonEntered(string dungeonId, string dungeonSlug)
    {
        AppendLog($"Entered dungeon '{dungeonSlug}' (ID: {dungeonId}).");
        // Treat dungeon entry as a zone transition: refresh zone state and send EnterZone.
        _ = InitializeAsync(CharacterName, dungeonId);
        _ = _connection.SendCommandAsync<object>("EnterZone", dungeonId);
    }

    private async Task LoadWorldContextAsync(string? regionId, string currentZoneId)
    {
        if (regionId is null) return;
        _regionId = regionId;

        var region = await _zoneService.GetRegionAsync(regionId);
        if (region is null) return;

        RegionName = region.Name;
        RegionDescription = region.Description;
        RegionType = region.Type;
        RegionMinLevel = region.MinLevel;
        RegionMaxLevel = region.MaxLevel;

        var zones = await _zoneService.GetZonesByRegionAsync(regionId);
        RegionZones.Clear();
        foreach (var z in zones)
        {
            var zId = z.Id;
            RegionZones.Add(new ZoneNodeViewModel(
                z.Id, z.Name, z.Type, z.MinLevel,
                z.HasInn, z.HasMerchant,
                isCurrentZone: z.Id == currentZoneId,
                onTravel: z.Id != currentZoneId ? () => DoTravelToZoneAsync(zId) : null));
        }

        var world = await _zoneService.GetWorldAsync(region.WorldId);
        if (world is not null)
        {
            WorldName = world.Name;
            WorldEra = world.Era;
        }

        var allRegions = await _zoneService.GetRegionsAsync();
        WorldRegions.Clear();
        foreach (var r in allRegions)
        {
            var rId = r.Id;
            WorldRegions.Add(new RegionCardViewModel(
                r.Id, r.Name, r.Type,
                r.MinLevel, r.MaxLevel,
                isCurrentRegion: r.Id == regionId,
                onExplore: () => DoShowRegionDetailsAsync(rId)));
        }
    }

    private async Task DoTravelToZoneAsync(string zoneId)
    {
        if (zoneId == _currentZoneId) return;
        try
        {
            await _connection.SendCommandAsync<object>("EnterZone", zoneId);
            await InitializeAsync(CharacterName, zoneId);
        }
        catch (Exception ex)
        {
            AppendLog($"Travel failed: {ex.Message}");
        }
    }

    private async Task DoShowRegionDetailsAsync(string regionId)
    {
        if (string.IsNullOrEmpty(regionId)) return;

        var region = await _zoneService.GetRegionAsync(regionId);
        if (region is null) return;

        RegionName = region.Name;
        RegionDescription = region.Description;
        RegionType = region.Type;
        RegionMinLevel = region.MinLevel;
        RegionMaxLevel = region.MaxLevel;

        var zones = await _zoneService.GetZonesByRegionAsync(regionId);
        RegionZones.Clear();
        foreach (var z in zones)
        {
            var zId = z.Id;
            RegionZones.Add(new ZoneNodeViewModel(
                z.Id, z.Name, z.Type, z.MinLevel,
                z.HasInn, z.HasMerchant,
                isCurrentZone: z.Id == _currentZoneId,
                onTravel: z.Id != _currentZoneId ? () => DoTravelToZoneAsync(zId) : null));
        }

        ZoneViewMode = "Region";
    }

    /// <summary>Seeds all character stat properties from the <c>CharacterSelected</c> hub event so the HUD shows correct values immediately on login.</summary>
    /// <param name="level">Character level.</param>
    /// <param name="experience">Experience toward the next level.</param>
    /// <param name="currentHealth">Current hit points.</param>
    /// <param name="maxHealth">Maximum hit points.</param>
    /// <param name="currentMana">Current mana points.</param>
    /// <param name="maxMana">Maximum mana points.</param>
    /// <param name="gold">Gold held.</param>
    /// <param name="unspentAttributePoints">Attribute points not yet allocated.</param>
    public void SeedInitialStats(
        int level, long experience,
        int currentHealth, int maxHealth,
        int currentMana, int maxMana,
        int gold, int unspentAttributePoints)
    {
        Level = level;
        Experience = experience;
        CurrentHealth = currentHealth;
        MaxHealth = maxHealth;
        CurrentMana = currentMana;
        MaxMana = maxMana;
        Gold = gold;
        UnspentAttributePoints = unspentAttributePoints;
    }

    private async Task DoLogoutAsync()
    {
        // Leave zone, disconnect hub, then go back to main menu
        try { await _connection.SendCommandAsync("LeaveZone"); } catch { /* ignore */ }
        _audioPlayer?.StopMusic();
        await _connection.DisconnectAsync();
        _navigation.NavigateTo<MainMenuViewModel>();
    }

    private async Task DoRestAtLocationAsync()
    {
        try
        {
            await _connection.SendCommandAsync<object>("RestAtLocation", new { LocationId = _currentZoneId, CostInGold = 10 });
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

    private async Task DoUseAbilityAsync(string abilityId)
    {
        try
        {
            await _connection.SendCommandAsync<object>("UseAbility", abilityId);
        }
        catch (Exception ex)
        {
            AppendLog($"Ability use failed: {ex.Message}");
        }
    }

    private async Task DoAwardSkillXpAsync(string skillId, int amount)
    {
        try
        {
            await _connection.SendCommandAsync<object>("AwardSkillXp", new { SkillId = skillId, Amount = amount });
        }
        catch (Exception ex)
        {
            AppendLog($"Skill XP award failed: {ex.Message}");
        }
    }

    private async Task DoEquipItemAsync(string slot, string? itemRef)
    {
        try
        {
            await _connection.SendCommandAsync<object>("EquipItem", new { Slot = slot, ItemRef = itemRef });
        }
        catch (Exception ex)
        {
            AppendLog($"Equip failed: {ex.Message}");
        }
    }

    private async Task DoAddGoldAsync(int amount, string? source)
    {
        try
        {
            await _connection.SendCommandAsync<object>("AddGold", new { Amount = amount, Source = source });
        }
        catch (Exception ex)
        {
            AppendLog($"Gold transaction failed: {ex.Message}");
        }
    }

    private async Task DoTakeDamageAsync(int damageAmount, string? source)
    {
        try
        {
            await _connection.SendCommandAsync<object>("TakeDamage", new { DamageAmount = damageAmount, Source = source });
        }
        catch (Exception ex)
        {
            AppendLog($"Damage application failed: {ex.Message}");
        }
    }

    private async Task DoGainExperienceAsync(int amount, string? source)
    {
        try
        {
            await _connection.SendCommandAsync<object>("GainExperience", new { Amount = amount, Source = source });
        }
        catch (Exception ex)
        {
            AppendLog($"Experience award failed: {ex.Message}");
        }
    }

    private async Task DoCraftItemAsync(string recipeSlug)
    {
        try
        {
            await _connection.SendCommandAsync<object>("CraftItem", recipeSlug);
        }
        catch (Exception ex)
        {
            AppendLog($"Crafting failed: {ex.Message}");
        }
    }

    private async Task DoEnterDungeonAsync(string dungeonSlug)
    {
        try
        {
            await _connection.SendCommandAsync<object>("EnterDungeon", dungeonSlug);
        }
        catch (Exception ex)
        {
            AppendLog($"Enter dungeon failed: {ex.Message}");
        }
    }

    private async Task DoVisitShopAsync()
    {
        try
        {
            await _connection.SendCommandAsync<object>("VisitShop", new { ZoneId = _currentZoneId });
        }
        catch (Exception ex)
        {
            AppendLog($"Shop visit failed: {ex.Message}");
        }
    }

    private async Task DoNavigateToLocationAsync(string locationSlug)
    {
        try
        {
            await _connection.SendCommandAsync<object>("NavigateToLocation", new { LocationSlug = locationSlug });
        }
        catch (Exception ex)
        {
            AppendLog($"Navigation failed: {ex.Message}");
        }
    }

    private async Task DoSearchAreaAsync()
    {
        try
        {
            await _connection.SendCommandAsync("SearchArea");
        }
        catch (Exception ex)
        {
            AppendLog($"Area search failed: {ex.Message}");
        }
    }

    private async Task DoTraverseConnectionAsync(string fromLocationSlug, string connectionType)
    {
        try
        {
            await _connection.SendCommandAsync<object>("TraverseConnection",
                new { FromLocationSlug = fromLocationSlug, ConnectionType = connectionType });
        }
        catch (Exception ex)
        {
            AppendLog($"Traversal failed: {ex.Message}");
        }
    }

    /// <summary>Called from hub when the server confirms the character has entered a zone location.</summary>
    /// <param name="locationSlug">The slug of the location entered.</param>
    /// <param name="locationDisplayName">The display name of the location.</param>
    /// <param name="locationType">The type of location (e.g. "dungeon", "location", "environment").</param>
    public void OnLocationEntered(string locationSlug, string locationDisplayName, string locationType)
    {
        CurrentZoneLocationSlug = locationSlug;
        AppendLog($"Arrived at {locationDisplayName} ({locationType}).");
    }

    /// <summary>Called from hub when a hidden zone location has been newly unlocked for this character.</summary>
    /// <param name="locationSlug">The slug of the unlocked location.</param>
    /// <param name="locationDisplayName">The display name of the unlocked location.</param>
    /// <param name="locationType">The location type.</param>
    /// <param name="unlockSource">How the location was unlocked (e.g. "skill_check_passive", "quest").</param>
    public void OnZoneLocationUnlocked(string locationSlug, string locationDisplayName, string locationType, string unlockSource)
    {
        var sourceLabel = unlockSource switch
        {
            "skill_check_passive" => "You notice something nearby",
            "skill_check_active"  => "Your search reveals",
            "quest"               => "Quest reward",
            "item"                => "An item reveals",
            _                     => "Discovered",
        };
        AppendLog($"{sourceLabel}: {locationDisplayName} ({locationType}).");
    }

    /// <summary>Called from hub when an active area search completes.</summary>
    /// <param name="rollValue">The search roll result.</param>
    /// <param name="anyFound">Whether at least one hidden location was discovered.</param>
    public void OnAreaSearched(int rollValue, bool anyFound)
    {
        AppendLog(anyFound
            ? $"[{rollValue:+0;-0}] You find something hidden nearby!"
            : $"[{rollValue:+0;-0}] Your search turns up nothing new.");
    }

    /// <summary>Called from hub when the server confirms a connection traversal has completed.</summary>
    /// <param name="toLocationSlug">The slug of the destination location, or <see langword="null"/> for zone-entry connections.</param>
    /// <param name="toZoneId">The destination zone ID when this was a cross-zone traversal, otherwise <see langword="null"/>.</param>
    /// <param name="isCrossZone">Whether traversal moved the character into a different zone.</param>
    public void OnConnectionTraversed(string? toLocationSlug, string? toZoneId, bool isCrossZone)
    {
        if (isCrossZone && toZoneId is not null)
            AppendLog($"You travel to {toZoneId}.");
        else if (toLocationSlug is not null)
            AppendLog($"You move to {toLocationSlug}.");
    }

    /// <summary>Handles the ShopVisited hub event: opens the shop panel for the given zone.</summary>
    /// <param name="zoneId">The zone ID of the visited shop.</param>
    /// <param name="zoneName">The display name of the zone.</param>
    public void OnShopVisited(string zoneId, string zoneName)
    {
        ShopZoneName = zoneName;
        IsShopOpen   = true;
        AppendLog($"Welcome to the shop at {zoneName}!");
    }

    /// <summary>Called from the hub when the server confirms the active character has left their current zone.</summary>
    public void OnZoneLeft()
        => AppendLog("You have left the zone.");

    /// <summary>Called from the hub when the server responds with the character's current inventory items.</summary>
    /// <param name="items">Inventory entries loaded from the server.</param>
    public void OnInventoryLoaded(IReadOnlyList<InventoryItemEntry> items)
    {
        InventoryItems.Clear();
        foreach (var item in items)
            InventoryItems.Add(new InventoryItemViewModel(item.ItemRef, item.Quantity, item.Durability));
        IsInventoryOpen = true;
    }

    private async Task DoToggleInventoryAsync()
    {
        if (IsInventoryOpen)
        {
            IsInventoryOpen = false;
            return;
        }

        try
        {
            await _connection.SendCommandAsync("GetInventory");
        }
        catch (Exception ex)
        {
            AppendLog($"Inventory load failed: {ex.Message}");
        }
    }

    private void AppendLog(string message)
    {
        ActionLog.Add($"[{DateTime.Now:HH:mm}] {message}");
        while (ActionLog.Count > 100)
            ActionLog.RemoveAt(0);
    }

    private async Task RefreshAllEquippedIconsAsync(IAssetStore assetStore)
    {
        foreach (var equipSlot in EquipmentSlots)
        {
            if (equipSlot.ItemRef is null)
                continue;
            var bytes = await assetStore.LoadImageAsync(equipSlot.ItemRef);
            equipSlot.ItemIcon = bytes is null ? null : new Bitmap(new MemoryStream(bytes));
        }
    }

    private async Task LoadEquipmentIconsAsync(IAssetStore assetStore)
    {
        var weaponBytes = await assetStore.LoadImageAsync(ItemAssets.Weapon01);
        var shieldBytes = await assetStore.LoadImageAsync(ItemAssets.Shield01);
        var armorBytes = await assetStore.LoadImageAsync(ItemAssets.Armor01);
        var ringBytes = await assetStore.LoadImageAsync(ItemAssets.Accessory18);
        var necklaceBytes = await assetStore.LoadImageAsync(ItemAssets.Accessory01);
        Bitmap? weaponIcon = weaponBytes is null ? null : new Bitmap(new MemoryStream(weaponBytes));
        Bitmap? shieldIcon = shieldBytes is null ? null : new Bitmap(new MemoryStream(shieldBytes));
        Bitmap? armorIcon = armorBytes is null ? null : new Bitmap(new MemoryStream(armorBytes));
        Bitmap? ringIcon = ringBytes is null ? null : new Bitmap(new MemoryStream(ringBytes));
        Bitmap? necklaceIcon = necklaceBytes is null ? null : new Bitmap(new MemoryStream(necklaceBytes));
        foreach (var slot in EquipmentSlots)
        {
            Bitmap? icon;
            if (slot.SlotName is "MainHand")
                icon = weaponIcon;
            else if (slot.SlotName is "OffHand")
                icon = shieldIcon;
            else if (slot.SlotName is "Ring")
                icon = ringIcon;
            else if (slot.SlotName is "Amulet")
                icon = necklaceIcon;
            else
                icon = armorIcon;
            slot.Icon = icon;
        }
    }
}

/// <summary>
/// A single item-slot entry received in the <c>InventoryLoaded</c> hub payload.
/// Mirrors <c>InventoryItemDto</c> on the server side.
/// </summary>
/// <param name="ItemRef">Item-reference slug (e.g. <c>"iron_sword"</c>).</param>
/// <param name="Quantity">Stack size.</param>
/// <param name="Durability">Current durability (0–100), or <see langword="null"/> for stackable items.</param>
public record InventoryItemEntry(string ItemRef, int Quantity, int? Durability);
