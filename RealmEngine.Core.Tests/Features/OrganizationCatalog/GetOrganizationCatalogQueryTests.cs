using Moq;
using RealmEngine.Core.Features.OrganizationCatalog.Queries;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.OrganizationCatalog;

public class GetOrganizationCatalogQueryHandlerTests
{
    private static OrganizationEntry MakeOrg() =>
        new("slug", "Display", "npc", "guild", 1);

    private static IOrganizationRepository BuildRepo(IEnumerable<OrganizationEntry> data)
    {
        var mock = new Mock<IOrganizationRepository>();
        mock.Setup(r => r.GetAllAsync()).ReturnsAsync(data.ToList());
        mock.Setup(r => r.GetByTypeAsync(It.IsAny<string>())).ReturnsAsync(data.ToList());
        return mock.Object;
    }

    [Fact]
    public async Task Handle_NoFilter_ReturnsAllFromRepository()
    {
        var data = new List<OrganizationEntry> { MakeOrg(), MakeOrg(), MakeOrg() };
        var handler = new GetOrganizationCatalogQueryHandler(BuildRepo(data));

        var result = await handler.Handle(new GetOrganizationCatalogQuery(), CancellationToken.None);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_WithFilter_CallsFilteredMethod()
    {
        var mock = new Mock<IOrganizationRepository>();
        mock.Setup(r => r.GetByTypeAsync(It.IsAny<string>())).ReturnsAsync([MakeOrg()]);
        var handler = new GetOrganizationCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetOrganizationCatalogQuery("guild"), CancellationToken.None);

        mock.Verify(r => r.GetByTypeAsync(It.IsAny<string>()), Times.Once);
        mock.Verify(r => r.GetAllAsync(), Times.Never);
    }

    [Fact]
    public async Task Handle_WithFilter_PassesFilterValueToRepository()
    {
        var mock = new Mock<IOrganizationRepository>();
        mock.Setup(r => r.GetByTypeAsync(It.IsAny<string>())).ReturnsAsync([]);
        var handler = new GetOrganizationCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetOrganizationCatalogQuery("faction"), CancellationToken.None);

        mock.Verify(r => r.GetByTypeAsync("faction"), Times.Once);
    }

    [Fact]
    public void Validator_NullFilter_IsValid()
    {
        var result = new GetOrganizationCatalogQueryValidator().Validate(new GetOrganizationCatalogQuery(null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_ValidFilter_IsValid()
    {
        var result = new GetOrganizationCatalogQueryValidator().Validate(new GetOrganizationCatalogQuery("guild"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_EmptyStringFilter_IsInvalid()
    {
        var result = new GetOrganizationCatalogQueryValidator().Validate(new GetOrganizationCatalogQuery(""));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_TooLongFilter_IsInvalid()
    {
        var result = new GetOrganizationCatalogQueryValidator().Validate(new GetOrganizationCatalogQuery(new string('x', 101)));
        result.IsValid.Should().BeFalse();
    }
}
