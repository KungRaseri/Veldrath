using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Progression.Commands;
using RealmEngine.Core.Features.Progression.Queries;
using RealmEngine.Core.Features.Progression.Services;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Progression;

[Trait("Category", "Feature")]
public class AwardSkillXPHandlerTests
{
    /// <summary>
    /// Builds a real SkillDataService pre-initialized with the given skill definitions.
    /// </summary>
    private static async Task<SkillDataService> BuildSkillDataService(IEnumerable<SkillDefinition> skills)
    {
        var mockRepo = new Mock<ISkillRepository>();
        mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(skills.ToList());
        var svc = new SkillDataService(mockRepo.Object);
        await svc.InitializeAsync();
        return svc;
    }

    private static SkillDefinition MakeSkillDef(string id, int baseXP = 100, double multiplier = 0.5, int maxRank = 5) =>
        new() { SkillId = id, Name = id, DisplayName = id, Description = id, Category = "combat", BaseXPCost = baseXP, CostMultiplier = multiplier, MaxRank = maxRank };

    [Fact]
    public async Task Handle_AwardsXP_WithoutRankUp()
    {
        var skillDef = MakeSkillDef("swords", baseXP: 100);
        var catalogSvc = await BuildSkillDataService([skillDef]);
        var progressionSvc = new SkillProgressionService(catalogSvc);
        var handler = new AwardSkillXPHandler(progressionSvc, NullLogger<AwardSkillXPHandler>.Instance);

        var character = new Character { Name = "Hero" };
        var command = new AwardSkillXPCommand { Character = character, SkillId = "swords", XPAmount = 50 };
        var result = await handler.Handle(command, default);

        result.SkillId.Should().Be("swords");
        result.RanksGained.Should().Be(0);
        result.DidRankUp.Should().BeFalse();
        character.Skills["swords"].CurrentXP.Should().Be(50);
    }

    [Fact]
    public async Task Handle_RanksUp_WhenXPThresholdMet()
    {
        // Base XP cost = 100, rank 0 → rank 1 costs 100 XP
        var catalogSvc = await BuildSkillDataService([MakeSkillDef("archery", baseXP: 100)]);
        var progressionSvc = new SkillProgressionService(catalogSvc);
        var handler = new AwardSkillXPHandler(progressionSvc, NullLogger<AwardSkillXPHandler>.Instance);

        var character = new Character { Name = "Hero" };
        var result = await handler.Handle(new AwardSkillXPCommand { Character = character, SkillId = "archery", XPAmount = 100 }, default);

        result.RanksGained.Should().Be(1);
        result.NewRank.Should().Be(1);
        result.Notifications.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_ZeroXP_ReturnsNoProgress()
    {
        var catalogSvc = await BuildSkillDataService([MakeSkillDef("mining")]);
        var progressionSvc = new SkillProgressionService(catalogSvc);
        var handler = new AwardSkillXPHandler(progressionSvc, NullLogger<AwardSkillXPHandler>.Instance);

        var character = new Character { Name = "Hero" };
        var result = await handler.Handle(new AwardSkillXPCommand { Character = character, SkillId = "mining", XPAmount = 0 }, default);

        result.RanksGained.Should().Be(0);
    }
}

[Trait("Category", "Feature")]
public class LearnPowerHandlerTests
{
    private static async Task<PowerDataService> BuildPowerDataService(IEnumerable<Power> abilities)
    {
        var mockRepo = new Mock<IPowerRepository>();
        mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(abilities.ToList());
        var svc = new PowerDataService(mockRepo.Object);
        await svc.InitializeAsync();
        return svc;
    }

    private static Power MakeAbility(string id, int requiredLevel = 1, string? className = null) =>
        new()
        {
            Id = id,
            Slug = id,
            DisplayName = id,
            RequiredLevel = requiredLevel,
            AllowedClasses = className is null ? [] : [className]
        };

