namespace Veldrath.GameClient.Components.Payloads;

/// <summary>
/// Hub event payload received when a character respawns after death.
/// Contains the character's updated health and mana.
/// </summary>
/// <param name="CharacterId">The respawned character's identifier.</param>
/// <param name="CurrentHealth">The character's health after respawn.</param>
/// <param name="CurrentMana">The character's mana after respawn.</param>
public sealed record CharacterRespawnedPayload(Guid CharacterId, int CurrentHealth, int CurrentMana);
