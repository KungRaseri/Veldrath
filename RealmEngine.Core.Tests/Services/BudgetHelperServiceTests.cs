using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Services.Budget;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Services;

[Trait("Category", "Service")]
public class BudgetHelperServiceTests
{
    // BudgetConfig defaults: EnemyLevelMultiplier=5, BossMultiplier=2.5, EliteMultiplier=1.5
    private static BudgetHelperService CreateService()
    {
        var calc = new BudgetCalculator(new BudgetConfig(), NullLogger<BudgetCalculator>.Instance);
        return new BudgetHelperService(calc, NullLogger<BudgetHelperService>.Instance);
    }

    // GetBudgetForEnemyLoot
    [Fact]
    public void GetBudgetForEnemyLoot_AppliesNoMultiplier_ForNormalEnemy()
    {
        // level=10 → baseBudget=50; multiplier=1.0
        CreateService().GetBudgetForEnemyLoot("goblin", 10).Should().Be(50);
    }

    [Theory]
    [InlineData("dungeon boss",    10, 75)]  // contains "boss"   → 1.5 × 50
    [InlineData("fire dragon",     10, 75)]  // contains "dragon" → 1.5 × 50
    [InlineData("elite knight",    10, 65)]  // contains "elite"  → 1.3 × 50
    [InlineData("veteran knight",  10, 65)]  // contains "knight" → 1.3 × 50
    public void GetBudgetForEnemyLoot_AppliesTypeMultiplier(string enemyType, int level, int expected)
    {
        CreateService().GetBudgetForEnemyLoot(enemyType, level).Should().Be(expected);
    }

    // GetBudgetForChest
    [Theory]
    [InlineData(RarityTier.Common,    10, 40)]   // 10*5 * 0.8 = 40
    [InlineData(RarityTier.Uncommon,  10, 60)]   // 10*5 * 1.2 = 60
    [InlineData(RarityTier.Rare,      10, 90)]   // 10*5 * 1.8 = 90
    [InlineData(RarityTier.Epic,      10, 125)]  // 10*5 * 2.5 = 125
    [InlineData(RarityTier.Legendary, 10, 200)]  // 10*5 * 4.0 = 200
    public void GetBudgetForChest_AppliesRarityMultiplier(RarityTier rarity, int locationLevel, int expected)
    {
        CreateService().GetBudgetForChest(rarity, locationLevel).Should().Be(expected);
    }

    [Fact]
    public void GetBudgetForChest_EnforcesMinimumBudget_WhenLevelIsLow()
    {
        // level=1, Common: 1*5 * 0.8 = 4 → below minimum of 10
        CreateService().GetBudgetForChest(RarityTier.Common, 1).Should().Be(10);
    }

    // GetBudgetForShopItem
    [Theory]
    [InlineData(100, 1, 50)]   // 100 * 0.5
    [InlineData(100, 2, 100)]  // 100 * 1.0
    [InlineData(100, 3, 200)]  // 100 * 2.0
    public void GetBudgetForShopItem_AppliesTierMultiplier(int merchantWealth, int tier, int expected)
    {
        CreateService().GetBudgetForShopItem(merchantWealth, tier).Should().Be(expected);
    }

    [Fact]
    public void GetBudgetForShopItem_EnforcesMinimumBudget_ForPoorMerchant()
    {
        // merchantWealth=10, tier=1: 10 * 0.5 = 5 → below minimum of 20
        CreateService().GetBudgetForShopItem(10, 1).Should().Be(20);
    }

    // GetCraftingSuccessChance
    [Fact]
    public void GetCraftingSuccessChance_Returns50Percent_WhenSkillEqualsRequirement()
    {
        CreateService().GetCraftingSuccessChance(50, 50).Should().Be(50);
    }

    [Fact]
    public void GetCraftingSuccessChance_Increases_WithHigherSkill()
    {
        // 50 + (70-50)*2 = 90
        CreateService().GetCraftingSuccessChance(70, 50).Should().Be(90);
    }

    [Fact]
    public void GetCraftingSuccessChance_ClampsToMinimum_WhenSkillFarBelowRequirement()
    {
        CreateService().GetCraftingSuccessChance(1, 100).Should().Be(5);
    }

    [Fact]
    public void GetCraftingSuccessChance_ClampsToMaximum_WhenSkillFarAboveRequirement()
    {
        CreateService().GetCraftingSuccessChance(200, 1).Should().Be(99);
    }

    // GetCraftingCriticalChance
    [Fact]
    public void GetCraftingCriticalChance_Returns5Percent_WhenSkillEqualsRequirement()
    {
        CreateService().GetCraftingCriticalChance(50, 50).Should().Be(5);
    }

    [Fact]
    public void GetCraftingCriticalChance_Increases_Per10SkillPoints()
    {
        // skillDiff=50 → 5 + (50/10) = 10
        CreateService().GetCraftingCriticalChance(100, 50).Should().Be(10);
    }

