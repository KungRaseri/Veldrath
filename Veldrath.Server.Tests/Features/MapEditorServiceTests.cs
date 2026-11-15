using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Seeders;
using Veldrath.Contracts.Map;
using Veldrath.Server.Features.Content;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Covers the Foundry map editor's write path against the real seeded catalog, in-memory. The interesting
/// saves are the ones that must be <em>refused</em>: a stranded dungeon room, an exit to nowhere, and a stale
/// copy of the graph.
/// </summary>
public class MapEditorServiceTests
{
    private const string HalrowMapId = "zone:the-halrow";

    private static async Task<(ContentDbContext Db, MapEditorService Editor)> SeededAsync()
    {
        var db = new ContentDbContext(
            new DbContextOptionsBuilder<ContentDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        await MapGraphSeeder.SeedAsync(db);
        await ZoneLocationsSeeder.SeedAsync(db);

        return (db, new MapEditorService(db, NullLogger<MapEditorService>.Instance));
    }

    private static MapGraphDto WithNode(
        MapGraphDto graph,
        string nodeId,
        Func<MapNodeDto, MapNodeDto> change) =>
        graph with
        {
            Nodes = graph.Nodes.Select(node => node.Id == nodeId ? change(node) : node).ToList(),
        };

    [Fact]
    public async Task CreateDraftAsync_MakesAnInactiveMapWithOneEntryNode()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var (map, error) = await editor.CreateDraftAsync(
            new MapCreateRequest("Zone", "draft-zone", "Draft Zone"),
            Guid.NewGuid());

        error.Should().BeNull();
        map!.Id.Should().Be("zone:draft-zone");
        map.IsActive.Should().BeFalse("a new map is a draft until a reviewer approves it");
        map.Nodes.Should().ContainSingle(node => node.IsEntryPoint);
        map.EntryNodeId.Should().Be("zone:draft-zone:entry");
    }

    [Fact]
    public async Task CreateDraftAsync_RefusesAnOwnerThatAlreadyHasAMap()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var (map, error) = await editor.CreateDraftAsync(
            new MapCreateRequest("Zone", "the-halrow", "Another Halrow"),
            Guid.NewGuid());

