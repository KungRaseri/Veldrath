using Moq;
using RealmEngine.Core.Features.LootTableCatalog.Queries;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.LootTableCatalog;

public class GetLootTableCatalogQueryHandlerTests
{
    private static ILootTableRepository BuildRepo(IEnumerable<LootTableData> data)
    {
        var mock = new Mock<ILootTableRepository>();
        mock.Setup(r => r.GetAllAsync()).ReturnsAsync(data.ToList());
        mock.Setup(r => r.GetByContextAsync(It.IsAny<string>())).ReturnsAsync(data.ToList());
        return mock.Object;
    }

    [Fact]
    public async Task Handle_NoFilter_ReturnsAllFromRepository()
    {
        var data = new List<LootTableData> { new(), new(), new() };
        var handler = new GetLootTableCatalogQueryHandler(BuildRepo(data));

        var result = await handler.Handle(new GetLootTableCatalogQuery(), CancellationToken.None);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_WithFilter_CallsFilteredMethod()
    {
        var mock = new Mock<ILootTableRepository>();
        mock.Setup(r => r.GetByContextAsync(It.IsAny<string>())).ReturnsAsync([new LootTableData()]);
        var handler = new GetLootTableCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetLootTableCatalogQuery("enemies"), CancellationToken.None);

        mock.Verify(r => r.GetByContextAsync(It.IsAny<string>()), Times.Once);
        mock.Verify(r => r.GetAllAsync(), Times.Never);
    }

    [Fact]
    public async Task Handle_WithFilter_PassesFilterValueToRepository()
    {
        var mock = new Mock<ILootTableRepository>();
        mock.Setup(r => r.GetByContextAsync(It.IsAny<string>())).ReturnsAsync([]);
        var handler = new GetLootTableCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetLootTableCatalogQuery("chests"), CancellationToken.None);

        mock.Verify(r => r.GetByContextAsync("chests"), Times.Once);
    }

    [Fact]
    public void Validator_NullFilter_IsValid()
    {
        var result = new GetLootTableCatalogQueryValidator().Validate(new GetLootTableCatalogQuery(null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_ValidFilter_IsValid()
    {
        var result = new GetLootTableCatalogQueryValidator().Validate(new GetLootTableCatalogQuery("enemies"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_EmptyStringFilter_IsInvalid()
    {
        var result = new GetLootTableCatalogQueryValidator().Validate(new GetLootTableCatalogQuery(""));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_TooLongFilter_IsInvalid()
    {
        var result = new GetLootTableCatalogQueryValidator().Validate(new GetLootTableCatalogQuery(new string('x', 101)));
        result.IsValid.Should().BeFalse();
    }
}
