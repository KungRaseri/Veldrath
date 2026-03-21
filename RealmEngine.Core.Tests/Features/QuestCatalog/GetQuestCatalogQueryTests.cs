using Moq;
using RealmEngine.Core.Features.QuestCatalog.Queries;
using RealmEngine.Shared.Abstractions;
using SharedQuest = RealmEngine.Shared.Models.Quest;

namespace RealmEngine.Core.Tests.Features.QuestCatalog;

public class GetQuestCatalogQueryHandlerTests
{
    private static IQuestRepository BuildRepo(IEnumerable<SharedQuest> data)
    {
        var mock = new Mock<IQuestRepository>();
        mock.Setup(r => r.GetAllAsync()).ReturnsAsync(data.ToList());
        mock.Setup(r => r.GetByTypeKeyAsync(It.IsAny<string>())).ReturnsAsync(data.ToList());
        return mock.Object;
    }

    [Fact]
    public async Task Handle_NoFilter_ReturnsAllFromRepository()
    {
        var data = new List<SharedQuest> { new(), new(), new() };
        var handler = new GetQuestCatalogQueryHandler(BuildRepo(data));

        var result = await handler.Handle(new GetQuestCatalogQuery(), CancellationToken.None);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_WithFilter_CallsFilteredMethod()
    {
        var mock = new Mock<IQuestRepository>();
        mock.Setup(r => r.GetByTypeKeyAsync(It.IsAny<string>())).ReturnsAsync([new SharedQuest()]);
        var handler = new GetQuestCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetQuestCatalogQuery("main-story"), CancellationToken.None);

        mock.Verify(r => r.GetByTypeKeyAsync(It.IsAny<string>()), Times.Once);
        mock.Verify(r => r.GetAllAsync(), Times.Never);
    }

    [Fact]
    public async Task Handle_WithFilter_PassesFilterValueToRepository()
    {
        var mock = new Mock<IQuestRepository>();
        mock.Setup(r => r.GetByTypeKeyAsync(It.IsAny<string>())).ReturnsAsync([]);
        var handler = new GetQuestCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetQuestCatalogQuery("side"), CancellationToken.None);

        mock.Verify(r => r.GetByTypeKeyAsync("side"), Times.Once);
    }

    [Fact]
    public void Validator_NullFilter_IsValid()
    {
        var result = new GetQuestCatalogQueryValidator().Validate(new GetQuestCatalogQuery(null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_ValidFilter_IsValid()
    {
        var result = new GetQuestCatalogQueryValidator().Validate(new GetQuestCatalogQuery("main-story"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_EmptyStringFilter_IsInvalid()
    {
        var result = new GetQuestCatalogQueryValidator().Validate(new GetQuestCatalogQuery(""));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_TooLongFilter_IsInvalid()
    {
        var result = new GetQuestCatalogQueryValidator().Validate(new GetQuestCatalogQuery(new string('x', 101)));
        result.IsValid.Should().BeFalse();
    }
}
