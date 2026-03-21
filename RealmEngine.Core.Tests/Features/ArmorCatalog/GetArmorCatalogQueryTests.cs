using Moq;
using RealmEngine.Core.Features.ArmorCatalog.Queries;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.ArmorCatalog;

public class GetArmorCatalogQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsAllFromRepository()
    {
        var data = new List<Item> { new(), new() };
        var mock = new Mock<IArmorRepository>();
        mock.Setup(r => r.GetAllAsync()).ReturnsAsync(data);
        var handler = new GetArmorCatalogQueryHandler(mock.Object);

        var result = await handler.Handle(new GetArmorCatalogQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public void Validator_AlwaysValid()
    {
        var result = new GetArmorCatalogQueryValidator().Validate(new GetArmorCatalogQuery());
        result.IsValid.Should().BeTrue();
    }
}
