using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RealmEngine.Shared.Abstractions;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Characters.Combat;
using Veldrath.Server.Features.Quest;
using Veldrath.Server.Settings;
using Veldrath.Server.Tests.Infrastructure;
using Character = Veldrath.Server.Data.Entities.Character;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Guard-branch tests for the remaining combat commands and the tutorial/quest-log commands: stale sessions, a
/// vanished enemy, a missing character, a successful flight, and a quest catalog that cannot be read.
/// </summary>
public class CombatAndQuestGuardTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory = new();
    private readonly FakeEnemyInstanceStore _enemies = new();
    private readonly FakeCombatSessionStore _sessions = new();

    public void Dispose() => _dbFactory.Dispose();

    private static async Task<Character> SeedCharacterAsync(ApplicationDbContext db, DateTimeOffset? deletedAt = null)
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
            Attributes = """{"CurrentHealth":50,"MaxHealth":100}""",
            DeletedAt = deletedAt,
        };
        db.Characters.Add(character);
        await db.SaveChangesAsync();
        return character;
    }

    private SpawnedEnemy AddEnemy(int hp = 100)
    {
        var enemy = new SpawnedEnemy { Name = "Golem", Level = 1, CurrentHealth = hp, MaxHealth = Math.Max(1, hp) };
        _enemies.Add("test/zone", "dungeon", enemy);
        return enemy;
    }

    private void SetSession(Guid characterId, Guid enemyId) =>
        _sessions.Set(characterId, new ActiveCombatSession("test/zone", "dungeon", enemyId, false, 0, DateTimeOffset.UtcNow));

    private DefendActionHubCommandHandler DefendHandler(ApplicationDbContext db, IRandomSource? random = null) =>
        new(new CharacterRepository(db), _enemies, _sessions,
            Options.Create(new DeathPenaltyOptions()),
            NullLogger<DefendActionHubCommandHandler>.Instance,
            random);

    private FleeFromCombatHubCommandHandler FleeHandler(ApplicationDbContext db, IRandomSource random) =>
        new(new CharacterRepository(db), _enemies, _sessions,
            Options.Create(new DeathPenaltyOptions()),
            NullLogger<FleeFromCombatHubCommandHandler>.Instance,
            random);

    // ── Defend ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Defend_ClearsTheSession_WhenTheEnemyNoLongerExists()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db);
        SetSession(character.Id, Guid.NewGuid());

        var result = await DefendHandler(db, FakeRandomSource.Returning(0))
            .Handle(new DefendActionHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Enemy no longer exists");
        _sessions.IsInCombat(character.Id).Should().BeFalse();
    }

    [Fact]
    public async Task Defend_ClearsTheSession_WhenTheEnemyIsAlreadyDead()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db);
        var enemy = AddEnemy(hp: 0);
        SetSession(character.Id, enemy.Id);

        var result = await DefendHandler(db, FakeRandomSource.Returning(0))
            .Handle(new DefendActionHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Enemy is already dead");
        _sessions.IsInCombat(character.Id).Should().BeFalse();
    }

    [Fact]
    public async Task Defend_ReturnsAnError_WhenTheCharacterRowIsGone()
    {
        await using var db = _dbFactory.CreateContext();
        var enemy = AddEnemy();
        var characterId = Guid.NewGuid();
        SetSession(characterId, enemy.Id);

        var result = await DefendHandler(db, FakeRandomSource.Returning(0))
            .Handle(new DefendActionHubCommand(characterId), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Character not found");
    }

    // ── Flee ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Flee_EndsTheSession_AndHandsAggroOn_WhenTheEscapeSucceeds()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db);
        var enemy = AddEnemy();
        enemy.Participants.Add(character.Id);
        enemy.LastAttackerId = character.Id;
        SetSession(character.Id, enemy.Id);

        var result = await FleeHandler(db, FakeRandomSource.Returning(0))
            .Handle(new FleeFromCombatHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Fled.Should().BeTrue();
        _sessions.IsInCombat(character.Id).Should().BeFalse();
        enemy.Participants.Should().NotContain(character.Id);
        enemy.LastAttackerId.Should().Be(Guid.Empty, "the last attacker left, so aggro passes to nobody");
    }

    [Fact]
    public async Task Flee_ReturnsAnError_WhenTheCharacterRowIsGoneAndTheEscapeFailed()
    {
        await using var db = _dbFactory.CreateContext();
        var enemy = AddEnemy();
        enemy.Participants.Add(Guid.NewGuid());
        var characterId = Guid.NewGuid();
        SetSession(characterId, enemy.Id);

        var result = await FleeHandler(db, FakeRandomSource.Returning(99))
            .Handle(new FleeFromCombatHubCommand(characterId), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Character not found");
    }

    // ── PrepareTutorial ───────────────────────────────────────────────────────

    private static PrepareTutorialHubCommandHandler TutorialHandler(ApplicationDbContext db) =>
        new(new CharacterRepository(db),
            Mock.Of<IZoneLocationRepository>(),
            Mock.Of<MediatR.ISender>(),
            Options.Create(new TutorialOptions { TutorialQuestSlug = "first-steps" }),
            NullLogger<PrepareTutorialHubCommandHandler>.Instance);

    [Fact]
    public async Task PrepareTutorial_ReturnsAnError_WhenTheCharacterIsMissing()
    {
        await using var db = _dbFactory.CreateContext();

        var result = await TutorialHandler(db).Handle(
            new PrepareTutorialHubCommand(Guid.NewGuid()), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task PrepareTutorial_SkipsADeletedCharacter()
    {
        // The handler owns this guard, so it is exercised with a repository that still hands back the soft-deleted row.
        await using var db = _dbFactory.CreateContext();
        var deleted = new Character
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            Name = "Fallen",
            ClassName = "Warrior",
            SlotIndex = 1,
            DeletedAt = DateTimeOffset.UtcNow,
        };
        var repo = new Mock<ICharacterRepository>();
        repo.Setup(r => r.GetByIdAsync(deleted.Id, It.IsAny<CancellationToken>())).ReturnsAsync(deleted);
        var handler = new PrepareTutorialHubCommandHandler(
            repo.Object,
            Mock.Of<IZoneLocationRepository>(),
            Mock.Of<MediatR.ISender>(),
            Options.Create(new TutorialOptions { TutorialQuestSlug = "first-steps" }),
            NullLogger<PrepareTutorialHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new PrepareTutorialHubCommand(deleted.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Skipped.Should().BeTrue();
        result.QuestSlug.Should().Be("first-steps");
    }

    // ── GetQuestLog ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetQuestLog_ReturnsTheJournal_WhenTheCatalogFaults()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db);
        var quests = new Mock<IQuestRepository>();
        quests.Setup(r => r.GetAllAsync()).ThrowsAsync(new InvalidOperationException("catalog offline"));
        var handler = new GetQuestLogHubCommandHandler(
            new CharacterRepository(db), quests.Object, NullLogger<GetQuestLogHubCommandHandler>.Instance);

        var result = await handler.Handle(new GetQuestLogHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Available.Should().BeEmpty();
    }
}
