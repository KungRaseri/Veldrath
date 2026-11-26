using System.Globalization;

namespace RealmEngine.Data.Content;

/// <summary>
/// What a dialogue option asks of the character before it can be taken, or hands them once it is.
/// </summary>
/// <remarks>
/// One enum covers both sides because the runtime questions are identical: does this character meet it, and what
/// changes if they do. The validator rejects a kind on the wrong side — <see cref="Gold"/> and
/// <see cref="QuestStart"/> are grants, <see cref="Level"/> and the two quest states are requirements.
/// </remarks>
public enum DialogueEffectKind
{
    /// <summary>A minimum character level. Requirement only.</summary>
    Level,

    /// <summary>An item the character must hold, or receives. <see cref="DialogueEffect.Key"/> is the item slug.</summary>
    Item,

    /// <summary>A quest that must be in progress. Requirement only.</summary>
    QuestActive,

    /// <summary>A quest that must already be complete. Requirement only.</summary>
    QuestComplete,

    /// <summary>A quest objective to advance or check. <see cref="DialogueEffect.Key"/> is the objective kind.</summary>
    Objective,

    /// <summary>Gold the character receives. Grant only.</summary>
    Gold,

    /// <summary>A quest the character is enrolled in. Grant only.</summary>
    QuestStart,
}

/// <summary>
/// One parsed requirement or grant from a dialogue option.
/// </summary>
/// <param name="Kind">Which kind of effect this is.</param>
/// <param name="Key">
/// The primary subject: an item slug, a quest slug, or the objective kind for <see cref="DialogueEffectKind.Objective"/>.
/// Empty for <see cref="DialogueEffectKind.Level"/> and <see cref="DialogueEffectKind.Gold"/>.
/// </param>
/// <param name="Detail">The objective key for <see cref="DialogueEffectKind.Objective"/>; otherwise null.</param>
/// <param name="Amount">
/// The quantity, count or level. Zero when the kind carries no number.
/// </param>
public sealed record DialogueEffect(DialogueEffectKind Kind, string Key, string? Detail, int Amount)
{
    /// <summary>
    /// Renders the effect as player-facing words, e.g. <c>2 × glowcap-mushroom</c> or <c>level 3</c>.
    /// </summary>
    /// <remarks>
    /// The client shows the same wording it will be held to, so the phrasing lives here rather than in a component:
    /// a requirement and a grant that read differently on screen than they behave is the failure this prevents.
    /// </remarks>
    /// <returns>A short human-readable description.</returns>
    public string Describe() => Kind switch
    {
        DialogueEffectKind.Level => $"level {Amount}",
        DialogueEffectKind.Item => $"{Amount} × {Key}",
        DialogueEffectKind.QuestActive => $"quest {Key} in progress",
        DialogueEffectKind.QuestComplete => $"quest {Key} complete",
        // "talk village-blacksmith" reads as the deed itself; the count only appears when it is more than one.
        DialogueEffectKind.Objective => Amount > 1 ? $"{Key} {Detail} ×{Amount}" : $"{Key} {Detail}",
        DialogueEffectKind.Gold => $"{Amount} gold",
        DialogueEffectKind.QuestStart => $"quest {Key}",
        _ => Kind.ToString(),
    };
}

/// <summary>
/// Parses the requirement and grant lines authored on a dialogue option.
/// </summary>
/// <remarks>
/// <para>
/// The authored form is one entry per line, <c>kind:subject[:detail][:amount]</c>:
/// </para>
/// <code>
/// required:  level:3
///            item:glowcap-mushroom:2
///            quest:active:the-lost-shipment
///            objective:talk:village-blacksmith:1
/// grants:    item:healing-draught:1
///            gold:25
///            quest-start:clear-the-forest
///            objective:talk:wayfarer-elder:1
/// </code>
/// <para>
/// A single string per list, rather than a nested object graph, is deliberate: the content team authors these in the
/// Foundry form, and a nested list of lists is neither editable there nor supported by the owned-JSON mapping this
/// entity uses. Parsing is shared by the validator and by the runtime, so "valid" and "understood" cannot diverge.
/// </para>
/// <para>
/// The grammar is structural only. Whether an objective kind is implemented belongs to the quest rules
/// (<c>unsupported-quest-kind</c>), and whether a named item, quest or dialogue exists is a reference rule.
/// </para>
/// </remarks>
public static class DialogueOptionGrammar
{
    /// <summary>The maximum number of options a single dialogue entry may carry.</summary>
    /// <remarks>A conversation is a scene, not a menu; a cap keeps the panel readable and the payload small.</remarks>
    public const int MaxOptionsPerEntry = 8;

