using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Features.Upgrading;
using RealmEngine.Core.Features.Upgrading.Queries;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Upgrading;

/// <summary>
/// Unit tests for <see cref="GetUpgradePreviewHandler"/>.
/// </summary>
[Trait("Category", "Feature")]
public class GetUpgradePreviewHandlerTests
{
    private static GetUpgradePreviewHandler CreateHandler() =>
        new(new UpgradeService(NullLogger<UpgradeService>.Instance));

    private static Item MakeWeapon(int upgradeLevel = 0, ItemRarity rarity = ItemRarity.Common) =>
        new()
        {
            Name = "Iron Sword",
            Rarity = rarity,
            Type = ItemType.Weapon,
            UpgradeLevel = upgradeLevel,
            Traits = new Dictionary<string, TraitValue>
            {
                ["Damage"] = new TraitValue(20, TraitType.Number)
            }
        };

    [Fact]
    public async Task Handle_ReturnsSuccess_ForUpgradableItem()
    {
        var item = MakeWeapon(upgradeLevel: 0);
        var result = await CreateHandler().Handle(new GetUpgradePreviewQuery(item), default);

        result.Success.Should().BeTrue();
        result.ItemName.Should().Be("Iron Sword");
    }

    [Fact]
    public async Task Handle_ReturnsCanUpgrade_WhenBelowMaxLevel()
    {
        var item = MakeWeapon(upgradeLevel: 3);
        var result = await CreateHandler().Handle(new GetUpgradePreviewQuery(item), default);

        result.CanUpgrade.Should().BeTrue();
        result.NextLevelPreview.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ReturnsCannotUpgrade_WhenAtMaxLevel()
    {
        var item = MakeWeapon(upgradeLevel: 10); // max is +10
        var result = await CreateHandler().Handle(new GetUpgradePreviewQuery(item), default);

        result.Success.Should().BeTrue();
        result.CanUpgrade.Should().BeFalse();
        result.NextLevelPreview.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ReturnsRemainingLevels_ForPartiallyUpgradedItem()
    {
        var item = MakeWeapon(upgradeLevel: 7, rarity: ItemRarity.Legendary); // Legendary max = +10
        var result = await CreateHandler().Handle(new GetUpgradePreviewQuery(item), default);

        // Levels 8, 9, 10 remaining
        result.RemainingLevels.Should().HaveCount(3);
        result.RemainingLevels.Should().AllSatisfy(l => l.Level.Should().BeGreaterThan(7));
    }

    [Fact]
    public async Task Handle_ReturnsEmptyRemainingLevels_WhenAtMaxLevel()
    {
        var item = MakeWeapon(upgradeLevel: 10);
        var result = await CreateHandler().Handle(new GetUpgradePreviewQuery(item), default);

        result.RemainingLevels.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MarksSafeZone_ForLevels1Through5()
    {
        var item = MakeWeapon(upgradeLevel: 0);
        var result = await CreateHandler().Handle(new GetUpgradePreviewQuery(item), default);

        result.NextLevelPreview!.IsSafeZone.Should().BeTrue(); // upgrading from +0 to +1
    }

    [Fact]
    public async Task Handle_MarksNotSafeZone_ForLevelsBeyond5()
    {
        var item = MakeWeapon(upgradeLevel: 5, rarity: ItemRarity.Legendary); // Legendary max = +10, so +6 is reachable
        var result = await CreateHandler().Handle(new GetUpgradePreviewQuery(item), default);

        result.NextLevelPreview!.IsSafeZone.Should().BeFalse(); // +5 → +6 is risky
    }

    [Fact]
    public async Task Handle_ReturnsMessageWithMaxUpgrade_WhenAtMaxLevel()
    {
        var item = MakeWeapon(upgradeLevel: 10);
        var result = await CreateHandler().Handle(new GetUpgradePreviewQuery(item), default);

        result.Message.Should().Contain("maximum");
    }

    [Fact]
    public async Task Handle_RemainingLevels_ContainCorrectSuccessRates()
    {
        var item = MakeWeapon(upgradeLevel: 4, rarity: ItemRarity.Legendary); // Legendary max = +10
        var result = await CreateHandler().Handle(new GetUpgradePreviewQuery(item), default);

        // +5 should be 100% (safe zone), +6 should be 95%
        result.RemainingLevels.First(l => l.Level == 5).SuccessRate.Should().Be(100.0);
        result.RemainingLevels.First(l => l.Level == 6).SuccessRate.Should().Be(95.0);
    }
}
