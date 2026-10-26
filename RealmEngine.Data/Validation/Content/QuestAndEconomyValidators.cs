using RealmEngine.Data.Entities;

namespace RealmEngine.Data.Validation.Content;

/// <summary>
/// Validates quests: they must be completable, their objectives must resolve, and their rewards must be sane.
/// </summary>
/// <remarks>
/// The objective rules delegate to <see cref="ContentReferenceRules.AuditQuestObjectives"/>, which is the same code
/// the startup audit runs — that is what keeps the audit and the write path reporting one rule and one identifier.
/// </remarks>
public sealed class QuestValidator : ContentValidator<Quest>
{
    /// <inheritdoc />
    public override string ContentType => "quest";

    /// <inheritdoc />
    protected override IReadOnlyList<ContentIssue> ValidateEntity(Quest entity, ContentReferenceCatalog catalog)
    {
        var issues = new List<ContentIssue>();
        ContentShapeRules.ValidateShared(entity, issues);

        if (entity.MinLevel < 0)
        {
            AddError(
                issues,
                ContentRules.ValueOutOfRange,
                $"Quest '{entity.Slug}' has a negative minimum level ({entity.MinLevel}).",
                entity.Slug,
                "minLevel");
        }

        foreach (var (rewardField, amount) in new[]
                 {
                     ("stats.xpReward", entity.Stats.XpReward),
                     ("stats.goldReward", entity.Stats.GoldReward),
                     ("stats.reputationReward", entity.Stats.ReputationReward),
                 })
        {
            if (amount < 0)
            {
                AddError(
                    issues,
                    ContentRules.ValueOutOfRange,
                    $"Quest '{entity.Slug}' has a negative reward in {rewardField} ({amount}).",
                    entity.Slug,
                    rewardField);
            }
        }

        var objectives = entity.Objectives.Items;

        if (objectives.Count == 0)
        {
            AddError(
                issues,
                ContentRules.QuestWithoutObjectives,
                $"Quest '{entity.Slug}' declares no objectives, so it would be completable the moment it is accepted.",
                entity.Slug,
                "objectives");
            return issues;
        }

        for (var index = 0; index < objectives.Count; index++)
        {
            var objective = objectives[index];
            var field = $"objectives[{index}]";

            if (string.IsNullOrWhiteSpace(objective.Type))
            {
                AddError(
                    issues,
                    ContentRules.QuestObjectiveInvalid,
                    $"Quest '{entity.Slug}' objective #{index + 1} has no type.",
                    entity.Slug,
                    field);
                continue;
            }

            if (string.IsNullOrWhiteSpace(objective.Target))
            {
                AddError(
                    issues,
                    ContentRules.QuestObjectiveInvalid,
                    $"Quest '{entity.Slug}' objective '{objective.Type}' has no target.",
                    entity.Slug,
                    field);
                continue;
            }

            if (objective.Quantity < 1)
            {
                AddError(
                    issues,
                    ContentRules.QuestObjectiveInvalid,
                    $"Quest '{entity.Slug}' objective '{objective.Type}:{objective.Target}' has quantity {objective.Quantity}; it must be at least 1.",
                    entity.Slug,
                    field);
            }
        }

        issues.AddRange(ContentReferenceRules.AuditQuestObjectives(
            objectives.Select(objective => new QuestObjectiveReference(entity.Slug, objective.Type, objective.Target)),
            catalog.Archetypes,
            catalog.Items,
            catalog.Locations));

        return issues;
    }
}

/// <summary>
/// Validates loot tables: every table must be able to drop something, and every entry must resolve to a real item.
/// </summary>
public sealed class LootTableValidator : ContentValidator<LootTable>
{
    /// <inheritdoc />
    public override string ContentType => "loottable";

    /// <inheritdoc />
    protected override IReadOnlyList<ContentIssue> ValidateEntity(LootTable entity, ContentReferenceCatalog catalog)
    {
        var issues = new List<ContentIssue>();
        ContentShapeRules.ValidateShared(entity, issues);

        if (entity.Entries.Count == 0)
        {
            AddError(
                issues,
                ContentRules.LootTableWithoutEntries,
                $"Loot table '{entity.Slug}' has no entries, so it can never drop anything.",
                entity.Slug,
                "entries");
            return issues;
        }

        var index = 0;
        foreach (var entry in entity.Entries)
        {
            var field = $"entries[{index}]";

            if (entry.DropWeight <= 0)
            {
                AddError(
                    issues,
                    ContentRules.LootWeightInvalid,
                    $"Loot table '{entity.Slug}' entry '{entry.ItemSlug}' has drop weight {entry.DropWeight}; it would never drop.",
                    entity.Slug,
                    $"{field}.dropWeight");
            }

            ContentShapeRules.ValidateRange(
                issues,
                entity.Slug,
                $"{field}.quantityMin",
                entry.QuantityMin,
                entry.QuantityMax);

            if (!string.IsNullOrWhiteSpace(entry.ItemSlug) && !catalog.Items.Contains(entry.ItemSlug))
            {
                ContentShapeRules.AddMissingReference(
                    issues,
                    entity.Slug,
                    $"{field}.itemSlug",
                    ContentRules.MissingLootItem,
                    entry.ItemSlug,
                    "item");
            }

            index++;
        }

        return issues;
    }
}

