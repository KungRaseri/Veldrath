using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Features.Combat.Commands.EncounterBoss;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Data.Services;
using RealmEngine.Shared.Models;
using Xunit;

namespace RealmEngine.Core.Tests.Features.Combat.Integration;

/// <summary>
/// Integration tests for boss encounter system.
/// </summary>
public class BossEncounterIntegrationTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IMediator _mediator;

    public BossEncounterIntegrationTests()
    {
        var services = new ServiceCollection();

        // Register logging
        services.AddLogging();

        // Register game data services
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var dataPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "RealmEngine.Data", "Data", "Json");
        var gameDataCache = new GameDataCache(dataPath, memoryCache);
        
        services.AddSingleton(gameDataCache);
        services.AddSingleton<ReferenceResolverService>();
        services.AddSingleton<EnemyGenerator>();

        // Register MediatR
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(EncounterBossCommand).Assembly));

        _serviceProvider = services.BuildServiceProvider();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Should_Generate_Boss_With_Enhanced_Rewards()
    {
        // Act
        var result = await _mediator.Send(new EncounterBossCommand
        {
            BossCategory = "demons",
            BossName = "Dark Lord"
        });

        // Assert
        result.Success.Should().BeTrue();
        result.Boss.Should().NotBeNull();
        result.Boss!.Name.Should().Be("Dark Lord");
        result.Boss.Difficulty.Should().Be(EnemyDifficulty.Boss);
        result.Boss.XP.Should().BeGreaterThanOrEqualTo(10000); // 5000 * 2 (boss multiplier)
        result.Boss.GoldReward.Should().BeGreaterThanOrEqualTo(20); // Enhanced rewards (3x multiplier)
    }

    [Fact]
    public async Task Should_Return_Detailed_Boss_Information()
    {
        // Act
        var result = await _mediator.Send(new EncounterBossCommand
        {
            BossCategory = "demons",
            BossName = "Dark Lord"
        });

        // Assert
        result.Success.Should().BeTrue();
        result.Info.Should().NotBeNull();
        result.Info!.Name.Should().Be("Dark Lord");
        result.Info.Level.Should().BeGreaterThan(0);
        result.Info.RecommendedPlayerLevel.Should().BeLessThanOrEqualTo(result.Info.Level);
        result.Info.EstimatedXP.Should().BeGreaterThan(0);
        result.Info.EstimatedGold.Should().BeGreaterThan(0);
        result.Info.HealthTotal.Should().BeGreaterThan(0);
        result.Info.WarningMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Should_Include_Boss_Abilities_In_Info()
    {
        // Act
        var result = await _mediator.Send(new EncounterBossCommand
        {
            BossCategory = "demons",
            BossName = "Dark Lord"
        });

        // Assert
        result.Success.Should().BeTrue();
        result.Boss.Should().NotBeNull();
        result.Boss!.Abilities.Should().NotBeNullOrEmpty();
        result.Info!.Abilities.Should().NotBeNullOrEmpty();
        result.Info.Abilities.Count.Should().Be(result.Boss.Abilities.Count);
    }

    [Fact]
    public async Task Should_Set_Boss_Type_And_Difficulty()
    {
        // Act
        var result = await _mediator.Send(new EncounterBossCommand
        {
            BossCategory = "demons",
            BossName = "Dark Lord"
        });

        // Assert
        result.Success.Should().BeTrue();
        result.Boss.Should().NotBeNull();
        result.Boss!.Type.Should().Be(EnemyType.Boss);
        result.Boss.Difficulty.Should().Be(EnemyDifficulty.Boss);
    }

    [Fact]
    public async Task Should_Handle_Unknown_Boss_Gracefully()
    {
        // Act
        var result = await _mediator.Send(new EncounterBossCommand
        {
            BossCategory = "demons",
            BossName = "Nonexistent Boss"
        });

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
        result.Boss.Should().BeNull();
        result.Info.Should().BeNull();
    }

    [Fact]
    public async Task Should_Generate_Warning_Message_For_High_Level_Boss()
    {
        // Act
        var result = await _mediator.Send(new EncounterBossCommand
        {
            BossCategory = "demons",
            BossName = "Dark Lord"
        });

        // Assert
        result.Success.Should().BeTrue();
        result.Info.Should().NotBeNull();
        result.Info!.WarningMessage.Should().Contain("WARNING");
    }

    [Fact]
    public async Task Should_Extract_Special_Traits_From_Boss()
    {
        // Act
        var result = await _mediator.Send(new EncounterBossCommand
        {
            BossCategory = "demons",
            BossName = "Dark Lord"
        });

        // Assert
        result.Success.Should().BeTrue();
        result.Info.Should().NotBeNull();
        result.Info!.SpecialTraits.Should().NotBeNullOrEmpty();
        result.Info.SpecialTraits.Should().Contain(trait => trait.Contains("Boss"));
    }

    [Fact]
    public async Task Should_Hydrate_Boss_With_Full_Abilities()
    {
        // Act
        var result = await _mediator.Send(new EncounterBossCommand
        {
            BossCategory = "demons",
            BossName = "Dark Lord"
        });

        // Assert
        result.Success.Should().BeTrue();
        result.Boss.Should().NotBeNull();
        result.Boss!.Abilities.Should().NotBeNullOrEmpty();
        
        // Verify abilities are fully hydrated (have display names)
        result.Boss.Abilities.Should().AllSatisfy(ability =>
        {
            ability.Name.Should().NotBeNullOrEmpty();
        });
    }

    [Theory]
    [InlineData("demons", "Dark Lord", EnemyDifficulty.Boss)] // Has isBoss in catalog
    [InlineData("wolves", "Fenrir the Ancient", EnemyDifficulty.Easy)] // rarityWeight 100 = common, defaults to Easy
    public async Task Should_Generate_Different_Bosses_From_Different_Categories(string category, string bossName, EnemyDifficulty expectedDifficulty)
    {
        // Act
        var result = await _mediator.Send(new EncounterBossCommand
        {
            BossCategory = category,
            BossName = bossName
        });

        // Assert
        result.Success.Should().BeTrue();
        result.Boss.Should().NotBeNull();
        result.Boss!.Name.Should().Be(bossName);
        result.Boss.Difficulty.Should().Be(expectedDifficulty);
    }
}
