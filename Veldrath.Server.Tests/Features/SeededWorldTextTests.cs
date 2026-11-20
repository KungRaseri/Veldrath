using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Tests.Infrastructure;
using MapNodeType = RealmEngine.Shared.Models.MapNodeType;
using MapType = RealmEngine.Shared.Models.MapType;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Guards the shipped world text: the seeded prose is what players read in map tooltips, the dungeon panel and the
/// <c>ZoneEntered</c> payload, so retired lore must not survive there and the two seeders must not drift apart.
/// </summary>
/// <remarks>
/// The names this suite rejects were replaced in the canon rewrite (see
/// <c>.github/agent-memory/world-lore-plan.md</c>): Sundering / Age of Embers / Dravan / Thysmara / Kaldrek became
/// Collapse / Ashen Age / Calethic Empire / Aethori / Unbound Ruin. Matching is case-sensitive on purpose — the zone
/// id <c>kaldrek-maw</c> is an opaque key and is expected to stay.
/// </remarks>
[Trait("Category", "Validation")]
public class SeededWorldTextTests
{
    private static readonly string[] RetiredTokens = ["Sundering", "Age of Embers", "Dravan", "Thysmara", "Kaldrek"];

    private const string ExpectedEra = "The Ashen Age (the Age of Reclaiming)";

    [Fact]
    public async Task Seeded_world_text_carries_no_retired_lore()
    {
        await using var probe = new SeededWorldProbe();
        await using var app = probe.CreateApplicationContext();
        await using var content = await probe.CreateContentContextAsync();

        var offenders = new List<string>();

        void Check(string entity, string field, string? value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            foreach (var token in RetiredTokens)
            {
                if (value.Contains(token, StringComparison.Ordinal))
                    offenders.Add($"{entity}.{field} contains retired token \"{token}\": \"{value}\"");
            }
        }

        var world = await app.Worlds.AsNoTracking().SingleAsync();
        Check($"world:{world.Id}", nameof(World.Description), world.Description);
        Check($"world:{world.Id}", nameof(World.Era), world.Era);

        foreach (var region in await app.Regions.AsNoTracking().ToListAsync())
        {
            Check($"region:{region.Id}", nameof(Region.Name), region.Name);
            Check($"region:{region.Id}", nameof(Region.Description), region.Description);
        }

        foreach (var zone in await app.Zones.AsNoTracking().ToListAsync())
        {
            Check($"zone:{zone.Id}", nameof(Zone.Name), zone.Name);
            Check($"zone:{zone.Id}", nameof(Zone.Description), zone.Description);
        }

        foreach (var map in await content.Maps.AsNoTracking().ToListAsync())
        {
            Check($"map:{map.Id}", nameof(Map.Name), map.Name);
            Check($"map:{map.Id}", nameof(Map.Description), map.Description);
        }

        foreach (var node in await content.MapNodes.AsNoTracking().ToListAsync())
        {
            Check($"mapnode:{node.Id}", nameof(MapNode.DisplayName), node.DisplayName);
            Check($"mapnode:{node.Id}", nameof(MapNode.Description), node.Description);
        }

        offenders.Should().BeEmpty("the seeded world text is player-facing and must not carry retired lore");
    }

