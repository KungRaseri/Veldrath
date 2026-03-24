using Moq;
using RealmEngine.Core.Features.Progression.Commands;
using RealmEngine.Core.Features.Progression.Services;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Progression.Commands;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for CastSpellHandler.
/// </summary>
public class CastSpellHandlerTests
{
    private static async Task<SpellCastingService> CreateSpellCastingServiceAsync(
        IEnumerable<Power>? powers = null,
        IEnumerable<SkillDefinition>? skills = null)
    {
        var powerMockRepo = new Mock<IPowerRepository>();
        powerMockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync((powers ?? []).ToList());
        var powerDataSvc = new PowerDataService(powerMockRepo.Object);
        await powerDataSvc.InitializeAsync();

        var skillMockRepo = new Mock<ISkillRepository>();
        skillMockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync((skills ?? []).ToList());
        var skillDataSvc = new SkillDataService(skillMockRepo.Object);
        await skillDataSvc.InitializeAsync();

        return new SpellCastingService(powerDataSvc, new SkillProgressionService(skillDataSvc));
    }

    private static Power MakeSpell(string id, int manaCost = 10, int minSkillRank = 0, int cooldown = 0) =>
        new()
        {
            Id = id,
            Name = id,
            DisplayName = id,
            Description = id,
            Tradition = MagicalTradition.Arcane,
            Rank = 0,
            MinimumSkillRank = minSkillRank,
            ManaCost = manaCost,
            Cooldown = cooldown,
            BaseEffectValue = "5"
        };

    private static SkillDefinition MakeSkillDef(string id) =>
        new() { SkillId = id, Name = id, DisplayName = id, Description = id, Category = "magic", BaseXPCost = 10000 };

    /// <summary>Sets up a character who knows the spell and has the arcane skill.</summary>
    private static Character CreateCasterWithSpell(string spellId, int rank = 50, int mana = 200, int maxMana = 200)
    {
        var character = new Character { Name = "Mage", Mana = mana, MaxMana = maxMana };
        character.LearnedSpells[spellId] = new CharacterSpell { SpellId = spellId };
        character.Skills["arcane"] = new CharacterSkill
        {
            SkillId = "arcane",
            Name = "Arcane",
            Category = "magic",
            CurrentRank = rank,
            XPToNextRank = 1_000_000  // prevent rank-ups in tests
        };
        return character;
    }

