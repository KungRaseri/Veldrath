using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Shared.Abstractions;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Characters;
using Veldrath.Server.Features.LevelUp;
using SharedModels = RealmEngine.Shared.Models;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Unit tests for <see cref="GainExperienceHubCommandHandler"/>: the rolling <c>Level × 100</c> XP formula, the
/// validation guards, and the persistence of the new level and XP total.
/// </summary>
public class GainExperienceHubCommandHandlerTests
{
    /// <summary>The seeded warrior curve: 120 health and 20 mana at level 1, 12/2 per level.</summary>
    private static SharedModels.CharacterClass WarriorClass() => new()
    {
        Slug = "warrior",
        Name = "Warrior",
        DisplayName = "Warrior",
        StartingHealth = 120,
        StartingMana = 20,
        Progression = new SharedModels.ClassProgression
        {
            StatGrowth = new SharedModels.StatGrowth { HealthPerLevel = 12, ManaPerLevel = 2 },
        },
    };

    /// <summary>The seeded mage curve: 60 health and 100 mana at level 1, 6/10 per level.</summary>
    private static SharedModels.CharacterClass MageClass() => new()
    {
        Slug = "mage",
        Name = "Mage",
        DisplayName = "Mage",
        StartingHealth = 60,
        StartingMana = 100,
        Progression = new SharedModels.ClassProgression
        {
            StatGrowth = new SharedModels.StatGrowth { HealthPerLevel = 6, ManaPerLevel = 10 },
        },
    };

    private static (GainExperienceHubCommandHandler Handler, Mock<ICharacterRepository> Repo, Character Character)
        Arrange(
            long experience = 0,
            int level = 1,
            string? attributes = null,
            SharedModels.CharacterClass? characterClass = null)
    {
        var character = new Character
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            Name = "TestHero",
            ClassName = characterClass?.Name ?? "Warrior",
            SlotIndex = 1,
            Level = level,
            Experience = experience,
            Attributes = attributes ?? string.Empty,
        };

