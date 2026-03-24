using Moq;
using RealmEngine.Core.Features.Progression.Commands;
using RealmEngine.Core.Features.Progression.Services;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Progression.Commands;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for LearnSpellHandler.
/// </summary>
public class LearnSpellHandlerTests
{
    private static async Task<SpellCastingService> CreateSpellCastingServiceAsync(
        IEnumerable<Power>? spells = null,
        IEnumerable<SkillDefinition>? skills = null)
    {
        var spellMockRepo = new Mock<IPowerRepository>();
        spellMockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync((spells ?? []).ToList());
        var spellDataSvc = new PowerDataService(spellMockRepo.Object);
        await spellDataSvc.InitializeAsync();

        var skillMockRepo = new Mock<ISkillRepository>();
        skillMockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync((skills ?? []).ToList());
        var skillDataSvc = new SkillDataService(skillMockRepo.Object);
        await skillDataSvc.InitializeAsync();

        return new SpellCastingService(spellDataSvc, new SkillProgressionService(skillDataSvc));
    }

    private static Power MakeSpell(string id, MagicalTradition tradition = MagicalTradition.Arcane, int minSkillRank = 0, int manaCost = 10) =>
        new()
        {
        Id = id, Slug = id,
            Name = id,
            DisplayName = id,
            Description = id,
            Tradition = tradition,
            Rank = 1,
            MinimumSkillRank = minSkillRank,
            ManaCost = manaCost
        };

    [Fact]
    public async Task Returns_Failure_WhenSpellNotInCatalog()
    {
        // Arrange
        var service = await CreateSpellCastingServiceAsync([]);
        var handler = new LearnSpellHandler(service);
        var character = new Character { Name = "Hero" };
        var command = new LearnSpellCommand { Character = character, SpellId = "nonexistent-spell" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Unknown Power");
    }

    [Fact]
    public async Task Returns_Failure_WhenSpellAlreadyLearned()
    {
        // Arrange
        var service = await CreateSpellCastingServiceAsync([MakeSpell("fireball")]);
        var handler = new LearnSpellHandler(service);
        var character = new Character { Name = "Hero" };
        character.LearnedSpells["fireball"] = new CharacterSpell { SpellId = "fireball" };
        var command = new LearnSpellCommand { Character = character, SpellId = "fireball" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already know");
    }

    [Fact]
    public async Task Returns_Failure_WhenCharacterLacksTraditionSkill()
    {
        // Arrange - character has no arcane skill at all
        var service = await CreateSpellCastingServiceAsync([MakeSpell("force-missile", MagicalTradition.Arcane)]);
        var handler = new LearnSpellHandler(service);
        var character = new Character { Name = "Hero" }; // no skills at all
        var command = new LearnSpellCommand { Character = character, SpellId = "force-missile" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Arcane");
    }

    [Fact]
    public async Task Returns_Failure_WhenSkillRankTooLow()
    {
        // Arrange - Power requires rank 30; character has arcane skill at rank 0 (0+20=20 < 30)
        var service = await CreateSpellCastingServiceAsync([MakeSpell("meteor", minSkillRank: 30)]);
        var handler = new LearnSpellHandler(service);
        var character = new Character { Name = "Hero" };
        character.Skills["arcane"] = new CharacterSkill { SkillId = "arcane", Name = "Arcane", Category = "magic", CurrentRank = 0, XPToNextRank = 100 };
        var command = new LearnSpellCommand { Character = character, SpellId = "meteor" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("30");
    }

    [Fact]
    public async Task Succeeds_WhenSkillMeetsMinimumRank()
    {
        // Arrange - Power requires rank 5; character has rank 5
        var service = await CreateSpellCastingServiceAsync([MakeSpell("magic-bolt", minSkillRank: 5)]);
        var handler = new LearnSpellHandler(service);
        var character = new Character { Name = "Hero" };
        character.Skills["arcane"] = new CharacterSkill { SkillId = "arcane", Name = "Arcane", Category = "magic", CurrentRank = 5, XPToNextRank = 200 };
        var command = new LearnSpellCommand { Character = character, SpellId = "magic-bolt" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        character.LearnedSpells.Should().ContainKey("magic-bolt");
        result.SpellLearned.Should().NotBeNull();
        result.SpellLearned!.Id.Should().Be("magic-bolt");
    }

    [Fact]
    public async Task Succeeds_WithSkillTolerance_WhenRankSlightlyBelowRequirement()
    {
        // Arrange - tolerance is: can learn if character rank + 20 >= minSkillRank
        // Power needs rank 29; character has rank 10 (10+20=30 >= 29 → qualifies)
        var service = await CreateSpellCastingServiceAsync([MakeSpell("shadow-bolt", minSkillRank: 29)]);
        var handler = new LearnSpellHandler(service);
        var character = new Character { Name = "Hero" };
        character.Skills["arcane"] = new CharacterSkill { SkillId = "arcane", Name = "Arcane", Category = "magic", CurrentRank = 10, XPToNextRank = 300 };
        var command = new LearnSpellCommand { Character = character, SpellId = "shadow-bolt" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue("rank 10 + tolerance 20 = 30 >= requirement 29");
    }

    [Fact]
    public async Task AddsSpell_WithDefaultTrackingState()
    {
        // Arrange
        var service = await CreateSpellCastingServiceAsync([MakeSpell("ice-shard")]);
        var handler = new LearnSpellHandler(service);
        var character = new Character { Name = "Hero" };
        character.Skills["arcane"] = new CharacterSkill { SkillId = "arcane", Name = "Arcane", Category = "magic", CurrentRank = 10, XPToNextRank = 300 };
        var command = new LearnSpellCommand { Character = character, SpellId = "ice-shard" };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var learned = character.LearnedSpells["ice-shard"];
        learned.TimesCast.Should().Be(0, "newly learned Power has no cast history");
        learned.TimesFizzled.Should().Be(0);
        learned.IsFavorite.Should().BeFalse();
    }

    [Theory]
    [InlineData(MagicalTradition.Arcane, "arcane")]
    [InlineData(MagicalTradition.Divine, "divine")]
    [InlineData(MagicalTradition.Occult, "occult")]
    [InlineData(MagicalTradition.Primal, "primal")]
    public async Task Requires_CorrectTraditionSkill_PerSpellTradition(MagicalTradition tradition, string skillId)
    {
        // Arrange
        var service = await CreateSpellCastingServiceAsync([MakeSpell("test-spell", tradition)]);
        var handler = new LearnSpellHandler(service);
        var character = new Character { Name = "Hero" };
        // Give them the CORRECT tradition skill
        character.Skills[skillId] = new CharacterSkill { SkillId = skillId, Name = skillId, Category = "magic", CurrentRank = 5, XPToNextRank = 300 };
        var command = new LearnSpellCommand { Character = character, SpellId = "test-spell" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue($"character with {skillId} skill should be able to learn {tradition} spells");
    }
}
