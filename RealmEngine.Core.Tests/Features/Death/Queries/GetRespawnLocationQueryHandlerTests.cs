using FluentAssertions;
using Moq;
using RealmEngine.Core.Features.Death.Queries;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Death.Queries;

public class GetRespawnLocationQueryHandlerTests
{
    private static GetRespawnLocationQueryHandler CreateHandler(ISaveGameService? saveGameService = null) =>
        new(saveGameService ?? Mock.Of<ISaveGameService>());

    [Fact]
    public async Task Handle_AlwaysReturnsHubTown_AsDefaultLocation()
    {
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new GetRespawnLocationQuery(), default);

        result.DefaultLocation.Should().Be("Hub Town");
    }

    [Fact]
    public async Task Handle_ReturnsHubTownInList_WhenNoSaveExists()
    {
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new GetRespawnLocationQuery(), default);

        result.AvailableLocations.Should().Contain("Hub Town");
        result.HasCustomRespawnPoints.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_IncludesDiscoveredTowns_WhenSaveExists()
    {
        var saveGame = new SaveGame();
        saveGame.DiscoveredLocations.AddRange(["Riverside Town", "Some Dungeon", "Harbor Village"]);

        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns(saveGame);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new GetRespawnLocationQuery(), default);

        result.AvailableLocations.Should().Contain("Riverside Town");
        result.AvailableLocations.Should().Contain("Harbor Village");
        result.AvailableLocations.Should().NotContain("Some Dungeon");
    }

    [Fact]
    public async Task Handle_ReturnsHasCustomRespawnPoints_WhenTownsDiscovered()
    {
        var saveGame = new SaveGame();
        saveGame.DiscoveredLocations.Add("Eastern Town");

        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns(saveGame);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new GetRespawnLocationQuery(), default);

        result.HasCustomRespawnPoints.Should().BeTrue();
        result.AvailableLocations.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public async Task Handle_ExcludesDuplicates_InAvailableLocations()
    {
        var saveGame = new SaveGame();
        saveGame.DiscoveredLocations.Add("Hub Town"); // already in the default list

        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns(saveGame);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new GetRespawnLocationQuery(), default);

        result.AvailableLocations.Should().ContainSingle(l => l == "Hub Town");
    }

    [Fact]
    public async Task Handle_IncludesSanctuaries_AsRespawnPoints()
    {
        var saveGame = new SaveGame();
        saveGame.DiscoveredLocations.Add("Forest Sanctuary");

        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns(saveGame);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new GetRespawnLocationQuery(), default);

        result.AvailableLocations.Should().Contain("Forest Sanctuary");
    }
}
