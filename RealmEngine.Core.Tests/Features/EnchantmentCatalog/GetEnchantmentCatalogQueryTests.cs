using Moq;
using RealmEngine.Core.Features.EnchantmentCatalog.Queries;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.EnchantmentCatalog;

public class GetEnchantmentCatalogQueryHandlerTests
{
    private static IEnchantmentRepository BuildRepo(IEnumerable<Enchantment> data)
    {
        var mock = new Mock<IEnchantmentRepository>();
        mock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(data.ToList());
        mock.Setup(r => r.GetByTargetSlotAsync(It.IsAny<string>()))
            .ReturnsAsync((string s) => data.Where(e => e.Slug.Contains(s, StringComparison.OrdinalIgnoreCase)).ToList());
        return mock.Object;
    }

    [Fact]
    public async Task Handle_ReturnsAllEnchantments_WhenNoFilterGiven()
    {
        var data = new List<Enchantment>
        {
            new() { Slug = "sharpness",  DisplayName = "Sharpness" },
            new() { Slug = "protection", DisplayName = "Protection" },
        };
        var handler = new GetEnchantmentCatalogQueryHandler(BuildRepo(data));

        var result = await handler.Handle(new GetEnchantmentCatalogQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_DelegatesToGetByTargetSlot_WhenFilterProvided()
    {
        var mock = new Mock<IEnchantmentRepository>();
        var returned = new List<Enchantment> { new() { Slug = "sharpness" } };
        mock.Setup(r => r.GetByTargetSlotAsync("weapon")).ReturnsAsync(returned);
        var handler = new GetEnchantmentCatalogQueryHandler(mock.Object);

        var result = await handler.Handle(new GetEnchantmentCatalogQuery("weapon"), CancellationToken.None);

        result.Should().HaveCount(1);
        mock.Verify(r => r.GetByTargetSlotAsync("weapon"), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsEmpty_WhenNoEnchantments()
    {
        var handler = new GetEnchantmentCatalogQueryHandler(BuildRepo([]));

        var result = await handler.Handle(new GetEnchantmentCatalogQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }
}