        map.Should().BeNull();
        error.Should().Contain("already");
    }

    [Fact]
    public async Task SubmitAsync_RefusesAGraphWithErrors()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        await editor.CreateDraftAsync(new MapCreateRequest("Zone", "draft-zone", "Draft Zone"), Guid.NewGuid());
        var map = await db.Maps.SingleAsync(candidate => candidate.Id == "zone:draft-zone");
        map.EntryNodeId = "zone:draft-zone:ghost";
        await db.SaveChangesAsync();

        var result = await editor.SubmitAsync("zone:draft-zone", Guid.NewGuid());

        result.Success.Should().BeFalse();
        result.Status.Should().Be("invalid");
        result.Issues.Should().Contain(issue => issue.Code == "entry_not_found");
    }

    [Fact]
    public async Task ReviewAsync_ApprovesASubmittedMapAndRecordsTheApprovedVersion()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var reviewer = Guid.NewGuid();
        await editor.CreateDraftAsync(new MapCreateRequest("Zone", "draft-zone", "Draft Zone"), Guid.NewGuid());
        (await editor.SubmitAsync("zone:draft-zone", Guid.NewGuid())).Success.Should().BeTrue();

        var result = await editor.ReviewAsync("zone:draft-zone", reviewer, new MapReviewRequest(true));

        result.Response.Success.Should().BeTrue();
        var map = await db.Maps.SingleAsync(candidate => candidate.Id == "zone:draft-zone");
        map.IsActive.Should().BeTrue();
        map.ApprovedVersion.Should().Be(map.Version);
        map.ApprovedByAccountId.Should().Be(reviewer);
    }

    [Fact]
    public async Task ReviewAsync_RefusesAMapThatWasNeverSubmitted()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        await editor.CreateDraftAsync(new MapCreateRequest("Zone", "draft-zone", "Draft Zone"), Guid.NewGuid());

        var result = (await editor.ReviewAsync("zone:draft-zone", Guid.NewGuid(), new MapReviewRequest(true)))
            .Response;

        result.Success.Should().BeFalse();
        result.Status.Should().Be("conflict");
        result.Message.Should().Contain("submitted");
    }

    [Fact]
    public async Task ReviewAsync_RejectionKeepsTheNotesOnTheMapAndClearsTheSubmission()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        await editor.CreateDraftAsync(new MapCreateRequest("Zone", "draft-zone", "Draft Zone"), Guid.NewGuid());
        await editor.SubmitAsync("zone:draft-zone", Guid.NewGuid());

        var outcome = await editor.ReviewAsync(
            "zone:draft-zone",
            Guid.NewGuid(),
            new MapReviewRequest(false, "Room 3 has no exit."));

        outcome.Response.Success.Should().BeTrue();

        var map = await db.Maps.SingleAsync(candidate => candidate.Id == "zone:draft-zone");
        map.IsActive.Should().BeFalse();
        map.SubmittedAt.Should().BeNull("a rejected map is a draft again");
        map.LastReviewNotes.Should().Be("Room 3 has no exit.");
        map.LastReviewedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeactivateAsync_RefusesAMapOtherNodesStillExitTo()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var result = await editor.DeactivateAsync("region:varenmark");

        result.Success.Should().BeFalse();
        result.Status.Should().Be("conflict");
        result.Message.Should().Contain("still exit here");
    }

    [Fact]
    public async Task DeleteDraftAsync_RefusesALiveMap()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var result = await editor.DeleteDraftAsync(HalrowMapId);

        result.Success.Should().BeFalse();
        result.Status.Should().Be("conflict");
    }

    [Fact]
    public async Task GetTargetsAsync_ListsEveryReferenceAPickerNeeds()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var targets = await editor.GetTargetsAsync();

        targets.Maps.Should().Contain(option => option.Id == HalrowMapId);
        targets.Zones.Should().Contain(option => option.Id == "the-halrow");
        targets.Regions.Should().Contain(option => option.Id == "varenmark");
        targets.Locations.Should().Contain(option => option.Id == "halrow-threshold");
        targets.Nodes.Should().Contain(option => option.Id == "zone:the-halrow:entry" && option.IsEntryPoint);
    }

    [Fact]
    public async Task ListAsync_ReturnsEverySeededMapWithItsVersion()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var maps = await editor.ListAsync();

        maps.Should().HaveCount(23);
        maps.Should().OnlyContain(map => map.Version >= 1);
        maps.Select(map => map.Id).Should().Contain(HalrowMapId);
    }

    [Fact]
    public async Task GetAsync_ReturnsTheWholeGraph()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var graph = await editor.GetAsync(HalrowMapId);

        graph.Should().NotBeNull();
        graph!.Nodes.Should().Contain(node => node.Id == "zone:the-halrow:entry");
        graph.Nodes.Should().Contain(node => node.RefId == "halrow-threshold");
        graph.Edges.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SaveAsync_RenamesANodeAndBumpsTheVersion()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var graph = (await editor.GetAsync(HalrowMapId))!;
        var renamed = WithNode(graph, "zone:the-halrow:location:halrow-threshold", node =>
            node with { DisplayName = "The Threshold of Roots" });

        var result = await editor.SaveAsync(HalrowMapId, renamed, graph.Version);

        result.Success.Should().BeTrue();
        result.Status.Should().Be("ok");
        result.Version.Should().Be(graph.Version + 1);

        var reloaded = (await editor.GetAsync(HalrowMapId))!;
        reloaded.Version.Should().Be(result.Version);
        reloaded.Nodes.Should().Contain(node => node.DisplayName == "The Threshold of Roots");
    }

    [Fact]
    public async Task SaveAsync_RefusesAStaleVersion()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var graph = (await editor.GetAsync(HalrowMapId))!;

        var result = await editor.SaveAsync(HalrowMapId, graph, expectedVersion: 99);

        result.Success.Should().BeFalse();
        result.Status.Should().Be("conflict");
        result.Version.Should().Be(graph.Version);
    }

    [Fact]
    public async Task SaveAsync_RefusesAGraphThatLeavesADungeonRoomWithoutAWayOut()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var graph = (await editor.GetAsync(HalrowMapId))!;
        var exitNode = graph.Nodes.Single(node => node.Id.EndsWith("exit:region:varenmark", StringComparison.Ordinal));
        var stranded = graph with
        {
            Nodes = graph.Nodes.Where(node => node.Id != exitNode.Id).ToList(),
            Edges = graph.Edges
                .Where(edge => edge.FromNodeId != exitNode.Id && edge.ToNodeId != exitNode.Id)
                .ToList(),
        };

        var result = await editor.SaveAsync(HalrowMapId, stranded, graph.Version);

        result.Success.Should().BeFalse();
        result.Status.Should().Be("invalid");
        result.Issues.Should().Contain(issue => issue.Code == "map_without_exit" && issue.Severity == "Error");
    }

    [Fact]
    public async Task SaveAsync_RefusesAnExitToAMapThatDoesNotExist()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var graph = (await editor.GetAsync(HalrowMapId))!;
        var exitNode = graph.Nodes.Single(node => node.Id.EndsWith("exit:region:varenmark", StringComparison.Ordinal));
        var broken = WithNode(graph, exitNode.Id, node => node with { TargetMapId = "region:nowhere" });

        var result = await editor.SaveAsync(HalrowMapId, broken, graph.Version);

        result.Success.Should().BeFalse();
        result.Status.Should().Be("invalid");
        result.Issues.Should().Contain(issue => issue.Code == "target_map_unknown");
    }

    [Fact]
    public async Task SaveAsync_RefusesAGraphWhoseIdDoesNotMatchTheRoute()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var graph = (await editor.GetAsync(HalrowMapId))!;
        var mismatched = graph with { Id = "zone:crestfall" };

        var result = await editor.SaveAsync(HalrowMapId, mismatched, graph.Version);

        result.Success.Should().BeFalse();
        result.Status.Should().Be("invalid");
        result.Issues.Should().Contain(issue => issue.Code == "request_invalid");
    }

    [Fact]
    public async Task ValidateAsync_ReportsTheIssuesASaveWouldRefuseOnWithoutWriting()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var graph = (await editor.GetAsync(HalrowMapId))!;
        var exitNode = graph.Nodes.Single(node => node.Id.EndsWith("exit:region:varenmark", StringComparison.Ordinal));
        var broken = WithNode(graph, exitNode.Id, node => node with { TargetNodeId = "ghost" });

        var result = await editor.ValidateAsync(HalrowMapId, broken);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(issue => issue.Code == "target_node_unknown");

        var stored = await editor.GetAsync(HalrowMapId);
        stored!.Version.Should().Be(graph.Version);
    }

    [Fact]
    public async Task ValidateAsync_AcceptsAValidGraphWithoutBumpingTheVersion()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var graph = (await editor.GetAsync(HalrowMapId))!;

        var result = await editor.ValidateAsync(HalrowMapId, graph);

        result.IsValid.Should().BeTrue();
        result.Issues.Should().NotContain(issue => issue.Severity == "Error");

        var stored = await editor.GetAsync(HalrowMapId);
        stored!.Version.Should().Be(graph.Version);
    }

    [Fact]
    public async Task ValidateAsync_ReportsAMismatchedIdAsARequestProblem()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var graph = (await editor.GetAsync(HalrowMapId))!;

        var result = await editor.ValidateAsync(HalrowMapId, graph with { Id = "zone:crestfall" });

        result.IsValid.Should().BeFalse();
        result.Issues.Should().ContainSingle(issue => issue.Code == "request_invalid");
    }

    [Fact]
    public async Task SaveAsync_ReturnsNotFoundForAnUnknownMap()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var graph = (await editor.GetAsync(HalrowMapId))!;

        var result = await editor.SaveAsync("zone:nowhere", graph with { Id = "zone:nowhere" }, 1);

        result.Success.Should().BeFalse();
        result.Status.Should().Be("not_found");
    }
}
