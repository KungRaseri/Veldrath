using System.ComponentModel;
using RealmEngine.Shared.Models;
using Veldrath.GameClient.Core.Abstractions;
using Veldrath.GameClient.Core.Payloads;

namespace Veldrath.GameClient.Core.Tests.Infrastructure;

/// <summary>
/// Fake implementation of <see cref="IGameStateService"/> for unit testing.
/// Tracks which Apply methods were called and records the payloads.
/// </summary>
public sealed class FakeGameStateService : IGameStateService
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    // ── Tracked state ─────────────────────────────────────────────────────────

    /// <summary>Gets or sets the simulated connection state.</summary>
    public bool IsConnected { get; set; }

    /// <summary>Gets or sets the simulated combat state.</summary>
    public bool IsInCombat { get; set; }

    /// <summary>Gets or sets the simulated current zone identifier.</summary>
    public string? CurrentZoneId { get; set; }

    /// <summary>Gets or sets the simulated current zone name.</summary>
    public string? CurrentZoneName { get; set; }

    /// <summary>Gets or sets the simulated ZonTileMap.</summary>
    public object? ZoneTileMap { get; set; }

    /// <summary>Gets or sets the simulated current character identifier.</summary>
    public string? CurrentCharacterId { get; set; }

    /// <summary>Gets or sets the simulated current character name.</summary>
    public string? CurrentCharacterName { get; set; }

    /// <summary>Gets or sets the simulated current character level.</summary>
    public int CurrentCharacterLevel { get; set; }

    /// <inheritdoc />
    public int CurrentCharacterGold { get; set; }

    /// <inheritdoc />
    public IReadOnlyList<Item> InventoryItems { get; set; } = [];

    /// <inheritdoc />
    public IReadOnlyDictionary<string, Item> EquippedItems { get; set; } = new Dictionary<string, Item>();

    /// <inheritdoc />
    public IReadOnlyList<ShopItemEntry> ShopCatalog { get; set; } = [];

    /// <summary>Gets or sets the simulated server connection identifier.</summary>
    public string? ServerConnectionId { get; set; }

    // ── Live character state ──────────────────────────────────────────────────

    /// <inheritdoc />
    public CharacterState CurrentCharacter { get; set; } = new();

    // ── Kick state ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public bool IsKicked { get; set; }

    /// <inheritdoc />
    public string? KickReason { get; set; }

    // ── Shop state ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public bool InShop { get; set; }

    /// <inheritdoc />
    public string? ShopName { get; set; }

    // ── Dungeon state ─────────────────────────────────────────────────────────

    /// <inheritdoc />
    public string? CurrentDungeonId { get; set; }

    // ── Call tracking ─────────────────────────────────────────────────────────

    /// <summary>Records all Apply* method calls for assertion.</summary>
    public List<(string Method, object? Payload)> AppliedCalls { get; } = [];

    /// <summary>Clears the applied calls log.</summary>
    public void ClearCalls() => AppliedCalls.Clear();

    // ── IGameStateService Apply methods ───────────────────────────────────────

    /// <inheritdoc />
    public void ApplyCharacterSelected(CharacterSelectedPayload payload)
    {
        CurrentCharacterId = payload.Id.ToString();
        CurrentCharacterName = payload.Name;
        CurrentCharacterLevel = payload.Level;
        CurrentZoneId = payload.CurrentZoneId;
        IsConnected = true;
        AppliedCalls.Add((nameof(ApplyCharacterSelected), payload));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCharacterId)));
    }

    /// <inheritdoc />
    public void ApplyZoneEntered(ZoneEnteredPayload payload)
    {
        CurrentZoneId = payload.Id;
        CurrentZoneName = payload.Name;
        AppliedCalls.Add((nameof(ApplyZoneEntered), payload));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentZoneId)));
    }

    /// <inheritdoc />
    public void ApplyCombatStarted(CombatStartedPayload payload)
    {
        IsInCombat = true;
        AppliedCalls.Add((nameof(ApplyCombatStarted), payload));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsInCombat)));
    }

    /// <inheritdoc />
    public void ApplyCombatTurn(CombatTurnPayload payload)
    {
        AppliedCalls.Add((nameof(ApplyCombatTurn), payload));
    }

    /// <inheritdoc />
    public void ApplyCombatEnded(CombatEndedPayload payload)
    {
        IsInCombat = false;
        AppliedCalls.Add((nameof(ApplyCombatEnded), payload));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsInCombat)));
    }

    /// <inheritdoc />
    public void ApplyChatMessage(ChatMessageHubDto payload)
    {
        AppliedCalls.Add((nameof(ApplyChatMessage), payload));
    }

    /// <inheritdoc />
    public void ApplyPlayerEntered(PlayerEnteredPayload payload)
    {
        AppliedCalls.Add((nameof(ApplyPlayerEntered), payload));
    }

    /// <inheritdoc />
    public void ApplyPlayerLeft(PlayerLeftPayload payload)
    {
        AppliedCalls.Add((nameof(ApplyPlayerLeft), payload));
    }

    /// <inheritdoc />
    public void ApplyCharacterMoved(Veldrath.Contracts.Tilemap.CharacterMovedPayload payload)
    {
        AppliedCalls.Add((nameof(ApplyCharacterMoved), payload));
    }

    /// <inheritdoc />
    public void ApplyZoneEntitiesSnapshot(Veldrath.Contracts.Tilemap.ZoneEntitiesSnapshotPayload payload)
    {
        AppliedCalls.Add((nameof(ApplyZoneEntitiesSnapshot), payload));
    }

    /// <inheritdoc />
    public void ApplyEnemyDefeated(EnemyDefeatedPayload payload)
    {
        AppliedCalls.Add((nameof(ApplyEnemyDefeated), payload));
    }

    /// <inheritdoc />
    public void ApplyInventoryUpdated(IReadOnlyList<Item> items, IReadOnlyDictionary<string, Item> equipped)
    {
        InventoryItems = items;
        EquippedItems = equipped;
        AppliedCalls.Add((nameof(ApplyInventoryUpdated), (items, equipped)));
    }

    /// <inheritdoc />
    public void ApplyEquipmentChanged(string slot, Item? item)
    {
        var dict = new Dictionary<string, Item>(EquippedItems);
        if (item is null)
            dict.Remove(slot);
        else
            dict[slot] = item;
        EquippedItems = dict;
        AppliedCalls.Add((nameof(ApplyEquipmentChanged), (slot, item)));
    }

    /// <inheritdoc />
    public void ApplyShopCatalogUpdated(IReadOnlyList<ShopItemEntry> catalog)
    {
        ShopCatalog = catalog;
        AppliedCalls.Add((nameof(ApplyShopCatalogUpdated), catalog));
    }

    // ── Quest log ───────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public IReadOnlyList<QuestLogEntry> QuestLog { get; set; } = [];

    /// <inheritdoc />
    public IReadOnlyList<QuestLogEntry> CompletedQuests { get; set; } = [];

    /// <inheritdoc />
    public void ApplyQuestLogUpdated(IReadOnlyList<QuestLogEntry> active, IReadOnlyList<QuestLogEntry> completed)
    {
        QuestLog = active;
        CompletedQuests = completed;
        AppliedCalls.Add((nameof(ApplyQuestLogUpdated), (active, completed)));
    }

    // ── Settings ────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public GameSettingsState Settings { get; set; } = new();

    /// <inheritdoc />
    public void ApplySettings(GameSettingsState settings)
    {
        Settings = settings;
        AppliedCalls.Add((nameof(ApplySettings), settings));
    }

    // ── Kick ────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public void ApplyKicked(string reason)
    {
        IsKicked = true;
        KickReason = reason;
        IsConnected = false;
        AppliedCalls.Add((nameof(ApplyKicked), reason));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsKicked)));
    }

    // ── Progression ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public void ApplyExperienceGained(int newLevel, int newXP, int newLeveledUpTo)
    {
        CurrentCharacterLevel = newLevel;
        CurrentCharacter = CurrentCharacter with { Level = newLevel, XP = newXP };
        AppliedCalls.Add((nameof(ApplyExperienceGained), (newLevel, newXP, newLeveledUpTo)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCharacterLevel)));
    }

    /// <inheritdoc />
    public void ApplyGoldChanged(int goldAdded, int newGoldTotal)
    {
        CurrentCharacterGold = newGoldTotal;
        CurrentCharacter = CurrentCharacter with { Gold = newGoldTotal };
        AppliedCalls.Add((nameof(ApplyGoldChanged), (goldAdded, newGoldTotal)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCharacterGold)));
    }

    /// <inheritdoc />
    public void ApplyDamageTaken(int damage, int currentHP, int maxHP, bool isDead)
    {
        CurrentCharacter = CurrentCharacter with
        {
            CurrentHealth = currentHP,
            MaxHealth = maxHP,
            IsDead = isDead,
        };
        AppliedCalls.Add((nameof(ApplyDamageTaken), (damage, currentHP, maxHP, isDead)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCharacter)));
    }

    // ── Shop visit ──────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public void ApplyShopVisited(string zoneId, string zoneName)
    {
        InShop = true;
        ShopName = zoneName;
        AppliedCalls.Add((nameof(ApplyShopVisited), (zoneId, zoneName)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InShop)));
    }

    // ── Inventory transaction ───────────────────────────────────────────────────

    /// <inheritdoc />
    public void ApplyItemTransacted(string itemName, int newGold, List<Item> inventory)
    {
        CurrentCharacterGold = newGold;
        InventoryItems = inventory;
        CurrentCharacter = CurrentCharacter with { Gold = newGold };
        AppliedCalls.Add((nameof(ApplyItemTransacted), (itemName, newGold, inventory)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCharacterGold)));
    }

    // ── Attributes ──────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public void ApplyAttributePointsAllocated(int remaining, Dictionary<string, int> stats)
    {
        CurrentCharacter = CurrentCharacter with
        {
            UnspentAttributePoints = remaining,
            Strength = stats.GetValueOrDefault("Strength", CurrentCharacter.Strength),
            Dexterity = stats.GetValueOrDefault("Dexterity", CurrentCharacter.Dexterity),
            Constitution = stats.GetValueOrDefault("Constitution", CurrentCharacter.Constitution),
            Intelligence = stats.GetValueOrDefault("Intelligence", CurrentCharacter.Intelligence),
            Wisdom = stats.GetValueOrDefault("Wisdom", CurrentCharacter.Wisdom),
            Charisma = stats.GetValueOrDefault("Charisma", CurrentCharacter.Charisma),
        };
        AppliedCalls.Add((nameof(ApplyAttributePointsAllocated), (remaining, stats)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCharacter)));
    }

    // ── Rest ────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public void ApplyCharacterRested(int hp, int maxHp, int mp, int maxMp, int gold)
    {
        CurrentCharacter = CurrentCharacter with
        {
            CurrentHealth = hp,
            MaxHealth = maxHp,
            CurrentMana = mp,
            MaxMana = maxMp,
            Gold = gold,
        };
        CurrentCharacterGold = gold;
        AppliedCalls.Add((nameof(ApplyCharacterRested), (hp, maxHp, mp, maxMp, gold)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCharacter)));
    }

    // ── Ability ─────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public void ApplyAbilityUsed(string abilityId, int remainingMana, int hpRestored)
    {
        CurrentCharacter = CurrentCharacter with { CurrentMana = remainingMana };
        if (hpRestored > 0)
        {
            var newHp = Math.Min(CurrentCharacter.CurrentHealth + hpRestored, CurrentCharacter.MaxHealth);
            CurrentCharacter = CurrentCharacter with { CurrentHealth = newHp };
        }
        AppliedCalls.Add((nameof(ApplyAbilityUsed), (abilityId, remainingMana, hpRestored)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCharacter)));
    }

    // ── Skill XP ────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public void ApplySkillXpGained(string skillId, int xpGained, int newRank, bool rankedUp)
    {
        AppliedCalls.Add((nameof(ApplySkillXpGained), (skillId, xpGained, newRank, rankedUp)));
    }

    // ── Crafting ────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public void ApplyItemCrafted(string recipeName, int goldSpent, int remainingGold)
    {
        CurrentCharacterGold = remainingGold;
        CurrentCharacter = CurrentCharacter with { Gold = remainingGold };
        AppliedCalls.Add((nameof(ApplyItemCrafted), (recipeName, goldSpent, remainingGold)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCharacterGold)));
    }

    // ── Dungeon ─────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public void ApplyDungeonEntered(string dungeonId)
    {
        CurrentDungeonId = dungeonId;
        AppliedCalls.Add((nameof(ApplyDungeonEntered), dungeonId));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentDungeonId)));
    }
}
