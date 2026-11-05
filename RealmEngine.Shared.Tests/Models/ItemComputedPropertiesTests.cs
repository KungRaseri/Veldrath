using FluentAssertions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Tests.Models;

/// <summary>
/// Behaviour tests for the derived and compositional members of <see cref="Item"/>:
/// trait merging across every enhancement source, display-name composition, socket
/// reporting, upgrade ceilings, binding and stacking rules.
/// </summary>
[Trait("Category", "Unit")]
public class ItemComputedPropertiesTests
{
    private static Item NewItem(string name = "Longsword") => new()
    {
        Name = name,
        BaseName = name,
        Slug = name.ToLowerInvariant(),
    };

    private static Socket SocketWith(ISocketable? content, SocketType type = SocketType.Gem) =>
        new() { Type = type, Content = content };

    private static Enchantment EnchantmentWith(string name, params (string Key, TraitValue Value)[] traits)
    {
        var enchantment = new Enchantment { Name = name };
        foreach (var (key, value) in traits)
        {
            enchantment.Traits[key] = value;
        }
        return enchantment;
    }

    // ── GetTotalTraits / GetTotalTrait ────────────────────────────────────────

    [Fact]
    public void GetTotalTraits_WithNoSources_ReturnsEmptyDictionary()
    {
        // Act
        var traits = NewItem().GetTotalTraits();

        // Assert
        traits.Should().BeEmpty();
    }

    [Fact]
    public void GetTotalTraits_CopiesBaseTraitsIntoTheResult()
    {
        // Arrange
        var item = NewItem();
        item.Traits["Strength"] = new TraitValue(4, TraitType.Number);
        item.Traits["Flavour"] = new TraitValue("warm", TraitType.String);

        // Act
        var traits = item.GetTotalTraits();

        // Assert
        traits.Should().HaveCount(2);
        traits["Strength"].AsInt().Should().Be(4);
        traits["Flavour"].AsString().Should().Be("warm");
    }

    [Fact]
    public void GetTotalTraits_AddsNumericMaterialTraitsToTheBaseValue()
    {
        // Arrange
        var item = NewItem();
        item.Traits["Strength"] = new TraitValue(4, TraitType.Number);
        item.MaterialTraits["Strength"] = new TraitValue(3, TraitType.Number);

        // Act
        var result = item.GetTotalTraits();

        // Assert
        result["Strength"].AsInt().Should().Be(7);
    }

    [Fact]
    public void GetTotalTraits_MaterialTextTraitOverridesTheBaseTrait()
    {
        // Arrange
        var item = NewItem();
        item.Traits["Flavour"] = new TraitValue("plain", TraitType.String);
        item.MaterialTraits["Flavour"] = new TraitValue("mithril", TraitType.String);

        // Act
        var result = item.GetTotalTraits();

        // Assert
        result["Flavour"].AsString().Should().Be("mithril");
    }

    [Fact]
    public void GetTotalTraits_AddsMaterialTraitsThatTheBaseItemDoesNotHave()
    {
        // Arrange
        var item = NewItem();
        item.MaterialTraits["weight"] = new TraitValue(2.5, TraitType.Number);

        // Act
        var result = item.GetTotalTraits();

        // Assert
        result.Should().ContainKey("weight");
        result["weight"].AsDouble().Should().Be(2.5);
    }

    [Fact]
    public void GetTotalTraits_SumsTraitsFromMultipleEnchantments()
    {
        // Arrange
        var item = NewItem();
        item.Traits["FireDamage"] = new TraitValue(2, TraitType.Number);
        item.Enchantments.Add(EnchantmentWith("Flaming", ("FireDamage", new TraitValue(3, TraitType.Number))));
        item.Enchantments.Add(EnchantmentWith("Scorching", ("FireDamage", new TraitValue(5, TraitType.Number))));

        // Act
        var result = item.GetTotalTraits();

        // Assert
        result["FireDamage"].AsInt().Should().Be(10);
    }

    [Fact]
    public void GetTotalTraits_LastEnchantmentWinsForTextTraits()
    {
        // Arrange
        var item = NewItem();
        item.Traits["Element"] = new TraitValue("physical", TraitType.String);
        item.Enchantments.Add(EnchantmentWith("Flaming", ("Element", new TraitValue("fire", TraitType.String))));
        item.Enchantments.Add(EnchantmentWith("Freezing", ("Element", new TraitValue("ice", TraitType.String))));

        // Act
        var result = item.GetTotalTraits();

        // Assert
        result["Element"].AsString().Should().Be("ice");
    }

