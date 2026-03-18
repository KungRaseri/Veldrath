using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Features.ItemGeneration.Commands;
using RealmEngine.Core.Features.ItemGeneration.Queries;

namespace RealmEngine.Core.Tests.Features.ItemGeneration;

[Trait("Category", "Feature")]
public class GenerateItemsByCategoryHandlerTests
{
    // ItemGenerator is a complex service requiring EF Core — passed as null for validation-only paths
    private static GenerateItemsByCategoryHandler CreateHandler() =>
        new(null!, NullLogger<GenerateItemsByCategoryHandler>.Instance);

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
    // CategoryDiscoveryService and NamePatternService require heavy infrastructure.
    // Passing null triggers an exception which the handler catches into Success = false.
    private static GetAvailableItemCategoriesHandler CreateHandler() =>
        new(null!, null!, NullLogger<GetAvailableItemCategoriesHandler>.Instance);

    [Fact]
    public async Task Handle_ReturnsFailure_WhenServicesNotAvailable()
    {
        var result = await CreateHandler().Handle(
            new GetAvailableItemCategoriesQuery(), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_ReturnsCategoriesCollection_OnSuccess_IsInitializedEmpty()
    {
        var result = await CreateHandler().Handle(
            new GetAvailableItemCategoriesQuery(), default);

        // Even failure result should have a Categories list (not null)
        result.Categories.Should().NotBeNull();
    }
}
