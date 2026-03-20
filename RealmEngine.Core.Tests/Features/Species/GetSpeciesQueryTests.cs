using Moq;
using RealmEngine.Core.Features.Species.Queries;
using RealmEngine.Shared.Abstractions;
using SharedSpecies = RealmEngine.Shared.Models.Species;

namespace RealmEngine.Core.Tests.Features.Species;

public class GetSpeciesQueryHandlerTests
{
    private static ISpeciesRepository BuildRepo(IEnumerable<SharedSpecies> data)
    {
        var mock = new Mock<ISpeciesRepository>();
        mock.Setup(r => r.GetAllSpeciesAsync())
            .ReturnsAsync(data.ToList());
        mock.Setup(r => r.GetSpeciesByTypeAsync(It.IsAny<string>()))
            .ReturnsAsync((string t) => data.Where(s => s.TypeKey == t).ToList());
        return mock.Object;
    }

    [Fact]
    public async Task Handle_ReturnsAllSpecies_WhenNoFilterGiven()
    {
        var data = new List<SharedSpecies>
        {
            new() { Slug = "human",  TypeKey = "humanoid" },
            new() { Slug = "wolf",   TypeKey = "beast" },
        };
        var handler = new GetSpeciesQueryHandler(BuildRepo(data));

        var result = await handler.Handle(new GetSpeciesQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_FiltersOnTypeKey_WhenProvided()
    {
        var data = new List<SharedSpecies>
        {
            new() { Slug = "human", TypeKey = "humanoid" },
            new() { Slug = "wolf",  TypeKey = "beast" },
            new() { Slug = "elf",   TypeKey = "humanoid" },
        };
        var handler = new GetSpeciesQueryHandler(BuildRepo(data));

        var result = await handler.Handle(new GetSpeciesQuery("humanoid"), CancellationToken.None);

        result.Should().HaveCount(2).And.OnlyContain(s => s.TypeKey == "humanoid");
    }

    [Fact]
    public async Task Handle_ReturnsEmpty_WhenNoMatchForType()
    {
        var handler = new GetSpeciesQueryHandler(BuildRepo([]));

        var result = await handler.Handle(new GetSpeciesQuery("dragon"), CancellationToken.None);

        result.Should().BeEmpty();
    }
}