        var repo = new Mock<ICharacterRepository>();
        repo.Setup(r => r.GetByIdAsync(character.Id, It.IsAny<CancellationToken>())).ReturnsAsync(character);
        repo.Setup(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var classRepo = new Mock<ICharacterClassRepository>();
        classRepo.Setup(r => r.GetByName(It.IsAny<string>())).Returns(characterClass);

        var handler = new GainExperienceHubCommandHandler(
            repo.Object,
            classRepo.Object,
            NullLogger<GainExperienceHubCommandHandler>.Instance);

        return (handler, repo, character);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-25)]
    public async Task Handle_RejectsNonPositiveAwards(int amount)
    {
        var (handler, repo, _) = Arrange();

        var result = await handler.Handle(
            new GainExperienceHubCommand { CharacterId = Guid.NewGuid(), Amount = amount },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Experience amount must be positive");
        repo.Verify(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReturnsAnError_WhenTheCharacterDoesNotExist()
    {
        var repo = new Mock<ICharacterRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Character?)null);
        var classRepo = new Mock<ICharacterClassRepository>();
        var handler = new GainExperienceHubCommandHandler(
            repo.Object,
            classRepo.Object,
            NullLogger<GainExperienceHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new GainExperienceHubCommand { CharacterId = Guid.NewGuid(), Amount = 50 },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Character not found");
    }

    [Fact]
    public async Task Handle_AccumulatesExperienceWithoutLevellingUp()
    {
        var (handler, repo, character) = Arrange(experience: 30, level: 2);

        var result = await handler.Handle(
            new GainExperienceHubCommand { CharacterId = character.Id, Amount = 40, Source = "Combat" },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.NewLevel.Should().Be(2);
        result.NewExperience.Should().Be(70);
        result.LeveledUp.Should().BeFalse();
        result.LeveledUpTo.Should().BeNull();
        character.Experience.Should().Be(70);
        repo.Verify(r => r.UpdateAsync(character, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_LevelsUp_WhenTheAwardMeetsTheThresholdExactly()
    {
        var (handler, _, character) = Arrange(experience: 90, level: 1);

        var result = await handler.Handle(
            new GainExperienceHubCommand { CharacterId = character.Id, Amount = 10 },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.NewLevel.Should().Be(2);
        result.NewExperience.Should().Be(0);
        result.LeveledUp.Should().BeTrue();
        result.LeveledUpTo.Should().Be(2);
        character.Level.Should().Be(2);
    }

    [Fact]
    public async Task Handle_LevelsUpRepeatedly_WhenTheAwardCarriesOver()
    {
        var (handler, _, character) = Arrange(experience: 50, level: 1);

        // 1 → 2 costs 100, 2 → 3 costs 200, 3 → 4 costs 300: 50 + 500 leaves 250 toward level 4.
        var result = await handler.Handle(
            new GainExperienceHubCommand { CharacterId = character.Id, Amount = 500 },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.NewLevel.Should().Be(3);
        result.NewExperience.Should().Be(250);
        result.LeveledUp.Should().BeTrue();
        result.LeveledUpTo.Should().Be(3);
    }

    [Fact]
    public async Task Handle_LevelUp_ExtendsTheClassPoolsAndKeepsTheCurrentValues()
    {
        var attributes = JsonSerializer.Serialize(new Dictionary<string, int>
        {
            ["Constitution"] = 12,
            ["Wisdom"] = 10,
            ["CurrentHealth"] = 100,
            ["MaxHealth"] = 120,
            ["CurrentMana"] = 15,
            ["MaxMana"] = 20,
        });
        var (handler, _, character) = Arrange(
            experience: 90, level: 1, attributes: attributes, characterClass: WarriorClass());

        var result = await handler.Handle(
            new GainExperienceHubCommand { CharacterId = character.Id, Amount = 10 },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.LeveledUp.Should().BeTrue();

        var stored = JsonSerializer.Deserialize<Dictionary<string, int>>(character.Attributes)!;
        // The class curve creation encoded: warrior 120/20 at level 1, +12 health and +2 mana per level.
        stored["MaxHealth"].Should().Be(132);
        stored["MaxMana"].Should().Be(22);

        // A bigger bar is a ceiling, not a heal: the current pools keep the value the fight left them at.
        stored["CurrentHealth"].Should().Be(100);
        stored["CurrentMana"].Should().Be(15);

        result.MaxHealth.Should().Be(132);
        result.CurrentHealth.Should().Be(100);
        result.MaxMana.Should().Be(22);
        result.CurrentMana.Should().Be(15);
    }

    [Fact]
    public async Task Handle_LevelUp_NeverLowersAMagesManaBelowTheClassPool()
    {
        // A legal 27-point mage build: elf, Wisdom 9. The engine formula would derive 5×9 + 2×3 = 51 mana at level 2
        // and clamp the level-1 class pool (100) down to it; the class curve must not.
        var attributes = JsonSerializer.Serialize(new Dictionary<string, int>
        {
            ["Constitution"] = 8,
            ["Wisdom"] = 9,
            ["CurrentHealth"] = 60,
            ["MaxHealth"] = 60,
            ["CurrentMana"] = 100,
            ["MaxMana"] = 100,
        });
        var (handler, _, character) = Arrange(
            experience: 90, level: 1, attributes: attributes, characterClass: MageClass());

        var result = await handler.Handle(
            new GainExperienceHubCommand { CharacterId = character.Id, Amount = 10 },
            CancellationToken.None);

        var stored = JsonSerializer.Deserialize<Dictionary<string, int>>(character.Attributes)!;
        stored["MaxMana"].Should().Be(110, "the mage class curve grows mana by 10 per level from 100");
        stored["CurrentMana"].Should().Be(100, "a rising ceiling must not clamp the current mana down");
        stored["MaxHealth"].Should().Be(66, "the mage class curve grows health by 6 per level from 60");
        result.MaxMana.Should().Be(110);
    }

    [Fact]
    public async Task Handle_LevelUp_NeverLowersAPoolTheCurveDisagreesWith()
    {
        // The API creation path writes the engine formulas (Con 10 at level 1 → 105 health, 53 mana), so the class
        // curve's level-2 mana (22) is below what the character already carries. The blob is the floor.
        var attributes = JsonSerializer.Serialize(new Dictionary<string, int>
        {
            ["Constitution"] = 10,
            ["Wisdom"] = 10,
            ["CurrentHealth"] = 105,
            ["MaxHealth"] = 105,
            ["CurrentMana"] = 53,
            ["MaxMana"] = 53,
        });
        var (handler, _, character) = Arrange(
            experience: 90, level: 1, attributes: attributes, characterClass: WarriorClass());

        await handler.Handle(
            new GainExperienceHubCommand { CharacterId = character.Id, Amount = 10 },
            CancellationToken.None);

        var stored = JsonSerializer.Deserialize<Dictionary<string, int>>(character.Attributes)!;
        stored["MaxHealth"].Should().Be(132);
        stored["MaxMana"].Should().Be(53, "the stored pool is a floor even when the class curve is lower");
        stored["CurrentMana"].Should().Be(53);
    }

    [Fact]
    public async Task Handle_LevelUp_WithoutAClassRow_FallsBackToTheEngineFormulas()
    {
        // No class catalogue row resolves (the stub repository returns null), so the engine formula is the only
        // derivation left. Constitution 12 at level 2: 12 × 10 + 2 × 5 = 130; Wisdom 10: 10 × 5 + 2 × 3 = 56.
        var attributes = JsonSerializer.Serialize(new Dictionary<string, int>
        {
            ["Constitution"] = 12,
            ["Wisdom"] = 10,
            ["CurrentHealth"] = 100,
            ["MaxHealth"] = 120,
            ["CurrentMana"] = 15,
            ["MaxMana"] = 50,
        });
        var (handler, _, character) = Arrange(experience: 90, level: 1, attributes: attributes);

        await handler.Handle(
            new GainExperienceHubCommand { CharacterId = character.Id, Amount = 10 },
            CancellationToken.None);

        var stored = JsonSerializer.Deserialize<Dictionary<string, int>>(character.Attributes)!;
        stored["MaxHealth"].Should().Be(130);
        stored["MaxMana"].Should().Be(56);
    }

    [Fact]
    public async Task Handle_LevelUp_RebuildsACorruptAttributesBlobFromTheLevel()
    {
        var (handler, _, character) = Arrange(experience: 90, level: 1, attributes: "{not json");

        var result = await handler.Handle(
            new GainExperienceHubCommand { CharacterId = character.Id, Amount = 10 },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.LeveledUp.Should().BeTrue();

        var stored = JsonSerializer.Deserialize<Dictionary<string, int>>(character.Attributes)!;
        // No Constitution/Wisdom in the blob and no class row, so the engine defaults of 10 apply at the new level.
        stored["MaxHealth"].Should().Be(110);
        stored["MaxMana"].Should().Be(56);
    }

    [Fact]
    public async Task Handle_AwardInsideTheLevel_DoesNotRewriteTheAttributesBlob()
    {
        const string attributes = "{not json";
        var (handler, repo, character) = Arrange(experience: 30, level: 2, attributes: attributes);

        var result = await handler.Handle(
            new GainExperienceHubCommand { CharacterId = character.Id, Amount = 40 },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.LeveledUp.Should().BeFalse();
        character.Attributes.Should().Be(attributes, "only a level-up changes the derived pools");
        repo.Verify(r => r.UpdateAsync(character, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AwardInsideTheLevel_ReportsTheBankedPointsWithoutRewritingTheBlob()
    {
        const string attributes = "{\"UnspentAttributePoints\":4}";
        var (handler, _, character) = Arrange(experience: 30, level: 2, attributes: attributes);

        var result = await handler.Handle(
            new GainExperienceHubCommand { CharacterId = character.Id, Amount = 40 },
            CancellationToken.None);

        result.LeveledUp.Should().BeFalse();
        result.UnspentAttributePoints.Should().Be(4, "the result reports the balance the turn-in leaves banked");
        character.Attributes.Should().Be(attributes, "an award inside the level does not touch the blob");
    }

    [Fact]
    public async Task Handle_LevelUp_ReportsTheBankedPointsThePersistedBlobCarries()
    {
        var attributes = JsonSerializer.Serialize(new Dictionary<string, int>
        {
            ["UnspentAttributePoints"] = 4,
        });
        var (handler, _, character) = Arrange(experience: 90, level: 1, attributes: attributes);

        var result = await handler.Handle(
            new GainExperienceHubCommand { CharacterId = character.Id, Amount = 10 },
            CancellationToken.None);

        result.LeveledUp.Should().BeTrue();
        var stored = JsonSerializer.Deserialize<Dictionary<string, int>>(character.Attributes)!;
        stored.Should().ContainKey("UnspentAttributePoints");
        // The client budgets the allocation panel from this number, so it has to be the post-award balance the
        // blob actually holds — a pre-award snapshot would leave a level's banked points invisible.
        result.UnspentAttributePoints.Should().Be(stored["UnspentAttributePoints"]);
    }

    [Fact]
    public async Task Handle_CorruptAttributesBlob_ReportsNoBankedPoints()
    {
        var (handler, _, character) = Arrange(experience: 30, level: 2, attributes: "{not json");
        character.Attributes.Should().Be("{not json");

        var result = await handler.Handle(
            new GainExperienceHubCommand { CharacterId = character.Id, Amount = 40 },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.UnspentAttributePoints.Should().Be(0, "an unreadable blob carries no balance");
    }

    [Fact]
    public async Task Handle_StampsTheLastPlayedTime()
    {
        var (handler, _, character) = Arrange(experience: 0, level: 3);
        character.LastPlayedAt = DateTimeOffset.UtcNow.AddDays(-7);
        var before = character.LastPlayedAt;

        var result = await handler.Handle(
            new GainExperienceHubCommand { CharacterId = character.Id, Amount = 5, Source = null },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.NewLevel.Should().Be(3);
        result.NewExperience.Should().Be(5);
        character.LastPlayedAt.Should().BeAfter(before);
    }

    // Attribute points: the engine's own LevelUp() hands the player points to spend. Nothing in Veldrath.Server
    // incremented the blob key before this, so the field stayed at zero for a character's whole life and
    // AllocateAttributePoints could only ever answer "not enough points".

    [Fact]
    public async Task Handle_LevelUp_BanksTheAttributePointsTheEngineAwards()
    {
        var (handler, _, character) = Arrange(experience: 90, level: 1);

        await handler.Handle(
            new GainExperienceHubCommand { CharacterId = character.Id, Amount = 10 },
            CancellationToken.None);

        var stored = JsonSerializer.Deserialize<Dictionary<string, int>>(character.Attributes)!;
        stored["UnspentAttributePoints"].Should().Be(3, "reaching level 2 is worth the engine's 3 attribute points");
        // The pools still move in the same write: banking points must not cost the level-up its derived stats.
        stored["MaxHealth"].Should().Be(110);
    }

    [Fact]
    public async Task Handle_MultiLevelAward_BanksPointsForEveryLevelCrossedAndKeepsTheBalance()
    {
        // A character that already banked 4 points (a previous level-up nobody has spent yet) crosses 1 → 4.
        var attributes = JsonSerializer.Serialize(new Dictionary<string, int>
        {
            ["UnspentAttributePoints"] = 4,
            ["Strength"] = 10,
        });
        var (handler, _, character) = Arrange(experience: 0, level: 1, attributes: attributes);

        // 1 → 2 costs 100, 2 → 3 costs 200, 3 → 4 costs 300.
        var result = await handler.Handle(
            new GainExperienceHubCommand { CharacterId = character.Id, Amount = 600 },
            CancellationToken.None);

        result.NewLevel.Should().Be(4);
        var stored = JsonSerializer.Deserialize<Dictionary<string, int>>(character.Attributes)!;
        stored["UnspentAttributePoints"].Should().Be(13, "three levels at 3 points each, on top of the unspent 4");
    }

    [Fact]
    public async Task Handle_LevelUp_OnAFifthLevel_BanksTheBonusPoints()
    {
        var (handler, _, character) = Arrange(experience: 0, level: 4);

        // 4 → 5 costs 400.
        await handler.Handle(
            new GainExperienceHubCommand { CharacterId = character.Id, Amount = 400 },
            CancellationToken.None);

        var stored = JsonSerializer.Deserialize<Dictionary<string, int>>(character.Attributes)!;
        stored["UnspentAttributePoints"].Should().Be(5, "levels 5, 10, 15 … are worth 3 + 2 points");
    }

    [Fact]
    public async Task Handle_MultiLevelAward_PaysTheBonusForEveryFifthLevelItCrosses()
    {
        var (handler, _, character) = Arrange(experience: 0, level: 3);

        // 3 → 4 costs 300, 4 → 5 costs 400, 5 → 6 costs 500.
        await handler.Handle(
            new GainExperienceHubCommand { CharacterId = character.Id, Amount = 1200 },
            CancellationToken.None);

        var stored = JsonSerializer.Deserialize<Dictionary<string, int>>(character.Attributes)!;
        stored["UnspentAttributePoints"].Should().Be(11, "levels 4 and 6 are worth 3, level 5 is worth 5");
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    public async Task Handle_LevelUp_BanksExactlyWhatTheEngineLevelUpBanks(int newLevel)
    {
        // The engine is the author of the award; the server must not become a second opinion on it. Drive a real
        // engine level-up for the same level and compare. If either rate changes (3 per level, +2 every fifth) this
        // reddens rather than drifting silently.
        var engine = new SharedModels.Character { Level = newLevel - 1, Experience = 0 };
        engine.GainExperience((newLevel - 1) * 100);
        engine.Level.Should().Be(newLevel, "one engine level-up per test level");

        var (handler, _, character) = Arrange(experience: 0, level: newLevel - 1);
        await handler.Handle(
            new GainExperienceHubCommand { CharacterId = character.Id, Amount = (newLevel - 1) * 100 },
            CancellationToken.None);

        var stored = JsonSerializer.Deserialize<Dictionary<string, int>>(character.Attributes)!;
        stored["UnspentAttributePoints"].Should().Be(
            engine.UnspentAttributePoints, "the server banks the same points the engine's LevelUp() awards");
    }

    [Fact]
    public async Task Handle_LevelUp_BanksPointsTheAllocateAttributePointsCommandCanSpend()
    {
        // The end of the story the card was filed about: a level-up has to leave the character able to allocate.
        var attributes = JsonSerializer.Serialize(new Dictionary<string, int> { ["Strength"] = 10 });
        var (handler, repo, character) = Arrange(experience: 90, level: 1, attributes: attributes);

        await handler.Handle(
            new GainExperienceHubCommand { CharacterId = character.Id, Amount = 10 },
            CancellationToken.None);

        var allocate = new AllocateAttributePointsHubCommandHandler(
            repo.Object, NullLogger<AllocateAttributePointsHubCommandHandler>.Instance);

        var result = await allocate.Handle(
            new AllocateAttributePointsHubCommand
            {
                CharacterId = character.Id,
                Allocations = new Dictionary<string, int> { ["Strength"] = 3 },
            },
            CancellationToken.None);

        result.Success.Should().BeTrue("the level-up banked exactly the 3 points this allocation spends");
        result.PointsSpent.Should().Be(3);
        result.RemainingPoints.Should().Be(0);
        result.NewAttributes["Strength"].Should().Be(13);
        repo.Verify(r => r.UpdateAsync(character, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
