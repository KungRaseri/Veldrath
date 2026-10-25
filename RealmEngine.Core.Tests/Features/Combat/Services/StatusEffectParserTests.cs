using RealmEngine.Core.Features.Combat.Services;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Combat.Services;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for <see cref="StatusEffectParser"/>, which turns power traits into status effects.
/// </summary>
public class StatusEffectParserTests
{
    private static Power CreatePower(params (string Key, object Value)[] traits)
    {
        var power = new Power { Id = "fire-bolt", DisplayName = "Fire Bolt" };
        foreach (var (key, value) in traits)
        {
            power.Traits[key] = value;
        }
        return power;
    }

    [Fact]
    public void ParseStatusEffectFromAbility_Should_Return_Null_Without_StatusEffect_Trait()
    {
        // Arrange
        var power = CreatePower(("damageType", "fire"));

        // Act
        var effect = StatusEffectParser.ParseStatusEffectFromAbility(power, "Fire Bolt");

        // Assert
        effect.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("exploding")]
    public void ParseStatusEffectFromAbility_Should_Return_Null_For_Blank_Or_Unknown_Effect(string value)
    {
        // Arrange
        var power = CreatePower(("statusEffect", value));

        // Act
        var effect = StatusEffectParser.ParseStatusEffectFromAbility(power, "Fire Bolt");

        // Assert
        effect.Should().BeNull();
    }

    [Theory]
    [InlineData("burning", StatusEffectType.Burning)]
    [InlineData("poisoned", StatusEffectType.Poisoned)]
    [InlineData("bleeding", StatusEffectType.Bleeding)]
    [InlineData("frozen", StatusEffectType.Frozen)]
    [InlineData("stunned", StatusEffectType.Stunned)]
    [InlineData("paralyzed", StatusEffectType.Paralyzed)]
    [InlineData("feared", StatusEffectType.Feared)]
    [InlineData("confused", StatusEffectType.Confused)]
    [InlineData("silenced", StatusEffectType.Silenced)]
    [InlineData("weakened", StatusEffectType.Weakened)]
    [InlineData("cursed", StatusEffectType.Cursed)]
    [InlineData("regenerating", StatusEffectType.Regenerating)]
    [InlineData("shielded", StatusEffectType.Shielded)]
    [InlineData("strengthened", StatusEffectType.Strengthened)]
    [InlineData("hasted", StatusEffectType.Hasted)]
    [InlineData("protected", StatusEffectType.Protected)]
    [InlineData("blessed", StatusEffectType.Blessed)]
    [InlineData("enraged", StatusEffectType.Enraged)]
    [InlineData("invisible", StatusEffectType.Invisible)]
    [InlineData("taunted", StatusEffectType.Taunted)]
    [InlineData("slowed", StatusEffectType.Weakened)]
    [InlineData("charmed", StatusEffectType.Confused)]
    [InlineData("BURNING", StatusEffectType.Burning)]
    public void ParseStatusEffectFromAbility_Should_Map_Effect_Names_Case_Insensitively(
        string value, StatusEffectType expected)
    {
        // Arrange
        var power = CreatePower(("statusEffect", value));

        // Act
        var effect = StatusEffectParser.ParseStatusEffectFromAbility(power, "Fire Bolt");

        // Assert
        effect.Should().NotBeNull();
        effect!.Type.Should().Be(expected);
        effect.Category.Should().Be(expected.GetCategory());
        effect.IconName.Should().Be(expected.GetDefaultIcon());
        effect.Name.Should().NotBeNullOrWhiteSpace();
        effect.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("burning", 3)]
    [InlineData("regenerating", 4)]
    [InlineData("stunned", 2)]
    [InlineData("weakened", 3)]
    [InlineData("hasted", 3)]
    public void ParseStatusEffectFromAbility_Should_Use_Category_Default_Duration(string value, int expected)
    {
        // Arrange
        var power = CreatePower(("statusEffect", value));

        // Act
        var effect = StatusEffectParser.ParseStatusEffectFromAbility(power, "Fire Bolt");

        // Assert
        effect!.RemainingDuration.Should().Be(expected);
        effect.OriginalDuration.Should().Be(expected);
    }

    [Theory]
    [InlineData(6)]
    [InlineData("6")]
    public void ParseStatusEffectFromAbility_Should_Use_StatusDuration_Override(object duration)
    {
        // Arrange
        var power = CreatePower(("statusEffect", "stunned"), ("statusDuration", duration));

        // Act
        var effect = StatusEffectParser.ParseStatusEffectFromAbility(power, "Fire Bolt");

        // Assert
        effect!.RemainingDuration.Should().Be(6);
        effect.OriginalDuration.Should().Be(6);
    }

