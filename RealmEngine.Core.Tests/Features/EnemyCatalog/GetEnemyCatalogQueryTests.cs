using Moq;
using RealmEngine.Core.Features.EnemyCatalog.Queries;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.EnemyCatalog;

public class GetEnemyCatalogQueryHandlerTests
{
    private static IEnemyRepository BuildRepo(IEnumerable<Enemy> data)
    {
        var mock = new Mock<IEnemyRepository>();
        mock.Setup(r => r.GetAllAsync()).ReturnsAsync(data.ToList());
        mock.Setup(r => r.GetByFamilyAsync(It.IsAny<string>())).ReturnsAsync(data.ToList());
        return mock.Object;
    }

    [Fact]
    public async Task Handle_NoFilter_ReturnsAllFromRepository()
    {
        var data = new List<Enemy> { new(), new(), new() };
        var handler = new GetEnemyCatalogQueryHandler(BuildRepo(data));

        var result = await handler.Handle(new GetEnemyCatalogQuery(), CancellationToken.None);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_WithFilter_CallsFilteredMethod()
    {
        var mock = new Mock<IEnemyRepository>();
        mock.Setup(r => r.GetByFamilyAsync(It.IsAny<string>())).ReturnsAsync([new Enemy()]);
        var handler = new GetEnemyCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetEnemyCatalogQuery("wolves"), CancellationToken.None);

        mock.Verify(r => r.GetByFamilyAsync(It.IsAny<string>()), Times.Once);
        mock.Verify(r => r.GetAllAsync(), Times.Never);
    }

    [Fact]
    public async Task Handle_WithFilter_PassesFilterValueToRepository()
    {
        var mock = new Mock<IEnemyRepository>();
        mock.Setup(r => r.GetByFamilyAsync(It.IsAny<string>())).ReturnsAsync([]);
        var handler = new GetEnemyCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetEnemyCatalogQuery("wolves"), CancellationToken.None);

        mock.Verify(r => r.GetByFamilyAsync("wolves"), Times.Once);
    }

    [Fact]
    public void Validator_NullFilter_IsValid()
    {
        var result = new GetEnemyCatalogQueryValidator().Validate(new GetEnemyCatalogQuery(null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_ValidFilter_IsValid()
    {
        var result = new GetEnemyCatalogQueryValidator().Validate(new GetEnemyCatalogQuery("wolves"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_EmptyStringFilter_IsInvalid()
    {
        var result = new GetEnemyCatalogQueryValidator().Validate(new GetEnemyCatalogQuery(""));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_TooLongFilter_IsInvalid()
    {
        var result = new GetEnemyCatalogQueryValidator().Validate(new GetEnemyCatalogQuery(new string('x', 101)));
        result.IsValid.Should().BeFalse();
    }
}
