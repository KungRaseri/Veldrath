using Moq;
using RealmEngine.Core.Features.RecipeCatalog.Queries;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.RecipeCatalog;

public class GetRecipeCatalogQueryHandlerTests
{
    private static Recipe MakeRecipe(string id = "recipe") => new() { Id = id, Name = id };

    private static IRecipeRepository BuildRepo(IEnumerable<Recipe> data)
    {
        var mock = new Mock<IRecipeRepository>();
        mock.Setup(r => r.GetAllAsync()).ReturnsAsync(data.ToList());
        mock.Setup(r => r.GetByCraftingSkillAsync(It.IsAny<string>())).ReturnsAsync(data.ToList());
        return mock.Object;
    }

    [Fact]
    public async Task Handle_NoFilter_ReturnsAllFromRepository()
    {
        var data = new List<Recipe> { MakeRecipe(), MakeRecipe("r2"), MakeRecipe("r3") };
        var handler = new GetRecipeCatalogQueryHandler(BuildRepo(data));

        var result = await handler.Handle(new GetRecipeCatalogQuery(), CancellationToken.None);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_WithFilter_CallsFilteredMethod()
    {
        var mock = new Mock<IRecipeRepository>();
        mock.Setup(r => r.GetByCraftingSkillAsync(It.IsAny<string>())).ReturnsAsync([MakeRecipe()]);
        var handler = new GetRecipeCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetRecipeCatalogQuery("blacksmithing"), CancellationToken.None);

        mock.Verify(r => r.GetByCraftingSkillAsync(It.IsAny<string>()), Times.Once);
        mock.Verify(r => r.GetAllAsync(), Times.Never);
    }

    [Fact]
    public async Task Handle_WithFilter_PassesFilterValueToRepository()
    {
        var mock = new Mock<IRecipeRepository>();
        mock.Setup(r => r.GetByCraftingSkillAsync(It.IsAny<string>())).ReturnsAsync([]);
        var handler = new GetRecipeCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetRecipeCatalogQuery("alchemy"), CancellationToken.None);

        mock.Verify(r => r.GetByCraftingSkillAsync("alchemy"), Times.Once);
    }

    [Fact]
    public void Validator_NullFilter_IsValid()
    {
        var result = new GetRecipeCatalogQueryValidator().Validate(new GetRecipeCatalogQuery(null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_ValidFilter_IsValid()
    {
        var result = new GetRecipeCatalogQueryValidator().Validate(new GetRecipeCatalogQuery("blacksmithing"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_EmptyStringFilter_IsInvalid()
    {
        var result = new GetRecipeCatalogQueryValidator().Validate(new GetRecipeCatalogQuery(""));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_TooLongFilter_IsInvalid()
    {
        var result = new GetRecipeCatalogQueryValidator().Validate(new GetRecipeCatalogQuery(new string('x', 101)));
        result.IsValid.Should().BeFalse();
    }
}
