using RealmEngine.Data.Content;
using RealmEngine.Data.Entities;

namespace RealmEngine.Data.Validation.Content;

/// <summary>
/// Validates zone locations: level bands must be sane and every spawn-pool entry must resolve to a real archetype.
/// </summary>
public sealed class ZoneLocationValidator : ContentValidator<ZoneLocation>
{
    /// <inheritdoc />
    public override string ContentType => "zonelocation";

    /// <inheritdoc />
    protected override IReadOnlyList<ContentIssue> ValidateEntity(ZoneLocation entity, ContentReferenceCatalog catalog)
    {
        var issues = new List<ContentIssue>();
        ContentShapeRules.ValidateShared(entity, issues);

        if (entity.Stats.MinLevel is not null && entity.Stats.MaxLevel is not null && entity.Stats.MinLevel > entity.Stats.MaxLevel)
        {
            AddError(
                issues,
                ContentRules.LevelBandInvalid,
                $"Location '{entity.Slug}' has min level {entity.Stats.MinLevel} above max level {entity.Stats.MaxLevel}.",
                entity.Slug,
                "stats.minLevel");
        }

        if (entity.Stats.DangerLevel is < 0)
        {
            AddError(
                issues,
                ContentRules.ValueOutOfRange,
                $"Location '{entity.Slug}' has a negative danger level ({entity.Stats.DangerLevel}).",
                entity.Slug,
                "stats.dangerLevel");
        }

        // Null-tolerant so a null spawn pool is an issue, not an unhandled exception (see the loot-table note).
        var actorPool = entity.ActorPool ?? [];

        if (actorPool.Count == 0)
        {
            AddWarning(
                issues,
                ContentRules.SpawnPoolEmpty,
                $"Location '{entity.Slug}' has an empty spawn pool. Safe locations are legitimate, so this is only a warning.",
                entity.Slug,
                "actorPool");
        }

        issues.AddRange(ContentReferenceRules.AuditSpawnPools(
            [new SpawnPoolReference(entity.Slug, [.. actorPool.Select(entry => entry.ArchetypeSlug)])],
            catalog.Archetypes));

        var index = 0;
        foreach (var entry in actorPool)
        {
            if (entry.Weight <= 0)
            {
                AddError(
                    issues,
                    ContentRules.LootWeightInvalid,
                    $"Location '{entity.Slug}' spawn entry '{entry.ArchetypeSlug}' has weight {entry.Weight}; it would never spawn.",
                    entity.Slug,
                    $"actorPool[{index}].weight");
            }

            index++;
        }

        return issues;
    }
}

/// <summary>
/// Validates actor archetypes: level and reward bands must be sane, and every soft reference must resolve.
/// </summary>
public sealed class ActorArchetypeValidator : ContentValidator<ActorArchetype>
{
    /// <inheritdoc />
    public override string ContentType => "archetype";

    /// <inheritdoc />
    protected override IReadOnlyList<ContentIssue> ValidateEntity(ActorArchetype entity, ContentReferenceCatalog catalog)
    {
        var issues = new List<ContentIssue>();
        ContentShapeRules.ValidateShared(entity, issues);

        if (entity.MinLevel > entity.MaxLevel)
        {
            AddError(
                issues,
                ContentRules.ArchetypeRangeInvalid,
                $"Archetype '{entity.Slug}' has min level {entity.MinLevel} above max level {entity.MaxLevel}.",
                entity.Slug,
                "minLevel");
        }

        ContentShapeRules.ValidateRange(
            issues,
            entity.Slug,
            "stats.goldRewardMin",
            entity.Stats.GoldRewardMin,
            entity.Stats.GoldRewardMax);

        if (entity.Stats.Health is <= 0)
        {
            AddError(
                issues,
                ContentRules.ValueOutOfRange,
                $"Archetype '{entity.Slug}' has {entity.Stats.Health?.ToString() ?? "no"} health, so it would die before it could act.",
                entity.Slug,
                "stats.health");
        }

        if (entity.Stats.ExperienceReward is < 0)
        {
            AddError(
                issues,
                ContentRules.ValueOutOfRange,
                $"Archetype '{entity.Slug}' grants negative experience ({entity.Stats.ExperienceReward}).",
                entity.Slug,
                "stats.experienceReward");
        }

        return issues;
    }
}

