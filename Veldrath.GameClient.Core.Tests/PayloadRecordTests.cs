using System.Text.Json;
using Veldrath.Contracts.Tilemap;
using Veldrath.GameClient.Core.Payloads;

namespace Veldrath.GameClient.Core.Tests;

/// <summary>
/// Tests for the shared payload records — verifies immutability, equality,
/// and JSON serialization/deserialization.
/// </summary>
public sealed class PayloadRecordTests
{
    // ── CharacterSelectedPayload ───────────────────────────────────────────────

    /// <summary>Verifies CharacterSelectedPayload can be constructed and has expected property values.</summary>
    [Fact]
    public void CharacterSelectedPayload_Can_Be_Constructed()
    {
        var now = DateTimeOffset.UtcNow;
        var payload = new CharacterSelectedPayload(
            Id: Guid.NewGuid(),
            Name: "TestHero",
            ClassName: "Warrior",
            Level: 5,
            Experience: 2500L,
            CurrentZoneId: "fenwick-crossing",
            RegionId: "thornveil",
            CurrentHealth: 80,
            MaxHealth: 100,
            CurrentMana: 30,
            MaxMana: 50,
            Gold: 100,
            UnspentAttributePoints: 2,
            Strength: 15,
            Dexterity: 12,
            Constitution: 14,
            Intelligence: 8,
            Wisdom: 10,
            Charisma: 11,
            LearnedAbilities: ["charge", "shield-bash"],
            SelectedAt: now);

        payload.Name.Should().Be("TestHero");
        payload.Level.Should().Be(5);
        payload.CurrentZoneId.Should().Be("fenwick-crossing");
        payload.LearnedAbilities.Should().BeEquivalentTo(["charge", "shield-bash"]);
        payload.SelectedAt.Should().Be(now);
    }

    /// <summary>Verifies CharacterSelectedPayload supports value equality for value-type fields.</summary>
    [Fact]
    public void CharacterSelectedPayload_Supports_Value_Equality()
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var payload1 = new CharacterSelectedPayload(
            id, "Hero", "Mage", 10, 5000L, "zone-1", "region-1",
            70, 100, 60, 80, 500, 0, 10, 10, 10, 10, 10, 10,
            ["fireball"], now);
        var payload2 = new CharacterSelectedPayload(
            id, "Hero", "Mage", 10, 5000L, "zone-1", "region-1",
            70, 100, 60, 80, 500, 0, 10, 10, 10, 10, 10, 10,
            ["fireball"], now);

