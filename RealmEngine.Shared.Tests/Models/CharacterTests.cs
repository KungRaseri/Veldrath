using FluentAssertions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Tests.Models;

[Trait("Category", "Unit")]
public class CharacterTests
{
    [Fact]
    public void Character_Should_Initialize_With_Default_Values()
    {
        // Arrange & Act
        var character = new Character();

        // Assert
        character.Level.Should().Be(1);
        character.Health.Should().Be(100);
        character.MaxHealth.Should().Be(100);
        character.Mana.Should().Be(50);
        character.MaxMana.Should().Be(50);
        character.Gold.Should().Be(0);
        character.Experience.Should().Be(0);
    }

    [Fact]
    public void GainExperience_Should_Increase_Experience()
    {
        // Arrange
        var character = new Character { Level = 1, Experience = 0 };

        // Act
        character.GainExperience(50);

        // Assert
        character.Experience.Should().Be(50);
        character.Level.Should().Be(1); // Not enough XP to level up
    }

    [Fact]
    public void GainExperience_Should_Level_Up_When_Enough_XP()
    {
        // Arrange
        var character = new Character { Level = 1, Experience = 0 };

        // Act
        character.GainExperience(100); // Exactly enough for level 2

        // Assert
        character.Level.Should().Be(2);
        character.Experience.Should().Be(0); // XP resets after level up
    }

    [Fact]
    public void GainExperience_Should_Increase_Stats_On_Level_Up()
    {
        // Arrange
        var character = new Character
        {
            Level = 1,
            Experience = 0
        };

        // Set initial values from D20 calculations
        character.MaxHealth = character.GetMaxHealth(); // (CON:10 × 10) + (Level:1 × 5) = 105
        character.Health = character.MaxHealth;
        character.MaxMana = character.GetMaxMana();     // (WIS:10 × 5) + (Level:1 × 3) = 53
        character.Mana = character.MaxMana;

        var initialMaxHealth = character.MaxHealth;
        var initialMaxMana = character.MaxMana;
        var initialConstitution = character.Constitution;
        var initialAttributePoints = character.UnspentAttributePoints;

        // Act - Level up to 2
        character.GainExperience(100);

        // Assert
        character.Level.Should().Be(2);

        // Stats should NOT auto-increase (new system - player allocates)
        character.Constitution.Should().Be(initialConstitution); // No auto-increase
        character.Strength.Should().Be(10);   // Base stats unchanged
        character.Dexterity.Should().Be(10);
        character.Intelligence.Should().Be(10);
        character.Wisdom.Should().Be(10);
        character.Charisma.Should().Be(10);

        // Should have unspent attribute points
        character.UnspentAttributePoints.Should().Be(initialAttributePoints + 3); // 3 points per level

        // Should have a pending level-up
        character.PendingLevelUps.Should().HaveCount(1);

        // MaxHealth and MaxMana should still recalculate based on level
        character.MaxHealth.Should().BeGreaterThan(initialMaxHealth);
        character.MaxMana.Should().BeGreaterThan(initialMaxMana);

        // Health and Mana should be fully restored
        character.Health.Should().Be(character.MaxHealth);
        character.Mana.Should().Be(character.MaxMana);
    }

    [Theory]
    [InlineData(100, 2, 0)]
    [InlineData(150, 2, 50)]
    [InlineData(300, 3, 0)]
    [InlineData(350, 3, 50)]
    public void GainExperience_Should_Handle_Multiple_Level_Ups(int xpGained, int expectedLevel, int expectedRemainingXp)
    {
        // Arrange
        var character = new Character { Level = 1, Experience = 0 };

        // Act
        character.GainExperience(xpGained);

        // Assert
        character.Level.Should().Be(expectedLevel);
        character.Experience.Should().Be(expectedRemainingXp);
    }

    [Fact]
    public void Character_Should_Allow_Setting_Name()
    {
        // Arrange
        var character = new Character();

        // Act
        character.Name = "Hero";

        // Assert
        character.Name.Should().Be("Hero");
    }

    [Fact]
    public void Character_Should_Allow_Gaining_Gold()
    {
        // Arrange
        var character = new Character { Gold = 100 };

        // Act
        character.Gold += 50;

        // Assert
        character.Gold.Should().Be(150);
    }

    [Fact]
    public void GetMaxHealth_Should_Calculate_Based_On_Constitution_And_Level()
    {
        // Arrange
        var character = new Character
        {
            Constitution = 14,
            Level = 5
        };

        // Act
        var maxHealth = character.GetMaxHealth();

        // Assert
        // Formula: (CON × 10) + (Level × 5)
        // (14 × 10) + (5 × 5) = 140 + 25 = 165
        maxHealth.Should().Be(165);
    }

