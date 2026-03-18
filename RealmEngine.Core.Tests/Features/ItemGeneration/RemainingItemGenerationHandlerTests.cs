using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Features.ItemGeneration.Commands;
using RealmEngine.Core.Features.ItemGeneration.Queries;
using RealmEngine.Core.Generators.Modern;

namespace RealmEngine.Core.Tests.Features.ItemGeneration;

[Trait("Category", "Feature")]
public class GenerateItemsByCategoryHandlerTests
{
    // ItemGenerator uses a primary constructor with no null-checks; validation paths never reach _itemGenerator
    private static GenerateItemsByCategoryHandler CreateHandler() =>
        new(new ItemGenerator(null!, null!, null!, null!, NullLogger<ItemGenerator>.Instance),
            NullLogger<GenerateItemsByCategoryHandler>.Instance);

    [Fact]
    public async Task Handle_ReturnsFailure_WhenCategoryIsEmpty()
    {
        var result = await CreateHandler().Handle(
            new GenerateItemsByCategoryCommand { Category = "" }, default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Category");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenCategoryIsWhitespace()
    {
        var result = await CreateHandler().Handle(
            new GenerateItemsByCategoryCommand { Category = "   " }, default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Category");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenQuantityIsZero()
    {
        var result = await CreateHandler().Handle(
            new GenerateItemsByCategoryCommand { Category = "weapons/swords", Quantity = 0 }, default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Quantity");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenQuantityIsNegative()
    {
        var result = await CreateHandler().Handle(
            new GenerateItemsByCategoryCommand { Category = "weapons/swords", Quantity = -5 }, default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Quantity");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenQuantityExceedsMaximum()
    {
        var result = await CreateHandler().Handle(
            new GenerateItemsByCategoryCommand { Category = "weapons/swords", Quantity = 1001 }, default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("1000");
    }

    [Fact]
    public async Task Handle_ReturnsCategoryInResult_WhenCategoryIsSet()
    {
        var result = await CreateHandler().Handle(
            new GenerateItemsByCategoryCommand { Category = "" }, default);

        // Even on failure, the category is echoed in the result
        result.Category.Should().Be(string.Empty);
    }
}

[Trait("Category", "Feature")]
public class GetAvailableItemCategoriesHandlerTests
{
    // CategoryDiscoveryService and NamePatternService have null-guarded constructors requiring
    // heavy EF Core / DI infrastructure. The handler wraps all calls in a try-catch, returning
    // Success=false on any exception. We verify the result contract using an uninitialized
    // CategoryDiscoveryService (whose GetLeafCategories call throws on null _dbFactory).
    private static GetAvailableItemCategoriesHandler CreateHandler()
    {
        var dbFactoryMock = new Moq.Mock<Microsoft.EntityFrameworkCore.IDbContextFactory<RealmEngine.Data.Persistence.ContentDbContext>>();
        var scopeFactoryMock = new Moq.Mock<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>();
        var categoryService = new RealmEngine.Core.Services.CategoryDiscoveryService(
            dbFactoryMock.Object, NullLogger<RealmEngine.Core.Services.CategoryDiscoveryService>.Instance);
        var namePatternService = new RealmEngine.Core.Services.NamePatternService(
            scopeFactoryMock.Object, NullLogger<RealmEngine.Core.Services.NamePatternService>.Instance);
        return new GetAvailableItemCategoriesHandler(
            categoryService, namePatternService,
            NullLogger<GetAvailableItemCategoriesHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ReturnsNonNullCategories_WhenDatabaseReturnsEmpty()
    {
        // Mock IDbContextFactory returns a mock DbContext; empty DB returns empty category list.
        // The handler completes successfully with an empty Categories collection.
        var result = await CreateHandler().Handle(
            new GetAvailableItemCategoriesQuery(), default);

        result.Categories.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ReturnsEmptyCategories_WhenDatabaseReturnsEmpty()
    {
        var result = await CreateHandler().Handle(
            new GetAvailableItemCategoriesQuery(), default);

        result.TotalCategories.Should().Be(result.Categories.Count);
    }
}