    [Fact]
    public void GetTotalTraits_IncludesPlayerAppliedEnchantments()
    {
        // Arrange
        var item = NewItem();
        item.Traits["Strength"] = new TraitValue(1, TraitType.Number);
        item.PlayerEnchantments.Add(EnchantmentWith("of the Bear", ("Strength", new TraitValue(6, TraitType.Number))));

        // Act
        var result = item.GetTotalTraits();

        // Assert
        result["Strength"].AsInt().Should().Be(7);
    }

    [Fact]
    public void GetTotalTraits_IncludesSocketContentTraits_andIgnoresEmptySockets()
    {
        // Arrange
        var item = NewItem();
        item.Traits["CritChance"] = new TraitValue(1, TraitType.Number);
        var gem = new Gem { Name = "Ruby" };
        gem.Traits["CritChance"] = new TraitValue(4, TraitType.Number);

        item.Sockets[SocketType.Gem] =
        [
            SocketWith(gem),
            SocketWith(null),
        ];

        // Act
        var result = item.GetTotalTraits();

        // Assert
        result["CritChance"].AsInt().Should().Be(5);
    }

    [Fact]
    public void GetTotalTraits_UpgradeLevelAddsTwoPerLevelToEveryNumericTraitOnly()
    {
        // Arrange
        var item = NewItem();
        item.UpgradeLevel = 3;
        item.Traits["Strength"] = new TraitValue(10, TraitType.Number);
        item.Traits["Element"] = new TraitValue("fire", TraitType.String);
        item.Traits["Cursed"] = new TraitValue(true, TraitType.Boolean);

        // Act
        var result = item.GetTotalTraits();

        // Assert
        result["Strength"].AsInt().Should().Be(16); // 10 + (3 * 2)
        result["Element"].AsString().Should().Be("fire");
        result["Cursed"].AsBool().Should().BeTrue();
    }

    [Fact]
    public void GetTotalTrait_ReturnsConfiguredDefault_WhenTraitIsMissing()
    {
        // Arrange
        var item = NewItem();

        // Act
        var value = item.GetTotalTrait("Strength", 42);

        // Assert
        value.Should().Be(42);
    }

    [Fact]
    public void GetTotalTrait_MergesEveryEnhancementSource()
    {
        // Arrange
        var item = NewItem();
        item.UpgradeLevel = 1;
        item.Traits["Strength"] = new TraitValue(2, TraitType.Number);
        item.MaterialTraits["Strength"] = new TraitValue(1, TraitType.Number);
        item.Enchantments.Add(EnchantmentWith("Herculean", ("Strength", new TraitValue(3, TraitType.Number))));
        item.PlayerEnchantments.Add(EnchantmentWith("of the Bear", ("Strength", new TraitValue(4, TraitType.Number))));

        var crystal = new Crystal { Name = "Might Crystal" };
        crystal.Traits["Strength"] = new TraitValue(5, TraitType.Number);
        item.Sockets[SocketType.Crystal] = [SocketWith(crystal, SocketType.Crystal)];

        // Act
        var total = item.GetTotalTrait("Strength");

        // Assert
        total.Should().Be(17); // (2+1+3+4+5) + 2 upgrade bonus
    }

    // ── Names ────────────────────────────────────────────────────────────────

    [Fact]
    public void GetDisplayName_AppendsPlayerEnchantmentsInSquareBrackets()
    {
        // Arrange
        var item = NewItem("Longsword");
        item.PlayerEnchantments.Add(EnchantmentWith("of the Fox"));

        // Act
        var name = item.GetDisplayName();

        // Assert
        name.Should().Be("Longsword [of the Fox]");
    }

    [Fact]
    public void GetDisplayName_OrdersUpgradeNameAndBothEnchantmentKinds()
    {
        // Arrange
        var item = NewItem("Longsword");
        item.UpgradeLevel = 2;
        item.Enchantments.Add(EnchantmentWith("Flaming"));
        item.PlayerEnchantments.Add(EnchantmentWith("of the Bear"));

        // Act
        var name = item.GetDisplayName();

        // Assert
        name.Should().Be("+2 Longsword (Flaming) [of the Bear]");
    }

