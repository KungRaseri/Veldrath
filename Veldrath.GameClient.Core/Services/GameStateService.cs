using System.ComponentModel;
using System.Runtime.CompilerServices;
using RealmEngine.Shared.Models;
using Veldrath.Contracts.Map;
using Veldrath.GameClient.Core.Abstractions;
using Veldrath.GameClient.Core.Payloads;

namespace Veldrath.GameClient.Core.Services;

// ── GameStateService ─────────────────────────────────────────────────────────

/// <summary>
/// Per-circuit game state manager.  Holds the authoritative state for the current player's
/// game session and implements <see cref="INotifyPropertyChanged"/> so consumers
/// can react to state changes.  Call <c>Apply*</c> methods from hub event handlers to
/// update state.
/// Implements <see cref="IGameStateService"/> for abstraction across consumers.
/// </summary>
public sealed class GameStateService : IGameStateService
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raised when a new chat message is received.  Components can subscribe to this
    /// for targeted updates without re-rendering on every <see cref="PropertyChanged"/> event.
    /// </summary>
    public event Action<string>? ChatMessageReceived;

    /// <summary>
    /// Raised when a new system message is received.  Components can subscribe to this
    /// for targeted updates without re-rendering on every <see cref="PropertyChanged"/> event.
    /// </summary>
    public event Action<string>? SystemMessageReceived;

    // ── Connection state ────────────────────────────────────────────────────────

    /// <summary>The server-assigned connection ID for the current SignalR session.</summary>
    public string? ServerConnectionId { get; private set; }

    /// <summary>Whether the SignalR connection to the game hub is currently established.</summary>
    public bool IsConnected { get; private set; }

    // ── Character state ─────────────────────────────────────────────────────────

    /// <summary>The currently selected character's basic information, or <c>null</c> if none selected.</summary>
    public CharacterBasicInfo? CurrentCharacter { get; private set; }

    /// <inheritdoc />
    string? IGameStateService.CurrentCharacterId => CurrentCharacter?.Id.ToString();

    /// <inheritdoc />
    string? IGameStateService.CurrentCharacterName => CurrentCharacter?.Name;

    /// <inheritdoc />
    int IGameStateService.CurrentCharacterLevel => CurrentCharacter?.Level ?? 0;

    /// <inheritdoc />
    int IGameStateService.CurrentCharacterGold => CurrentCharacter?.Gold ?? 0;

    /// <inheritdoc />
    ZoneState IGameStateService.CurrentZone => CurrentZone;

    /// <inheritdoc />
    string? IGameStateService.CurrentZoneLocationSlug => CurrentZoneLocationSlug;

    /// <inheritdoc />
    IReadOnlyList<ZoneLocationEntry> IGameStateService.ZoneLocations => ZoneLocations;

    /// <inheritdoc />
    IReadOnlyList<ZoneConnectionLink> IGameStateService.CurrentLocationConnections => CurrentLocationConnections;

    // ── Live character state (progression / combat) ─────────────────────────────

    /// <summary>The current character's live state snapshot. Updates create new instances via <c>with</c>.</summary>
    public CharacterState CurrentCharacterState { get; private set; } = new();

    /// <inheritdoc />
    CharacterState IGameStateService.CurrentCharacter => CurrentCharacterState;

    /// <inheritdoc />
    CharacterBasicInfo? IGameStateService.CurrentCharacterInfo => CurrentCharacter;

    /// <inheritdoc />
    IReadOnlyList<ChatMessage> IGameStateService.ChatMessages => ChatMessages;

    /// <inheritdoc />
    IReadOnlyList<OccupantInfo> IGameStateService.ZoneOccupants => ZoneOccupants;

    /// <inheritdoc />
    IReadOnlyList<EnemyInfo> IGameStateService.ZoneEnemies => ZoneEnemies;

    // ── Inventory & equipment ───────────────────────────────────────────────────

    /// <inheritdoc />
    IReadOnlyList<Item> IGameStateService.InventoryItems => InventoryItems;

    /// <inheritdoc />
    IReadOnlyDictionary<string, Item> IGameStateService.EquippedItems => EquippedItems;

    /// <summary>The items currently in the character's inventory bag.</summary>
    public List<Item> InventoryItems { get; private set; } = [];

    /// <summary>The items currently equipped by the character, keyed by slot name.</summary>
    public Dictionary<string, Item> EquippedItems { get; private set; } = [];

    // ── Shop state ──────────────────────────────────────────────────────────────

    /// <inheritdoc />
    IReadOnlyList<ShopItemEntry> IGameStateService.ShopCatalog => ShopCatalog;

    /// <summary>The current merchant's shop catalog.</summary>
    public List<ShopItemEntry> ShopCatalog { get; private set; } = [];

    /// <summary>Whether the character is currently in a shop or merchant interface.</summary>
    public bool InShop { get; private set; }

    /// <summary>The display name of the current shop, or <c>null</c> if not in a shop.</summary>
    public string? ShopName { get; private set; }

    // ── Zone state ──────────────────────────────────────────────────────────────

    /// <summary>The current zone's unique identifier.</summary>
    public string? CurrentZoneId { get; private set; }

    /// <summary>The display name of the current zone.</summary>
    public string? CurrentZoneName { get; private set; }

    /// <summary>Other players occupying the current zone.</summary>
    public List<OccupantInfo> ZoneOccupants { get; private set; } = [];

    /// <summary>Visible enemies in the current zone.</summary>
    public List<EnemyInfo> ZoneEnemies { get; private set; } = [];

    /// <summary>The current zone metadata.</summary>
    public ZoneState CurrentZone { get; private set; } = new();

    /// <summary>The slug of the current zone location (POI), or <c>null</c> if not at a named location.</summary>
    public string? CurrentZoneLocationSlug { get; private set; }

    /// <summary>Stable identifier of the current region or zone map.</summary>
    public string? CurrentMapId { get; private set; }

    /// <summary>Stable identifier of the node the character occupies.</summary>
    public string? CurrentNodeId { get; private set; }

    /// <summary>The currently loaded map graph, or <c>null</c> before it is requested.</summary>
    public MapGraphDto? CurrentMap { get; private set; }

    /// <summary>Summaries of all active maps.</summary>
    public List<MapSummaryDto> AvailableMaps { get; private set; } = [];

    /// <inheritdoc />
    IReadOnlyList<MapSummaryDto> IGameStateService.AvailableMaps => AvailableMaps;

    /// <summary>The zone locations available in the current zone.</summary>
    public List<ZoneLocationEntry> ZoneLocations { get; private set; } = [];

    /// <summary>The traversal connections available from the current location.</summary>
    public List<ZoneConnectionLink> CurrentLocationConnections { get; private set; } = [];

    // ── Location discovery state ─────────────────────────────────────────────────

    /// <summary>The locations the character has discovered and can fast-travel to.</summary>
    public List<IGameStateService.LocationReference> KnownLocations { get; private set; } = [];

    /// <summary>Whether the player is currently standing on a location tile.</summary>
    public bool IsAtLocation { get; private set; }

    /// <summary>The prose description of the current location, or <c>null</c>.</summary>
    public string? CurrentLocationDescription { get; private set; }

    /// <inheritdoc />
    IReadOnlyList<IGameStateService.LocationReference> IGameStateService.KnownLocations => KnownLocations;

    /// <inheritdoc />
    bool IGameStateService.IsAtLocation => IsAtLocation;

    /// <inheritdoc />
    string? IGameStateService.CurrentLocationDescription => CurrentLocationDescription;

    // ── Combat state ────────────────────────────────────────────────────────────

    /// <summary>Whether the player is currently engaged in combat.</summary>
    public bool IsInCombat { get; private set; }

    /// <summary>The enemy the player is currently fighting, or <c>null</c> if not in combat.</summary>
    public EnemyInfo? CombatEnemy { get; private set; }

    /// <summary>The result description of the last combat action (e.g. "You hit for 12 damage").</summary>
    public string? LastCombatActionResult { get; private set; }

    // ── Kick state ──────────────────────────────────────────────────────────────

    /// <summary>Whether the player has been forcibly kicked from the server.</summary>
    public bool IsKicked { get; private set; }

    /// <summary>The reason for the kick, or <c>null</c> if not kicked.</summary>
    public string? KickReason { get; private set; }

    // ── Skill XP ────────────────────────────────────────────────────────────────

    /// <summary>Skill experience points keyed by skill identifier slug.</summary>
    public Dictionary<string, int> SkillXp { get; private set; } = [];

    /// <summary>Skill ranks keyed by skill identifier slug.</summary>
    public Dictionary<string, int> SkillRanks { get; private set; } = [];

    // ── Chat state ──────────────────────────────────────────────────────────────

    /// <summary>The ordered list of chat messages received during the current session.</summary>
    public List<ChatMessage> ChatMessages { get; private set; } = [];

    // ── IGameStateService Apply methods (called from hub event handlers) ────────

    /// <inheritdoc />
    public void ApplyCharacterSelected(CharacterSelectedPayload payload)
    {
        var character = new CharacterBasicInfo(
            payload.Id, payload.Name, payload.ClassName, payload.Level,
            payload.Experience, payload.CurrentHealth, payload.MaxHealth,
            payload.CurrentMana, payload.MaxMana, payload.Gold);

        ApplyCharacterSelected(character, payload.CurrentZoneId);

        if (!string.IsNullOrWhiteSpace(payload.CurrentMapId))
            CurrentMapId = payload.CurrentMapId;
        if (!string.IsNullOrWhiteSpace(payload.CurrentNodeId))
            CurrentNodeId = payload.CurrentNodeId;

        // Seed equipment from the selection payload so starter gear is visible before the
        // first inventory request. Full catalog details replace these placeholders later.
        if (payload.Equipment is { Count: > 0 })
        {
            EquippedItems = payload.Equipment.ToDictionary(
                pair => pair.Key,
                pair => (Item)new() { Slug = pair.Value, Name = HumanizeItemRef(pair.Value) },
                StringComparer.OrdinalIgnoreCase);
            RaisePropertyChanged(nameof(EquippedItems));
        }

        CurrentCharacterState = new CharacterState
        {
            Level = payload.Level,
            XP = (int)payload.Experience,
            Gold = payload.Gold,
            CurrentHealth = payload.CurrentHealth,
            MaxHealth = payload.MaxHealth,
            CurrentMana = payload.CurrentMana,
            MaxMana = payload.MaxMana,
            IsDead = false,
            Strength = payload.Strength,
            Dexterity = payload.Dexterity,
            Constitution = payload.Constitution,
            Intelligence = payload.Intelligence,
            Wisdom = payload.Wisdom,
            Charisma = payload.Charisma,
            UnspentAttributePoints = payload.UnspentAttributePoints,
        };
        RaisePropertyChanged(nameof(CurrentCharacterState));
        RaisePropertyChanged(nameof(CurrentMapId));
        RaisePropertyChanged(nameof(CurrentNodeId));
    }

    /// <inheritdoc />
    public void ApplyZoneEntered(ZoneEnteredPayload payload)
    {
        ApplyZoneEntered(payload.Id, payload.Name);

        CurrentZone = new ZoneState
        {
            ZoneId       = payload.Id,
            Name         = payload.Name,
            Description  = payload.Description,
            Type         = payload.ZoneType,
            HasInn       = payload.HasInn,
            HasMerchant  = payload.HasMerchant,
        };

        if (!string.IsNullOrWhiteSpace(payload.CurrentMapId))
            CurrentMapId = payload.CurrentMapId;
        if (!string.IsNullOrWhiteSpace(payload.CurrentNodeId))
            CurrentNodeId = payload.CurrentNodeId;

        RaisePropertyChanged(nameof(CurrentZone));
        RaisePropertyChanged(nameof(CurrentMapId));
        RaisePropertyChanged(nameof(CurrentNodeId));
    }

    /// <inheritdoc />
    public void ApplyMapGraph(MapGraphReceivedPayload payload)
    {
        if (!payload.Success || payload.Map is null)
        {
            if (!string.IsNullOrWhiteSpace(payload.ErrorMessage))
                ApplySystemMessage(payload.ErrorMessage);
            return;
        }

        CurrentMap = payload.Map;
        CurrentMapId = payload.Map.Id;
        CurrentNodeId = string.IsNullOrWhiteSpace(payload.CurrentNodeId)
            ? payload.Map.EntryNodeId
            : payload.CurrentNodeId;
        AvailableMaps = payload.AvailableMaps is null ? [] : [.. payload.AvailableMaps];

        RaisePropertyChanged(nameof(CurrentMap));
        RaisePropertyChanged(nameof(CurrentMapId));
        RaisePropertyChanged(nameof(CurrentNodeId));
        RaisePropertyChanged(nameof(AvailableMaps));
    }

    /// <inheritdoc />
    public void ApplyNodeTraveled(NodeTraveledPayload payload)
    {
        if (!payload.Success)
        {
            if (!string.IsNullOrWhiteSpace(payload.ErrorMessage))
                ApplySystemMessage(payload.ErrorMessage);
            return;
        }

        var mapChanged = !string.IsNullOrWhiteSpace(payload.MapId) &&
                         !string.Equals(payload.MapId, CurrentMapId, StringComparison.OrdinalIgnoreCase);

        CurrentMapId = string.IsNullOrWhiteSpace(payload.MapId) ? CurrentMapId : payload.MapId;
        CurrentNodeId = string.IsNullOrWhiteSpace(payload.NodeId) ? CurrentNodeId : payload.NodeId;
        CurrentZoneLocationSlug = payload.LocationSlug;
        IsAtLocation = !string.IsNullOrWhiteSpace(payload.LocationSlug);

        if (mapChanged)
            CurrentMap = null;

        RaisePropertyChanged(nameof(CurrentMapId));
        RaisePropertyChanged(nameof(CurrentNodeId));
        RaisePropertyChanged(nameof(CurrentZoneLocationSlug));
        RaisePropertyChanged(nameof(IsAtLocation));
        if (mapChanged)
            RaisePropertyChanged(nameof(CurrentMap));

        if (!string.IsNullOrWhiteSpace(payload.Message))
            ApplySystemMessage(payload.Message);
    }

    /// <inheritdoc />
    public void ApplyCombatStarted(CombatStartedPayload payload)
    {
        var enemy = new EnemyInfo(
            payload.EnemyId, payload.EnemyName, payload.EnemyLevel,
            payload.EnemyCurrentHealth, payload.EnemyMaxHealth);
        ApplyCombatStarted(enemy);
    }

    /// <inheritdoc />
    public void ApplyCombatTurn(CombatTurnPayload payload)
    {
        var resultText = BuildCombatResultText(payload);
        ApplyCombatTurn(resultText);
        ApplyCharacterHealth(payload.PlayerRemainingHealth);
    }

    /// <inheritdoc />
    public void ApplyCombatEnded(CombatEndedPayload payload)
    {
        ApplyCombatEnded();
        ApplySystemMessage($"Combat ended: {payload.Reason}");
    }

    /// <inheritdoc />
    public void ApplyChatMessage(ChatMessageHubDto payload)
    {
        var msg = new ChatMessage(
            payload.CharacterId, payload.Channel, payload.Sender,
            payload.Message, payload.Timestamp);
        ApplyChatMessage(msg);
    }

    /// <inheritdoc />
    public void ApplyPlayerEntered(PlayerEnteredPayload payload)
    {
        var occupant = new OccupantInfo(payload.CharacterId, payload.CharacterName, DateTimeOffset.UtcNow);
        ApplyPlayerEntered(occupant);
    }

    /// <inheritdoc />
    public void ApplyPlayerLeft(PlayerLeftPayload payload)
    {
        ApplyPlayerLeft(payload.CharacterId);
    }

    /// <inheritdoc />
    public void ApplyEnemyDefeated(EnemyDefeatedPayload payload)
    {
        var enemyName = CombatEnemy?.EnemyName ?? "an enemy";
        var isCurrentPlayer = CurrentCharacter is not null && CurrentCharacter.Id == payload.CharacterId;

        if (isCurrentPlayer)
            ApplySystemMessage($"You defeated {enemyName}!");
        else
            ApplySystemMessage($"{payload.CharacterId} defeated an enemy!");
    }

    /// <inheritdoc />
    public void ApplyCharacterRespawned()
    {
        var maxHp = CurrentCharacterState.MaxHealth;
        var maxMp = CurrentCharacterState.MaxMana;

        CurrentCharacterState = CurrentCharacterState with
        {
            IsDead = false,
            CurrentHealth = maxHp,
            CurrentMana = maxMp,
        };

        if (CurrentCharacter is not null)
        {
            CurrentCharacter = CurrentCharacter with
            {
                CurrentHealth = maxHp,
                CurrentMana = maxMp,
            };
            RaisePropertyChanged(nameof(CurrentCharacter));
        }

        RaisePropertyChanged(nameof(CurrentCharacterState));
        ApplySystemMessage("You have been revived!");
    }

    // ── Inventory & equipment Apply methods ─────────────────────────────────────

    /// <inheritdoc />
    public void ApplyInventoryUpdated(IReadOnlyList<Item> items, IReadOnlyDictionary<string, Item> equipped)
    {
        InventoryItems = [.. items];
        EquippedItems = new Dictionary<string, Item>(equipped);
        RaisePropertyChanged(nameof(InventoryItems));
        RaisePropertyChanged(nameof(EquippedItems));
    }

    /// <inheritdoc />
    public void ApplyEquipmentChanged(string slot, Item? item)
    {
        if (item is null)
            EquippedItems.Remove(slot);
        else
            EquippedItems[slot] = item;
        RaisePropertyChanged(nameof(EquippedItems));
    }

    // ── Shop Apply methods ──────────────────────────────────────────────────────

    /// <inheritdoc />
    public void ApplyShopCatalogUpdated(IReadOnlyList<ShopItemEntry> catalog)
    {
        ShopCatalog = [.. catalog];
        RaisePropertyChanged(nameof(ShopCatalog));
    }

    // ── Quest log ───────────────────────────────────────────────────────────────

    /// <inheritdoc />
    IReadOnlyList<QuestLogEntry> IGameStateService.QuestLog => QuestLog;

    /// <inheritdoc />
    IReadOnlyList<QuestLogEntry> IGameStateService.CompletedQuests => CompletedQuests;

    /// <summary>Gets the currently active quests for the selected character.</summary>
    public List<QuestLogEntry> QuestLog { get; private set; } = [];

    /// <summary>Gets the completed quests for the selected character.</summary>
    public List<QuestLogEntry> CompletedQuests { get; private set; } = [];

    /// <inheritdoc />
    public void ApplyQuestLogUpdated(IReadOnlyList<QuestLogEntry> active, IReadOnlyList<QuestLogEntry> completed)
    {
        QuestLog = [.. active];
        CompletedQuests = [.. completed];
        RaisePropertyChanged(nameof(QuestLog));
        RaisePropertyChanged(nameof(CompletedQuests));
    }

    // ── Settings ────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    GameSettingsState IGameStateService.Settings => Settings;

    /// <summary>Gets the current game settings for this session.</summary>
    public GameSettingsState Settings { get; private set; } = new();

    /// <inheritdoc />
    public void ApplySettings(GameSettingsState settings)
    {
        Settings = settings;
        RaisePropertyChanged(nameof(Settings));
    }

    // ── Kick Apply methods ──────────────────────────────────────────────────────

    /// <inheritdoc />
    public void ApplyKicked(string reason)
    {
        IsKicked = true;
        KickReason = reason;
        IsConnected = false;
        ApplySystemMessage($"You have been kicked: {reason}");
        RaisePropertyChanged(nameof(IsKicked));
        RaisePropertyChanged(nameof(KickReason));
        RaisePropertyChanged(nameof(IsConnected));
    }

    // ── Progression Apply methods ───────────────────────────────────────────────

    /// <inheritdoc />
    public void ApplyExperienceGained(int newLevel, int newXP, int newLeveledUpTo)
    {
        var leveledUp = newLeveledUpTo > 0;
        CurrentCharacterState = CurrentCharacterState with
        {
            Level = newLevel,
            XP = newXP,
        };

        if (CurrentCharacter is not null)
        {
            CurrentCharacter = CurrentCharacter with { Level = newLevel, Experience = newXP };
            RaisePropertyChanged(nameof(CurrentCharacter));
        }

        RaisePropertyChanged(nameof(CurrentCharacterState));

        if (leveledUp)
            ApplySystemMessage($"You reached level {newLeveledUpTo}!");
        else
            ApplySystemMessage("You gained XP!");
    }

    /// <inheritdoc />
    public void ApplyGoldChanged(int goldAdded, int newGoldTotal)
    {
        CurrentCharacterState = CurrentCharacterState with { Gold = newGoldTotal };

        if (CurrentCharacter is not null)
        {
            CurrentCharacter = CurrentCharacter with { Gold = newGoldTotal };
            RaisePropertyChanged(nameof(CurrentCharacter));
        }

        RaisePropertyChanged(nameof(CurrentCharacterState));

        if (goldAdded >= 0)
            ApplySystemMessage($"Received {goldAdded} gold.");
        else
            ApplySystemMessage($"Spent {-goldAdded} gold.");
    }

    /// <inheritdoc />
    public void ApplyDamageTaken(int damage, int currentHP, int maxHP, bool isDead)
    {
        CurrentCharacterState = CurrentCharacterState with
        {
            CurrentHealth = currentHP,
            MaxHealth = maxHP,
            IsDead = isDead,
        };

        if (CurrentCharacter is not null)
        {
            CurrentCharacter = CurrentCharacter with { CurrentHealth = currentHP, MaxHealth = maxHP };
            RaisePropertyChanged(nameof(CurrentCharacter));
        }

        RaisePropertyChanged(nameof(CurrentCharacterState));

        if (isDead)
            ApplySystemMessage("You have been slain!");
    }

    // ── Shop state Apply methods ────────────────────────────────────────────────

    /// <inheritdoc />
    public void ApplyShopVisited(string zoneId, string zoneName)
    {
        InShop = true;
        ShopName = zoneName;
        RaisePropertyChanged(nameof(InShop));
        RaisePropertyChanged(nameof(ShopName));
    }

    // ── Inventory transaction Apply methods ─────────────────────────────────────

    /// <inheritdoc />
    public void ApplyItemTransacted(string itemName, int newGold, List<Item> inventory)
    {
        InventoryItems = inventory;
        CurrentCharacterState = CurrentCharacterState with { Gold = newGold };

        if (CurrentCharacter is not null)
        {
            CurrentCharacter = CurrentCharacter with { Gold = newGold };
            RaisePropertyChanged(nameof(CurrentCharacter));
        }

        RaisePropertyChanged(nameof(InventoryItems));
        RaisePropertyChanged(nameof(CurrentCharacterState));
    }

    // ── Attribute allocation ────────────────────────────────────────────────────

    /// <inheritdoc />
    public void ApplyAttributePointsAllocated(int remaining, Dictionary<string, int> stats)
    {
        CurrentCharacterState = CurrentCharacterState with
        {
            UnspentAttributePoints = remaining,
            Strength = stats.GetValueOrDefault("Strength", CurrentCharacterState.Strength),
            Dexterity = stats.GetValueOrDefault("Dexterity", CurrentCharacterState.Dexterity),
            Constitution = stats.GetValueOrDefault("Constitution", CurrentCharacterState.Constitution),
            Intelligence = stats.GetValueOrDefault("Intelligence", CurrentCharacterState.Intelligence),
            Wisdom = stats.GetValueOrDefault("Wisdom", CurrentCharacterState.Wisdom),
            Charisma = stats.GetValueOrDefault("Charisma", CurrentCharacterState.Charisma),
        };

        RaisePropertyChanged(nameof(CurrentCharacterState));
        ApplySystemMessage("Attribute points allocated.");
    }

    // ── Rest ────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public void ApplyCharacterRested(int hp, int maxHp, int mp, int maxMp, int gold)
    {
        CurrentCharacterState = CurrentCharacterState with
        {
            CurrentHealth = hp,
            MaxHealth = maxHp,
            CurrentMana = mp,
            MaxMana = maxMp,
            Gold = gold,
        };

        if (CurrentCharacter is not null)
        {
            CurrentCharacter = CurrentCharacter with
            {
                CurrentHealth = hp,
                MaxHealth = maxHp,
                CurrentMana = mp,
                MaxMana = maxMp,
                Gold = gold,
            };
            RaisePropertyChanged(nameof(CurrentCharacter));
        }

        RaisePropertyChanged(nameof(CurrentCharacterState));
        ApplySystemMessage("You feel well rested.");
    }

    // ── Ability ─────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public void ApplyAbilityUsed(string abilityId, int remainingMana, int hpRestored)
    {
        CurrentCharacterState = CurrentCharacterState with { CurrentMana = remainingMana };

        if (hpRestored > 0)
        {
            var newHp = Math.Min(CurrentCharacterState.CurrentHealth + hpRestored, CurrentCharacterState.MaxHealth);
            CurrentCharacterState = CurrentCharacterState with { CurrentHealth = newHp };
        }

        if (CurrentCharacter is not null)
        {
            CurrentCharacter = CurrentCharacter with { CurrentMana = remainingMana };
            if (hpRestored > 0)
            {
                var newHp = Math.Min(CurrentCharacter.CurrentHealth + hpRestored, CurrentCharacter.MaxHealth);
                CurrentCharacter = CurrentCharacter with { CurrentHealth = newHp };
            }
            RaisePropertyChanged(nameof(CurrentCharacter));
        }

        RaisePropertyChanged(nameof(CurrentCharacterState));
        ApplySystemMessage($"Ability used: {abilityId}.");
    }

    // ── Skills ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public void ApplySkillXpGained(string skillId, int xpGained, int newRank, bool rankedUp)
    {
        SkillXp[skillId] = SkillXp.GetValueOrDefault(skillId) + xpGained;
        SkillRanks[skillId] = newRank;

        RaisePropertyChanged(nameof(SkillXp));
        RaisePropertyChanged(nameof(SkillRanks));

        var msg = rankedUp
            ? $"Skill XP gained: {skillId} — reached rank {newRank}!"
            : $"Skill XP gained: {skillId} (+{xpGained} XP)";
        ApplySystemMessage(msg);
    }

    // ── Crafting ────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public void ApplyItemCrafted(string recipeName, int goldSpent, int remainingGold)
    {
        CurrentCharacterState = CurrentCharacterState with { Gold = remainingGold };

        if (CurrentCharacter is not null)
        {
            CurrentCharacter = CurrentCharacter with { Gold = remainingGold };
            RaisePropertyChanged(nameof(CurrentCharacter));
        }

        RaisePropertyChanged(nameof(CurrentCharacterState));
        ApplySystemMessage($"Crafted {recipeName}.");
    }

    // ── Dungeon ─────────────────────────────────────────────────────────────────

    /// <summary>The current dungeon identifier, or <c>null</c> if not in a dungeon.</summary>
    public string? CurrentDungeonId { get; private set; }

    /// <inheritdoc />
    public void ApplyDungeonEntered(string dungeonId)
    {
        CurrentDungeonId = dungeonId;
        RaisePropertyChanged(nameof(CurrentDungeonId));
        ApplySystemMessage("Entering dungeon...");
    }

    // ── Zone / Location Apply methods (G36-G47) ─────────────────────────────────

    /// <inheritdoc />
    public void ApplyLocationEntered(LocationEnteredPayload location)
    {
        CurrentZoneLocationSlug = location.LocationSlug;
        ZoneEnemies = location.SpawnedEnemies
            .Select(e => new EnemyInfo(e.Id, e.Name, e.Level, e.CurrentHealth, e.MaxHealth))
            .ToList();
        CurrentZone = CurrentZone with
        {
            Name = location.LocationDisplayName,
            Type = location.TypeKey,
        };
        // The wire payload carries no prose description; connections arrive via ApplyConnectionTraversed.
        CurrentLocationDescription = null;
        IsAtLocation = true;

        // Auto-discover this location for fast travel.
        var locRef = new IGameStateService.LocationReference(
            location.LocationSlug, location.LocationDisplayName, location.TypeKey,
            CurrentZoneId ?? "", null, null);
        if (KnownLocations.All(l => l.Slug != location.LocationSlug))
            KnownLocations.Add(locRef);

        RaisePropertyChanged(nameof(CurrentZoneLocationSlug));
        RaisePropertyChanged(nameof(ZoneEnemies));
        RaisePropertyChanged(nameof(CurrentZone));
        RaisePropertyChanged(nameof(CurrentLocationDescription));
        RaisePropertyChanged(nameof(IsAtLocation));
        RaisePropertyChanged(nameof(KnownLocations));
        ApplySystemMessage($"Entered {location.LocationDisplayName}.");
    }

    /// <inheritdoc />
    public void ApplyZoneLocationUnlocked(string slug, string name, string type)
    {
        if (ZoneLocations.All(z => z.Slug != slug))
        {
            ZoneLocations.Add(new ZoneLocationEntry(slug, name, type, CurrentZoneId ?? "", 0, null, null));
        }
        RaisePropertyChanged(nameof(ZoneLocations));

        // Also add to known locations for fast travel.
        var locRef = new IGameStateService.LocationReference(slug, name, type, CurrentZoneId ?? "");
        if (KnownLocations.All(l => l.Slug != slug))
            KnownLocations.Add(locRef);
        RaisePropertyChanged(nameof(KnownLocations));

        ApplySystemMessage($"Discovered {name} ({type})!");
    }

    /// <inheritdoc />
    public void ApplyLocationDiscovered(IGameStateService.LocationReference location)
    {
        if (KnownLocations.All(l => l.Slug != location.Slug))
            KnownLocations.Add(location);
        RaisePropertyChanged(nameof(KnownLocations));
    }

    /// <inheritdoc />
    public void ApplyLocationExited()
    {
        IsAtLocation = false;
        CurrentLocationDescription = null;
        RaisePropertyChanged(nameof(IsAtLocation));
        RaisePropertyChanged(nameof(CurrentLocationDescription));
    }

    /// <inheritdoc />
    public void ApplyConnectionTraversed(string slug, string zoneId, bool isCrossZone, IReadOnlyList<ZoneConnectionLink> connections)
    {
        CurrentLocationConnections = [.. connections];
        if (isCrossZone)
        {
            CurrentZoneId = zoneId;
            CurrentZoneLocationSlug = slug;
            RaisePropertyChanged(nameof(CurrentZoneId));
            RaisePropertyChanged(nameof(CurrentZoneLocationSlug));
        }
        RaisePropertyChanged(nameof(CurrentLocationConnections));
    }

    // ── Existing Apply methods (preserved for backward compatibility) ─────────

    /// <summary>Updates the connection ID after a successful hub connection.</summary>
    /// <param name="connectionId">The server-assigned connection ID.</param>
    public void ApplyServerInfo(string connectionId)
    {
        ServerConnectionId = connectionId;
        IsConnected = true;
        RaisePropertyChanged(nameof(ServerConnectionId));
        RaisePropertyChanged(nameof(IsConnected));
    }

    /// <summary>Updates state after a character has been selected.</summary>
    /// <param name="character">The selected character's basic information.</param>
    /// <param name="currentZoneId">Optional zone ID the character is currently in (from server).</param>
    public void ApplyCharacterSelected(CharacterBasicInfo character, string? currentZoneId = null)
    {
        CurrentCharacter = character;
        if (currentZoneId is not null)
            CurrentZoneId = currentZoneId;
        RaisePropertyChanged(nameof(CurrentCharacter));
        RaisePropertyChanged(nameof(CurrentZoneId));
    }

    /// <summary>Updates state after entering a zone.</summary>
    /// <param name="zoneId">The zone identifier.</param>
    /// <param name="zoneName">The zone display name.</param>
    public void ApplyZoneEntered(string zoneId, string zoneName)
    {
        CurrentZoneId = zoneId;
        CurrentZoneName = zoneName;
        RaisePropertyChanged(nameof(CurrentZoneId));
        RaisePropertyChanged(nameof(CurrentZoneName));
    }

    /// <summary>Adds an occupant that entered the zone.</summary>
    /// <param name="occupant">The occupant information.</param>
    public void ApplyPlayerEntered(OccupantInfo occupant)
    {
        ZoneOccupants.Add(occupant);
        RaisePropertyChanged(nameof(ZoneOccupants));
    }

    /// <summary>Removes an occupant that left the zone.</summary>
    /// <param name="characterId">The character identifier of the leaving player.</param>
    public void ApplyPlayerLeft(Guid characterId)
    {
        ZoneOccupants.RemoveAll(o => o.CharacterId == characterId);
        RaisePropertyChanged(nameof(ZoneOccupants));
    }

    /// <summary>Updates state when combat starts with an enemy.</summary>
    /// <param name="enemy">The enemy being engaged.</param>
    public void ApplyCombatStarted(EnemyInfo enemy)
    {
        IsInCombat = true;
        CombatEnemy = enemy;
        LastCombatActionResult = null;
        RaisePropertyChanged(nameof(IsInCombat));
        RaisePropertyChanged(nameof(CombatEnemy));
        RaisePropertyChanged(nameof(LastCombatActionResult));
    }

    /// <summary>Updates state after a combat turn has been processed.</summary>
    /// <param name="result">A description of the turn result (e.g. "You hit for 12 damage").</param>
    public void ApplyCombatTurn(string result)
    {
        LastCombatActionResult = result;
        RaisePropertyChanged(nameof(LastCombatActionResult));
    }

    /// <summary>Updates state when combat ends (flee, victory, or defeat).</summary>
    public void ApplyCombatEnded()
    {
        IsInCombat = false;
        CombatEnemy = null;
        LastCombatActionResult = null;
        RaisePropertyChanged(nameof(IsInCombat));
        RaisePropertyChanged(nameof(CombatEnemy));
        RaisePropertyChanged(nameof(LastCombatActionResult));
    }

    /// <summary>Updates state when an enemy is defeated (outside of <see cref="ApplyCombatEnded"/>).</summary>
    /// <param name="enemyName">The name of the defeated enemy.</param>
    /// <param name="xpGained">The experience points gained.</param>
    public void ApplyEnemyDefeated(string enemyName, int xpGained)
    {
        var msg = $"Defeated {enemyName}! Gained {xpGained} XP.";
        ApplySystemMessage(msg);
    }

    /// <summary>Appends a chat message to the chat log.</summary>
    /// <param name="msg">The chat message to append.</param>
    public void ApplyChatMessage(ChatMessage msg)
    {
        ChatMessages.Add(msg);
        ChatMessageReceived?.Invoke(msg.Message);
        RaisePropertyChanged(nameof(ChatMessages));
    }

    /// <summary>Appends a system message to the chat log.</summary>
    /// <param name="message">The system message text.</param>
    public void ApplySystemMessage(string message)
    {
        ChatMessages.Add(new ChatMessage(
            Guid.Empty,
            "system",
            "System",
            message,
            DateTimeOffset.UtcNow));
        SystemMessageReceived?.Invoke(message);
        RaisePropertyChanged(nameof(ChatMessages));
    }

    /// <summary>
    /// Resets all state to defaults.  Call when disconnecting, logging out, or when
    /// the game session ends.
    /// </summary>
    public void Reset()
    {
        ServerConnectionId = null;
        IsConnected = false;
        CurrentCharacter = null;
        CurrentCharacterState = new();
        CurrentZoneId = null;
        CurrentZoneName = null;
        ZoneOccupants = [];
        ZoneEnemies = [];
        IsInCombat = false;
        CombatEnemy = null;
        LastCombatActionResult = null;
        ChatMessages = [];
        InventoryItems = [];
        EquippedItems = [];
        ShopCatalog = [];
        InShop = false;
        ShopName = null;
        QuestLog = [];
        CompletedQuests = [];
        Settings = new();
        IsKicked = false;
        KickReason = null;
        SkillXp = [];
        SkillRanks = [];
        CurrentDungeonId = null;
        CurrentZone = new();
        CurrentZoneLocationSlug = null;
        ZoneLocations = [];
        CurrentLocationConnections = [];

        RaisePropertyChanged(nameof(ServerConnectionId));
        RaisePropertyChanged(nameof(IsConnected));
        RaisePropertyChanged(nameof(CurrentCharacter));
        RaisePropertyChanged(nameof(CurrentCharacterState));
        RaisePropertyChanged(nameof(CurrentZoneId));
        RaisePropertyChanged(nameof(CurrentZoneName));
        RaisePropertyChanged(nameof(ZoneOccupants));
        RaisePropertyChanged(nameof(ZoneEnemies));
        RaisePropertyChanged(nameof(IsInCombat));
        RaisePropertyChanged(nameof(CombatEnemy));
        RaisePropertyChanged(nameof(LastCombatActionResult));
        RaisePropertyChanged(nameof(ChatMessages));
        RaisePropertyChanged(nameof(InventoryItems));
        RaisePropertyChanged(nameof(EquippedItems));
        RaisePropertyChanged(nameof(ShopCatalog));
        RaisePropertyChanged(nameof(InShop));
        RaisePropertyChanged(nameof(ShopName));
        RaisePropertyChanged(nameof(QuestLog));
        RaisePropertyChanged(nameof(CompletedQuests));
        RaisePropertyChanged(nameof(Settings));
        RaisePropertyChanged(nameof(IsKicked));
        RaisePropertyChanged(nameof(KickReason));
        RaisePropertyChanged(nameof(SkillXp));
        RaisePropertyChanged(nameof(SkillRanks));
        RaisePropertyChanged(nameof(CurrentDungeonId));
        RaisePropertyChanged(nameof(CurrentZone));
        RaisePropertyChanged(nameof(CurrentZoneLocationSlug));
        RaisePropertyChanged(nameof(ZoneLocations));
        RaisePropertyChanged(nameof(CurrentLocationConnections));
    }

    /// <summary>Subscribe to any property change. Returns an <see cref="IDisposable"/> that unsubscribes.</summary>
    /// <param name="handler">The handler to invoke when any property changes.</param>
    /// <returns>An <see cref="IDisposable"/> that unsubscribes the handler when disposed.</returns>
    public IDisposable OnStateChanged(Action handler)
    {
        PropertyChanged += onChanged;
        return new Subscription(() => PropertyChanged -= onChanged);

        void onChanged(object? s, PropertyChangedEventArgs e) => handler();
    }

    /// <summary>Simple disposable that runs an action on dispose.</summary>
    private sealed class Subscription(Action onDispose) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose() => onDispose();
    }

    private void ApplyCharacterHealth(int playerRemainingHealth)
    {
        if (CurrentCharacter is not null)
        {
            CurrentCharacter = CurrentCharacter with { CurrentHealth = playerRemainingHealth };
            RaisePropertyChanged(nameof(CurrentCharacter));
        }

        CurrentCharacterState = CurrentCharacterState with { CurrentHealth = playerRemainingHealth };
        RaisePropertyChanged(nameof(CurrentCharacterState));
    }

    /// <summary>Builds a human-readable combat result string from the turn payload.</summary>
    private static string HumanizeItemRef(string itemRef)
    {
        var leaf = itemRef.Split(':', '/').LastOrDefault() ?? itemRef;
        var words = leaf.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Length == 0
                ? word
                : char.ToUpperInvariant(word[0]) + word[1..]);
        return string.Join(' ', words);
    }

    private static string BuildCombatResultText(CombatTurnPayload p)
    {
        var parts = new List<string>();

        if (p.Action == "attack" && p.PlayerDamage > 0)
            parts.Add($"You hit for {p.PlayerDamage} damage.");
        else if (p.Action == "attack")
            parts.Add("Your attack missed.");

        if (p.Action == "defend")
            parts.Add("You brace for impact.");

        if (p.Action == "ability" && p.AbilityDamage > 0)
            parts.Add($"Your ability dealt {p.AbilityDamage} damage.");
        if (p.Action == "ability" && p.HealthRestored > 0)
            parts.Add($"You restored {p.HealthRestored} health.");

        if (p.EnemyDamage > 0)
            parts.Add(string.IsNullOrEmpty(p.EnemyAbilityUsed)
                ? $"Enemy hits you for {p.EnemyDamage} damage."
                : $"Enemy uses {p.EnemyAbilityUsed} for {p.EnemyDamage} damage.");
        else if (p.Action == "defend")
            parts.Add("You blocked the enemy attack.");

        if (p.EnemyDefeated)
            parts.Add("Enemy defeated!");

        if (p.PlayerDefeated)
            parts.Add("You have been defeated!");

        if (p.XpEarned > 0)
            parts.Add($"Gained {p.XpEarned} XP.");
        if (p.GoldEarned > 0)
            parts.Add($"Gained {p.GoldEarned} gold.");

        return parts.Count > 0 ? string.Join(" ", parts) : "Combat turn processed.";
    }

    private void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
