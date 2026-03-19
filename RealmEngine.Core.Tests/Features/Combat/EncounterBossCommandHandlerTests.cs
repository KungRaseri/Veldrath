using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Combat.Commands.EncounterBoss;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Combat;

[Trait("Category", "Feature")]
public class EncounterBossCommandHandlerTests
{
    // EnemyGenerator uses a primary constructor (no null-checks); IEnemyRepository is mocked.
    private static EnemyGenerator MakeGenerator(Mock<IEnemyRepository>? repo = null) =>
        new((repo ?? new Mock<IEnemyRepository>()).Object, NullLogger<EnemyGenerator>.Instance);

    private static EncounterBossCommandHandler CreateHandler(EnemyGenerator? generator = null) =>
        new(generator ?? MakeGenerator(), NullLogger<EncounterBossCommandHandler>.Instance);

    [Fact]
    public async Task Handle_ReturnsFailure_WhenBossNotFoundInRepository()
    {
        var repo = new Mock<IEnemyRepository>();
        repo.Setup(r => r.GetBySlugAsync(It.IsAny<string>())).ReturnsAsync((Enemy?)null);
        var handler = CreateHandler(MakeGenerator(repo));

        var result = await handler.Handle(
            new EncounterBossCommand { BossName = "ancient-dragon", BossCategory = "dragons" }, default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("ancient-dragon");
    }

    [Fact]
    public async Task Handle_ReturnsSuccess_WithPopulatedBossInfo_WhenBossFound()
    {
        var boss = new Enemy
        {
            Name = "Ancient Dragon",
            BaseName = "Dragon",
            Level = 50,
            MaxHealth = 5000,
            XP = 2000,
            GoldReward = 500,
            Difficulty = EnemyDifficulty.Boss,
            Type = EnemyType.Dragon,
            BasePhysicalDamage = 200,
            BaseMagicDamage = 150,
            Prefixes = [],
            Abilities = []
        };
        var repo = new Mock<IEnemyRepository>();
        repo.Setup(r => r.GetBySlugAsync("ancient-dragon")).ReturnsAsync(boss);
        var handler = CreateHandler(MakeGenerator(repo));

        var result = await handler.Handle(
            new EncounterBossCommand { BossName = "ancient-dragon", BossCategory = "dragons" }, default);

        result.Success.Should().BeTrue();
        result.Boss.Should().BeSameAs(boss);
        result.Info.Should().NotBeNull();
        result.Info!.Name.Should().Be("Ancient Dragon");
        result.Info.Level.Should().Be(50);
        result.Info.EstimatedXP.Should().Be(2000);
    }

    [Fact]
    public async Task Handle_GeneratesBossTitle_BasedOnEnemyType()
    {
        var boss = new Enemy
        {
            Name = "Balarog",
            BaseName = "Demon",
            Level = 40,
            Type = EnemyType.Demon,
            Difficulty = EnemyDifficulty.Boss,
            Prefixes = [],
            Abilities = []
        };
        var repo = new Mock<IEnemyRepository>();
        repo.Setup(r => r.GetBySlugAsync("balarog")).ReturnsAsync(boss);
        var handler = CreateHandler(MakeGenerator(repo));

        var result = await handler.Handle(
            new EncounterBossCommand { BossName = "balarog", BossCategory = "demons" }, default);

        result.Success.Should().BeTrue();
        result.Info!.Title.Should().Contain("Demon Lord");
    }

    [Fact]
    public async Task Handle_UsesPrefixedTitle_WhenBossHasPrefixes()
    {
        var boss = new Enemy
        {
            Name = "Razgol the Unbroken",
            BaseName = "Orc Warchief",
            Level = 35,
            Type = EnemyType.Humanoid,
            Difficulty = EnemyDifficulty.Boss,
            Prefixes = [new NameComponent { Value = "Razgol the Unbroken" }],
            Abilities = []
        };
        var repo = new Mock<IEnemyRepository>();
        repo.Setup(r => r.GetBySlugAsync("razgol")).ReturnsAsync(boss);

        var result = await CreateHandler(MakeGenerator(repo)).Handle(
            new EncounterBossCommand { BossName = "razgol", BossCategory = "orcs" }, default);

        result.Success.Should().BeTrue();
        result.Info!.Title.Should().Contain("Razgol the Unbroken");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenRepositoryThrows()
    {
        var repo = new Mock<IEnemyRepository>();
        repo.Setup(r => r.GetBySlugAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("DB connection lost"));

        var result = await CreateHandler(MakeGenerator(repo)).Handle(
            new EncounterBossCommand { BossName = "some-boss", BossCategory = "bosses" }, default);

        // EnemyGenerator.GenerateEnemyByNameAsync catches all exceptions and returns null,
        // so the handler sees a null boss and returns NotFound failure (not an exception failure).
        result.Success.Should().BeFalse();
    }
}
