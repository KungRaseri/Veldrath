namespace RealmEngine.Shared.Models;

/// <summary>Lightweight world location catalog projection used by the repository layer.</summary>
public record WorldLocationEntry(
    string Slug,
    string DisplayName,
    string TypeKey,
    string LocationType,
    int RarityWeight,
    int? MinLevel,
    int? MaxLevel);
