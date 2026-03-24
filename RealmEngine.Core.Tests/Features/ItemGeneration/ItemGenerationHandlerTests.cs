using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.ItemGeneration.Commands;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Core.Services;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;

namespace RealmEngine.Core.Tests.Features.ItemGeneration;

[Trait("Category", "Feature")]
public class GenerateItemCommandHandlerTests
{
    // ItemGenerator is assigned directly without null-check, so null! is safe for validation-only paths
    private static GenerateItemCommandHandler CreateHandler() =>
        new(null!, NullLogger<GenerateItemCommandHandler>.Instance);

    [Fact]
    public async Task Handle_ReturnsFailure_WhenCategoryIsEmpty()
    {
        var result = await CreateHandler().Handle(new GenerateItemCommand { Category = "" }, default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Category cannot be empty");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenCategoryIsWhitespace()
    {
        var result = await CreateHandler().Handle(new GenerateItemCommand { Category = "   " }, default);

        result.Success.Should().BeFalse();
    }
}

[Trait("Category", "Feature")]
public class GenerateEnemyCommandHandlerTests
{
    private static GenerateEnemyCommandHandler CreateHandler() =>
        new(null!, NullLogger<GenerateEnemyCommandHandler>.Instance);

    [Fact]
    public async Task Handle_ReturnsFailure_WhenCategoryIsEmpty()
    {
        var result = await CreateHandler().Handle(new GenerateEnemyCommand { Category = "" }, default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Category cannot be empty");
    }
}

[Trait("Category", "Feature")]
public class GenerateNPCCommandHandlerTests
{
    private static GenerateNPCCommandHandler CreateHandler() =>
        new(null!, NullLogger<GenerateNPCCommandHandler>.Instance);

    [Fact]
    public async Task Handle_ReturnsFailure_WhenCategoryIsEmpty()
    {
        var result = await CreateHandler().Handle(new GenerateNPCCommand { Category = "" }, default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Category cannot be empty");
    }
}

[Trait("Category", "Feature")]
public class GeneratePowerCommandHandlerTests
{
    private static GeneratePowerCommandHandler CreateHandler()
    {
        var mockRepo = new Mock<IPowerRepository>();
        mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([]);
        var generator = new PowerGenerator(mockRepo.Object, NullLogger<PowerGenerator>.Instance);
        return new GeneratePowerCommandHandler(generator, NullLogger<GeneratePowerCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenCategoryIsEmpty()
    {
        var command = new GeneratePowerCommand { Category = "", Subcategory = "offensive" };
        var result = await CreateHandler().Handle(command, default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Category is required");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenSubcategoryIsEmpty()
    {
        var command = new GeneratePowerCommand { Category = "active", Subcategory = "" };
        var result = await CreateHandler().Handle(command, default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Subcategory is required");
    }
}

[Trait("Category", "Feature")]
public class GenerateRandomItemsHandlerTests
{
    private static GenerateRandomItemsHandler CreateHandler()
    {
        var mockDbFactory = new Mock<IDbContextFactory<ContentDbContext>>();
        var categorySvc = new CategoryDiscoveryService(mockDbFactory.Object, NullLogger<CategoryDiscoveryService>.Instance);
        // ItemGenerator primary constructor has no null-guards, safe to use null! for unused deps in validation path
        var itemGen = new ItemGenerator(null!, null!, null!, null!, NullLogger<ItemGenerator>.Instance);
        return new GenerateRandomItemsHandler(itemGen, categorySvc, NullLogger<GenerateRandomItemsHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenQuantityIsZero()
    {
        var result = await CreateHandler().Handle(new GenerateRandomItemsCommand { Quantity = 0 }, default);

        result.Success.Should().BeFalse();
        result.RequestedQuantity.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenQuantityIsNegative()
    {
        var result = await CreateHandler().Handle(new GenerateRandomItemsCommand { Quantity = -5 }, default);

        result.Success.Should().BeFalse();
    }
}
