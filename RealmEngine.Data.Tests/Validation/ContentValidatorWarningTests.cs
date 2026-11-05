using RealmEngine.Data.Entities;
using RealmEngine.Data.Validation.Content;

namespace RealmEngine.Data.Tests.Validation;

/// <summary>
/// Tests for the validator findings that only warn or only fire on a narrow range check: a dialogue with a blank
/// line or no speaker, an archetype that awards negative experience, and an actor instance with a non-positive
/// level override.
/// </summary>
[Trait("Category", "Validation")]
public class ContentValidatorWarningTests
{
    private static readonly ContentReferenceCatalog Catalog = new()
    {
        Archetypes = ContentReferenceCatalog.Slugs(["giant-rat"]),
    };

    [Fact]
    public void A_dialogue_with_a_blank_line_only_warns()
    {
        var dialogue = new Dialogue
        {
            Slug = "halda-greeting",
            TypeKey = "greetings",
            DisplayName = "Halda's greeting",
            Speaker = "innkeeper-halda",
            Stats = new DialogueStats { Lines = ["Welcome, traveller.", "   "] },
        };

        var result = new DialogueValidator().Validate(dialogue, Catalog);

        result.IsValid.Should().BeTrue("a blank line is a content smell, not a broken reference");
        result.Warnings.Should().ContainSingle(issue =>
            issue.Rule == ContentRules.ValueOutOfRange && issue.Field == "stats.lines");
        result.Warnings.Single().Message.Should().Contain("halda-greeting").And.Contain("blank line");
    }

    [Fact]
    public void A_dialogue_without_a_speaker_only_warns()
    {
        var dialogue = new Dialogue
        {
            Slug = "halda-greeting",
            TypeKey = "greetings",
            DisplayName = "Halda's greeting",
            Speaker = "  ",
            Stats = new DialogueStats { Lines = ["Welcome, traveller."] },
        };

        var result = new DialogueValidator().Validate(dialogue, Catalog);

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle(issue =>
            issue.Rule == ContentRules.FieldInconsistent && issue.Field == "speaker");
    }

    [Fact]
    public void A_clean_dialogue_produces_no_findings()
    {
        var dialogue = new Dialogue
        {
            Slug = "halda-greeting",
            TypeKey = "greetings",
            DisplayName = "Halda's greeting",
            Speaker = "innkeeper-halda",
            Stats = new DialogueStats { Lines = ["Welcome, traveller."] },
        };

        var result = new DialogueValidator().Validate(dialogue, Catalog);

        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void An_archetype_that_awards_negative_experience_is_rejected()
    {
        var archetype = new ActorArchetype
        {
            Slug = "giant-rat",
            TypeKey = "vermin",
            DisplayName = "Giant Rat",
            MinLevel = 1,
            MaxLevel = 2,
            Stats = new ArchetypeStats { Health = 10, ExperienceReward = -5 },
        };

        var result = new ActorArchetypeValidator().Validate(archetype, Catalog);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(issue =>
            issue.Rule == ContentRules.ValueOutOfRange && issue.Field == "stats.experienceReward");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void An_actor_instance_with_a_non_Positive_level_override_is_rejected(int levelOverride)
    {
        var instance = new ActorInstance
        {
            Slug = "giant-rat-of-the-cellar",
            TypeKey = "vermin",
            DisplayName = "Giant Rat of the Cellar",
            ArchetypeId = Guid.NewGuid(),
            LevelOverride = levelOverride,
        };

        var result = new ActorInstanceValidator().Validate(instance, Catalog);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(issue =>
            issue.Rule == ContentRules.ValueOutOfRange && issue.Field == "levelOverride");
    }
}
