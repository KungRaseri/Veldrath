using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Progression.Services;
using RealmEngine.Core.Tests.Infrastructure;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Progression.Services;

/// <summary>
/// Covers <see cref="SpellCastingService"/>'s effect resolution and cooldown lifecycle: tradition-less
/// powers, missing catalog entries, damage/heal/buff/debuff/default effects, dice parsing, the XP table
/// per spell rank, cooldown application and decrementing.
/// </summary>
/// <remarks>
/// Success is scripted through <see cref="FakeRandomSource"/>: rank 50 against a rank-0 spell clamps the
/// success rate to 0.99, so a 0.0 roll always succeeds; each dice term consumes one
/// <see cref="IRandomSource.Next(int,int)"/> draw, which the tests script explicitly.
/// </remarks>
[Trait("Category", "Service")]
public class SpellCastingServiceEffectsTests
{
    private static async Task<SpellCastingService> CreateServiceAsync(
        IEnumerable<Power>? powers = null,
        IRandomSource? randomSource = null)
    {
        var powerRepository = new Mock<IPowerRepository>();
        powerRepository.Setup(r => r.GetAllAsync()).ReturnsAsync((powers ?? []).ToList());
        var powerCatalog = new PowerDataService(powerRepository.Object);
        await powerCatalog.InitializeAsync();

        var skillRepository = new Mock<ISkillRepository>();
        skillRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(
        [
            new SkillDefinition
            {
                SkillId = "arcane",
                Name = "Arcane",
                DisplayName = "Arcane",
                Description = "Arcane magic",
                Category = "magic",
                BaseXPCost = 10000
            }
        ]);
        var skillCatalog = new SkillDataService(skillRepository.Object);
        await skillCatalog.InitializeAsync();

        return new SpellCastingService(
            powerCatalog,
            new SkillProgressionService(skillCatalog),
            NullLogger<SpellCastingService>.Instance,
            randomSource ?? FakeRandomSource.ReturningDoubles(0.0));
    }

    private static Power MakeSpell(
        string id,
        MagicalTradition? tradition = MagicalTradition.Arcane,
        PowerEffectType effectType = PowerEffectType.Damage,
        string? baseEffectValue = "10",
        int manaCost = 10,
        int minimumSkillRank = 0,
        int cooldown = 0,
        int rank = 0) => new()
        {
            Id = id,
            Name = id,
            DisplayName = id,
            Description = id,
            Tradition = tradition,
            EffectType = effectType,
            BaseEffectValue = baseEffectValue,
            ManaCost = manaCost,
            MinimumSkillRank = minimumSkillRank,
            Cooldown = cooldown,
            Rank = rank,
            DamageType = "fire"
        };

    private static Character CreateCaster(int rank = 50, int mana = 200)
    {
        var character = new Character
        {
            Name = "Mage",
            Mana = mana,
            MaxMana = 200,
            Health = 55,
            Constitution = 10,
            Level = 1
        };
        character.Skills["arcane"] = new CharacterSkill
        {
            SkillId = "arcane",
            Name = "Arcane",
            Category = "magic",
            CurrentRank = rank,
            XPToNextRank = 1_000_000
        };
        return character;
    }

    // ── Learning ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task LearnSpell_WithUnknownId_Fails()
    {
        var service = await CreateServiceAsync();

        var result = service.LearnSpell(new Character { Name = "Mage" }, "missing");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Unknown spell");
    }

    [Fact]
    public async Task LearnSpell_WithTraditionLessPower_Fails()
    {
        var service = await CreateServiceAsync([MakeSpell("wild-magic", tradition: null)]);

        var result = service.LearnSpell(new Character { Name = "Mage" }, "wild-magic");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("no tradition");
    }

    [Fact]
    public async Task LearnSpell_WithoutTheTraditionSkill_Fails()
    {
        var service = await CreateServiceAsync([MakeSpell("fireball")]);

        var result = service.LearnSpell(new Character { Name = "Mage" }, "fireball");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("need Arcane magic skill");
    }