    [Fact]
    public void GetFullDisplayName_IncludesQualityMaterialPrefixBaseAndSuffix()
    {
        // Arrange
        var item = NewItem("Longsword");
        item.Quality = new ItemQuality { Name = "Fine" };
        item.Material = new ItemMaterial { Name = "Iron" };
        item.PrefixComponents.Add(new ItemPrefix { Name = "Flaming" });
        item.PrefixComponents.Add(new ItemPrefix { Name = "Sharp" });
        item.SuffixComponents.Add(new ItemSuffix { Name = "of the Bear" });
        item.SuffixComponents.Add(new ItemSuffix { Name = "of the Fox" });

        // Act
        var name = item.GetFullDisplayName();

        // Assert
        name.Should().Be("Fine Iron Flaming Longsword of the Bear");
    }

    [Fact]
    public void GetFullDisplayName_WithNoComponents_ReturnsOnlyTheBaseName()
    {
        // Arrange
        var item = NewItem("Longsword");

        // Act
        var name = item.GetFullDisplayName();

        // Assert
        name.Should().Be("Longsword");
    }

    [Fact]
    public void GetShortName_OmitsQualityAndMaterial()
    {
        // Arrange
        var item = NewItem("Longsword");
        item.Quality = new ItemQuality { Name = "Fine" };
        item.Material = new ItemMaterial { Name = "Iron" };
        item.PrefixComponents.Add(new ItemPrefix { Name = "Flaming" });
        item.SuffixComponents.Add(new ItemSuffix { Name = "of the Bear" });

        // Act
        var name = item.GetShortName();

        // Assert
        name.Should().Be("Flaming Longsword of the Bear");
    }

    [Fact]
    public void GetShortName_WithNoComponents_ReturnsOnlyTheBaseName()
    {
        // Act
        var name = NewItem("Longsword").GetShortName();

        // Assert
        name.Should().Be("Longsword");
    }

    // ── Sockets ──────────────────────────────────────────────────────────────

    [Fact]
    public void GetSocketsInfo_WithNoSockets_ReturnsEmptyList()
    {
        // Act
        var info = NewItem().GetSocketsInfo();

        // Assert
        info.Should().BeEmpty();
    }

    [Fact]
    public void GetSocketsInfo_SkipsSocketTypesWithNoSockets()
    {
        // Arrange
        var item = NewItem();
        item.Sockets[SocketType.Gem] = [SocketWith(new Gem { Name = "Ruby" })];
        item.Sockets[SocketType.Rune] = [];

        // Act
        var info = item.GetSocketsInfo();

        // Assert
        info.Should().ContainSingle();
        info[0].Type.Should().Be(SocketType.Gem);
    }

    [Fact]
    public void GetSocketsInfo_CountsFilledAndTotalSockets_andKeepsTheSocketInstances()
    {
        // Arrange
        var item = NewItem();
        var filled = SocketWith(new Gem { Name = "Ruby" });
        var empty = SocketWith(null);
        item.Sockets[SocketType.Gem] = [filled, empty];

        // Act
        var info = item.GetSocketsInfo();

        // Assert
        info[0].FilledCount.Should().Be(1);
        info[0].TotalCount.Should().Be(2);
        info[0].Sockets.Should().BeEquivalentTo([filled, empty]);
    }

    [Fact]
    public void GetSocketsInfo_OrdersSocketTypesByEnumValue()
    {
        // Arrange
        var item = NewItem();
        item.Sockets[SocketType.Orb] = [SocketWith(new Orb { Name = "Strike" }, SocketType.Orb)];
        item.Sockets[SocketType.Gem] = [SocketWith(new Gem { Name = "Ruby" })];

        // Act
        var info = item.GetSocketsInfo();

        // Assert
        info.Select(i => i.Type).Should().ContainInOrder(SocketType.Gem, SocketType.Orb);
    }

    [Fact]
    public void GetSocketsDisplayText_WithNoSockets_ReturnsEmptyString()
    {
        // Act
        var text = NewItem().GetSocketsDisplayText();

        // Assert
        text.Should().BeEmpty();
    }

    [Fact]
    public void GetSocketsDisplayText_JoinsEachSocketTypeWithPipes()
    {
        // Arrange
        var item = NewItem();
        item.Sockets[SocketType.Gem] = [SocketWith(new Gem { Name = "Ruby" }), SocketWith(null)];
        item.Sockets[SocketType.Rune] = [SocketWith(new Rune { Name = "Fury" }, SocketType.Rune)];

        // Act
        var text = item.GetSocketsDisplayText();

        // Assert
        text.Should().Be("Gem: 1/2 | Rune: 1/1");
    }

    // ── Player enchantment slots ─────────────────────────────────────────────

