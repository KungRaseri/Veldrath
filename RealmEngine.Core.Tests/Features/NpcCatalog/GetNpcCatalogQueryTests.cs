using Moq;
using RealmEngine.Core.Features.NpcCatalog.Queries;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.NpcCatalog;

public class GetNpcCatalogQueryHandlerTests
{
    private static INpcRepository BuildRepo(IEnumerable<NPC> data)
    {
        var mock = new Mock<INpcRepository>();
        mock.Setup(r => r.GetAllAsync()).ReturnsAsync(data.ToList());
        mock.Setup(r => r.GetByCategoryAsync(It.IsAny<string>())).ReturnsAsync(data.ToList());
        return mock.Object;
    }

    [Fact]
    public async Task Handle_NoFilter_ReturnsAllFromRepository()
    {
        var data = new List<NPC> { new(), new(), new() };
        var handler = new GetNpcCatalogQueryHandler(BuildRepo(data));

        var result = await handler.Handle(new GetNpcCatalogQuery(), CancellationToken.None);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_WithFilter_CallsFilteredMethod()
    {
        var mock = new Mock<INpcRepository>();
        mock.Setup(r => r.GetByCategoryAsync(It.IsAny<string>())).ReturnsAsync([new NPC()]);
        var handler = new GetNpcCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetNpcCatalogQuery("merchants"), CancellationToken.None);

        mock.Verify(r => r.GetByCategoryAsync(It.IsAny<string>()), Times.Once);
        mock.Verify(r => r.GetAllAsync(), Times.Never);
    }

    [Fact]
    public async Task Handle_WithFilter_PassesFilterValueToRepository()
    {
        var mock = new Mock<INpcRepository>();
        mock.Setup(r => r.GetByCategoryAsync(It.IsAny<string>())).ReturnsAsync([]);
        var handler = new GetNpcCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetNpcCatalogQuery("guards"), CancellationToken.None);

        mock.Verify(r => r.GetByCategoryAsync("guards"), Times.Once);
    }

    [Fact]
    public void Validator_NullFilter_IsValid()
    {
        var result = new GetNpcCatalogQueryValidator().Validate(new GetNpcCatalogQuery(null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_ValidFilter_IsValid()
    {
        var result = new GetNpcCatalogQueryValidator().Validate(new GetNpcCatalogQuery("merchants"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_EmptyStringFilter_IsInvalid()
    {
        var result = new GetNpcCatalogQueryValidator().Validate(new GetNpcCatalogQuery(""));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_TooLongFilter_IsInvalid()
    {
        var result = new GetNpcCatalogQueryValidator().Validate(new GetNpcCatalogQuery(new string('x', 101)));
        result.IsValid.Should().BeFalse();
    }
}
