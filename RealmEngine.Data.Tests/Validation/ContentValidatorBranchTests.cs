using RealmEngine.Data.Entities;
using RealmEngine.Data.Validation.Content;

namespace RealmEngine.Data.Tests.Validation;

/// <summary>
/// Exercises the validator branches the primary content-validator suite does not reach:
/// missing discriminators, negative reward quantities, one-sided warnings and the
/// ingredient/reference rules on recipes and locations.
/// </summary>
[Trait("Category", "Validation")]
public class ContentValidatorBranchTests
{
    private static readonly ContentReferenceCatalog Catalog = new()
    {
        Archetypes = ContentReferenceCatalog.Slugs(["giant-rat"]),
        Items = ContentReferenceCatalog.Slugs(["iron-sword"]),
        Materials = ContentReferenceCatalog.Slugs(["iron-ingot"]),
        Locations = ContentReferenceCatalog.Slugs(["grey-cup"]),
        Skills = ContentReferenceCatalog.Slugs(["blacksmithing"]),
    };

    private static Item Weapon() => new()
    {
        Slug = "iron-sword",
        TypeKey = "heavy-blades",
        DisplayName = "Iron Sword",
        ItemType = "weapon",
        WeaponType = "sword",
        HandsRequired = 1,
        Stats = new ItemStats { DamageMin = 4, DamageMax = 8 },
    };

    private static Recipe RecipeWith(string outputSlug = "iron-sword", int outputQuantity = 1) => new()
    {
        Slug = "iron-sword-recipe",
        TypeKey = "weapons",
        DisplayName = "Iron Sword Recipe",
        OutputItemSlug = outputSlug,
        OutputQuantity = outputQuantity,
        CraftingSkill = "blacksmithing",
        CraftingLevel = 1,
        Ingredients = [],
    };

    private static Quest QuestWith(params QuestObjective[] objectives) => new()
    {
        Slug = "the-lost-shipment",
        TypeKey = "side",
        DisplayName = "The Lost Shipment",
        MinLevel = 1,
        Objectives = new QuestObjectives { Items = [.. objectives] },
    };

    private static QuestObjective Objective(string type, string target, int quantity = 1) =>
        new() { Type = type, Target = target, Quantity = quantity };

    // ── ItemValidator ────────────────────────────────────────────────────────

    [Fact]
    public void Item_without_an_item_type_is_rejected_before_any_other_rule_runs()
    {
        // Arrange
        var item = Weapon();
        item.ItemType = "  ";

        // Act
        var result = new ItemValidator().Validate(item, Catalog);

        // Assert
        result.Errors.Should().ContainSingle(issue => issue.Rule == ContentRules.DiscriminatorMissing);
    }

    [Fact]
    public void Weapon_without_any_damage_only_warns()
    {
        // Arrange
        var item = Weapon();
        item.Stats = new ItemStats();

        // Act
        var result = new ItemValidator().Validate(item, Catalog);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle(issue => issue.Rule == ContentRules.ValueOutOfRange);
    }

    [Fact]
    public void Weapon_with_zero_damage_on_both_ends_only_warns()
    {
        // Arrange
        var item = Weapon();
        item.Stats = new ItemStats { DamageMin = 0, DamageMax = 0 };

        // Act
        var result = new ItemValidator().Validate(item, Catalog);

        // Assert
        result.Warnings.Should().Contain(issue => issue.Rule == ContentRules.ValueOutOfRange);
    }

    [Fact]
    public void Weapon_flagged_two_handed_with_one_hand_required_warns_about_the_inconsistency()
    {
        // Arrange
        var item = Weapon();
        item.HandsRequired = 1;
        item.Traits.TwoHanded = true;

        // Act
        var result = new ItemValidator().Validate(item, Catalog);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle(issue => issue.Rule == ContentRules.FieldInconsistent);
    }

