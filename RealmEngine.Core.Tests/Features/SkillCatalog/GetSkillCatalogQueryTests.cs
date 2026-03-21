using Moq;
using RealmEngine.Core.Features.SkillCatalog.Queries;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.SkillCatalog;

public class GetSkillCatalogQueryHandlerTests
{
    private static SkillDefinition MakeSkillDef(string id = "skill", string category = "combat") =>
        new() { SkillId = id, Name = id, DisplayName = id, Description = id, Category = category };

    private static ISkillRepository BuildRepo(IEnumerable<SkillDefinition> data)
    {
        var mock = new Mock<ISkillRepository>();
        mock.Setup(r => r.GetAllAsync()).ReturnsAsync(data.ToList());
        mock.Setup(r => r.GetByCategoryAsync(It.IsAny<string>())).ReturnsAsync(data.ToList());
        return mock.Object;
    }

    [Fact]
    public async Task Handle_NoFilter_ReturnsAllFromRepository()
    {
        var data = new List<SkillDefinition> { MakeSkillDef(), MakeSkillDef("s2"), MakeSkillDef("s3") };
        var handler = new GetSkillCatalogQueryHandler(BuildRepo(data));

        var result = await handler.Handle(new GetSkillCatalogQuery(), CancellationToken.None);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_WithFilter_CallsFilteredMethod()
    {
        var mock = new Mock<ISkillRepository>();
        mock.Setup(r => r.GetByCategoryAsync(It.IsAny<string>())).ReturnsAsync([MakeSkillDef()]);
        var handler = new GetSkillCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetSkillCatalogQuery("combat"), CancellationToken.None);

        mock.Verify(r => r.GetByCategoryAsync(It.IsAny<string>()), Times.Once);
        mock.Verify(r => r.GetAllAsync(), Times.Never);
    }

    [Fact]
    public async Task Handle_WithFilter_PassesFilterValueToRepository()
    {
        var mock = new Mock<ISkillRepository>();
        mock.Setup(r => r.GetByCategoryAsync(It.IsAny<string>())).ReturnsAsync([]);
        var handler = new GetSkillCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetSkillCatalogQuery("stealth"), CancellationToken.None);

        mock.Verify(r => r.GetByCategoryAsync("stealth"), Times.Once);
    }

    [Fact]
    public void Validator_NullFilter_IsValid()
    {
        var result = new GetSkillCatalogQueryValidator().Validate(new GetSkillCatalogQuery(null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_ValidFilter_IsValid()
    {
        var result = new GetSkillCatalogQueryValidator().Validate(new GetSkillCatalogQuery("combat"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_EmptyStringFilter_IsInvalid()
    {
        var result = new GetSkillCatalogQueryValidator().Validate(new GetSkillCatalogQuery(""));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_TooLongFilter_IsInvalid()
    {
        var result = new GetSkillCatalogQueryValidator().Validate(new GetSkillCatalogQuery(new string('x', 101)));
        result.IsValid.Should().BeFalse();
    }
}
