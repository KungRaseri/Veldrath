using Veldrath.Client.Services;
using Veldrath.Client.Tests.Infrastructure;
using Veldrath.Client.ViewModels;

namespace Veldrath.Client.Tests.ViewModels;

/// <summary>Tilemap / entity-tracking unit tests for <see cref="GameViewModel"/>.</summary>
public class GameViewModelTilemapTests : TestBase
{
    private static GameViewModel MakeVm() =>
        new(new FakeServerConnectionService(), new FakeZoneService(), new TokenStore(), new FakeNavigationService());

    // ── OnEnemyMoved ─────────────────────────────────────────────────────────

    [Fact]
    public void OnEnemyMoved_UsesProvidedSpriteKey()
    {
        var vm       = MakeVm();
        var entityId = Guid.NewGuid();

        vm.OnEnemyMoved(entityId, "goblin-scout", 5, 5, "S");

        vm.Tilemap!.Entities.Should().ContainSingle()
            .Which.SpriteKey.Should().Be("goblin-scout");
    }

    [Fact]
    public void OnEnemyMoved_DoesNotHardcode_EnemyKey()
    {
        var vm = MakeVm();
        vm.OnEnemyMoved(Guid.NewGuid(), "bandit-ruffian", 3, 3, "S");

        vm.Tilemap!.Entities.Should().ContainSingle()
            .Which.SpriteKey.Should().NotBe("enemy");
    }

    [Fact]
    public void OnEnemyMoved_SpriteKey_Preserved_On_Subsequent_Move()
    {
        var vm       = MakeVm();
        var entityId = Guid.NewGuid();

        // Initial snapshot gives entity its correct sprite key
        vm.Tilemap!.UpsertEntity(entityId, "enemy", "goblin-scout", 4, 4, "S");

        // Enemy moves — sprite key must survive the update
        vm.OnEnemyMoved(entityId, "goblin-scout", 5, 4, "E");

        vm.Tilemap.Entities.Should().ContainSingle()
            .Which.SpriteKey.Should().Be("goblin-scout");
    }

    [Fact]
    public void OnEnemyMoved_UpdatesTilePositionAndDirection()
    {
        var vm       = MakeVm();
        var entityId = Guid.NewGuid();

        vm.OnEnemyMoved(entityId, "goblin-scout", 7, 3, "N");

        var entity = vm.Tilemap!.Entities.Should().ContainSingle().Subject;
        entity.TileX.Should().Be(7);
        entity.TileY.Should().Be(3);
        entity.Direction.Should().Be("N");
    }

    [Fact]
    public void OnEnemyMoved_EntityType_IsEnemy()
    {
        var vm = MakeVm();
        vm.OnEnemyMoved(Guid.NewGuid(), "bandit-ruffian", 2, 2, "E");

        vm.Tilemap!.Entities.Should().ContainSingle()
            .Which.EntityType.Should().Be("enemy");
    }

    [Fact]
    public void OnEnemyMoved_TracksTwoDistinctEnemies()
    {
        var vm  = MakeVm();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        vm.OnEnemyMoved(id1, "goblin-scout",   1, 1, "S");
        vm.OnEnemyMoved(id2, "bandit-ruffian", 2, 2, "N");

        vm.Tilemap!.Entities.Should().HaveCount(2);
        vm.Tilemap.Entities.Single(e => e.EntityId == id1).SpriteKey.Should().Be("goblin-scout");
        vm.Tilemap.Entities.Single(e => e.EntityId == id2).SpriteKey.Should().Be("bandit-ruffian");
    }

    [Fact]
    public void OnEnemyMoved_SecondCallUpdatesExistingEntity()
    {
        var vm       = MakeVm();
        var entityId = Guid.NewGuid();

        vm.OnEnemyMoved(entityId, "goblin-scout", 1, 1, "S");
        vm.OnEnemyMoved(entityId, "goblin-scout", 2, 1, "E");

        // Still only one entity — position updated, key unchanged
        vm.Tilemap!.Entities.Should().ContainSingle()
            .Which.Should().Match<TileEntityState>(e =>
                e.TileX == 2 && e.TileY == 1 &&
                e.Direction == "E" && e.SpriteKey == "goblin-scout");
    }
}
