using RealmEngine.Data.Entities;
using RealmEngine.Data.Validation.Content;

namespace RealmEngine.Data.Tests.Validation;

/// <summary>
/// Tests for the shared content validators: each family must reject a broken fixture with a stable rule id and
/// accept a good one, and the reference rules must report the same rule for the same defect everywhere.
/// </summary>
[Trait("Category", "Validation")]
public class ContentValidatorTests
{
    private static readonly ContentReferenceCatalog Catalog = new()
    {
        Archetypes = ContentReferenceCatalog.Slugs(["giant-rat", "forest-wolf"]),
        Items = ContentReferenceCatalog.Slugs(["iron-sword", "glowcap-mushroom"]),
        Locations = ContentReferenceCatalog.Slugs(["grey-cup"]),
        Skills = ContentReferenceCatalog.Slugs(["blacksmithing"]),
        NodeIdsByMap = new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["woods"] = ContentReferenceCatalog.Slugs(["clearing"]),
        },
    };

    private static Item Sword() => new()
    {
        Slug = "iron-sword",
        TypeKey = "heavy-blades",
        DisplayName = "Iron Sword",
        ItemType = "weapon",
        WeaponType = "sword",
        HandsRequired = 1,
        Stats = new ItemStats { DamageMin = 4, DamageMax = 8 },
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

    // ── Shared shape rules ─────────────────────────────────────────────────────

    [Fact]
    public void Shared_rules_reject_a_missing_slug_display_name_and_type_key()
    {
        var item = new Item { ItemType = "consumable", Slug = string.Empty, DisplayName = null, TypeKey = string.Empty };

        var result = ContentValidatorRegistry.CreateDefault().Validate("item", item, Catalog);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(issue => issue.Rule).Should().Contain([
            ContentRules.SlugMissing,
            ContentRules.DisplayNameMissing,
            ContentRules.TypeKeyMissing,
        ]);
    }

    [Theory]
    [InlineData("Iron Sword")]
    [InlineData("iron_sword")]
    [InlineData("iron--sword")]
    [InlineData("-iron-sword")]
    [InlineData("iron-sword-")]
    public void Shared_rules_reject_a_slug_that_is_not_lowercase_hyphenated(string slug)
    {
        var item = Sword();
        item.Slug = slug;

        var result = new ItemValidator().Validate(item, Catalog);

        result.Errors.Should().ContainSingle(issue => issue.Rule == ContentRules.SlugFormat);
    }

    [Fact]
    public void Shared_rules_warn_about_an_out_of_range_rarity_weight_without_blocking()
    {
        var item = Sword();
        item.RarityWeight = 0;

        var result = new ItemValidator().Validate(item, Catalog);

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle(issue => issue.Rule == ContentRules.RarityWeightOutOfRange);
    }

    // ── Items ──────────────────────────────────────────────────────────────────

    [Fact]
    public void A_valid_weapon_passes()
    {
        new ItemValidator().Validate(Sword(), Catalog).IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_weapon_without_a_weapon_type_is_rejected()
    {
        var item = Sword();
        item.WeaponType = null;

        var result = new ItemValidator().Validate(item, Catalog);

        result.Errors.Should().ContainSingle(issue => issue.Rule == ContentRules.FieldInconsistent
            && issue.Field == "weaponType");
    }

    [Fact]
    public void An_inverted_damage_range_is_rejected()
    {
        var item = Sword();
        item.Stats.DamageMin = 10;
        item.Stats.DamageMax = 2;

        var result = new ItemValidator().Validate(item, Catalog);

        result.Errors.Should().ContainSingle(issue => issue.Rule == ContentRules.RangeInvalid
            && issue.Field == "stats.damageMin");
    }

    [Fact]
    public void Armour_without_an_equip_slot_is_rejected()
    {
        var armor = new Item
        {
            Slug = "leather-cap",
            TypeKey = "light",
            DisplayName = "Leather Cap",
            ItemType = "armor",
            ArmorType = "light",
            Stats = new ItemStats { ArmorRating = 2 },
        };

        var result = new ItemValidator().Validate(armor, Catalog);

        result.Errors.Should().ContainSingle(issue => issue.Field == "equipSlot");
    }

    [Fact]
    public void The_registry_validates_weapon_and_armor_through_the_item_validator()
    {
        var registry = ContentValidatorRegistry.CreateDefault();
        var armor = new Item
        {
            Slug = "leather-cap",
            TypeKey = "light",
            DisplayName = "Leather Cap",
            ItemType = "armor",
            ArmorType = "light",
            EquipSlot = "head",
            Stats = new ItemStats { ArmorRating = 2 },
        };

        registry.Validate("weapon", Sword(), Catalog).IsValid.Should().BeTrue();
        registry.Validate("armor", armor, Catalog).IsValid.Should().BeTrue();
    }

    // ── Powers ─────────────────────────────────────────────────────────────────

    [Fact]
    public void A_cooldown_flag_without_a_cooldown_is_rejected()
    {
        var power = new Power
        {
            Slug = "cleave",
            TypeKey = "active/offensive",
            DisplayName = "Cleave",
            PowerType = "active",
            Stats = new PowerStats { DamageMin = 5, DamageMax = 9 },
            Traits = new PowerTraits { HasCooldown = true },
        };

        var result = new PowerValidator().Validate(power, Catalog);

        result.Errors.Should().ContainSingle(issue => issue.Rule == ContentRules.FieldInconsistent
            && issue.Field == "stats.cooldown");
    }

    [Fact]
    public void A_power_that_requires_a_missing_item_is_rejected()
    {
        var power = new Power
        {
            Slug = "shield-bash",
            TypeKey = "active/offensive",
            DisplayName = "Shield Bash",
            PowerType = "active",
            RequiresItem = "mithril-shield",
        };

        var result = new PowerValidator().Validate(power, Catalog);

        result.Errors.Should().ContainSingle(issue => issue.Field == "requiresItem");
    }

    [Fact]
    public void A_condition_with_no_chance_is_rejected()
    {
        var power = new Power
        {
            Slug = "frost-nova",
            TypeKey = "active/offensive",
            DisplayName = "Frost Nova",
            PowerType = "active",
            Effects = new PowerEffects { ConditionApplied = "chilled" },
        };

        var result = new PowerValidator().Validate(power, Catalog);

        result.Errors.Should().ContainSingle(issue => issue.Field == "effects.conditionChance");
    }

    // ── Quests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void A_quest_with_no_objectives_is_rejected()
    {
        var result = new QuestValidator().Validate(QuestWith(), Catalog);

        result.Errors.Should().ContainSingle(issue => issue.Rule == ContentRules.QuestWithoutObjectives);
    }

    [Fact]
    public void A_quest_pointing_at_a_missing_item_reports_the_audit_rule()
    {
        var quest = QuestWith(Objective("collect", "lost-cargo"));

        var result = new QuestValidator().Validate(quest, Catalog);

        var issue = result.Errors.Should().ContainSingle(i => i.Rule == ContentReferenceRules.MissingQuestTarget).Subject;
        issue.Identifier.Should().Be("the-lost-shipment");
        issue.Message.Should().Contain("lost-cargo");
    }

    [Fact]
    public void The_validator_and_the_startup_audit_report_the_same_rule_and_identifier()
    {
        var quest = QuestWith(Objective("collect", "lost-cargo"));

        var fromValidator = new QuestValidator().Validate(quest, Catalog).Errors;
        var fromAudit = ContentReferenceRules.AuditQuestObjectives(
            [new QuestObjectiveReference(quest.Slug, "collect", "lost-cargo")],
            Catalog.Archetypes,
            Catalog.Items,
            Catalog.Locations);

        fromValidator.Should().BeEquivalentTo(fromAudit);
    }

    [Fact]
    public void A_quest_using_a_reserved_objective_kind_is_rejected()
    {
        var quest = QuestWith(Objective("deliver", "iron-sword"));

        var result = new QuestValidator().Validate(quest, Catalog);

        result.Errors.Should().ContainSingle(issue => issue.Rule == ContentReferenceRules.UnsupportedQuestKind);
    }

    [Fact]
    public void A_quest_objective_with_a_zero_quantity_is_rejected()
    {
        var quest = QuestWith(Objective("collect", "glowcap-mushroom", quantity: 0));

        var result = new QuestValidator().Validate(quest, Catalog);

        result.Errors.Should().ContainSingle(issue => issue.Rule == ContentRules.QuestObjectiveInvalid);
    }

    [Fact]
    public void A_valid_quest_passes()
    {
        var quest = QuestWith(
            Objective("collect", "glowcap-mushroom", 3),
            Objective("kill", "giant-rat", 5),
            Objective("reach_level", "5"));

        new QuestValidator().Validate(quest, Catalog).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Reach_level_is_checked_as_a_number_not_a_slug()
    {
        var quest = QuestWith(Objective("reach_level", "soon"));

        var result = new QuestValidator().Validate(quest, Catalog);

        result.Errors.Should().ContainSingle(issue => issue.Rule == ContentReferenceRules.MissingQuestTarget);
    }

    // ── Loot tables and recipes ────────────────────────────────────────────────

    [Fact]
    public void An_empty_loot_table_is_rejected()
    {
        var table = new LootTable { Slug = "wolves", TypeKey = "enemies", DisplayName = "Wolves" };

        var result = new LootTableValidator().Validate(table, Catalog);

        result.Errors.Should().ContainSingle(issue => issue.Rule == ContentRules.LootTableWithoutEntries);
    }

    [Fact]
    public void A_loot_entry_with_a_zero_weight_is_rejected()
    {
        var table = new LootTable
        {
            Slug = "wolves",
            TypeKey = "enemies",
            DisplayName = "Wolves",
            Entries =
            [
                new LootTableEntry { ItemSlug = "glowcap-mushroom", DropWeight = 0 },
            ],
        };

        var result = new LootTableValidator().Validate(table, Catalog);

        result.Errors.Should().ContainSingle(issue => issue.Rule == ContentRules.LootWeightInvalid);
    }

    [Fact]
    public void A_loot_entry_pointing_at_a_missing_item_is_rejected()
    {
        var table = new LootTable
        {
            Slug = "wolves",
            TypeKey = "enemies",
            DisplayName = "Wolves",
            Entries = [new LootTableEntry { ItemSlug = "wolf-pelt", DropWeight = 50 }],
        };

        var result = new LootTableValidator().Validate(table, Catalog);

        result.Errors.Should().ContainSingle(issue => issue.Rule == ContentRules.MissingLootItem);
    }

    [Fact]
    public void A_recipe_producing_a_missing_item_is_rejected()
    {
        var recipe = new Recipe
        {
            Slug = "iron-sword",
            TypeKey = "blacksmithing",
            DisplayName = "Iron Sword",
            OutputItemSlug = "steel-sword",
            OutputQuantity = 1,
            CraftingSkill = "blacksmithing",
            CraftingLevel = 1,
            Ingredients = [new RecipeIngredient { ItemSlug = "iron-sword", Quantity = 2 }],
        };

        var result = new RecipeValidator().Validate(recipe, Catalog);

        result.Errors.Should().ContainSingle(issue => issue.Field == "outputItemSlug");
    }

    [Fact]
    public void A_recipe_consuming_nothing_only_warns()
    {
        var recipe = new Recipe
        {
            Slug = "iron-sword",
            TypeKey = "blacksmithing",
            DisplayName = "Iron Sword",
            OutputItemSlug = "iron-sword",
            OutputQuantity = 1,
            CraftingSkill = "blacksmithing",
            CraftingLevel = 1,
        };

        var result = new RecipeValidator().Validate(recipe, Catalog);

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle(issue => issue.Field == "ingredients");
    }

    // ── World and actors ───────────────────────────────────────────────────────

    [Fact]
    public void A_location_spawning_an_unknown_archetype_reports_the_audit_rule()
    {
        var location = new ZoneLocation
        {
            Slug = "grey-cup",
            TypeKey = "town",
            DisplayName = "The Grey Cup",
            Stats = new ZoneLocationStats { MinLevel = 1, MaxLevel = 3 },
            ActorPool = [new ActorPoolEntry { ArchetypeSlug = "dire-wolf", Weight = 10 }],
        };

        var result = new ZoneLocationValidator().Validate(location, Catalog);

        result.Errors.Should().ContainSingle(issue => issue.Rule == ContentReferenceRules.MissingSpawnArchetype);
    }

    [Fact]
    public void A_location_with_an_empty_spawn_pool_only_warns()
    {
        var location = new ZoneLocation
        {
            Slug = "grey-cup",
            TypeKey = "town",
            DisplayName = "The Grey Cup",
            Stats = new ZoneLocationStats { MinLevel = 1, MaxLevel = 3 },
        };

        var result = new ZoneLocationValidator().Validate(location, Catalog);

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle(issue => issue.Rule == ContentRules.SpawnPoolEmpty);
    }

    [Fact]
    public void A_location_with_an_inverted_level_band_is_rejected()
    {
        var location = new ZoneLocation
        {
            Slug = "grey-cup",
            TypeKey = "town",
            DisplayName = "The Grey Cup",
            Stats = new ZoneLocationStats { MinLevel = 9, MaxLevel = 3 },
        };

        var result = new ZoneLocationValidator().Validate(location, Catalog);

        result.Errors.Should().ContainSingle(issue => issue.Rule == ContentRules.LevelBandInvalid);
    }

    [Fact]
    public void An_archetype_with_no_health_or_an_inverted_level_range_is_rejected()
    {
        var archetype = new ActorArchetype
        {
            Slug = "giant-rat",
            TypeKey = "vermin",
            DisplayName = "Giant Rat",
            MinLevel = 5,
            MaxLevel = 1,
            Stats = new ArchetypeStats { Health = 0 },
        };

        var result = new ActorArchetypeValidator().Validate(archetype, Catalog);

        result.Errors.Select(issue => issue.Rule).Should().Contain([
            ContentRules.ArchetypeRangeInvalid,
            ContentRules.ValueOutOfRange,
        ]);
    }

    [Fact]
    public void An_actor_instance_without_an_archetype_is_rejected()
    {
        var instance = new ActorInstance
        {
            Slug = "giant-rat-of-the-cellar",
            TypeKey = "vermin",
            DisplayName = "Giant Rat of the Cellar",
        };

        var result = new ActorInstanceValidator().Validate(instance, Catalog);

        result.Errors.Should().ContainSingle(issue => issue.Rule == ContentRules.InstanceWithoutArchetype);
    }

    [Fact]
    public void A_dialogue_with_no_lines_is_rejected()
    {
        var dialogue = new Dialogue
        {
            Slug = "innkeeper-halda-greeting",
            TypeKey = "greetings",
            DisplayName = "Halda's greeting",
            Speaker = "innkeeper-halda",
        };

        var result = new DialogueValidator().Validate(dialogue, Catalog);

        result.Errors.Should().ContainSingle(issue => issue.Rule == ContentRules.DialogueWithoutLines);
    }

    // ── Registry behaviour ─────────────────────────────────────────────────────

    [Fact]
    public void Every_structural_family_is_registered()
    {
        var registry = ContentValidatorRegistry.CreateDefault();

        foreach (var contentType in ContentValidatorRegistry.StructuralContentTypes)
        {
            registry.TryGetValidator(contentType, out var validator).Should().BeTrue();
            validator.ContentType.Should().Be(contentType);
        }
    }

    [Fact]
    public void An_unknown_content_type_throws_with_the_known_types_listed()
    {
        var registry = ContentValidatorRegistry.CreateDefault();

        var act = () => registry.Validate("spaceship", Sword(), Catalog);

        act.Should().Throw<KeyNotFoundException>().WithMessage("*spaceship*item*");
    }

    [Fact]
    public void Passing_the_wrong_entity_type_is_a_programming_error_not_an_issue()
    {
        var act = () => new ItemValidator().Validate(QuestWith(Objective("kill", "giant-rat")), Catalog);

        act.Should().Throw<ArgumentException>().WithMessage("*Item*");
    }
}
