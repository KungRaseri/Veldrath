using FluentAssertions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Tests.Models;

[Trait("Category", "Unit")]
public class ItemTests
{
    [Fact]
    public void Item_Should_Initialize_With_Default_Values()
    {
        // Arrange & Act
        var item = new Item();

        // Assert
        item.Id.Should().NotBeNullOrEmpty();
        item.Name.Should().Be(string.Empty);
        item.Description.Should().Be(string.Empty);
        item.Price.Should().Be(0);
        item.Rarity.Should().Be(ItemRarity.Common);
        item.Type.Should().Be(ItemType.Consumable);
    }

    [Fact]
    public void Item_Should_Generate_Unique_Ids()
    {
        // Arrange & Act
        var item1 = new Item();
        var item2 = new Item();

        // Assert
        item1.Id.Should().NotBe(item2.Id);
    }

    [Fact]
    public void Item_Properties_Should_Be_Settable()
    {
        // Arrange
        var item = new Item();

        // Act
        item.Name = "Health Potion";
        item.Description = "Restores 50 HP";
        item.Price = 25;
        item.Rarity = ItemRarity.Rare;
        item.Type = ItemType.Consumable;

        // Assert
        item.Name.Should().Be("Health Potion");
        item.Description.Should().Be("Restores 50 HP");
        item.Price.Should().Be(25);
        item.Rarity.Should().Be(ItemRarity.Rare);
        item.Type.Should().Be(ItemType.Consumable);
    }

    [Theory]
    [InlineData(ItemRarity.Common)]
    [InlineData(ItemRarity.Uncommon)]
    [InlineData(ItemRarity.Rare)]
    [InlineData(ItemRarity.Epic)]
    [InlineData(ItemRarity.Legendary)]
    public void Item_Should_Accept_All_Rarity_Levels(ItemRarity rarity)
    {
        // Arrange & Act
        var item = new Item { Rarity = rarity };

        // Assert
        item.Rarity.Should().Be(rarity);
    }

    [Theory]
    [InlineData(ItemType.Weapon)]
    [InlineData(ItemType.Chest)]
    [InlineData(ItemType.Consumable)]
    [InlineData(ItemType.QuestItem)]
    [InlineData(ItemType.Ring)]
    public void Item_Should_Accept_All_itemTypes(ItemType type)
    {
        // Arrange & Act
        var item = new Item { Type = type };

        // Assert
        item.Type.Should().Be(type);
    }

    // GetTotalTrait Calculation Tests
    [Fact]
    public void GetTotalTrait_Should_Return_Base_Value_When_No_Enchantments_Or_Upgrades()
    {
        var item = new Item
        {
            Traits = new Dictionary<string, TraitValue>
            {
                { "Strength", new TraitValue(5, TraitType.Number) }
            }
        };
        item.GetTotalTrait("Strength").Should().Be(5);
    }

    [Fact]
    public void GetTotalTrait_Should_Include_Upgrade_Level_Bonus()
    {
        var item = new Item
        {
            Traits = new Dictionary<string, TraitValue>
            {
                { "Strength", new TraitValue(3, TraitType.Number) }
            },
            UpgradeLevel = 2
        };
        item.GetTotalTrait("Strength").Should().Be(7); // 3 + (2*2)
    }

    [Fact]
    public void GetTotalTrait_Should_Include_Enchantment_Bonuses()
    {
        var item = new Item
        {
            Traits = new Dictionary<string, TraitValue>
            {
                { "Strength", new TraitValue(2, TraitType.Number) }
            },
            Enchantments = new List<Enchantment>
            {
                new Enchantment
                {
                    Traits = new Dictionary<string, TraitValue>
                    {
                        { "Strength", new TraitValue(3, TraitType.Number) }
                    }
                },
                new Enchantment
                {
                    Traits = new Dictionary<string, TraitValue>
                    {
                        { "Strength", new TraitValue(5, TraitType.Number) }
                    }
                }
            }
        };
        item.GetTotalTrait("Strength").Should().Be(10); // 2 + 3 + 5
    }

    [Fact]
    public void GetTotalTrait_Should_Combine_All_Sources()
    {
        var item = new Item
        {
            Traits = new Dictionary<string, TraitValue>
            {
                { "Strength", new TraitValue(5, TraitType.Number) }
            },
            UpgradeLevel = 3,
            Enchantments = new List<Enchantment>
            {
                new Enchantment
                {
                    Traits = new Dictionary<string, TraitValue>
                    {
                        { "Strength", new TraitValue(4, TraitType.Number) }
                    }
                },
                new Enchantment
                {
                    Traits = new Dictionary<string, TraitValue>
                    {
                        { "Strength", new TraitValue(2, TraitType.Number) }
                    }
                }
            }
        };
        item.GetTotalTrait("Strength").Should().Be(17); // 5 + 6 + 4 + 2
    }

    [Fact]
    public void GetTotalTrait_Dexterity_Should_Combine_All_Sources()
    {
        var item = new Item
        {
            Traits = new Dictionary<string, TraitValue> { { "Dexterity", new TraitValue(3, TraitType.Number) } },
            UpgradeLevel = 2,
            Enchantments = new List<Enchantment>
            {
                new Enchantment
                {
                    Traits = new Dictionary<string, TraitValue> { { "Dexterity", new TraitValue(5, TraitType.Number) } }
                }
            }
        };
        item.GetTotalTrait("Dexterity").Should().Be(12); // 3 + 4 + 5
    }

