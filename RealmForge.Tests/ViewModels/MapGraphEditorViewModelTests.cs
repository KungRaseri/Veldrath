using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Models;
using RealmForge.Services;
using RealmForge.Tests.Infrastructure;
using RealmForge.ViewModels;

namespace RealmForge.Tests.ViewModels;

public sealed class MapGraphEditorViewModelTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly MapGraphEditorService _service;

    public MapGraphEditorViewModelTests()
    {
        var services = new ServiceCollection();
        var databaseName = $"forge-map-editor-{Guid.NewGuid():N}";
        services.AddDbContext<ContentDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        _provider = services.BuildServiceProvider();
        _service = new MapGraphEditorService(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MapGraphEditorService>.Instance);
    }

    private MapGraphEditorViewModel CreateViewModel() => new(_service);

    [Fact]
    public async Task LoadMapsAsync_Selects_First_Map_And_Populates_Rows()
    {
        await _service.SaveAsync(MapGraphTestData.CreateZoneGraph());
        var viewModel = CreateViewModel();

        await viewModel.LoadMapsAsync();

        viewModel.Maps.Should().ContainSingle();
        viewModel.MapId.Should().Be("zone:crestfall");
        viewModel.Name.Should().Be("Crestfall");
        viewModel.Nodes.Should().HaveCount(3);
        viewModel.Edges.Should().ContainSingle();
        viewModel.IsDirty.Should().BeFalse();
        viewModel.IsNew.Should().BeFalse();
        viewModel.Title.Should().Contain("zone:crestfall");
    }

    [Fact]
    public void AddNode_Assigns_Unique_Identifiers_And_Sort_Order()
    {
        var viewModel = CreateViewModel();
        viewModel.StartNewMap();

        viewModel.AddNode();
        viewModel.AddNode();
        viewModel.AddNode();

        viewModel.Nodes.Select(n => n.Id).Should().Equal("node-1", "node-2", "node-3");
        viewModel.Nodes[2].SortOrder.Should().Be(2);
        viewModel.IsDirty.Should().BeTrue();
        viewModel.NodeCountText.Should().Contain("3");
    }

    [Fact]
    public void RemoveSelectedNode_Removes_Referencing_Edges()
    {
        var viewModel = CreateViewModel();
        viewModel.StartNewMap();
        viewModel.AddNode();
        viewModel.AddNode();

        viewModel.SelectedNode = viewModel.Nodes[0];
        viewModel.AddEdge();
        viewModel.Edges.Should().ContainSingle();
        viewModel.Edges[0].FromNodeId.Should().Be("node-1");

        viewModel.RemoveSelectedNode();

        viewModel.Nodes.Should().ContainSingle();
        viewModel.Nodes[0].Id.Should().Be("node-2");
        viewModel.Edges.Should().BeEmpty();
        viewModel.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void BuildGraph_Projects_Map_And_Children()
    {
        var viewModel = CreateViewModel();
        viewModel.StartNewMap();
        viewModel.MapId = "zone:test";
        viewModel.OwnerId = "test";
        viewModel.Name = "Test Zone";
        viewModel.AddNode();
        viewModel.EntryNodeId = "node-1";

        var graph = viewModel.BuildGraph();

        graph.Id.Should().Be("zone:test");
        graph.OwnerId.Should().Be("test");
        graph.EntryNodeId.Should().Be("node-1");
        graph.Nodes.Should().ContainSingle();
        graph.Nodes[0].MapId.Should().Be("zone:test");
        graph.Nodes[0].Id.Should().Be("node-1");
    }

    [Fact]
    public void ValidateCurrent_Reports_Missing_Owner_And_Entry_Node()
    {
        var viewModel = CreateViewModel();
        viewModel.StartNewMap();
        viewModel.AddNode();

        viewModel.ValidateCurrent();

        viewModel.ValidationIssues.Should().NotBeEmpty();
        viewModel.ValidationSummary.Should().Contain("error");
    }

    [Fact]
    public async Task SaveAsync_Persists_New_Map_And_Reloads_The_List()
    {
        var viewModel = CreateViewModel();
        viewModel.StartNewMap();
        viewModel.MapId = "zone:new";
        viewModel.OwnerId = "new";
        viewModel.Name = "New Zone";
        viewModel.AddNode();
        viewModel.Nodes[0].NodeType = MapNodeType.EntryPoint;
        viewModel.Nodes[0].IsEntryPoint = true;
        viewModel.EntryNodeId = "node-1";

        await viewModel.SaveAsync();

        viewModel.IsDirty.Should().BeFalse();
        viewModel.IsNew.Should().BeFalse();
        viewModel.Maps.Should().Contain(m => m.Id == "zone:new");

        var saved = await _service.LoadAsync("zone:new");
        saved.Should().NotBeNull();
        saved!.Nodes.Should().ContainSingle();
    }

    [Fact]
    public async Task SaveAsync_Without_Map_Id_Does_Not_Persist()
    {
        var viewModel = CreateViewModel();
        viewModel.StartNewMap();

        await viewModel.SaveAsync();

        viewModel.StatusMessage.Should().Contain("Map id is required");
        viewModel.Maps.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteSelectedAsync_Removes_Map_From_Database_And_List()
    {
        await _service.SaveAsync(MapGraphTestData.CreateZoneGraph());
        var viewModel = CreateViewModel();
        await viewModel.LoadMapsAsync();

        await viewModel.DeleteSelectedAsync();

        viewModel.Maps.Should().BeEmpty();
        (await _service.LoadAllAsync()).Should().BeEmpty();
        viewModel.IsNew.Should().BeTrue();
    }

    [Fact]
    public async Task ImportJsonAsync_Populates_Editor()
    {
        await _service.SaveAsync(MapGraphTestData.CreateZoneGraph());
        var json = await _service.ExportJsonAsync();

        var viewModel = CreateViewModel();
        var written = await viewModel.ImportJsonAsync(json);

        written.Should().Be(1);
        viewModel.Maps.Should().ContainSingle();
        viewModel.MapId.Should().Be("zone:crestfall");
        viewModel.Nodes.Should().HaveCount(3);
    }

    [Fact]
    public async Task ExportToFileAsync_And_ImportFromFileAsync_RoundTrip()
    {
        await _service.SaveAsync(MapGraphTestData.CreateZoneGraph());
        var viewModel = CreateViewModel();
        await viewModel.LoadMapsAsync();
        var path = Path.Combine(Path.GetTempPath(), $"forge-vm-maps-{Guid.NewGuid():N}.json");

        try
        {
            (await viewModel.ExportToFileAsync(path)).Should().BeTrue();
            File.Exists(path).Should().BeTrue();

            var written = await viewModel.ImportFromFileAsync(path, overwriteExisting: true);

            written.Should().Be(1);
            viewModel.MapId.Should().Be("zone:crestfall");
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    public void Dispose() => _provider.Dispose();
}
