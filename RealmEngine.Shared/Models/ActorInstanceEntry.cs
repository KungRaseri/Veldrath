namespace RealmEngine.Shared.Models;

/// <summary>Lightweight actor instance catalog projection used by the repository layer.</summary>
public record ActorInstanceEntry(
    string Slug,
    string DisplayName,
    string TypeKey,
    Guid ArchetypeId,
    int? LevelOverride,
    string? FactionOverride,
    int RarityWeight);
