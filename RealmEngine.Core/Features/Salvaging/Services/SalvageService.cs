using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Salvaging.Services;

/// <summary>Pure calculation helpers shared by the salvaging command and preview query.</summary>
public static class SalvageService
{
    /// <summary>Returns <see langword="true"/> if the item type is eligible for salvaging.</summary>
    public static bool CanBeSalvaged(ItemType type)
        => type is not (ItemType.Consumable or ItemType.QuestItem or ItemType.Material);

    /// <summary>Returns the crafting skill that governs salvage yield for the given item type.</summary>
    public static string GetSkillName(ItemType itemType) => itemType switch
    {
        ItemType.Weapon    => "Blacksmithing",
        ItemType.Shield    => "Blacksmithing",
        ItemType.OffHand   => "Blacksmithing",
        ItemType.Helmet    => "Blacksmithing",
        ItemType.Shoulders => "Leatherworking",
        ItemType.Chest     => "Blacksmithing",
        ItemType.Bracers   => "Leatherworking",
        ItemType.Gloves    => "Leatherworking",
        ItemType.Belt      => "Leatherworking",
        ItemType.Legs      => "Blacksmithing",
        ItemType.Boots     => "Leatherworking",
        ItemType.Necklace  => "Jewelcrafting",
        ItemType.Ring      => "Jewelcrafting",
        _                  => "Salvaging"
    };

    /// <summary>
    /// Calculates the yield rate (40–100 %) based on the character's rank in the
    /// governing crafting skill. Formula: 40 % base + (rank × 0.3 %), capped at 100 %.
    /// </summary>
    public static double CalculateYieldRate(Character character, ItemType itemType)
    {
        var skillName  = GetSkillName(itemType);
        var skillLevel = character.Skills.TryGetValue(skillName, out var skill) ? skill.CurrentRank : 0;
        return Math.Min(40.0 + (skillLevel * 0.3), 100.0);
    }

    /// <summary>
    /// Returns the expected scrap materials and their quantities at the given yield rate.
    /// Implements the two-tier system: salvage → scrap → refine (3:1) → materials.
    /// </summary>
    public static Dictionary<string, int> GetExpectedScrap(Item item, double yieldRate)
    {
        var result = new Dictionary<string, int>();
        var (primaryScrap, secondaryScrap) = GetScrapTypes(item.Type);

        var baseYield = item.Rarity switch
        {
            ItemRarity.Common    => 3,
            ItemRarity.Uncommon  => 4,
            ItemRarity.Rare      => 5,
            ItemRarity.Epic      => 7,
            ItemRarity.Legendary => 10,
            _                    => 3
        };

        var totalYield = (int)Math.Ceiling((baseYield + item.UpgradeLevel) * (yieldRate / 100.0));

        // Guarantee at least 1 scrap when yield rate is meaningful
        if (totalYield < 1 && yieldRate >= 10)
            totalYield = 1;

        var primaryAmount   = !string.IsNullOrEmpty(secondaryScrap)
            ? Math.Max(1, (int)Math.Ceiling(totalYield * 0.7))
            : totalYield;
        var secondaryAmount = !string.IsNullOrEmpty(secondaryScrap)
            ? Math.Max(1, totalYield - primaryAmount)
            : 0;

        if (primaryAmount > 0)
            result[primaryScrap] = primaryAmount;

        if (secondaryAmount > 0 && !string.IsNullOrEmpty(secondaryScrap))
            result[secondaryScrap] = secondaryAmount;

        return result;
    }

    /// <summary>Returns the primary and secondary scrap type names for an item.</summary>
    internal static (string primary, string secondary) GetScrapTypes(ItemType itemType) => itemType switch
    {
        ItemType.Weapon    => ("Scrap Metal",       "Scrap Wood"),
        ItemType.Shield    => ("Scrap Metal",       "Scrap Wood"),
        ItemType.Helmet    => ("Scrap Metal",       ""),
        ItemType.Chest     => ("Scrap Metal",       "Scrap Leather"),
        ItemType.Legs      => ("Scrap Metal",       ""),
        ItemType.Shoulders => ("Scrap Leather",     "Scrap Cloth"),
        ItemType.Bracers   => ("Scrap Leather",     ""),
        ItemType.Gloves    => ("Scrap Leather",     ""),
        ItemType.Belt      => ("Scrap Leather",     ""),
        ItemType.Boots     => ("Scrap Leather",     ""),
        ItemType.Necklace  => ("Gemstone Fragments","Scrap Metal"),
        ItemType.Ring      => ("Gemstone Fragments","Scrap Metal"),
        ItemType.OffHand   => ("Scrap Wood",        "Scrap Cloth"),
        _                  => ("Scrap Metal",       "")
    };
}
