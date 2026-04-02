using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.CharacterCreation.Commands;
using RealmEngine.Core.Features.CharacterCreation.Queries;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using SpeciesModel = RealmEngine.Shared.Models.Species;

namespace RealmEngine.Core.Tests.Features.CharacterCreation.Commands;

[Trait("Category", "Feature")]
public class SetCreationChoiceCommandHandlerTests
{
    private readonly Mock<ICharacterCreationSessionStore> _sessionStoreMock      = new();
    private readonly Mock<IMediator>                      _mediatorMock          = new();
    private readonly Mock<ISpeciesRepository>             _speciesRepoMock       = new();
    private readonly Mock<IBackgroundRepository>          _backgroundRepoMock    = new();
    private readonly Mock<IZoneLocationRepository>        _zoneLocationRepoMock  = new();

    private static CharacterCreationSession MakeSession() => new();

    // ----- SetCreationClassHandler -----

    [Fact]
    public async Task SetCreationClass_SessionNotFound_ReturnsFailed()
    {
        _sessionStoreMock.Setup(s => s.GetSessionAsync(It.IsAny<Guid>())).ReturnsAsync((CharacterCreationSession?)null);
        var handler = new SetCreationClassHandler(_sessionStoreMock.Object, _mediatorMock.Object,
            NullLogger<SetCreationClassHandler>.Instance);

        var result = await handler.Handle(new SetCreationClassCommand(Guid.NewGuid(), "warrior"), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task SetCreationClass_ClassNotFound_ReturnsFailed()
    {
        _sessionStoreMock.Setup(s => s.GetSessionAsync(It.IsAny<Guid>())).ReturnsAsync(MakeSession());
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetCharacterClassQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetCharacterClassResult { Found = false });
        var handler = new SetCreationClassHandler(_sessionStoreMock.Object, _mediatorMock.Object,
            NullLogger<SetCreationClassHandler>.Instance);

        var result = await handler.Handle(new SetCreationClassCommand(Guid.NewGuid(), "unknown"), default);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task SetCreationClass_Valid_SetsClassAndReturnsSuccess()
    {
        var session = MakeSession();
        var cls     = new CharacterClass { Name = "Warrior" };
        _sessionStoreMock.Setup(s => s.GetSessionAsync(It.IsAny<Guid>())).ReturnsAsync(session);
        _sessionStoreMock.Setup(s => s.UpdateSessionAsync(session)).Returns(Task.CompletedTask);
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetCharacterClassQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetCharacterClassResult { Found = true, CharacterClass = cls });
        var handler = new SetCreationClassHandler(_sessionStoreMock.Object, _mediatorMock.Object,
            NullLogger<SetCreationClassHandler>.Instance);

        var result = await handler.Handle(new SetCreationClassCommand(session.SessionId, "warrior"), default);

        result.Success.Should().BeTrue();
        session.SelectedClass.Should().Be(cls);
    }

    // ----- SetCreationNameHandler -----

    [Fact]
    public async Task SetCreationName_EmptyName_ReturnsFailed()
    {
        var handler = new SetCreationNameHandler(_sessionStoreMock.Object,
            NullLogger<SetCreationNameHandler>.Instance);

        var result = await handler.Handle(new SetCreationNameCommand(Guid.NewGuid(), "  "), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("empty");
    }

    [Fact]
    public async Task SetCreationName_SessionNotFound_ReturnsFailed()
    {
        _sessionStoreMock.Setup(s => s.GetSessionAsync(It.IsAny<Guid>())).ReturnsAsync((CharacterCreationSession?)null);
        var handler = new SetCreationNameHandler(_sessionStoreMock.Object,
            NullLogger<SetCreationNameHandler>.Instance);

        var result = await handler.Handle(new SetCreationNameCommand(Guid.NewGuid(), "Hero"), default);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task SetCreationName_Valid_SetsNameAndReturnsSuccess()
    {
        var session = MakeSession();
        _sessionStoreMock.Setup(s => s.GetSessionAsync(It.IsAny<Guid>())).ReturnsAsync(session);
        _sessionStoreMock.Setup(s => s.UpdateSessionAsync(session)).Returns(Task.CompletedTask);
        var handler = new SetCreationNameHandler(_sessionStoreMock.Object,
            NullLogger<SetCreationNameHandler>.Instance);

        var result = await handler.Handle(new SetCreationNameCommand(session.SessionId, "Heroic"), default);

        result.Success.Should().BeTrue();
        session.CharacterName.Should().Be("Heroic");
    }

    // ----- SetCreationSpeciesHandler -----

    [Fact]
    public async Task SetCreationSpecies_SessionNotFound_ReturnsFailed()
    {
        _sessionStoreMock.Setup(s => s.GetSessionAsync(It.IsAny<Guid>())).ReturnsAsync((CharacterCreationSession?)null);
        var handler = new SetCreationSpeciesHandler(_sessionStoreMock.Object, _speciesRepoMock.Object,
            NullLogger<SetCreationSpeciesHandler>.Instance);

        var result = await handler.Handle(new SetCreationSpeciesCommand(Guid.NewGuid(), "human"), default);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task SetCreationSpecies_SpeciesNotFound_ReturnsFailed()
    {
        _sessionStoreMock.Setup(s => s.GetSessionAsync(It.IsAny<Guid>())).ReturnsAsync(MakeSession());
        _speciesRepoMock.Setup(r => r.GetSpeciesBySlugAsync("unknown")).ReturnsAsync((SpeciesModel?)null);
        var handler = new SetCreationSpeciesHandler(_sessionStoreMock.Object, _speciesRepoMock.Object,
            NullLogger<SetCreationSpeciesHandler>.Instance);

        var result = await handler.Handle(new SetCreationSpeciesCommand(Guid.NewGuid(), "unknown"), default);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task SetCreationSpecies_Valid_SetsSpeciesAndReturnsSuccess()
    {
        var session = MakeSession();
        var species = new SpeciesModel { Slug = "human", DisplayName = "Human" };
        _sessionStoreMock.Setup(s => s.GetSessionAsync(It.IsAny<Guid>())).ReturnsAsync(session);
        _sessionStoreMock.Setup(s => s.UpdateSessionAsync(session)).Returns(Task.CompletedTask);
        _speciesRepoMock.Setup(r => r.GetSpeciesBySlugAsync("human")).ReturnsAsync(species);
        var handler = new SetCreationSpeciesHandler(_sessionStoreMock.Object, _speciesRepoMock.Object,
            NullLogger<SetCreationSpeciesHandler>.Instance);

        var result = await handler.Handle(new SetCreationSpeciesCommand(session.SessionId, "human"), default);

        result.Success.Should().BeTrue();
        session.SelectedSpecies.Should().Be(species);
    }

    // ----- SetCreationBackgroundHandler -----

    [Fact]
    public async Task SetCreationBackground_SessionNotFound_ReturnsFailed()
    {
        _sessionStoreMock.Setup(s => s.GetSessionAsync(It.IsAny<Guid>())).ReturnsAsync((CharacterCreationSession?)null);
        var handler = new SetCreationBackgroundHandler(_sessionStoreMock.Object, _backgroundRepoMock.Object,
            NullLogger<SetCreationBackgroundHandler>.Instance);

        var result = await handler.Handle(new SetCreationBackgroundCommand(Guid.NewGuid(), "bg-id"), default);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task SetCreationBackground_Valid_SetsBackgroundAndReturnsSuccess()
    {
        var session    = MakeSession();
        var background = new Background { Name = "Soldier", PrimaryAttribute = "Strength", Slug = "soldier" };
        _sessionStoreMock.Setup(s => s.GetSessionAsync(It.IsAny<Guid>())).ReturnsAsync(session);
        _sessionStoreMock.Setup(s => s.UpdateSessionAsync(session)).Returns(Task.CompletedTask);
        _backgroundRepoMock.Setup(r => r.GetBackgroundByIdAsync(It.IsAny<string>())).ReturnsAsync(background);
        var handler = new SetCreationBackgroundHandler(_sessionStoreMock.Object, _backgroundRepoMock.Object,
            NullLogger<SetCreationBackgroundHandler>.Instance);

        var result = await handler.Handle(new SetCreationBackgroundCommand(session.SessionId, "bg-id"), default);

        result.Success.Should().BeTrue();
        session.SelectedBackground.Should().Be(background);
    }

    // ----- SetCreationEquipmentPreferencesHandler -----

    [Fact]
    public async Task SetCreationEquipmentPreferences_SessionNotFound_ReturnsFailed()
    {
        _sessionStoreMock.Setup(s => s.GetSessionAsync(It.IsAny<Guid>())).ReturnsAsync((CharacterCreationSession?)null);
        var handler = new SetCreationEquipmentPreferencesHandler(_sessionStoreMock.Object);

        var result = await handler.Handle(
            new SetCreationEquipmentPreferencesCommand(Guid.NewGuid(), "light", "sword", true), default);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task SetCreationEquipmentPreferences_Valid_SetsPreferencesAndReturnsSuccess()
    {
        var session = MakeSession();
        _sessionStoreMock.Setup(s => s.GetSessionAsync(It.IsAny<Guid>())).ReturnsAsync(session);
        _sessionStoreMock.Setup(s => s.UpdateSessionAsync(session)).Returns(Task.CompletedTask);
        var handler = new SetCreationEquipmentPreferencesHandler(_sessionStoreMock.Object);

        var result = await handler.Handle(
            new SetCreationEquipmentPreferencesCommand(session.SessionId, "light", "sword", true), default);

        result.Success.Should().BeTrue();
        session.PreferredArmorType.Should().Be("light");
        session.PreferredWeaponType.Should().Be("sword");
        session.IncludeShield.Should().BeTrue();
    }

    // ----- SetCreationLocationHandler -----

    [Fact]
    public async Task SetCreationLocation_SessionNotFound_ReturnsFailed()
    {
        _sessionStoreMock.Setup(s => s.GetSessionAsync(It.IsAny<Guid>())).ReturnsAsync((CharacterCreationSession?)null);
        var handler = new SetCreationLocationHandler(_sessionStoreMock.Object, _zoneLocationRepoMock.Object);

        var result = await handler.Handle(new SetCreationLocationCommand(Guid.NewGuid(), "loc-1"), default);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task SetCreationLocation_Valid_SetsLocationAndReturnsSuccess()
    {
        var session = MakeSession();
        _sessionStoreMock.Setup(s => s.GetSessionAsync(It.IsAny<Guid>())).ReturnsAsync(session);
        _sessionStoreMock.Setup(s => s.UpdateSessionAsync(session)).Returns(Task.CompletedTask);
        _zoneLocationRepoMock.Setup(r => r.GetBySlugAsync("loc-1"))
            .ReturnsAsync(new ZoneLocationEntry("loc-1", "Loc One", "location", "zone-1", "outpost", 10, 1, 5));
        var handler = new SetCreationLocationHandler(_sessionStoreMock.Object, _zoneLocationRepoMock.Object);

        var result = await handler.Handle(new SetCreationLocationCommand(session.SessionId, "loc-1"), default);

        result.Success.Should().BeTrue();
        session.SelectedLocationId.Should().Be("loc-1");
    }

    // ----- Status guard -----

    [Fact]
    public async Task SetCreationClass_FinalizedSession_ReturnsFailed()
    {
        var session = new CharacterCreationSession { Status = CreationSessionStatus.Finalized };
        _sessionStoreMock.Setup(s => s.GetSessionAsync(session.SessionId)).ReturnsAsync(session);
        var handler = new SetCreationClassHandler(_sessionStoreMock.Object, _mediatorMock.Object,
            NullLogger<SetCreationClassHandler>.Instance);

        var result = await handler.Handle(new SetCreationClassCommand(session.SessionId, "warrior"), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Finalized");
    }

    [Fact]
    public async Task SetCreationName_FinalizedSession_ReturnsFailed()
    {
        var session = new CharacterCreationSession { Status = CreationSessionStatus.Finalized };
        _sessionStoreMock.Setup(s => s.GetSessionAsync(session.SessionId)).ReturnsAsync(session);
        var handler = new SetCreationNameHandler(_sessionStoreMock.Object,
            NullLogger<SetCreationNameHandler>.Instance);

        var result = await handler.Handle(new SetCreationNameCommand(session.SessionId, "Heroic"), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Finalized");
    }

    // ----- Name validation -----

    [Fact]
    public async Task SetCreationName_TooShort_ReturnsFailed()
    {
        var handler = new SetCreationNameHandler(_sessionStoreMock.Object,
            NullLogger<SetCreationNameHandler>.Instance);

        var result = await handler.Handle(new SetCreationNameCommand(Guid.NewGuid(), "A"), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("2 characters");
    }

    [Fact]
    public async Task SetCreationName_TooLong_ReturnsFailed()
    {
        var handler = new SetCreationNameHandler(_sessionStoreMock.Object,
            NullLogger<SetCreationNameHandler>.Instance);

        var result = await handler.Handle(
            new SetCreationNameCommand(Guid.NewGuid(), new string('A', 31)), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("30 characters");
    }

    [Fact]
    public async Task SetCreationName_ContainsNumber_ReturnsFailed()
    {
        var handler = new SetCreationNameHandler(_sessionStoreMock.Object,
            NullLogger<SetCreationNameHandler>.Instance);

        var result = await handler.Handle(new SetCreationNameCommand(Guid.NewGuid(), "H3ro"), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("only contain letters");
    }

    [Fact]
    public async Task SetCreationName_ContainsSpace_ReturnsFailed()
    {
        var handler = new SetCreationNameHandler(_sessionStoreMock.Object,
            NullLogger<SetCreationNameHandler>.Instance);

        var result = await handler.Handle(new SetCreationNameCommand(Guid.NewGuid(), "Hero One"), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("only contain letters");
    }

    [Fact]
    public async Task SetCreationName_Valid_StoresTrimmedName()
    {
        var session = MakeSession();
        _sessionStoreMock.Setup(s => s.GetSessionAsync(session.SessionId)).ReturnsAsync(session);
        _sessionStoreMock.Setup(s => s.UpdateSessionAsync(session)).Returns(Task.CompletedTask);
        var handler = new SetCreationNameHandler(_sessionStoreMock.Object,
            NullLogger<SetCreationNameHandler>.Instance);

        var result = await handler.Handle(
            new SetCreationNameCommand(session.SessionId, "  Heroic  "), default);

        result.Success.Should().BeTrue();
        session.CharacterName.Should().Be("Heroic");
    }
}
