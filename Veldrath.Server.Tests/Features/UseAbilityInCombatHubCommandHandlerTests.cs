using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Shared.Abstractions;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Characters.Combat;
using Veldrath.Server.Hubs;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>Unit tests for <see cref="UseAbilityInCombatHubCommandHandler"/>.</summary>
public class UseAbilityInCombatHubCommandHandlerTests : IDisposable
{
    private readonly TestDbContextFactory              _dbFactory      = new();
    private readonly List<(string Key, Guid EnemyId)> _enemyCleanup   = [];
    private readonly List<Guid>                        _sessionCleanup = [];

    private UseAbilityInCombatHubCommandHandler MakeHandler(ApplicationDbContext db) =>
        new(
            new CharacterRepository(db),
            Mock.Of<IPowerRepository>(),
            Mock.Of<IServiceScopeFactory>(),
            Mock.Of<IHubContext<GameHub>>(),
            NullLogger<UseAbilityInCombatHubCommandHandler>.Instance);

    public void Dispose()
    {
        foreach (var (key, id) in _enemyCleanup)  ZoneLocationEnemyStore.RemoveEnemy(key, id);
        foreach (var id        in _sessionCleanup) CombatSessionStore.Remove(id);
        _dbFactory.Dispose();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static async Task<Character> SeedCharacterAsync(ApplicationDbContext db, string? attrsJson = null)
    {
        var account = new PlayerAccount { UserName = $"u_{Guid.NewGuid():N}" };
        account.NormalizedUserName = account.UserName.ToUpperInvariant();
        db.Users.Add(account);
        var character = new Character
        {
            AccountId    = account.Id,
            Name         = $"Char_{Guid.NewGuid():N}",
            ClassName    = "Mage",
            SlotIndex    = 1,
            Attributes   = attrsJson ?? "{}",
            AbilitiesBlob = "[\"fireball\",\"heal\"]",
        };
        db.Characters.Add(character);
        await db.SaveChangesAsync();
        return character;
    }

    private (string ZoneGroup, string LocationSlug, SpawnedEnemy Enemy) AddEnemy(int hp = 100)
    {
        var zoneGroup    = $"test/{Guid.NewGuid():N}";
        const string loc = "dungeon";
        var key          = ZoneLocationEnemyStore.MakeKey(zoneGroup, loc);
        var enemy        = new SpawnedEnemy { Name = "Golem", Level = 1, CurrentHealth = hp, MaxHealth = hp };
        ZoneLocationEnemyStore.AddEnemy(key, enemy);
        _enemyCleanup.Add((key, enemy.Id));
        return (zoneGroup, loc, enemy);
    }

    private void SetSession(Guid charId, string zoneGroup, string loc, Guid enemyId)
    {
        CombatSessionStore.Set(charId, new ActiveCombatSession(zoneGroup, loc, enemyId, false, 0, DateTimeOffset.UtcNow));
        _sessionCleanup.Add(charId);
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Returns_Error_When_AbilityId_Is_Empty()
    {
        await using var db = _dbFactory.CreateContext();

        var result = await MakeHandler(db).Handle(
            new UseAbilityInCombatHubCommand(Guid.NewGuid(), ""),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty");
    }

    [Fact]
    public async Task Handle_Returns_Error_When_Not_In_Combat()
    {
        await using var db = _dbFactory.CreateContext();

        var result = await MakeHandler(db).Handle(
            new UseAbilityInCombatHubCommand(Guid.NewGuid(), "fireball"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Not in combat");
    }

    [Fact]
    public async Task Handle_Returns_Error_When_Ability_Is_On_Cooldown()
    {
        await using var db = _dbFactory.CreateContext();
        const string abilityId = "fireball";
        // Serialize attrs with cooldown already active.
        string attrs = $$"""{"CurrentMana":100,"MaxMana":100,"AbilityCooldown_{{abilityId}}":3}""";
        var character = await SeedCharacterAsync(db, attrs);
        var (zg, loc, enemy) = AddEnemy();
        SetSession(character.Id, zg, loc, enemy.Id);

        var result = await MakeHandler(db).Handle(
            new UseAbilityInCombatHubCommand(character.Id, abilityId),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cooldown");
    }

    [Fact]
    public async Task Handle_Returns_Error_When_Not_Enough_Mana()
    {
        await using var db = _dbFactory.CreateContext();
        // CurrentMana=0 — cannot pay the 10-mana cost.
        var character = await SeedCharacterAsync(db, """{"CurrentMana":0,"MaxMana":100}""");
        var (zg, loc, enemy) = AddEnemy();
        SetSession(character.Id, zg, loc, enemy.Id);

        var result = await MakeHandler(db).Handle(
            new UseAbilityInCombatHubCommand(character.Id, "fireball"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("mana");
    }

    [Fact]
    public async Task Handle_Deals_Damage_When_Ability_Used_Successfully()
    {
        await using var db = _dbFactory.CreateContext();
        // Enough mana, no cooldown — ability fires.
        var character = await SeedCharacterAsync(db, """{"CurrentMana":50,"MaxMana":50}""");
        var (zg, loc, enemy) = AddEnemy(hp: 200);
        SetSession(character.Id, zg, loc, enemy.Id);

        var result = await MakeHandler(db).Handle(
            new UseAbilityInCombatHubCommand(character.Id, "fireball"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.AbilityDamage.Should().BeGreaterThan(0);
        enemy.CurrentHealth.Should().BeLessThan(200);
    }
}