    [Fact]
    public async Task Handle_ReturnsFailure_WhenAbilityUnknown()
    {
        var abilitySvc = await BuildPowerDataService([]);
        var handler = new LearnPowerHandler(abilitySvc);

        var character = new Character { Name = "Hero", Level = 1 };
        var result = await handler.Handle(new LearnPowerCommand { Character = character, PowerId = "fireball" }, default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Unknown ability");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenAlreadyLearned()
    {
        var abilitySvc = await BuildPowerDataService([MakeAbility("slash")]);
        var handler = new LearnPowerHandler(abilitySvc);

        var character = new Character { Name = "Hero", Level = 5 };
        character.LearnedAbilities["slash"] = new CharacterAbility { AbilityId = "slash" };

        var result = await handler.Handle(new LearnPowerCommand { Character = character, PowerId = "slash" }, default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already know");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenLevelTooLow()
    {
        var abilitySvc = await BuildPowerDataService([MakeAbility("meteor", requiredLevel: 20)]);
        var handler = new LearnPowerHandler(abilitySvc);

        var character = new Character { Name = "Hero", Level = 5 };
        var result = await handler.Handle(new LearnPowerCommand { Character = character, PowerId = "meteor" }, default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("20");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenClassMismatch()
    {
        var abilitySvc = await BuildPowerDataService([MakeAbility("smite", className: "Paladin")]);
        var handler = new LearnPowerHandler(abilitySvc);

        var character = new Character { Name = "Hero", Level = 10, ClassName = "Wizard" };
        var result = await handler.Handle(new LearnPowerCommand { Character = character, PowerId = "smite" }, default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not available");
    }

    [Fact]
    public async Task Handle_LearnsAbility_WhenAllConditionsMet()
    {
        var abilitySvc = await BuildPowerDataService([MakeAbility("fireball", requiredLevel: 5)]);
        var handler = new LearnPowerHandler(abilitySvc);

        var character = new Character { Name = "Hero", Level = 10 };
        var result = await handler.Handle(new LearnPowerCommand { Character = character, PowerId = "fireball" }, default);

        result.Success.Should().BeTrue();
        character.LearnedAbilities.Should().ContainKey("fireball");
    }
}

[Trait("Category", "Feature")]
public class GetSkillProgressHandlerTests
{
    private static async Task<GetSkillProgressHandler> CreateHandlerAsync(IEnumerable<SkillDefinition>? skills = null)
    {
        var mockRepo = new Mock<ISkillRepository>();
        mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync((skills ?? []).ToList());
        var catalogSvc = new SkillDataService(mockRepo.Object);
        await catalogSvc.InitializeAsync();
        return new GetSkillProgressHandler(new SkillProgressionService(catalogSvc));
    }

    [Fact]
    public async Task Handle_ReturnsUntrainedData_WhenCharacterLacksSkill()
    {
        var skillDef = new SkillDefinition { SkillId = "swords", Name = "Swords", DisplayName = "Swords", Description = "Swords skill", Category = "combat", BaseXPCost = 100 };
        var handler = await CreateHandlerAsync([skillDef]);
        var character = new Character { Name = "Hero" };

        var result = await handler.Handle(new GetSkillProgressQuery { Character = character, SkillId = "swords" }, default);

        result.SkillId.Should().Be("swords");
        result.CurrentRank.Should().Be(0);
        result.CurrentXP.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ReturnsCorrectRankAndXP_WhenCharacterHasSkill()
    {
        var handler = await CreateHandlerAsync();
        var character = new Character { Name = "Aria", Level = 5 };
        character.Skills["archery"] = new CharacterSkill
        {
            SkillId = "archery", Name = "Archery", CurrentRank = 3, CurrentXP = 50, XPToNextRank = 150
        };

        var result = await handler.Handle(new GetSkillProgressQuery { Character = character, SkillId = "archery" }, default);

        result.CurrentRank.Should().Be(3);
        result.CurrentXP.Should().Be(50);
        result.XPToNextRank.Should().Be(150);
    }
}