    [Fact]
    public void Two_handed_weapon_declaring_two_hands_does_not_warn()
    {
        // Arrange
        var item = Weapon();
        item.HandsRequired = 2;
        item.Traits.TwoHanded = true;

        // Act
        var result = new ItemValidator().Validate(item, Catalog);

        // Assert
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Armour_without_an_armour_type_is_rejected()
    {
        // Arrange
        var item = Weapon();
        item.ItemType = "armor";
        item.ArmorType = null;
        item.EquipSlot = "chest";

        // Act
        var result = new ItemValidator().Validate(item, Catalog);

        // Assert
        result.Errors.Should().ContainSingle(issue => issue.Rule == ContentRules.FieldInconsistent &&
                                                     issue.Field == "armorType");
    }

    [Fact]
    public void Armour_with_a_negative_armour_rating_is_rejected()
    {
        // Arrange
        var item = Weapon();
        item.ItemType = "armor";
        item.ArmorType = "heavy";
        item.EquipSlot = "chest";
        item.Stats = new ItemStats { ArmorRating = -1 };

        // Act
        var result = new ItemValidator().Validate(item, Catalog);

        // Assert
        result.Errors.Should().Contain(issue => issue.Rule == ContentRules.RangeInvalid);
    }

    [Fact]
    public void Armour_without_a_weapon_type_is_not_penalised_for_it()
    {
        // Arrange
        var item = Weapon();
        item.ItemType = "armor";
        item.ArmorType = "light";
        item.EquipSlot = "chest";
        item.WeaponType = null;
        item.Stats = new ItemStats { ArmorRating = 3 };

        // Act
        var result = new ItemValidator().Validate(item, Catalog);

        // Assert
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Consumable_without_an_effect_power_only_warns()
    {
        // Arrange
        var item = Weapon();
        item.ItemType = "consumable";
        item.Traits.Consumable = true;
        item.Stats = new ItemStats();

        // Act
        var result = new ItemValidator().Validate(item, Catalog);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle(issue => issue.Rule == ContentRules.FieldInconsistent);
    }

    [Fact]
    public void Consumable_with_an_effect_power_does_not_warn()
    {
        // Arrange
        var item = Weapon();
        item.ItemType = "consumable";
        item.Traits.Consumable = true;
        item.Stats = new ItemStats { EffectPower = 25 };

        // Act
        var result = new ItemValidator().Validate(item, Catalog);

        // Assert
        result.Warnings.Should().BeEmpty();
    }

    // ── RecipeValidator ──────────────────────────────────────────────────────

    [Fact]
    public void Recipe_without_an_output_item_is_rejected()
    {
        // Arrange
        var recipe = RecipeWith(outputSlug: "  ");

        // Act
        var result = new RecipeValidator().Validate(recipe, Catalog);

        // Assert
        result.Errors.Should().ContainSingle(issue => issue.Rule == ContentRules.RecipeInconsistent &&
                                                     issue.Field == "outputItemSlug");
    }

    [Fact]
    public void Recipe_producing_zero_items_is_rejected()
    {
        // Arrange
        var recipe = RecipeWith(outputQuantity: 0);

        // Act
        var result = new RecipeValidator().Validate(recipe, Catalog);

        // Assert
        result.Errors.Should().ContainSingle(issue => issue.Rule == ContentRules.ValueOutOfRange &&
                                                     issue.Field == "outputQuantity");
    }

    [Fact]
    public void Recipe_without_a_crafting_skill_is_rejected()
    {
        // Arrange
        var recipe = RecipeWith();
        recipe.CraftingSkill = string.Empty;

        // Act
        var result = new RecipeValidator().Validate(recipe, Catalog);

        // Assert
        result.Errors.Should().ContainSingle(issue => issue.Rule == ContentRules.FieldInconsistent &&
                                                     issue.Field == "craftingSkill");
    }

    [Fact]
    public void Recipe_naming_an_unknown_crafting_skill_only_warns()
    {
        // Arrange
        var recipe = RecipeWith();
        recipe.CraftingSkill = "jewelsmithing";

        // Act
        var result = new RecipeValidator().Validate(recipe, Catalog);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle(issue => issue.Field == "craftingSkill");
    }

    [Fact]
    public void Recipe_with_a_negative_crafting_level_is_rejected()
    {
        // Arrange
        var recipe = RecipeWith();
        recipe.CraftingLevel = -2;

        // Act
        var result = new RecipeValidator().Validate(recipe, Catalog);

        // Assert
        result.Errors.Should().ContainSingle(issue => issue.Field == "craftingLevel");
    }

    [Fact]
    public void Recipe_ingredient_with_a_zero_quantity_is_rejected()
    {
        // Arrange
        var recipe = RecipeWith();
        recipe.Ingredients =
        [
            new RecipeIngredient { ItemDomain = "items/materials", ItemSlug = "iron-ingot", Quantity = 0 },
        ];

        // Act
        var result = new RecipeValidator().Validate(recipe, Catalog);

        // Assert
        result.Errors.Should().ContainSingle(issue => issue.Field == "ingredients[0].quantity");
    }

    [Fact]
    public void Recipe_ingredient_resolves_against_the_materials_domain()
    {
        // Arrange
        var recipe = RecipeWith();
        recipe.Ingredients =
        [
            new RecipeIngredient { ItemDomain = "items/materials", ItemSlug = "iron-ingot", Quantity = 2 },
        ];

        // Act
        var result = new RecipeValidator().Validate(recipe, Catalog);

        // Assert
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Recipe_ingredient_in_the_items_domain_is_checked_against_the_item_catalog()
    {
        // Arrange — iron-ingot is a material, not an item, so the item-domain lookup fails
        var recipe = RecipeWith();
        recipe.Ingredients =
        [
            new RecipeIngredient { ItemDomain = "items/general", ItemSlug = "iron-ingot", Quantity = 1 },
        ];

        // Act
        var result = new RecipeValidator().Validate(recipe, Catalog);

        // Assert
        result.Errors.Should().ContainSingle(issue => issue.Field == "ingredients[0].itemSlug");
    }

    [Fact]
    public void Recipe_with_a_valid_ingredient_list_and_output_passes()
    {
        // Arrange
        var recipe = RecipeWith();
        recipe.Ingredients =
        [
            new RecipeIngredient { ItemDomain = "items/materials", ItemSlug = "iron-ingot", Quantity = 2 },
        ];

        // Act
        var result = new RecipeValidator().Validate(recipe, Catalog);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Issues.Should().BeEmpty();
    }

    // ── QuestValidator ───────────────────────────────────────────────────────

    [Fact]
    public void Quest_with_a_negative_minimum_level_is_rejected()
    {
        // Arrange
        var quest = QuestWith(Objective("kill", "giant-rat"));
        quest.MinLevel = -1;

        // Act
        var result = new QuestValidator().Validate(quest, Catalog);

        // Assert
        result.Errors.Should().ContainSingle(issue => issue.Field == "minLevel");
    }

    [Theory]
    [InlineData("stats.xpReward")]
    [InlineData("stats.goldReward")]
    [InlineData("stats.reputationReward")]
    public void Quest_with_a_negative_reward_is_rejected(string field)
    {
        // Arrange
        var quest = QuestWith(Objective("kill", "giant-rat"));
        quest.Stats = field switch
        {
            "stats.xpReward" => new QuestStats { XpReward = -1 },
            "stats.goldReward" => new QuestStats { GoldReward = -5 },
            _ => new QuestStats { ReputationReward = -10 },
        };

        // Act
        var result = new QuestValidator().Validate(quest, Catalog);

        // Assert
        result.Errors.Should().ContainSingle(issue => issue.Field == field);
    }

    [Fact]
    public void Quest_objective_without_a_type_is_rejected()
    {
        // Arrange
        var quest = QuestWith(Objective("", "giant-rat"));

        // Act
        var result = new QuestValidator().Validate(quest, Catalog);

        // Assert
        result.Errors.Should().ContainSingle(issue => issue.Field == "objectives[0]" &&
                                                     issue.Rule == ContentRules.QuestObjectiveInvalid);
    }

    [Fact]
    public void Quest_objective_without_a_target_is_rejected()
    {
        // Arrange
        var quest = QuestWith(Objective("kill", " "));

        // Act
        var result = new QuestValidator().Validate(quest, Catalog);

        // Assert
        result.Errors.Should().ContainSingle(issue => issue.Field == "objectives[0]" &&
                                                     issue.Rule == ContentRules.QuestObjectiveInvalid);
    }

    [Fact]
    public void Quest_reports_each_invalid_objective_with_its_own_index()
    {
        // Arrange
        var quest = QuestWith(
            Objective("kill", "giant-rat"),
            Objective("", "giant-rat"),
            Objective("collect", ""));

        // Act
        var result = new QuestValidator().Validate(quest, Catalog);

        // Assert
        var fields = result.Errors.Select(issue => issue.Field).ToList();
        fields.Should().Contain("objectives[1]");
        fields.Should().Contain("objectives[2]");
    }

    [Fact]
    public void Quest_with_a_complete_valid_objective_passes()
    {
        // Arrange
        var quest = QuestWith(Objective("kill", "giant-rat", 3));

        // Act
        var result = new QuestValidator().Validate(quest, Catalog);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Issues.Should().BeEmpty();
    }

    // ── PowerValidator ───────────────────────────────────────────────────────

    private static Power Spell() => new()
    {
        Slug = "fireball",
        TypeKey = "fire",
        DisplayName = "Fireball",
        PowerType = "spell",
        Stats = new PowerStats { ManaCost = 10, Cooldown = 3 },
        Traits = new PowerTraits { HasCooldown = true },
        Effects = new PowerEffects(),
    };

    [Fact]
    public void Power_without_a_power_type_is_rejected()
    {
        // Arrange
        var power = Spell();
        power.PowerType = string.Empty;

        // Act
        var result = new PowerValidator().Validate(power, Catalog);

        // Assert
        result.Errors.Should().ContainSingle(issue => issue.Rule == ContentRules.DiscriminatorMissing);
    }

    [Fact]
    public void Power_with_a_negative_mana_cost_is_rejected()
    {
        // Arrange
        var power = Spell();
        power.Stats.ManaCost = -1;

        // Act
        var result = new PowerValidator().Validate(power, Catalog);

        // Assert
        result.Errors.Should().ContainSingle(issue => issue.Field == "stats.manaCost");
    }

    [Fact]
    public void Power_with_an_inverted_damage_range_is_rejected()
    {
        // Arrange
        var power = Spell();
        power.Stats.DamageMin = 10;
        power.Stats.DamageMax = 2;

        // Act
        var result = new PowerValidator().Validate(power, Catalog);

        // Assert
        result.Errors.Should().ContainSingle(issue => issue.Field == "stats.damageMin" &&
                                                     issue.Rule == ContentRules.RangeInvalid);
    }

    [Fact]
    public void Power_with_an_inverted_heal_range_is_rejected()
    {
        // Arrange
        var power = Spell();
        power.Stats.HealMin = 20;
        power.Stats.HealMax = 5;

        // Act
        var result = new PowerValidator().Validate(power, Catalog);

        // Assert
        result.Errors.Should().ContainSingle(issue => issue.Field == "stats.healMin" &&
                                                     issue.Rule == ContentRules.RangeInvalid);
    }

    [Fact]
    public void Power_that_requires_a_known_item_passes()
    {
        // Arrange
        var power = Spell();
        power.RequiresItem = "iron-sword";

        // Act
        var result = new PowerValidator().Validate(power, Catalog);

        // Assert
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void A_well_formed_spell_passes()
    {
        // Arrange
        var power = Spell();
        power.Effects.ConditionApplied = "burning";
        power.Effects.ConditionChance = 0.5f;

        // Act
        var result = new PowerValidator().Validate(power, Catalog);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    // ── ZoneLocationValidator ────────────────────────────────────────────────

    private static ZoneLocation Location() => new()
    {
        Slug = "grey-cup",
        TypeKey = "dungeons",
        DisplayName = "Grey Cup",
        ZoneId = "crestfall",
        Stats = new ZoneLocationStats { MinLevel = 1, MaxLevel = 5, DangerLevel = 2 },
        ActorPool = [new ActorPoolEntry { ArchetypeSlug = "giant-rat", Weight = 5 }],
    };

    [Fact]
    public void Location_with_a_negative_danger_level_is_rejected()
    {
        // Arrange
        var location = Location();
        location.Stats.DangerLevel = -1;

        // Act
        var result = new ZoneLocationValidator().Validate(location, Catalog);

        // Assert
        result.Errors.Should().ContainSingle(issue => issue.Field == "stats.dangerLevel");
    }

    [Fact]
    public void Location_spawn_entry_with_a_zero_weight_is_rejected()
    {
        // Arrange
        var location = Location();
        location.ActorPool = [new ActorPoolEntry { ArchetypeSlug = "giant-rat", Weight = 0 }];

        // Act
        var result = new ZoneLocationValidator().Validate(location, Catalog);

        // Assert
        result.Errors.Should().ContainSingle(issue => issue.Field == "actorPool[0].weight");
    }

    [Fact]
    public void Location_spawn_entry_with_a_negative_weight_is_rejected()
    {
        // Arrange
        var location = Location();
        location.ActorPool = [new ActorPoolEntry { ArchetypeSlug = "giant-rat", Weight = -3 }];

        // Act
        var result = new ZoneLocationValidator().Validate(location, Catalog);

        // Assert
        result.Errors.Should().ContainSingle(issue => issue.Rule == ContentRules.LootWeightInvalid);
    }

    [Fact]
    public void Location_with_an_inverted_level_band_is_rejected()
    {
        // Arrange
        var location = Location();
        location.Stats.MinLevel = 10;
        location.Stats.MaxLevel = 2;

        // Act
        var result = new ZoneLocationValidator().Validate(location, Catalog);

        // Assert
        result.Errors.Should().ContainSingle(issue => issue.Rule == ContentRules.LevelBandInvalid);
    }

    [Fact]
    public void Location_with_an_open_level_band_is_accepted()
    {
        // Arrange — only a minimum level is declared
        var location = Location();
        location.Stats.MinLevel = 10;
        location.Stats.MaxLevel = null;

        // Act
        var result = new ZoneLocationValidator().Validate(location, Catalog);

        // Assert
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Location_spawning_an_unknown_archetype_is_rejected()
    {
        // Arrange
        var location = Location();
        location.ActorPool = [new ActorPoolEntry { ArchetypeSlug = "void-horror", Weight = 1 }];

        // Act
        var result = new ZoneLocationValidator().Validate(location, Catalog);

        // Assert
        result.Errors.Should().ContainSingle(issue => issue.Rule == ContentRules.MissingSpawnArchetype);
    }

    [Fact]
    public void A_well_formed_location_passes()
    {
        // Act
        var result = new ZoneLocationValidator().Validate(Location(), Catalog);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Issues.Should().BeEmpty();
    }
}
