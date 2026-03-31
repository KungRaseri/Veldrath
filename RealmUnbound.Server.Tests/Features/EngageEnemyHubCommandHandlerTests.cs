using Microsoft.Extensions.Logging.Abstractions;
using RealmUnbound.Server.Features.Characters.Combat;

namespace RealmUnbound.Server.Tests.Features;

/// <summary>Unit tests for <see cref="EngageEnemyHubCommandHandler"/>.</summary>
public class EngageEnemyHubCommandHandlerTests : IDisposable
{
    private readonly EngageEnemyHubCommandHandler _handler =
        new(NullLogger<EngageEnemyHubCommandHandler>.Instance);

    private readonly List<(string Key, Guid EnemyId)> _enemyCleanup  = [];
    private readonly List<Guid>                        _sessionCleanup = [];

    public void Dispose()
    {
        foreach (var (key, id) in _enemyCleanup)  ZoneLocationEnemyStore.RemoveEnemy(key, id);
        foreach (var id        in _sessionCleanup) CombatSessionStore.Remove(id);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private (string ZoneGroup, string LocationSlug, SpawnedEnemy Enemy) AddEnemy(int hp = 100)
    {
        var zoneGroup    = $"test/{Guid.NewGuid():N}";
        const string loc = "test-path";
        var key          = ZoneLocationEnemyStore.MakeKey(zoneGroup, loc);
        var enemy        = new SpawnedEnemy { Name = "Goblin", Level = 1, CurrentHealth = hp, MaxHealth = 100 };
        ZoneLocationEnemyStore.AddEnemy(key, enemy);
        _enemyCleanup.Add((key, enemy.Id));
        return (zoneGroup, loc, enemy);
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Returns_Error_When_Already_In_Combat()
    {
        var charId = Guid.NewGuid();
        CombatSessionStore.Set(charId, new ActiveCombatSession("z", "l", Guid.NewGuid(), false, 0, DateTimeOffset.UtcNow));
        _sessionCleanup.Add(charId);

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
        _sessionCleanup.Add(charId);

        var result = await _handler.Handle(
            new EngageEnemyHubCommand(charId, zoneGroup, loc, enemy.Id),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.EnemyId.Should().Be(enemy.Id);
        result.EnemyName.Should().Be("Goblin");
        result.EnemyCurrentHealth.Should().Be(100);
        CombatSessionStore.IsInCombat(charId).Should().BeTrue();
        enemy.Participants.Should().Contain(charId);
    }
}
