using Newtonsoft.Json;

namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents a character background providing origin story and attribute bonuses
/// </summary>
public class Background
{
    /// <summary>
    /// Unique identifier slug for the background
    /// </summary>
    [JsonProperty("slug")]
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the background
    /// </summary>
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Rarity weight for selection probability
    /// </summary>
    [JsonProperty("rarityWeight")]
    public int RarityWeight { get; set; }

    /// <summary>
    /// Description of the background's origin story
    /// </summary>
    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Primary attribute that receives the larger bonus
    /// </summary>
    [JsonProperty("primaryAttribute")]
    public string PrimaryAttribute { get; set; } = string.Empty;

    /// <summary>
    /// Bonus value applied to primary attribute (typically +2)
    /// </summary>
    [JsonProperty("primaryBonus")]
    public int PrimaryBonus { get; set; }

    /// <summary>
    /// Secondary attribute that receives the smaller bonus
    /// </summary>
    [JsonProperty("secondaryAttribute")]
    public string SecondaryAttribute { get; set; } = string.Empty;

    /// <summary>
    /// Bonus value applied to secondary attribute (typically +1)
    /// </summary>
    [JsonProperty("secondaryBonus")]
    public int SecondaryBonus { get; set; }

    /// <summary>
    /// List of recommended location types for starting zones (settlement, wilderness, dungeon)
    /// </summary>
    [JsonProperty("recommendedLocationTypes")]
    public List<string> RecommendedLocationTypes { get; set; } = new();

    /// <summary>
    /// Gets the full background ID in format "backgrounds/[type]:[slug]"
    /// </summary>
    public string GetBackgroundId()
    {
        var category = PrimaryAttribute.ToLowerInvariant();
        return $"backgrounds/{category}:{Slug}";
    }

    /// <summary>
    /// Apply attribute bonuses to a character's base stats
    /// </summary>
    public void ApplyBonuses(Character character)
    {
        ApplyAttributeBonus(character, PrimaryAttribute, PrimaryBonus);
        ApplyAttributeBonus(character, SecondaryAttribute, SecondaryBonus);
    }

    private void ApplyAttributeBonus(Character character, string attribute, int bonus)
    {
        switch (attribute.ToLowerInvariant())
        {
            case "strength":
                character.Strength += bonus;
                break;
            case "dexterity":
                character.Dexterity += bonus;
                break;
            case "constitution":
                character.Constitution += bonus;
                break;
            case "intelligence":
                character.Intelligence += bonus;
                break;
            case "wisdom":
                character.Wisdom += bonus;
                break;
            case "charisma":
                character.Charisma += bonus;
                break;
        }
    }
}
