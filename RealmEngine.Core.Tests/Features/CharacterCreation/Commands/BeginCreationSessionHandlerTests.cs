using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.CharacterCreation.Commands;
using RealmEngine.Core.Features.CharacterCreation.Queries;
using RealmEngine.Core.Features.Exploration.Queries;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using SpeciesModel = RealmEngine.Shared.Models.Species;

namespace RealmEngine.Core.Tests.Features.CharacterCreation.Commands;

[Trait("Category", "Feature")]
public class BeginCreationSessionHandlerTests
{
    private readonly Mock<ICharacterCreationSessionStore> _sessionStoreMock = new();
    private readonly Mock<ISpeciesRepository>             _speciesRepoMock  = new();
    private readonly Mock<IBackgroundRepository>          _backgroundRepoMock = new();
    private readonly Mock<IMediator>                      _mediatorMock     = new();

    private BeginCreationSessionHandler CreateHandler() =>
        new(_sessionStoreMock.Object, _speciesRepoMock.Object, _backgroundRepoMock.Object,
            _mediatorMock.Object, NullLogger<BeginCreationSessionHandler>.Instance);

    [Fact]
    public async Task Handle_CreatesSession_ReturnsSuccessWithSessionId()
    {
        var session = new CharacterCreationSession();
        _sessionStoreMock.Setup(s => s.CreateSessionAsync()).ReturnsAsync(session);
        SetupDefaultDependencies(classes: [], species: [], backgrounds: [], locations: []);

        var result = await CreateHandler().Handle(new BeginCreationSessionCommand(), default);

        result.Success.Should().BeTrue();
        result.SessionId.Should().Be(session.SessionId);
    }

    [Fact]
    public async Task Handle_ReturnsPointBuyConfig()
    {
        _sessionStoreMock.Setup(s => s.CreateSessionAsync()).ReturnsAsync(new CharacterCreationSession());
        SetupDefaultDependencies(classes: [], species: [], backgrounds: [], locations: []);

        var result = await CreateHandler().Handle(new BeginCreationSessionCommand(), default);

        result.PointBuyConfig.Should().NotBeNull();
        result.PointBuyConfig.TotalPoints.Should().Be(27);
    }

    [Fact]
    public async Task Handle_ReturnsAvailableClasses_FromMediator()
    {
        var classes = new List<CharacterClass> { new() { Name = "Warrior" }, new() { Name = "Mage" } };
        _sessionStoreMock.Setup(s => s.CreateSessionAsync()).ReturnsAsync(new CharacterCreationSession());
        SetupDefaultDependencies(classes: classes, species: [], backgrounds: [], locations: []);

        var result = await CreateHandler().Handle(new BeginCreationSessionCommand(), default);

        result.AvailableClasses.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_ReturnsAvailableSpecies_FromRepository()
    {
        var species = new List<SpeciesModel> { new() { Slug = "human" }, new() { Slug = "elf" } };
        _sessionStoreMock.Setup(s => s.CreateSessionAsync()).ReturnsAsync(new CharacterCreationSession());
        SetupDefaultDependencies(classes: [], species: species, backgrounds: [], locations: []);

        var result = await CreateHandler().Handle(new BeginCreationSessionCommand(), default);

        result.AvailableSpecies.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_ReturnsAvailableBackgrounds_FromRepository()
    {
        var backgrounds = new List<Background> { new() { Name = "Soldier" } };
        _sessionStoreMock.Setup(s => s.CreateSessionAsync()).ReturnsAsync(new CharacterCreationSession());
        SetupDefaultDependencies(classes: [], species: [], backgrounds: backgrounds, locations: []);

        var result = await CreateHandler().Handle(new BeginCreationSessionCommand(), default);

        result.AvailableBackgrounds.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_ReturnsAvailableLocations_FromMediator()
    {
        var locations = new List<Location>
        {
            new() { Id = "loc-1", Name = "Village",  Description = "A village",  Type = "town" },
            new() { Id = "loc-2", Name = "Outpost",  Description = "An outpost", Type = "town" },
        };
        _sessionStoreMock.Setup(s => s.CreateSessionAsync()).ReturnsAsync(new CharacterCreationSession());
        SetupDefaultDependencies(classes: [], species: [], backgrounds: [], locations: locations);

        var result = await CreateHandler().Handle(new BeginCreationSessionCommand(), default);

        result.AvailableLocations.Should().HaveCount(2);
    }

    private void SetupDefaultDependencies(
        List<CharacterClass> classes,
        List<SpeciesModel> species,
        List<Background> backgrounds,
        List<Location> locations)
    {
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetAvailableClassesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetAvailableClassesResult { Success = true, Classes = classes });

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetStartingLocationsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(locations);

        _speciesRepoMock
            .Setup(r => r.GetAllSpeciesAsync())
            .ReturnsAsync(species);

        _backgroundRepoMock
            .Setup(r => r.GetAllBackgroundsAsync())
            .ReturnsAsync(backgrounds);
    }
}
