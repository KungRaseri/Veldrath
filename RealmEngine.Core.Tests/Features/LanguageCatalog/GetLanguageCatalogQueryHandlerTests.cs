using Moq;
using RealmEngine.Core.Features.LanguageCatalog.Queries;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.LanguageCatalog;

public class GetLanguageCatalogQueryHandlerTests
{
    private static Language MakeLanguage(string slug = "calethic", string typeKey = "imperial") =>
        new() { Slug = slug, DisplayName = slug, TypeKey = typeKey };

    private static ILanguageRepository BuildRepo(IEnumerable<Language> data)
    {
        var mock = new Mock<ILanguageRepository>();
        mock.Setup(r => r.GetAllAsync()).ReturnsAsync(data.ToList());
        mock.Setup(r => r.GetByTypeKeyAsync(It.IsAny<string>())).ReturnsAsync(data.ToList());
        return mock.Object;
    }

    [Fact]
    public async Task Handle_NoFilter_ReturnsAllFromRepository()
    {
        var data = new List<Language> { MakeLanguage(), MakeLanguage("elvish", "elven"), MakeLanguage("orcish", "orcish") };
        var handler = new GetLanguageCatalogQueryHandler(BuildRepo(data));

        var result = await handler.Handle(new GetLanguageCatalogQuery(), CancellationToken.None);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_WithFilter_CallsFilteredMethod()
    {
        var mock = new Mock<ILanguageRepository>();
        mock.Setup(r => r.GetByTypeKeyAsync(It.IsAny<string>())).ReturnsAsync([MakeLanguage()]);
        var handler = new GetLanguageCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetLanguageCatalogQuery("imperial"), CancellationToken.None);

        mock.Verify(r => r.GetByTypeKeyAsync(It.IsAny<string>()), Times.Once);
        mock.Verify(r => r.GetAllAsync(), Times.Never);
    }

    [Fact]
    public async Task Handle_WithFilter_PassesFilterValueToRepository()
    {
        var mock = new Mock<ILanguageRepository>();
        mock.Setup(r => r.GetByTypeKeyAsync(It.IsAny<string>())).ReturnsAsync([]);
        var handler = new GetLanguageCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetLanguageCatalogQuery("elven"), CancellationToken.None);

        mock.Verify(r => r.GetByTypeKeyAsync("elven"), Times.Once);
    }

    [Fact]
    public async Task Handle_EmptyRepository_ReturnsEmptyList()
    {
        var handler = new GetLanguageCatalogQueryHandler(BuildRepo([]));

        var result = await handler.Handle(new GetLanguageCatalogQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Validator_NullFilter_IsValid()
    {
        var result = new GetLanguageCatalogQueryValidator().Validate(new GetLanguageCatalogQuery(null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_ValidFilter_IsValid()
    {
        var result = new GetLanguageCatalogQueryValidator().Validate(new GetLanguageCatalogQuery("imperial"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_EmptyStringFilter_IsInvalid()
    {
        var result = new GetLanguageCatalogQueryValidator().Validate(new GetLanguageCatalogQuery(""));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_TooLongFilter_IsInvalid()
    {
        var result = new GetLanguageCatalogQueryValidator().Validate(new GetLanguageCatalogQuery(new string('x', 101)));
        result.IsValid.Should().BeFalse();
    }
}