/// <summary>
/// Validates actor instances: an instance must be an override of an existing archetype, not a replacement for one,
/// and every field its overrides actually apply must be usable where it lands.
/// </summary>
/// <remarks>
/// <c>ArmorClass</c> and <c>AttackBonus</c> are deliberately absent from this list: both are reserved and have no
/// target on the spawned template, so a value there changes nothing and is not a rule to enforce.
/// </remarks>
public sealed class ActorInstanceValidator : ContentValidator<ActorInstance>
{
    /// <inheritdoc />
    public override string ContentType => "instance";

    /// <inheritdoc />
    protected override IReadOnlyList<ContentIssue> ValidateEntity(ActorInstance entity, ContentReferenceCatalog catalog)
    {
        var issues = new List<ContentIssue>();
        ContentShapeRules.ValidateShared(entity, issues);

        if (entity.ArchetypeId == Guid.Empty)
        {
            AddError(
                issues,
                ContentRules.InstanceWithoutArchetype,
                $"Actor instance '{entity.Slug}' has no archetype, so it has no stats, powers or loot to inherit.",
                entity.Slug,
                "archetypeId");
        }

        if (entity.LevelOverride is <= 0)
        {
            AddError(
                issues,
                ContentRules.ValueOutOfRange,
                $"Actor instance '{entity.Slug}' has level override {entity.LevelOverride}; it must be at least 1.",
                entity.Slug,
                "levelOverride");
        }

        var overrides = entity.StatOverrides;

        RequirePositive(issues, entity, "statOverrides.health", overrides.Health, "health");
        RequirePositive(issues, entity, "statOverrides.mana", overrides.Mana, "mana");
        RequirePositive(issues, entity, "statOverrides.damage", overrides.Damage, "damage");
        RequirePositive(issues, entity, "statOverrides.strength", overrides.Strength, "strength");
        RequirePositive(issues, entity, "statOverrides.agility", overrides.Agility, "agility");
        RequirePositive(issues, entity, "statOverrides.intelligence", overrides.Intelligence, "intelligence");
        RequirePositive(issues, entity, "statOverrides.constitution", overrides.Constitution, "constitution");

        // Zero experience is legitimate; a negative reward would pay the player back for a kill.
        if (overrides.ExperienceReward is < 0)
        {
            AddError(
                issues,
                ContentRules.ValueOutOfRange,
                $"Actor instance '{entity.Slug}' grants negative experience ({overrides.ExperienceReward}).",
                entity.Slug,
                "statOverrides.experienceReward");
        }

        // The pair is composed and rolled as one range, so an inverted or negative pair has to fail here rather than
        // surface as a nonsense reward at spawn time.
        ContentShapeRules.ValidateRange(
            issues,
            entity.Slug,
            "statOverrides.goldRewardMin",
            overrides.GoldRewardMin,
            overrides.GoldRewardMax);

        if (entity.LocationSlug is { } location && !catalog.Locations.Contains(location))
        {
            ContentShapeRules.AddMissingReference(
                issues,
                entity.Slug,
                "locationSlug",
                ContentRules.MissingPlacementLocation,
                location,
                "zone location");
        }

        if (entity.LootTableOverride is { } lootTableId && !catalog.LootTableIds.Contains(lootTableId))
        {
            ContentShapeRules.AddMissingReference(
                issues,
                entity.Slug,
                "lootTableOverride",
                ContentRules.MissingLootTable,
                lootTableId.ToString(),
                "loot table");
        }

        return issues;
    }

    /// <summary>Reports an applied override that is zero or negative.</summary>
    /// <param name="issues">Issue list to append to.</param>
    /// <param name="entity">The instance being validated.</param>
    /// <param name="field">Dotted field path of the override.</param>
    /// <param name="value">The override value, when present.</param>
    /// <param name="meaning">Human-readable name of the stat.</param>
    private void RequirePositive(
        List<ContentIssue> issues,
        ActorInstance entity,
        string field,
        int? value,
        string meaning)
    {
        if (value is null or >= 1)
        {
            return;
        }

        AddError(
            issues,
            ContentRules.ValueOutOfRange,
            $"Actor instance '{entity.Slug}' overrides {meaning} to {value}; an override is an absolute value, so it must be at least 1.",
            entity.Slug,
            field);
    }
}

