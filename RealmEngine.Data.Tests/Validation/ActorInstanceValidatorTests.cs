using RealmEngine.Data.Entities;
using RealmEngine.Data.Validation.Content;

namespace RealmEngine.Data.Tests.Validation;

/// <summary>
/// Guards <see cref="ActorInstanceValidator"/>: every field the instance path actually applies is range-checked, the
/// placement has to resolve, and the reserved fields are left alone.
/// </summary>
[Trait("Category", "Validation")]
public class ActorInstanceValidatorTests
{
    private static readonly Guid ArchetypeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid LootTableId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly ContentReferenceCatalog Catalog = new()
    {
        Archetypes = ContentReferenceCatalog.Slugs(["goblin-scout"]),
        Locations = ContentReferenceCatalog.Slugs(["old-clearing"]),
        LootTables = ContentReferenceCatalog.Slugs(["goblin-common-drops"]),
        LootTableIds = new HashSet<Guid> { LootTableId },
    };

    private static ActorInstance Instance() => new()
    {
        Slug = "elder-goblin-chief",
        TypeKey = "boss",
        DisplayName = "Elder Goblin Chief",
        ArchetypeId = ArchetypeId,
        LocationSlug = "old-clearing",
        IsActive = true,
    };

    [Fact]
    public void An_unplaced_instance_with_no_overrides_is_valid()
    {
        var instance = Instance();
        instance.LocationSlug = null;

        var result = new ActorInstanceValidator().Validate(instance, Catalog);

        result.IsValid.Should().BeTrue("an unplaced instance is catalogue-only, which is a legitimate row");
    }

    [Fact]
    public void A_placed_instance_whose_location_does_not_exist_is_rejected()
    {
        var instance = Instance();
        instance.LocationSlug = "the-moon";

        var result = new ActorInstanceValidator().Validate(instance, Catalog);

        result.Errors.Should().ContainSingle(issue => issue.Rule == ContentRules.MissingPlacementLocation);
    }

    [Fact]
    public void An_inverted_gold_pair_is_rejected()
    {
        var instance = Instance();
        instance.StatOverrides.GoldRewardMin = 30;
        instance.StatOverrides.GoldRewardMax = 10;

        var result = new ActorInstanceValidator().Validate(instance, Catalog);

        result.Errors.Should().ContainSingle(issue => issue.Rule == ContentRules.RangeInvalid);
    }

    [Theory]
    [InlineData("health")]
    [InlineData("damage")]
    public void A_zero_health_or_damage_override_is_rejected(string field)
    {
        var instance = Instance();
        if (field == "health")
        {
            instance.StatOverrides.Health = 0;
        }
        else
        {
            instance.StatOverrides.Damage = 0;
        }

        var result = new ActorInstanceValidator().Validate(instance, Catalog);

        result.Errors.Should().Contain(issue => issue.Rule == ContentRules.ValueOutOfRange);
    }

    [Fact]
    public void A_negative_experience_override_is_rejected()
    {
        var instance = Instance();
        instance.StatOverrides.ExperienceReward = -5;

        var result = new ActorInstanceValidator().Validate(instance, Catalog);

        result.Errors.Should().Contain(issue => issue.Rule == ContentRules.ValueOutOfRange);
    }

    [Fact]
    public void A_zero_attribute_override_is_rejected()
    {
        var instance = Instance();
        instance.StatOverrides.Constitution = 0;

        var result = new ActorInstanceValidator().Validate(instance, Catalog);

        result.Errors.Should().Contain(issue => issue.Rule == ContentRules.ValueOutOfRange);
    }

    [Fact]
    public void A_loot_table_override_that_does_not_resolve_is_rejected()
    {
        var instance = Instance();
        instance.LootTableOverride = Guid.NewGuid();

        var result = new ActorInstanceValidator().Validate(instance, Catalog);

        result.Errors.Should().ContainSingle(issue => issue.Rule == ContentRules.MissingLootTable);
    }

    [Fact]
    public void A_resolving_loot_table_override_is_accepted()
    {
        var instance = Instance();
        instance.LootTableOverride = LootTableId;

        var result = new ActorInstanceValidator().Validate(instance, Catalog);

        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Reserved_armor_and_attack_bonus_values_are_not_validated()
    {
        var instance = Instance();
        instance.StatOverrides.ArmorClass = 0;
        instance.StatOverrides.AttackBonus = -3;

        var result = new ActorInstanceValidator().Validate(instance, Catalog);

        result.Errors.Should().BeEmpty("both fields are reserved, so a value there applies nowhere and breaks nothing");
    }
}
