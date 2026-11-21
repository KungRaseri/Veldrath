namespace RealmEngine.Shared.Models;

/// <summary>Lightweight actor instance catalog projection used by the repository layer.</summary>
/// <param name="Slug">URL-safe identifier.</param>
/// <param name="DisplayName">Human-readable name.</param>
/// <param name="TypeKey">Origin category (e.g. "boss", "story", "unique").</param>
/// <param name="ArchetypeId">ID of the base actor archetype.</param>
/// <param name="LevelOverride">Level override, or <see langword="null"/> to use the archetype default.</param>
/// <param name="FactionOverride">Faction override, or <see langword="null"/> to use the archetype default.</param>
/// <param name="RarityWeight">Selection weight for random draws.</param>
/// <param name="LocationSlug">
/// The zone location this instance stands at, or <see langword="null"/> when it is catalogue-only. Trailing so every
/// existing construction site keeps compiling.
/// </param>
public record ActorInstanceEntry(
    string Slug,
    string DisplayName,
    string TypeKey,
    Guid ArchetypeId,
    int? LevelOverride,
    string? FactionOverride,
    int RarityWeight,
    string? LocationSlug = null);
