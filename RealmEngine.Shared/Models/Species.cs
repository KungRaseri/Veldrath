namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents a playable species (e.g. Human, Elf) selectable during character creation.
/// </summary>
public class Species
{
    /// <summary>Unique URL-safe identifier for this species.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Display name shown to players.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Lore description of the species shown during character creation.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Species family classification (e.g. "humanoid", "beast", "undead").</summary>
    public string TypeKey { get; set; } = string.Empty;

    /// <summary>Rarity weight for procedural generation — lower values are more common.</summary>
    public int RarityWeight { get; set; } = 50;

    /// <summary>Strength bonus applied additively to the character's point-buy allocation at creation.</summary>
    public int BonusStrength { get; set; } = 0;

    /// <summary>Dexterity bonus applied additively to the character's point-buy allocation at creation.</summary>
    public int BonusDexterity { get; set; } = 0;

    /// <summary>Constitution bonus applied additively to the character's point-buy allocation at creation.</summary>
    public int BonusConstitution { get; set; } = 0;

    /// <summary>Intelligence bonus applied additively to the character's point-buy allocation at creation.</summary>
    public int BonusIntelligence { get; set; } = 0;

    /// <summary>Wisdom bonus applied additively to the character's point-buy allocation at creation.</summary>
    public int BonusWisdom { get; set; } = 0;

    /// <summary>Charisma bonus applied additively to the character's point-buy allocation at creation.</summary>
    public int BonusCharisma { get; set; } = 0;

    /// <summary>
    /// Applies this species' stat bonuses to <paramref name="character"/> additively.
    /// Only non-zero bonuses modify the character.
    /// </summary>
    /// <param name="character">The character to apply bonuses to.</param>
    public void ApplyBonuses(Character character)
    {
        character.Strength     += BonusStrength;
        character.Dexterity    += BonusDexterity;
        character.Constitution += BonusConstitution;
        character.Intelligence += BonusIntelligence;
        character.Wisdom       += BonusWisdom;
        character.Charisma     += BonusCharisma;
    }
}
