using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.CharacterCreation.Queries;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using SpeciesModel = RealmEngine.Shared.Models.Species;

namespace RealmEngine.Core.Tests.Features.CharacterCreation.Queries;

[Trait("Category", "Feature")]
public class GetCreationPreviewHandlerTests
{
    private readonly Mock<ICharacterCreationSessionStore> _sessionStoreMock = new();

    private GetCreationPreviewHandler CreateHandler() =>
        new(_sessionStoreMock.Object, NullLogger<GetCreationPreviewHandler>.Instance);

    private static CharacterClass MakeClass(
        int str = 0, int dex = 0, int con = 0,
        int intel = 0, int wis = 0, int cha = 0,
        int startHp = 100, int startMana = 50) =>
        new()
        {
            Name = "TestClass", Slug = "test-class",
            BonusStrength = str, BonusDexterity = dex, BonusConstitution = con,
            BonusIntelligence = intel, BonusWisdom = wis, BonusCharisma = cha,
            StartingHealth = startHp, StartingMana = startMana,
        };

    [Fact]
    public async Task Handle_SessionNotFound_ReturnsFailed()
    {
        _sessionStoreMock.Setup(s => s.GetSessionAsync(It.IsAny<Guid>())).ReturnsAsync((CharacterCreationSession?)null);

        var result = await CreateHandler().Handle(new GetCreationPreviewQuery(Guid.NewGuid()), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_NoClassSelected_ReturnsFailed()
    {
        var session = new CharacterCreationSession(); // SelectedClass = null
        _sessionStoreMock.Setup(s => s.GetSessionAsync(session.SessionId)).ReturnsAsync(session);

        var result = await CreateHandler().Handle(new GetCreationPreviewQuery(session.SessionId), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("class");
    }

    [Fact]
    public async Task Handle_ClassSelected_ReturnsPreviewCharacter()
    {
        var session = new CharacterCreationSession { SelectedClass = MakeClass() };
        _sessionStoreMock.Setup(s => s.GetSessionAsync(session.SessionId)).ReturnsAsync(session);

        var result = await CreateHandler().Handle(new GetCreationPreviewQuery(session.SessionId), default);

        result.Success.Should().BeTrue();
        result.Character.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ClassBonuses_StackOnTopOfAllocations()
    {
        var session = new CharacterCreationSession
        {
            SelectedClass = MakeClass(str: 3),
            AttributeAllocations = new Dictionary<string, int>
            {
                ["Strength"] = 12, ["Dexterity"] = 10, ["Constitution"] = 10,
                ["Intelligence"] = 10, ["Wisdom"] = 10, ["Charisma"] = 10,
            },
        };
        _sessionStoreMock.Setup(s => s.GetSessionAsync(session.SessionId)).ReturnsAsync(session);

        var result = await CreateHandler().Handle(new GetCreationPreviewQuery(session.SessionId), default);

        result.Character!.Strength.Should().Be(15); // 12 alloc + 3 class bonus
    }

    [Fact]
    public async Task Handle_NoAllocations_DefaultsToTen()
    {
        var session = new CharacterCreationSession
        {
            SelectedClass = MakeClass(str: 0),
        };
        _sessionStoreMock.Setup(s => s.GetSessionAsync(session.SessionId)).ReturnsAsync(session);

        var result = await CreateHandler().Handle(new GetCreationPreviewQuery(session.SessionId), default);

        result.Character!.Strength.Should().Be(10);
    }

    [Fact]
    public async Task Handle_SpeciesBonuses_StackOnPreview()
    {
        var species = new SpeciesModel { Slug = "elf", BonusDexterity = 2 };
        var session = new CharacterCreationSession
        {
            SelectedClass   = MakeClass(),
            SelectedSpecies = species,
        };
        _sessionStoreMock.Setup(s => s.GetSessionAsync(session.SessionId)).ReturnsAsync(session);

        var result = await CreateHandler().Handle(new GetCreationPreviewQuery(session.SessionId), default);

        result.Character!.Dexterity.Should().Be(12); // 10 base + 2 species bonus
    }

    [Fact]
    public async Task Handle_StartingHealthAndMana_ComeFromClass()
    {
        var session = new CharacterCreationSession
        {
            SelectedClass = MakeClass(startHp: 150, startMana: 80),
        };
        _sessionStoreMock.Setup(s => s.GetSessionAsync(session.SessionId)).ReturnsAsync(session);

        var result = await CreateHandler().Handle(new GetCreationPreviewQuery(session.SessionId), default);

        result.Character!.Health.Should().Be(150);
        result.Character.MaxHealth.Should().Be(150);
        result.Character.Mana.Should().Be(80);
    }

    [Fact]
    public async Task Handle_BackgroundBonuses_StackOnPreview()
    {
        var background = new Background
        {
            Slug               = "soldier",
            Name               = "Soldier",
            PrimaryAttribute   = "Strength",
            PrimaryBonus       = 2,
            SecondaryAttribute = "Constitution",
            SecondaryBonus     = 1,
        };
        var session = new CharacterCreationSession
        {
            SelectedClass      = MakeClass(),
            SelectedBackground = background,
        };
        _sessionStoreMock.Setup(s => s.GetSessionAsync(session.SessionId)).ReturnsAsync(session);

        var result = await CreateHandler().Handle(new GetCreationPreviewQuery(session.SessionId), default);

        result.Character!.Strength.Should().Be(12);      // 10 default + 2 background bonus
        result.Character.Constitution.Should().Be(11);   // 10 default + 1 background bonus
    }
}
