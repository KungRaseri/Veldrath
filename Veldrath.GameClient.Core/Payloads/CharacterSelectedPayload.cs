namespace Veldrath.GameClient.Core.Payloads;

/// <summary>
/// Hub event payload received when a character is successfully selected on the server.
/// Contains all stats needed to initialise the game HUD.
/// </summary>
/// <param name="Id">The character's unique identifier.</param>
/// <param name="Name">The character's display name.</param>
/// <param name="ClassName">The character's class (e.g. "Warrior", "Mage").</param>
/// <param name="Level">The character's current level.</param>
/// <param name="Experience">Total experience points earned.</param>
/// <param name="CurrentZoneId">The zone the character is currently in, or <c>null</c> if not yet placed.</param>
/// <param name="RegionId">The region the character belongs to.</param>
/// <param name="CurrentHealth">Current health points.</param>
/// <param name="MaxHealth">Maximum health points.</param>
/// <param name="CurrentMana">Current mana points.</param>
/// <param name="MaxMana">Maximum mana points.</param>
/// <param name="Gold">Gold coins in the character's possession.</param>
/// <param name="UnspentAttributePoints">Unspent attribute points.</param>
/// <param name="Strength">Strength attribute value.</param>
/// <param name="Dexterity">Dexterity attribute value.</param>
/// <param name="Constitution">Constitution attribute value.</param>
/// <param name="Intelligence">Intelligence attribute value.</param>
/// <param name="Wisdom">Wisdom attribute value.</param>
/// <param name="Charisma">Charisma attribute value.</param>
/// <param name="LearnedAbilities">List of ability slugs the character has learned.</param>
/// <param name="SelectedAt">When the selection was confirmed on the server.</param>
public sealed record CharacterSelectedPayload(
    Guid Id,
    string Name,
    string ClassName,
    int Level,
    long Experience,
    string? CurrentZoneId,
    string RegionId,
    int CurrentHealth,
    int MaxHealth,
    int CurrentMana,
    int MaxMana,
    int Gold,
    int UnspentAttributePoints,
    int Strength,
    int Dexterity,
    int Constitution,
    int Intelligence,
    int Wisdom,
    int Charisma,
    List<string> LearnedAbilities,
    DateTimeOffset SelectedAt);
