namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents a playable species (e.g. Human, Elf) selectable during character creation.
/// </summary>
public class Species
{
    /// <summary>Unique URL-safe identifier for this species.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Display name shown to players.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Species family classification (e.g. "humanoid", "beast", "undead").</summary>
    public string TypeKey { get; set; } = string.Empty;

    /// <summary>Rarity weight for procedural generation — lower values are more common.</summary>
    public int RarityWeight { get; set; } = 50;
}