    [Fact]
    public void GetMaxMana_Should_Calculate_Based_On_Wisdom_And_Level()
    {
        // Arrange
        var character = new Character
        {
            Wisdom = 16,
            Level = 8
        };

        // Act
        var maxMana = character.GetMaxMana();

        // Assert
        // Formula: (WIS × 5) + (Level × 3)
        // (16 × 5) + (8 × 3) = 80 + 24 = 104
        maxMana.Should().Be(104);
    }

    [Fact]
    public void GetPhysicalDamageBonus_Should_Calculate_Based_On_Strength()
    {
        // Arrange
        var character = new Character { Strength = 18 };

        // Act
        var damage = character.GetPhysicalDamageBonus();

        // Assert
        // Formula: STR
        damage.Should().Be(18);
    }

    [Fact]
    public void GetMagicDamageBonus_Should_Calculate_Based_On_Intelligence()
    {
        // Arrange
        var character = new Character { Intelligence = 20 };

        // Act
        var damage = character.GetMagicDamageBonus();

        // Assert
        // Formula: INT
        damage.Should().Be(20);
    }

    [Fact]
    public void GetDodgeChance_Should_Calculate_Based_On_Dexterity()
    {
        // Arrange
        var character = new Character { Dexterity = 16 };

        // Act
        var dodgeChance = character.GetDodgeChance();

        // Assert
        // Check that it returns a reasonable value
        dodgeChance.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void GetCriticalChance_Should_Calculate_Based_On_Dexterity()
    {
        // Arrange
        var character = new Character { Dexterity = 14 };

        // Act
        var critChance = character.GetCriticalChance();

        // Assert
        // Check that it returns a reasonable value
        critChance.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void GetPhysicalDefense_Should_Calculate_Based_On_Constitution()
    {
        // Arrange
        var character = new Character { Constitution = 18 };

        // Act
        var defense = character.GetPhysicalDefense();

        // Assert
        // Check that it returns a positive value
        defense.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetMagicResistance_Should_Calculate_Based_On_Wisdom()
    {
        // Arrange
        var character = new Character { Wisdom = 14 };

        // Act
        var resistance = character.GetMagicResistance();

        // Assert
        // Check that it returns a reasonable value
        resistance.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void GetRareItemChance_Should_Calculate_Based_On_Charisma()
    {
        // Arrange
        var character = new Character { Charisma = 16 };

        // Act
        var rareChance = character.GetRareItemChance();

        // Assert
        // Check that it returns a positive value
        rareChance.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Character_Should_Start_With_Empty_Inventory()
    {
        // Arrange & Act
        var character = new Character();

        // Assert
        character.Inventory.Should().NotBeNull();
        character.Inventory.Should().BeEmpty();
    }

    [Fact]
    public void Character_Should_Allow_Adding_Items_To_Inventory()
    {
        // Arrange
        var character = new Character();
        var item = new Item { Name = "Potion" };

        // Act
        character.Inventory.Add(item);

        // Assert
        character.Inventory.Should().HaveCount(1);
        character.Inventory[0].Should().Be(item);
    }

    [Fact]
    public void Character_Should_Start_With_Base_Attributes_Of_10()
    {
        // Arrange & Act
        var character = new Character();

        // Assert
        character.Strength.Should().Be(10);
        character.Dexterity.Should().Be(10);
        character.Constitution.Should().Be(10);
        character.Intelligence.Should().Be(10);
        character.Wisdom.Should().Be(10);
        character.Charisma.Should().Be(10);
    }

    [Fact]
    public void Character_Should_Start_With_Zero_Unspent_Points()
    {
        // Arrange & Act
        var character = new Character();

        // Assert
        character.UnspentAttributePoints.Should().Be(0);
        character.UnspentSkillPoints.Should().Be(0);
    }

    [Fact]
    public void IsAlive_Should_Return_True_When_Health_Above_Zero()
    {
        // Arrange
        var character = new Character { Health = 1 };

        // Act
        var isAlive = character.IsAlive();

        // Assert
        isAlive.Should().BeTrue();
    }

    [Fact]
    public void IsAlive_Should_Return_False_When_Health_Is_Zero()
    {
        // Arrange
        var character = new Character { Health = 0 };

        // Act
        var isAlive = character.IsAlive();

        // Assert
        isAlive.Should().BeFalse();
    }

    [Fact]
    public void Character_Should_Allow_Setting_ClassName()
    {
        // Arrange
        var character = new Character();

        // Act
        character.ClassName = "Warrior";

        // Assert
        character.ClassName.Should().Be("Warrior");
    }

    [Fact]
    public void GetPhysicalDamageBonus_Should_Return_Strength_Value()
    {
        // Arrange
        var character = new Character { Strength = 10 };

        // Act
        var damage = character.GetPhysicalDamageBonus();

        // Assert
        damage.Should().Be(10);
    }

    [Fact]
    public void GetPhysicalDefense_Should_Return_Value_For_Constitution()
    {
        // Arrange
        var character = new Character { Constitution = 8 };

        // Act
        var defense = character.GetPhysicalDefense();

        // Assert
        defense.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void GainExperience_Should_Create_Pending_LevelUp_Info()
    {
        // Arrange
        var character = new Character { Level = 1, Experience = 0 };

        // Act
        character.GainExperience(100);

        // Assert
        character.PendingLevelUps.Should().HaveCount(1);
        character.PendingLevelUps[0].NewLevel.Should().Be(2);
        character.PendingLevelUps[0].IsProcessed.Should().BeFalse();
    }

    [Fact]
    public void Character_Should_Start_With_Empty_PendingLevelUps()
    {
        // Arrange & Act
        var character = new Character();

        // Assert
        character.PendingLevelUps.Should().NotBeNull();
        character.PendingLevelUps.Should().BeEmpty();
    }

    [Fact]
    public void Character_Should_Start_With_Empty_Skills()
    {
        // Arrange & Act
        var character = new Character();

        // Assert
        character.Skills.Should().NotBeNull();
        character.Skills.Should().BeEmpty();
    }

    [Fact]
    public void GetShopDiscount_Should_Calculate_Based_On_Charisma()
    {
        // Arrange
        var character = new Character
        {
            Charisma = 20 // 20 * 1.0 = 20% discount
        };

        // Act
        var discount = character.GetShopDiscount();

        // Assert
        discount.Should().Be(20.0);
    }

    [Fact]
    public void GetShopDiscount_Should_Return_Small_Value_For_Low_Charisma()
    {
        // Arrange
        var character = new Character
        {
            Charisma = 3 // 3 * 1.0 = 3% discount
        };

        // Act
        var discount = character.GetShopDiscount();

        // Assert
        discount.Should().Be(3.0);
    }

    [Theory]
    [InlineData(10, 10.0)] // 10 * 1.0 = 10%
    [InlineData(25, 25.0)] // 25 * 1.0 = 25%
    [InlineData(50, 50.0)] // 50 * 1.0 = 50%
    [InlineData(100, 100.0)] // 100 * 1.0 = 100%
    public void GetShopDiscount_Should_Scale_Linearly_With_Charisma(int charisma, double expectedDiscount)
    {
        // Arrange
        var character = new Character
        {
            Charisma = charisma
        };

        // Act
        var discount = character.GetShopDiscount();

        // Assert
        discount.Should().BeApproximately(expectedDiscount, 0.001);
    }

    [Fact]
    public void GetTotalStrength_Should_Return_Base_Value_Without_Equipment()
    {
        // Arrange
        var character = new Character
        {
            Strength = 15
        };

        // Act
        var totalStrength = character.GetTotalStrength();

        // Assert
        totalStrength.Should().Be(15);
    }

    [Fact]
    public void GetTotalDexterity_Should_Return_Base_Value_Without_Equipment()
    {
        // Arrange
        var character = new Character
        {
            Dexterity = 20
        };

        // Act
        var totalDexterity = character.GetTotalDexterity();

        // Assert
        totalDexterity.Should().Be(20);
    }

    [Fact]
    public void GetTotalConstitution_Should_Return_Base_Value_Without_Equipment()
    {
        // Arrange
        var character = new Character
        {
            Constitution = 18
        };

        // Act
        var totalConstitution = character.GetTotalConstitution();

        // Assert
        totalConstitution.Should().Be(18);
    }

    [Fact]
    public void GetTotalIntelligence_Should_Return_Base_Value_Without_Equipment()
    {
        // Arrange
        var character = new Character
        {
            Intelligence = 22
        };

        // Act
        var totalIntelligence = character.GetTotalIntelligence();

        // Assert
        totalIntelligence.Should().Be(22);
    }

    [Fact]
    public void GetTotalWisdom_Should_Return_Base_Value_Without_Equipment()
    {
        // Arrange
        var character = new Character
        {
            Wisdom = 16
        };

        // Act
        var totalWisdom = character.GetTotalWisdom();

        // Assert
        totalWisdom.Should().Be(16);
    }

    [Fact]
    public void GetTotalCharisma_Should_Return_Base_Value_Without_Equipment()
    {
        // Arrange
        var character = new Character
        {
            Charisma = 14
        };

        // Act
        var totalCharisma = character.GetTotalCharisma();

        // Assert
        totalCharisma.Should().Be(14);
    }

    [Fact]
    public void GetTotalStrength_Should_Include_MainHand_Weapon_Bonus()
    {
        // Arrange
        var character = new Character
        {
            Strength = 10,
            EquippedMainHand = new Item
            {
                Name = "Iron Sword",
                Traits = new Dictionary<string, TraitValue> { { "Strength", new TraitValue(5, TraitType.Number) } }
            }
        };

        // Act
        var totalStrength = character.GetTotalStrength();

        // Assert
        totalStrength.Should().Be(15); // 10 + 5
    }

    [Fact]
    public void GetTotalStrength_Should_Include_Multiple_Equipment_Pieces()
    {
        // Arrange
        var character = new Character
        {
            Strength = 10,
            EquippedMainHand = new Item { Name = "Sword", Traits = new Dictionary<string, TraitValue> { { "Strength", new TraitValue(5, TraitType.Number) } } },
            EquippedChest = new Item { Name = "Plate Armor", Traits = new Dictionary<string, TraitValue> { { "Strength", new TraitValue(3, TraitType.Number) } } },
            EquippedGloves = new Item { Name = "Gauntlets", Traits = new Dictionary<string, TraitValue> { { "Strength", new TraitValue(2, TraitType.Number) } } }
        };

        // Act
        var totalStrength = character.GetTotalStrength();

        // Assert
        totalStrength.Should().Be(20); // 10 + 5 + 3 + 2
    }

    [Fact]
    public void GetTotalStrength_Should_Include_Jewelry_Bonuses()
    {
        // Arrange
        var character = new Character
        {
            Strength = 10,
            EquippedNecklace = new Item { Name = "Amulet of Strength", Traits = new Dictionary<string, TraitValue> { { "Strength", new TraitValue(3, TraitType.Number) } } },
            EquippedRing1 = new Item { Name = "Ring of Power", Traits = new Dictionary<string, TraitValue> { { "Strength", new TraitValue(2, TraitType.Number) } } },
            EquippedRing2 = new Item { Name = "Band of Might", Traits = new Dictionary<string, TraitValue> { { "Strength", new TraitValue(2, TraitType.Number) } } }
        };

        // Act
        var totalStrength = character.GetTotalStrength();

        // Assert
        totalStrength.Should().Be(17); // 10 + 3 + 2 + 2
    }

    [Fact]
    public void GetTotalStrength_Should_Include_All_13_Equipment_Slots()
    {
        // Arrange
        var character = new Character
        {
            Strength = 10,
            EquippedMainHand = new Item { Name = "Weapon", Traits = new Dictionary<string, TraitValue> { { "Strength", new TraitValue(1, TraitType.Number) } } },
            EquippedOffHand = new Item { Name = "Shield", Traits = new Dictionary<string, TraitValue> { { "Strength", new TraitValue(1, TraitType.Number) } } },
            EquippedHelmet = new Item { Name = "Helmet", Traits = new Dictionary<string, TraitValue> { { "Strength", new TraitValue(1, TraitType.Number) } } },
            EquippedShoulders = new Item { Name = "Shoulders", Traits = new Dictionary<string, TraitValue> { { "Strength", new TraitValue(1, TraitType.Number) } } },
            EquippedChest = new Item { Name = "Chest", Traits = new Dictionary<string, TraitValue> { { "Strength", new TraitValue(1, TraitType.Number) } } },
            EquippedBracers = new Item { Name = "Bracers", Traits = new Dictionary<string, TraitValue> { { "Strength", new TraitValue(1, TraitType.Number) } } },
            EquippedGloves = new Item { Name = "Gloves", Traits = new Dictionary<string, TraitValue> { { "Strength", new TraitValue(1, TraitType.Number) } } },
            EquippedBelt = new Item { Name = "Belt", Traits = new Dictionary<string, TraitValue> { { "Strength", new TraitValue(1, TraitType.Number) } } },
            EquippedLegs = new Item { Name = "Legs", Traits = new Dictionary<string, TraitValue> { { "Strength", new TraitValue(1, TraitType.Number) } } },
            EquippedBoots = new Item { Name = "Boots", Traits = new Dictionary<string, TraitValue> { { "Strength", new TraitValue(1, TraitType.Number) } } },
            EquippedNecklace = new Item { Name = "Necklace", Traits = new Dictionary<string, TraitValue> { { "Strength", new TraitValue(1, TraitType.Number) } } },
            EquippedRing1 = new Item { Name = "Ring1", Traits = new Dictionary<string, TraitValue> { { "Strength", new TraitValue(1, TraitType.Number) } } },
            EquippedRing2 = new Item { Name = "Ring2", Traits = new Dictionary<string, TraitValue> { { "Strength", new TraitValue(1, TraitType.Number) } } }
        };

        // Act
        var totalStrength = character.GetTotalStrength();

        // Assert
        totalStrength.Should().Be(23); // 10 + 13 (all slots)
    }

    [Fact]
    public void GetTotalDexterity_Should_Include_Equipment_Bonuses()
    {
        // Arrange
        var character = new Character
        {
            Dexterity = 10,
            EquippedMainHand = new Item { Name = "Dagger", Traits = new Dictionary<string, TraitValue> { { "Dexterity", new TraitValue(4, TraitType.Number) } } },
            EquippedGloves = new Item { Name = "Thief Gloves", Traits = new Dictionary<string, TraitValue> { { "Dexterity", new TraitValue(3, TraitType.Number) } } }
        };

        // Act
        var totalDexterity = character.GetTotalDexterity();

        // Assert
        totalDexterity.Should().Be(17); // 10 + 4 + 3
    }

    [Fact]
    public void GetTotalConstitution_Should_Include_Equipment_Bonuses()
    {
        // Arrange
        var character = new Character
        {
            Constitution = 10,
            EquippedChest = new Item { Name = "Iron Armor", Traits = new Dictionary<string, TraitValue> { { "Constitution", new TraitValue(5, TraitType.Number) } } },
            EquippedHelmet = new Item { Name = "Iron Helm", Traits = new Dictionary<string, TraitValue> { { "Constitution", new TraitValue(2, TraitType.Number) } } }
        };

        // Act
        var totalConstitution = character.GetTotalConstitution();

        // Assert
        totalConstitution.Should().Be(17); // 10 + 5 + 2
    }

    [Fact]
    public void GetTotalIntelligence_Should_Include_Equipment_Bonuses()
    {
        // Arrange
        var character = new Character
        {
            Intelligence = 10,
            EquippedMainHand = new Item { Name = "Magic Staff", Traits = new Dictionary<string, TraitValue> { { "Intelligence", new TraitValue(7, TraitType.Number) } } },
            EquippedNecklace = new Item { Name = "Arcane Amulet", Traits = new Dictionary<string, TraitValue> { { "Intelligence", new TraitValue(3, TraitType.Number) } } }
        };

        // Act
        var totalIntelligence = character.GetTotalIntelligence();

        // Assert
        totalIntelligence.Should().Be(20); // 10 + 7 + 3
    }

    [Fact]
    public void GetTotalWisdom_Should_Include_Equipment_Bonuses()
    {
        // Arrange
        var character = new Character
        {
            Wisdom = 10,
            EquippedHelmet = new Item { Name = "Crown of Wisdom", Traits = new Dictionary<string, TraitValue> { { "Wisdom", new TraitValue(5, TraitType.Number) } } },
            EquippedRing1 = new Item { Name = "Ring of Insight", Traits = new Dictionary<string, TraitValue> { { "Wisdom", new TraitValue(2, TraitType.Number) } } }
        };

        // Act
        var totalWisdom = character.GetTotalWisdom();

        // Assert
        totalWisdom.Should().Be(17); // 10 + 5 + 2
    }

    [Fact]
    public void GetTotalCharisma_Should_Include_Equipment_Bonuses()
    {
        // Arrange
        var character = new Character
        {
            Charisma = 10,
            EquippedChest = new Item { Name = "Royal Robes", Traits = new Dictionary<string, TraitValue> { { "Charisma", new TraitValue(4, TraitType.Number) } } },
            EquippedNecklace = new Item { Name = "Noble Pendant", Traits = new Dictionary<string, TraitValue> { { "Charisma", new TraitValue(3, TraitType.Number) } } }
        };

        // Act
        var totalCharisma = character.GetTotalCharisma();

        // Assert
        totalCharisma.Should().Be(17); // 10 + 4 + 3
    }

    [Fact]
    public void GetTotalStrength_Should_Include_Item_Enchantments()
    {
        // Arrange
        var character = new Character
        {
            Strength = 10,
            EquippedMainHand = new Item
            {
                Name = "Enchanted Sword",
                Traits = new Dictionary<string, TraitValue> { { "Strength", new TraitValue(5, TraitType.Number) } },
                Enchantments = new List<Enchantment>
                {
                    new Enchantment
                    {
                        Name = "Strength I",
                        Traits = new Dictionary<string, TraitValue> { { "Strength", new TraitValue(3, TraitType.Number) } }
                    }
                }
            }
        };

        // Act
        var totalStrength = character.GetTotalStrength();

        // Assert
        totalStrength.Should().Be(18); // 10 + 5 base + 3 enchantment
    }

    [Fact]
    public void GetTotalDexterity_Should_Include_Item_Upgrade_Levels()
    {
        // Arrange
        var character = new Character
        {
            Dexterity = 10,
            EquippedMainHand = new Item
            {
                Name = "Upgraded Dagger +3",
                Traits = new Dictionary<string, TraitValue> { { "Dexterity", new TraitValue(5, TraitType.Number) } },
                UpgradeLevel = 3 // +6 to all stats (3 * 2)
            }
        };

        // Act
        var totalDexterity = character.GetTotalDexterity();

        // Assert
        totalDexterity.Should().Be(21); // 10 + 5 base + 6 upgrade bonus
    }

    #region GetEquippedItems Tests

    [Fact]
    public void GetEquippedItems_Should_Return_Empty_List_When_No_Equipment()
    {
        // Arrange
        var character = new Character();

        // Act
        var equippedItems = character.GetEquippedItems();

        // Assert
        equippedItems.Should().NotBeNull();
        equippedItems.Should().BeEmpty();
    }

    [Fact]
    public void GetEquippedItems_Should_Return_Only_Equipped_Items()
    {
        // Arrange
        var sword = new Item { Id = "1", Name = "Sword", Type = ItemType.Weapon };
        var shield = new Item { Id = "2", Name = "Shield", Type = ItemType.Shield };
        var helmet = new Item { Id = "3", Name = "Helmet", Type = ItemType.Helmet };

        var character = new Character
        {
            EquippedMainHand = sword,
            EquippedOffHand = shield,
            EquippedHelmet = helmet
            // Other slots are null
        };

        // Act
        var equippedItems = character.GetEquippedItems();

        // Assert
        equippedItems.Should().HaveCount(3);
        equippedItems.Should().Contain(sword);
        equippedItems.Should().Contain(shield);
        equippedItems.Should().Contain(helmet);
    }

    [Fact]
    public void GetEquippedItems_Should_Return_All_Equipped_Items()
    {
        // Arrange
        var character = new Character
        {
            EquippedMainHand = new Item { Id = "1", Name = "Sword" },
            EquippedOffHand = new Item { Id = "2", Name = "Shield" },
            EquippedHelmet = new Item { Id = "3", Name = "Helmet" },
            EquippedShoulders = new Item { Id = "4", Name = "Shoulders" },
            EquippedChest = new Item { Id = "5", Name = "Chestplate" },
            EquippedBracers = new Item { Id = "6", Name = "Bracers" },
            EquippedGloves = new Item { Id = "7", Name = "Gloves" },
            EquippedBelt = new Item { Id = "8", Name = "Belt" },
            EquippedLegs = new Item { Id = "9", Name = "Leggings" },
            EquippedBoots = new Item { Id = "10", Name = "Boots" },
            EquippedNecklace = new Item { Id = "11", Name = "Necklace" },
            EquippedRing1 = new Item { Id = "12", Name = "Ring of Power" },
            EquippedRing2 = new Item { Id = "13", Name = "Ring of Wisdom" }
        };

        // Act
        var equippedItems = character.GetEquippedItems();

        // Assert
        equippedItems.Should().HaveCount(13);
    }

    #endregion

    #region GetActiveEquipmentSets Tests

    [Fact]
    public void GetActiveEquipmentSets_Should_Return_Empty_When_No_Set_Items()
    {
        // Arrange
        var character = new Character
        {
            EquippedMainHand = new Item { Id = "1", Name = "Sword", SetName = null },
            EquippedHelmet = new Item { Id = "2", Name = "Helmet", SetName = "" }
        };

        // Act
        var activeSets = character.GetActiveEquipmentSets();

        // Assert
        activeSets.Should().BeEmpty();
    }

    [Fact]
    public void GetActiveEquipmentSets_Should_Count_Single_Set()
    {
        // Arrange
        var character = new Character
        {
            EquippedHelmet = new Item { Id = "1", Name = "Warrior Helmet", SetName = "Warrior Set" },
            EquippedChest = new Item { Id = "2", Name = "Warrior Chestplate", SetName = "Warrior Set" },
            EquippedGloves = new Item { Id = "3", Name = "Warrior Gloves", SetName = "Warrior Set" }
        };

        // Act
        var activeSets = character.GetActiveEquipmentSets();

        // Assert
        activeSets.Should().ContainKey("Warrior Set");
        activeSets["Warrior Set"].Should().Be(3);
    }

    [Fact]
    public void GetActiveEquipmentSets_Should_Count_Multiple_Sets()
    {
        // Arrange
        var character = new Character
        {
            EquippedHelmet = new Item { Id = "1", Name = "Warrior Helmet", SetName = "Warrior Set" },
            EquippedChest = new Item { Id = "2", Name = "Warrior Chestplate", SetName = "Warrior Set" },
            EquippedBoots = new Item { Id = "3", Name = "Mage Boots", SetName = "Mage Set" },
            EquippedGloves = new Item { Id = "4", Name = "Mage Gloves", SetName = "Mage Set" },
            EquippedRing1 = new Item { Id = "5", Name = "Assassin Ring", SetName = "Assassin Set" }
        };

        // Act
        var activeSets = character.GetActiveEquipmentSets();

        // Assert
        activeSets.Should().HaveCount(3);
        activeSets["Warrior Set"].Should().Be(2);
        activeSets["Mage Set"].Should().Be(2);
        activeSets["Assassin Set"].Should().Be(1);
    }

    [Fact]
    public void GetActiveEquipmentSets_Should_Mix_Set_And_NonSet_Items()
    {
        // Arrange
        var character = new Character
        {
            EquippedHelmet = new Item { Id = "1", Name = "Warrior Helmet", SetName = "Warrior Set" },
            EquippedChest = new Item { Id = "2", Name = "Random Chestplate", SetName = null },
            EquippedGloves = new Item { Id = "3", Name = "Warrior Gloves", SetName = "Warrior Set" }
        };

        // Act
        var activeSets = character.GetActiveEquipmentSets();

        // Assert
        activeSets.Should().HaveCount(1);
        activeSets["Warrior Set"].Should().Be(2);
    }

    #endregion

    #region GetSetBonuses Tests

    [Fact]
    public void GetSetBonuses_Should_Return_Empty_When_No_Sets_Available()
    {
        // Arrange
        var character = new Character
        {
            EquippedHelmet = new Item { Id = "1", Name = "Warrior Helmet", SetName = "Warrior Set" }
        };
        var availableSets = new List<EquipmentSet>();

        // Act
        var bonuses = character.GetSetBonuses(availableSets, "Strength");

        // Assert
        bonuses.Should().BeEmpty();
    }

    [Fact]
    public void GetSetBonuses_Should_Return_Empty_When_No_Sets_Equipped()
    {
        // Arrange
        var character = new Character
        {
            EquippedHelmet = new Item { Id = "1", Name = "Random Helmet", SetName = null }
        };
        var availableSets = new List<EquipmentSet>
        {
            new EquipmentSet
            {
                Name = "Warrior Set",
                Bonuses = new Dictionary<int, SetBonus>
                {
                    { 2, new SetBonus { BonusStrength = 10 } }
                }
            }
        };

        // Act
        var bonuses = character.GetSetBonuses(availableSets, "Strength");

        // Assert
        bonuses.Should().BeEmpty();
    }

    [Fact]
    public void GetSetBonuses_Should_Calculate_Strength_Bonus()
    {
        // Arrange
        var character = new Character
        {
            EquippedHelmet = new Item { Id = "1", Name = "Warrior Helmet", SetName = "Warrior Set" },
            EquippedChest = new Item { Id = "2", Name = "Warrior Chestplate", SetName = "Warrior Set" }
        };
        var availableSets = new List<EquipmentSet>
        {
            new EquipmentSet
            {
                Name = "Warrior Set",
                Bonuses = new Dictionary<int, SetBonus>
                {
                    { 2, new SetBonus { BonusStrength = 10, PiecesRequired = 2 } }
                }
            }
        };

        // Act
        var bonuses = character.GetSetBonuses(availableSets, "Strength");

        // Assert
        bonuses.Should().ContainKey("Warrior Set (2 pieces)");
        bonuses["Warrior Set (2 pieces)"].Should().Be(10);
    }

    [Fact]
    public void GetSetBonuses_Should_Calculate_Multiple_Stat_Types()
    {
        // Arrange
        var character = new Character
        {
            EquippedHelmet = new Item { Id = "1", Name = "Mage Helmet", SetName = "Mage Set" },
            EquippedChest = new Item { Id = "2", Name = "Mage Robe", SetName = "Mage Set" }
        };
        var availableSets = new List<EquipmentSet>
        {
            new EquipmentSet
            {
                Name = "Mage Set",
                Bonuses = new Dictionary<int, SetBonus>
                {
                    { 2, new SetBonus { BonusIntelligence = 15, BonusWisdom = 10, PiecesRequired = 2 } }
                }
            }
        };

        // Act
        var intelligenceBonuses = character.GetSetBonuses(availableSets, "Intelligence");
        var wisdomBonuses = character.GetSetBonuses(availableSets, "Wisdom");
        var strengthBonuses = character.GetSetBonuses(availableSets, "Strength");

        // Assert
        intelligenceBonuses["Mage Set (2 pieces)"].Should().Be(15);
        wisdomBonuses["Mage Set (2 pieces)"].Should().Be(10);
        strengthBonuses.Should().BeEmpty(); // No strength bonus on this set
    }

    [Fact]
    public void GetSetBonuses_Should_Only_Apply_Bonuses_For_Equipped_Pieces()
    {
        // Arrange
        var character = new Character
        {
            EquippedHelmet = new Item { Id = "1", Name = "Warrior Helmet", SetName = "Warrior Set" },
            EquippedChest = new Item { Id = "2", Name = "Warrior Chestplate", SetName = "Warrior Set" }
            // Only 2 pieces equipped
        };
        var availableSets = new List<EquipmentSet>
        {
            new EquipmentSet
            {
                Name = "Warrior Set",
                Bonuses = new Dictionary<int, SetBonus>
                {
                    { 2, new SetBonus { BonusStrength = 10, PiecesRequired = 2 } },
                    { 4, new SetBonus { BonusStrength = 25, PiecesRequired = 4 } }
                }
            }
        };

        // Act
        var bonuses = character.GetSetBonuses(availableSets, "Strength");

        // Assert
        bonuses.Should().HaveCount(1);
        bonuses.Should().ContainKey("Warrior Set (2 pieces)");
        bonuses.Should().NotContainKey("Warrior Set (4 pieces)"); // Not enough pieces
    }

    [Fact]
    public void GetSetBonuses_Should_Apply_Multiple_Tier_Bonuses()
    {
        // Arrange
        var character = new Character
        {
            EquippedHelmet = new Item { Id = "1", Name = "Warrior Helmet", SetName = "Warrior Set" },
            EquippedChest = new Item { Id = "2", Name = "Warrior Chestplate", SetName = "Warrior Set" },
            EquippedGloves = new Item { Id = "3", Name = "Warrior Gloves", SetName = "Warrior Set" },
            EquippedBoots = new Item { Id = "4", Name = "Warrior Boots", SetName = "Warrior Set" }
            // 4 pieces equipped
        };
        var availableSets = new List<EquipmentSet>
        {
            new EquipmentSet
            {
                Name = "Warrior Set",
                Bonuses = new Dictionary<int, SetBonus>
                {
                    { 2, new SetBonus { BonusStrength = 10, PiecesRequired = 2 } },
                    { 4, new SetBonus { BonusStrength = 25, PiecesRequired = 4 } }
                }
            }
        };

        // Act
        var bonuses = character.GetSetBonuses(availableSets, "Strength");

        // Assert
        bonuses.Should().HaveCount(2);
        bonuses["Warrior Set (2 pieces)"].Should().Be(10);
        bonuses["Warrior Set (4 pieces)"].Should().Be(25);
    }

    [Fact]
    public void GetSetBonuses_Should_Handle_All_Stat_Types()
    {
        // Arrange
        var character = new Character
        {
            EquippedHelmet = new Item { Id = "1", Name = "Divine Helmet", SetName = "Divine Set" },
            EquippedChest = new Item { Id = "2", Name = "Divine Armor", SetName = "Divine Set" }
        };
        var availableSets = new List<EquipmentSet>
        {
            new EquipmentSet
            {
                Name = "Divine Set",
                Bonuses = new Dictionary<int, SetBonus>
                {
                    { 2, new SetBonus
                        {
                            BonusStrength = 5,
                            BonusDexterity = 6,
                            BonusConstitution = 7,
                            BonusIntelligence = 8,
                            BonusWisdom = 9,
                            BonusCharisma = 10,
                            PiecesRequired = 2
                        }
                    }
                }
            }
        };

        // Act & Assert
        character.GetSetBonuses(availableSets, "Strength")["Divine Set (2 pieces)"].Should().Be(5);
        character.GetSetBonuses(availableSets, "Dexterity")["Divine Set (2 pieces)"].Should().Be(6);
        character.GetSetBonuses(availableSets, "Constitution")["Divine Set (2 pieces)"].Should().Be(7);
        character.GetSetBonuses(availableSets, "Intelligence")["Divine Set (2 pieces)"].Should().Be(8);
        character.GetSetBonuses(availableSets, "Wisdom")["Divine Set (2 pieces)"].Should().Be(9);
        character.GetSetBonuses(availableSets, "Charisma")["Divine Set (2 pieces)"].Should().Be(10);
    }

    [Fact]
    public void GetSetBonuses_Should_Ignore_Unknown_Stat_Types()
    {
        // Arrange
        var character = new Character
        {
            EquippedHelmet = new Item { Id = "1", Name = "Warrior Helmet", SetName = "Warrior Set" },
            EquippedChest = new Item { Id = "2", Name = "Warrior Chestplate", SetName = "Warrior Set" }
        };
        var availableSets = new List<EquipmentSet>
        {
            new EquipmentSet
            {
                Name = "Warrior Set",
                Bonuses = new Dictionary<int, SetBonus>
                {
                    { 2, new SetBonus { BonusStrength = 10, PiecesRequired = 2 } }
                }
            }
        };

        // Act
        var bonuses = character.GetSetBonuses(availableSets, "UnknownStat");

        // Assert
        bonuses.Should().BeEmpty();
    }

    #endregion
}
