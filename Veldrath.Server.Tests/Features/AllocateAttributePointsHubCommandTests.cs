using System.Text.Json;
using FluentAssertions;
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
/// Facts for <see cref="AllocateAttributePointsHubCommandHandler"/>: an allocation moves the attribute and the
/// unspent balance, and deliberately leaves the derived pools alone. The stored <c>MaxHealth</c>/<c>MaxMana</c> are
/// the class curve <see cref="CharacterProgression"/> writes on a level-up, and that curve reads no attribute, so a
/// Constitution point cannot move the health bar. These facts pin the decision recorded in the handler's remarks
/// and in <c>.github/agent-memory/areas/character-attributes-json-blob-schema.md</c>; if the pool model changes, they
/// redden on purpose instead of the behaviour drifting silently.
/// </summary>
public class AllocateAttributePointsHubCommandTests
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

    /// <summary>
    /// A level-2 warrior's blob as a level-up leaves it: the class curve's 132 health and 22 mana, the current pools
    /// the fight left at 120/20, and 3 banked points.
    /// </summary>
    private static Dictionary<string, int> LevelTwoWarriorBlob() => new()
    {
        ["UnspentAttributePoints"] = 3,
        ["Strength"] = 10,
        ["Dexterity"] = 10,
        ["Constitution"] = 10,
        ["Intelligence"] = 10,
        ["Wisdom"] = 10,
        ["Charisma"] = 10,
        ["CurrentHealth"] = 120,
        ["MaxHealth"] = 132,
        ["CurrentMana"] = 20,
        ["MaxMana"] = 22,
    };

    private static (AllocateAttributePointsHubCommandHandler Handler, Mock<ICharacterRepository> Repo, Character Character)
        Arrange(Dictionary<string, int> attributes, int level = 2)
    {
        var character = new Character
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            Name = "TestHero",
            ClassName = "Warrior",
            SlotIndex = 1,
            Level = level,
            Experience = 0,
            Attributes = JsonSerializer.Serialize(attributes),
        };

        var repo = new Mock<ICharacterRepository>();
        repo.Setup(r => r.GetByIdAsync(character.Id, It.IsAny<CancellationToken>())).ReturnsAsync(character);
        repo.Setup(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new AllocateAttributePointsHubCommandHandler(
            repo.Object, NullLogger<AllocateAttributePointsHubCommandHandler>.Instance);

        return (handler, repo, character);
    }

    private static Dictionary<string, int> Stored(Character character) =>
        JsonSerializer.Deserialize<Dictionary<string, int>>(character.Attributes)!;

    [Fact]
    public async Task Handle_AllocatingConstitution_MovesTheAttributeAndLeavesTheStoredPoolsAlone()
    {
        var blob = LevelTwoWarriorBlob();
        var (handler, _, character) = Arrange(blob);

        var result = await handler.Handle(
            new AllocateAttributePointsHubCommand
            {
                CharacterId = character.Id,
                Allocations = new Dictionary<string, int> { ["Constitution"] = 3 },
            },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.PointsSpent.Should().Be(3);
        result.RemainingPoints.Should().Be(0);

        // The whole blob is compared, not just the pools: any key an allocation starts rewriting shows up here.
        var expected = LevelTwoWarriorBlob();
        expected["UnspentAttributePoints"] = 0;
        expected["Constitution"] = 13;
        Stored(character).Should().Equal(expected);

        var stored = Stored(character);
        stored["MaxHealth"].Should().Be(
            132, "the stored pool is the warrior class curve (120 + 12 at level 2), not Constitution × 10 + Level × 5 (140)");
        stored["CurrentHealth"].Should().Be(120, "an allocation never refills the current pool");
    }

    [Fact]
    public async Task Handle_AllocatingWisdom_MovesTheAttributeAndLeavesTheStoredPoolsAlone()
    {
        var (handler, _, character) = Arrange(LevelTwoWarriorBlob());

        var result = await handler.Handle(
            new AllocateAttributePointsHubCommand
            {
                CharacterId = character.Id,
                Allocations = new Dictionary<string, int> { ["Wisdom"] = 3 },
            },
            CancellationToken.None);

        result.Success.Should().BeTrue();

        var expected = LevelTwoWarriorBlob();
        expected["UnspentAttributePoints"] = 0;
        expected["Wisdom"] = 13;
        Stored(character).Should().Equal(expected);

        var stored = Stored(character);
        stored["MaxMana"].Should().Be(
            22, "the stored pool is the warrior class curve (20 + 2 at level 2), not Wisdom × 5 + Level × 3 (71)");
        stored["CurrentMana"].Should().Be(20, "an allocation never refills the current pool");
    }

    [Fact]
    public async Task Handle_AfterALevelUp_AllocatingConstitutionKeepsTheLevelUpsClassCurvePool()
    {
        // The story the card was filed about: level, bank 3 points, put them into Constitution. The bar does not
        // move, and that is the decision — the attribute is a combat modifier, the pool is the class curve.
        var start = new Dictionary<string, int>
        {
            ["Strength"] = 10,
            ["Dexterity"] = 10,
            ["Constitution"] = 10,
            ["Intelligence"] = 10,
            ["Wisdom"] = 10,
            ["Charisma"] = 10,
            ["CurrentHealth"] = 120,
            ["MaxHealth"] = 120,
            ["CurrentMana"] = 20,
            ["MaxMana"] = 20,
        };
        var (_, repo, character) = Arrange(start, level: 1);
        character.Experience = 90;

        var classRepo = new Mock<ICharacterClassRepository>();
        classRepo.Setup(r => r.GetByName(It.IsAny<string>())).Returns(WarriorClass());
        var gainExperience = new GainExperienceHubCommandHandler(
            repo.Object, classRepo.Object, NullLogger<GainExperienceHubCommandHandler>.Instance);

        // Level 1 → 2 on 10 XP: the level-up writes the class curve's 132/22 and banks 3 points.
        await gainExperience.Handle(
            new GainExperienceHubCommand { CharacterId = character.Id, Amount = 10 },
            CancellationToken.None);

        var afterLevelUp = Stored(character);
        afterLevelUp["MaxHealth"].Should().Be(132);
        afterLevelUp["UnspentAttributePoints"].Should().Be(3);

        var allocate = new AllocateAttributePointsHubCommandHandler(
            repo.Object, NullLogger<AllocateAttributePointsHubCommandHandler>.Instance);

        var result = await allocate.Handle(
            new AllocateAttributePointsHubCommand
            {
                CharacterId = character.Id,
                Allocations = new Dictionary<string, int> { ["Constitution"] = 3 },
            },
            CancellationToken.None);

        result.Success.Should().BeTrue("the level-up banked exactly the 3 points this allocation spends");
        result.RemainingPoints.Should().Be(0);

        var stored = Stored(character);
        stored["Constitution"].Should().Be(13);
        stored["MaxHealth"].Should().Be(132, "allocating Constitution does not rewrite a pool the class curve owns");
        stored["CurrentHealth"].Should().Be(120, "the raised-by-allocation fantasy would also be a free heal");

        // The level-up and the allocation are two separate writes: the allocation does not re-persist a recompute.
        repo.Verify(r => r.UpdateAsync(character, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
