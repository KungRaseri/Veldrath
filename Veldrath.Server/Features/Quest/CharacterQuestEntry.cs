using System.Text.Json;

namespace Veldrath.Server.Features.Quest;

/// <summary>
/// One accepted quest as stored in the character's <c>QuestBlob</c>.
/// </summary>
/// <param name="Slug">Quest slug from the content catalog.</param>
/// <param name="Title">Display title.</param>
/// <param name="Description">Objective description shown in the journal.</param>
/// <param name="Status"><c>Active</c> or <c>Completed</c>.</param>
/// <param name="GoldReward">Gold granted on turn-in.</param>
/// <param name="XpReward">Experience granted on turn-in.</param>
/// <param name="Objectives">Objective key → required count.</param>
/// <param name="Progress">Objective key → current count.</param>
/// <param name="AcceptedAt">When the quest was accepted (UTC).</param>
public record CharacterQuestEntry(
    string Slug,
    string Title,
    string Description,
    string Status,
    int GoldReward,
    int XpReward,
    IReadOnlyDictionary<string, int> Objectives,
    IReadOnlyDictionary<string, int> Progress,
    DateTimeOffset AcceptedAt)
{
    /// <summary>Gets the status value used for accepted, unfinished quests.</summary>
    public const string StatusActive = "Active";

    /// <summary>Gets the status value used for turned-in quests.</summary>
    public const string StatusCompleted = "Completed";

    /// <summary>Gets a value indicating whether every objective has been met.</summary>
    public bool IsComplete => Objectives.All(
        objective => Progress.GetValueOrDefault(objective.Key) >= objective.Value);
}

/// <summary>
/// Reads and writes the character's quest journal blob.
/// </summary>
/// <remarks>
/// The blob is a plain JSON array of <see cref="CharacterQuestEntry"/> so quest state travels with the
/// character row and needs no extra tables.
/// </remarks>
/// <summary>
/// The objective kinds a quest may use. Every kind is written <c><kind>:<key></c> and matched by prefix, so a
/// new kind needs exactly three things: a constant here, one server call site, and content-audit coverage.
/// </summary>
public static class QuestObjectiveKinds
{
    /// <summary>Kill an actor archetype — <c>kill:giant-rat</c>. Advanced for every combat participant.</summary>
    public const string Kill = "kill";

    /// <summary>Collect an item — <c>collect:glowcap-mushroom</c>. Advanced when the item is acquired.</summary>
    public const string Collect = "collect";

    /// <summary>Visit a zone location — <c>visit:grey-cup</c>. Advanced on arrival.</summary>
    public const string Visit = "visit";

    /// <summary>Craft an item — <c>craft:iron-sword</c>. Advanced when a craft succeeds.</summary>
    public const string Craft = "craft";

    /// <summary>Reach a character level — <c>reach_level:5</c>. Advanced on level-up.</summary>
    public const string ReachLevel = "reach_level";

    /// <summary>Speak to an NPC — <c>talk:innkeeper-halda</c>. Advanced by the hub's <c>TalkToNpc</c> call.</summary>
    public const string Talk = "talk";

    /// <summary>
    /// Hand an item to an NPC — <c>deliver:iron-sword</c>. Reserved: there is no hand-over surface yet, so the content
    /// audit reports this kind rather than letting authored content silently never progress.
    /// </summary>
    public const string Deliver = "deliver";
}

public static class QuestJournal
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Deserialises the journal, treating anything unreadable as empty.</summary>
    /// <param name="blob">Raw <c>QuestBlob</c> value.</param>
    /// <returns>The journal entries.</returns>
    public static List<CharacterQuestEntry> Parse(string? blob)
    {
        if (string.IsNullOrWhiteSpace(blob) || blob == "[]")
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<CharacterQuestEntry>>(blob, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Serialises a journal back into a blob value.</summary>
    /// <param name="entries">Entries to persist.</param>
    /// <returns>The serialised blob.</returns>
    public static string Serialize(IEnumerable<CharacterQuestEntry> entries) =>
        JsonSerializer.Serialize(entries);

    /// <summary>
    /// Advances every active objective whose key matches <paramref name="kind"/> + <paramref name="key"/>.
    /// </summary>
    /// <param name="entries">Journal entries, updated in place.</param>
    /// <param name="kind">One of <see cref="QuestObjectiveKinds"/>.</param>
    /// <param name="key">The identifier the objective names (enemy, item, location, NPC or level).</param>
    /// <param name="amount">Progress to add.</param>
    /// <returns>The number of objectives that changed.</returns>
    public static int Advance(List<CharacterQuestEntry> entries, string kind, string key, int amount = 1)
    {
        var changed = 0;
        var prefix = $"{kind}:";

        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            if (entry.Status != CharacterQuestEntry.StatusActive)
                continue;

            var progress = new Dictionary<string, int>(entry.Progress, StringComparer.OrdinalIgnoreCase);
            var touched = false;

            foreach (var objective in entry.Objectives)
            {
                if (!MatchesObjective(objective.Key, prefix, key))
                    continue;

                progress[objective.Key] = Math.Min(objective.Value, progress.GetValueOrDefault(objective.Key) + amount);
                touched = true;
            }

            if (!touched)
                continue;

            entries[index] = entry with { Progress = progress };
            changed++;
        }

        return changed;
    }

    private static bool MatchesObjective(string objectiveKey, string prefix, string key)
    {
        if (!objectiveKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var target = objectiveKey[prefix.Length..];
        return string.Equals(target, key, StringComparison.OrdinalIgnoreCase);
    }
}
