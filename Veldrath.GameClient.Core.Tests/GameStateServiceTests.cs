using Veldrath.GameClient.Core.Abstractions;
using Veldrath.GameClient.Core.Payloads;
using Veldrath.GameClient.Core.Tests.Infrastructure;

namespace Veldrath.GameClient.Core.Tests;

/// <summary>
/// Tests for the <see cref="IGameStateService"/> contract using
/// <see cref="FakeGameStateService"/> as the test double.
/// </summary>
public sealed class GameStateServiceTests
{
    // ── Character selection ────────────────────────────────────────────────────

    /// <summary>Verifies ApplyCharacterSelected updates the character state.</summary>
    [Fact]
    public void ApplyCharacterSelected_Sets_Character_State()
    {
        var state = new FakeGameStateService();

        var payload = new CharacterSelectedPayload(
            Guid.NewGuid(), "TestHero", "Warrior", 5, 2500L,
            "fenwick-crossing", "thornveil", 80, 100, 30, 50, 100, 2,
            15, 12, 14, 8, 10, 11, ["charge"], DateTimeOffset.UtcNow);

        state.ApplyCharacterSelected(payload);

        state.CurrentCharacterId.Should().Be(payload.Id.ToString());
        state.CurrentCharacterName.Should().Be("TestHero");
        state.CurrentCharacterLevel.Should().Be(5);
        state.AppliedCalls.Should().ContainSingle(c => c.Method == nameof(IGameStateService.ApplyCharacterSelected));
    }

    // ── Zone entry ─────────────────────────────────────────────────────────────

    /// <summary>Verifies ApplyZoneEntered updates the zone state.</summary>
    [Fact]
    public void ApplyZoneEntered_Sets_Zone_State()
    {
        var state = new FakeGameStateService();
        var payload = new ZoneEnteredPayload(
            "fenwick-crossing", "Fenwick's Crossing", "A quiet town.", "town", []);

        state.ApplyZoneEntered(payload);

        state.CurrentZoneId.Should().Be("fenwick-crossing");
        state.CurrentZoneName.Should().Be("Fenwick's Crossing");
        state.AppliedCalls.Should().ContainSingle(c => c.Method == nameof(IGameStateService.ApplyZoneEntered));
    }

    // ── Combat state ───────────────────────────────────────────────────────────

    /// <summary>Verifies ApplyCombatStarted sets IsInCombat to true.</summary>
    [Fact]
    public void ApplyCombatStarted_Sets_IsInCombat()
    {
        var state = new FakeGameStateService();
        var payload = new CombatStartedPayload(
            Guid.NewGuid(), Guid.NewGuid(), "Goblin", 3, 25, 25, ["scratch"]);

        state.ApplyCombatStarted(payload);

        state.IsInCombat.Should().BeTrue();
        state.AppliedCalls.Should().ContainSingle(c => c.Method == nameof(IGameStateService.ApplyCombatStarted));
    }

    /// <summary>Verifies ApplyCombatEnded sets IsInCombat to false.</summary>
    [Fact]
    public void ApplyCombatEnded_Clears_IsInCombat()
    {
        var state = new FakeGameStateService();
        state.IsInCombat = true;

        state.ApplyCombatEnded(new CombatEndedPayload(Guid.NewGuid(), "Victory"));

        state.IsInCombat.Should().BeFalse();
        state.AppliedCalls.Should().ContainSingle(c => c.Method == nameof(IGameStateService.ApplyCombatEnded));
    }

    /// <summary>Verifies ApplyCombatTurn records the turn result.</summary>
    [Fact]
    public void ApplyCombatTurn_Records_Call()
    {
        var state = new FakeGameStateService();
        var payload = new CombatTurnPayload(
            "attack", 10, 15, false, 5, null, 75, false, false,
            XpEarned: 20, GoldEarned: 5);

        state.ApplyCombatTurn(payload);

        state.AppliedCalls.Should().ContainSingle(c => c.Method == nameof(IGameStateService.ApplyCombatTurn));
    }

    // ── Chat ───────────────────────────────────────────────────────────────────

    /// <summary>Verifies ApplyChatMessage records the call.</summary>
    [Fact]
    public void ApplyChatMessage_Records_Call()
    {
        var state = new FakeGameStateService();
        var payload = new ChatMessageHubDto(
            Guid.NewGuid(), "zone", "TestHero", "Hello!", DateTimeOffset.UtcNow);

        state.ApplyChatMessage(payload);

        state.AppliedCalls.Should().ContainSingle(c => c.Method == nameof(IGameStateService.ApplyChatMessage));
    }

    // ── Player enter/leave ─────────────────────────────────────────────────────

