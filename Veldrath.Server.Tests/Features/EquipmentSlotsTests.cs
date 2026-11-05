using RealmEngine.Shared.Models;
using Veldrath.Server.Features.Characters;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Unit tests for <see cref="EquipmentSlots"/>: every equippable item type must map to its canonical slot, and
/// non-equippable types must map to <see langword="null"/>.
/// </summary>
public class EquipmentSlotsTests
{
    [Theory]
    [InlineData(ItemType.Weapon, EquipmentSlots.MainHand)]
    [InlineData(ItemType.Shield, EquipmentSlots.OffHand)]
    [InlineData(ItemType.OffHand, EquipmentSlots.OffHand)]
    [InlineData(ItemType.Helmet, EquipmentSlots.Head)]
    [InlineData(ItemType.Shoulders, EquipmentSlots.Shoulders)]
    [InlineData(ItemType.Chest, EquipmentSlots.Chest)]
    [InlineData(ItemType.Bracers, EquipmentSlots.Bracers)]
    [InlineData(ItemType.Gloves, EquipmentSlots.Gloves)]
    [InlineData(ItemType.Belt, EquipmentSlots.Belt)]
    [InlineData(ItemType.Legs, EquipmentSlots.Legs)]
    [InlineData(ItemType.Boots, EquipmentSlots.Feet)]
    [InlineData(ItemType.Ring, EquipmentSlots.Ring)]
    [InlineData(ItemType.Necklace, EquipmentSlots.Amulet)]
    public void ForItemType_MapsEveryEquippableTypeToItsSlot(ItemType type, string expectedSlot)
    {
        EquipmentSlots.ForItemType(type).Should().Be(expectedSlot);
    }

    [Theory]
    [InlineData(ItemType.Consumable)]
    [InlineData(ItemType.EnchantmentScroll)]
    [InlineData(ItemType.QuestItem)]
    public void ForItemType_ReturnsNull_ForItemsThatCannotBeWorn(ItemType type)
    {
        EquipmentSlots.ForItemType(type).Should().BeNull();
    }

    [Fact]
    public void ForItem_PrefersTheItemType()
    {
        var item = new Item { Type = ItemType.Chest, TypeKey = "shield", Slug = "iron-shield" };

        EquipmentSlots.ForItem(item).Should().Be(EquipmentSlots.Chest);
    }

    [Theory]
    [InlineData("shield", null)]
    [InlineData("shields", null)]
    [InlineData(null, "shield")]
    [InlineData(null, "shields")]
    [InlineData(null, null)]
    public void ForItem_FallsBackToShieldCategoryKeys(string? typeKey, string? armorType)
    {
        var item = new Item
        {
            Type = ItemType.EnchantmentScroll,
            TypeKey = typeKey,
            ArmorType = armorType,
            Slug = "buckler",
        };

        EquipmentSlots.ForItem(item).Should().Be(
            typeKey is not null || armorType is not null
                ? EquipmentSlots.OffHand
                : (string?)null);
    }

    [Fact]
    public void ForItem_RecognisesAShieldByItsSlug()
    {
        var item = new Item { Type = ItemType.EnchantmentScroll, Slug = "oak-shield" };

        EquipmentSlots.ForItem(item).Should().Be(EquipmentSlots.OffHand);
    }

    [Fact]
    public void ForItem_ReturnsNull_ForAnItemThatIsNeitherArmourNorAShield()
    {
        var item = new Item { Type = ItemType.Consumable, Slug = "healing-potion", TypeKey = "potions" };

        EquipmentSlots.ForItem(item).Should().BeNull();
    }
}
