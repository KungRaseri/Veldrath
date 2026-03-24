using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Features.Enchanting;
using RealmEngine.Core.Features.Enchanting.Queries;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Enchanting;

/// <summary>
/// Unit tests for <see cref="GetEnchantmentsHandler"/> and <see cref="GetEnchantmentCostHandler"/>.
/// </summary>
[Trait("Category", "Feature")]
public class GetEnchantmentsHandlerTests
{
    private static GetEnchantmentsHandler CreateHandler() =>
        new(new EnchantingService(NullLogger<EnchantingService>.Instance));

    private static Item MakeItem(ItemRarity rarity = ItemRarity.Common, string name = "Sword") =>
        new() { Name = name, Rarity = rarity, Type = ItemType.Weapon };

    private static Enchantment MakeEnchantment(string enchantName = "Fire Damage") =>
        new()
        {
            Name = enchantName,
            Description = $"Adds {enchantName}",
            Rarity = EnchantmentRarity.Lesser,
            Position = EnchantmentPosition.Suffix,
            Traits = []
        };

    [Fact]
    public async Task Handle_ReturnsSuccess_ForItemWithNoEnchantments()
    {
        var item = MakeItem();
        var result = await CreateHandler().Handle(new GetEnchantmentsQuery(item, 0), default);

        result.Success.Should().BeTrue();
        result.PlayerEnchantments.Should().BeEmpty();
        result.InherentEnchantments.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsCorrectItemName()
    {
        var item = MakeItem(name: "Flame Sword");
        var result = await CreateHandler().Handle(new GetEnchantmentsQuery(item, 0), default);

        result.ItemName.Should().Be("Flame Sword");
    }

    [Fact]
    public async Task Handle_MapsPlayerEnchantments_ToSlotInfo()
    {
        var item = MakeItem(ItemRarity.Rare);
        item.MaxPlayerEnchantments = 1;
        item.PlayerEnchantments.Add(MakeEnchantment("Fire Damage"));

        var result = await CreateHandler().Handle(new GetEnchantmentsQuery(item, 0), default);

        result.PlayerEnchantments.Should().ContainSingle(s => s.Name == "Fire Damage");
        result.PlayerEnchantments[0].Index.Should().Be(0);
    }

    [Fact]
    public async Task Handle_MapsInherentEnchantments_ToSlotInfo()
    {
        var item = MakeItem(ItemRarity.Rare);
        item.Enchantments.Add(MakeEnchantment("Frost Aura"));

        var result = await CreateHandler().Handle(new GetEnchantmentsQuery(item, 0), default);

        result.InherentEnchantments.Should().ContainSingle(s => s.Name == "Frost Aura");
    }

    [Fact]
    public async Task Handle_ReturnsMaxPossibleSlots_ByRarity()
    {
        var legendaryItem = MakeItem(ItemRarity.Legendary);
        var result = await CreateHandler().Handle(new GetEnchantmentsQuery(legendaryItem, 0), default);

        result.MaxPossibleSlots.Should().Be(3);
    }

    [Fact]
    public async Task Handle_ReturnsCanEnchant_WhenSlotsAvailable()
    {
        var item = MakeItem(ItemRarity.Rare);
        item.MaxPlayerEnchantments = 1; // slot available

        var result = await CreateHandler().Handle(new GetEnchantmentsQuery(item, 0), default);

        result.CanEnchant.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ReturnsCannotEnchant_WhenNoSlots()
    {
        var item = MakeItem(ItemRarity.Common);
        item.MaxPlayerEnchantments = 1;
        item.PlayerEnchantments.Add(MakeEnchantment("Fire Damage")); // slot filled

        var result = await CreateHandler().Handle(new GetEnchantmentsQuery(item, 0), default);

        result.CanEnchant.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_IncludesRateSummary()
    {
        var item = MakeItem(ItemRarity.Rare);
        item.MaxPlayerEnchantments = 2;

        var result = await CreateHandler().Handle(new GetEnchantmentsQuery(item, 0), default);

        result.RateSummary.Should().NotBeNull();
    }
}

/// <summary>
/// Unit tests for <see cref="GetEnchantmentCostHandler"/>.
/// </summary>
[Trait("Category", "Feature")]
public class GetEnchantmentCostHandlerTests
{
    private static GetEnchantmentCostHandler CreateHandler() =>
        new(new EnchantingService(NullLogger<EnchantingService>.Instance));

    private static Item MakeItem(ItemRarity rarity = ItemRarity.Common, int maxSlots = 0) =>
        new() { Name = "Sword", Rarity = rarity, Type = ItemType.Weapon, MaxPlayerEnchantments = maxSlots };

    private static Enchantment MakeEnchantment(string name = "Fire Damage") =>
        new()
        {
            Name = name,
            Description = name,
            Rarity = EnchantmentRarity.Lesser,
            Position = EnchantmentPosition.Suffix,
            Traits = []
        };

    // ApplyEnchantment
    [Fact]
    public async Task Handle_Apply_IsPossible_WhenSlotAvailable()
    {
        var item = MakeItem(ItemRarity.Common, maxSlots: 1);
        var result = await CreateHandler().Handle(new GetEnchantmentCostQuery(item, EnchantmentOperationType.ApplyEnchantment, 0), default);

        result.Success.Should().BeTrue();
        result.IsPossible.Should().BeTrue();
        result.RequiredConsumable.Should().Be("Enchantment Scroll");
    }

    [Fact]
    public async Task Handle_Apply_IsNotPossible_WhenNoSlotsAvailable()
    {
        var item = MakeItem(ItemRarity.Common, maxSlots: 1);
        item.PlayerEnchantments.Add(MakeEnchantment()); // fill the only slot

        var result = await CreateHandler().Handle(new GetEnchantmentCostQuery(item, EnchantmentOperationType.ApplyEnchantment, 0), default);

        result.Success.Should().BeTrue();
        result.IsPossible.Should().BeFalse();
        result.BlockedReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_Apply_FirstSlot_Returns100SuccessRate()
    {
        var item = MakeItem(ItemRarity.Rare, maxSlots: 2); // no enchantments yet → slot 1
        var result = await CreateHandler().Handle(new GetEnchantmentCostQuery(item, EnchantmentOperationType.ApplyEnchantment, 0), default);

        result.SuccessRate.Should().Be(100.0);
    }

    // RemoveEnchantment
    [Fact]
    public async Task Handle_Remove_IsNotPossible_WhenNoPlayerEnchantments()
    {
        var item = MakeItem();
        var result = await CreateHandler().Handle(new GetEnchantmentCostQuery(item, EnchantmentOperationType.RemoveEnchantment), default);

        result.Success.Should().BeTrue();
        result.IsPossible.Should().BeFalse();
        result.RequiredConsumable.Should().Be("Removal Scroll");
    }

    [Fact]
    public async Task Handle_Remove_IsPossible_WhenPlayerEnchantmentExists()
    {
        var item = MakeItem(ItemRarity.Rare, maxSlots: 1);
        item.PlayerEnchantments.Add(MakeEnchantment("Fire"));

        var result = await CreateHandler().Handle(new GetEnchantmentCostQuery(item, EnchantmentOperationType.RemoveEnchantment), default);

        result.Success.Should().BeTrue();
        result.IsPossible.Should().BeTrue();
        result.SuccessRate.Should().Be(100.0);
    }

    // UnlockSlot
    [Fact]
    public async Task Handle_UnlockSlot_IsPossible_WithSufficientSkill()
    {
        var item = MakeItem(ItemRarity.Rare, maxSlots: 0); // no slots → next slot is 1 (requires skill 0)
        var result = await CreateHandler().Handle(new GetEnchantmentCostQuery(item, EnchantmentOperationType.UnlockSlot, 0), default);

        result.Success.Should().BeTrue();
        result.IsPossible.Should().BeTrue();
        result.RequiredConsumable.Should().Be("Socket Crystal");
    }

    [Fact]
    public async Task Handle_UnlockSlot_IsNotPossible_WhenAtMaxSlots()
    {
        var item = MakeItem(ItemRarity.Common, maxSlots: 1); // Common max is 1
        var result = await CreateHandler().Handle(new GetEnchantmentCostQuery(item, EnchantmentOperationType.UnlockSlot, 0), default);

        result.Success.Should().BeTrue();
        result.IsPossible.Should().BeFalse();
    }
}
