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
    int? MaxLevel,
    bool IsHidden = false,
    string? UnlockType = null,
    string? UnlockKey = null,
    int? DiscoverThreshold = null);

/// <summary>A traversal edge linking one ZoneLocation to another location or zone.</summary>
public record ZoneLocationConnectionEntry(
    int ConnectionId,
    string FromLocationSlug,
    string? ToLocationSlug,
    string? ToZoneId,
    string ConnectionType,
    bool IsTraversable,
    bool IsHidden = false);
