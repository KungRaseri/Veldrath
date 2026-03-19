using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Harvesting;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Services;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using RealmEngine.Shared.Models.Harvesting;
using Microsoft.EntityFrameworkCore;

namespace RealmEngine.Core.Tests.Features.Harvesting;

/// <summary>
/// Unit tests for <see cref="GetNearbyNodesQueryHandler"/>.
/// </summary>
[Trait("Category", "Feature")]
public class GetNearbyNodesQueryHandlerTests
{
    private static GetNearbyNodesQueryHandler CreateHandler(
        List<HarvestableNode>? nodes = null,
        SaveGame? save = null)
    {
        var nodeRepo = new Mock<INodeRepository>();
        nodeRepo
            .Setup(r => r.GetNodesByLocationAsync(It.IsAny<string>()))
            .ReturnsAsync(nodes ?? []);

        var saveService = new Mock<ISaveGameService>();
        saveService.Setup(s => s.GetCurrentSave()).Returns(save);

        return new GetNearbyNodesQueryHandler(
            NullLogger<GetNearbyNodesQueryHandler>.Instance,
            nodeRepo.Object,
            saveService.Object);
    }

    private static HarvestableNode MakeNode(
        string nodeId = "node-1",
        string nodeType = "ore_vein",
        string locationId = "loc-1",
        bool canHarvest = true) =>
        new()
        {
            NodeId = nodeId,
            DisplayName = $"{nodeType} {nodeId}",
            NodeType = nodeType,
            LocationId = locationId,
            CurrentHealth = canHarvest ? 100 : 0,
            MaxHealth = 100
        };

