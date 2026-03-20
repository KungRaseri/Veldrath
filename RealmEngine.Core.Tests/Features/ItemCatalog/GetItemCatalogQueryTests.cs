using Moq;
using RealmEngine.Core.Features.ItemCatalog.Queries;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.ItemCatalog;

public class GetItemCatalogQueryHandlerTests
{
    private static IItemRepository BuildRepo(IEnumerable<Item> data)
    {
        var mock = new Mock<IItemRepository>();
        mock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(data.ToList());
        mock.Setup(r => r.GetByTypeAsync(It.IsAny<string>()))
            .ReturnsAsync((string t) => data.Where(i => i.TypeKey == t).ToList());
        return mock.Object;
    }

    [Fact]
    public async Task Handle_ReturnsAllItems_WhenNoFilterGiven()
    {
        var data = new List<Item>
        {
            new() { Slug = "health-potion", TypeKey = "consumables" },
            new() { Slug = "ruby",          TypeKey = "gems" },
        };
        var handler = new GetItemCatalogQueryHandler(BuildRepo(data));

        var result = await handler.Handle(new GetItemCatalogQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_FiltersOnItemType_WhenProvided()
    {
        var data = new List<Item>
        {
            new() { Slug = "health-potion", TypeKey = "consumables" },
            new() { Slug = "ruby",          TypeKey = "gems" },
            new() { Slug = "mana-potion",   TypeKey = "consumables" },
        };
        var handler = new GetItemCatalogQueryHandler(BuildRepo(data));

        var result = await handler.Handle(new GetItemCatalogQuery("consumables"), CancellationToken.None);

        result.Should().HaveCount(2).And.OnlyContain(i => i.TypeKey == "consumables");
    }

    [Fact]
    public async Task Handle_ReturnsEmpty_WhenNoMatchForType()
    {
        var handler = new GetItemCatalogQueryHandler(BuildRepo([]));

        var result = await handler.Handle(new GetItemCatalogQuery("runes"), CancellationToken.None);

        result.Should().BeEmpty();
    }
}