    [Fact]
    public async Task Returns_Failure_WhenSpellNotKnown()
    {
        // Arrange
        var service = await CreateSpellCastingServiceAsync([MakeSpell("fireball")]);
        var handler = new CastSpellHandler(service);
        var character = new Character { Name = "Mage" }; // no learned spells
        var command = new CastSpellCommand { Caster = character, SpellId = "fireball" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("don't know");
    }

    [Fact]
    public async Task Returns_Failure_WhenSpellOnCooldown()
    {
        // Arrange
        var service = await CreateSpellCastingServiceAsync([MakeSpell("ice-lance", cooldown: 3)]);
        var handler = new CastSpellHandler(service);
        var character = CreateCasterWithSpell("ice-lance");
        character.SpellCooldowns["ice-lance"] = 2; // 2 turns remaining

        var command = new CastSpellCommand { Caster = character, SpellId = "ice-lance" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("cooling down");
        result.Message.Should().Contain("2");
    }

    [Fact]
    public async Task Returns_Failure_WhenNotEnoughMana()
    {
        // Arrange
        var service = await CreateSpellCastingServiceAsync([MakeSpell("meteor", manaCost: 100)]);
        var handler = new CastSpellHandler(service);
        var character = CreateCasterWithSpell("meteor", mana: 30, maxMana: 200); // only 30 mana

        var command = new CastSpellCommand { Caster = character, SpellId = "meteor" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("mana");
    }

    [Fact]
    public async Task Returns_Failure_WhenNoTraditionSkill()
    {
        // Arrange - character knows the spell but has no arcane skill
        var service = await CreateSpellCastingServiceAsync([MakeSpell("force-bolt")]);
        var handler = new CastSpellHandler(service);
        var character = new Character { Name = "Mage", Mana = 100 };
        character.LearnedSpells["force-bolt"] = new CharacterSpell { SpellId = "force-bolt" };
        // Note: no "arcane" entry in character.Skills

        var command = new CastSpellCommand { Caster = character, SpellId = "force-bolt" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Arcane");
    }

    [Fact]
    public async Task DeductsMana_WhenCastAttempted_RegardlessOfFizzle()
    {
        // Arrange - spell costs 15 mana; set up so we will get at least one cast attempt
        var spell = MakeSpell("energy-bolt", manaCost: 15);
        var service = await CreateSpellCastingServiceAsync([spell], [MakeSkillDef("arcane")]);
        var handler = new CastSpellHandler(service);

        // Character at rank 50 vs minSkillRank 0 → success rate 99%; cast multiple times
        // At least one cast will consume mana
        var character = CreateCasterWithSpell("energy-bolt", rank: 50, mana: 200);
        var initialMana = character.Mana;
        var command = new CastSpellCommand { Caster = character, SpellId = "energy-bolt" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert - mana is always consumed once validation passes
        character.Mana.Should().BeLessThan(initialMana, "mana must be deducted after passing validation");
        result.ManaCostPaid.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task IncrementsFizzleCount_WhenSpellFizzles()
    {
        // Arrange - use rank 0 vs minSkillRank 60 → rankDiff = -60 → success rate = 60%
        // At 40% fizzle rate across 50 attempts, P(zero fizzles) = 0.6^50 ≈ 1.3e-11 ≈ 0
        var spell = MakeSpell("unstable-bolt", manaCost: 5, minSkillRank: 60);
        var service = await CreateSpellCastingServiceAsync([spell], [MakeSkillDef("arcane")]);
        var handler = new CastSpellHandler(service);

        for (int attempt = 0; attempt < 50; attempt++)
        {
            var character = CreateCasterWithSpell("unstable-bolt", rank: 0, mana: 200);
            var command = new CastSpellCommand { Caster = character, SpellId = "unstable-bolt" };

            var result = await handler.Handle(command, CancellationToken.None);

            if (result.WasFizzle)
            {
                // Assert fizzle increments the counter
                character.LearnedSpells["unstable-bolt"].TimesFizzled.Should().Be(1);
                return;
            }
        }

        Assert.Fail("Expected at least one fizzle in 50 attempts at 60% success rate");
    }

    [Fact]
    public async Task IncrementsTimesCast_OnSuccessfulCast()
    {
        // Arrange - rank 50 vs minSkillRank 0 → 99% success rate
        var spell = MakeSpell("spark", manaCost: 5);
        var service = await CreateSpellCastingServiceAsync([spell], [MakeSkillDef("arcane")]);
        var handler = new CastSpellHandler(service);

        for (int attempt = 0; attempt < 50; attempt++)
        {
            var character = CreateCasterWithSpell("spark", rank: 50, mana: 200);
            var command = new CastSpellCommand { Caster = character, SpellId = "spark" };

            var result = await handler.Handle(command, CancellationToken.None);

            if (result.Success && !result.WasFizzle)
            {
                // Assert - TimesCast should be 1 after first successful cast
                character.LearnedSpells["spark"].TimesCast.Should().Be(1);
                return;
            }
        }

        Assert.Fail("Expected at least one successful cast in 50 attempts at 99% success rate");
    }

    [Fact]
    public async Task SetsCooldown_AfterSuccessfulCast()
    {
        // Arrange - cooldown >= 1 so it gets set
        var spell = MakeSpell("arcane-surge", manaCost: 5, cooldown: 2);
        var service = await CreateSpellCastingServiceAsync([spell], [MakeSkillDef("arcane")]);
        var handler = new CastSpellHandler(service);

        for (int attempt = 0; attempt < 50; attempt++)
        {
            var character = CreateCasterWithSpell("arcane-surge", rank: 50, mana: 200);
            var command = new CastSpellCommand { Caster = character, SpellId = "arcane-surge" };

            var result = await handler.Handle(command, CancellationToken.None);

            if (result.Success && !result.WasFizzle)
            {
                character.SpellCooldowns.Should().ContainKey("arcane-surge");
                character.SpellCooldowns["arcane-surge"].Should().Be(2);
                return;
            }
        }

        Assert.Fail("Expected at least one successful cast in 50 attempts at 99% success rate");
    }
}