    /// <summary>The longest option label the panel renders without truncation.</summary>
    public const int MaxLabelLength = 80;

    /// <summary>Splits an authored block into trimmed, non-empty entries.</summary>
    /// <remarks>Newlines and semicolons both separate entries, because a form author types whichever comes naturally.</remarks>
    /// <param name="block">The authored text, which may be null or empty.</param>
    /// <returns>One entry per line.</returns>
    public static IReadOnlyList<string> Split(string? block)
    {
        if (string.IsNullOrWhiteSpace(block))
        {
            return [];
        }

        return block
            .Split(['\n', '\r', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(entry => entry.Length > 0)
            .ToList();
    }

    /// <summary>Parses one requirement entry.</summary>
    /// <param name="entry">A single authored entry, e.g. <c>level:3</c>.</param>
    /// <param name="effect">The parsed effect when this returns true.</param>
    /// <param name="error">Why parsing failed, when it did.</param>
    /// <returns>True when the entry is a well-formed requirement.</returns>
    public static bool TryParseRequirement(string? entry, out DialogueEffect effect, out string error) =>
        TryParse(entry, IsRequirementKind, out effect, out error);

    /// <summary>Parses one grant entry.</summary>
    /// <param name="entry">A single authored entry, e.g. <c>gold:25</c>.</param>
    /// <param name="effect">The parsed effect when this returns true.</param>
    /// <param name="error">Why parsing failed, when it did.</param>
    /// <returns>True when the entry is a well-formed grant.</returns>
    public static bool TryParseGrant(string? entry, out DialogueEffect effect, out string error) =>
        TryParse(entry, IsGrantKind, out effect, out error);

    /// <summary>Names the kinds that may appear in a requirement list.</summary>
    /// <returns>The requirement kinds, in the order they read best in a message.</returns>
    public static IReadOnlyList<DialogueEffectKind> RequirementKinds { get; } = GetKinds(IsRequirementKind);

    /// <summary>Names the kinds that may appear in a grant list.</summary>
    /// <returns>The grant kinds, in the order they read best in a message.</returns>
    public static IReadOnlyList<DialogueEffectKind> GrantKinds { get; } = GetKinds(IsGrantKind);

    private static bool IsRequirementKind(DialogueEffectKind kind) =>
        kind is DialogueEffectKind.Level
            or DialogueEffectKind.Item
            or DialogueEffectKind.QuestActive
            or DialogueEffectKind.QuestComplete
            or DialogueEffectKind.Objective;

    private static bool IsGrantKind(DialogueEffectKind kind) =>
        kind is DialogueEffectKind.Item
            or DialogueEffectKind.Gold
            or DialogueEffectKind.QuestStart
            or DialogueEffectKind.Objective;

    private static IReadOnlyList<DialogueEffectKind> GetKinds(Func<DialogueEffectKind, bool> predicate) =>
        Enum.GetValues<DialogueEffectKind>().Where(predicate).ToList();

    private static bool TryParse(
        string? entry,
        Func<DialogueEffectKind, bool> allowed,
        out DialogueEffect effect,
        out string error)
    {
        effect = default!;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(entry))
        {
            error = "the entry is empty";
            return false;
        }

        var parts = entry.Split(':', StringSplitOptions.TrimEntries);
        var head = parts[0].ToLowerInvariant();

        switch (head)
        {
            case "level":
                if (!allowed(DialogueEffectKind.Level))
                {
                    error = "level is a requirement, not a grant";
                    return false;
                }

                if (parts.Length != 2 || !TryPositive(parts[1], out var level))
                {
                    error = "level needs a whole number: level:3";
                    return false;
                }

                effect = new DialogueEffect(DialogueEffectKind.Level, string.Empty, null, level);
                return true;

            case "item":
                if (!allowed(DialogueEffectKind.Item))
                {
                    error = "item is not allowed here";
                    return false;
                }

                if (parts.Length != 3)
                {
                    error = "item needs a slug and a quantity: item:glowcap-mushroom:2";
                    return false;
                }

                if (!IsSlug(parts[1]))
                {
                    error = $"'{parts[1]}' is not a lowercase-hyphenated slug";
                    return false;
                }

                if (!TryPositive(parts[2], out var quantity))
                {
                    error = "the item quantity must be a whole number of at least 1";
                    return false;
                }

                effect = new DialogueEffect(DialogueEffectKind.Item, parts[1], null, quantity);
                return true;

            case "quest":
                if (parts.Length != 3)
                {
                    error = "quest needs a state and a slug: quest:active:the-lost-shipment";
                    return false;
                }

                var state = parts[1].ToLowerInvariant();
                var questKind = state switch
                {
                    "active" => DialogueEffectKind.QuestActive,
                    "complete" => DialogueEffectKind.QuestComplete,
                    _ => (DialogueEffectKind?)null,
                };

                if (questKind is null || !allowed(questKind.Value))
                {
                    error = "the quest state must be 'active' or 'complete', and only as a requirement";
                    return false;
                }

                if (!IsSlug(parts[2]))
                {
                    error = $"'{parts[2]}' is not a lowercase-hyphenated slug";
                    return false;
                }

                effect = new DialogueEffect(questKind.Value, parts[2], null, 0);
                return true;

            case "objective":
                if (!allowed(DialogueEffectKind.Objective))
                {
                    error = "objective is not allowed here";
                    return false;
                }

                if (parts.Length != 4)
                {
                    error = "objective needs a kind, a key and a count: objective:talk:village-blacksmith:1";
                    return false;
                }

                if (!IsToken(parts[1]) || !IsToken(parts[2]))
                {
                    error = "the objective kind and key must be simple lowercase tokens";
                    return false;
                }

                if (!TryPositive(parts[3], out var count))
                {
                    error = "the objective count must be a whole number of at least 1";
                    return false;
                }

                effect = new DialogueEffect(DialogueEffectKind.Objective, parts[1].ToLowerInvariant(), parts[2], count);
                return true;

            case "gold":
                if (!allowed(DialogueEffectKind.Gold))
                {
                    error = "gold is a grant, not a requirement";
                    return false;
                }

                if (parts.Length != 2 || !TryPositive(parts[1], out var gold))
                {
                    error = "gold needs a whole number: gold:25";
                    return false;
                }

                effect = new DialogueEffect(DialogueEffectKind.Gold, string.Empty, null, gold);
                return true;

            case "quest-start":
                if (!allowed(DialogueEffectKind.QuestStart))
                {
                    error = "quest-start is a grant, not a requirement";
                    return false;
                }

                if (parts.Length != 2 || !IsSlug(parts[1]))
                {
                    error = "quest-start needs a quest slug: quest-start:the-lost-shipment";
                    return false;
                }

                effect = new DialogueEffect(DialogueEffectKind.QuestStart, parts[1], null, 0);
                return true;

            default:
                error = $"'{head}' is not a known kind; requirements are level, item, quest and objective, " +
                        "grants are item, gold, quest-start and objective";
                return false;
        }
    }

    private static bool TryPositive(string text, out int value) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= 1;

    private static bool IsSlug(string text) =>
        text.Length is > 0 and <= 64 && text.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-');

    private static bool IsToken(string text) =>
        text.Length is > 0 and <= 64 && text.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '-' or '_');
}