    [Fact]
    public async Task Handle_ReturnsSuccess_WhenLocationIdProvided()
    {
        var handler = CreateHandler();
        var result = await handler.Handle(
            new GetNearbyNodesQuery { CharacterName = "Hero", LocationId = "forest-1" }, default);

        result.Success.Should().BeTrue();
        result.LocationId.Should().Be("forest-1");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoSaveAndNoLocationId()
    {
        var handler = CreateHandler(save: null);
        var result = await handler.Handle(
            new GetNearbyNodesQuery { CharacterName = "Hero", LocationId = null }, default);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_UsesLastVisitedLocation_WhenNoLocationIdProvided()
    {
        var save = new SaveGame
        {
            Character = new Character { Name = "Hero" },
            VisitedLocations = ["zone-a", "zone-b"]
        };
        var handler = CreateHandler(save: save);
        var result = await handler.Handle(
            new GetNearbyNodesQuery { CharacterName = "Hero" }, default);

        result.Success.Should().BeTrue();
        result.LocationId.Should().Be("zone-b");
    }

    [Fact]
    public async Task Handle_ReturnsNodes_ForSpecifiedLocation()
    {
        var nodes = new List<HarvestableNode> { MakeNode("node-1"), MakeNode("node-2") };
        var handler = CreateHandler(nodes);
        var result = await handler.Handle(
            new GetNearbyNodesQuery { CharacterName = "Hero", LocationId = "loc-1" }, default);

        result.Success.Should().BeTrue();
        result.Nodes.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_FiltersDepletedNodes_WhenOnlyHarvestableIsTrue()
    {
        var nodes = new List<HarvestableNode>
        {
            MakeNode("n1", canHarvest: true),
            MakeNode("n2", canHarvest: false)
        };
        var handler = CreateHandler(nodes);
        var result = await handler.Handle(
            new GetNearbyNodesQuery { CharacterName = "Hero", LocationId = "loc-1", OnlyHarvestable = true }, default);

        result.Nodes.Should().ContainSingle(n => n.NodeId == "n1");
    }

    [Fact]
    public async Task Handle_ReturnsAllNodes_WhenOnlyHarvestableIsFalse()
    {
        var nodes = new List<HarvestableNode>
        {
            MakeNode("n1", canHarvest: true),
            MakeNode("n2", canHarvest: false)
        };
        var handler = CreateHandler(nodes);
        var result = await handler.Handle(
            new GetNearbyNodesQuery { CharacterName = "Hero", LocationId = "loc-1", OnlyHarvestable = false }, default);

        result.Nodes.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_FiltersNodesByType_WhenNodeTypeFilterProvided()
    {
        var nodes = new List<HarvestableNode>
        {
            MakeNode("n1", nodeType: "ore_vein"),
            MakeNode("n2", nodeType: "tree")
        };
        var handler = CreateHandler(nodes);
        var result = await handler.Handle(
            new GetNearbyNodesQuery
            {
                CharacterName = "Hero",
                LocationId = "loc-1",
                OnlyHarvestable = false,
                NodeTypeFilter = "ore_vein"
            }, default);

        result.Nodes.Should().ContainSingle(n => n.NodeType == "ore_vein");
    }

    [Fact]
    public async Task Handle_ReturnsTotalNodeCount()
    {
        var nodes = Enumerable.Range(1, 5).Select(i => MakeNode($"n{i}")).ToList();
        var handler = CreateHandler(nodes);
        var result = await handler.Handle(
            new GetNearbyNodesQuery { CharacterName = "Hero", LocationId = "loc-1" }, default);

        result.TotalNodes.Should().Be(5);
    }
}

/// <summary>
/// Unit tests for <see cref="InspectNodeQueryHandler"/>.
/// </summary>
[Trait("Category", "Feature")]
public class InspectNodeQueryHandlerTests
{
    private static LootTableService MakeLootTableService() =>
        new(NullLogger<LootTableService>.Instance,
            Mock.Of<ILootTableRepository>(),
            new Mock<IDbContextFactory<ContentDbContext>>().Object);

    private static InspectNodeQueryHandler CreateHandler(HarvestableNode? node = null, SaveGame? save = null)
    {
        var nodeRepo = new Mock<INodeRepository>();
        nodeRepo
            .Setup(r => r.GetNodeByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(node);

        var saveService = new Mock<ISaveGameService>();
        saveService.Setup(s => s.GetCurrentSave()).Returns(save);

        return new InspectNodeQueryHandler(
            NullLogger<InspectNodeQueryHandler>.Instance,
            nodeRepo.Object,
            saveService.Object,
            MakeLootTableService());
    }

    private static HarvestableNode MakeNode(string nodeId = "node-1", int health = 100, int maxHealth = 100) =>
        new()
        {
            NodeId = nodeId,
            DisplayName = "Copper Vein",
            NodeType = "ore_vein",
            MaterialTier = "common",
            CurrentHealth = health,
            MaxHealth = maxHealth,
            LootTableRef = string.Empty // skip loot table lookup in tests
        };

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNodeNotFound()
    {
        var handler = CreateHandler(node: null);
        var result = await handler.Handle(
            new InspectNodeQuery { CharacterName = "Hero", NodeId = "missing" }, default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_ReturnsSuccess_WhenNodeExists()
    {
        var handler = CreateHandler(MakeNode());
        var result = await handler.Handle(
            new InspectNodeQuery { CharacterName = "Hero", NodeId = "node-1" }, default);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ReturnsNodeDetails_WhenNodeFound()
    {
        var handler = CreateHandler(MakeNode("node-42", health: 80, maxHealth: 100));
        var result = await handler.Handle(
            new InspectNodeQuery { CharacterName = "Hero", NodeId = "node-42" }, default);

        result.Success.Should().BeTrue();
        result.NodeId.Should().Be("node-42");
        result.DisplayName.Should().Be("Copper Vein");
        result.NodeType.Should().Be("ore_vein");
    }

    [Fact]
    public async Task Handle_ReturnsCanHarvest_WhenNodeHasHealth()
    {
        var handler = CreateHandler(MakeNode(health: 100, maxHealth: 100));
        var result = await handler.Handle(
            new InspectNodeQuery { CharacterName = "Hero", NodeId = "node-1" }, default);

        result.CanHarvest.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ReturnsCannotHarvest_WhenNodeDepleted()
    {
        var handler = CreateHandler(MakeNode(health: 0, maxHealth: 100));
        var result = await handler.Handle(
            new InspectNodeQuery { CharacterName = "Hero", NodeId = "node-1" }, default);

        result.CanHarvest.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ReturnsEmptyMaterials_WhenNoLootTableRef()
    {
        var handler = CreateHandler(MakeNode()); // LootTableRef is empty
        var result = await handler.Handle(
            new InspectNodeQuery { CharacterName = "Hero", NodeId = "node-1" }, default);

        result.Success.Should().BeTrue();
        result.PossibleMaterials.Should().BeEmpty();
    }
}