/// <summary>
/// Validates dialogues: lines that can be spoken, and options that can actually be offered.
/// </summary>
/// <remarks>
/// The option rules are the ones that matter for dialogue depth: an option nobody can read, a requirement the
/// runtime cannot parse, and a grant that names content which does not exist are all silent failures in play — the
/// panel simply shows less than the author wrote. Everything here fails at authoring time instead.
/// </remarks>
public sealed class DialogueValidator : ContentValidator<Dialogue>
{
    /// <inheritdoc />
    public override string ContentType => "dialogue";

    /// <inheritdoc />
    protected override IReadOnlyList<ContentIssue> ValidateEntity(Dialogue entity, ContentReferenceCatalog catalog)
    {
        var issues = new List<ContentIssue>();
        ContentShapeRules.ValidateShared(entity, issues);

        // Null-tolerant so a null line list is an issue, not an unhandled exception (see the loot-table note).
        var lines = entity.Stats.Lines ?? [];

        if (lines.Count == 0)
        {
            AddError(
                issues,
                ContentRules.DialogueWithoutLines,
                $"Dialogue '{entity.Slug}' has no lines, so speaking to it would show nothing.",
                entity.Slug,
                "stats.lines");
        }

        if (lines.Any(string.IsNullOrWhiteSpace))
        {
            AddWarning(
                issues,
                ContentRules.ValueOutOfRange,
                $"Dialogue '{entity.Slug}' contains a blank line.",
                entity.Slug,
                "stats.lines");
        }

        if (string.IsNullOrWhiteSpace(entity.Speaker))
        {
            AddWarning(
                issues,
                ContentRules.FieldInconsistent,
                $"Dialogue '{entity.Slug}' names no speaker, so the client has no attribution.",
                entity.Slug,
                "speaker");
        }

        ValidateOptions(entity, catalog, issues);

        return issues;
    }