    [Fact]
    public async Task LearnSpell_WithTooLowARank_Fails()
    {
        var service = await CreateServiceAsync([MakeSpell("meteor", minimumSkillRank: 90)]);
        var caster = CreateCaster(rank: 5);

        var result = service.LearnSpell(caster, "meteor");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("too low");
        caster.LearnedSpells.Should().NotContainKey("meteor");
    }

    [Fact]
    public async Task LearnSpell_Succeeds_AndRecordsTheSpell()
    {
        var service = await CreateServiceAsync([MakeSpell("fireball")]);
        var caster = CreateCaster();

        var result = service.LearnSpell(caster, "fireball");

        result.Success.Should().BeTrue();
        result.SpellLearned!.Id.Should().Be("fireball");
        caster.LearnedSpells.Should().ContainKey("fireball");
        caster.LearnedSpells["fireball"].TimesCast.Should().Be(0);
    }

    // ── Cast guards ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CastSpell_WhenPowerIsMissingFromTheCatalog_Fails()
    {
        var service = await CreateServiceAsync();
        var caster = CreateCaster();
        caster.LearnedSpells["ghost-spell"] = new CharacterSpell { SpellId = "ghost-spell" };

        var result = service.CastSpell(caster, "ghost-spell");

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Spell not found!");
    }

    [Fact]
    public async Task CastSpell_WhenThePowerHasNoTradition_Fails()
    {
        var service = await CreateServiceAsync([MakeSpell("wild-magic", tradition: null)]);
        var caster = CreateCaster();
        caster.LearnedSpells["wild-magic"] = new CharacterSpell { SpellId = "wild-magic" };

        var result = service.CastSpell(caster, "wild-magic");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("no tradition");
    }

    [Fact]
    public async Task CastSpell_WithoutTheTraditionSkill_Fails()
    {
        var service = await CreateServiceAsync([MakeSpell("fireball")]);
        var caster = CreateCaster();
        caster.Skills.Clear();
        caster.LearnedSpells["fireball"] = new CharacterSpell { SpellId = "fireball" };

        var result = service.CastSpell(caster, "fireball");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("lack the Arcane magic skill");
    }

    [Fact]
    public async Task CastSpell_WithoutEnoughMana_Fails_AndSpendsNothing()
    {
        var service = await CreateServiceAsync([MakeSpell("fireball", manaCost: 50)]);
        var caster = CreateCaster(mana: 10);
        caster.LearnedSpells["fireball"] = new CharacterSpell { SpellId = "fireball" };

        var result = service.CastSpell(caster, "fireball");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Not enough mana");
        caster.Mana.Should().Be(10);
    }

    // ── Effects ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CastSpell_DamageEffect_DamagesTheTarget()
    {
        var service = await CreateServiceAsync(
            [MakeSpell("fireball", baseEffectValue: "25")],
            FakeRandomSource.ReturningDoubles(0.0));
        var caster = CreateCaster();
        caster.LearnedSpells["fireball"] = new CharacterSpell { SpellId = "fireball" };
        var target = new Character { Name = "Goblin", Health = 100 };

        var result = service.CastSpell(caster, "fireball", target);

        result.Success.Should().BeTrue();
        target.Health.Should().Be(65);
        result.Message.Should().Be("Dealt 35 fire damage to Goblin!");
        result.EffectValue.Should().Be("35", "numeric effect values gain +1 per 5 skill ranks (rank 50 => +10)");
    }