    [Fact]
    public void CanAddPlayerEnchantment_IsTrueWhileBelowTheLimit()
    {
        // Arrange
        var item = NewItem();
        item.MaxPlayerEnchantments = 2;
        item.PlayerEnchantments.Add(EnchantmentWith("first"));

        // Act & Assert
        item.CanAddPlayerEnchantment().Should().BeTrue();
    }

    [Fact]
    public void CanAddPlayerEnchantment_IsFalseWhenTheLimitIsReached()
    {
        // Arrange
        var item = NewItem();
        item.MaxPlayerEnchantments = 1;
        item.PlayerEnchantments.Add(EnchantmentWith("first"));

        // Act & Assert
        item.CanAddPlayerEnchantment().Should().BeFalse();
    }

    [Fact]
    public void HasPlayerEnchantmentSlots_ReflectsTheMaxSlotCount()
    {
        // Arrange
        var none = NewItem();
        none.MaxPlayerEnchantments = 0;
        var some = NewItem();

        // Act & Assert
        none.HasPlayerEnchantmentSlots().Should().BeFalse();
        some.HasPlayerEnchantmentSlots().Should().BeTrue(); // default is 1
    }

    [Fact]
    public void AvailablePlayerEnchantmentSlots_SubtractsUsedSlots()
    {
        // Arrange
        var item = NewItem();
        item.MaxPlayerEnchantments = 3;
        item.PlayerEnchantments.Add(EnchantmentWith("first"));
        item.PlayerEnchantments.Add(EnchantmentWith("second"));

        // Act
        var available = item.AvailablePlayerEnchantmentSlots();

        // Assert
        available.Should().Be(1);
    }

    [Fact]
    public void AvailablePlayerEnchantmentSlots_GoesNegativeIfTheCapIsLoweredBelowUsage()
    {
        // Arrange
        var item = NewItem();
        item.MaxPlayerEnchantments = 1;
        item.PlayerEnchantments.Add(EnchantmentWith("first"));
        item.PlayerEnchantments.Add(EnchantmentWith("second"));

        // Act
        var available = item.AvailablePlayerEnchantmentSlots();

        // Assert
        available.Should().Be(-1);
    }

    // ── Upgrade ceiling ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(ItemRarity.Common, 5)]
    [InlineData(ItemRarity.Uncommon, 5)]
    [InlineData(ItemRarity.Rare, 7)]
    [InlineData(ItemRarity.Epic, 9)]
    [InlineData(ItemRarity.Legendary, 10)]
    public void GetMaxUpgradeLevel_MapsRarityToCeiling(ItemRarity rarity, int expected)
    {
        // Arrange
        var item = NewItem();
        item.Rarity = rarity;

        // Act & Assert
        item.GetMaxUpgradeLevel().Should().Be(expected);
    }

    [Fact]
    public void CanUpgrade_IsTrueBelowTheCeilingAndFalseAtIt()
    {
        // Arrange
        var below = NewItem();
        below.Rarity = ItemRarity.Common;
        below.UpgradeLevel = 4;

        var atCeiling = NewItem();
        atCeiling.Rarity = ItemRarity.Common;
        atCeiling.UpgradeLevel = 5;

        // Act & Assert
        below.CanUpgrade().Should().BeTrue();
        atCeiling.CanUpgrade().Should().BeFalse();
    }

    // ── Binding ──────────────────────────────────────────────────────────────

    [Fact]
    public void BindToCharacter_SetsBoundFlagAndOwner()
    {
        // Arrange
        var item = NewItem();

        // Act
        item.BindToCharacter("Aria");

        // Assert
        item.IsBound.Should().BeTrue();
        item.BoundToCharacter.Should().Be("Aria");
    }

    [Fact]
    public void Binding_DefaultsToUnbound()
    {
        // Act
        var item = NewItem();

        // Assert
        item.Binding.Should().Be(BindingType.Unbound);
        item.IsBound.Should().BeFalse();
        item.BoundToCharacter.Should().BeNull();
    }

    // ── Stacking ─────────────────────────────────────────────────────────────

    private static Item Stackable(string name = "Health Potion") => new()
    {
        Name = name,
        Type = ItemType.Consumable,
        IsStackable = true,
        StackSize = 20,
    };

    [Fact]
    public void CanStackWith_RequiresBothItemsToBeStackable()
    {
        // Arrange
        var stackable = Stackable();
        var notStackable = new Item { Name = "Health Potion", Type = ItemType.Consumable, IsStackable = false };

        // Act & Assert
        stackable.CanStackWith(notStackable).Should().BeFalse();
        notStackable.CanStackWith(stackable).Should().BeFalse();
    }

