using Microsoft.Extensions.Logging.Abstractions;
using Veldrath.Server.Features.Characters.Combat;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>Unit tests for <see cref="EngageEnemyHubCommandHandler"/>.</summary>
public class EngageEnemyHubCommandHandlerTests : IDisposable
{
    private readonly FakeEnemyInstanceStore _enemies = new();
    private readonly FakeCombatSessionStore _sessions = new();
    private readonly FakePartyCombatQuery _party = new();
    private readonly EngageEnemyHubCommandHandler _handler;

    public EngageEnemyHubCommandHandlerTests()
    {
        _handler = new(_enemies, _sessions, _party, NullLogger<EngageEnemyHubCommandHandler>.Instance);
    }

    public void Dispose()
    {
        // The fakes are per-test instances, so there is nothing to clean up.
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private (string ZoneGroup, string LocationSlug, SpawnedEnemy Enemy) AddEnemy(int hp = 100)
    {
        var zoneGroup = $"test/{Guid.NewGuid():N}";
        const string loc = "test-path";
        var enemy = new SpawnedEnemy { Name = "Goblin", Level = 1, CurrentHealth = hp, MaxHealth = 100 };
        _enemies.Add(zoneGroup, loc, enemy);
        return (zoneGroup, loc, enemy);
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Returns_Error_When_Already_In_Combat()
    {
        var charId = Guid.NewGuid();
        _sessions.Set(charId, new ActiveCombatSession("z", "l", Guid.NewGuid(), false, 0, DateTimeOffset.UtcNow));

        var result = await _handler.Handle(
            new EngageEnemyHubCommand(charId, "z", "l", Guid.NewGuid()),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Already in combat");
    }

    [Fact]
    public async Task Handle_Returns_Error_When_Enemy_Not_Found()
    {
        var charId = Guid.NewGuid();

        var result = await _handler.Handle(
            new EngageEnemyHubCommand(charId, $"zone/{Guid.NewGuid():N}", "nowhere", Guid.NewGuid()),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_Returns_Error_When_Enemy_Is_Dead()
    {
        var (zoneGroup, loc, enemy) = AddEnemy(hp: 0);
        var charId = Guid.NewGuid();

        var result = await _handler.Handle(
            new EngageEnemyHubCommand(charId, zoneGroup, loc, enemy.Id),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("already dead");
    }

    [Fact]
    public async Task Handle_Returns_Success_And_Registers_Session()
    {
        var (zoneGroup, loc, enemy) = AddEnemy();
        var charId = Guid.NewGuid();

        var result = await _handler.Handle(
            new EngageEnemyHubCommand(charId, zoneGroup, loc, enemy.Id),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.EnemyId.Should().Be(enemy.Id);
        result.EnemyName.Should().Be("Goblin");
        result.EnemyCurrentHealth.Should().Be(100);
        _sessions.IsInCombat(charId).Should().BeTrue();
        enemy.Participants.Should().Contain(charId);
        result.PartyMemberIds.Should().BeEmpty("a solo character drags nobody into the fight");
    }

    [Fact]
    public async Task Handle_Adds_Party_Members_Standing_At_The_Same_Location()
    {
        var (zoneGroup, loc, enemy) = AddEnemy();
        var charId = Guid.NewGuid();
        var allyId = Guid.NewGuid();
        _party.Members.Add(allyId);

        var result = await _handler.Handle(
            new EngageEnemyHubCommand(charId, zoneGroup, loc, enemy.Id),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.PartyMemberIds.Should().ContainSingle().Which.Should().Be(allyId);
        enemy.Participants.Should().BeEquivalentTo([charId, allyId]);
        _party.Queried.Should().ContainSingle().Which.Should().Be(charId);
    }

    [Fact]
    public async Task Handle_Does_Not_Force_A_Party_Member_Into_A_Turn_Loop()
    {
        var (zoneGroup, loc, enemy) = AddEnemy();
        var charId = Guid.NewGuid();
        var allyId = Guid.NewGuid();
        _party.Members.Add(allyId);

        await _handler.Handle(new EngageEnemyHubCommand(charId, zoneGroup, loc, enemy.Id), CancellationToken.None);

        // The ally is a participant — they share the kill and the quest progress — but they act only if they engage.
        _sessions.IsInCombat(allyId).Should().BeFalse();
    }
}
