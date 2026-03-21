using Moq;
using RealmEngine.Core.Features.MaterialPropertyCatalog.Queries;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.MaterialPropertyCatalog;

public class GetMaterialPropertyCatalogQueryHandlerTests
{
    private static MaterialPropertyEntry MakeProp() =>
        new("slug", "Display", "material", "metal", 1.0f, 1);

    private static IMaterialPropertyRepository BuildRepo(IEnumerable<MaterialPropertyEntry> data)
    {
        var mock = new Mock<IMaterialPropertyRepository>();
        mock.Setup(r => r.GetAllAsync()).ReturnsAsync(data.ToList());
        mock.Setup(r => r.GetByFamilyAsync(It.IsAny<string>())).ReturnsAsync(data.ToList());
        return mock.Object;
    }

    [Fact]
    public async Task Handle_NoFilter_ReturnsAllFromRepository()
    {
        var data = new List<MaterialPropertyEntry> { MakeProp(), MakeProp(), MakeProp() };
        var handler = new GetMaterialPropertyCatalogQueryHandler(BuildRepo(data));

        var result = await handler.Handle(new GetMaterialPropertyCatalogQuery(), CancellationToken.None);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_WithFilter_CallsFilteredMethod()
    {
        var mock = new Mock<IMaterialPropertyRepository>();
        mock.Setup(r => r.GetByFamilyAsync(It.IsAny<string>())).ReturnsAsync([MakeProp()]);
        var handler = new GetMaterialPropertyCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetMaterialPropertyCatalogQuery("metal"), CancellationToken.None);

        mock.Verify(r => r.GetByFamilyAsync(It.IsAny<string>()), Times.Once);
        mock.Verify(r => r.GetAllAsync(), Times.Never);
    }

    [Fact]
    public async Task Handle_WithFilter_PassesFilterValueToRepository()
    {
        var mock = new Mock<IMaterialPropertyRepository>();
        mock.Setup(r => r.GetByFamilyAsync(It.IsAny<string>())).ReturnsAsync([]);
        var handler = new GetMaterialPropertyCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetMaterialPropertyCatalogQuery("wood"), CancellationToken.None);

        mock.Verify(r => r.GetByFamilyAsync("wood"), Times.Once);
    }

    [Fact]
    public void Validator_NullFilter_IsValid()
    {
        var result = new GetMaterialPropertyCatalogQueryValidator().Validate(new GetMaterialPropertyCatalogQuery(null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_ValidFilter_IsValid()
    {
        var result = new GetMaterialPropertyCatalogQueryValidator().Validate(new GetMaterialPropertyCatalogQuery("metal"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_EmptyStringFilter_IsInvalid()
    {
        var result = new GetMaterialPropertyCatalogQueryValidator().Validate(new GetMaterialPropertyCatalogQuery(""));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_TooLongFilter_IsInvalid()
    {
        var result = new GetMaterialPropertyCatalogQueryValidator().Validate(new GetMaterialPropertyCatalogQuery(new string('x', 101)));
        result.IsValid.Should().BeFalse();
    }
}
