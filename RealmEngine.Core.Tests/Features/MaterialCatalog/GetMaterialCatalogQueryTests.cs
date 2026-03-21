using Moq;
using RealmEngine.Core.Features.MaterialCatalog.Queries;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.MaterialCatalog;

public class GetMaterialCatalogQueryHandlerTests
{
    private static MaterialEntry MakeEntry(string slug, string family) =>
        new(slug, slug, family, 1.0f, true, null, null, null, null);

    private static IMaterialRepository BuildRepo(IEnumerable<MaterialEntry> data)
    {
        var mock = new Mock<IMaterialRepository>();
        mock.Setup(r => r.GetAllAsync()).ReturnsAsync(data.ToList());
        mock.Setup(r => r.GetByFamiliesAsync(It.IsAny<IEnumerable<string>>())).ReturnsAsync(data.ToList());
        return mock.Object;
    }

    [Fact]
    public async Task Handle_NoFilter_ReturnsAllFromRepository()
    {
        var data = new List<MaterialEntry>
        {
            MakeEntry("iron", "metal"),
            MakeEntry("oak", "wood"),
            MakeEntry("leather", "leather"),
        };
        var handler = new GetMaterialCatalogQueryHandler(BuildRepo(data));

        var result = await handler.Handle(new GetMaterialCatalogQuery(), CancellationToken.None);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_WithFilter_CallsFilteredMethod()
    {
        var mock = new Mock<IMaterialRepository>();
        mock.Setup(r => r.GetByFamiliesAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([MakeEntry("iron", "metal")]);
        var handler = new GetMaterialCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetMaterialCatalogQuery("metal"), CancellationToken.None);

        mock.Verify(r => r.GetByFamiliesAsync(It.IsAny<IEnumerable<string>>()), Times.Once);
        mock.Verify(r => r.GetAllAsync(), Times.Never);
    }

    [Fact]
    public async Task Handle_WithFilter_PassesFilterValueToRepository()
    {
        var mock = new Mock<IMaterialRepository>();
        mock.Setup(r => r.GetByFamiliesAsync(It.IsAny<IEnumerable<string>>())).ReturnsAsync([]);
        var handler = new GetMaterialCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetMaterialCatalogQuery("wood"), CancellationToken.None);

        mock.Verify(r => r.GetByFamiliesAsync(It.Is<IEnumerable<string>>(f => f.Single() == "wood")), Times.Once);
    }

    [Fact]
    public void Validator_NullFilter_IsValid()
    {
        var result = new GetMaterialCatalogQueryValidator().Validate(new GetMaterialCatalogQuery(null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_ValidFilter_IsValid()
    {
        var result = new GetMaterialCatalogQueryValidator().Validate(new GetMaterialCatalogQuery("metal"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_EmptyStringFilter_IsInvalid()
    {
        var result = new GetMaterialCatalogQueryValidator().Validate(new GetMaterialCatalogQuery(""));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_TooLongFilter_IsInvalid()
    {
        var result = new GetMaterialCatalogQueryValidator().Validate(new GetMaterialCatalogQuery(new string('x', 101)));
        result.IsValid.Should().BeFalse();
    }
}