    private void ValidateOptions(Dialogue entity, ContentReferenceCatalog catalog, List<ContentIssue> issues)
    {
        var options = entity.Stats.Options;
        if (options is null || options.Count == 0)
        {
            return;
        }

        if (options.Count > DialogueOptionGrammar.MaxOptionsPerEntry)
        {
            AddError(
                issues,
                ContentRules.DialogueOptionInvalid,
                $"Dialogue '{entity.Slug}' has {options.Count} options; at most " +
                $"{DialogueOptionGrammar.MaxOptionsPerEntry} can be shown in one conversation.",
                entity.Slug,
                "stats.options");
        }

        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < options.Count; index++)
        {
            var option = options[index];
            var field = $"stats.options[{index}]";
            var named = string.IsNullOrWhiteSpace(option.Key) ? $"option #{index + 1}" : $"option '{option.Key}'";

            if (string.IsNullOrWhiteSpace(option.Key))
            {
                AddError(
                    issues,
                    ContentRules.DialogueOptionInvalid,
                    $"Dialogue '{entity.Slug}' {named} has no key, so the client cannot refer to it.",
                    entity.Slug,
                    $"{field}.key");
            }
            else if (!seenKeys.Add(option.Key))
            {
                AddError(
                    issues,
                    ContentRules.DialogueOptionInvalid,
                    $"Dialogue '{entity.Slug}' {named} repeats a key used by an earlier option; the client would " +
                    "always pick the first one.",
                    entity.Slug,
                    $"{field}.key");
            }

            if (string.IsNullOrWhiteSpace(option.Label))
            {
                AddError(
                    issues,
                    ContentRules.DialogueOptionInvalid,
                    $"Dialogue '{entity.Slug}' {named} has no label, so the player sees an empty button.",
                    entity.Slug,
                    $"{field}.label");
            }
            else if (option.Label.Length > DialogueOptionGrammar.MaxLabelLength)
            {
                AddError(
                    issues,
                    ContentRules.DialogueOptionInvalid,
                    $"Dialogue '{entity.Slug}' {named} has a {option.Label.Length}-character label; the panel " +
                    $"shows at most {DialogueOptionGrammar.MaxLabelLength}.",
                    entity.Slug,
                    $"{field}.label");
            }

            if (string.IsNullOrWhiteSpace(option.Response))
            {
                AddError(
                    issues,
                    ContentRules.DialogueOptionInvalid,
                    $"Dialogue '{entity.Slug}' {named} has no response, so choosing it would say nothing.",
                    entity.Slug,
                    $"{field}.response");
            }

            if (!string.IsNullOrWhiteSpace(option.NextSlug))
            {
                if (string.Equals(option.NextSlug, entity.Slug, StringComparison.OrdinalIgnoreCase))
                {
                    AddError(
                        issues,
                        ContentRules.DialogueOptionInvalid,
                        $"Dialogue '{entity.Slug}' {named} continues into itself; a conversation cannot chain to " +
                        "the entry it is already in.",
                        entity.Slug,
                        $"{field}.nextSlug");
                }
                else if (!catalog.Dialogues.Contains(option.NextSlug))
                {
                    AddError(
                        issues,
                        ContentRules.MissingDialogueReference,
                        $"Dialogue '{entity.Slug}' {named} continues into '{option.NextSlug}', which is not a " +
                        "dialogue entry.",
                        entity.Slug,
                        $"{field}.nextSlug");
                }
            }

            ValidateEffects(entity, option, named, field, requirement: true, catalog, issues);
            var grants = ValidateEffects(entity, option, named, field, requirement: false, catalog, issues);

            if (grants > 0 && option.OneShot == false)
            {
                AddWarning(
                    issues,
                    ContentRules.DialogueRepeatableGrant,
                    $"Dialogue '{entity.Slug}' {named} grants {grants} consequence(s) but is marked repeatable, so " +
                    "the player can claim them again and again.",
                    entity.Slug,
                    $"{field}.oneShot");
            }
        }
    }

    /// <summary>Validates one side of an option and returns how many entries it declares.</summary>
    private int ValidateEffects(
        Dialogue entity,
        DialogueOption option,
        string named,
        string field,
        bool requirement,
        ContentReferenceCatalog catalog,
        List<ContentIssue> issues)
    {
        var entries = DialogueOptionGrammar.Split(requirement ? option.Requires : option.Grants);
        var side = requirement ? "requires" : "grants";

        foreach (var entry in entries)
        {
            DialogueEffect effect;
            string error;
            var parsed = requirement
                ? DialogueOptionGrammar.TryParseRequirement(entry, out effect, out error)
                : DialogueOptionGrammar.TryParseGrant(entry, out effect, out error);

            if (!parsed)
            {
                AddError(
                    issues,
                    ContentRules.DialogueEffectInvalid,
                    $"Dialogue '{entity.Slug}' {named} {side} '{entry}': {error}.",
                    entity.Slug,
                    $"{field}.{side}");
                continue;
            }

            var reference = effect.Kind switch
            {
                DialogueEffectKind.Item => effect.Key,
                DialogueEffectKind.QuestActive or DialogueEffectKind.QuestComplete or DialogueEffectKind.QuestStart
                    => effect.Key,
                _ => null,
            };

            if (reference is null)
            {
                continue;
            }

            var resolves = effect.Kind == DialogueEffectKind.Item
                ? catalog.Items.Contains(reference) || catalog.Materials.Contains(reference)
                : catalog.Quests.Contains(reference);

            if (!resolves)
            {
                var what = effect.Kind == DialogueEffectKind.Item ? "item" : "quest";
                AddError(
                    issues,
                    ContentRules.MissingDialogueReference,
                    $"Dialogue '{entity.Slug}' {named} {side} '{entry}': no {what} '{reference}' exists in content.",
                    entity.Slug,
                    $"{field}.{side}");
            }
        }

        return entries.Count;
    }
}

/// <summary>
/// Validates the families whose rules are currently only structural: species, classes, backgrounds, skills, materials,
/// material properties, enchantments and organisations.
/// </summary>
/// <remarks>
/// One instance per content-type key from <see cref="ContentValidatorRegistry"/> — the shared rules are identical, so a
/// class per family would be eight copies of the same code. When a family grows a real rule, it gets its own validator.
/// </remarks>
public sealed class StructuralContentValidator : ContentValidator<ContentBase>
{
    private readonly string _contentType;

    /// <summary>Initializes a new instance of the <see cref="StructuralContentValidator"/> class.</summary>
    /// <param name="contentType">The content-type key this instance handles.</param>
    public StructuralContentValidator(string contentType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        _contentType = contentType;
    }

    /// <inheritdoc />
    public override string ContentType => _contentType;

    /// <inheritdoc />
    protected override IReadOnlyList<ContentIssue> ValidateEntity(ContentBase entity, ContentReferenceCatalog catalog)
    {
        var issues = new List<ContentIssue>();
        ContentShapeRules.ValidateShared(entity, issues);
        return issues;
    }
}
