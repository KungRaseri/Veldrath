namespace RealmUnbound.Contracts.Players;

/// <summary>Public profile of a player account, summarising their highest-level active character.</summary>
/// <param name="AccountId">Unique identifier of the player account.</param>
/// <param name="Username">Display username of the account.</param>
/// <param name="Level">Level of the player's highest-level active character; <c>0</c> if the account has no characters.</param>
/// <param name="CharacterClass">Class name of the highest-level character (e.g. <c>"Warrior"</c>); <c>null</c> if no characters exist.</param>
/// <param name="Species">Species slug of the highest-level character (e.g. <c>"human"</c>); <c>null</c> if no characters exist.</param>
/// <param name="CurrentZone">Zone ID where the highest-level character is currently located; <c>null</c> if no characters exist.</param>
/// <param name="RegisteredAt">UTC timestamp when the account was created.</param>
public record PlayerProfileDto(
    Guid AccountId,
    string Username,
    int Level,
    string? CharacterClass,
    string? Species,
    string? CurrentZone,
    DateTimeOffset RegisteredAt);