    [Fact]
    public void CanStackWith_RequiresMatchingNameAndType()
    {
        // Arrange
        var potion = Stackable();
        var otherName = Stackable("Mana Potion");
        var otherType = Stackable();
        otherType.Type = ItemType.Material;

        // Act & Assert
        potion.CanStackWith(otherName).Should().BeFalse();
        potion.CanStackWith(otherType).Should().BeFalse();
    }

    [Fact]
    public void CanStackWith_IsTrueForIdenticalPlainStacks()
    {
        // Arrange
        var left = Stackable();
        var right = Stackable();

        // Act & Assert
        left.CanStackWith(right).Should().BeTrue();
    }

    [Fact]
    public void CanStackWith_RejectsItemsWithPlayerEnchantments()
    {
        // Arrange
        var left = Stackable();
        var right = Stackable();
        right.PlayerEnchantments.Add(EnchantmentWith("of the Fox"));

        // Act & Assert
        left.CanStackWith(right).Should().BeFalse();
        right.CanStackWith(left).Should().BeFalse();
    }

    [Fact]
    public void CanStackWith_RejectsItemsWithSockets()
    {
        // Arrange
        var left = Stackable();
        var right = Stackable();
        right.Sockets[SocketType.Gem] = [SocketWith(null)];

        // Act & Assert
        left.CanStackWith(right).Should().BeFalse();
        right.CanStackWith(left).Should().BeFalse();
    }

    [Fact]
    public void CanStackWith_RejectsUpgradedItems()
    {
        // Arrange
        var left = Stackable();
        var right = Stackable();
        right.UpgradeLevel = 1;

        // Act & Assert
        left.CanStackWith(right).Should().BeFalse();
        right.CanStackWith(left).Should().BeFalse();
    }

    [Fact]
    public void CanStackWith_RejectsItemsMadeFromDifferentMaterials()
    {
        // Arrange
        var left = Stackable();
        var right = Stackable();
        right.Material = new ItemMaterial { Name = "Iron" };

        // Act & Assert
        left.CanStackWith(right).Should().BeFalse();
    }

    [Fact]
    public void CanStackWith_AcceptsItemsMadeFromTheSameMaterial()
    {
        // Arrange
        var left = Stackable();
        left.Material = new ItemMaterial { Name = "Glass" };
        var right = Stackable();
        right.Material = left.Material;

        // Act & Assert
        left.CanStackWith(right).Should().BeTrue();
    }

    // ── Quantity ─────────────────────────────────────────────────────────────

    [Fact]
    public void AddQuantity_IncreasesTheStack()
    {
        // Arrange
        var item = Stackable();
        item.Quantity = 5;

        // Act
        item.AddQuantity(3);

        // Assert
        item.Quantity.Should().Be(8);
    }

    [Fact]
    public void AddQuantity_OnNonStackableItem_Throws()
    {
        // Arrange
        var item = new Item { Name = "Iron Sword" };

        // Act
        var act = () => item.AddQuantity(1);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*Iron Sword*");
    }

    [Fact]
    public void RemoveQuantity_DecreasesTheStackAndReturnsTrue()
    {
        // Arrange
        var item = Stackable();
        item.Quantity = 5;

        // Act
        var removed = item.RemoveQuantity(2);

        // Assert
        removed.Should().BeTrue();
        item.Quantity.Should().Be(3);
    }

    [Fact]
    public void RemoveQuantity_MoreThanHeld_ReturnsFalseAndLeavesTheStackUntouched()
    {
        // Arrange
        var item = Stackable();
        item.Quantity = 2;

        // Act
        var removed = item.RemoveQuantity(3);

        // Assert
        removed.Should().BeFalse();
        item.Quantity.Should().Be(2);
    }

    [Fact]
    public void RemoveQuantity_OnNonStackableItem_Throws()
    {
        // Arrange
        var item = new Item { Name = "Iron Sword" };

        // Act
        var act = () => item.RemoveQuantity(1);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("Cannot remove quantity*");
    }

    // ── Tooltip ──────────────────────────────────────────────────────────────