    [Fact]
    public void GetCraftingCriticalChance_CapsAt20Percent()
    {
        CreateService().GetCraftingCriticalChance(500, 0).Should().Be(20);
    }

    // GetCraftingQualityBonus
    [Fact]
    public void GetCraftingQualityBonus_Returns0_WhenSkillEqualsRequirement()
    {
        CreateService().GetCraftingQualityBonus(50, 50).Should().Be(0);
    }

    [Fact]
    public void GetCraftingQualityBonus_Returns1_Per10SkillAboveRequirement()
    {
        CreateService().GetCraftingQualityBonus(70, 50).Should().Be(2);
    }

    [Fact]
    public void GetCraftingQualityBonus_AddsBonusTier_OnCriticalSuccess()
    {
        // normal: (70-50)/10 = 2; +1 for critical = 3
        CreateService().GetCraftingQualityBonus(70, 50, criticalSuccess: true).Should().Be(3);
    }

    [Fact]
    public void GetCraftingQualityBonus_CapsAt3()
    {
        CreateService().GetCraftingQualityBonus(200, 0).Should().Be(3);
    }

    // GetCraftingFailureSeverity
    [Fact]
    public void GetCraftingFailureSeverity_Returns0_OnSuccess()
    {
        CreateService().GetCraftingFailureSeverity(40, 50).Should().Be(0);
    }

    [Theory]
    [InlineData(55, 50, 1)]  // margin=5  → marginal
    [InlineData(70, 50, 2)]  // margin=20 → moderate
    [InlineData(90, 50, 3)]  // margin=40 → critical
    public void GetCraftingFailureSeverity_ClassifiesFailureByMargin(int roll, int chance, int expected)
    {
        CreateService().GetCraftingFailureSeverity(roll, chance).Should().Be(expected);
    }

    // GetQualityReductionForFailure
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    public void GetQualityReductionForFailure_MatchesExpected(int severity, int expected)
    {
        CreateService().GetQualityReductionForFailure(severity).Should().Be(expected);
    }

    // GetMaterialRefundPercentage
    [Fact]
    public void GetMaterialRefundPercentage_Returns50_ForCriticalFailure()
    {
        CreateService().GetMaterialRefundPercentage(3).Should().Be(50);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void GetMaterialRefundPercentage_Returns0_ForNonCriticalFailure(int severity)
    {
        CreateService().GetMaterialRefundPercentage(severity).Should().Be(0);
    }

    // GetBudgetRangeForQuestReward
    [Theory]
    [InlineData("easy",      10, 40,  60)]  // 50 * [0.8, 1.2]
    [InlineData("normal",    10, 50,  75)]  // 50 * [1.0, 1.5]
    [InlineData("hard",      10, 75, 125)]  // 50 * [1.5, 2.5]
    [InlineData("legendary", 10,150, 250)]  // 50 * [3.0, 5.0]
    public void GetBudgetRangeForQuestReward_AppliesDifficultyMultipliers(
        string difficulty, int questLevel, int expectedMin, int expectedMax)
    {
        var (min, max) = CreateService().GetBudgetRangeForQuestReward(questLevel, difficulty);
        min.Should().Be(expectedMin);
        max.Should().Be(expectedMax);
    }

    [Fact]
    public void GetBudgetRangeForQuestReward_EnforcesMinimums_ForLowLevelQuests()
    {
        // level=1: levelBudget=5; easy: min=5*0.8=4 (→15), max=5*1.2=6 (→30)
        var (min, max) = CreateService().GetBudgetRangeForQuestReward(1, "easy");
        min.Should().Be(15);
        max.Should().Be(30);
    }

    // GetBudgetForEventReward
    [Theory]
    [InlineData("world_boss",        10, 320)]   // 10*8 * 4.0
    [InlineData("dungeon_completion", 10, 200)]  // 10*8 * 2.5
    [InlineData("pvp_tournament",    10, 240)]   // 10*8 * 3.0
    public void GetBudgetForEventReward_AppliesEventMultiplier(string eventType, int level, int expected)
    {
        CreateService().GetBudgetForEventReward(eventType, level).Should().Be(expected);
    }

    [Fact]
    public void GetBudgetForEventReward_EnforcesMinimum_ForLowLevelParticipants()
    {
        // level=1: 1*8 * 4.0 = 32 → below minimum of 50
        CreateService().GetBudgetForEventReward("world_boss", 1).Should().Be(50);
    }

    // GetBudgetForItemUpgrade
    [Theory]
    [InlineData(100, 1, 25)]   // +25%
    [InlineData(100, 2, 50)]   // +50%
    [InlineData(100, 3, 100)]  // +100%
    public void GetBudgetForItemUpgrade_AppliesUpgradeMultiplier(int currentValue, int tier, int expected)
    {
        CreateService().GetBudgetForItemUpgrade(currentValue, tier).Should().Be(expected);
    }
}
