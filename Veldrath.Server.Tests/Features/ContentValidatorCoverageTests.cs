using RealmEngine.Data.Validation.Content;
using Veldrath.Contracts.Content;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Guards the contract B2 depends on: every content type the schema advertises must have a validator, and the
/// startup audit must be running the same reference rules the write path uses.
/// </summary>
[Trait("Category", "Validation")]
public class ContentValidatorCoverageTests
{
    [Fact]
    public void Every_schema_content_type_has_a_validator()
    {
        var registry = ContentValidatorRegistry.CreateDefault();

        var missing = ContentSchemaRegistry.AllTypes
            .Where(contentType => !registry.TryGetValidator(contentType, out _))
            .ToList();

        missing.Should().BeEmpty(
            "the write endpoint resolves a validator from the URL segment, so an unregistered type would fail at runtime");
    }

    [Fact]
    public void The_registry_does_not_advertise_types_the_schema_cannot_describe()
    {
        var registry = ContentValidatorRegistry.CreateDefault();

        var unknown = registry.ContentTypes
            .Where(contentType => ContentSchemaRegistry.Get(contentType) is null)
            .ToList();

        unknown.Should().BeEmpty("a validator with no schema has no field metadata, so Foundry could not render it");
    }

    [Fact]
    public void The_audit_and_the_validator_use_the_same_rule_ids()
    {
        // The rule ids are the contract for "same defect, same rule and identifier" across the audit, the write path
        // and the CI guard. These are the values the audit shipped with, so a rename must be deliberate.
        ContentReferenceRules.MissingSpawnArchetype.Should().Be("missing-spawn-archetype");
        ContentReferenceRules.MissingQuestTarget.Should().Be("missing-quest-target");
        ContentReferenceRules.UnsupportedQuestKind.Should().Be("unsupported-quest-kind");
        ContentReferenceRules.MissingMapNode.Should().Be("missing-map-node");
    }

    [Fact]
    public void The_default_registry_covers_every_family_the_engine_stores()
    {
        var registry = ContentValidatorRegistry.CreateDefault();

        registry.ContentTypes.Should().BeEquivalentTo(new[]
        {
            "ability", "spell", "species", "class", "archetype", "instance", "background", "skill",
            "weapon", "armor", "item", "material", "materialproperty", "enchantment", "quest", "recipe",
            "loottable", "organization", "zonelocation", "dialogue", "language",
        });
    }
}