    [Fact]
    public async Task CastSpell_DamageEffect_RollsDiceNotation()
    {
        // One success roll, then two d6 draws of 3 and 4.
        var service = await CreateServiceAsync(
            [MakeSpell("shadow-bolt", baseEffectValue: "2d6")],
            FakeRandomSource.ReturningDoubles(0.0).Enqueue(3, 4));
        var caster = CreateCaster();
        caster.LearnedSpells["shadow-bolt"] = new CharacterSpell { SpellId = "shadow-bolt" };
        var target = new Character { Name = "Goblin", Health = 100 };

        var result = service.CastSpell(caster, "shadow-bolt", target);

        result.Success.Should().BeTrue();
        target.Health.Should().Be(93);
        result.Message.Should().Contain("7");
    }

    [Fact]
    public async Task CastSpell_DamageEffect_WithoutATarget_ReportsNoTarget()
    {
        var service = await CreateServiceAsync([MakeSpell("fireball")]);
        var caster = CreateCaster();
        caster.LearnedSpells["fireball"] = new CharacterSpell { SpellId = "fireball" };

        var result = service.CastSpell(caster, "fireball");

        result.Success.Should().BeTrue();
        result.Message.Should().Be("No valid target!");
    }

    [Fact]
    public async Task CastSpell_WithMalformedEffectValue_FallsBackToTenDamage()
    {
        var service = await CreateServiceAsync([MakeSpell("chaos-bolt", baseEffectValue: "abc-nonsense")]);
        var caster = CreateCaster();
        caster.LearnedSpells["chaos-bolt"] = new CharacterSpell { SpellId = "chaos-bolt" };
        var target = new Character { Name = "Goblin", Health = 100 };

        var result = service.CastSpell(caster, "chaos-bolt", target);

        target.Health.Should().Be(90, "unparseable effect values deal the documented default of 10");
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task CastSpell_WithUnparseableDiceNotation_FallsBackToTenDamage()
    {
        var service = await CreateServiceAsync([MakeSpell("broken-dice", baseEffectValue: "3dZ")]);
        var caster = CreateCaster();
        caster.LearnedSpells["broken-dice"] = new CharacterSpell { SpellId = "broken-dice" };
        var target = new Character { Name = "Goblin", Health = 100 };

        var result = service.CastSpell(caster, "broken-dice", target);

        result.Success.Should().BeTrue();
        target.Health.Should().Be(90, "dice that fail to parse deal the documented default of 10");
    }

    [Fact]
    public async Task CastSpell_HealEffect_RestoresAndClampsAtMaxHealth()
    {
        var service = await CreateServiceAsync(
            [MakeSpell("mend", effectType: PowerEffectType.Heal, baseEffectValue: "999")]);
        var caster = CreateCaster();
        caster.Health = 55; // GetMaxHealth = Constitution 10 * 10 + Level 1 * 5 = 105
        caster.LearnedSpells["mend"] = new CharacterSpell { SpellId = "mend" };

        var result = service.CastSpell(caster, "mend", caster);

        result.Success.Should().BeTrue();
        caster.Health.Should().Be(105);
        result.Message.Should().Be("Restored 50 health!");
    }

    [Fact]
    public async Task CastSpell_BuffEffect_ReportsTheBuff()
    {
        var service = await CreateServiceAsync([MakeSpell("haste", effectType: PowerEffectType.Buff)]);
        var caster = CreateCaster();
        caster.LearnedSpells["haste"] = new CharacterSpell { SpellId = "haste" };

        var result = service.CastSpell(caster, "haste");

        result.Message.Should().Be("Applied haste buff!");
    }

    [Fact]
    public async Task CastSpell_DebuffEffect_RequiresATarget()
    {
        var service = await CreateServiceAsync(
            [MakeSpell("curse", effectType: PowerEffectType.Debuff)],
            FakeRandomSource.ReturningDoubles(0.0, 0.0));
        var caster = CreateCaster();
        caster.LearnedSpells["curse"] = new CharacterSpell { SpellId = "curse" };

        var withoutTarget = service.CastSpell(caster, "curse");
        var withTarget = service.CastSpell(caster, "curse", new Character { Name = "Goblin" });

        withoutTarget.Message.Should().Be("No valid target!");
        withTarget.Message.Should().Be("Applied curse debuff to Goblin!");
    }

    [Fact]
    public async Task CastSpell_UncategorisedEffect_ReportsTheCast()
    {
        var service = await CreateServiceAsync([MakeSpell("odd-ritual", effectType: PowerEffectType.None)]);
        var caster = CreateCaster();
        caster.LearnedSpells["odd-ritual"] = new CharacterSpell { SpellId = "odd-ritual" };

        var result = service.CastSpell(caster, "odd-ritual");

        result.Message.Should().Be("Cast odd-ritual!");
    }

    // ── Mana, XP and cooldowns ───────────────────────────────────────────────

    [Fact]
    public async Task CastSpell_ReducesManaCostByHalf_AtHighSkillRank()
    {
        var service = await CreateServiceAsync([MakeSpell("fireball", manaCost: 40)]);
        var caster = CreateCaster(rank: 0);
        caster.Skills["arcane"].CurrentRank = 100;
        caster.LearnedSpells["fireball"] = new CharacterSpell { SpellId = "fireball" };

        var result = service.CastSpell(caster, "fireball", new Character { Name = "Goblin" });

        result.ManaCostPaid.Should().Be(20, "50% is the maximum skill cost reduction");
        caster.Mana.Should().Be(180);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(1, 8)]
    [InlineData(2, 10)]
    [InlineData(3, 12)]
    [InlineData(4, 15)]
    [InlineData(5, 18)]
    [InlineData(6, 22)]
    [InlineData(7, 26)]
    [InlineData(8, 30)]
    [InlineData(9, 35)]
    [InlineData(10, 40)]
    [InlineData(11, 8)]
    public async Task CastSpell_AwardsSkillXpFromTheRankTable(int spellRank, int expectedXp)
    {
        var service = await CreateServiceAsync([MakeSpell("fireball", rank: spellRank)]);
        var caster = CreateCaster();
        caster.LearnedSpells["fireball"] = new CharacterSpell { SpellId = "fireball" };
        var manaBefore = caster.Mana;

        var result = service.CastSpell(caster, "fireball", new Character { Name = "Goblin" });

        result.Success.Should().BeTrue();
        caster.Skills["arcane"].CurrentXP.Should().Be(expectedXp);
        caster.Skills["arcane"].XPSources["cast_fireball"].Should().Be(expectedXp);
    }

    [Fact]
    public async Task CastSpell_AppliesConfiguredCooldown_AndCountsTheCast()
    {
        var service = await CreateServiceAsync([MakeSpell("fireball", cooldown: 3)]);
        var caster = CreateCaster();
        caster.LearnedSpells["fireball"] = new CharacterSpell { SpellId = "fireball" };

        var result = service.CastSpell(caster, "fireball", new Character { Name = "Goblin" });

        result.Success.Should().BeTrue();
        caster.SpellCooldowns["fireball"].Should().Be(3);
        caster.LearnedSpells["fireball"].TimesCast.Should().Be(1);
    }

    [Fact]
    public async Task CastSpell_WhileOnCooldown_Fails()
    {
        var service = await CreateServiceAsync([MakeSpell("fireball", cooldown: 3)]);
        var caster = CreateCaster();
        caster.LearnedSpells["fireball"] = new CharacterSpell { SpellId = "fireball" };
        caster.SpellCooldowns["fireball"] = 2;

        var result = service.CastSpell(caster, "fireball");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("cooling down (2 turns)");
    }

    [Fact]
    public async Task DecreaseSpellCooldowns_DecrementsAndDropsExpiredEntries()
    {
        var service = await CreateServiceAsync();
        var caster = CreateCaster();
        caster.SpellCooldowns["expiring"] = 1;
        caster.SpellCooldowns["ticking"] = 3;

        service.DecreaseSpellCooldowns(caster);

        caster.SpellCooldowns.Should().NotContainKey("expiring");
        caster.SpellCooldowns["ticking"].Should().Be(2);
    }
}