    /// <summary>Verifies ApplyPlayerEntered records the call.</summary>
    [Fact]
    public void ApplyPlayerEntered_Records_Call()
    {
        var state = new FakeGameStateService();
        var payload = new PlayerEnteredPayload(Guid.NewGuid(), "NewPlayer", "fenwick-crossing");

        state.ApplyPlayerEntered(payload);

        state.AppliedCalls.Should().ContainSingle(c => c.Method == nameof(IGameStateService.ApplyPlayerEntered));
    }

    /// <summary>Verifies ApplyPlayerLeft records the call.</summary>
    [Fact]
    public void ApplyPlayerLeft_Records_Call()
    {
        var state = new FakeGameStateService();
        var payload = new PlayerLeftPayload(Guid.NewGuid(), "LeavingPlayer", "fenwick-crossing");

        state.ApplyPlayerLeft(payload);

        state.AppliedCalls.Should().ContainSingle(c => c.Method == nameof(IGameStateService.ApplyPlayerLeft));
    }

    // ── Movement ───────────────────────────────────────────────────────────────

    /// <summary>Verifies ApplyCharacterMoved records the call.</summary>
    [Fact]
    public void ApplyCharacterMoved_Records_Call()
    {
        var state = new FakeGameStateService();
        var payload = new Veldrath.Contracts.Tilemap.CharacterMovedPayload(Guid.NewGuid(), 5, 10, "N");

        state.ApplyCharacterMoved(payload);

        state.AppliedCalls.Should().ContainSingle(c => c.Method == nameof(IGameStateService.ApplyCharacterMoved));
    }

    // ── Enemy defeated ─────────────────────────────────────────────────────────

    /// <summary>Verifies ApplyEnemyDefeated records the call.</summary>
    [Fact]
    public void ApplyEnemyDefeated_Records_Call()
    {
        var state = new FakeGameStateService();
        var payload = new EnemyDefeatedPayload(Guid.NewGuid());

        state.ApplyEnemyDefeated(payload);

        state.AppliedCalls.Should().ContainSingle(c => c.Method == nameof(IGameStateService.ApplyEnemyDefeated));
    }

    // ── INotifyPropertyChanged ─────────────────────────────────────────────────

    /// <summary>Verifies property change events fire when state changes.</summary>
    [Fact]
    public void ApplyCharacterSelected_Fires_PropertyChanged()
    {
        var state = new FakeGameStateService();
        var changedProperties = new List<string>();
        state.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        var payload = new CharacterSelectedPayload(
            Guid.NewGuid(), "Hero", "Mage", 10, 5000L,
            "zone-1", "region-1", 70, 100, 60, 80, 500, 0,
            10, 10, 10, 10, 10, 10, ["fireball"], DateTimeOffset.UtcNow);

        state.ApplyCharacterSelected(payload);

        changedProperties.Should().Contain(nameof(IGameStateService.CurrentCharacterId));
    }

    // ── Multiple Apply calls ───────────────────────────────────────────────────

    /// <summary>Verifies multiple Apply calls are tracked in order.</summary>
    [Fact]
    public void Multiple_Apply_Calls_Are_Ordered()
    {
        var state = new FakeGameStateService();

        state.ApplyCharacterSelected(new CharacterSelectedPayload(
            Guid.NewGuid(), "Hero", "Mage", 1, 0L,
            null, "region-1", 100, 100, 50, 50, 0, 0,
            10, 10, 10, 10, 10, 10, [], DateTimeOffset.UtcNow));

        state.ApplyZoneEntered(new ZoneEnteredPayload(
            "zone-1", "Zone One", "Desc", "town", []));

        state.AppliedCalls.Should().HaveCount(2);
        state.AppliedCalls[0].Method.Should().Be(nameof(IGameStateService.ApplyCharacterSelected));
        state.AppliedCalls[1].Method.Should().Be(nameof(IGameStateService.ApplyZoneEntered));
    }

    // ── ZoneEntitiesSnapshot ───────────────────────────────────────────────────

    /// <summary>Verifies ApplyZoneEntitiesSnapshot records the call.</summary>
    [Fact]
    public void ApplyZoneEntitiesSnapshot_Records_Call()
    {
        var state = new FakeGameStateService();
        var payload = new Veldrath.Contracts.Tilemap.ZoneEntitiesSnapshotPayload([
            new Veldrath.Contracts.Tilemap.TileEntityDto(Guid.NewGuid(), "player", "hero", 3, 7, "S")
        ]);

        state.ApplyZoneEntitiesSnapshot(payload);

        state.AppliedCalls.Should().ContainSingle(c => c.Method == nameof(IGameStateService.ApplyZoneEntitiesSnapshot));
    }
}
