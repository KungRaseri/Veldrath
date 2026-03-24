using Moq;
using RealmEngine.Core.Features.Progression.Queries;
using RealmEngine.Core.Features.Progression.Services;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Progression.Queries;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for GetLearnableSpellsHandler.
/// </summary>
public class GetLearnableSpellsHandlerTests
{
    private static async Task<GetLearnableSpellsHandler> CreateHandlerAsync(IEnumerable<Power>? spells = null)
    {
        var mockRepo = new Mock<IPowerRepository>();
        mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync((spells ?? []).ToList());
        var spellDataSvc = new PowerDataService(mockRepo.Object);
        await spellDataSvc.InitializeAsync();
        return new GetLearnableSpellsHandler(spellDataSvc);
    }

    private static Power MakeSpell(string id, MagicalTradition tradition = MagicalTradition.Arcane, int minSkillRank = 0) =>
        new()
        {
            Id = id,
            Name = id,
            DisplayName = id,
            Description = id,
            Tradition = tradition,
            Rank = 1,
            MinimumSkillRank = minSkillRank,
            ManaCost = 10
        };

    [Fact]
    public async Task Returns_EmptyList_WhenCatalogIsEmpty()
    {
        // Arrange
        var handler = await CreateHandlerAsync([]);
        var character = new Character { Name = "Hero" };
        var query = new GetLearnableSpellsQuery { Character = character };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Spells.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Returns_EmptyList_WhenCharacterHasNoTraditionSkills()
    {
        // Arrange - several spells in catalog but character has no magic skills
        var handler = await CreateHandlerAsync([
            MakeSpell("fireball", MagicalTradition.Arcane),
            MakeSpell("heal", MagicalTradition.Divine)
        ]);
        var character = new Character { Name = "Fighter" }; // no magic skills

        var query = new GetLearnableSpellsQuery { Character = character };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Spells.Should().BeEmpty("character with no magic skills can't learn any spells");
    }

    [Fact]
    public async Task Returns_OnlySpells_ForTraditionsCharacterKnows()
    {
        // Arrange - two traditions; character only has arcane skill
        var handler = await CreateHandlerAsync([
            MakeSpell("fireball", MagicalTradition.Arcane),
            MakeSpell("holy-smite", MagicalTradition.Divine)
        ]);
        var character = new Character { Name = "Mage" };
        character.Skills["arcane"] = new CharacterSkill { SkillId = "arcane", Name = "Arcane", Category = "magic", CurrentRank = 10, XPToNextRank = 300 };

        var query = new GetLearnableSpellsQuery { Character = character };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Spells.Should().HaveCount(1);
        result.Spells[0].Id.Should().Be("fireball");
    }

    [Fact]
    public async Task Excludes_SpellsBeyondSkillTolerance()
    {
        // Arrange - tolerance: minSkillRank <= characterRank + 10
        // Character has arcane rank 5 → can learn up to minSkillRank 15
        var handler = await CreateHandlerAsync([
            MakeSpell("spark", minSkillRank: 0),         // learnable (0 <= 15)
            MakeSpell("magic-bolt", minSkillRank: 15),   // learnable (15 <= 15)
            MakeSpell("meteor", minSkillRank: 16)        // too high (16 > 15)
        ]);
        var character = new Character { Name = "Mage" };
        character.Skills["arcane"] = new CharacterSkill { SkillId = "arcane", Name = "Arcane", Category = "magic", CurrentRank = 5, XPToNextRank = 300 };

        var query = new GetLearnableSpellsQuery { Character = character };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Spells.Should().HaveCount(2);
        result.Spells.Select(s => s.Id).Should().BeEquivalentTo(["spark", "magic-bolt"]);
    }

    [Fact]
    public async Task FiltersBy_Tradition_WhenTraditionSpecified()
    {
        // Arrange - character knows both arcane and divine
        var handler = await CreateHandlerAsync([
            MakeSpell("fireball", MagicalTradition.Arcane),
            MakeSpell("ice-shard", MagicalTradition.Arcane),
            MakeSpell("heal", MagicalTradition.Divine)
        ]);
        var character = new Character { Name = "Battlemage" };
        character.Skills["arcane"] = new CharacterSkill { SkillId = "arcane", Name = "Arcane", Category = "magic", CurrentRank = 10, XPToNextRank = 300 };
        character.Skills["divine"] = new CharacterSkill { SkillId = "divine", Name = "Divine", Category = "magic", CurrentRank = 10, XPToNextRank = 300 };

        var query = new GetLearnableSpellsQuery { Character = character, Tradition = MagicalTradition.Arcane };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert - only arcane spells returned
        result.Spells.Should().HaveCount(2);
        result.Spells.All(s => s.Tradition == MagicalTradition.Arcane).Should().BeTrue();
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Returns_AllTraditionSpells_WhenNoTraditionFilter()
    {
        // Arrange - character knows arcane and divine
        var handler = await CreateHandlerAsync([
            MakeSpell("fireball", MagicalTradition.Arcane),
            MakeSpell("heal", MagicalTradition.Divine)
        ]);
        var character = new Character { Name = "Battlemage" };
        character.Skills["arcane"] = new CharacterSkill { SkillId = "arcane", Name = "Arcane", Category = "magic", CurrentRank = 10, XPToNextRank = 300 };
        character.Skills["divine"] = new CharacterSkill { SkillId = "divine", Name = "Divine", Category = "magic", CurrentRank = 10, XPToNextRank = 300 };

        var query = new GetLearnableSpellsQuery { Character = character }; // no Tradition filter

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert - both traditions appear
        result.Spells.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
    }

    [Theory]
    [InlineData(MagicalTradition.Arcane, "arcane")]
    [InlineData(MagicalTradition.Divine, "divine")]
    [InlineData(MagicalTradition.Occult, "occult")]
    [InlineData(MagicalTradition.Primal, "primal")]
    public async Task Returns_SpellForEachTradition_WhenCharacterHasSkill(MagicalTradition tradition, string skillId)
    {
        // Arrange
        var handler = await CreateHandlerAsync([MakeSpell("test-Power", tradition)]);
        var character = new Character { Name = "Scholar" };
        character.Skills[skillId] = new CharacterSkill { SkillId = skillId, Name = skillId, Category = "magic", CurrentRank = 5, XPToNextRank = 300 };

        var query = new GetLearnableSpellsQuery { Character = character };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Spells.Should().HaveCount(1, $"character with {skillId} skill should see {tradition} spells");
    }
}
