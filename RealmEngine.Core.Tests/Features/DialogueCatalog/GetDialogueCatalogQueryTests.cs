using Moq;
using RealmEngine.Core.Features.DialogueCatalog.Queries;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.DialogueCatalog;

public class GetDialogueCatalogQueryHandlerTests
{
    private static DialogueEntry MakeDialogue() =>
        new("slug", "Display", "npc", "Elder", 1, []);

    private static IDialogueRepository BuildRepo(IEnumerable<DialogueEntry> data)
    {
        var mock = new Mock<IDialogueRepository>();
        mock.Setup(r => r.GetAllAsync()).ReturnsAsync(data.ToList());
        mock.Setup(r => r.GetBySpeakerAsync(It.IsAny<string>())).ReturnsAsync(data.ToList());
        return mock.Object;
    }

    [Fact]
    public async Task Handle_NoFilter_ReturnsAllFromRepository()
    {
        var data = new List<DialogueEntry> { MakeDialogue(), MakeDialogue(), MakeDialogue() };
        var handler = new GetDialogueCatalogQueryHandler(BuildRepo(data));

        var result = await handler.Handle(new GetDialogueCatalogQuery(), CancellationToken.None);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_WithFilter_CallsFilteredMethod()
    {
        var mock = new Mock<IDialogueRepository>();
        mock.Setup(r => r.GetBySpeakerAsync(It.IsAny<string>())).ReturnsAsync([MakeDialogue()]);
        var handler = new GetDialogueCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetDialogueCatalogQuery("Elder"), CancellationToken.None);

        mock.Verify(r => r.GetBySpeakerAsync(It.IsAny<string>()), Times.Once);
        mock.Verify(r => r.GetAllAsync(), Times.Never);
    }

    [Fact]
    public async Task Handle_WithFilter_PassesFilterValueToRepository()
    {
        var mock = new Mock<IDialogueRepository>();
        mock.Setup(r => r.GetBySpeakerAsync(It.IsAny<string>())).ReturnsAsync([]);
        var handler = new GetDialogueCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetDialogueCatalogQuery("Merchant"), CancellationToken.None);

        mock.Verify(r => r.GetBySpeakerAsync("Merchant"), Times.Once);
    }

    [Fact]
    public void Validator_NullFilter_IsValid()
    {
        var result = new GetDialogueCatalogQueryValidator().Validate(new GetDialogueCatalogQuery(null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_ValidFilter_IsValid()
    {
        var result = new GetDialogueCatalogQueryValidator().Validate(new GetDialogueCatalogQuery("Elder"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_EmptyStringFilter_IsInvalid()
    {
        var result = new GetDialogueCatalogQueryValidator().Validate(new GetDialogueCatalogQuery(""));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_TooLongFilter_IsInvalid()
    {
        var result = new GetDialogueCatalogQueryValidator().Validate(new GetDialogueCatalogQuery(new string('x', 101)));
        result.IsValid.Should().BeFalse();
    }
}
