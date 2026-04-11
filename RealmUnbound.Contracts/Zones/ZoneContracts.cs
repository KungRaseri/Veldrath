namespace Veldrath.Contracts.Zones;

/// <summary>Summary of a zone returned by zone list and detail endpoints.</summary>
public record ZoneDto(
    string Id,
    string Name,
    string Description,
    string Type,
    int MinLevel,
    int MaxPlayers,
    bool IsStarter,
    int OnlinePlayers,
    string? RegionId = null,
    bool HasInn = false,
    bool HasMerchant = false);

/// <summary>Summary of a geographic region.</summary>
public record RegionDto(
    string Id,
    string Name,
    string Description,
    string Type,
    int MinLevel,
    int MaxLevel,
    bool IsStarter,
    string WorldId);

/// <summary>Summary of a world container.</summary>
public record WorldDto(
    string Id,
    string Name,
    string Description,
    string Era);