    [Fact]
    public void GetTooltipData_DescribesTheBaseSectionAndIdentity()
    {
        // Arrange
        var item = NewItem("Longsword");
        item.Rarity = ItemRarity.Rare;
        item.Type = ItemType.Weapon;
        item.Lore = "Forged in the north.";
        item.Traits["attack"] = new TraitValue(12, TraitType.Number);

        // Act
        var tooltip = item.GetTooltipData();

        // Assert
        tooltip.DisplayName.Should().Be("Longsword");
        tooltip.Rarity.Should().Be(ItemRarity.Rare);
        tooltip.Type.Should().Be(ItemType.Weapon);
        tooltip.Lore.Should().Be("Forged in the north.");
        tooltip.BaseSection.Header.Should().Be("Longsword");
        tooltip.BaseSection.Bonuses.Should().ContainSingle().Which.Should().Be("attack: 12");
        tooltip.QualitySection.Should().BeNull();
        tooltip.MaterialSection.Should().BeNull();
        tooltip.PrefixSections.Should().BeEmpty();
        tooltip.SuffixSections.Should().BeEmpty();
        tooltip.EnchantmentSections.Should().BeEmpty();
    }

    [Fact]
    public void GetTooltipData_AddsSectionsForEveryComponentSource()
    {
        // Arrange
        var item = NewItem("Longsword");
        item.Traits["attack"] = new TraitValue(10, TraitType.Number);
        item.Traits["Flavour"] = new TraitValue("warm", TraitType.String);

        item.Quality = new ItemQuality { Name = "Fine" };
        item.Quality.Traits["accuracy"] = new TraitValue(2, TraitType.Number);

        item.Material = new ItemMaterial { Name = "Iron" };
        item.Material.Traits["durability"] = new TraitValue(50, TraitType.Number);

        var prefix = new ItemPrefix { Name = "Flaming" };
        prefix.Traits["fireDamage"] = new TraitValue(4, TraitType.Number);
        item.PrefixComponents.Add(prefix);

        var suffix = new ItemSuffix { Name = "of the Bear" };
        suffix.Traits["strength"] = new TraitValue(3, TraitType.Number);
        item.SuffixComponents.Add(suffix);

        item.Enchantments.Add(EnchantmentWith("Runed", ("mana", new TraitValue(7, TraitType.Number))));

        // Act
        var tooltip = item.GetTooltipData();

        // Assert
        tooltip.BaseSection.Bonuses.Should().HaveCount(2);
        tooltip.BaseSection.Bonuses.Should().Contain("attack: 10").And.Contain("Flavour: warm");

        tooltip.QualitySection!.Header.Should().Be("Quality: Fine");
        tooltip.QualitySection.Bonuses.Should().ContainSingle().Which.Should().Be("accuracy: 2");

        tooltip.MaterialSection!.Header.Should().Be("Material: Iron");
        tooltip.MaterialSection.Bonuses.Should().ContainSingle().Which.Should().Be("durability: 50");

        tooltip.PrefixSections.Should().ContainSingle();
        tooltip.PrefixSections[0].Header.Should().Be("Flaming");
        tooltip.PrefixSections[0].Bonuses.Should().ContainSingle().Which.Should().Be("fireDamage: 4");

        tooltip.SuffixSections.Should().ContainSingle();
        tooltip.SuffixSections[0].Header.Should().Be("of the Bear");

        tooltip.EnchantmentSections.Should().ContainSingle();
        tooltip.EnchantmentSections[0].Header.Should().Be("Enchantment: Runed");
        tooltip.EnchantmentSections[0].Bonuses.Should().ContainSingle().Which.Should().Be("mana: 7");
    }

    [Fact]
    public void GetTooltipData_UsesFullDisplayName_WhenComponentsArePresent()
    {
        // Arrange
        var item = NewItem("Longsword");
        item.Quality = new ItemQuality { Name = "Fine" };
        item.Material = new ItemMaterial { Name = "Iron" };

        // Act
        var tooltip = item.GetTooltipData();

        // Assert
        tooltip.DisplayName.Should().Be("Fine Iron Longsword");
    }

    // ── Defaults on the contained value object ───────────────────────────────

    [Fact]
    public void ItemDamage_DefaultsToASingleD4()
    {
        // Act
        var damage = new ItemDamage();

        // Assert
        damage.Min.Should().Be(1);
        damage.Max.Should().Be(4);
        damage.Modifier.Should().BeEmpty();
    }

    [Fact]
    public void Item_Defaults_StackSizeIsOneAndNotStackable()
    {
        // Act
        var item = new Item();

        // Assert
        item.Quantity.Should().Be(1);
        item.StackSize.Should().Be(1);
        item.IsStackable.Should().BeFalse();
        item.MaxPlayerEnchantments.Should().Be(1);
        item.UpgradeLevel.Should().Be(0);
        item.RequiredItems.Should().BeEmpty();
    }
}
