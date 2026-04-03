using Avalonia.Media.Imaging;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive;
using RealmUnbound.Assets;
using RealmUnbound.Assets.Manifest;
using RealmUnbound.Client.Services;

namespace RealmUnbound.Client.ViewModels;

/// <summary>Bundles all initial character stat values for <see cref="GameViewModel.SeedInitialStats"/>.</summary>
/// <param name="Level">Character level.</param>
/// <param name="Experience">Experience toward the next level.</param>
/// <param name="CurrentHealth">Current hit points.</param>
/// <param name="MaxHealth">Maximum hit points.</param>
/// <param name="CurrentMana">Current mana points.</param>
/// <param name="MaxMana">Maximum mana points.</param>
/// <param name="Gold">Gold held.</param>
/// <param name="UnspentAttributePoints">Attribute points not yet allocated.</param>
/// <param name="Strength">Strength attribute value.</param>
/// <param name="Dexterity">Dexterity attribute value.</param>
/// <param name="Constitution">Constitution attribute value.</param>
/// <param name="Intelligence">Intelligence attribute value.</param>
/// <param name="Wisdom">Wisdom attribute value.</param>
/// <param name="Charisma">Charisma attribute value.</param>
/// <param name="LearnedAbilities">Ability slugs the character has learned.</param>
/// <param name="CharacterId">The character's unique identifier.</param>
/// <param name="ClassName">The character class name.</param>
public record SeedInitialStatsArgs(
    int Level, long Experience,
    int CurrentHealth, int MaxHealth,
    int CurrentMana, int MaxMana,
    int Gold, int UnspentAttributePoints,
    int Strength = 10, int Dexterity = 10, int Constitution = 10,
    int Intelligence = 10, int Wisdom = 10, int Charisma = 10,
    IReadOnlyList<string>? LearnedAbilities = null,
    Guid? CharacterId = null,
    string ClassName = "");

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
    private string _className = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isStatusMessageDismissable = true;
    private string _currentZoneId = string.Empty;
    private string? _currentZoneLocationSlug;
    private Guid? _characterId;

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

    /// <summary>The character's class name (e.g. Warrior, Mage).</summary>
    public string ClassName
    {
        get => _className;
        private set => this.RaiseAndSetIfChanged(ref _className, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    /// <summary>Whether the current status message can be dismissed by the player.</summary>
    public bool IsStatusMessageDismissable
    {
        get => _isStatusMessageDismissable;
        private set => this.RaiseAndSetIfChanged(ref _isStatusMessageDismissable, value);
    }

    /// <summary>Slug of the zone location the character is currently at, or <see langword="null"/> if not at a specific location.</summary>
    public string? CurrentZoneLocationSlug
    {
        get => _currentZoneLocationSlug;
        private set
        {
            this.RaiseAndSetIfChanged(ref _currentZoneLocationSlug, value);
            this.RaisePropertyChanged(nameof(CurrentZoneLocationDisplayName));
        }
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
    private int _strength;
    private int _dexterity;
    private int _constitution;
    private int _intelligence;
    private int _wisdom;
    private int _charisma;
    private AttributeAllocationViewModel? _attributeAllocation;
    private bool _isAttributeAllocationOpen;

    // Combat state
    private bool _isInCombat;
    private bool _hasSpawnedEnemies;
    private bool _isPlayerDead;
    private bool _isHardcoreDeath;
    private Guid? _combatEnemyId;
    private string _combatEnemyName = string.Empty;
    private int _combatEnemyLevel;
    private int _combatEnemyCurrentHealth;
    private int _combatEnemyMaxHealth;

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

    /// <summary>Strength attribute of the active character.</summary>
    public int Strength
    {
        get => _strength;
        set => this.RaiseAndSetIfChanged(ref _strength, value);
    }

    /// <summary>Dexterity attribute of the active character.</summary>
    public int Dexterity
    {
        get => _dexterity;
        set => this.RaiseAndSetIfChanged(ref _dexterity, value);
    }

    /// <summary>Constitution attribute of the active character.</summary>
    public int Constitution
    {
        get => _constitution;
        set => this.RaiseAndSetIfChanged(ref _constitution, value);
    }

    /// <summary>Intelligence attribute of the active character.</summary>
    public int Intelligence
    {
        get => _intelligence;
        set => this.RaiseAndSetIfChanged(ref _intelligence, value);
    }

    /// <summary>Wisdom attribute of the active character.</summary>
    public int Wisdom
    {
        get => _wisdom;
        set => this.RaiseAndSetIfChanged(ref _wisdom, value);
    }

    /// <summary>Charisma attribute of the active character.</summary>
    public int Charisma
    {
        get => _charisma;
        set => this.RaiseAndSetIfChanged(ref _charisma, value);
    }

    /// <summary>Whether the attribute allocation overlay is currently open.</summary>
    public bool IsAttributeAllocationOpen
    {
        get => _isAttributeAllocationOpen;
        private set => this.RaiseAndSetIfChanged(ref _isAttributeAllocationOpen, value);
    }

    /// <summary>The active attribute allocation draft, created fresh each time the overlay is opened.</summary>
    public AttributeAllocationViewModel? AttributeAllocation
    {
        get => _attributeAllocation;
        private set => this.RaiseAndSetIfChanged(ref _attributeAllocation, value);
    }

    // Combat properties

    /// <summary>Whether the active character is currently engaged in combat.</summary>
    public bool IsInCombat
    {
        get => _isInCombat;
        private set => this.RaiseAndSetIfChanged(ref _isInCombat, value);
    }

    /// <summary>Whether the active character has been defeated this session.</summary>
    public bool IsPlayerDead
    {
        get => _isPlayerDead;
        private set => this.RaiseAndSetIfChanged(ref _isPlayerDead, value);
    }

    /// <summary>Whether the active character has been permanently deleted by a hardcore-mode death.</summary>
    public bool IsHardcoreDeath
    {
        get => _isHardcoreDeath;
        private set => this.RaiseAndSetIfChanged(ref _isHardcoreDeath, value);
    }

    /// <summary>Instance ID of the enemy the player is currently fighting, or <see langword="null"/> when not in combat.</summary>
    public Guid? CombatEnemyId
    {
        get => _combatEnemyId;
        private set => this.RaiseAndSetIfChanged(ref _combatEnemyId, value);
    }

    /// <summary>Display name of the enemy currently being fought.</summary>
    public string CombatEnemyName
    {
        get => _combatEnemyName;
        private set => this.RaiseAndSetIfChanged(ref _combatEnemyName, value);
    }

    /// <summary>Level of the enemy currently being fought.</summary>
    public int CombatEnemyLevel
    {
        get => _combatEnemyLevel;
        private set => this.RaiseAndSetIfChanged(ref _combatEnemyLevel, value);
    }

    /// <summary>Current HP of the enemy being fought.</summary>
    public int CombatEnemyCurrentHealth
    {
        get => _combatEnemyCurrentHealth;
        private set => this.RaiseAndSetIfChanged(ref _combatEnemyCurrentHealth, value);
    }

    /// <summary>Maximum HP of the enemy being fought.</summary>
    public int CombatEnemyMaxHealth
    {
        get => _combatEnemyMaxHealth;
        private set => this.RaiseAndSetIfChanged(ref _combatEnemyMaxHealth, value);
    }

    /// <summary>Display names of abilities the current combat enemy can use.</summary>
    public ObservableCollection<string> EnemyAbilityNames { get; } = [];

    // Left panel state
    private bool _isLeftPanelOpen = true;
    private bool _isRightPanelOpen = true;

    // Connection state
    private ConnectionState _connectionState = ConnectionState.Disconnected;

    // Settings flyout state
    private bool _isSettingsOpen;

    // Chat state
    private string _chatInput = string.Empty;
    private string _activeChatChannel = "System";
    private string _whisperTarget = string.Empty;

    /// <summary>Whether the collapsible left character-sheet panel is expanded.</summary>
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

    /// <summary>Whether the collapsible right action-log panel is expanded.</summary>
    public bool IsRightPanelOpen
    {
        get => _isRightPanelOpen;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isRightPanelOpen, value);
            this.RaisePropertyChanged(nameof(RightPanelToggleIcon));
        }
    }

    /// <summary>Icon text for the right panel toggle button: <c>▶</c> when open, <c>◀</c> when collapsed.</summary>
    public string RightPanelToggleIcon => IsRightPanelOpen ? "▶" : "◀";

    // Connection status display

    /// <summary>Current health state of the server hub connection.</summary>
    public ConnectionState ConnectionStateValue
    {
        get => _connectionState;
        private set
        {
            this.RaiseAndSetIfChanged(ref _connectionState, value);
            this.RaisePropertyChanged(nameof(ConnectionStatusColor));
            this.RaisePropertyChanged(nameof(ConnectionStatusTooltip));
        }
    }

    /// <summary>Hex colour string representing the current connection state for the indicator dot.</summary>
    public string ConnectionStatusColor => _connectionState switch
    {
        ConnectionState.Connected    => "#22c55e",
        ConnectionState.Degraded     => "#eab308",
        ConnectionState.Reconnecting => "#f97316",
        ConnectionState.Disconnected => "#ef4444",
        ConnectionState.Failed       => "#ef4444",
        _                            => "#6b7280",
    };

    /// <summary>Tooltip text for the connection status indicator dot.</summary>
    public string ConnectionStatusTooltip => _connectionState switch
    {
        ConnectionState.Connected    => "Connected",
        ConnectionState.Degraded     => "Degraded (high latency)",
        ConnectionState.Reconnecting => "Reconnecting\u2026",
        ConnectionState.Disconnected => "Disconnected",
        ConnectionState.Failed       => "Connection failed",
        _                            => "Unknown",
    };

    // Settings flyout

    /// <summary>Whether the ⚙ settings flyout is currently open.</summary>
    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        private set => this.RaiseAndSetIfChanged(ref _isSettingsOpen, value);
    }

    /// <summary>Whether background music is currently muted.</summary>
    public bool IsMusicMuted => _audioPlayer?.IsMusicMuted ?? false;

    /// <summary>Label for the music mute toggle button.</summary>
    public string MusicMuteLabel => IsMusicMuted ? "🔇 Unmute Music" : "🎵 Mute Music";

    /// <summary>Whether sound effects are currently muted.</summary>
    public bool IsSfxMuted => _audioPlayer?.IsSfxMuted ?? false;

    /// <summary>Label for the SFX mute toggle button.</summary>
    public string SfxMuteLabel => IsSfxMuted ? "🔇 Unmute SFX" : "🔊 Mute SFX";

    // Chat

    /// <summary>Scrolling chat log (last 200 messages across all channels).</summary>
    public ObservableCollection<ChatMessageViewModel> ChatMessages { get; } = [];

    /// <summary>Text the player has typed into the chat input box.</summary>
    public string ChatInput
    {
        get => _chatInput;
        set
        {
            this.RaiseAndSetIfChanged(ref _chatInput, value);
            // /w prefix interception: auto-switch to Whisper channel
            if (value.StartsWith("/w ", StringComparison.OrdinalIgnoreCase))
            {
                var rest = value[3..];
                var spaceIdx = rest.IndexOf(' ');
                if (spaceIdx > 0)
                {
                    WhisperTarget = rest[..spaceIdx];
                    ActiveChatChannel = "Whisper";
                    // Replace input with just the message body (suppress re-entrancy via field)
                    _chatInput = rest[(spaceIdx + 1)..];
                    this.RaisePropertyChanged(nameof(ChatInput));
                }
            }
        }
    }

    /// <summary>Currently selected chat channel: <c>Zone</c>, <c>Global</c>, <c>Whisper</c>, or <c>System</c>.</summary>
    public string ActiveChatChannel
    {
        get => _activeChatChannel;
        set
        {
            this.RaiseAndSetIfChanged(ref _activeChatChannel, value);
            this.RaisePropertyChanged(nameof(IsWhisperChannelActive));
            this.RaisePropertyChanged(nameof(IsChatInputVisible));
            this.RaisePropertyChanged(nameof(ChatChannelIndex));
        }
    }

    /// <summary>Zero-based tab index for the chat channel selector (0=System, 1=Global, 2=Zone, 3=Whisper). Two-way bound to <see cref="ActiveChatChannel"/>.</summary>
    public int ChatChannelIndex
    {
        get => _activeChatChannel switch { "Global" => 1, "Zone" => 2, "Whisper" => 3, _ => 0 };
        set
        {
            var ch = value switch { 1 => "Global", 2 => "Zone", 3 => "Whisper", _ => "System" };
            ActiveChatChannel = ch;
            this.RaisePropertyChanged();
        }
    }

    /// <summary>The whisper target character name, set when the Whisper channel is active.</summary>
    public string WhisperTarget
    {
        get => _whisperTarget;
        set => this.RaiseAndSetIfChanged(ref _whisperTarget, value);
    }

    /// <summary>Whether the Whisper target input is currently visible (i.e. Whisper channel is active).</summary>
    public bool IsWhisperChannelActive => ActiveChatChannel == "Whisper";

    /// <summary>Whether the chat input row is visible (hidden when System channel is active, which is read-only).</summary>
    public bool IsChatInputVisible => ActiveChatChannel != "System";

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
    public ObservableCollection<OnlinePlayerViewModel> OnlinePlayers { get; } = [];

    /// <summary>Scrolling action log (last 100 entries).</summary>
    public ObservableCollection<string> ActionLog { get; } = [];

    /// <summary>The eight equipment slots for the active character.</summary>
    public IReadOnlyList<EquipmentSlotViewModel> EquipmentSlots { get; }

    /// <summary>Items currently loaded in the character's inventory panel.</summary>
    public ObservableCollection<InventoryItemViewModel> InventoryItems { get; } = [];

    /// <summary>Items available for purchase in the current zone's merchant shop.</summary>
    public ObservableCollection<ShopItemViewModel> ShopItems { get; } = [];

    /// <summary>All zones in the current region, ordered by minimum level. Used by the zone and region panels.</summary>
    public ObservableCollection<ZoneNodeViewModel> RegionZones { get; } = [];

    /// <summary>All regions in the current world, used by the world overview panel.</summary>
    public ObservableCollection<RegionCardViewModel> WorldRegions { get; } = [];

    /// <summary>Zone locations within the current zone, shown in the Zone panel.</summary>
    public ObservableCollection<ZoneLocationItemViewModel> ZoneLocations { get; } = [];

    /// <summary>Outgoing connections available from the character's current zone location.</summary>
    public ObservableCollection<ZoneConnectionLinkViewModel> CurrentLocationConnections { get; } = [];

    /// <summary>Live enemy roster at the character's current zone location.</summary>
    public ObservableCollection<SpawnedEnemyItemViewModel> SpawnedEnemies { get; } = [];

    /// <summary>Ability slugs the active character has learned, used to render combat ability buttons.</summary>
    public ObservableCollection<string> LearnedAbilities { get; } = [];

    /// <summary>Six hotbar slots populated from <see cref="LearnedAbilities"/>; empty slots show as greyed-out placeholders.</summary>
    public ObservableCollection<HotbarSlotViewModel> HotbarSlots { get; } = [];

    /// <summary>Gets whether there is at least one enemy in the roster at the current location.</summary>
    public bool HasSpawnedEnemies
    {
        get => _hasSpawnedEnemies;
        private set => this.RaiseAndSetIfChanged(ref _hasSpawnedEnemies, value);
    }

    /// <summary>Display name of the zone location the character is currently standing at, or <see langword="null"/> if not at a specific location.</summary>
    public string? CurrentZoneLocationDisplayName =>
        ZoneLocations.FirstOrDefault(l => l.Slug == CurrentZoneLocationSlug)?.DisplayName;

    /// <summary>Toggles the collapsible left character-sheet panel open or closed.</summary>
    public ReactiveCommand<Unit, Unit> ToggleLeftPanelCommand { get; }

    /// <summary>Toggles the collapsible right action-log panel open or closed.</summary>
    public ReactiveCommand<Unit, Unit> ToggleRightPanelCommand { get; }

    /// <summary>Dismisses the current status message banner. Only available when <see cref="IsStatusMessageDismissable"/> is <see langword="true"/>.</summary>
    public ReactiveCommand<Unit, Unit> DismissStatusMessageCommand { get; }

    /// <summary>Toggles the inventory panel: opens it and fetches items from the server when closed; closes it when open.</summary>
    public ReactiveCommand<Unit, Unit> ToggleInventoryCommand { get; }

    /// <summary>Closes the town shop panel.</summary>
    public ReactiveCommand<Unit, Unit> CloseShopCommand { get; }

    /// <summary>Logs out the character, leaves the zone, and returns to the main menu.</summary>
    public ReactiveCommand<Unit, Unit> LogoutCommand { get; }

    /// <summary>Toggles the ⚙ settings flyout open or closed.</summary>
    public ReactiveCommand<Unit, Unit> ToggleSettingsCommand { get; }

    /// <summary>Toggles background music mute.</summary>
    public ReactiveCommand<Unit, Unit> ToggleMusicMuteCommand { get; }

    /// <summary>Toggles sound effects mute.</summary>
    public ReactiveCommand<Unit, Unit> ToggleSfxMuteCommand { get; }

    /// <summary>Switch the active chat channel. Parameter is the channel name string.</summary>
    public ReactiveCommand<string, Unit> SetChatChannelCommand { get; }

    /// <summary>Send the current <see cref="ChatInput"/> on the <see cref="ActiveChatChannel"/>.</summary>
    public ReactiveCommand<Unit, Unit> SendChatCommand { get; }

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

    /// <summary>Opens the attribute allocation overlay, allowing the player to distribute unspent points.</summary>
    public ReactiveCommand<Unit, Unit> OpenAttributeAllocationCommand { get; }

    /// <summary>Closes the attribute allocation overlay without applying changes.</summary>
    public ReactiveCommand<Unit, Unit> CloseAttributeAllocationCommand { get; }

    /// <summary>Activate an ability by ID, consuming mana and optionally restoring health.</summary>
    public ReactiveCommand<string, Unit> UseAbilityCommand { get; }

    /// <summary>Award XP to a skill by ID and amount. Tuple: (skillId, amount).</summary>
    public ReactiveCommand<(string SkillId, int Amount), Unit> AwardSkillXpCommand { get; }

    /// <summary>Equip or unequip an item in a named slot. Tuple: (slot, itemRef) — pass <see langword="null"/> itemRef to unequip.</summary>
    public ReactiveCommand<(string Slot, string? ItemRef), Unit> EquipItemCommand { get; }

    /// <summary>Drop one unit of an item from the active character's inventory (permanently removes it).</summary>
    public ReactiveCommand<string, Unit> DropItemCommand { get; }

    /// <summary>Buy one unit of an item from the current zone merchant.</summary>
    public ReactiveCommand<string, Unit> BuyItemCommand { get; }

    /// <summary>Sell one unit of an item to the current zone merchant.</summary>
    public ReactiveCommand<string, Unit> SellItemCommand { get; }

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

    /// <summary>Open the traversal-graph map screen.</summary>
    public ReactiveCommand<Unit, Unit> OpenMapCommand { get; }

    // Combat commands

    /// <summary>Engage a specific enemy by its instance ID at the current location.</summary>
    public ReactiveCommand<Guid, Unit> EngageEnemyCommand { get; }

    /// <summary>Perform a basic melee attack against the engaged enemy.</summary>
    public ReactiveCommand<Unit, Unit> AttackEnemyCommand { get; }

    /// <summary>Take a defensive stance this combat turn, reducing incoming damage.</summary>
    public ReactiveCommand<Unit, Unit> DefendActionCommand { get; }

    /// <summary>Attempt to flee from active combat (50% success chance).</summary>
    public ReactiveCommand<Unit, Unit> FleeFromCombatCommand { get; }

    /// <summary>Use a named ability in combat. Parameter is the ability ID.</summary>
    public ReactiveCommand<string, Unit> UseAbilityInCombatCommand { get; }

    /// <summary>
    /// Fires the hotbar ability slot's assigned ability, routing to the correct hub method
    /// depending on whether the character is currently in combat.
    /// </summary>
    public ReactiveCommand<string, Unit> UseHotbarAbilityCommand { get; }

    /// <summary>Respawn the character after defeat in normal mode.</summary>
    public ReactiveCommand<Unit, Unit> RespawnCommand { get; }

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

        ToggleLeftPanelCommand  = ReactiveCommand.Create(() => { IsLeftPanelOpen  = !IsLeftPanelOpen; });
        ToggleRightPanelCommand = ReactiveCommand.Create(() => { IsRightPanelOpen = !IsRightPanelOpen; });
        var canDismiss = this.WhenAnyValue(x => x.IsStatusMessageDismissable);
        DismissStatusMessageCommand = ReactiveCommand.Create(() => { StatusMessage = string.Empty; }, canDismiss);
        ToggleInventoryCommand = ReactiveCommand.CreateFromTask(DoToggleInventoryAsync);
        CloseShopCommand = ReactiveCommand.Create(() => { IsShopOpen = false; });
        LogoutCommand = ReactiveCommand.CreateFromTask(DoLogoutAsync);
        DevGainXpCommand = ReactiveCommand.CreateFromTask(() => DoGainExperienceAsync(100, "dev"));
        DevAddGoldCommand = ReactiveCommand.CreateFromTask(() => DoAddGoldAsync(50, "dev"));
        DevTakeDamageCommand = ReactiveCommand.CreateFromTask(() => DoTakeDamageAsync(10, "dev"));
        RestAtLocationCommand = ReactiveCommand.CreateFromTask(DoRestAtLocationAsync);
        AllocateAttributePointsCommand = ReactiveCommand.CreateFromTask<Dictionary<string, int>>(DoAllocateAttributePointsAsync);
        OpenAttributeAllocationCommand = ReactiveCommand.Create(() =>
        {
            AttributeAllocation = new AttributeAllocationViewModel(this);
            IsAttributeAllocationOpen = true;
        });
        CloseAttributeAllocationCommand = ReactiveCommand.Create(() => { IsAttributeAllocationOpen = false; });
        UseAbilityCommand = ReactiveCommand.CreateFromTask<string>(DoUseAbilityAsync);
        AwardSkillXpCommand = ReactiveCommand.CreateFromTask<(string, int)>(t => DoAwardSkillXpAsync(t.Item1, t.Item2));
        EquipItemCommand = ReactiveCommand.CreateFromTask<(string, string?)>(t => DoEquipItemAsync(t.Item1, t.Item2));
        DropItemCommand = ReactiveCommand.CreateFromTask<string>(DoDropItemAsync);
        BuyItemCommand = ReactiveCommand.CreateFromTask<string>(DoBuyItemAsync);
        SellItemCommand = ReactiveCommand.CreateFromTask<string>(DoSellItemAsync);
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
        OpenMapCommand = ReactiveCommand.Create(DoOpenMap);

        EngageEnemyCommand        = ReactiveCommand.CreateFromTask<Guid>(DoEngageEnemyAsync);
        AttackEnemyCommand        = ReactiveCommand.CreateFromTask(DoAttackEnemyAsync);
        DefendActionCommand       = ReactiveCommand.CreateFromTask(DoDefendActionAsync);
        FleeFromCombatCommand     = ReactiveCommand.CreateFromTask(DoFleeFromCombatAsync);
        UseAbilityInCombatCommand = ReactiveCommand.CreateFromTask<string>(DoUseAbilityInCombatAsync);
        UseHotbarAbilityCommand   = ReactiveCommand.CreateFromTask<string>(DoUseHotbarAbilityAsync);
        RespawnCommand            = ReactiveCommand.CreateFromTask(DoRespawnAsync);

        // Initialize 6 empty hotbar slots
        for (var i = 1; i <= 6; i++)
            HotbarSlots.Add(new HotbarSlotViewModel(i, UseHotbarAbilityCommand));
        LearnedAbilities.CollectionChanged += (_, _) => SyncHotbarSlots();

        SpawnedEnemies.CollectionChanged += (_, _) => HasSpawnedEnemies = SpawnedEnemies.Count > 0;

        // Settings + audio mute commands
        ToggleSettingsCommand  = ReactiveCommand.Create(() => { IsSettingsOpen = !IsSettingsOpen; });
        ToggleMusicMuteCommand = ReactiveCommand.Create(() =>
        {
            _audioPlayer?.ToggleMusicMute();
            this.RaisePropertyChanged(nameof(IsMusicMuted));
            this.RaisePropertyChanged(nameof(MusicMuteLabel));
        });
        ToggleSfxMuteCommand   = ReactiveCommand.Create(() =>
        {
            _audioPlayer?.ToggleSfxMute();
            this.RaisePropertyChanged(nameof(IsSfxMuted));
            this.RaisePropertyChanged(nameof(SfxMuteLabel));
        });

        // Chat commands
        SetChatChannelCommand = ReactiveCommand.Create<string>(ch => { ActiveChatChannel = ch; });
        var canSend = this.WhenAnyValue(x => x.ChatInput, x => x.ActiveChatChannel,
            (input, ch) => !string.IsNullOrWhiteSpace(input) && ch != "System");
        SendChatCommand = ReactiveCommand.CreateFromTask(DoSendChatAsync, canSend);

        // Subscribe to connection state changes
        _connection.StateChanged += state => { ConnectionStateValue = state; };
        ConnectionStateValue = _connection.State;

        // Auto-redirect to main menu if the hub drops (e.g. failed token refresh mid-session).
        _connection.ConnectionLost += OnConnectionLost;
    }

    /// <summary>Called by <see cref="CharacterSelectViewModel"/> after SelectCharacter + EnterZone succeeds.</summary>
    public async Task InitializeAsync(string characterName, string zoneId)
    {
        CharacterName = characterName;
        ZoneViewMode = "Zone";

        await LoadZoneCoreAsync(zoneId);

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
        if (!OnlinePlayers.Any(p => p.Name == playerName))
            OnlinePlayers.Add(new OnlinePlayerViewModel(playerName, StartWhisperFromPlayer));
        AppendLog($"{playerName} entered the zone.");
    }

    /// <summary>Called from hub when another player leaves the zone.</summary>
    public void OnPlayerLeft(string playerName)
    {
        var existing = OnlinePlayers.FirstOrDefault(p => p.Name == playerName);
        if (existing is not null)
            OnlinePlayers.Remove(existing);
        AppendLog($"{playerName} left the zone.");
    }

    /// <summary>Called from hub when zone state is received with initial occupant list.</summary>
    public void SetOccupants(IEnumerable<string> playerNames)
    {
        OnlinePlayers.Clear();
        foreach (var name in playerNames)
            if (name != CharacterName)
                OnlinePlayers.Add(new OnlinePlayerViewModel(name, StartWhisperFromPlayer));
    }

    /// <summary>Called from hub when a chat message is received.</summary>
    /// <param name="channel">The chat channel the message was sent on.</param>
    /// <param name="sender">The name of the player who sent the message.</param>
    /// <param name="message">The message text.</param>
    /// <param name="timestamp">The UTC timestamp of the message.</param>
    public void OnChatMessageReceived(string channel, string sender, string message, DateTimeOffset timestamp)
    {
        var isOwn = sender == CharacterName || sender.StartsWith("To ", StringComparison.Ordinal);
        ChatMessages.Add(new ChatMessageViewModel(channel, sender, message, timestamp, isOwn));
    }

    private void StartWhisperFromPlayer(string name)
    {
        ActiveChatChannel = "Whisper";
        WhisperTarget = name;
    }

    /// <summary>Called from hub when the active character's attribute points have been allocated.</summary>
    public void OnAttributePointsAllocated(int remainingPoints, Dictionary<string, int> newAttributes)
    {
        UnspentAttributePoints = remainingPoints;
        if (newAttributes.TryGetValue("Strength",     out var str)) Strength     = str;
        if (newAttributes.TryGetValue("Dexterity",    out var dex)) Dexterity    = dex;
        if (newAttributes.TryGetValue("Constitution", out var con)) Constitution = con;
        if (newAttributes.TryGetValue("Intelligence", out var intel)) Intelligence = intel;
        if (newAttributes.TryGetValue("Wisdom",       out var wis)) Wisdom       = wis;
        if (newAttributes.TryGetValue("Charisma",     out var cha)) Charisma     = cha;
        IsAttributeAllocationOpen = false;
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

    private async Task LoadZoneCoreAsync(string zoneId)
    {
        _currentZoneId = zoneId;
        CurrentZoneLocationSlug = null;
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
        await LoadZoneLocationsAsync(zoneId);
    }

    private async Task LoadZoneLocationsAsync(string zoneId)
    {
        var locations = await _zoneService.GetZoneLocationsAsync(zoneId, _characterId);
        ZoneLocations.Clear();
        foreach (var loc in locations)
        {
            var slug = loc.Slug;
            ZoneLocations.Add(new ZoneLocationItemViewModel(
                loc.Slug, loc.DisplayName, loc.LocationType, loc.MinLevel,
                isCurrent: loc.Slug == CurrentZoneLocationSlug,
                onNavigate: () => DoNavigateToLocationAsync(slug)));
        }
        this.RaisePropertyChanged(nameof(CurrentZoneLocationDisplayName));
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
    /// <param name="args">All initial stat values bundled together.</param>
    public void SeedInitialStats(SeedInitialStatsArgs args)
    {
        Level = args.Level;
        Experience = args.Experience;
        CurrentHealth = args.CurrentHealth;
        MaxHealth = args.MaxHealth;
        CurrentMana = args.CurrentMana;
        MaxMana = args.MaxMana;
        Gold = args.Gold;
        UnspentAttributePoints = args.UnspentAttributePoints;
        Strength = args.Strength;
        Dexterity = args.Dexterity;
        Constitution = args.Constitution;
        Intelligence = args.Intelligence;
        Wisdom = args.Wisdom;
        Charisma = args.Charisma;
        if (args.CharacterId.HasValue)
            _characterId = args.CharacterId;
        ClassName = args.ClassName;

        LearnedAbilities.Clear();
        if (args.LearnedAbilities is not null)
            foreach (var slug in args.LearnedAbilities)
                LearnedAbilities.Add(slug);
        SyncHotbarSlots();
    }

    private async Task DoLogoutAsync()
    {
        // Leave zone, disconnect hub, then go back to main menu
        try { await _connection.SendCommandAsync("LeaveZone"); } catch { /* ignore */ }
        _audioPlayer?.StopMusic();
        await _connection.DisconnectAsync();
        _navigation.NavigateTo<MainMenuViewModel>();
    }

    private void OnConnectionLost()
    {
        // Hub dropped mid-session (e.g. token refresh failed). Stop audio and redirect.
        _audioPlayer?.StopMusic();
        _navigation.NavigateTo<MainMenuViewModel>();
    }

    private void DoOpenMap()
    {
        var mapVm = new MapViewModel(_zoneService, _currentZoneId, _regionId, _currentZoneLocationSlug, _characterId);
        mapVm.CloseCommand.Subscribe(_ => _navigation.NavigateTo(this));
        _navigation.NavigateTo(mapVm);
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

    private async Task DoDropItemAsync(string itemRef)
    {
        try
        {
            await _connection.SendCommandAsync<object>("DropItem", new { ItemRef = itemRef });
        }
        catch (Exception ex)
        {
            AppendLog($"Drop failed: {ex.Message}");
        }
    }

    private async Task DoBuyItemAsync(string itemRef)
    {
        try
        {
            await _connection.SendCommandAsync<object>("BuyItem", new { ItemRef = itemRef });
        }
        catch (Exception ex)
        {
            AppendLog($"Purchase failed: {ex.Message}");
        }
    }

    private async Task DoSellItemAsync(string itemRef)
    {
        try
        {
            await _connection.SendCommandAsync<object>("SellItem", new { ItemRef = itemRef });
        }
        catch (Exception ex)
        {
            AppendLog($"Sale failed: {ex.Message}");
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

    /// <summary>Called from hub when combat begins with an enemy.</summary>
    /// <param name="enemyId">The instance ID of the engaged enemy.</param>
    /// <param name="enemyName">The enemy's display name.</param>
    /// <param name="enemyLevel">The enemy's combat level.</param>
    /// <param name="enemyCurrentHealth">The enemy's current HP at the start of combat.</param>
    /// <param name="enemyMaxHealth">The enemy's maximum HP.</param>
    /// <param name="abilityNames">Display names of abilities the enemy can use.</param>
    public void OnCombatStarted(Guid enemyId, string enemyName, int enemyLevel,
        int enemyCurrentHealth, int enemyMaxHealth, IReadOnlyList<string> abilityNames)
    {
        IsInCombat             = true;
        IsPlayerDead           = false;
        CombatEnemyId          = enemyId;
        CombatEnemyName        = enemyName;
        CombatEnemyLevel       = enemyLevel;
        CombatEnemyCurrentHealth = enemyCurrentHealth;
        CombatEnemyMaxHealth   = enemyMaxHealth;
        EnemyAbilityNames.Clear();
        foreach (var n in abilityNames) EnemyAbilityNames.Add(n);
        AppendLog($"Combat started! You face {enemyName} (Lv {enemyLevel}).");
    }

    /// <summary>Called from hub after each combat turn resolves.</summary>
    /// <param name="action">Action keyword: "attack", "defend", "flee_failed", or "ability".</param>
    /// <param name="playerDamage">Damage dealt by the player this turn.</param>
    /// <param name="healthRestored">HP healed by an ability this turn.</param>
    /// <param name="enemyRemainingHealth">Enemy HP after the player's action.</param>
    /// <param name="enemyDefeated">Whether the enemy was killed this turn.</param>
    /// <param name="enemyDamage">Damage dealt by the enemy counter-attack.</param>
    /// <param name="enemyAbilityUsed">Enemy ability name used, or <see langword="null"/> for basic attack.</param>
    /// <param name="playerRemainingHealth">Player HP after the enemy counter-attack.</param>
    /// <param name="playerDefeated">Whether the player was killed by the counter-attack.</param>
    /// <param name="playerHardcoreDeath">Whether the player was permanently deleted.</param>
    /// <param name="xpEarned">XP rewarded on enemy defeat.</param>
    /// <param name="goldEarned">Gold rewarded on enemy defeat.</param>
    public void OnCombatTurn(string action, int playerDamage, int healthRestored,
        int enemyRemainingHealth, bool enemyDefeated,
        int enemyDamage, string? enemyAbilityUsed,
        int playerRemainingHealth, bool playerDefeated, bool playerHardcoreDeath,
        int xpEarned, int goldEarned)
    {
        CombatEnemyCurrentHealth = enemyRemainingHealth;
        CurrentHealth = playerRemainingHealth;

        if (healthRestored > 0)
            AppendLog($"[{action}] You restore {healthRestored} HP.");
        if (playerDamage > 0)
            AppendLog($"[{action}] You deal {playerDamage} damage. Enemy HP: {enemyRemainingHealth}/{CombatEnemyMaxHealth}");

        if (enemyDefeated)
        {
            CombatEnemyCurrentHealth = 0;
            IsInCombat = false;
            var enemyItem = SpawnedEnemies.FirstOrDefault(e => e.Id == CombatEnemyId);
            if (enemyItem is not null) enemyItem.CurrentHealth = 0;
            AppendLog($"Enemy defeated! +{xpEarned} XP, +{goldEarned} Gold.");
            Experience += xpEarned;
            Gold += goldEarned;
            return;
        }

        if (enemyAbilityUsed is not null)
            AppendLog($"Enemy uses {enemyAbilityUsed} — deals {enemyDamage} damage.");
        else if (enemyDamage > 0)
            AppendLog($"Enemy attacks for {enemyDamage} damage. Your HP: {playerRemainingHealth}/{MaxHealth}");

        if (playerDefeated)
        {
            IsPlayerDead = true;
            IsHardcoreDeath = playerHardcoreDeath;
            IsInCombat = false;
            AppendLog(playerHardcoreDeath
                ? "You have died. Your hardcore character is gone."
                : "You have been defeated! You can respawn.");
        }
    }

    /// <summary>Called from hub when combat ends by fleeing.</summary>
    /// <param name="reason">Reason for combat ending (e.g. "fled").</param>
    public void OnCombatEnded(string reason)
    {
        IsInCombat = false;
        AppendLog($"Combat ended: {reason}.");
    }

    /// <summary>Called from hub when another player in the zone defeats an enemy.</summary>
    /// <param name="charId">The character who killed the enemy.</param>
    public void OnEnemyDefeated(Guid charId)
    {
        AppendLog("Another player defeated an enemy nearby.");
    }

    /// <summary>Called from hub when another player engages an enemy at this location.</summary>
    /// <param name="charId">The character who engaged.</param>
    /// <param name="enemyId">The instance ID of the engaged enemy.</param>
    /// <param name="enemyName">The enemy's display name.</param>
    public void OnEnemyEngaged(Guid charId, Guid enemyId, string enemyName)
    {
        AppendLog($"An ally engages {enemyName}!");
    }

    /// <summary>Called from hub when an enemy respawns at the current location.</summary>
    /// <param name="enemyId">The new instance ID.</param>
    /// <param name="name">The enemy's display name.</param>
    /// <param name="level">The enemy's level.</param>
    /// <param name="currentHealth">Starting HP.</param>
    /// <param name="maxHealth">Maximum HP.</param>
    public void OnEnemySpawned(Guid enemyId, string name, int level, int currentHealth, int maxHealth)
    {
        SpawnedEnemies.Add(new SpawnedEnemyItemViewModel
        {
            Id            = enemyId,
            Name          = name,
            Level         = level,
            CurrentHealth = currentHealth,
            MaxHealth     = maxHealth,
        });
        AppendLog($"A {name} (Lv {level}) has appeared!");
    }

    /// <summary>Called from hub when the character respawns after death in normal mode.</summary>
    /// <param name="currentHealth">HP after respawn.</param>
    /// <param name="currentMana">Mana after respawn.</param>
    public void OnCharacterRespawned(int currentHealth, int currentMana)
    {
        CurrentHealth   = currentHealth;
        CurrentMana     = currentMana;
        IsPlayerDead    = false;
        IsHardcoreDeath = false;
        AppendLog($"You have respawned. HP: {currentHealth}/{MaxHealth}");
    }

    private async Task DoEngageEnemyAsync(Guid enemyId)
    {
        try
        {
            await _connection.SendCommandAsync<object>("EngageEnemy",
                new { LocationSlug = CurrentZoneLocationSlug ?? string.Empty, EnemyId = enemyId });
        }
        catch (Exception ex)
        {
            AppendLog($"Engage failed: {ex.Message}");
        }
    }

    private async Task DoAttackEnemyAsync()
    {
        try
        {
            await _connection.SendCommandAsync("AttackEnemy");
        }
        catch (Exception ex)
        {
            AppendLog($"Attack failed: {ex.Message}");
        }
    }

    private async Task DoDefendActionAsync()
    {
        try
        {
            await _connection.SendCommandAsync("DefendAction");
        }
        catch (Exception ex)
        {
            AppendLog($"Defend failed: {ex.Message}");
        }
    }

    private async Task DoFleeFromCombatAsync()
    {
        try
        {
            await _connection.SendCommandAsync("FleeFromCombat");
        }
        catch (Exception ex)
        {
            AppendLog($"Flee failed: {ex.Message}");
        }
    }

    private async Task DoUseAbilityInCombatAsync(string abilityId)
    {
        try
        {
            await _connection.SendCommandAsync<object>("UseAbilityInCombat", new { AbilityId = abilityId });
        }
        catch (Exception ex)
        {
            AppendLog($"Ability failed: {ex.Message}");
        }
    }

    private Task DoUseHotbarAbilityAsync(string abilityId) =>
        IsInCombat ? DoUseAbilityInCombatAsync(abilityId) : DoUseAbilityAsync(abilityId);

    private async Task DoSendChatAsync()
    {
        var message = ChatInput.Trim();
        if (string.IsNullOrEmpty(message)) return;
        try
        {
            switch (ActiveChatChannel)
            {
                case "Zone":
                    await _connection.SendCommandAsync<object>("SendZoneMessage", new { Message = message });
                    break;
                case "Global":
                    await _connection.SendCommandAsync<object>("SendGlobalMessage", new { Message = message });
                    break;
                case "Whisper":
                    await _connection.SendCommandAsync<object>("SendWhisper", new { TargetCharacterName = WhisperTarget, Message = message });
                    break;
            }
            ChatInput = string.Empty;
        }
        catch (Exception ex)
        {
            AppendLog($"Chat failed: {ex.Message}");
        }
    }

    private async Task DoRespawnAsync()
    {
        try
        {
            await _connection.SendCommandAsync("Respawn");
        }
        catch (Exception ex)
        {
            AppendLog($"Respawn failed: {ex.Message}");
        }
    }

    /// <summary>Called from hub when the server confirms the character has entered a zone location.</summary>
    /// <param name="locationSlug">The slug of the location entered.</param>
    /// <param name="locationDisplayName">The display name of the location.</param>
    /// <param name="locationType">The type of location (e.g. "dungeon", "location", "environment").</param>
    /// <param name="spawnedEnemies">Enemy roster at the arrived location.</param>
    /// <param name="availableConnections">Outgoing connections from this location.</param>
    public void OnLocationEntered(string locationSlug, string locationDisplayName, string locationType,
        IReadOnlyList<SpawnedEnemyItemViewModel>? spawnedEnemies = null,
        IReadOnlyList<(string ToSlug, string ConnectionType, bool IsTraversable)>? availableConnections = null)
    {
        CurrentZoneLocationSlug = locationSlug;
        foreach (var loc in ZoneLocations) loc.IsCurrent = loc.Slug == locationSlug;

        SpawnedEnemies.Clear();
        if (spawnedEnemies is not null)
            foreach (var e in spawnedEnemies)
                SpawnedEnemies.Add(e);

        PopulateConnections(locationSlug, availableConnections);

        AppendLog($"Arrived at {locationDisplayName} ({locationType}).");
        if (SpawnedEnemies.Count > 0)
            AppendLog($"{SpawnedEnemies.Count} enemy/enemies present.");
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
    /// <param name="availableConnections">Outgoing connections from the destination location.</param>
    public void OnConnectionTraversed(string? toLocationSlug, string? toZoneId, bool isCrossZone,
        IReadOnlyList<(string ToSlug, string ConnectionType, bool IsTraversable)>? availableConnections = null)
    {
        _ = HandleConnectionTraversedAsync(toLocationSlug, toZoneId, isCrossZone, availableConnections);
    }

    private async Task HandleConnectionTraversedAsync(string? toLocationSlug, string? toZoneId, bool isCrossZone,
        IReadOnlyList<(string ToSlug, string ConnectionType, bool IsTraversable)>? availableConnections = null)
    {
        if (isCrossZone && toZoneId is not null)
        {
            AppendLog($"You travel to {toZoneId}.");
            await LoadZoneCoreAsync(toZoneId);
        }
        else if (toLocationSlug is not null)
        {
            CurrentZoneLocationSlug = toLocationSlug;
            foreach (var loc in ZoneLocations) loc.IsCurrent = loc.Slug == toLocationSlug;
            PopulateConnections(toLocationSlug, availableConnections);
            AppendLog($"You move to {toLocationSlug}.");
        }
    }

    private void PopulateConnections(string fromSlug,
        IReadOnlyList<(string ToSlug, string ConnectionType, bool IsTraversable)>? connections)
    {
        CurrentLocationConnections.Clear();
        if (connections is null) return;
        foreach (var c in connections)
        {
            var toSlug = c.ToSlug;
            var connType = c.ConnectionType;
            CurrentLocationConnections.Add(new ZoneConnectionLinkViewModel(
                toSlug, connType, c.IsTraversable,
                onTraverse: () => DoTraverseConnectionAsync(fromSlug, connType)));
        }
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
            InventoryItems.Add(new InventoryItemViewModel(item.ItemRef, item.Quantity, item.Durability, t => DoEquipItemAsync(t.Item1, t.Item2), DoDropItemAsync));
        IsInventoryOpen = true;
    }

    /// <summary>Populates the shop catalog with items received from the server.</summary>
    public void OnShopCatalogReceived(IReadOnlyList<ShopCatalogItemEntry> items)
    {
        ShopItems.Clear();
        foreach (var item in items)
            ShopItems.Add(new ShopItemViewModel(
                item.ItemRef, item.DisplayName, item.BuyPrice, item.SellPrice,
                onBuy:  () => DoBuyItemAsync(item.ItemRef),
                onSell: () => DoSellItemAsync(item.ItemRef)));
    }

    /// <summary>Updates gold and inventory after a successful purchase.</summary>
    public void OnItemPurchased(string itemRef, string displayName, int newGoldTotal, IReadOnlyList<InventoryItemEntry> newInventory)
    {
        Gold = newGoldTotal;
        InventoryItems.Clear();
        foreach (var item in newInventory)
            InventoryItems.Add(new InventoryItemViewModel(item.ItemRef, item.Quantity, item.Durability, t => DoEquipItemAsync(t.Item1, t.Item2), DoDropItemAsync));
        AppendLog($"Purchased {displayName}.");
    }

    /// <summary>Updates gold and inventory after a successful sale.</summary>
    public void OnItemSold(string itemRef, string displayName, int newGoldTotal, IReadOnlyList<InventoryItemEntry> newInventory)
    {
        Gold = newGoldTotal;
        InventoryItems.Clear();
        foreach (var item in newInventory)
            InventoryItems.Add(new InventoryItemViewModel(item.ItemRef, item.Quantity, item.Durability, t => DoEquipItemAsync(t.Item1, t.Item2), DoDropItemAsync));
        AppendLog($"Sold {displayName}.");
    }

    /// <summary>Removes a dropped item from the inventory list.</summary>
    public void OnItemDropped(string itemRef, IReadOnlyList<InventoryItemEntry> newInventory)
    {
        InventoryItems.Clear();
        foreach (var item in newInventory)
            InventoryItems.Add(new InventoryItemViewModel(item.ItemRef, item.Quantity, item.Durability, t => DoEquipItemAsync(t.Item1, t.Item2), DoDropItemAsync));
        AppendLog($"Dropped {itemRef}.");
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

    /// <summary>Sets the status message banner text and whether it can be player-dismissed.</summary>
    /// <param name="message">The message to display. Pass an empty string to clear the banner.</param>
    /// <param name="dismissable">
    /// <see langword="true"/> (default) for informational messages the player can close;
    /// <see langword="false"/> for critical server messages that must persist until the underlying state changes.
    /// </param>
    public void SetStatusMessage(string message, bool dismissable = true)
    {
        IsStatusMessageDismissable = dismissable;
        StatusMessage = message;
    }

    private void SyncHotbarSlots()
    {
        for (var i = 0; i < HotbarSlots.Count; i++)
            HotbarSlots[i].AbilitySlug = i < LearnedAbilities.Count ? LearnedAbilities[i] : null;
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

/// <summary>Represents one of the six ability hotbar slots in the game footer.</summary>
public class HotbarSlotViewModel : ReactiveObject
{
    private readonly ReactiveCommand<string, Unit> _useHotbarAbilityCommand;
    private string? _abilitySlug;

    /// <summary>Initializes a new instance of <see cref="HotbarSlotViewModel"/>.</summary>
    /// <param name="slotNumber">The 1-based position of this slot in the hotbar (1–6).</param>
    /// <param name="useHotbarAbilityCommand">The parent view model's hotbar ability command.</param>
    public HotbarSlotViewModel(int slotNumber, ReactiveCommand<string, Unit> useHotbarAbilityCommand)
    {
        SlotNumber = slotNumber;
        _useHotbarAbilityCommand = useHotbarAbilityCommand;

        var canUse = this.WhenAnyValue(x => x.IsEmpty, isEmpty => !isEmpty);
        UseCommand = ReactiveCommand.CreateFromObservable(
            () => _useHotbarAbilityCommand.Execute(AbilitySlug!),
            canUse);
    }

    /// <summary>Gets the 1-based slot number displayed on the hotbar button label.</summary>
    public int SlotNumber { get; }

    /// <summary>Gets or sets the ability slug assigned to this slot, or <see langword="null"/> if the slot is empty.</summary>
    public string? AbilitySlug
    {
        get => _abilitySlug;
        set
        {
            this.RaiseAndSetIfChanged(ref _abilitySlug, value);
            this.RaisePropertyChanged(nameof(DisplayLabel));
            this.RaisePropertyChanged(nameof(IsEmpty));
        }
    }

    /// <summary>Gets the button label: <c>"{N}: {ability}"</c> when occupied, or <c>"{N} —"</c> when empty.</summary>
    public string DisplayLabel => IsEmpty ? $"{SlotNumber} \u2014" : $"{SlotNumber}: {AbilitySlug}";

    /// <summary>Gets whether this slot has no ability assigned.</summary>
    public bool IsEmpty => AbilitySlug is null;

    /// <summary>Executes the ability assigned to this slot via the parent's combat command. Disabled when the slot is empty.</summary>
    public ReactiveCommand<Unit, Unit> UseCommand { get; }
}

/// <summary>
/// A single item-slot entry received in the <c>InventoryLoaded</c> hub payload.
/// Mirrors <c>InventoryItemDto</c> on the server side.
/// </summary>
/// <param name="ItemRef">Item-reference slug (e.g. <c>"iron_sword"</c>).</param>
/// <param name="Quantity">Stack size.</param>
/// <param name="Durability">Current durability (0–100), or <see langword="null"/> for stackable items.</param>
public record InventoryItemEntry(string ItemRef, int Quantity, int? Durability);

/// <summary>A single item available in a merchant's shop, including buy and sell prices.</summary>
/// <param name="ItemRef">Item-reference slug.</param>
/// <param name="DisplayName">Human-readable name shown in the shop UI.</param>
/// <param name="BuyPrice">Gold cost to purchase the item.</param>
/// <param name="SellPrice">Gold the merchant pays when the character sells the item.</param>
public record ShopCatalogItemEntry(string ItemRef, string DisplayName, int BuyPrice, int SellPrice);

/// <summary>A live enemy at the character's current zone location, displayed in the enemy roster UI.</summary>
public class SpawnedEnemyItemViewModel : ReactiveObject
{
    private int _currentHealth;

    /// <summary>Gets the unique instance ID of this spawned enemy.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the display name of this enemy.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the combat level of this enemy.</summary>
    public int Level { get; init; }

    /// <summary>Gets the maximum HP of this enemy.</summary>
    public int MaxHealth { get; init; }

    /// <summary>Gets or sets the current HP of this enemy.</summary>
    public int CurrentHealth
    {
        get => _currentHealth;
        set
        {
            this.RaiseAndSetIfChanged(ref _currentHealth, value);
            this.RaisePropertyChanged(nameof(IsAlive));
        }
    }

    /// <summary>Gets a value indicating whether this enemy has any remaining health.</summary>
    public bool IsAlive => CurrentHealth > 0;
}
