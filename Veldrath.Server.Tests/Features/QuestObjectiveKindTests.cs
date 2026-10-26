using RealmEngine.Shared.Models;
using Veldrath.Server.Features.Quest;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Tests that every <see cref="QuestObjectiveKinds"/> value advances only the objective that names it. The matcher is
/// prefix-generic, so these lock down the contract each call site relies on.
/// </summary>
public class QuestObjectiveKindTests
{
    private static List<CharacterQuestEntry> JournalWith(params string[] objectiveKeys) =>
    [
        new CharacterQuestEntry(
            "q",
            "A quest",
            string.Empty,
            CharacterQuestEntry.StatusActive,
            0,
            0,
            objectiveKeys.ToDictionary(key => key, _ => 1, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            DateTimeOffset.UtcNow),
    ];

    [Theory]
    [InlineData(QuestObjectiveKinds.Craft, "craft:iron-sword", "iron-sword", 1)]
    [InlineData(QuestObjectiveKinds.Craft, "craft:iron-sword", "steel-sword", 0)]
    [InlineData(QuestObjectiveKinds.ReachLevel, "reach_level:5", "5", 1)]
    [InlineData(QuestObjectiveKinds.ReachLevel, "reach_level:5", "6", 0)]
    [InlineData(QuestObjectiveKinds.Talk, "talk:innkeeper-halda", "innkeeper-halda", 1)]
    [InlineData(QuestObjectiveKinds.Talk, "talk:innkeeper-halda", "innkeeper-hald", 0)]
    [InlineData(QuestObjectiveKinds.Deliver, "deliver:iron-sword", "iron-sword", 1)]
    // The name form never matches a slug objective — which is exactly why the kill path advances both.
    [InlineData(QuestObjectiveKinds.Kill, "kill:giant-rat", "Giant Rat", 0)]
    public void Advance_Touches_Only_The_Matching_Objective(
        string kind,
        string objectiveKey,
        string key,
        int expectedChanged)
    {
        var journal = JournalWith(objectiveKey);

        var changed = QuestJournal.Advance(journal, kind, key);

        changed.Should().Be(expectedChanged);
        journal[0].Progress.GetValueOrDefault(objectiveKey).Should().Be(expectedChanged);
    }

    [Fact]
    public void Advance_Ignores_Completed_Quests()
    {
        var completed = JournalWith("craft:iron-sword")[0] with
        {
            Status = CharacterQuestEntry.StatusCompleted,
        };
        List<CharacterQuestEntry> journal = [completed];

        QuestJournal.Advance(journal, QuestObjectiveKinds.Craft, "iron-sword").Should().Be(0);
    }
}
