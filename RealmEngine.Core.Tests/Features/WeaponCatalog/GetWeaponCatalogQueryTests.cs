using Moq;
using RealmEngine.Core.Features.WeaponCatalog.Queries;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.WeaponCatalog;

public class GetWeaponCatalogQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsAllFromRepository()
    {
        var data = new List<Item> { new(), new() };
        var mock = new Mock<IWeaponRepository>();
        mock.Setup(r => r.GetAllAsync()).ReturnsAsync(data);
        var handler = new GetWeaponCatalogQueryHandler(mock.Object);

        var result = await handler.Handle(new GetWeaponCatalogQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public void Validator_AlwaysValid()
    {
        var result = new GetWeaponCatalogQueryValidator().Validate(new GetWeaponCatalogQuery());
        result.IsValid.Should().BeTrue();
    }
}
