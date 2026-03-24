using Moq;
using RealmEngine.Core.Features.PowerCatalog.Queries;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.AbilityCatalog;

public class GetPowerCatalogQueryHandlerTests
{
    private static IPowerRepository BuildRepo(IEnumerable<Power> data)
    {
        var mock = new Mock<IPowerRepository>();
        mock.Setup(r => r.GetAllAsync()).ReturnsAsync(data.ToList());
        mock.Setup(r => r.GetByTypeAsync(It.IsAny<string>())).ReturnsAsync(data.ToList());
        mock.Setup(r => r.GetBySchoolAsync(It.IsAny<string>())).ReturnsAsync(data.ToList());
        return mock.Object;
    }

    [Fact]
    public async Task Handle_NoFilter_ReturnsAllFromRepository()
    {
        var data = new List<Power> { new(), new(), new() };
        var handler = new GetPowerCatalogQueryHandler(BuildRepo(data));

        var result = await handler.Handle(new GetPowerCatalogQuery(), CancellationToken.None);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_WithPowerTypeFilter_CallsFilteredMethod()
    {
        var mock = new Mock<IPowerRepository>();
        mock.Setup(r => r.GetByTypeAsync(It.IsAny<string>())).ReturnsAsync([new Power()]);
        var handler = new GetPowerCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetPowerCatalogQuery(PowerType: "passive"), CancellationToken.None);

        mock.Verify(r => r.GetByTypeAsync(It.IsAny<string>()), Times.Once);
        mock.Verify(r => r.GetAllAsync(), Times.Never);
    }

    [Fact]
    public async Task Handle_WithPowerTypeFilter_PassesFilterValueToRepository()
    {
        var mock = new Mock<IPowerRepository>();
        mock.Setup(r => r.GetByTypeAsync(It.IsAny<string>())).ReturnsAsync([]);
        var handler = new GetPowerCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetPowerCatalogQuery(PowerType: "spell"), CancellationToken.None);

        mock.Verify(r => r.GetByTypeAsync("spell"), Times.Once);
    }

    [Fact]
    public async Task Handle_WithSchoolFilter_CallsFilteredMethod()
    {
        var mock = new Mock<IPowerRepository>();
        mock.Setup(r => r.GetBySchoolAsync(It.IsAny<string>())).ReturnsAsync([new Power()]);
        var handler = new GetPowerCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetPowerCatalogQuery(School: "fire"), CancellationToken.None);

        mock.Verify(r => r.GetBySchoolAsync(It.IsAny<string>()), Times.Once);
        mock.Verify(r => r.GetAllAsync(), Times.Never);
    }

    [Fact]
    public void Validator_NullFilters_IsValid()
    {
        var result = new GetPowerCatalogQueryValidator().Validate(new GetPowerCatalogQuery(null, null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_ValidPowerTypeFilter_IsValid()
    {
        var result = new GetPowerCatalogQueryValidator().Validate(new GetPowerCatalogQuery(PowerType: "spell"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_EmptyPowerTypeFilter_IsInvalid()
    {
        var result = new GetPowerCatalogQueryValidator().Validate(new GetPowerCatalogQuery(PowerType: ""));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_TooLongPowerTypeFilter_IsInvalid()
    {
        var result = new GetPowerCatalogQueryValidator().Validate(new GetPowerCatalogQuery(PowerType: new string('x', 101)));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_ValidSchoolFilter_IsValid()
    {
        var result = new GetPowerCatalogQueryValidator().Validate(new GetPowerCatalogQuery(School: "arcane"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_EmptySchoolFilter_IsInvalid()
    {
        var result = new GetPowerCatalogQueryValidator().Validate(new GetPowerCatalogQuery(School: ""));
        result.IsValid.Should().BeFalse();
    }
}