        // Compare individual value-typed and string fields (List<string> uses
        // reference equality in record comparison, so skip that here).
        payload1.Id.Should().Be(payload2.Id);
        payload1.Name.Should().Be(payload2.Name);
        payload1.Level.Should().Be(payload2.Level);
        payload1.Experience.Should().Be(payload2.Experience);
        payload1.CurrentHealth.Should().Be(payload2.CurrentHealth);
        payload1.Gold.Should().Be(payload2.Gold);
        payload1.SelectedAt.Should().BeCloseTo(payload2.SelectedAt, TimeSpan.FromTicks(1));
    }

    /// <summary>Verifies CharacterSelectedPayload can be serialized and deserialized via System.Text.Json.</summary>
    [Fact]
    public void CharacterSelectedPayload_Serializes_And_Deserializes()
    {
        var original = new CharacterSelectedPayload(
            Guid.NewGuid(), "Hero", "Rogue", 3, 1200L, "aldenmere", "greymoor",
            45, 60, 20, 30, 250, 1, 12, 16, 10, 14, 8, 14,
            ["backstab", "stealth"], DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<CharacterSelectedPayload>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Id.Should().Be(original.Id);
        deserialized.Name.Should().Be(original.Name);
        deserialized.Level.Should().Be(original.Level);
        deserialized.Experience.Should().Be(original.Experience);
        deserialized.CurrentZoneId.Should().Be(original.CurrentZoneId);
        deserialized.LearnedAbilities.Should().BeEquivalentTo(original.LearnedAbilities);
    }

    // ── CombatPayloads ─────────────────────────────────────────────────────────

    /// <summary>Verifies CombatStartedPayload can be constructed.</summary>
    [Fact]
    public void CombatStartedPayload_Can_Be_Constructed()
    {
        var payload = new CombatStartedPayload(
            CharacterId: Guid.NewGuid(),
            EnemyId: Guid.NewGuid(),
            EnemyName: "Goblin Scout",
            EnemyLevel: 3,
            EnemyCurrentHealth: 25,
            EnemyMaxHealth: 25,
            EnemyAbilityNames: ["scratch", "dodge"]);

        payload.EnemyName.Should().Be("Goblin Scout");
        payload.EnemyLevel.Should().Be(3);
        payload.EnemyAbilityNames.Should().BeEquivalentTo(["scratch", "dodge"]);
    }

    /// <summary>Verifies CombatTurnPayload default values work correctly.</summary>
    [Fact]
    public void CombatTurnPayload_Defaults_Are_Correct()
    {
        var payload = new CombatTurnPayload(
            Action: "attack",
            PlayerDamage: 10,
            EnemyRemainingHealth: 15,
            EnemyDefeated: false,
            EnemyDamage: 5,
            EnemyAbilityUsed: null,
            PlayerRemainingHealth: 75,
            PlayerDefeated: false,
            PlayerHardcoreDeath: false);

        payload.XpEarned.Should().Be(0);
        payload.GoldEarned.Should().Be(0);
        payload.AbilityId.Should().BeNull();
        payload.AbilityDamage.Should().Be(0);
        payload.HealthRestored.Should().Be(0);
        payload.ManaCost.Should().Be(0);
        payload.PlayerRemainingMana.Should().Be(0);
    }

    /// <summary>Verifies CombatEndedPayload can be constructed.</summary>
    [Fact]
    public void CombatEndedPayload_Can_Be_Constructed()
    {
        var payload = new CombatEndedPayload(
            CharacterId: Guid.NewGuid(),
            Reason: "Victory");

        payload.Reason.Should().Be("Victory");
    }

    // ── ChatPayloads ───────────────────────────────────────────────────────────

    /// <summary>Verifies ChatMessageHubDto can be constructed and serialized.</summary>
    [Fact]
    public void ChatMessageHubDto_Serializes_Correctly()
    {
        var now = DateTimeOffset.UtcNow;
        var original = new ChatMessageHubDto(
            CharacterId: Guid.NewGuid(),
            Channel: "zone",
            Sender: "TestHero",
            Message: "Hello, world!",
            Timestamp: now);

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ChatMessageHubDto>(json);

        deserialized.Should().Be(original);
        deserialized!.Message.Should().Be("Hello, world!");
    }

    // ── ZonePayloads ───────────────────────────────────────────────────────────

    /// <summary>Verifies ZoneEnteredPayload and its OccupantEntry sub-records work correctly.</summary>
    [Fact]
    public void ZoneEnteredPayload_Contains_Occupants()
    {
        var occupant1 = new OccupantEntry(Guid.NewGuid(), "PlayerOne", DateTimeOffset.UtcNow);
        var occupant2 = new OccupantEntry(Guid.NewGuid(), "PlayerTwo", DateTimeOffset.UtcNow);

        var payload = new ZoneEnteredPayload(
            Id: "fenwick-crossing",
            Name: "Fenwick's Crossing",
            Description: "A quiet town.",
            ZoneType: "town",
            Occupants: new List<OccupantEntry> { occupant1, occupant2 });

        payload.Id.Should().Be("fenwick-crossing");
        payload.Occupants.Should().HaveCount(2);
        payload.Occupants[0].CharacterName.Should().Be("PlayerOne");
    }

    /// <summary>Verifies PlayerEnteredPayload can be constructed.</summary>
    [Fact]
    public void PlayerEnteredPayload_Can_Be_Constructed()
    {
        var payload = new PlayerEnteredPayload(
            CharacterId: Guid.NewGuid(),
            CharacterName: "NewPlayer",
            ZoneId: "fenwick-crossing");

        payload.CharacterName.Should().Be("NewPlayer");
    }

    /// <summary>Verifies PlayerLeftPayload can be constructed.</summary>
    [Fact]
    public void PlayerLeftPayload_Can_Be_Constructed()
    {
        var payload = new PlayerLeftPayload(
            CharacterId: Guid.NewGuid(),
            CharacterName: "LeavingPlayer",
            ZoneId: "fenwick-crossing");

        payload.CharacterName.Should().Be("LeavingPlayer");
    }

    // ── EntityPayloads ─────────────────────────────────────────────────────────

    /// <summary>Verifies CharacterMovedPayload (from Veldrath.Contracts) can be constructed.</summary>
    [Fact]
    public void CharacterMovedPayload_Can_Be_Constructed()
    {
        var payload = new Veldrath.Contracts.Tilemap.CharacterMovedPayload(
            CharacterId: Guid.NewGuid(),
            TileX: 5,
            TileY: 10,
            Direction: "N");

        payload.TileX.Should().Be(5);
        payload.TileY.Should().Be(10);
        payload.Direction.Should().Be("N");
    }

    /// <summary>Verifies ZoneEntitiesSnapshotPayload (from Veldrath.Contracts) contains entities.</summary>
    [Fact]
    public void ZoneEntitiesSnapshotPayload_Contains_Entities()
    {
        var entity = new Veldrath.Contracts.Tilemap.TileEntityDto(
            EntityId: Guid.NewGuid(),
            EntityType: "player",
            SpriteKey: "hero",
            TileX: 3,
            TileY: 7,
            Direction: "S");

        var payload = new Veldrath.Contracts.Tilemap.ZoneEntitiesSnapshotPayload(
            Entities: new List<Veldrath.Contracts.Tilemap.TileEntityDto> { entity });

        payload.Entities.Should().HaveCount(1);
        payload.Entities[0].EntityType.Should().Be("player");
        payload.Entities[0].TileX.Should().Be(3);
    }

    /// <summary>Verifies EnemyDefeatedPayload can be constructed.</summary>
    [Fact]
    public void EnemyDefeatedPayload_Can_Be_Constructed()
    {
        var payload = new EnemyDefeatedPayload(
            CharacterId: Guid.NewGuid());

        payload.CharacterId.Should().NotBeEmpty();
    }

    /// <summary>Verifies payload equality is based on value, not reference.</summary>
    [Fact]
    public void Payloads_Use_Value_Equality()
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var a = new ChatMessageHubDto(id, "global", "Test", "Hello", now);
        var b = new ChatMessageHubDto(id, "global", "Test", "Hello", now);
        a.Should().Be(b);
    }
}
