namespace RealmEngine.Shared.Models;

/// <summary>Lightweight zone location catalog projection used by the repository layer.</summary>
public record ZoneLocationEntry(
    string Slug,
    string DisplayName,
    string TypeKey,
    string ZoneId,
    string LocationType,
    int RarityWeight,
    int? MinLevel,
    int? MaxLevel);