/// <summary>
/// Validates recipes: the output and every ingredient must resolve, quantities must be positive, and the crafting
/// skill must exist.
/// </summary>
public sealed class RecipeValidator : ContentValidator<Recipe>
{
    /// <inheritdoc />
    public override string ContentType => "recipe";

    /// <inheritdoc />
    protected override IReadOnlyList<ContentIssue> ValidateEntity(Recipe entity, ContentReferenceCatalog catalog)
    {
        var issues = new List<ContentIssue>();
        ContentShapeRules.ValidateShared(entity, issues);

        if (string.IsNullOrWhiteSpace(entity.OutputItemSlug))
        {
            AddError(
                issues,
                ContentRules.RecipeInconsistent,
                $"Recipe '{entity.Slug}' produces no item.",
                entity.Slug,
                "outputItemSlug");
        }
        else if (!catalog.Items.Contains(entity.OutputItemSlug))
        {
            ContentShapeRules.AddMissingReference(
                issues,
                entity.Slug,
                "outputItemSlug",
                ContentRules.RecipeInconsistent,
                entity.OutputItemSlug,
                "item");
        }

        if (entity.OutputQuantity < 1)
        {
            AddError(
                issues,
                ContentRules.ValueOutOfRange,
                $"Recipe '{entity.Slug}' produces {entity.OutputQuantity} items; it must produce at least one.",
                entity.Slug,
                "outputQuantity");
        }

        if (string.IsNullOrWhiteSpace(entity.CraftingSkill))
        {
            AddError(
                issues,
                ContentRules.FieldInconsistent,
                $"Recipe '{entity.Slug}' names no crafting skill, so nothing can gate it.",
                entity.Slug,
                "craftingSkill");
        }
        else if (catalog.Skills.Count > 0 && !catalog.Skills.Contains(entity.CraftingSkill))
        {
            // A warning, not an error: `craftingSkill` is a crafting-profession name (blacksmithing, leatherworking)
            // and the game currently has no seeded skill catalog to resolve it against. Blocking on it would reject
            // every shipped recipe for a reference the game never reads.
            AddWarning(
                issues,
                ContentRules.FieldInconsistent,
                $"Recipe '{entity.Slug}' names crafting skill '{entity.CraftingSkill}', which is not in the skill catalog.",
                entity.Slug,
                "craftingSkill");
        }

        if (entity.CraftingLevel < 0)
        {
            AddError(
                issues,
                ContentRules.ValueOutOfRange,
                $"Recipe '{entity.Slug}' has a negative crafting level ({entity.CraftingLevel}).",
                entity.Slug,
                "craftingLevel");
        }

        if (entity.Ingredients.Count == 0)
        {
            AddWarning(
                issues,
                ContentRules.RecipeInconsistent,
                $"Recipe '{entity.Slug}' consumes nothing, so its output is free.",
                entity.Slug,
                "ingredients");
        }

        var index = 0;
        foreach (var ingredient in entity.Ingredients)
        {
            var field = $"ingredients[{index}]";

            if (ingredient.Quantity < 1)
            {
                AddError(
                    issues,
                    ContentRules.ValueOutOfRange,
                    $"Recipe '{entity.Slug}' ingredient '{ingredient.ItemSlug}' has quantity {ingredient.Quantity}; it must be at least 1.",
                    entity.Slug,
                    $"{field}.quantity");
            }

            // Ingredients carry their own domain (`items/materials` versus `items/general`), so the lookup has to use
            // it — resolving every ingredient against the item table would report every material as missing.
            if (!string.IsNullOrWhiteSpace(ingredient.ItemSlug)
                && !catalog.ForDomain(ingredient.ItemDomain).Contains(ingredient.ItemSlug))
            {
                ContentShapeRules.AddMissingReference(
                    issues,
                    entity.Slug,
                    $"{field}.itemSlug",
                    ContentRules.RecipeInconsistent,
                    ingredient.ItemSlug,
                    ingredient.ItemDomain?.Contains("materials", StringComparison.OrdinalIgnoreCase) == true
                        ? "material"
                        : "item");
            }

            index++;
        }

        return issues;
    }
}
