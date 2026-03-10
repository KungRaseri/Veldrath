namespace RealmUnbound.Server.Features.Characters;

/// <summary>Request body for creating a new character.</summary>
public record CreateCharacterRequest(string Name, string ClassName);

/// <summary>Read model returned by character endpoints.</summary>
public record CharacterDto(
    Guid Id,
    int SlotIndex,
    string Name,
    string ClassName,
    int Level,
    long Experience,
    DateTimeOffset LastPlayedAt,
    string CurrentZoneId);
