using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Models;
using RealmForge.Services;
using RealmForge.Tests.Infrastructure;

namespace RealmForge.Tests.Services;

public sealed class MapGraphEditorServiceTests : IDisposable
{
    private readonly List<ServiceProvider> _providers = [];

    private MapGraphEditorService CreateService(out ServiceProvider provider, bool configureDatabase = true)
    {
        var services = new ServiceCollection();
        if (configureDatabase)
        {
            var databaseName = $"forge-maps-{Guid.NewGuid():N}";
            services.AddDbContext<ContentDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));
        }

        provider = services.BuildServiceProvider();
        _providers.Add(provider);

        return new MapGraphEditorService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MapGraphEditorService>.Instance);
    }

    [Fact]
    public async Task SaveAsync_Creates_Map_With_Nodes_And_Edges()
    {
        var service = CreateService(out _);
        var graph = MapGraphTestData.CreateZoneGraph();

        (await service.SaveAsync(graph)).Should().BeTrue();

        var loaded = await service.LoadAsync(graph.Id);
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Crestfall");
        loaded.MapType.Should().Be(MapType.Zone);
        loaded.EntryNodeId.Should().Be($"{graph.Id}:entry");
        loaded.Nodes.Should().HaveCount(3);
        loaded.Edges.Should().ContainSingle();
        loaded.Nodes.Should().BeInAscendingOrder(n => n.SortOrder);
    }

    [Fact]
    public async Task SaveAsync_Updates_Map_And_Removes_Stale_Children()
    {
        var service = CreateService(out var provider);
        var graph = MapGraphTestData.CreateZoneGraph();
        (await service.SaveAsync(graph)).Should().BeTrue();

        var trimmed = graph with
        {
            Name = "Crestfall Rebuilt",
            Nodes = [.. graph.Nodes.Take(2)],
            Edges = [],
        };

        (await service.SaveAsync(trimmed)).Should().BeTrue();

        var loaded = await service.LoadAsync(graph.Id);
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Crestfall Rebuilt");
        loaded.Nodes.Should().HaveCount(2);
        loaded.Edges.Should().BeEmpty();

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
        var version = await db.Maps.Where(m => m.Id == graph.Id).Select(m => m.Version).SingleAsync();
        version.Should().Be(2);
    }

    [Fact]
    public async Task LoadAllAsync_Returns_Every_Map_Ordered_By_Id()
    {
        var service = CreateService(out _);
        await service.SaveAsync(MapGraphTestData.CreateZoneGraph("zone:zephyr"));
        await service.SaveAsync(MapGraphTestData.CreateZoneGraph("zone:amber"));

        var maps = await service.LoadAllAsync();

        maps.Select(m => m.Id).Should().Equal("zone:amber", "zone:zephyr");
    }

    [Fact]
    public async Task DeleteAsync_Removes_Map_And_Its_Children()
    {
        var service = CreateService(out _);
        var graph = MapGraphTestData.CreateZoneGraph();
        await service.SaveAsync(graph);

        (await service.DeleteAsync(graph.Id)).Should().BeTrue();

        (await service.LoadAsync(graph.Id)).Should().BeNull();
        (await service.LoadAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task ExportJsonAsync_Then_ImportJsonAsync_RoundTrips_Into_Another_Database()
    {
        var source = CreateService(out _);
        await source.SaveAsync(MapGraphTestData.CreateZoneGraph());

        var json = await source.ExportJsonAsync();
        json.Should().Contain("\"formatVersion\": 1");

        var target = CreateService(out _);
        (await target.ImportJsonAsync(json)).Should().Be(1);

        var loaded = await target.LoadAsync("zone:crestfall");
        loaded.Should().NotBeNull();
        loaded!.Nodes.Should().HaveCount(3);
        loaded.Edges.Should().ContainSingle();
    }

    [Fact]
    public async Task ImportJsonAsync_Without_Overwrite_Skips_Existing_Maps()
    {
        var service = CreateService(out _);
        await service.SaveAsync(MapGraphTestData.CreateZoneGraph());
        var json = await service.ExportJsonAsync();

        (await service.ImportJsonAsync(json, overwriteExisting: false)).Should().Be(0);
        (await service.ImportJsonAsync(json, overwriteExisting: true)).Should().Be(1);
    }

    [Fact]
    public async Task ExportToFileAsync_And_ImportFromFileAsync_RoundTrip()
    {
        var service = CreateService(out _);
        await service.SaveAsync(MapGraphTestData.CreateZoneGraph());
        var path = Path.Combine(Path.GetTempPath(), $"forge-map-graphs-{Guid.NewGuid():N}.json");

        try
        {
            (await service.ExportToFileAsync(path)).Should().BeTrue();
            File.Exists(path).Should().BeTrue();

            var target = CreateService(out _);
            (await target.ImportFromFileAsync(path)).Should().Be(1);
            (await target.LoadAllAsync()).Should().ContainSingle();
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task Missing_Database_Reports_Error_Instead_Of_Throwing()
    {
        var service = CreateService(out _, configureDatabase: false);

        var maps = await service.LoadAllAsync();
        maps.Should().BeEmpty();
        service.LastError.Should().Contain("not configured");

        (await service.SaveAsync(MapGraphTestData.CreateZoneGraph())).Should().BeFalse();
        service.LastError.Should().Contain("not configured");
    }

    [Fact]
    public void Validate_Flags_Missing_Entry_Node()
    {
        var graph = MapGraphTestData.CreateZoneGraph() with { EntryNodeId = null };

        var result = MapGraphEditorService.Validate(graph);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Code == "entry_missing");
    }

    public void Dispose()
    {
        foreach (var provider in _providers)
            provider.Dispose();
    }
}