    [Fact]
    public async Task Map_graph_copies_agree_with_the_region_and_zone_seed()
    {
        // The original defect was two hand-maintained copies of the same prose drifting apart: the region/zone row
        // said one thing and the map node the player actually hovers said another. This is the assertion that
        // catches that drift, so a future lore edit must land in both seeders.
        await using var probe = new SeededWorldProbe();
        await using var app = probe.CreateApplicationContext();
        await using var content = await probe.CreateContentContextAsync();

        var regions = await app.Regions.AsNoTracking().ToListAsync();
        var zones = await app.Zones.AsNoTracking().ToListAsync();
        var maps = await content.Maps.AsNoTracking().ToListAsync();
        var nodes = await content.MapNodes.AsNoTracking().ToListAsync();

        var mismatches = new List<string>();

        void Compare(string left, string right, string? leftValue, string? rightValue)
        {
            if (!string.Equals(leftValue, rightValue, StringComparison.Ordinal))
                mismatches.Add($"{left} says \"{leftValue}\" but {right} says \"{rightValue}\"");
        }

        foreach (var region in regions)
        {
            var map = maps.SingleOrDefault(candidate => candidate.MapType == MapType.Region && candidate.OwnerId == region.Id);
            if (map is null)
            {
                mismatches.Add($"region:{region.Id} has no region map");
            }
            else
            {
                Compare($"map:{map.Id}.Description", $"region:{region.Id}.Description", map.Description, region.Description);
            }
        }

        foreach (var zone in zones)
        {
            var map = maps.SingleOrDefault(candidate => candidate.MapType == MapType.Zone && candidate.OwnerId == zone.Id);
            if (map is null)
            {
                mismatches.Add($"zone:{zone.Id} has no zone map");
            }
            else
            {
                Compare($"map:{map.Id}.Description", $"zone:{zone.Id}.Description", map.Description, zone.Description);
            }

            var zoneNodes = nodes
                .Where(node => node.NodeType == MapNodeType.Zone && node.RefId == zone.Id)
                .ToList();

            if (zoneNodes.Count == 0 && !zone.IsDevOnly)
                mismatches.Add($"zone:{zone.Id} has no node on a region map");

            foreach (var node in zoneNodes)
                Compare($"mapnode:{node.Id}.Description", $"zone:{zone.Id}.Description", node.Description, zone.Description);
        }

        // Region exits on a region map carry the destination region's prose, so they are a third copy of it.
        foreach (var node in nodes.Where(node =>
                     node.NodeType == MapNodeType.RegionExit
                     && node.MapId.StartsWith("region:", StringComparison.Ordinal)))
        {
            var region = regions.SingleOrDefault(candidate => candidate.Id == node.RefId);
            if (region is null)
            {
                mismatches.Add($"mapnode:{node.Id} exits to unknown region \"{node.RefId}\"");
                continue;
            }

            Compare($"mapnode:{node.Id}.Description", $"region:{region.Id}.Description", node.Description, region.Description);
        }

        mismatches.Should().BeEmpty("the map graph mirrors the region/zone seed and the two copies must not drift");
    }

    [Fact]
    public async Task World_era_names_both_eras()
    {
        await using var probe = new SeededWorldProbe();
        await using var app = probe.CreateApplicationContext();

        var world = await app.Worlds.AsNoTracking().SingleAsync();

        world.Era.Should().Be(ExpectedEra, "the Ashen Age and the Age of Reclaiming are in simultaneous use");
    }

    /// <summary>Builds a seeded world-geography context and a seeded content context, disposed together.</summary>
    private sealed class SeededWorldProbe : IAsyncDisposable
    {
        private readonly TestDbContextFactory _applicationFactory = new();
        private readonly ServiceProvider _contentProvider;
        private readonly AsyncServiceScope _contentScope;

        public SeededWorldProbe()
        {
            var databaseName = $"world-text-{Guid.NewGuid()}";

            _contentProvider = new ServiceCollection()
                .AddLogging()
                .AddDbContext<ContentDbContext>(options => options.UseInMemoryDatabase(databaseName))
                .BuildServiceProvider();

            _contentScope = _contentProvider.CreateAsyncScope();
        }

        /// <summary>Returns an application context seeded with the world, regions and zones.</summary>
        public ApplicationDbContext CreateApplicationContext() => _applicationFactory.CreateContext();

        /// <summary>Seeds the content catalog and returns a context over it, including the map graph.</summary>
        public async Task<ContentDbContext> CreateContentContextAsync()
        {
            await DatabaseSeeder.SeedAsync(_contentProvider);
            return _contentScope.ServiceProvider.GetRequiredService<ContentDbContext>();
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await _contentScope.DisposeAsync();
            await _contentProvider.DisposeAsync();
            _applicationFactory.Dispose();
        }
    }
}
