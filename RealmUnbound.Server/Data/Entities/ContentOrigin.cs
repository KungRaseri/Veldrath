namespace RealmUnbound.Server.Data.Entities;

/// <summary>
/// Tracks the origin of game content. Used by the Foundry workflow to distinguish
/// first-party content from community contributions.
/// </summary>
public enum ContentOrigin
{
    /// <summary>First-party content shipped with the game.</summary>
    Official,

    /// <summary>Submitted by a community member; pending or rejected.</summary>
    Community,

    /// <summary>Community submission that has been approved and promoted by a Curator.</summary>
    Curated,
}
