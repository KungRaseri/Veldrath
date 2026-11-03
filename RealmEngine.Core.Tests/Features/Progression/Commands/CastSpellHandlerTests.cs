using Moq;
using RealmEngine.Core.Features.Progression.Commands;
using RealmEngine.Core.Features.Progression.Services;
using RealmEngine.Core.Tests.Infrastructure;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Progression.Commands;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for CastSpellHandler.
/// </summary>
/// <remarks>
/// With a numeric <c>BaseEffectValue</c> (as every spell this suite builds uses) the only randomness
/// a cast reaches is the single <see cref="IRandomSource.NextDouble"/> draw in
/// <c>SpellCastingService.CheckCastSuccess</c>: <c>successRate = clamp(0.90 + 0.005 * (rank -
/// MinimumSkillRank), 0.60, 0.99)</c> and the cast succeeds when <c>roll &lt; successRate</c>. The
/// tests that assert a specific arm script that one roll with <see cref="FakeRandomSource"/> and
/// assert the draw count, instead of retrying until the wanted arm happens to land.
/// </remarks>
public class CastSpellHandlerTests
{
    /// <summary>Rank 50 against a rank-0 spell: <c>0.90 + 0.25 = 1.15</c>, clamped down to 0.99.</summary>
    private const double RollThatSucceeds = 0.989;

    /// <summary>Rank 0 against a rank-60 spell: <c>0.90 - 0.30</c>, clamped up to the 0.60 floor.</summary>
    private const double RollThatFizzles = 0.75;

    private static async Task<SpellCastingService> CreateSpellCastingServiceAsync(
        IEnumerable<Power>? powers = null,
        IEnumerable<SkillDefinition>? skills = null,
        IRandomSource? randomSource = null)
    {
        var powerMockRepo = new Mock<IPowerRepository>();
        powerMockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync((powers ?? []).ToList());
        var powerDataSvc = new PowerDataService(powerMockRepo.Object);
        await powerDataSvc.InitializeAsync();

        var skillMockRepo = new Mock<ISkillRepository>();
        skillMockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync((skills ?? []).ToList());
        var skillDataSvc = new SkillDataService(skillMockRepo.Object);
        await skillDataSvc.InitializeAsync();

        return new SpellCastingService(
            powerDataSvc,
            new SkillProgressionService(skillDataSvc),
            randomSource: randomSource);
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
        // Arrange - rank 0 vs MinimumSkillRank 60 → rankDifference = -60 →
        // 0.90 + (-60 × 0.005) = 0.60, clamped to the 0.60 floor. The roll must be >= 0.60 to fizzle,
        // so 0.75 lands squarely in the fizzle band without depending on float rounding at the boundary.
        var spell = MakeSpell("unstable-bolt", manaCost: 5, minSkillRank: 60);
        var random = FakeRandomSource.ReturningDoubles(RollThatFizzles);
        var service = await CreateSpellCastingServiceAsync([spell], [MakeSkillDef("arcane")], random);
        var handler = new CastSpellHandler(service);
        var character = CreateCasterWithSpell("unstable-bolt", rank: 0, mana: 200);
        var command = new CastSpellCommand { Caster = character, SpellId = "unstable-bolt" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert - one cast, one roll; the fizzle arm is asserted directly.
        random.DoubleCallCount.Should().Be(1, "a cast attempt makes exactly one success roll");
        random.RemainingDoubles.Should().Be(0);
        result.WasFizzle.Should().BeTrue();
        result.Success.Should().BeFalse();
        character.LearnedSpells["unstable-bolt"].TimesFizzled.Should().Be(1);
        character.LearnedSpells["unstable-bolt"].TimesCast.Should().Be(0);
    }

    [Fact]
    public async Task IncrementsTimesCast_OnSuccessfulCast()
    {
        // Arrange - rank 50 vs MinimumSkillRank 0 → 0.90 + 0.25 = 1.15, clamped to 0.99;
        // 0.989 < 0.99 succeeds, and would fizzle at the unclamped 0.90 base, so the arm also
        // proves the rank scaling applies.
        var spell = MakeSpell("spark", manaCost: 5);
        var random = FakeRandomSource.ReturningDoubles(RollThatSucceeds);
        var service = await CreateSpellCastingServiceAsync([spell], [MakeSkillDef("arcane")], random);
        var handler = new CastSpellHandler(service);
        var character = CreateCasterWithSpell("spark", rank: 50, mana: 200);
        var command = new CastSpellCommand { Caster = character, SpellId = "spark" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        random.DoubleCallCount.Should().Be(1, "a cast attempt makes exactly one success roll");
        random.RemainingDoubles.Should().Be(0);
        result.Success.Should().BeTrue();
        result.WasFizzle.Should().BeFalse();
        character.LearnedSpells["spark"].TimesCast.Should().Be(1);
        character.LearnedSpells["spark"].TimesFizzled.Should().Be(0);
    }

    [Fact]
    public async Task SetsCooldown_AfterSuccessfulCast()
    {
        // Arrange - cooldown >= 1 so it gets set; same 0.99 clamped success rate as above.
        var spell = MakeSpell("arcane-surge", manaCost: 5, cooldown: 2);
        var random = FakeRandomSource.ReturningDoubles(RollThatSucceeds);
        var service = await CreateSpellCastingServiceAsync([spell], [MakeSkillDef("arcane")], random);
        var handler = new CastSpellHandler(service);
        var character = CreateCasterWithSpell("arcane-surge", rank: 50, mana: 200);
        var command = new CastSpellCommand { Caster = character, SpellId = "arcane-surge" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        random.DoubleCallCount.Should().Be(1);
        random.RemainingDoubles.Should().Be(0);
        result.Success.Should().BeTrue();
        result.WasFizzle.Should().BeFalse();
        character.SpellCooldowns.Should().ContainKey("arcane-surge");
        character.SpellCooldowns["arcane-surge"].Should().Be(2);
    }
}
