using RealmUnbound.Client.Services;
using RealmUnbound.Client.Tests.Infrastructure;
using RealmUnbound.Client.ViewModels;

namespace RealmUnbound.Client.Tests.ViewModels;

/// <summary>Combat-state unit tests for <see cref="GameViewModel"/>.</summary>
public class GameViewModelCombatTests : TestBase
{
    private static GameViewModel MakeVm() =>
        new(new FakeServerConnectionService(), new FakeZoneService(), new TokenStore(), new FakeNavigationService());

    // ── OnCombatStarted ──────────────────────────────────────────────────────

    [Fact]
    public void OnCombatStarted_Sets_Combat_State()
    {
        var vm     = MakeVm();
        var enemyId = Guid.NewGuid();

        vm.OnCombatStarted(enemyId, "Orc", 3, 80, 100, ["Slash", "Roar"]);

        vm.IsInCombat.Should().BeTrue();
        vm.IsPlayerDead.Should().BeFalse();
        vm.CombatEnemyId.Should().Be(enemyId);
        vm.CombatEnemyName.Should().Be("Orc");
        vm.CombatEnemyLevel.Should().Be(3);
        vm.CombatEnemyCurrentHealth.Should().Be(80);
        vm.CombatEnemyMaxHealth.Should().Be(100);
        vm.EnemyAbilityNames.Should().BeEquivalentTo("Slash", "Roar");
    }

    [Fact]
    public void OnCombatStarted_Clears_Previous_Ability_Names()
    {
        var vm = MakeVm();
        vm.OnCombatStarted(Guid.NewGuid(), "Wolf",  1, 20, 20, ["Bite"]);
        vm.OnCombatStarted(Guid.NewGuid(), "Golem", 5, 150, 150, ["Smash"]);

        vm.EnemyAbilityNames.Should().ContainSingle().Which.Should().Be("Smash");
    }

    // ── OnCombatTurn ────────────────────────────────────────────────────────

    [Fact]
    public void OnCombatTurn_Updates_Enemy_And_Player_Health()
    {
        var vm = MakeVm();
        vm.OnCombatStarted(Guid.NewGuid(), "Orc", 3, 80, 100, []);

        vm.OnCombatTurn("attack", 15, 0, 65, false, 10, null, 90, false, false, 0, 0);

        vm.CombatEnemyCurrentHealth.Should().Be(65);
        vm.CurrentHealth.Should().Be(90);
    }

    [Fact]
    public void OnCombatTurn_EnemyDefeated_Clears_IsInCombat_And_Sets_EnemyHealth_Zero()
    {
        var vm = MakeVm();
        vm.OnCombatStarted(Guid.NewGuid(), "Orc", 3, 5, 100, []);

        // Enemy defeated this turn (enemyDefeated = true).
        vm.OnCombatTurn("attack", 5, 0, 0, true, 0, null, 100, false, false, 50, 10);

        vm.IsInCombat.Should().BeFalse();
        vm.CombatEnemyCurrentHealth.Should().Be(0);
        vm.Experience.Should().Be(50);
        vm.Gold.Should().Be(10);
    }

    [Fact]
    public void OnCombatTurn_PlayerDefeated_Sets_IsPlayerDead_And_Clears_IsInCombat()
    {
        var vm = MakeVm();
        vm.OnCombatStarted(Guid.NewGuid(), "Golem", 10, 200, 200, []);

        // Player killed by counter-attack.
        vm.OnCombatTurn("attack", 5, 0, 195, false, 999, null, 0, true, false, 0, 0);

        vm.IsPlayerDead.Should().BeTrue();
        vm.IsInCombat.Should().BeFalse();
        vm.IsHardcoreDeath.Should().BeFalse();
    }

    [Fact]
    public void OnCombatTurn_HardcoreDeath_Sets_IsHardcoreDeath()
    {
        var vm = MakeVm();
        vm.OnCombatStarted(Guid.NewGuid(), "Dragon", 20, 500, 500, []);

        vm.OnCombatTurn("attack", 1, 0, 499, false, 999, null, 0, true, true, 0, 0);

        vm.IsPlayerDead.Should().BeTrue();
        vm.IsHardcoreDeath.Should().BeTrue();
        vm.IsInCombat.Should().BeFalse();
    }

    // ── OnCombatEnded ────────────────────────────────────────────────────────

    [Fact]
    public void OnCombatEnded_Clears_IsInCombat()
    {
        var vm = MakeVm();
        vm.OnCombatStarted(Guid.NewGuid(), "Wolf", 1, 20, 20, []);
        vm.IsInCombat.Should().BeTrue();

        vm.OnCombatEnded("fled");

        vm.IsInCombat.Should().BeFalse();
    }

    // ── OnEnemySpawned ───────────────────────────────────────────────────────

    [Fact]
    public void OnEnemySpawned_Adds_Enemy_To_SpawnedEnemies()
    {
        var vm      = MakeVm();
        var enemyId = Guid.NewGuid();

        vm.OnEnemySpawned(enemyId, "Goblin", 2, 30, 30);

        vm.SpawnedEnemies.Should().ContainSingle(e => e.Id == enemyId);
        var spawned = vm.SpawnedEnemies[0];
        spawned.Name.Should().Be("Goblin");
        spawned.Level.Should().Be(2);
        spawned.CurrentHealth.Should().Be(30);
        spawned.MaxHealth.Should().Be(30);
    }

    [Fact]
    public void OnEnemySpawned_Multiple_Enemies_All_Added()
    {
        var vm = MakeVm();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        vm.OnEnemySpawned(id1, "Goblin", 1, 20, 20);
        vm.OnEnemySpawned(id2, "Orc",    3, 50, 50);

        vm.SpawnedEnemies.Should().HaveCount(2);
        vm.SpawnedEnemies.Select(e => e.Id).Should().Contain([id1, id2]);
    }

    // ── Enemy defeat in SpawnedEnemies roster ────────────────────────────────

    [Fact]
    public void OnCombatTurn_EnemyDefeated_Zeroes_Health_In_SpawnedEnemies_Roster()
    {
        var vm      = MakeVm();
        var enemyId = Guid.NewGuid();

        // Enemy is in the roster.
        vm.SpawnedEnemies.Add(new SpawnedEnemyItemViewModel
        {
            Id = enemyId, Name = "Orc", Level = 1, CurrentHealth = 10, MaxHealth = 10,
        });
        vm.OnCombatStarted(enemyId, "Orc", 1, 10, 10, []);

        vm.OnCombatTurn("attack", 10, 0, 0, true, 0, null, 100, false, false, 20, 5);

        var rosterItem = vm.SpawnedEnemies.Single(e => e.Id == enemyId);
        rosterItem.CurrentHealth.Should().Be(0);
        rosterItem.IsAlive.Should().BeFalse();
    }
}
