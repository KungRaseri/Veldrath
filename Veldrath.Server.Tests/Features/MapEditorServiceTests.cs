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

    /// <summary>An editor: may write any map, which is what `content.manage` grants.</summary>
    private static readonly MapCaller Editor = new(Guid.NewGuid(), CanManageAny: true, CanReview: true);

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

    /// <summary>Gives a graph a way out, pointing at a seeded region entry, so it can be approved.</summary>
    private static MapGraphDto WithAWayOut(MapGraphDto graph) =>
        graph with
        {
            Nodes =
            [
                .. graph.Nodes,
                new MapNodeDto(
                    $"{graph.Id}:exit",
                    graph.Id,
                    "RegionExit",
                    "cinderplain",
                    "Exit to Cinderplain",
                    string.Empty,
                    null,
                    graph.Nodes.Count,
                    0,
                    false,
                    true,
                    "region:cinderplain",
                    "region:cinderplain:entry",
                    false,
                    null,
                    null,
                    null,
                    (graph.Nodes.Count * 10) + 10),
            ],
        };

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
    public async Task CreateDraftAsync_RequiresAnExistingLocationForAnInterior()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var (missing, error) = await editor.CreateDraftAsync(
            new MapCreateRequest("Interior", "no-such-location", "Nowhere"),
            Guid.NewGuid());

        missing.Should().BeNull();
        error.Should().Contain("No location with slug");

        // A region or a zone map defines its owner; an interior hangs off a location that already exists.
        var locationSlug = await db.ZoneLocations.Select(location => location.Slug).FirstAsync();
        var (map, createdError) = await editor.CreateDraftAsync(
            new MapCreateRequest("Interior", locationSlug, "The Cellar"),
            Guid.NewGuid());

        createdError.Should().BeNull();
        map!.Id.Should().Be($"interior:{locationSlug}");
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

        var result = await editor.SubmitAsync("zone:draft-zone", Editor);

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

        // A draft saves with no way out — that is how a zone gets built — but approval refuses it, so give it
        // the exit an author would add before submitting.
        var draft = (await editor.GetAsync("zone:draft-zone", Editor))!;
        (await editor.SaveAsync(draft.Id, WithAWayOut(draft), draft.Version, Editor)).Success.Should().BeTrue();

        (await editor.SubmitAsync("zone:draft-zone", Editor)).Success.Should().BeTrue();

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
        await editor.SubmitAsync("zone:draft-zone", Editor);

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

        var result = await editor.DeleteDraftAsync(HalrowMapId, Editor);

        result.Success.Should().BeFalse();
        result.Status.Should().Be("conflict");
    }

    [Fact]
    public async Task GetTargetsAsync_ListsEveryReferenceAPickerNeeds()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var targets = await editor.GetTargetsAsync(Editor);

        targets.Maps.Should().Contain(option => option.Id == HalrowMapId);
        targets.Zones.Should().Contain(option => option.Id == "the-halrow");
        targets.Regions.Should().Contain(option => option.Id == "varenmark");
        targets.Locations.Should().Contain(option => option.Id == "halrow-threshold");
        targets.Nodes.Should().Contain(option => option.Id == "zone:the-halrow:entry" && option.IsEntryPoint);
    }

    [Fact]
    public async Task GetTargets_OmitsAStrangersDraftMapAndItsNodes()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var authorId = Guid.NewGuid();
        var (draft, draftError) = await editor.CreateDraftAsync(
            new MapCreateRequest("Zone", "strangers-draft", "A Stranger's Draft"), authorId);
        draftError.Should().BeNull();
        var (published, publishedError) = await editor.CreateDraftAsync(
            new MapCreateRequest("Zone", "strangers-published", "A Stranger's Published Zone"), authorId);
        publishedError.Should().BeNull();

        // The same author, with one draft still hidden and one map already live: the picker must follow the flag,
        // not the author.
        db.Maps.Single(map => map.Id == published!.Id).IsActive = true;
        await db.SaveChangesAsync();

        var stranger = new MapCaller(Guid.NewGuid(), CanManageAny: false, CanReview: false);
        var targets = await editor.GetTargetsAsync(stranger);

        targets.Maps.Should().NotContain(option => option.Id == draft!.Id);
        targets.Zones.Should().NotContain(option => option.Id == "strangers-draft");
        targets.Nodes.Should().NotContain(option => option.Id == $"{draft!.Id}:entry");

        targets.Maps.Should().Contain(option => option.Id == published!.Id);
        targets.Zones.Should().Contain(option => option.Id == "strangers-published");
        targets.Nodes.Should().Contain(option => option.Id == $"{published!.Id}:entry");
    }

    [Fact]
    public async Task GetTargets_IncludesTheCallersOwnDraft()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var authorId = Guid.NewGuid();
        var (draft, _) = await editor.CreateDraftAsync(
            new MapCreateRequest("Zone", "my-own-draft", "My Own Draft"), authorId);

        var author = new MapCaller(authorId, CanManageAny: false, CanReview: false);
        var targets = await editor.GetTargetsAsync(author);

        targets.Maps.Should().Contain(option => option.Id == draft!.Id);
        targets.Zones.Should().Contain(option => option.Id == "my-own-draft");
        targets.Nodes.Should().Contain(option => option.Id == $"{draft!.Id}:entry");
    }

    [Fact]
    public async Task GetTargets_IncludesEveryDraft_ForAReviewer()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var (draft, _) = await editor.CreateDraftAsync(
            new MapCreateRequest("Zone", "someone-elses-draft", "Someone Else's Draft"), Guid.NewGuid());

        var reviewer = new MapCaller(Guid.NewGuid(), CanManageAny: false, CanReview: true);
        var targets = await editor.GetTargetsAsync(reviewer);

        targets.Maps.Should().Contain(option => option.Id == draft!.Id);
        targets.Nodes.Should().Contain(option => option.Id == $"{draft!.Id}:entry");
    }

    [Fact]
    public async Task ListAsync_ReturnsEverySeededMapWithItsVersion()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var maps = await editor.ListAsync(Editor);

        maps.Should().HaveCount(23);
        maps.Should().OnlyContain(map => map.Version >= 1);
        maps.Select(map => map.Id).Should().Contain(HalrowMapId);
    }

    [Fact]
    public async Task GetAsync_ReturnsTheWholeGraph()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var graph = await editor.GetAsync(HalrowMapId, Editor);

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

        var graph = (await editor.GetAsync(HalrowMapId, Editor))!;
        var renamed = WithNode(graph, "zone:the-halrow:location:halrow-threshold", node =>
            node with { DisplayName = "The Threshold of Roots" });

        var result = await editor.SaveAsync(HalrowMapId, renamed, graph.Version, Editor);

        result.Success.Should().BeTrue();
        result.Status.Should().Be("ok");
        result.Version.Should().Be(graph.Version + 1);

        var reloaded = (await editor.GetAsync(HalrowMapId, Editor))!;
        reloaded.Version.Should().Be(result.Version);
        reloaded.Nodes.Should().Contain(node => node.DisplayName == "The Threshold of Roots");
    }

    [Fact]
    public async Task SaveAsync_RefusesAStaleVersion()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var graph = (await editor.GetAsync(HalrowMapId, Editor))!;

        var result = await editor.SaveAsync(HalrowMapId, graph, expectedVersion: 99, Editor);

        result.Success.Should().BeFalse();
        result.Status.Should().Be("conflict");
        result.Version.Should().Be(graph.Version);
    }

    [Fact]
    public async Task SaveAsync_RefusesAGraphThatLeavesADungeonRoomWithoutAWayOut()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var graph = (await editor.GetAsync(HalrowMapId, Editor))!;
        var exitNode = graph.Nodes.Single(node => node.Id.EndsWith("exit:region:varenmark", StringComparison.Ordinal));
        var stranded = graph with
        {
            Nodes = graph.Nodes.Where(node => node.Id != exitNode.Id).ToList(),
            Edges = graph.Edges
                .Where(edge => edge.FromNodeId != exitNode.Id && edge.ToNodeId != exitNode.Id)
                .ToList(),
        };

        var result = await editor.SaveAsync(HalrowMapId, stranded, graph.Version, Editor);

        result.Success.Should().BeFalse();
        result.Status.Should().Be("invalid");
        result.Issues.Should().Contain(issue => issue.Code == "map_without_exit" && issue.Severity == "Error");
    }

    [Fact]
    public async Task SaveAsync_RefusesAnExitToAMapThatDoesNotExist()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var graph = (await editor.GetAsync(HalrowMapId, Editor))!;
        var exitNode = graph.Nodes.Single(node => node.Id.EndsWith("exit:region:varenmark", StringComparison.Ordinal));
        var broken = WithNode(graph, exitNode.Id, node => node with { TargetMapId = "region:nowhere" });

        var result = await editor.SaveAsync(HalrowMapId, broken, graph.Version, Editor);

        result.Success.Should().BeFalse();
        result.Status.Should().Be("invalid");
        result.Issues.Should().Contain(issue => issue.Code == "target_map_unknown");
    }

    [Fact]
    public async Task SaveAsync_RefusesAGraphWhoseIdDoesNotMatchTheRoute()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var graph = (await editor.GetAsync(HalrowMapId, Editor))!;
        var mismatched = graph with { Id = "zone:crestfall" };

        var result = await editor.SaveAsync(HalrowMapId, mismatched, graph.Version, Editor);

        result.Success.Should().BeFalse();
        result.Status.Should().Be("invalid");
        result.Issues.Should().Contain(issue => issue.Code == "request_invalid");
    }

    [Fact]
    public async Task ValidateAsync_ReportsTheIssuesASaveWouldRefuseOnWithoutWriting()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var graph = (await editor.GetAsync(HalrowMapId, Editor))!;
        var exitNode = graph.Nodes.Single(node => node.Id.EndsWith("exit:region:varenmark", StringComparison.Ordinal));
        var broken = WithNode(graph, exitNode.Id, node => node with { TargetNodeId = "ghost" });

        var result = await editor.ValidateAsync(HalrowMapId, broken);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(issue => issue.Code == "target_node_unknown");

        var stored = await editor.GetAsync(HalrowMapId, Editor);
        stored!.Version.Should().Be(graph.Version);
    }

    [Fact]
    public async Task ValidateAsync_AcceptsAValidGraphWithoutBumpingTheVersion()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var graph = (await editor.GetAsync(HalrowMapId, Editor))!;

        var result = await editor.ValidateAsync(HalrowMapId, graph);

        result.IsValid.Should().BeTrue();
        result.Issues.Should().NotContain(issue => issue.Severity == "Error");

        var stored = await editor.GetAsync(HalrowMapId, Editor);
        stored!.Version.Should().Be(graph.Version);
    }

    [Fact]
    public async Task ValidateAsync_ReportsAMismatchedIdAsARequestProblem()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var graph = (await editor.GetAsync(HalrowMapId, Editor))!;

        var result = await editor.ValidateAsync(HalrowMapId, graph with { Id = "zone:crestfall" });

        result.IsValid.Should().BeFalse();
        result.Issues.Should().ContainSingle(issue => issue.Code == "request_invalid");
    }

    [Fact]
    public async Task GetAsync_CompactsSparseAuthoredCoordinates()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        // The seeded region graph is authored on a scattered grid — x=2 beside x=34, y=14 beside y=36. Those
        // numbers are an ordering, not a distance, and rendering them literally made the canvas thousands of
        // pixels wide for six nodes.
        var graph = (await editor.GetAsync("region:cinderplain", Editor))!;

        var columns = graph.Nodes.Select(node => node.LayoutX).OfType<int>().Distinct().Order().ToList();
        var rows = graph.Nodes.Select(node => node.LayoutY).OfType<int>().Distinct().Order().ToList();

        columns.Should().Equal(Enumerable.Range(0, columns.Count), "every used column collapses onto the next");
        rows.Should().Equal(Enumerable.Range(0, rows.Count), "every used row collapses onto the next");
        // The shape survives: the entry sits left of the zone it leads to, which is what the author drew.
        var entry = graph.Nodes.Single(node => node.IsEntryPoint);
        var firstZone = graph.Nodes.First(node => node.NodeType == "Zone");
        entry.LayoutX.Should().BeLessThan(firstZone.LayoutX!.Value);
    }

    [Fact]
    public async Task ListAsync_ShowsLiveMapsToEveryoneAndDraftsOnlyToTheirAuthor()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var author = Guid.NewGuid();
        var (draft, _) = await editor.CreateDraftAsync(
            new MapCreateRequest("Zone", "draft-zone", "Draft Zone"),
            author);
        var stranger = new MapCaller(Guid.NewGuid(), CanManageAny: false, CanReview: false);

        (await editor.ListAsync(stranger)).Should().NotContain(
            map => map.Id == draft!.Id,
            "a draft is not part of the public portal");

        var own = (await editor.ListAsync(new MapCaller(author, false, false)))
            .Single(map => map.Id == draft!.Id);
        own.CanEdit.Should().BeTrue("the author may write their own map");

        // A live map is public whatever its author, and a stranger still cannot write it.
        var live = await db.Maps.SingleAsync(map => map.Id == HalrowMapId);
        live.IsActive = true;
        await db.SaveChangesAsync();

        var strangerView = await editor.ListAsync(stranger);
        strangerView.Should().Contain(map => map.Id == HalrowMapId);
        strangerView.Single(map => map.Id == HalrowMapId).CanEdit.Should().BeFalse();
    }

    [Fact]
    public async Task GetAsync_HidesADraftFromAStrangerAndMarksTheAuthorsOwnEditable()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var author = Guid.NewGuid();
        var (draft, _) = await editor.CreateDraftAsync(
            new MapCreateRequest("Zone", "hidden-zone", "Hidden Zone"),
            author);

        (await editor.GetAsync(draft!.Id, new MapCaller(Guid.NewGuid(), false, false))).Should().BeNull();
        (await editor.GetAsync(draft.Id, Editor)).Should().NotBeNull();

        var own = await editor.GetAsync(draft.Id, new MapCaller(author, false, false));
        own!.CanEdit.Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_StillHidesAStrangersDraft()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var (draft, _) = await editor.CreateDraftAsync(
            new MapCreateRequest("Zone", "stranger-hidden-zone", "Stranger Hidden Zone"), Guid.NewGuid());

        // Pins the answer after the check moved into `MapCaller.CanSee`: the same read, the same null.
        (await editor.GetAsync(draft!.Id, new MapCaller(Guid.NewGuid(), false, false))).Should().BeNull();

        // And the path the predicate adds: reviewing is enough on its own, without managing content.
        (await editor.GetAsync(draft.Id, new MapCaller(Guid.NewGuid(), CanManageAny: false, CanReview: true)))
            .Should().NotBeNull();
    }

    [Fact]
    public async Task SaveAndDelete_RefuseAMapCreatedByAnotherAccount()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var author = Guid.NewGuid();
        var (draft, _) = await editor.CreateDraftAsync(
            new MapCreateRequest("Zone", "guarded-zone", "Guarded Zone"),
            author);
        var stranger = new MapCaller(Guid.NewGuid(), CanManageAny: false, CanReview: false);

        var save = await editor.SaveAsync(draft!.Id, draft, draft.Version, stranger);
        save.Success.Should().BeFalse();
        save.Status.Should().Be("forbidden");

        var delete = await editor.DeleteDraftAsync(draft.Id, stranger);
        delete.Success.Should().BeFalse();
        delete.Status.Should().Be("forbidden");

        // The author gets past ownership; the graph then has to satisfy the world validator on its own.
        (await editor.SaveAsync(draft.Id, draft, draft.Version, new MapCaller(author, false, false)))
            .Status.Should().NotBe("forbidden");
        (await editor.DeleteDraftAsync(draft.Id, new MapCaller(author, false, false)))
            .Success.Should().BeTrue();
    }

    [Fact]
    public async Task SubmitAsync_RefusesAMapSubmittedByAStranger()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var (draft, _) = await editor.CreateDraftAsync(
            new MapCreateRequest("Zone", "submit-zone", "Submit Zone"),
            Guid.NewGuid());

        var refused = await editor.SubmitAsync(
            draft!.Id,
            new MapCaller(Guid.NewGuid(), CanManageAny: false, CanReview: false));

        refused.Status.Should().Be("forbidden");
    }

    [Fact]
    public async Task SaveAsync_ReturnsNotFoundForAnUnknownMap()
    {
        var (db, editor) = await SeededAsync();
        await using var _ = db;

        var graph = (await editor.GetAsync(HalrowMapId, Editor))!;

        var result = await editor.SaveAsync("zone:nowhere", graph with { Id = "zone:nowhere" }, 1, Editor);

        result.Success.Should().BeFalse();
        result.Status.Should().Be("not_found");
    }
}
