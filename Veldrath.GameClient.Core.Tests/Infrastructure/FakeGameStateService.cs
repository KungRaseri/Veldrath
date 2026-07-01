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
}
