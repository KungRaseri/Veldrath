namespace RealmEngine.Shared.Models;

/// <summary>
/// The objective kinds a quest may use. Every kind is written <c><kind>:<key></c> and matched by prefix, so a
/// new kind needs exactly three things: a constant here, one gameplay call site, and content-validation coverage.
/// </summary>
/// <remarks>
/// These constants live in the shared library rather than the server because two very different callers need
/// them: gameplay advances objectives (<c>QuestJournal</c> in <c>Veldrath.Server</c>) and content validation rejects
/// kinds that could never progress (<see cref="RealmEngine.Data.Validation.Content.QuestValidator"/> is the reason
/// this type had to move).
/// </remarks>
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

    /// <summary>Every kind that is implemented and can therefore progress.</summary>
    public static IReadOnlyList<string> Implemented { get; } =
        [Kill, Collect, Visit, Craft, ReachLevel, Talk];
}
