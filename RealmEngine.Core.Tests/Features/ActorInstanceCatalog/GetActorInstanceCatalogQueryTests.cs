using Moq;
using RealmEngine.Core.Features.ActorInstanceCatalog.Queries;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.ActorInstanceCatalog;

public class GetActorInstanceCatalogQueryHandlerTests
{
    private static ActorInstanceEntry MakeActor() =>
        new("slug", "Display", "enemy", Guid.Empty, null, null, 1);

    private static IActorInstanceRepository BuildRepo(IEnumerable<ActorInstanceEntry> data)
    {
        var mock = new Mock<IActorInstanceRepository>();
        mock.Setup(r => r.GetAllAsync()).ReturnsAsync(data.ToList());
        mock.Setup(r => r.GetByTypeKeyAsync(It.IsAny<string>())).ReturnsAsync(data.ToList());
        return mock.Object;
    }

    [Fact]
    public async Task Handle_NoFilter_ReturnsAllFromRepository()
    {
        var data = new List<ActorInstanceEntry> { MakeActor(), MakeActor(), MakeActor() };
        var handler = new GetActorInstanceCatalogQueryHandler(BuildRepo(data));

        var result = await handler.Handle(new GetActorInstanceCatalogQuery(), CancellationToken.None);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_WithFilter_CallsFilteredMethod()
    {
        var mock = new Mock<IActorInstanceRepository>();
        mock.Setup(r => r.GetByTypeKeyAsync(It.IsAny<string>())).ReturnsAsync([MakeActor()]);
        var handler = new GetActorInstanceCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetActorInstanceCatalogQuery("enemy"), CancellationToken.None);

        mock.Verify(r => r.GetByTypeKeyAsync(It.IsAny<string>()), Times.Once);
        mock.Verify(r => r.GetAllAsync(), Times.Never);
    }

    [Fact]
    public async Task Handle_WithFilter_PassesFilterValueToRepository()
    {
        var mock = new Mock<IActorInstanceRepository>();
        mock.Setup(r => r.GetByTypeKeyAsync(It.IsAny<string>())).ReturnsAsync([]);
        var handler = new GetActorInstanceCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetActorInstanceCatalogQuery("npc"), CancellationToken.None);

        mock.Verify(r => r.GetByTypeKeyAsync("npc"), Times.Once);
    }

    [Fact]
    public void Validator_NullFilter_IsValid()
    {
        var result = new GetActorInstanceCatalogQueryValidator().Validate(new GetActorInstanceCatalogQuery(null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_ValidFilter_IsValid()
    {
        var result = new GetActorInstanceCatalogQueryValidator().Validate(new GetActorInstanceCatalogQuery("enemy"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_EmptyStringFilter_IsInvalid()
    {
        var result = new GetActorInstanceCatalogQueryValidator().Validate(new GetActorInstanceCatalogQuery(""));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_TooLongFilter_IsInvalid()
    {
        var result = new GetActorInstanceCatalogQueryValidator().Validate(new GetActorInstanceCatalogQuery(new string('x', 101)));
        result.IsValid.Should().BeFalse();
    }
}