    [Fact]
    public void ParseStatusEffectFromAbility_Should_Ignore_Unparseable_Duration()
    {
        // Arrange
        var power = CreatePower(("statusEffect", "stunned"), ("statusDuration", "long"));

        // Act
        var effect = StatusEffectParser.ParseStatusEffectFromAbility(power, "Fire Bolt");

        // Assert
        effect!.RemainingDuration.Should().Be(2);
    }

    [Theory]
    [InlineData("burning", 5)]
    [InlineData("poisoned", 4)]
    [InlineData("bleeding", 3)]
    public void ParseStatusEffectFromAbility_Should_Use_Default_Tick_Damage_For_Damage_Over_Time(
        string value, int expected)
    {
        // Arrange
        var power = CreatePower(("statusEffect", value));

        // Act
        var effect = StatusEffectParser.ParseStatusEffectFromAbility(power, "Fire Bolt");

        // Assert
        effect!.TickDamage.Should().Be(expected);
        effect.TickHealing.Should().Be(0);
    }

    [Fact]
    public void ParseStatusEffectFromAbility_Should_Use_StatusDamage_Override()
    {
        // Arrange
        var power = CreatePower(("statusEffect", "burning"), ("statusDamage", 12));

        // Act
        var effect = StatusEffectParser.ParseStatusEffectFromAbility(power, "Fire Bolt");

        // Assert
        effect!.TickDamage.Should().Be(12);
    }

    [Fact]
    public void ParseStatusEffectFromAbility_Should_Not_Deal_Tick_Damage_Outside_Damage_Over_Time()
    {
        // Arrange
        var power = CreatePower(("statusEffect", "stunned"), ("statusDamage", 12));

        // Act
        var effect = StatusEffectParser.ParseStatusEffectFromAbility(power, "Fire Bolt");

        // Assert
        effect!.TickDamage.Should().Be(0);
    }

    [Fact]
    public void ParseStatusEffectFromAbility_Should_Heal_Over_Time_With_Default_And_Override()
    {
        // Arrange
        var defaultPower = CreatePower(("statusEffect", "regenerating"));
        var overridePower = CreatePower(("statusEffect", "regenerating"), ("statusHealing", "15"));

        // Act
        var defaultEffect = StatusEffectParser.ParseStatusEffectFromAbility(defaultPower, "Renew");
        var overrideEffect = StatusEffectParser.ParseStatusEffectFromAbility(overridePower, "Renew");

        // Assert
        defaultEffect!.TickHealing.Should().Be(8);
        defaultEffect.TickDamage.Should().Be(0);
        overrideEffect!.TickHealing.Should().Be(15);
    }

    [Theory]
    [InlineData("burning", "fire")]
    [InlineData("poisoned", "poison")]
    [InlineData("frozen", "ice")]
    [InlineData("stunned", "physical")]
    public void ParseStatusEffectFromAbility_Should_Default_Damage_Type_From_Effect(string value, string expected)
    {
        // Arrange
        var power = CreatePower(("statusEffect", value));

        // Act
        var effect = StatusEffectParser.ParseStatusEffectFromAbility(power, "Fire Bolt");

        // Assert
        effect!.DamageType.Should().Be(expected);
    }

    [Fact]
    public void ParseStatusEffectFromAbility_Should_Use_DamageType_Trait_Override()
    {
        // Arrange
        var power = CreatePower(("statusEffect", "burning"), ("damageType", "holy"));

        // Act
        var effect = StatusEffectParser.ParseStatusEffectFromAbility(power, "Fire Bolt");

        // Assert
        effect!.DamageType.Should().Be("holy");
    }

    [Theory]
    [InlineData("burning", true, 5)]
    [InlineData("poisoned", true, 5)]
    [InlineData("bleeding", true, 3)]
    [InlineData("regenerating", true, 3)]
    [InlineData("stunned", false, 1)]
    [InlineData("blessed", false, 1)]
    public void ParseStatusEffectFromAbility_Should_Set_Stacking_Rules(string value, bool canStack, int maxStacks)
    {
        // Arrange
        var power = CreatePower(("statusEffect", value));

        // Act
        var effect = StatusEffectParser.ParseStatusEffectFromAbility(power, "Fire Bolt");

        // Assert
        effect!.CanStack.Should().Be(canStack);
        effect.MaxStacks.Should().Be(maxStacks);
        effect.StackCount.Should().Be(1);
    }

