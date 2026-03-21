using Moq;
using RealmEngine.Core.Features.AbilityCatalog.Queries;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.AbilityCatalog;

public class GetAbilityCatalogQueryHandlerTests
{
    private static IAbilityRepository BuildRepo(IEnumerable<Ability> data)
    {
        var mock = new Mock<IAbilityRepository>();
        mock.Setup(r => r.GetAllAsync()).ReturnsAsync(data.ToList());
        mock.Setup(r => r.GetByTypeAsync(It.IsAny<string>())).ReturnsAsync(data.ToList());
        return mock.Object;
    }

    [Fact]
    public async Task Handle_NoFilter_ReturnsAllFromRepository()
    {
        var data = new List<Ability> { new(), new(), new() };
        var handler = new GetAbilityCatalogQueryHandler(BuildRepo(data));

        var result = await handler.Handle(new GetAbilityCatalogQuery(), CancellationToken.None);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_WithFilter_CallsFilteredMethod()
    {
        var mock = new Mock<IAbilityRepository>();
        mock.Setup(r => r.GetByTypeAsync(It.IsAny<string>())).ReturnsAsync([new Ability()]);
        var handler = new GetAbilityCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetAbilityCatalogQuery("active"), CancellationToken.None);

        mock.Verify(r => r.GetByTypeAsync(It.IsAny<string>()), Times.Once);
        mock.Verify(r => r.GetAllAsync(), Times.Never);
    }

    [Fact]
    public async Task Handle_WithFilter_PassesFilterValueToRepository()
    {
        var mock = new Mock<IAbilityRepository>();
        mock.Setup(r => r.GetByTypeAsync(It.IsAny<string>())).ReturnsAsync([]);
        var handler = new GetAbilityCatalogQueryHandler(mock.Object);

        await handler.Handle(new GetAbilityCatalogQuery("passive"), CancellationToken.None);

        mock.Verify(r => r.GetByTypeAsync("passive"), Times.Once);
    }

    [Fact]
    public void Validator_NullFilter_IsValid()
    {
        var result = new GetAbilityCatalogQueryValidator().Validate(new GetAbilityCatalogQuery(null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_ValidFilter_IsValid()
    {
        var result = new GetAbilityCatalogQueryValidator().Validate(new GetAbilityCatalogQuery("active"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_EmptyStringFilter_IsInvalid()
    {
        var result = new GetAbilityCatalogQueryValidator().Validate(new GetAbilityCatalogQuery(""));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_TooLongFilter_IsInvalid()
    {
        var result = new GetAbilityCatalogQueryValidator().Validate(new GetAbilityCatalogQuery(new string('x', 101)));
        result.IsValid.Should().BeFalse();
    }
}
