using RealmEngine.Data.Entities;

namespace RealmEngine.Data.Validation.Content;

/// <summary>
/// Validates items — weapons, armour and consumables share one table, so one validator covers the
/// <c>item</c>, <c>weapon</c> and <c>armor</c> content types.
/// </summary>
public sealed class ItemValidator : ContentValidator<Item>
{
    /// <inheritdoc />
    public override string ContentType => "item";

    /// <inheritdoc />
    protected override IReadOnlyList<ContentIssue> ValidateEntity(Item entity, ContentReferenceCatalog catalog)
    {
        var issues = new List<ContentIssue>();
        ContentShapeRules.ValidateShared(entity, issues);

        if (string.IsNullOrWhiteSpace(entity.ItemType))
        {
            AddError(
                issues,
                ContentRules.DiscriminatorMissing,
                $"Item '{entity.Slug}' has no item type, so nothing can decide how to use or equip it.",
                entity.Slug,
                "itemType");
            return issues;
        }

        var type = entity.ItemType.Trim().ToLowerInvariant();

        ContentShapeRules.ValidateRange(issues, entity.Slug, "stats.damageMin", entity.Stats.DamageMin, entity.Stats.DamageMax);

        if (type == "weapon")
        {
            if ((entity.Stats.DamageMin is null && entity.Stats.DamageMax is null)
                || (entity.Stats.DamageMin == 0 && entity.Stats.DamageMax == 0))
            {
                AddWarning(
                    issues,
                    ContentRules.ValueOutOfRange,
                    $"Weapon '{entity.Slug}' deals no damage (damageMin {entity.Stats.DamageMin?.ToString() ?? "unset"}, damageMax {entity.Stats.DamageMax?.ToString() ?? "unset"}).",
                    entity.Slug,
                    "stats.damageMin");
            }

            if (string.IsNullOrWhiteSpace(entity.WeaponType))
            {
                AddError(
                    issues,
                    ContentRules.FieldInconsistent,
                    $"Weapon '{entity.Slug}' has no weapon type, so no ability can require it.",
                    entity.Slug,
                    "weaponType");
            }

            if (entity.Traits.TwoHanded == true && entity.HandsRequired is 1)
            {
                AddWarning(
                    issues,
                    ContentRules.FieldInconsistent,
                    $"Weapon '{entity.Slug}' is flagged two-handed but declares handsRequired 1.",
                    entity.Slug,
                    "handsRequired");
            }
        }
        else if (type == "armor")
        {
            if (string.IsNullOrWhiteSpace(entity.ArmorType))
            {
                AddError(
                    issues,
                    ContentRules.FieldInconsistent,
                    $"Armour '{entity.Slug}' has no armour type, so no ability can require it.",
                    entity.Slug,
                    "armorType");
            }

            if (string.IsNullOrWhiteSpace(entity.EquipSlot))
            {
                AddError(
                    issues,
                    ContentRules.FieldInconsistent,
                    $"Armour '{entity.Slug}' has no equip slot, so it can never be worn.",
                    entity.Slug,
                    "equipSlot");
            }

            ContentShapeRules.ValidateRange(issues, entity.Slug, "stats.armorRating", entity.Stats.ArmorRating, null);
        }
        else if (entity.Traits.Consumable == true && entity.Stats.EffectPower is null)
        {
            AddWarning(
                issues,
                ContentRules.FieldInconsistent,
                $"Consumable '{entity.Slug}' has no effect power, so using it would do nothing.",
                entity.Slug,
                "stats.effectPower");
        }

        return issues;
    }
}

/// <summary>
/// Validates powers — abilities and spells share the <see cref="Power"/> table, so one validator covers the
/// <c>ability</c> and <c>spell</c> content types.
/// </summary>
public sealed class PowerValidator : ContentValidator<Power>
{
    /// <inheritdoc />
    public override string ContentType => "ability";

    /// <inheritdoc />
    protected override IReadOnlyList<ContentIssue> ValidateEntity(Power entity, ContentReferenceCatalog catalog)
    {
        var issues = new List<ContentIssue>();
        ContentShapeRules.ValidateShared(entity, issues);

        if (string.IsNullOrWhiteSpace(entity.PowerType))
        {
            AddError(
                issues,
                ContentRules.DiscriminatorMissing,
                $"Power '{entity.Slug}' has no power type, so nothing can decide whether it is active, passive or reactive.",
                entity.Slug,
                "powerType");
        }

        ContentShapeRules.ValidateRange(issues, entity.Slug, "stats.damageMin", entity.Stats.DamageMin, entity.Stats.DamageMax);
        ContentShapeRules.ValidateRange(issues, entity.Slug, "stats.healMin", entity.Stats.HealMin, entity.Stats.HealMax);

        if (entity.Stats.ManaCost < 0)
        {
            AddError(
                issues,
                ContentRules.ValueOutOfRange,
                $"Power '{entity.Slug}' has a negative mana cost ({entity.Stats.ManaCost}); a power cannot refund mana.",
                entity.Slug,
                "stats.manaCost");
        }

        if (entity.Traits.HasCooldown == true && entity.Stats.Cooldown is null or <= 0)
        {
            AddError(
                issues,
                ContentRules.FieldInconsistent,
                $"Power '{entity.Slug}' is flagged as having a cooldown but declares none.",
                entity.Slug,
                "stats.cooldown");
        }

        if (!string.IsNullOrWhiteSpace(entity.Effects.ConditionApplied)
            && entity.Effects.ConditionChance is null or <= 0)
        {
            AddError(
                issues,
                ContentRules.FieldInconsistent,
                $"Power '{entity.Slug}' applies condition '{entity.Effects.ConditionApplied}' with no chance, so it would never apply.",
                entity.Slug,
                "effects.conditionChance");
        }

        if (!string.IsNullOrWhiteSpace(entity.RequiresItem)
            && !catalog.Items.Contains(entity.RequiresItem))
        {
            ContentShapeRules.AddMissingReference(
                issues,
                entity.Slug,
                "requiresItem",
                ContentRules.MissingQuestTarget,
                entity.RequiresItem,
                "item");
        }

        return issues;
    }
}
