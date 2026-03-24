using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Services;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Xunit;

namespace RealmEngine.Core.Tests.Services;

[Trait("Category", "Services")]
public class RecipeDataServiceTests
{
    private readonly Mock<IRecipeRepository> _repoMock = new();

    private RecipeDataService CreateService() =>
        new(_repoMock.Object, NullLogger<RecipeDataService>.Instance);

    private static List<Recipe> SampleRecipes() =>
    [
        new Recipe { Id = "recipe_001", Name = "Iron Sword",    Slug = "iron-sword",    Category = "weapons", RequiredSkillLevel = 10 },
        new Recipe { Id = "recipe_002", Name = "Wooden Shield", Slug = "wooden-shield", Category = "armor",   RequiredSkillLevel = 5  },
        new Recipe { Id = "recipe_003", Name = "Steel Axe",     Slug = "steel-axe",     Category = "weapons", RequiredSkillLevel = 20 },
    ];

    // LoadAllRecipes
    [Fact]
    public void LoadAllRecipes_EmptyRepository_ReturnsEmpty()
    {
        _repoMock.Setup(r => r.GetAllAsync()).ReturnsAsync([]);
        CreateService().LoadAllRecipes().Should().BeEmpty();
    }

    [Fact]
    public void LoadAllRecipes_WithThreeRecipes_ReturnsAll()
    {
        _repoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(SampleRecipes());
        CreateService().LoadAllRecipes().Should().HaveCount(3);
    }

    [Fact]
    public void LoadAllRecipes_CalledMultipleTimes_OnlyQueriesRepositoryOnce()
    {
        _repoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(SampleRecipes());
        var service = CreateService();

        service.LoadAllRecipes();
        service.LoadAllRecipes();
        service.LoadAllRecipes();

        _repoMock.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [Fact]
    public void LoadAllRecipes_RepositoryThrows_ReturnsEmpty()
    {
        _repoMock.Setup(r => r.GetAllAsync()).ThrowsAsync(new InvalidOperationException("DB unavailable"));
        CreateService().LoadAllRecipes().Should().BeEmpty();
    }

    // LoadRecipesByCategory
    [Fact]
    public void LoadRecipesByCategory_KnownCategory_ReturnsMatchingSubset()
    {
        _repoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(SampleRecipes());
        var result = CreateService().LoadRecipesByCategory("weapons");
        result.Should().HaveCount(2).And.AllSatisfy(r => r.Category.Should().Be("weapons"));
    }

    [Fact]
    public void LoadRecipesByCategory_UnknownCategory_ReturnsEmpty()
    {
        _repoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(SampleRecipes());
        CreateService().LoadRecipesByCategory("consumables").Should().BeEmpty();
    }

    [Fact]
    public void LoadRecipesByCategory_CalledTwiceForSameCategory_OnlyQueriesRepositoryOnce()
    {
        _repoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(SampleRecipes());
        var service = CreateService();

        service.LoadRecipesByCategory("weapons");
        service.LoadRecipesByCategory("weapons");

        _repoMock.Verify(r => r.GetAllAsync(), Times.Once);
    }

    // GetRecipeById
    [Fact]
    public void GetRecipeById_ById_ReturnsCorrectRecipe()
    {
        _repoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(SampleRecipes());
        var result = CreateService().GetRecipeById("recipe_001");
        result.Should().NotBeNull();
        result!.Name.Should().Be("Iron Sword");
    }

    [Fact]
    public void GetRecipeById_BySlug_ReturnsCorrectRecipe()
    {
        _repoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(SampleRecipes());
        var result = CreateService().GetRecipeById("steel-axe");
        result.Should().NotBeNull();
        result!.Name.Should().Be("Steel Axe");
    }

    [Fact]
    public void GetRecipeById_NonExistentId_ReturnsNull()
    {
        _repoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(SampleRecipes());
        CreateService().GetRecipeById("recipe_999").Should().BeNull();
    }

    // GetAvailableRecipes
    [Fact]
    public void GetAvailableRecipes_SkillLevelTen_ExcludesHigherLevelRecipes()
    {
        _repoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(SampleRecipes());
        var result = CreateService().GetAvailableRecipes(skillLevel: 10);
        // RequiredSkillLevel ≤ 10: recipe_001 (10), recipe_002 (5)
        result.Should().HaveCount(2);
        result.Should().NotContain(r => r.Id == "recipe_003");
    }

    [Fact]
    public void GetAvailableRecipes_MaxSkillLevel_ReturnsAll()
    {
        _repoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(SampleRecipes());
        CreateService().GetAvailableRecipes(skillLevel: 100).Should().HaveCount(3);
    }

    [Fact]
    public void GetAvailableRecipes_ZeroSkillLevel_ReturnsOnlyZeroRequirementRecipes()
    {
        _repoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(SampleRecipes());
        // Minimum RequiredSkillLevel in sample data is 5, so none qualify at 0
        CreateService().GetAvailableRecipes(skillLevel: 0).Should().BeEmpty();
    }

    [Fact]
    public void GetAvailableRecipes_WithCategory_FiltersToThatCategory()
    {
        _repoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(SampleRecipes());
        var result = CreateService().GetAvailableRecipes(skillLevel: 20, category: "weapons");
        result.Should().HaveCount(2).And.AllSatisfy(r => r.Category.Should().Be("weapons"));
    }

    [Fact]
    public void GetAvailableRecipes_WithCategoryAndLowSkill_ApplicationBothFilters()
    {
        _repoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(SampleRecipes());
        // weapons with RequiredSkillLevel ≤ 10: only recipe_001
        var result = CreateService().GetAvailableRecipes(skillLevel: 10, category: "weapons");
        result.Should().HaveCount(1);
        result[0].Id.Should().Be("recipe_001");
    }

    // ClearCache
    [Fact]
    public void ClearCache_AfterLoad_ForcesRepositoryReload()
    {
        _repoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(SampleRecipes());
        var service = CreateService();

        service.LoadAllRecipes();
        service.ClearCache();
        service.LoadAllRecipes();

        _repoMock.Verify(r => r.GetAllAsync(), Times.Exactly(2));
    }

    [Fact]
    public void ClearCache_AfterCategoryLoad_ForcesRepositoryCategoryReload()
    {
        _repoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(SampleRecipes());
        var service = CreateService();

        service.LoadRecipesByCategory("weapons");
        service.ClearCache();
        service.LoadRecipesByCategory("weapons");

        _repoMock.Verify(r => r.GetAllAsync(), Times.Exactly(2));
    }
}