    [Fact]
    public void ParseStatusEffectFromAbility_Should_Record_Source_And_Unique_Id()
    {
        // Arrange
        var power = CreatePower(("statusEffect", "burning"));

        // Act
        var first = StatusEffectParser.ParseStatusEffectFromAbility(power, "Fire Bolt");
        var second = StatusEffectParser.ParseStatusEffectFromAbility(power, "Fire Bolt");

        // Assert
        first!.Source.Should().Be("Fire Bolt");
        first.Id.Should().StartWith("fire-bolt-burning-");
        first.Id.Should().NotBe(second!.Id);
    }

    [Fact]
    public void ParseStatusEffectFromAbility_Should_Use_Friendly_Names_For_Renamed_Effects()
    {
        // Arrange
        var regen = CreatePower(("statusEffect", "regenerating"));
        var haste = CreatePower(("statusEffect", "hasted"));

        // Act
        var regenEffect = StatusEffectParser.ParseStatusEffectFromAbility(regen, "Renew");
        var hasteEffect = StatusEffectParser.ParseStatusEffectFromAbility(haste, "Quicken");

        // Assert
        regenEffect!.Name.Should().Be("Regeneration");
        hasteEffect!.Name.Should().Be("Haste");
    }

    public static TheoryData<string, Dictionary<string, int>> DefaultModifiers => new()
    {
        { "weakened", new() { ["attack"] = -5 } },
        { "cursed", new() { ["attack"] = -3, ["defense"] = -3 } },
        { "strengthened", new() { ["attack"] = 10 } },
        { "protected", new() { ["defense"] = 10 } },
        { "hasted", new() { ["speed"] = 15 } },
        { "blessed", new() { ["attack"] = 5, ["defense"] = 5 } },
        { "enraged", new() { ["attack"] = 15, ["defense"] = -5 } },
        { "shielded", new() }
    };

    [Theory]
    [MemberData(nameof(DefaultModifiers))]
    public void ParseStatusEffectFromAbility_Should_Apply_Default_Stat_Modifiers_To_Buffs_And_Debuffs(
        string value, Dictionary<string, int> expected)
    {
        // Arrange
        var power = CreatePower(("statusEffect", value));

        // Act
        var effect = StatusEffectParser.ParseStatusEffectFromAbility(power, "Fire Bolt");

        // Assert
        effect!.StatModifiers.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void ParseStatusEffectFromAbility_Should_Merge_Custom_Stat_Modifiers()
    {
        // Arrange
        var custom = new Dictionary<string, object> { ["defense"] = 0, ["speed"] = "4", ["luck"] = "lots" };
        var power = CreatePower(("statusEffect", "enraged"), ("statModifiers", custom));

        // Act
        var effect = StatusEffectParser.ParseStatusEffectFromAbility(power, "Frenzy");

        // Assert
        effect!.StatModifiers.Should().BeEquivalentTo(new Dictionary<string, int>
        {
            ["attack"] = 15,
            ["defense"] = 0,
            ["speed"] = 4
        });
    }

    [Fact]
    public void ParseStatusEffectFromAbility_Should_Not_Apply_Stat_Modifiers_Outside_Buffs_And_Debuffs()
    {
        // Arrange
        var custom = new Dictionary<string, object> { ["attack"] = 5 };
        var power = CreatePower(("statusEffect", "stunned"), ("statModifiers", custom));

        // Act
        var effect = StatusEffectParser.ParseStatusEffectFromAbility(power, "Bash");

        // Assert
        effect!.StatModifiers.Should().BeEmpty();
    }

    [Theory]
    [InlineData(40, 40)]
    [InlineData("75", 75)]
    [InlineData(150, 100)]
    [InlineData(-10, 0)]
    public void GetStatusEffectChance_Should_Read_And_Clamp_StatusChance(object chance, int expected)
    {
        // Arrange
        var power = CreatePower(("statusChance", chance));

        // Act
        var result = StatusEffectParser.GetStatusEffectChance(power);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("often")]
    public void GetStatusEffectChance_Should_Default_To_Always(object? chance)
    {
        // Arrange
        var power = chance is null ? CreatePower() : CreatePower(("statusChance", chance));

        // Act
        var result = StatusEffectParser.GetStatusEffectChance(power);

        // Assert
        result.Should().Be(100);
    }
}
