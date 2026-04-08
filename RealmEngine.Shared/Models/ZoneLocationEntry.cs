namespace RealmEngine.Shared.Models;

/// <summary>Lightweight zone location catalog projection used by the repository layer.</summary>
public record ZoneLocationEntry(
    string Slug,
    string DisplayName,
    string TypeKey,
    string ZoneId,
    int RarityWeight,
    int? MinLevel,
    int? MaxLevel,
    bool IsHidden = false,
    string? UnlockType = null,
    string? UnlockKey = null,
    int? DiscoverThreshold = null,
    IReadOnlyList<ActorPoolEntry>? ActorPool = null);

/// <summary>A weighted entry in a zone location actor pool.</summary>
/// <param name="ArchetypeSlug">Slug of the archetype that can spawn at this location.</param>
/// <param name="Weight">Relative spawn weight (higher = more likely).</param>
public record ActorPoolEntry(string ArchetypeSlug, int Weight);
