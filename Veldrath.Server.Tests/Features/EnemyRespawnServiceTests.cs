using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Shared.Abstractions;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Features.Characters.Combat;
using Veldrath.Server.Hubs;
using Veldrath.Server.Settings;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Tests for the D3 respawn sweeper: the store-level sweep and prune, the hosted service that drives them, and the
/// options that configure them.
/// </summary>
public class EnemyRespawnServiceTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory = new();

    public void Dispose() => _dbFactory.Dispose();

    private static EfCoreEnemyInstanceStore CreateEnemyStore(ApplicationDbContext db, EnemyWorldOptions? options = null) =>
        new(
            db,
            new ActorPoolResolver(new EnemyGenerator(Mock.Of<IEnemyRepository>(), NullLogger<EnemyGenerator>.Instance)),
            Options.Create(options ?? new EnemyWorldOptions()),
            NullLogger<EfCoreEnemyInstanceStore>.Instance);

    private static SpawnedEnemy Enemy() => new()
    {
        ArchetypeSlug = "goblin",
        Name = "Goblin",
        Level = 1,
        CurrentHealth = 30,
        MaxHealth = 30,
        BaseXp = 5,
        GoldReward = 2,
    };

    private static async Task<Character> SeedCharacterAsync(ApplicationDbContext db)
    {
        var account = new PlayerAccount { UserName = $"u_{Guid.NewGuid():N}" };
        account.NormalizedUserName = account.UserName.ToUpperInvariant();
        db.Users.Add(account);

        var character = new Character
        {
            AccountId = account.Id,
            Name = $"Char_{Guid.NewGuid():N}",
            ClassName = "Warrior",
            SlotIndex = 1,
            Attributes = "{}",
        };
        db.Characters.Add(character);
        await db.SaveChangesAsync();
        return character;
    }

    private static async Task MakeDueAsync(ApplicationDbContext db, Guid enemyId)
    {
        var row = await db.EnemyInstances.SingleAsync(e => e.Id == enemyId);
        row.RespawnAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task SweepExpiredRespawnsAsync_Revives_Due_Enemies_And_Restores_Full_Health()
    {
        await using var db = _dbFactory.CreateContext();
        var store = CreateEnemyStore(db);
        var hero = await SeedCharacterAsync(db);

        var roster = await store.GetAliveOrSpawnAsync(
            "zone:test", "square", _ => Task.FromResult(new List<SpawnedEnemy> { Enemy() }));
        var enemy = roster[0];

        enemy.Participants.Add(hero.Id);
        enemy.CurrentHealth = 0;
        await store.SaveAsync(enemy, "zone:test", "square", CancellationToken.None);
        (await store.TryMarkDeadAsync(enemy.Id, CancellationToken.None)).Should().BeTrue();

        await MakeDueAsync(db, enemy.Id);

        var revived = await store.SweepExpiredRespawnsAsync(10, CancellationToken.None);

        revived.Should().HaveCount(1);
        revived[0].Id.Should().Be(enemy.Id);
        revived[0].ZoneGroup.Should().Be("zone:test");
        revived[0].LocationSlug.Should().Be("square");
        revived[0].Name.Should().Be("Goblin");
        revived[0].CurrentHealth.Should().Be(revived[0].MaxHealth, "a respawn reports full health");

        var alive = await store.GetAliveOrSpawnAsync("zone:test", "square", _ => Task.FromResult(new List<SpawnedEnemy>()));
        alive.Should().HaveCount(1);
        alive[0].CurrentHealth.Should().Be(30);
        alive[0].Participants.Should().BeEmpty("the sweeper clears participants on respawn");
    }

    [Fact]
    public async Task SweepExpiredRespawnsAsync_Ignores_Enemies_Whose_Timer_Has_Not_Passed()
    {
        await using var db = _dbFactory.CreateContext();
        var store = CreateEnemyStore(db);

        var roster = await store.GetAliveOrSpawnAsync(
            "zone:later", "square", _ => Task.FromResult(new List<SpawnedEnemy> { Enemy() }));

        roster[0].CurrentHealth = 0;
        await store.SaveAsync(roster[0], "zone:later", "square", CancellationToken.None);
        await store.TryMarkDeadAsync(roster[0].Id, CancellationToken.None);

        var revived = await store.SweepExpiredRespawnsAsync(10, CancellationToken.None);

        revived.Should().BeEmpty();
    }

    [Fact]
    public async Task PruneExpiredAsync_Deletes_Only_Disconnected_Sessions_Past_The_Grace()
    {
        await using var db = _dbFactory.CreateContext();
        var store = new EfCoreCombatSessionStore(db);
        var enemy = new EnemyInstance
        {
            ZoneGroup = "zone:test",
            LocationSlug = "square",
            ArchetypeSlug = "goblin",
            Name = "Goblin",
            CurrentHealth = 10,
            MaxHealth = 10,
        };
        db.EnemyInstances.Add(enemy);
        await db.SaveChangesAsync();

        var gone = await SeedCharacterAsync(db);
        var connected = await SeedCharacterAsync(db);

        await store.SetAsync(gone.Id, new ActiveCombatSession("zone:test", "square", enemy.Id, false, 1, DateTimeOffset.UtcNow), CancellationToken.None);
        await store.SetAsync(connected.Id, new ActiveCombatSession("zone:test", "square", enemy.Id, false, 1, DateTimeOffset.UtcNow), CancellationToken.None);

        var disconnected = await db.CombatSessions.SingleAsync(s => s.CharacterId == gone.Id);
        disconnected.DisconnectedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        await db.SaveChangesAsync();

        var pruned = await store.PruneExpiredAsync(TimeSpan.FromSeconds(60), CancellationToken.None);

        pruned.Should().BeEquivalentTo([gone.Id]);
        (await store.IsInCombatAsync(connected.Id, CancellationToken.None))
            .Should().BeTrue("a connected session keeps its fight");
    }

    [Fact]
    public async Task SweepAsync_Broadcasts_EnemySpawned_And_Prunes_Expired_Sessions()
    {
        var enemies = new FakeEnemyInstanceStore();
        var sessions = new FakeCombatSessionStore();
        var characterId = Guid.NewGuid();
        sessions.Set(characterId, new ActiveCombatSession("zone:test", "square", Guid.NewGuid(), false, 0, DateTimeOffset.UtcNow));
        sessions.ArrangeExpired(characterId);

        var respawn = new EnemyRespawnedEvent(Guid.NewGuid(), "zone:test", "square", "Goblin", 1, 30, 30);
        enemies.DueRespawns.Enqueue(respawn);

        var proxy = new Mock<IClientProxy>();
        proxy.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.Group(respawn.ZoneGroup)).Returns(proxy.Object);
        var hub = new Mock<IHubContext<GameHub>>();
        hub.SetupGet(h => h.Clients).Returns(clients.Object);

        using var provider = new ServiceCollection()
            .AddSingleton<IEnemyInstanceStore>(enemies)
            .AddSingleton<ICombatSessionStore>(sessions)
            .BuildServiceProvider();

        var service = new EnemyRespawnService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            hub.Object,
            Options.Create(new EnemyWorldOptions()),
            NullLogger<EnemyRespawnService>.Instance);

        await service.SweepAsync(CancellationToken.None);

        enemies.SweepCount.Should().Be(1);
        proxy.Verify(
            p => p.SendCoreAsync("EnemySpawned", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Once);
        sessions.IsInCombat(characterId).Should().BeFalse("an expired session is pruned by the same sweep");
    }

    [Theory]
    [InlineData(0, 5, 50, 60)]
    [InlineData(60, 0, 50, 60)]
    [InlineData(60, 5, 0, 60)]
    [InlineData(60, 5, 50, 0)]
    public void EnemyWorldOptions_Reject_Out_Of_Range_Values(
        int respawnDelaySeconds,
        int sweepIntervalSeconds,
        int respawnBatchSize,
        int sessionGraceSeconds)
    {
        var options = new EnemyWorldOptions
        {
            RespawnDelaySeconds = respawnDelaySeconds,
            SweepIntervalSeconds = sweepIntervalSeconds,
            RespawnBatchSize = respawnBatchSize,
            SessionGraceSeconds = sessionGraceSeconds,
        };

        var results = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);

        valid.Should().BeFalse("a nonsensical world setting must fail at startup rather than be silently defaulted");
        results.Should().NotBeEmpty();
    }
}
