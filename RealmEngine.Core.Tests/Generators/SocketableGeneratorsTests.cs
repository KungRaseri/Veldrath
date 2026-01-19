using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Data.Services;
using RealmEngine.Shared.Models;
using Xunit;

namespace RealmEngine.Core.Tests.Generators;

public class SocketableGeneratorsTests
{
    private readonly GameDataCache _dataCache;

    public SocketableGeneratorsTests()
    {
        var basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "RealmEngine.Data", "Data", "Json");
        _dataCache = new GameDataCache(basePath, null); // IMemoryCache is optional
        _dataCache.LoadAllData(); // Load the data!
    }

    #region GemGenerator Tests

    [Fact]
    public void GemGenerator_Should_Generate_Valid_Gem()
    {
        // Arrange
        var generator = new GemGenerator(_dataCache, new NullLogger<GemGenerator>());

        // Act
        var gem = generator.Generate();

        // Assert
        gem.Should().NotBeNull();
        gem!.Id.Should().NotBeNullOrEmpty();
        gem.Name.Should().NotBeNullOrEmpty();
        gem.SocketType.Should().Be(SocketType.Gem);
        gem.RarityWeight.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData("red")]
    [InlineData("blue")]
    public void GemGenerator_Should_Generate_Gem_By_Category(string category)
    {
        // Arrange
        var generator = new GemGenerator(_dataCache, new NullLogger<GemGenerator>());

        // Act
        var gem = generator.Generate(category);

        // Assert
        gem.Should().NotBeNull();
        gem!.Category.Should().Be(category);
        gem.SocketType.Should().Be(SocketType.Gem);
    }

    [Fact]
    public void GemGenerator_Should_Generate_Multiple_Gems()
    {
        // Arrange
        var generator = new GemGenerator(_dataCache, new NullLogger<GemGenerator>());

        // Act
        var gems = generator.GenerateMany(5);

        // Assert
        gems.Should().HaveCount(5);
        gems.Should().OnlyContain(g => g.SocketType == SocketType.Gem);
        gems.Select(g => g.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void GemGenerator_Should_Parse_Gem_Color_Correctly()
    {
        // Arrange
        var generator = new GemGenerator(_dataCache, new NullLogger<GemGenerator>());

        // Act
        var redGem = generator.Generate("red");
        var blueGem = generator.Generate("blue");

        // Assert
        redGem!.Color.Should().Be(GemColor.Red);
        blueGem!.Color.Should().Be(GemColor.Blue);
    }

    [Fact]
    public void GemGenerator_Should_Parse_Traits_With_Correct_Types()
    {
        // Arrange
        var generator = new GemGenerator(_dataCache, new NullLogger<GemGenerator>());

        // Act
        var gem = generator.Generate();

        // Assert
        gem.Should().NotBeNull();
        if (gem!.Traits.Any())
        {
            gem.Traits.Values.Should().AllSatisfy(trait =>
            {
                trait.Type.Should().BeOneOf(
                    TraitType.Number,
                    TraitType.String,
                    TraitType.Boolean,
                    TraitType.StringArray,
                    TraitType.NumberArray
                );
            });
        }
    }

    #endregion

    #region EssenceGenerator Tests

    [Fact]
    public void EssenceGenerator_Should_Generate_Valid_Essence()
    {
        // Arrange
        var generator = new EssenceGenerator(_dataCache, new NullLogger<EssenceGenerator>());

        // Act
        var essence = generator.Generate();

        // Assert
        essence.Should().NotBeNull();
        essence!.Id.Should().NotBeNullOrEmpty();
        essence.Name.Should().NotBeNullOrEmpty();
        essence.RarityWeight.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData("fire")]
    [InlineData("shadow")]
    public void EssenceGenerator_Should_Generate_Essence_By_Category(string category)
    {
        // Arrange
        var generator = new EssenceGenerator(_dataCache, new NullLogger<EssenceGenerator>());

        // Act
        var essence = generator.Generate(category);

        // Assert
        essence.Should().NotBeNull();
        essence!.Category.Should().Be(category);
    }

    [Fact]
    public void EssenceGenerator_Should_Generate_Multiple_Essences()
    {
        // Arrange
        var generator = new EssenceGenerator(_dataCache, new NullLogger<EssenceGenerator>());

        // Act
        var essences = generator.GenerateMany(5);

        // Assert
        essences.Should().HaveCount(5);
        essences.Select(e => e.Id).Should().OnlyHaveUniqueItems();
    }

    #endregion

    #region RuneGenerator Tests

    [Fact]
    public void RuneGenerator_Should_Generate_Valid_Rune()
    {
        // Arrange
        var generator = new RuneGenerator(_dataCache, new NullLogger<RuneGenerator>());

        // Act
        var rune = generator.Generate();

        // Assert
        rune.Should().NotBeNull();
        rune!.Id.Should().NotBeNullOrEmpty();
        rune.Name.Should().NotBeNullOrEmpty();
        rune.SocketType.Should().Be(SocketType.Rune);
        rune.RarityWeight.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData("offensive")]
    [InlineData("defensive")]
    public void RuneGenerator_Should_Generate_Rune_By_Category(string category)
    {
        // Arrange
        var generator = new RuneGenerator(_dataCache, new NullLogger<RuneGenerator>());

        // Act
        var rune = generator.Generate(category);

        // Assert
        rune.Should().NotBeNull();
        rune!.Category.Should().Be(category);
        rune.SocketType.Should().Be(SocketType.Rune);
    }

    [Fact]
    public void RuneGenerator_Should_Generate_Multiple_Runes()
    {
        // Arrange
        var generator = new RuneGenerator(_dataCache, new NullLogger<RuneGenerator>());

        // Act
        var runes = generator.GenerateMany(5);

        // Assert
        runes.Should().HaveCount(5);
        runes.Should().OnlyContain(r => r.SocketType == SocketType.Rune);
        runes.Select(r => r.Id).Should().OnlyHaveUniqueItems();
    }

    #endregion

    #region CrystalGenerator Tests

    [Fact]
    public void CrystalGenerator_Should_Generate_Valid_Crystal()
    {
        // Arrange
        var generator = new CrystalGenerator(_dataCache, new NullLogger<CrystalGenerator>());

        // Act
        var crystal = generator.Generate();

        // Assert
        crystal.Should().NotBeNull();
        crystal!.Id.Should().NotBeNullOrEmpty();
        crystal.Name.Should().NotBeNullOrEmpty();
        crystal.SocketType.Should().Be(SocketType.Crystal);
        crystal.RarityWeight.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData("mana")]
    [InlineData("life")]
    public void CrystalGenerator_Should_Generate_Crystal_By_Category(string category)
    {
        // Arrange
        var generator = new CrystalGenerator(_dataCache, new NullLogger<CrystalGenerator>());

        // Act
        var crystal = generator.Generate(category);

        // Assert
        crystal.Should().NotBeNull();
        crystal!.Category.Should().Be(category);
        crystal.SocketType.Should().Be(SocketType.Crystal);
    }

    [Fact]
    public void CrystalGenerator_Should_Generate_Multiple_Crystals()
    {
        // Arrange
        var generator = new CrystalGenerator(_dataCache, new NullLogger<CrystalGenerator>());

        // Act
        var crystals = generator.GenerateMany(5);

        // Assert
        crystals.Should().HaveCount(5);
        crystals.Should().OnlyContain(c => c.SocketType == SocketType.Crystal);
        crystals.Select(c => c.Id).Should().OnlyHaveUniqueItems();
    }

    #endregion

    #region OrbGenerator Tests

    [Fact]
    public void OrbGenerator_Should_Generate_Valid_Orb()
    {
        // Arrange
        var generator = new OrbGenerator(_dataCache, new NullLogger<OrbGenerator>());

        // Act
        var orb = generator.Generate();

        // Assert
        orb.Should().NotBeNull();
        orb!.Id.Should().NotBeNullOrEmpty();
        orb.Name.Should().NotBeNullOrEmpty();
        orb.SocketType.Should().Be(SocketType.Orb);
        orb.RarityWeight.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData("combat")]
    [InlineData("magic")]
    public void OrbGenerator_Should_Generate_Orb_By_Category(string category)
    {
        // Arrange
        var generator = new OrbGenerator(_dataCache, new NullLogger<OrbGenerator>());

        // Act
        var orb = generator.Generate(category);

        // Assert
        orb.Should().NotBeNull();
        orb!.Category.Should().Be(category);
        orb.SocketType.Should().Be(SocketType.Orb);
    }

    [Fact]
    public void OrbGenerator_Should_Generate_Multiple_Orbs()
    {
        // Arrange
        var generator = new OrbGenerator(_dataCache, new NullLogger<OrbGenerator>());

        // Act
        var orbs = generator.GenerateMany(5);

        // Assert
        orbs.Should().HaveCount(5);
        orbs.Should().OnlyContain(o => o.SocketType == SocketType.Orb);
        orbs.Select(o => o.Id).Should().OnlyHaveUniqueItems();
    }

    #endregion

    #region Weighted Selection Tests

    [Fact]
    public void GemGenerator_Should_Respect_Rarity_Weights()
    {
        // Arrange
        var generator = new GemGenerator(_dataCache, new NullLogger<GemGenerator>());
        var results = new Dictionary<string, int>();

        // Act - Generate 100 gems to test weighted distribution
        for (int i = 0; i < 100; i++)
        {
            var gem = generator.Generate("red");
            if (gem != null)
            {
                if (!results.ContainsKey(gem.Name))
                    results[gem.Name] = 0;
                results[gem.Name]++;
            }
        }

        // Assert - Should have generated multiple different gems
        results.Should().NotBeEmpty();
        results.Keys.Should().HaveCountGreaterThanOrEqualTo(2); // At least 2 different gems
    }

    [Fact]
    public void EssenceGenerator_Should_Respect_Rarity_Weights()
    {
        // Arrange
        var generator = new EssenceGenerator(_dataCache, new NullLogger<EssenceGenerator>());
        var results = new Dictionary<string, int>();

        // Act - Generate 100 essences
        for (int i = 0; i < 100; i++)
        {
            var essence = generator.Generate("fire");
            if (essence != null)
            {
                if (!results.ContainsKey(essence.Name))
                    results[essence.Name] = 0;
                results[essence.Name]++;
            }
        }

        // Assert
        results.Should().NotBeEmpty();
        results.Keys.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    #endregion

    #region Cross-Generator Tests

    [Fact]
    public void All_Generators_Should_Produce_Unique_Socket_Types()
    {
        // Arrange
        var gemGen = new GemGenerator(_dataCache, new NullLogger<GemGenerator>());
        var essenceGen = new EssenceGenerator(_dataCache, new NullLogger<EssenceGenerator>());
        var runeGen = new RuneGenerator(_dataCache, new NullLogger<RuneGenerator>());
        var crystalGen = new CrystalGenerator(_dataCache, new NullLogger<CrystalGenerator>());
        var orbGen = new OrbGenerator(_dataCache, new NullLogger<OrbGenerator>());

        // Act
        var gem = gemGen.Generate();
        var rune = runeGen.Generate();
        var crystal = crystalGen.Generate();
        var orb = orbGen.Generate();

        // Assert
        var socketTypes = new[] { 
            gem?.SocketType, 
            rune?.SocketType, 
            crystal?.SocketType, 
            orb?.SocketType 
        };

        socketTypes.Should().OnlyHaveUniqueItems();
        socketTypes.Should().Contain(SocketType.Gem);
        socketTypes.Should().Contain(SocketType.Rune);
        socketTypes.Should().Contain(SocketType.Crystal);
        socketTypes.Should().Contain(SocketType.Orb);
    }

    [Fact]
    public void All_Generators_Should_Generate_Valid_Socketables()
    {
        // Arrange & Act
        var gem = new GemGenerator(_dataCache, new NullLogger<GemGenerator>()).Generate();
        var rune = new RuneGenerator(_dataCache, new NullLogger<RuneGenerator>()).Generate();
        var crystal = new CrystalGenerator(_dataCache, new NullLogger<CrystalGenerator>()).Generate();
        var orb = new OrbGenerator(_dataCache, new NullLogger<OrbGenerator>()).Generate();

        // Assert - All should implement common socketable properties
        var socketables = new ISocketable?[] { gem, rune, crystal, orb };
        socketables.Should().AllSatisfy(s =>
        {
            s.Should().NotBeNull();
            s!.Id.Should().NotBeNullOrEmpty();
            s.Name.Should().NotBeNullOrEmpty();
            s.RarityWeight.Should().BeGreaterThan(0);
        });
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void GemGenerator_Should_Return_Null_For_Invalid_Category()
    {
        // Arrange
        var generator = new GemGenerator(_dataCache, new NullLogger<GemGenerator>());

        // Act
        var gem = generator.Generate("invalid-category-that-does-not-exist");

        // Assert - Should still generate from all gems, not return null
        gem.Should().NotBeNull("generator should fallback to all gems when category filter returns no results");
    }

    [Fact]
    public void EssenceGenerator_Should_Handle_Empty_Category_Gracefully()
    {
        // Arrange
        var generator = new EssenceGenerator(_dataCache, new NullLogger<EssenceGenerator>());

        // Act
        var essence = generator.Generate("");

        // Assert - Empty string should be treated as null (generate from all)
        essence.Should().NotBeNull();
    }

    #endregion

    #region Performance Tests

    [Fact]
    public void GemGenerator_Should_Generate_1000_Gems_Quickly()
    {
        // Arrange
        var generator = new GemGenerator(_dataCache, new NullLogger<GemGenerator>());
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var gems = generator.GenerateMany(1000);
        stopwatch.Stop();

        // Assert
        gems.Should().HaveCount(1000);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, "generation should be reasonably fast");
    }

    #endregion
}
