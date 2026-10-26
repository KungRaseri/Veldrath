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

        if (entity.ActorPool.Count == 0)
        {
            AddWarning(
                issues,
                ContentRules.SpawnPoolEmpty,
                $"Location '{entity.Slug}' has an empty spawn pool. Safe locations are legitimate, so this is only a warning.",
                entity.Slug,
                "actorPool");
        }

        issues.AddRange(ContentReferenceRules.AuditSpawnPools(
            [new SpawnPoolReference(entity.Slug, [.. entity.ActorPool.Select(entry => entry.ArchetypeSlug)])],
            catalog.Archetypes));

        var index = 0;
        foreach (var entry in entity.ActorPool)
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
/// Validates actor instances: an instance must be an override of an existing archetype, not a replacement for one.
/// </summary>
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

        ContentShapeRules.ValidateRange(
            issues,
            entity.Slug,
            "statOverrides.health",
            entity.StatOverrides.Health,
            null);

        return issues;
    }
}

/// <summary>Validates dialogues: a dialogue with no lines can never be spoken.</summary>
public sealed class DialogueValidator : ContentValidator<Dialogue>
{
    /// <inheritdoc />
    public override string ContentType => "dialogue";

    /// <inheritdoc />
    protected override IReadOnlyList<ContentIssue> ValidateEntity(Dialogue entity, ContentReferenceCatalog catalog)
    {
        var issues = new List<ContentIssue>();
        ContentShapeRules.ValidateShared(entity, issues);

        if (entity.Stats.Lines.Count == 0)
        {
            AddError(
                issues,
                ContentRules.DialogueWithoutLines,
                $"Dialogue '{entity.Slug}' has no lines, so speaking to it would show nothing.",
                entity.Slug,
                "stats.lines");
        }

        if (entity.Stats.Lines.Any(string.IsNullOrWhiteSpace))
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

        return issues;
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
