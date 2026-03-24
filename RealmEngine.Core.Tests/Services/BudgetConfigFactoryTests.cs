using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Services.Budget;
using RealmEngine.Data.Services;
using Xunit;

namespace RealmEngine.Core.Tests.Services;

[Trait("Category", "Services")]
public class BudgetConfigFactoryTests
{
    // Stub infrastructure
    /// <summary>Returns the provided JSON for any key; records the last key requested.</summary>
    private sealed class StubConfigService(string? json = null) : GameConfigService
    {
        public List<string> RequestedKeys { get; } = [];
        public override string? GetData(string key) { RequestedKeys.Add(key); return json; }
    }

    private static BudgetConfigFactory CreateFactory(string? json = null)
    {
        var stub = new StubConfigService(json);
        return new BudgetConfigFactory(stub, NullLogger<BudgetConfigFactory>.Instance);
    }

    // Returns type defaults when config is absent
    [Fact]
    public void GetBudgetConfig_WhenKeyAbsent_ReturnsNonNullDefault()
    {
        CreateFactory(json: null).GetBudgetConfig().Should().NotBeNull();
    }

    [Fact]
    public void GetMaterialPools_WhenKeyAbsent_ReturnsNonNullDefault()
    {
        var pools = CreateFactory(json: null).GetMaterialPools();

        pools.Should().NotBeNull();
        pools.Pools.Should().BeEmpty();
    }

    [Fact]
    public void GetEnemyTypes_WhenKeyAbsent_ReturnsNonNullDefault()
    {
        var types = CreateFactory(json: null).GetEnemyTypes();

        types.Should().NotBeNull();
        types.Types.Should().BeEmpty();
    }

    [Fact]
    public void GetMaterialFilters_WhenKeyAbsent_ReturnsNonNullDefault()
    {
        var filters = CreateFactory(json: null).GetMaterialFilters();

        filters.Should().NotBeNull();
        filters.Categories.Should().BeEmpty();
    }

    [Fact]
    public void GetSocketConfig_WhenKeyAbsent_ReturnsNonNullDefault()
    {
        var config = CreateFactory(json: null).GetSocketConfig();

        config.Should().NotBeNull();
        config.SocketCounts.Should().BeEmpty();
        config.SocketTypeWeights.Should().BeEmpty();
    }

    // Each method requests the correct config key
    [Theory]
    [InlineData("budget-config")]
    public void GetBudgetConfig_RequestsCorrectKey(string expectedKey)
    {
        var stub = new StubConfigService();
        var factory = new BudgetConfigFactory(stub, NullLogger<BudgetConfigFactory>.Instance);

        factory.GetBudgetConfig();

        stub.RequestedKeys.Should().Contain(expectedKey);
    }

    [Theory]
    [InlineData("material-pools")]
    public void GetMaterialPools_RequestsCorrectKey(string expectedKey)
    {
        var stub = new StubConfigService();
        var factory = new BudgetConfigFactory(stub, NullLogger<BudgetConfigFactory>.Instance);

        factory.GetMaterialPools();

        stub.RequestedKeys.Should().Contain(expectedKey);
    }

    [Theory]
    [InlineData("enemy-types")]
    public void GetEnemyTypes_RequestsCorrectKey(string expectedKey)
    {
        var stub = new StubConfigService();
        var factory = new BudgetConfigFactory(stub, NullLogger<BudgetConfigFactory>.Instance);

        factory.GetEnemyTypes();

        stub.RequestedKeys.Should().Contain(expectedKey);
    }

    [Theory]
    [InlineData("material-filters")]
    public void GetMaterialFilters_RequestsCorrectKey(string expectedKey)
    {
        var stub = new StubConfigService();
        var factory = new BudgetConfigFactory(stub, NullLogger<BudgetConfigFactory>.Instance);

        factory.GetMaterialFilters();

        stub.RequestedKeys.Should().Contain(expectedKey);
    }

    [Theory]
    [InlineData("socket-config")]
    public void GetSocketConfig_RequestsCorrectKey(string expectedKey)
    {
        var stub = new StubConfigService();
        var factory = new BudgetConfigFactory(stub, NullLogger<BudgetConfigFactory>.Instance);

        factory.GetSocketConfig();

        stub.RequestedKeys.Should().Contain(expectedKey);
    }

    // Falls back to type defaults on invalid JSON
    [Fact]
    public void GetBudgetConfig_InvalidJson_ReturnsTypeDefault()
    {
        CreateFactory(json: "{not valid!!").GetBudgetConfig().Should().NotBeNull();
    }

    [Fact]
    public void GetSocketConfig_InvalidJson_ReturnsTypeDefault()
    {
        CreateFactory(json: "{not valid!!").GetSocketConfig().SocketCounts.Should().BeEmpty();
    }

    // Deserializes valid JSON
    [Fact]
    public void GetBudgetConfig_ValidJson_DeserializesPatternCosts()
    {
        const string json = """{"patternCosts":{"prefix":5,"suffix":3}}""";
        var config = CreateFactory(json).GetBudgetConfig();

        config.PatternCosts.Should().ContainKey("prefix").WhoseValue.Should().Be(5);
        config.PatternCosts.Should().ContainKey("suffix").WhoseValue.Should().Be(3);
    }

    [Fact]
    public void GetMaterialPools_ValidJson_DeserializesPools()
    {
        const string json = """{"pools":{"common":{"materials":[]}}}""";
        var pools = CreateFactory(json).GetMaterialPools();

        pools.Pools.Should().ContainKey("common");
    }

    [Fact]
    public void GetSocketConfig_ValidJson_DeserializesSocketCounts()
    {
        const string json = """{"socketCounts":{"Rare":{"chances":[0,50,50]}}}""";
        var config = CreateFactory(json).GetSocketConfig();

        config.SocketCounts.Should().ContainKey("Rare");
        config.SocketCounts["Rare"].Chances.Should().Equal(0, 50, 50);
    }
}
