using RealmEngine.Data.Validation.Content;
using Veldrath.Server.Features.Content;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Tests for the audit rules that cover the A5 objective kinds: `craft:` against items, `reach_level:` as a number,
/// and `deliver:` reported as reserved instead of silently accepted.
/// </summary>
public class QuestObjectiveAuditTests
{
    private static readonly HashSet<string> Archetypes = new(StringComparer.OrdinalIgnoreCase) { "giant-rat" };
    private static readonly HashSet<string> Items = new(StringComparer.OrdinalIgnoreCase) { "iron-sword" };
    private static readonly HashSet<string> Locations = new(StringComparer.OrdinalIgnoreCase) { "grey-cup" };

    private static IReadOnlyList<ContentIssue> Audit(params QuestObjectiveReference[] objectives) =>
        ContentReferenceRules.AuditQuestObjectives(objectives, Archetypes, Items, Locations);

    [Fact]
    public void Craft_Objective_Against_A_Known_Item_Is_Clean()
    {
        Audit(new QuestObjectiveReference("smithing", "craft", "iron-sword")).Should().BeEmpty();
    }

    [Fact]
    public void Craft_Objective_Against_An_Unknown_Item_Is_Reported()
    {
        var issue = Audit(new QuestObjectiveReference("smithing", "craft", "mythril-sword"))
            .Should().ContainSingle().Subject;

        issue.Rule.Should().Be(ContentReferenceRules.MissingQuestTarget);
        issue.Message.Should().Contain("unknown items");
    }

    [Fact]
    public void Reach_Level_Objective_Needs_A_Positive_Number()
    {
        Audit(new QuestObjectiveReference("training", "reach_level", "5")).Should().BeEmpty();

        var issue = Audit(new QuestObjectiveReference("training", "reach_level", "five"))
            .Should().ContainSingle().Subject;

        issue.Message.Should().Contain("positive level number");
    }

    [Fact]
    public void Deliver_Objective_Is_Reported_As_Reserved()
    {
        var issue = Audit(new QuestObjectiveReference("errand", "deliver", "iron-sword"))
            .Should().ContainSingle().Subject;

        issue.Rule.Should().Be(ContentReferenceRules.UnsupportedQuestKind);
        issue.Message.Should().Contain("reserved objective kind");
    }
}
