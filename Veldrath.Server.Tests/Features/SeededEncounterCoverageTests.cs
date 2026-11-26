using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using MapNodeType = RealmEngine.Shared.Models.MapNodeType;
using MapType = RealmEngine.Shared.Models.MapType;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Guards the shape of the seeded world's encounters: what spawns where, at what level, and what does not spawn at
/// all.
/// </summary>
/// <remarks>
/// <para>
/// This exists because the world once shipped with **no** spawn pool anywhere: a player could walk every zone and
/// never meet an enemy, and the content audit could not catch it, because it words an empty pool as a warning
/// ("safe locations are legitimate") and a legitimately safe town looks exactly like a forgotten one.
/// </para>
/// <para>
/// The rules below encode the intent instead: settlements and dungeon lobbies are empty on purpose, every other
/// location spawns something hostile, and the archetype it names exists and belongs to that location's level band.
/// </para>
/// </remarks>
[Trait("Category", "Validation")]
public class SeededEncounterCoverageTests
{
    [Fact]
    public async Task Settlements_and_dungeon_lobbies_never_spawn_anything()
    {
        await using var probe = new SeededContentProbe();
        await using var content = await probe.CreateContextAsync();

        var locations = await content.ZoneLocations.AsNoTracking().ToListAsync();

        // A settlement (IsTown), a respawn point (SafeRespawn) or a dungeon lobby (IsDungeon == false) must be quiet:
        // respawning into a location that spawns hostiles is a death loop, which is exactly what the e2e found.
        var shouldBeQuiet = locations
            .Where(location =>
                location.Traits.IsTown == true ||
                location.Traits.SafeRespawn == true ||
                location.Traits.IsDungeon == false)
            .ToList();

        shouldBeQuiet.Should().NotBeEmpty("the seed has towns, waypoints and one lobby per dungeon zone");

        var noisy = shouldBeQuiet
            .Where(location => location.ActorPool.Count > 0)
            .Select(location => location.Slug)
            .ToList();

        noisy.Should().BeEmpty("a town, respawn point or lobby that spawns hostiles is a content bug");

        // A placed named actor counts as spawn content too: putting a boss in the square is the same bug as pooling one
        // there, and the roster now emits placements as well.
        var quietSlugs = shouldBeQuiet.Select(location => location.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var placedInQuiet = await content.ActorInstances.AsNoTracking()
            .Where(instance => instance.IsActive && instance.LocationSlug != null)
            .Select(instance => new { instance.Slug, instance.LocationSlug })
            .ToListAsync();

        placedInQuiet
            .Where(instance => quietSlugs.Contains(instance.LocationSlug!))
            .Select(instance => $"{instance.LocationSlug}/{instance.Slug}")
            .Should()
            .BeEmpty("a named actor placed in a town, respawn point or lobby would stand there to be fought");
    }

    [Fact]
    public async Task Every_placed_instance_stands_somewhere_real_hostile_and_alone()
    {
        await using var probe = new SeededContentProbe();
        await using var content = await probe.CreateContextAsync();

        var placed = await content.ActorInstances.AsNoTracking()
            .Where(instance => instance.IsActive && instance.LocationSlug != null)
            .ToListAsync();

        placed.Should().NotBeEmpty("the seed places the two named actors this card made real");

        var locations = (await content.ZoneLocations.AsNoTracking().ToListAsync())
            .ToDictionary(location => location.Slug, StringComparer.OrdinalIgnoreCase);
        var archetypesById = (await content.ActorArchetypes.AsNoTracking().ToListAsync())
            .ToDictionary(archetype => archetype.Id);

        var problems = new List<string>();

        foreach (var instance in placed)
        {
            if (!locations.ContainsKey(instance.LocationSlug!))
            {
                problems.Add($"'{instance.Slug}' is placed at unknown location '{instance.LocationSlug}'");
                continue;
            }

            if (!archetypesById.TryGetValue(instance.ArchetypeId, out var archetype))
            {
                problems.Add($"'{instance.Slug}' overrides a missing archetype");
                continue;
            }

            if (archetype.Traits.Hostile != true)
            {
                problems.Add($"'{instance.Slug}' is placed but its archetype '{archetype.Slug}' is not hostile");
            }
        }

        problems.Should().BeEmpty();

        placed
            .Where(instance => string.Equals(instance.TypeKey, "boss", StringComparison.OrdinalIgnoreCase))
            .GroupBy(instance => instance.LocationSlug!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key} holds {group.Count()} bosses")
            .Should()
            .BeEmpty("one location, one unique boss — a boss is made unique by being placed, not by being pooled");
    }

    [Fact]
    public async Task Ashfields_resolves_exactly_one_respawn_anchor()
    {
        await using var probe = new SeededContentProbe();
        await using var content = await probe.CreateContextAsync();

        var ashfields = await content.ZoneLocations.AsNoTracking()
            .Where(location => location.ZoneId == "ashfields")
            .ToListAsync();

        // L3 (t_6c70dc04) moved the respawn flag off the battlefield and onto the grove, and made the
        // keeper-only shrine a rest point too. Two flags are correct; the point of this test is that only
        // one of them can ever be the anchor, because RespawnHubCommand resolves through
        // IZoneLocationRepository.GetByZoneIdAsync, which drops IsHidden locations.
        ashfields
            .Where(location => location.Traits.SafeRespawn == true)
            .Select(location => location.Slug)
            .Should().BeEquivalentTo(new[] { "obsidian-grove", "ash-shrine" },
                "the grove is the respawn point; the shrine is a keeper-only rest point");

        ashfields
            .Where(location => location.Traits.SafeRespawn == true && location.Traits.IsHidden != true)
            .Select(location => location.Slug)
            .Should().ContainSingle("a hidden location is never offered to the respawn resolver")
            .Which.Should().Be("obsidian-grove", "the anchor is deterministic, not whichever row loads first");

        var battlefield = ashfields.Single(location => location.Slug == "scorched-battlefield");
        battlefield.Traits.SafeRespawn.Should().NotBe(true, "a respawn point on a battlefield reads wrong");
        battlefield.ActorPool.Should().NotBeEmpty("the battlefield keeps a pool (amendment 4)");
    }

    [Fact]
    public async Task Every_other_location_can_spawn_something()
    {
        await using var probe = new SeededContentProbe();
        await using var content = await probe.CreateContextAsync();

        var locations = await content.ZoneLocations.AsNoTracking().ToListAsync();
        var quiet = locations
            .Where(location =>
                location.Traits.IsTown != true &&
                location.Traits.SafeRespawn != true &&
                location.Traits.IsDungeon != false)
            .Where(location => location.ActorPool.Count == 0)
            .Select(location => $"{location.ZoneId}/{location.Slug}")
            .ToList();

        quiet.Should().BeEmpty("every non-settlement location is somewhere a fight can happen");

        var pooled = locations.Count(location => location.ActorPool.Count > 0);
        pooled.Should().BeGreaterThan(25, "the encounter map covers the whole level ladder, not one starter wood");
    }

    [Fact]
    public async Task Every_pooled_archetype_exists_and_is_hostile()
    {
        await using var probe = new SeededContentProbe();
        await using var content = await probe.CreateContextAsync();

        var archetypes = await content.ActorArchetypes.AsNoTracking().ToListAsync();
        var bySlug = archetypes.ToDictionary(archetype => archetype.Slug, StringComparer.Ordinal);
        var locations = await content.ZoneLocations.AsNoTracking().ToListAsync();

        var problems = new List<string>();
        foreach (var location in locations)
        {
            foreach (var entry in location.ActorPool)
            {
                if (!bySlug.TryGetValue(entry.ArchetypeSlug, out var archetype))
                {
                    problems.Add($"{location.Slug} pools unknown archetype '{entry.ArchetypeSlug}'");
                    continue;
                }

                if (archetype.Traits.Hostile != true)
                {
                    problems.Add($"{location.Slug} pools '{entry.ArchetypeSlug}', which is not hostile");
                }

                if (entry.Weight <= 0)
                {
                    problems.Add($"{location.Slug} pools '{entry.ArchetypeSlug}' with weight {entry.Weight}");
                }
            }
        }

        problems.Should().BeEmpty();
    }

    [Fact]
    public async Task Every_pooled_archetype_fits_its_location_level_band()
    {
        await using var probe = new SeededContentProbe();
        await using var content = await probe.CreateContextAsync();

        var archetypes = await content.ActorArchetypes.AsNoTracking()
            .ToDictionaryAsync(archetype => archetype.Slug, StringComparer.Ordinal);
        var locations = await content.ZoneLocations.AsNoTracking().ToListAsync();

        var mismatches = new List<string>();
        foreach (var location in locations)
        {
            var locationMin = location.Stats.MinLevel ?? 0;
            var locationMax = location.Stats.MaxLevel ?? int.MaxValue;

            foreach (var entry in location.ActorPool)
            {
                if (!archetypes.TryGetValue(entry.ArchetypeSlug, out var archetype)) continue;

                // The bands only have to overlap: a level 3–6 wolf on a level 5–12 moor is an early encounter there,
                // which is fine. A band that misses the location entirely is what this catches.
                var overlaps = archetype.MinLevel <= locationMax && archetype.MaxLevel >= locationMin;
                if (!overlaps)
                {
                    mismatches.Add(
                        $"{location.Slug} ({locationMin}-{locationMax}) pools {archetype.Slug} ({archetype.MinLevel}-{archetype.MaxLevel})");
                }
            }
        }

        mismatches.Should().BeEmpty("a pool whose band misses the location sends level 1 rats to a level 26 vault");
    }

    [Fact]
    public async Task Every_archetype_loot_table_resolves()
    {
        await using var probe = new SeededContentProbe();
        await using var content = await probe.CreateContextAsync();

        var lootTableIds = await content.LootTables.AsNoTracking().Select(table => table.Id).ToListAsync();
        var known = lootTableIds.ToHashSet();
        var archetypes = await content.ActorArchetypes.AsNoTracking().ToListAsync();

        var dangling = archetypes
            .Where(archetype => archetype.LootTableId is not null && !known.Contains(archetype.LootTableId.Value))
            .Select(archetype => archetype.Slug)
            .ToList();

        dangling.Should().BeEmpty("a loot table id that resolves to nothing is a reward that silently disappears");

        var bosses = archetypes
            .Where(archetype => archetype.Traits.Hostile == true && archetype.Traits.Boss == true)
            .ToList();

        bosses.Should().NotBeEmpty("the ladder has bosses at its top");
        bosses.Should().OnlyContain(archetype => archetype.LootTableId != null,
            "a boss with no loot table is a fight with no reason to have it");
    }

    [Fact]
    public async Task Every_zone_entry_can_walk_straight_to_its_locations()
    {
        await using var probe = new SeededContentProbe();
        await using var content = await probe.CreateContextAsync();

        var maps = await content.Maps.AsNoTracking()
            .Include(map => map.Nodes)
            .Include(map => map.Edges)
            .ToListAsync();

        var unreachable = new List<string>();

        foreach (var map in maps.Where(map => map.MapType == MapType.Zone))
        {
            if (string.IsNullOrWhiteSpace(map.EntryNodeId))
            {
                unreachable.Add($"{map.Id} has no entry node");
                continue;
            }

            // Walk only: travelling to an exit node leaves the zone, so a path through one is not a path a player has.
            var exits = map.Nodes
                .Where(node => node.NodeType == MapNodeType.ZoneExit || node.NodeType == MapNodeType.RegionExit)
                .Select(node => node.Id)
                .ToHashSet(StringComparer.Ordinal);

            var reachable = new HashSet<string>(StringComparer.Ordinal) { map.EntryNodeId };
            var queue = new Queue<string>();
            queue.Enqueue(map.EntryNodeId);

            while (queue.Count > 0)
            {
                var nodeId = queue.Dequeue();
                foreach (var edge in map.Edges.Where(edge =>
                             edge.IsTraversable && (edge.FromNodeId == nodeId || edge.ToNodeId == nodeId)))
                {
                    var next = edge.FromNodeId == nodeId ? edge.ToNodeId : edge.FromNodeId;
                    if (exits.Contains(next)) continue;
                    if (reachable.Add(next)) queue.Enqueue(next);
                }
            }

            foreach (var location in map.Nodes.Where(node => node.NodeType == MapNodeType.Location))
            {
                if (!reachable.Contains(location.Id)) unreachable.Add($"{map.Id}/{location.Id}");
            }
        }

        unreachable.Should().BeEmpty(
            "the authored graphs linked the entry only to the zone's exits, so a character could arrive but never walk to a location");
    }

    [Fact]
    public async Task Every_hostile_band_has_an_archetype()
    {
        await using var probe = new SeededContentProbe();
        await using var content = await probe.CreateContextAsync();

        var hostiles = await content.ActorArchetypes.AsNoTracking()
            .Where(archetype => archetype.Traits.Hostile == true)
            .ToListAsync();

        // The world runs 0–30; without a band above 6 nothing past the starter woods can be populated.
        hostiles.Should().Contain(archetype => archetype.MinLevel >= 20, "the Cinderplain ladder needs its own actors");
        hostiles.Should().Contain(archetype => archetype.MinLevel >= 6 && archetype.MinLevel <= 10,
            "Greymoor starts at level 7");
        hostiles.Should().Contain(archetype => archetype.MaxLevel >= 14, "Saltcliff runs to 20");
        hostiles.Count.Should().BeGreaterThan(8);
    }
}