    [Fact]
    public void GetTotalTrait_Constitution_Should_Combine_All_Sources()
    {
        var item = new Item
        {
            Traits = new Dictionary<string, TraitValue> { { "Constitution", new TraitValue(6, TraitType.Number) } },
            UpgradeLevel = 1,
            Enchantments = new List<Enchantment>
            {
                new Enchantment
                {
                    Traits = new Dictionary<string, TraitValue> { { "Constitution", new TraitValue(3, TraitType.Number) } }
                },
                new Enchantment
                {
                    Traits = new Dictionary<string, TraitValue> { { "Constitution", new TraitValue(1, TraitType.Number) } }
                }
            }
        };
        item.GetTotalTrait("Constitution").Should().Be(12); // 6 + 2 + 3 + 1
    }

    [Fact]
    public void GetTotalTrait_Intelligence_Should_Combine_All_Sources()
    {
        var item = new Item
        {
            Traits = new Dictionary<string, TraitValue> { { "Intelligence", new TraitValue(4, TraitType.Number) } },
            UpgradeLevel = 5,
            Enchantments = new List<Enchantment>
            {
                new Enchantment
                {
                    Traits = new Dictionary<string, TraitValue> { { "Intelligence", new TraitValue(7, TraitType.Number) } }
                }
            }
        };
        item.GetTotalTrait("Intelligence").Should().Be(21); // 4 + 10 + 7
    }

    [Fact]
    public void GetTotalTrait_Wisdom_Should_Combine_All_Sources()
    {
        var item = new Item
        {
            Traits = new Dictionary<string, TraitValue> { { "Wisdom", new TraitValue(5, TraitType.Number) } },
            UpgradeLevel = 3,
            Enchantments = new List<Enchantment>
            {
                new Enchantment
                {
                    Traits = new Dictionary<string, TraitValue> { { "Wisdom", new TraitValue(2, TraitType.Number) } }
                },
                new Enchantment
                {
                    Traits = new Dictionary<string, TraitValue> { { "Wisdom", new TraitValue(4, TraitType.Number) } }
                }
            }
        };
        item.GetTotalTrait("Wisdom").Should().Be(17); // 5 + 6 + 2 + 4
    }

    [Fact]
    public void GetTotalTrait_Charisma_Should_Combine_All_Sources()
    {
        var item = new Item
        {
            Traits = new Dictionary<string, TraitValue> { { "Charisma", new TraitValue(3, TraitType.Number) } },
            UpgradeLevel = 4,
            Enchantments = new List<Enchantment>
            {
                new Enchantment
                {
                    Traits = new Dictionary<string, TraitValue> { { "Charisma", new TraitValue(6, TraitType.Number) } }
                }
            }
        };
        item.GetTotalTrait("Charisma").Should().Be(17); // 3 + 8 + 6
    }

    // GetDisplayName Tests
    [Fact]
    public void GetDisplayName_Should_Return_Name_When_No_Upgrades_Or_Enchantments()
    {
        var item = new Item { Name = "Iron Sword" };
        item.GetDisplayName().Should().Be("Iron Sword");
    }

    [Fact]
    public void GetDisplayName_Should_Include_Upgrade_Level_Prefix()
    {
        var item = new Item { Name = "Steel Helmet", UpgradeLevel = 3 };
        item.GetDisplayName().Should().Be("+3 Steel Helmet");
    }

    [Fact]
    public void GetDisplayName_Should_Include_Enchantment_Suffixes()
    {
        var item = new Item
        {
            Name = "Leather Boots",
            Enchantments = new List<Enchantment>
            {
                new Enchantment { Name = "Swiftness" },
                new Enchantment { Name = "Stamina" }
            }
        };
        item.GetDisplayName().Should().Be("Leather Boots (Swiftness) (Stamina)");
    }

    [Fact]
    public void GetDisplayName_Should_Combine_Upgrade_And_Enchantments()
    {
        var item = new Item
        {
            Name = "Mythril Armor",
            UpgradeLevel = 5,
            Enchantments = new List<Enchantment>
            {
                new Enchantment { Name = "Fire Resistance" },
                new Enchantment { Name = "Strength" }
            }
        };
        item.GetDisplayName().Should().Be("+5 Mythril Armor (Fire Resistance) (Strength)");
    }

    // Theory Tests
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    public void Upgrade_Levels_Should_Add_Two_Per_Level(int upgradeLevel)
    {
        var item = new Item
        {
            Traits = new Dictionary<string, TraitValue>
            {
                { "Strength", new TraitValue(5, TraitType.Number) },
                { "Dexterity", new TraitValue(3, TraitType.Number) }
            },
            UpgradeLevel = upgradeLevel
        };
        item.GetTotalTrait("Strength").Should().Be(5 + (upgradeLevel * 2));
        item.GetTotalTrait("Dexterity").Should().Be(3 + (upgradeLevel * 2));
    }

    [Fact]
    public void All_Trait_Methods_Should_Handle_Empty_Enchantments()
    {
        var item = new Item
        {
            Traits = new Dictionary<string, TraitValue>
            {
                { "Strength", new TraitValue(10, TraitType.Number) },
                { "Dexterity", new TraitValue(8, TraitType.Number) },
                { "Constitution", new TraitValue(6, TraitType.Number) },
                { "Intelligence", new TraitValue(4, TraitType.Number) },
                { "Wisdom", new TraitValue(2, TraitType.Number) },
                { "Charisma", new TraitValue(1, TraitType.Number) }
            }
        };
        item.GetTotalTrait("Strength").Should().Be(10);
        item.GetTotalTrait("Dexterity").Should().Be(8);
        item.GetTotalTrait("Constitution").Should().Be(6);
        item.GetTotalTrait("Intelligence").Should().Be(4);
        item.GetTotalTrait("Wisdom").Should().Be(2);
        item.GetTotalTrait("Charisma").Should().Be(1);
    }
}
