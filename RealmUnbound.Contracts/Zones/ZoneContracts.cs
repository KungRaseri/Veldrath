namespace RealmUnbound.Contracts.Zones;

public record ZoneDto(
    string Id,
    string Name,
    string Description,
    string Type,
    int MinLevel,
    int MaxPlayers,
    bool IsStarter,
    int OnlinePlayers);
