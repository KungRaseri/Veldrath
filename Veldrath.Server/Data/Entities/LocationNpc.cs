namespace Veldrath.Server.Data.Entities;

/// <summary>
/// A person standing at a zone location — the placement that turns an authored actor archetype into someone a
/// player can actually meet.
/// </summary>
/// <remarks>
/// <para>
/// The content side already exists: <c>ActorArchetype</c> rows carry the role traits (shopkeeper, quest giver,
/// has dialogue, immortal, not hostile) and <c>Dialogue</c> rows carry lines keyed by the speaker's slug. What was
/// missing was any statement that <em>this</em> archetype stands <em>here</em>, which is why the main-story quest
/// could ask a player to return cargo to the Village Blacksmith and no blacksmith existed anywhere in the world.
/// </para>
/// <para>
/// Placement is deliberately not expressed as an actor pool entry. A pool means "what may spawn here, weighted",
/// and a shopkeeper who is present a quarter of the time is not a shopkeeper — worse, every pool entry becomes an
/// attackable enemy in the roster.
/// </para>
/// </remarks>
public class LocationNpc
{
    /// <summary>Gets or sets the primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the stable placement key, so a restart finds the same row instead of adding a twin.</summary>
    public string NpcId { get; set; } = string.Empty;

    /// <summary>Gets or sets the zone the person stands in.</summary>
    public string ZoneId { get; set; } = string.Empty;

    /// <summary>Gets or sets the zone-location slug the person stands at.</summary>
    public string LocationSlug { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the actor-archetype slug this placement presents. It doubles as the speaker key the dialogue
    /// catalog is queried with, and as the key <c>TalkToNpc</c> advances <c>talk:</c> objectives by.
    /// </summary>
    public string ArchetypeSlug { get; set; } = string.Empty;

    /// <summary>Gets or sets an optional name override, for when the archetype is generic and the person is not.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Gets or sets the order the person is listed in, alongside any others at the same location.</summary>
    public int SortOrder { get; set; }
}
