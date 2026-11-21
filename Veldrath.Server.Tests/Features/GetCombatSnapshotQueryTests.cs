using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Characters.Combat;
using Veldrath.Server.Tests.Infrastructure;
using PlayerAccount = Veldrath.Server.Data.Entities.PlayerAccount;
using ServerCharacter = Veldrath.Server.Data.Entities.Character;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Tests for <see cref="GetCombatSnapshotQueryHandler"/>, the shared view of a fight that every
/// client at the location receives.
/// </summary>
public class GetCombatSnapshotQueryTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory = new();
    private readonly FakeEnemyInstanceStore _enemies = new();

    public void Dispose() => _dbFactory.Dispose();

    private GetCombatSnapshotQueryHandler Handler(ApplicationDbContext db) =>
        new(_enemies, new CharacterRepository(db), NullLogger<GetCombatSnapshotQueryHandler>.Instance);

    private static async Task<ServerCharacter> SeedCharacterAsync(
        ApplicationDbContext db,
        string name = "Thornwarden",
        string? attributes = null,
        int level = 3)
    {
        var account = new PlayerAccount { UserName = $"u_{Guid.NewGuid():N}" };
        account.NormalizedUserName = account.UserName.ToUpperInvariant();
        db.Users.Add(account);

        var character = new ServerCharacter
        {
            AccountId = account.Id,
            Name = name,
            ClassName = "Warrior",
            SlotIndex = 1,
            Level = level,
            Attributes = attributes ?? """{"CurrentHealth":18,"MaxHealth":30}""",
        };
        db.Characters.Add(character);
        await db.SaveChangesAsync();
        return character;
    }

    private SpawnedEnemy AddEnemy(params string[] abilityNames)
    {
        var enemy = new SpawnedEnemy
        {
            ArchetypeSlug = "golem",
            Name = "Stone golem",
            Level = 4,
            CurrentHealth = 40,
            MaxHealth = 90,
            BaseXp = 20,
            GoldReward = 10,
            Template = new Enemy
            {
                Abilities = abilityNames.Select(name => new Power { Name = name, DisplayName = name }).ToList(),
                Traits =
                {
                    ["typeKey"] = new TraitValue("elementals", TraitType.String),
                },
            },
        };

        _enemies.Add("zone/1", "grotto", enemy);
        return enemy;
    }

    [Fact]
    public async Task Returns_null_when_the_enemy_no_longer_exists()
    {
        await using var db = _dbFactory.CreateContext();

        var snapshot = await Handler(db).Handle(
            new GetCombatSnapshotQuery(Guid.NewGuid()),
            CancellationToken.None);

        snapshot.Should().BeNull();
    }

    [Fact]
    public async Task Lists_participants_by_damage_with_rounded_shares()
    {
        await using var db = _dbFactory.CreateContext();
        var first = await SeedCharacterAsync(db, "Thornwarden");
        var second = await SeedCharacterAsync(db, "Ash", """{"CurrentHealth":5,"MaxHealth":20}""");
        var enemy = AddEnemy("Bite");
        enemy.Participants.Add(first.Id);
        enemy.Participants.Add(second.Id);
        enemy.DamageContributions[first.Id] = 30;
        enemy.DamageContributions[second.Id] = 10;

        var snapshot = await Handler(db).Handle(new GetCombatSnapshotQuery(enemy.Id), CancellationToken.None);

        snapshot.Should().NotBeNull();
        snapshot!.EnemyName.Should().Be("Stone golem");
        snapshot.EnemyArchetypeSlug.Should().Be("golem", "the portrait is addressed by the archetype slug");
        snapshot.EnemyTier.Should().Be("regular", "no boss or elite flag is set on this template");
        snapshot.EnemyArchetypeTypeKey.Should().Be(
            "elementals",
            "the client maps the content type onto the fallback glyph's silhouette class");
        snapshot.EnemyCurrentHealth.Should().Be(40);
        snapshot.EnemyMaxHealth.Should().Be(90);
        snapshot.EnemyAbilityNames.Should().Equal("Bite");

        snapshot.Participants.Should().HaveCount(2);
        snapshot.Participants[0].Name.Should().Be("Thornwarden");
        snapshot.Participants[0].DamageDealt.Should().Be(30);
        snapshot.Participants[0].ContributionPercent.Should().Be(75);
        snapshot.Participants[0].CurrentHealth.Should().Be(18);
        snapshot.Participants[0].MaxHealth.Should().Be(30);
        snapshot.Participants[1].Name.Should().Be("Ash");
        snapshot.Participants[1].ContributionPercent.Should().Be(25);
        snapshot.Participants[1].CurrentHealth.Should().Be(5);
    }

    [Fact]
    public async Task Falls_back_to_the_level_health_when_the_blob_has_none()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db, "Naked", "{}", level: 4);
        var enemy = AddEnemy();
        enemy.Participants.Add(character.Id);

        var snapshot = await Handler(db).Handle(new GetCombatSnapshotQuery(enemy.Id), CancellationToken.None);

        var participant = snapshot!.Participants.Should().ContainSingle().Subject;
        participant.MaxHealth.Should().Be(40);
        participant.CurrentHealth.Should().Be(40);
        participant.ContributionPercent.Should().Be(0);
    }

    [Fact]
    public async Task Lists_a_participant_whose_character_row_is_gone_without_a_name()
    {
        await using var db = _dbFactory.CreateContext();
        var enemy = AddEnemy();
        var ghost = Guid.NewGuid();
        enemy.Participants.Add(ghost);
        enemy.DamageContributions[ghost] = 5;

        var snapshot = await Handler(db).Handle(new GetCombatSnapshotQuery(enemy.Id), CancellationToken.None);

        var participant = snapshot!.Participants.Should().ContainSingle().Subject;
        participant.CharacterId.Should().Be(ghost);
        participant.Name.Should().BeNull();
        participant.CurrentHealth.Should().Be(0);
        participant.ContributionPercent.Should().Be(100);
    }

    [Fact]
    public async Task Treats_a_malformed_blob_as_zero_health_instead_of_throwing()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db, "Broken", "{not json", level: 2);
        var enemy = AddEnemy();
        enemy.Participants.Add(character.Id);

        var snapshot = await Handler(db).Handle(new GetCombatSnapshotQuery(enemy.Id), CancellationToken.None);

        var participant = snapshot!.Participants.Should().ContainSingle().Subject;
        participant.MaxHealth.Should().Be(20);
        participant.CurrentHealth.Should().Be(20);
    }

    [Fact]
    public async Task Skips_unnamed_enemy_abilities()
    {
        await using var db = _dbFactory.CreateContext();
        var enemy = AddEnemy("Bite", "   ");

        var snapshot = await Handler(db).Handle(new GetCombatSnapshotQuery(enemy.Id), CancellationToken.None);

        snapshot!.EnemyAbilityNames.Should().Equal("Bite");
    }
}
