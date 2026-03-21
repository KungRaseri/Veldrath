using Moq;
using RealmEngine.Core.Features.TraitCatalog.Queries;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.TraitCatalog;

public class GetTraitCatalogQueryHandlerTests
{
    private static TraitDefinitionEntry MakeTrait() =>
        new("trait-key", "string", null, null);

    private static ITraitDefinitionRepository BuildRepo(IEnumerable<TraitDefinitionEntry> data)
    {
        var mock = new Mock<ITraitDefinitionRepository>();
        mock.Setup(r => r.GetAllAsync()).ReturnsAsync(data.ToList());
        mock.Setup(r => r.GetByAppliesToAsync(It.IsAny<string>())).ReturnsAsync(data.ToList());
        return mock.Object;
    }

    [Fact]
    public async Task Handle_NoFilter_ReturnsAllFromRepository()
    {
        var data = new List<TraitDefinitionEntry> { MakeTrait(), MakeTrait(), MakeTrait() };
        var handler = new GetTraitCatalogQueryHandler(BuildRepo(data));

        var result = await handler.Handle(new GetTraitCatalogQuery(), CancellationToken.None);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_WithFilter_CallsFilteredMethod()
    {
        var mock = new Mock<ITraitDefinitionRepository>();
        mock.Setup(r => r.GetByAppliesToAsync(It.IsAny<string>())).ReturnsAsync([MakeTrait()]);
        var handler = new GetTraitCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetTraitCatalogQuery("enemies"), CancellationToken.None);

        mock.Verify(r => r.GetByAppliesToAsync(It.IsAny<string>()), Times.Once);
        mock.Verify(r => r.GetAllAsync(), Times.Never);
    }

    [Fact]
    public async Task Handle_WithFilter_PassesFilterValueToRepository()
    {
        var mock = new Mock<ITraitDefinitionRepository>();
        mock.Setup(r => r.GetByAppliesToAsync(It.IsAny<string>())).ReturnsAsync([]);
        var handler = new GetTraitCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetTraitCatalogQuery("weapons"), CancellationToken.None);

        mock.Verify(r => r.GetByAppliesToAsync("weapons"), Times.Once);
    }

    [Fact]
    public void Validator_NullFilter_IsValid()
    {
        var result = new GetTraitCatalogQueryValidator().Validate(new GetTraitCatalogQuery(null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_ValidFilter_IsValid()
    {
        var result = new GetTraitCatalogQueryValidator().Validate(new GetTraitCatalogQuery("enemies"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_EmptyStringFilter_IsInvalid()
    {
        var result = new GetTraitCatalogQueryValidator().Validate(new GetTraitCatalogQuery(""));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_TooLongFilter_IsInvalid()
    {
        var result = new GetTraitCatalogQueryValidator().Validate(new GetTraitCatalogQuery(new string('x', 101)));
        result.IsValid.Should().BeFalse();
    }
}
