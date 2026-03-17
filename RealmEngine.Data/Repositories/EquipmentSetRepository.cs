using RealmEngine.Shared.Models;
using RealmEngine.Shared.Abstractions;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// Repository of predefined equipment sets.
/// </summary>
public class EquipmentSetRepository : IEquipmentSetRepository
{
    /// <summary>
    /// Gets all available equipment sets.
    /// </summary>
    public List<EquipmentSet> GetAllSets()
    {
        return new List<EquipmentSet>
        {
            CreateDragonbornSet(),
            CreateShadowAssassinSet(),
            CreateArcaneScholarSet(),
            CreateIronGuardianSet(),
            CreateStormcallerSet()
        };
    }

    private EquipmentSet CreateDragonbornSet()
    {
        return new EquipmentSet
        {
            Name = "Dragonborn",
            Description = "Forged from dragon scales, this armor grants incredible strength and vitality.",
            SetItemNames = new List<string>
            {
                "Dragonborn Helmet",
                "Dragonborn Shoulders",
                "Dragonborn Chestpiece",
                "Dragonborn Gauntlets",
                "Dragonborn Greaves",
                "Dragonborn Boots"
            },
            Bonuses = new Dictionary<int, SetBonus>
            {
                { 2, new SetBonus { PiecesRequired = 2, Description = "+15 Strength", BonusStrength = 15 } },
                { 4, new SetBonus { PiecesRequired = 4, Description = "+30 Constitution", BonusConstitution = 30 } },
                { 6, new SetBonus { PiecesRequired = 6, Description = "+50 Strength, +25 Constitution", BonusStrength = 50, BonusConstitution = 25 } }
            }
        };
    }

    private EquipmentSet CreateShadowAssassinSet()
    {
        return new EquipmentSet
        {
            Name = "Shadow Assassin",
            Description = "Crafted for stealth and precision, favored by rogues and assassins.",
            SetItemNames = new List<string>
            {
                "Shadow Assassin Hood",
                "Shadow Assassin Cloak",
                "Shadow Assassin Gloves",
                "Shadow Assassin Leggings",
                "Shadow Assassin Boots"
            },
            Bonuses = new Dictionary<int, SetBonus>
            {
                { 2, new SetBonus { PiecesRequired = 2, Description = "+20 Dexterity", BonusDexterity = 20 } },
                { 4, new SetBonus { PiecesRequired = 4, Description = "+15 Strength, +30 Dexterity", BonusStrength = 15, BonusDexterity = 30 } },
                { 5, new SetBonus { PiecesRequired = 5, Description = "+40 Dexterity, +25 Constitution", BonusDexterity = 40, BonusConstitution = 25, SpecialEffect = "Critical Strike Chance +10%" } }
            }
        };
    }

    private EquipmentSet CreateArcaneScholarSet()
    {
        return new EquipmentSet
        {
            Name = "Arcane Scholar",
            Description = "Imbued with magical energy, perfect for spellcasters.",
            SetItemNames = new List<string>
            {
                "Arcane Scholar Circlet",
                "Arcane Scholar Robes",
                "Arcane Scholar Gloves",
                "Arcane Scholar Belt",
                "Arcane Scholar Boots"
            },
            Bonuses = new Dictionary<int, SetBonus>
            {
                { 2, new SetBonus { PiecesRequired = 2, Description = "+25 Intelligence", BonusIntelligence = 25 } },
                { 3, new SetBonus { PiecesRequired = 3, Description = "+40 Intelligence", BonusIntelligence = 40 } },
                { 5, new SetBonus { PiecesRequired = 5, Description = "+60 Intelligence, +20 Wisdom", BonusIntelligence = 60, BonusWisdom = 20, SpecialEffect = "Spell Power +15%" } }
            }
        };
    }

    private EquipmentSet CreateIronGuardianSet()
    {
        return new EquipmentSet
        {
            Name = "Iron Guardian",
            Description = "Heavy plate armor providing maximum protection.",
            SetItemNames = new List<string>
            {
                "Iron Guardian Helm",
                "Iron Guardian Pauldrons",
                "Iron Guardian Plate",
                "Iron Guardian Gauntlets",
                "Iron Guardian Greaves",
                "Iron Guardian Sabatons"
            },
            Bonuses = new Dictionary<int, SetBonus>
            {
                { 2, new SetBonus { PiecesRequired = 2, Description = "+20 Constitution", BonusConstitution = 20 } },
                { 4, new SetBonus { PiecesRequired = 4, Description = "+40 Constitution, +15 Strength", BonusConstitution = 40, BonusStrength = 15 } },
                { 6, new SetBonus { PiecesRequired = 6, Description = "+70 Constitution, +30 Strength", BonusConstitution = 70, BonusStrength = 30, SpecialEffect = "Damage Reduction +20%" } }
            }
        };
    }

    private EquipmentSet CreateStormcallerSet()
    {
        return new EquipmentSet
        {
            Name = "Stormcaller",
            Description = "Channels the power of storms, balancing magic and agility.",
            SetItemNames = new List<string>
            {
                "Stormcaller Crown",
                "Stormcaller Mantle",
                "Stormcaller Vestments",
                "Stormcaller Bindings",
                "Stormcaller Treads"
            },
            Bonuses = new Dictionary<int, SetBonus>
            {
                { 2, new SetBonus { PiecesRequired = 2, Description = "+15 Intelligence, +10 Dexterity", BonusIntelligence = 15, BonusDexterity = 10 } },
                { 3, new SetBonus { PiecesRequired = 3, Description = "+25 Intelligence, +20 Dexterity", BonusIntelligence = 25, BonusDexterity = 20 } },
                { 5, new SetBonus { PiecesRequired = 5, Description = "+45 Intelligence, +35 Dexterity", BonusIntelligence = 45, BonusDexterity = 35, SpecialEffect = "Lightning Damage +25%" } }
            }
        };
    }

    /// <inheritdoc />
    public EquipmentSet? GetById(string id) => GetByName(id); // ID is the name for equipment sets
    
    /// <inheritdoc />
    public EquipmentSet? GetByName(string name) => GetAllSets().FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    
    /// <inheritdoc />
    public List<EquipmentSet> GetAll() => GetAllSets();
}
