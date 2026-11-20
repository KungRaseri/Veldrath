using FluentAssertions;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Validation.Content;

namespace RealmEngine.Data.Tests.Validation;

/// <summary>
/// Tests for the dialogue option rules: an option that cannot be read, offered or resolved must fail at authoring
/// time rather than showing up as a missing button in play.
/// </summary>
[Trait("Category", "Validation")]
public class DialogueOptionValidationTests
{
    private static readonly ContentReferenceCatalog Catalog = new()
    {
        Items = ContentReferenceCatalog.Slugs(["healing-draught", "glowcap-mushroom"]),
        Materials = ContentReferenceCatalog.Slugs(["iron-ingot"]),
        Dialogues = ContentReferenceCatalog.Slugs(["elder-greeting", "elder-follow-up"]),
        Quests = ContentReferenceCatalog.Slugs(["the-lost-shipment"]),
    };

    private static ContentValidationResult Validate(Dialogue dialogue) =>
        ContentValidatorRegistry.CreateDefault().Validate("dialogue", dialogue, Catalog);

    private static Dialogue DialogueWith(params DialogueOption[] options) => new()
    {
        Slug = "elder-greeting",
        TypeKey = "greetings",
        DisplayName = "The Elder Greets You",
        Speaker = "wayfarer-elder",
        Stats = new DialogueStats
        {
            Lines = ["Well met, traveller."],
            Options = [.. options],
        },
    };

    private static DialogueOption Option(
        string key = "ask-about-woods",
        string label = "What happened in the wood?",
        string response = "Something older than the road walks there now.",
        string? requires = null,
        string? grants = null,
        string? next = null,
        bool? oneShot = null) => new()
        {
            Key = key,
            Label = label,
            Response = response,
            Requires = requires,
            Grants = grants,
            NextSlug = next,
            OneShot = oneShot,
        };

    [Fact]
    public void An_entry_with_no_options_stays_valid()
    {
        // Every dialogue authored before options existed is in this shape: spoken, not answered.
        var result = Validate(DialogueWith());

        result.IsValid.Should().BeTrue();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void A_well_formed_option_passes()
    {
        var result = Validate(DialogueWith(Option(
            requires: "level:3\nitem:glowcap-mushroom:2\nquest:active:the-lost-shipment\n" +
                      "objective:talk:village-blacksmith:1",
            grants: "item:healing-draught:1\ngold:25\nquest-start:the-lost-shipment\n" +
                    "objective:talk:wayfarer-elder:1",
            next: "elder-follow-up")));

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors.Select(issue => issue.Message)));
    }

    [Fact]
    public void A_material_slug_resolves_as_an_item_reference()
    {
        var result = Validate(DialogueWith(Option(grants: "item:iron-ingot:2")));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void An_option_with_no_key_label_or_response_reports_three_errors()
    {
        var result = Validate(DialogueWith(Option(key: " ", label: "  ", response: "")));

        result.IsValid.Should().BeFalse();
        result.Errors.Where(issue => issue.Rule == ContentRules.DialogueOptionInvalid)
            .Should().HaveCount(3);
    }

    [Fact]
    public void A_duplicate_option_key_is_rejected()
    {
        var result = Validate(DialogueWith(Option(key: "ask"), Option(key: "ask")));

        result.Errors.Select(issue => issue.Rule).Should().Contain(ContentRules.DialogueOptionInvalid);
        result.Errors.Should().Contain(issue => issue.Message.Contains("repeats a key"));
    }

    [Fact]
    public void More_options_than_the_cap_are_rejected()
    {
        var options = Enumerable.Range(1, 9)
            .Select(index => Option(key: $"choice-{index}"))
            .ToArray();

        var result = Validate(DialogueWith(options));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(issue => issue.Message.Contains("at most"));
    }

    [Fact]
    public void An_over_long_label_is_rejected()
    {
        var result = Validate(DialogueWith(Option(label: new string('a', 81))));

        result.Errors.Should().Contain(issue => issue.Rule == ContentRules.DialogueOptionInvalid &&
                                                issue.Message.Contains("character label"));
    }

    [Fact]
    public void An_option_cannot_continue_into_its_own_entry()
    {
        var result = Validate(DialogueWith(Option(next: "elder-greeting")));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(issue => issue.Message.Contains("continues into itself"));
    }

    [Fact]
    public void An_option_that_continues_into_a_missing_entry_is_a_dangling_reference()
    {
        var result = Validate(DialogueWith(Option(next: "elder-third")));

        result.Errors.Select(issue => issue.Rule).Should().Contain(ContentRules.MissingDialogueReference);
        result.Errors.Should().Contain(issue => issue.Message.Contains("elder-third"));
    }

    [Theory]
    [InlineData("mana:5", "requires")]
    [InlineData("item:glowcap-mushroom", "requires")]
    [InlineData("item:Glowcap-Mushroom:1", "requires")]
    [InlineData("level:0", "requires")]
    [InlineData("quest:done:the-lost-shipment", "requires")]
    [InlineData("objective:talk:village-blacksmith", "requires")]
    [InlineData("gold:25", "requires")]
    [InlineData("level:3", "grants")]
    [InlineData("quest:active:the-lost-shipment", "grants")]
    [InlineData("gold:0", "grants")]
    [InlineData("quest-start", "grants")]
    public void A_malformed_or_misplaced_effect_is_rejected(string entry, string side)
    {
        var option = side == "requires" ? Option(requires: entry) : Option(grants: entry);

        var result = Validate(DialogueWith(option));

        result.IsValid.Should().BeFalse();
        result.Errors.Select(issue => issue.Rule).Should().Contain(ContentRules.DialogueEffectInvalid);
    }

    [Fact]
    public void An_unknown_item_slug_is_a_dangling_reference()
    {
        var result = Validate(DialogueWith(Option(grants: "item:wolf-pelt:1")));

        result.Errors.Select(issue => issue.Rule).Should().Contain(ContentRules.MissingDialogueReference);
        result.Errors.Should().Contain(issue => issue.Message.Contains("no item 'wolf-pelt' exists"));
    }

    [Fact]
    public void An_unknown_quest_slug_is_a_dangling_reference()
    {
        var result = Validate(DialogueWith(Option(requires: "quest:complete:a-quest-nobody-wrote")));

        result.Errors.Select(issue => issue.Rule).Should().Contain(ContentRules.MissingDialogueReference);
        result.Errors.Should().Contain(issue => issue.Message.Contains("no quest 'a-quest-nobody-wrote' exists"));
    }

    [Fact]
    public void A_grant_bearing_option_marked_repeatable_warns_but_stays_valid()
    {
        var result = Validate(DialogueWith(Option(grants: "gold:25", oneShot: false)));

        result.IsValid.Should().BeTrue();
        result.Warnings.Select(issue => issue.Rule).Should().Contain(ContentRules.DialogueRepeatableGrant);
    }

    [Fact]
    public void An_option_that_grants_nothing_may_be_repeated_without_a_warning()
    {
        var result = Validate(DialogueWith(Option(oneShot: false)));

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Both_sides_report_every_bad_entry_rather_than_stopping_at_the_first()
    {
        var result = Validate(DialogueWith(Option(
            requires: "mana:5\nlevel:0",
            grants: "gold:0\nitem:not-a-thing:1")));

        result.Errors.Should().HaveCount(4);
    }
}
