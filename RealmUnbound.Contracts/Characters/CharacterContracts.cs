namespace RealmUnbound.Contracts.Characters;

public record CreateCharacterRequest(string Name, string ClassName);

public record CharacterDto(
    Guid Id,
    int SlotIndex,
    string Name,
    string ClassName,
    int Level,
    long Experience,
    DateTimeOffset LastPlayedAt,
    string CurrentZoneId);
