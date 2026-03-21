using Moq;
using RealmEngine.Core.Features.SpellCatalog.Queries;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.SpellCatalog;

public class GetSpellCatalogQueryHandlerTests
{
    private static Spell MakeSpell(string id = "spell") => new() { SpellId = id };

    private static ISpellRepository BuildRepo(IEnumerable<Spell> data)
    {
        var mock = new Mock<ISpellRepository>();
        mock.Setup(r => r.GetAllAsync()).ReturnsAsync(data.ToList());
        mock.Setup(r => r.GetBySchoolAsync(It.IsAny<string>())).ReturnsAsync(data.ToList());
        return mock.Object;
    }

    [Fact]
    public async Task Handle_NoFilter_ReturnsAllFromRepository()
    {
        var data = new List<Spell> { MakeSpell(), MakeSpell("s2"), MakeSpell("s3") };
        var handler = new GetSpellCatalogQueryHandler(BuildRepo(data));

        var result = await handler.Handle(new GetSpellCatalogQuery(), CancellationToken.None);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_WithFilter_CallsFilteredMethod()
    {
        var mock = new Mock<ISpellRepository>();
        mock.Setup(r => r.GetBySchoolAsync(It.IsAny<string>())).ReturnsAsync([MakeSpell()]);
        var handler = new GetSpellCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetSpellCatalogQuery("fire"), CancellationToken.None);

        mock.Verify(r => r.GetBySchoolAsync(It.IsAny<string>()), Times.Once);
        mock.Verify(r => r.GetAllAsync(), Times.Never);
    }

    [Fact]
    public async Task Handle_WithFilter_PassesFilterValueToRepository()
    {
        var mock = new Mock<ISpellRepository>();
        mock.Setup(r => r.GetBySchoolAsync(It.IsAny<string>())).ReturnsAsync([]);
        var handler = new GetSpellCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetSpellCatalogQuery("arcane"), CancellationToken.None);

        mock.Verify(r => r.GetBySchoolAsync("arcane"), Times.Once);
    }

    [Fact]
    public void Validator_NullFilter_IsValid()
    {
        var result = new GetSpellCatalogQueryValidator().Validate(new GetSpellCatalogQuery(null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_ValidFilter_IsValid()
    {
        var result = new GetSpellCatalogQueryValidator().Validate(new GetSpellCatalogQuery("fire"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_EmptyStringFilter_IsInvalid()
    {
        var result = new GetSpellCatalogQueryValidator().Validate(new GetSpellCatalogQuery(""));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_TooLongFilter_IsInvalid()
    {
        var result = new GetSpellCatalogQueryValidator().Validate(new GetSpellCatalogQuery(new string('x', 101)));
        result.IsValid.Should().BeFalse();
    }
}
