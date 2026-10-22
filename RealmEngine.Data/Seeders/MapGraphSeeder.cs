using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Seeders;

/// <summary>
/// Seeds the authored map graph (region and zone nodes plus cardinal edges).
/// Generated once from the legacy TMX layer during migration; this seeder is the
/// long-term source of truth and no longer depends on TMX assets or importer code.
/// </summary>
public static class MapGraphSeeder
{
    /// <summary>Seeds every map, node, and edge when the <c>Maps</c> table is empty (idempotent).</summary>
    /// <param name="db">Content database context.</param>
    public static async Task SeedAsync(ContentDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);

        if (await db.Maps.AnyAsync())
            return;

        var now = DateTimeOffset.UtcNow;
        db.Maps.AddRange(GetMaps(now));
        await db.SaveChangesAsync();
    }

    private static Map[] GetMaps(DateTimeOffset now) =>
    [
        new Map
        {
            Id = "region:cinderplain",
            MapType = MapType.Region,
            OwnerId = "cinderplain",
            Name = "Cinderplain",
            Description = "Kaldrek's scorched expanse of ash and cooled lava, where forge-tempered warbands and fire-touched creatures stake their claim.",
            EntryNodeId = "region:cinderplain:entry",
            IsActive = true,
            Version = 1,
            UpdatedAt = now,
            Nodes =
            [
                new MapNode { Id = "region:cinderplain:entry", MapId = "region:cinderplain", NodeType = MapNodeType.EntryPoint, RefId = null, DisplayName = "Arrival", Description = "Arrival point for Cinderplain.", IconKey = "login", LayoutX = 2, LayoutY = 20, IsEntryPoint = true, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 0 },
                new MapNode { Id = "region:cinderplain:zone:ashfields", MapId = "region:cinderplain", NodeType = MapNodeType.Zone, RefId = "ashfields", DisplayName = "The Ashfields", Description = "Grey ash plains stretching to the horizon, dotted with fused obsidian trees and the scorched skulls of fallen armies.", IconKey = "map", LayoutX = 34, LayoutY = 14, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 20 },
                new MapNode { Id = "region:cinderplain:zone:skarhold", MapId = "region:cinderplain", NodeType = MapNodeType.Zone, RefId = "skarhold", DisplayName = "Skarhold", Description = "A Kaldrek forge-city built inside a dormant caldera, where the best smiths in Veldrath ply their trade under sulphur skies.", IconKey = "map", LayoutX = 14, LayoutY = 20, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 30 },
                new MapNode { Id = "region:cinderplain:zone:smoldering-reach", MapId = "region:cinderplain", NodeType = MapNodeType.Zone, RefId = "smoldering-reach", DisplayName = "The Smoldering Reach", Description = "A cracked lava field where vents of superheated gas erupt without warning and heat-adapted predators hunt at dusk.", IconKey = "map", LayoutX = 38, LayoutY = 30, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 40 },
                new MapNode { Id = "region:cinderplain:zone:kaldrek-maw", MapId = "region:cinderplain", NodeType = MapNodeType.Zone, RefId = "kaldrek-maw", DisplayName = "Kaldrek's Maw", Description = "A vast volcanic vent complex known as the Maw, lair of Kaldrek's fire-bound ancient and final test of the worthy.", IconKey = "map", LayoutX = 20, LayoutY = 36, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 50 },
                new MapNode { Id = "region:cinderplain:exit:greymoor", MapId = "region:cinderplain", NodeType = MapNodeType.RegionExit, RefId = "greymoor", DisplayName = "Greymoor", Description = "Fog-wreathed Dravan highlands where ancient burial mounds dot the heather and wandering spirits trouble the living.", IconKey = "public", LayoutX = 0, LayoutY = 20, IsEntryPoint = false, IsExitPoint = true, TargetMapId = "region:greymoor", TargetNodeId = "region:greymoor:entry", IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 60 },
            ],
            Edges =
            [
                new MapEdge { Id = new Guid("b3cd2f00-d1f6-7012-dfb6-92677d07b7e9"), MapId = "region:cinderplain", FromNodeId = "region:cinderplain:entry", ToNodeId = "region:cinderplain:exit:greymoor", Direction = MapDirection.West, EdgeType = MapEdgeType.ExitToRegion, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("1ad207ad-0970-f82b-ce19-9a1e92dfea3d"), MapId = "region:cinderplain", FromNodeId = "region:cinderplain:exit:greymoor", ToNodeId = "region:cinderplain:zone:skarhold", Direction = MapDirection.East, EdgeType = MapEdgeType.ExitToRegion, Label = "skarhold-greymoor", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("4c22f343-cd20-7c58-99bf-20006abcc1ce"), MapId = "region:cinderplain", FromNodeId = "region:cinderplain:zone:ashfields", ToNodeId = "region:cinderplain:zone:smoldering-reach", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = "ashfields-smoldering-reach", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("1d1cf32b-6480-a9a0-7cfe-abea02312102"), MapId = "region:cinderplain", FromNodeId = "region:cinderplain:zone:ashfields", ToNodeId = "region:cinderplain:zone:skarhold", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = "skarhold-ashfields", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("54590fd6-c188-602f-5648-e51699ee7b61"), MapId = "region:cinderplain", FromNodeId = "region:cinderplain:zone:kaldrek-maw", ToNodeId = "region:cinderplain:zone:skarhold", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = "skarhold-kaldrek-maw", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("4969bc1e-1cb4-1954-252a-544e4410fa97"), MapId = "region:cinderplain", FromNodeId = "region:cinderplain:zone:kaldrek-maw", ToNodeId = "region:cinderplain:zone:smoldering-reach", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = "smoldering-reach-kaldrek-maw", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("60d63009-2499-6642-482c-540978c9445f"), MapId = "region:cinderplain", FromNodeId = "region:cinderplain:zone:skarhold", ToNodeId = "region:cinderplain:zone:kaldrek-maw", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = "skarhold-kaldrek-maw", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("5aff1509-367f-2020-256f-274b301fafbb"), MapId = "region:cinderplain", FromNodeId = "region:cinderplain:zone:skarhold", ToNodeId = "region:cinderplain:zone:ashfields", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = "skarhold-ashfields", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("4c7d2081-06c8-64cc-9156-48d2afb37b6a"), MapId = "region:cinderplain", FromNodeId = "region:cinderplain:zone:skarhold", ToNodeId = "region:cinderplain:exit:greymoor", Direction = MapDirection.West, EdgeType = MapEdgeType.ExitToRegion, Label = "skarhold-greymoor", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("931badd4-c894-8f77-4ce2-a437ae440efa"), MapId = "region:cinderplain", FromNodeId = "region:cinderplain:zone:smoldering-reach", ToNodeId = "region:cinderplain:zone:ashfields", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = "ashfields-smoldering-reach", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("1975c21a-e983-ded7-2c05-7f458c3921e8"), MapId = "region:cinderplain", FromNodeId = "region:cinderplain:zone:smoldering-reach", ToNodeId = "region:cinderplain:zone:kaldrek-maw", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = "smoldering-reach-kaldrek-maw", IsTraversable = true, IsHidden = false },
            ],
        },
        new Map
        {
            Id = "region:greymoor",
            MapType = MapType.Region,
            OwnerId = "greymoor",
            Name = "Greymoor",
            Description = "Fog-wreathed Dravan highlands where ancient burial mounds dot the heather and wandering spirits trouble the living.",
            EntryNodeId = "region:greymoor:entry",
            IsActive = true,
            Version = 1,
            UpdatedAt = now,
            Nodes =
            [
                new MapNode { Id = "region:greymoor:entry", MapId = "region:greymoor", NodeType = MapNodeType.EntryPoint, RefId = null, DisplayName = "Arrival", Description = "Arrival point for Greymoor.", IconKey = "login", LayoutX = 12, LayoutY = 20, IsEntryPoint = true, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 0 },
                new MapNode { Id = "region:greymoor:zone:pale-moor", MapId = "region:greymoor", NodeType = MapNodeType.Zone, RefId = "pale-moor", DisplayName = "The Pale Moor", Description = "Endless fog-blanketed moors where waymarks shift overnight and travellers learn quickly to trust local guides.", IconKey = "map", LayoutX = 30, LayoutY = 10, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 20 },
                new MapNode { Id = "region:greymoor:zone:aldenmere", MapId = "region:greymoor", NodeType = MapNodeType.Zone, RefId = "aldenmere", DisplayName = "Aldenmere", Description = "A grey-stoned Dravan waytown built atop old foundations, known for its mead, its mercenaries, and its many secrets.", IconKey = "map", LayoutX = 12, LayoutY = 18, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 30 },
                new MapNode { Id = "region:greymoor:zone:soddenfen", MapId = "region:greymoor", NodeType = MapNodeType.Zone, RefId = "soddenfen", DisplayName = "Soddenfen", Description = "A waterlogged fenland buzzing with plague insects and half-submerged ruins, avoided by all with good sense.", IconKey = "map", LayoutX = 36, LayoutY = 28, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 40 },
                new MapNode { Id = "region:greymoor:zone:barrow-deeps", MapId = "region:greymoor", NodeType = MapNodeType.Zone, RefId = "barrow-deeps", DisplayName = "The Barrow Deeps", Description = "A sprawling underground burial network carved through the moorland, where ancestor-beasts guard ancient Dravan relics.", IconKey = "map", LayoutX = 18, LayoutY = 34, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 50 },
                new MapNode { Id = "region:greymoor:exit:varenmark", MapId = "region:greymoor", NodeType = MapNodeType.RegionExit, RefId = "varenmark", DisplayName = "Varenmark", Description = "The oldest settled march in the lowlands. Farmsteads, drove roads and market crossings gave way to wilder land. Whatever was here before the settlers is still here.", IconKey = "public", LayoutX = 0, LayoutY = 20, IsEntryPoint = false, IsExitPoint = true, TargetMapId = "region:varenmark", TargetNodeId = "region:varenmark:entry", IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 60 },
                new MapNode { Id = "region:greymoor:exit:cinderplain", MapId = "region:greymoor", NodeType = MapNodeType.RegionExit, RefId = "cinderplain", DisplayName = "Cinderplain", Description = "Kaldrek's scorched expanse of ash and cooled lava, where forge-tempered warbands and fire-touched creatures stake their claim.", IconKey = "public", LayoutX = 47, LayoutY = 20, IsEntryPoint = false, IsExitPoint = true, TargetMapId = "region:cinderplain", TargetNodeId = "region:cinderplain:entry", IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 70 },
                new MapNode { Id = "region:greymoor:exit:saltcliff", MapId = "region:greymoor", NodeType = MapNodeType.RegionExit, RefId = "saltcliff", DisplayName = "Saltcliff", Description = "Thysmara sea cliffs battered by storm winds, home to sailors, smugglers, and the drowned remnants of a sunken empire.", IconKey = "public", LayoutX = 24, LayoutY = 39, IsEntryPoint = false, IsExitPoint = true, TargetMapId = "region:saltcliff", TargetNodeId = "region:saltcliff:entry", IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 80 },
            ],
            Edges =
            [
                new MapEdge { Id = new Guid("d53b751c-ceac-24fc-1de2-d82728c763d8"), MapId = "region:greymoor", FromNodeId = "region:greymoor:entry", ToNodeId = "region:greymoor:zone:aldenmere", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("da1ec8f4-ff2b-8970-25f3-4bfe297f1cec"), MapId = "region:greymoor", FromNodeId = "region:greymoor:exit:cinderplain", ToNodeId = "region:greymoor:zone:pale-moor", Direction = MapDirection.West, EdgeType = MapEdgeType.ExitToRegion, Label = "palemoor-cinderplain", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("1271109c-76f1-fcd5-ed98-152965c9b903"), MapId = "region:greymoor", FromNodeId = "region:greymoor:exit:saltcliff", ToNodeId = "region:greymoor:zone:barrow-deeps", Direction = MapDirection.West, EdgeType = MapEdgeType.ExitToRegion, Label = "barrowdeeps-saltcliff", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("e72498fd-9eb3-7533-588f-42ee02cba54c"), MapId = "region:greymoor", FromNodeId = "region:greymoor:exit:varenmark", ToNodeId = "region:greymoor:zone:aldenmere", Direction = MapDirection.East, EdgeType = MapEdgeType.ExitToRegion, Label = "aldenmere-varenmark", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("2e45c28f-1865-cb24-74d2-9da80e4fe978"), MapId = "region:greymoor", FromNodeId = "region:greymoor:zone:aldenmere", ToNodeId = "region:greymoor:zone:barrow-deeps", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = "aldenmere-barrowdeeps", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("e457dca8-98d5-6b0c-5439-f9104d960e39"), MapId = "region:greymoor", FromNodeId = "region:greymoor:zone:aldenmere", ToNodeId = "region:greymoor:zone:pale-moor", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = "aldenmere-palemoor", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("035c9bb9-2e11-b214-60bb-45b796b0d730"), MapId = "region:greymoor", FromNodeId = "region:greymoor:zone:aldenmere", ToNodeId = "region:greymoor:exit:varenmark", Direction = MapDirection.West, EdgeType = MapEdgeType.ExitToRegion, Label = "aldenmere-varenmark", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("cfa0acbd-a8be-84b2-626b-def3b707bff1"), MapId = "region:greymoor", FromNodeId = "region:greymoor:zone:barrow-deeps", ToNodeId = "region:greymoor:zone:aldenmere", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = "aldenmere-barrowdeeps", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("1bf59822-0eaa-287d-5802-7d3f94eb9b19"), MapId = "region:greymoor", FromNodeId = "region:greymoor:zone:barrow-deeps", ToNodeId = "region:greymoor:exit:saltcliff", Direction = MapDirection.East, EdgeType = MapEdgeType.ExitToRegion, Label = "barrowdeeps-saltcliff", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("fa9ecf80-8f0d-0d8e-2867-3540391631e1"), MapId = "region:greymoor", FromNodeId = "region:greymoor:zone:pale-moor", ToNodeId = "region:greymoor:zone:soddenfen", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = "palemoor-soddenfen", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("72acbbcb-a762-579c-0d7b-9a16d8a3ed3f"), MapId = "region:greymoor", FromNodeId = "region:greymoor:zone:pale-moor", ToNodeId = "region:greymoor:exit:cinderplain", Direction = MapDirection.East, EdgeType = MapEdgeType.ExitToRegion, Label = "palemoor-cinderplain", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("b69a3ec9-57f5-00fb-424b-607371d3978c"), MapId = "region:greymoor", FromNodeId = "region:greymoor:zone:pale-moor", ToNodeId = "region:greymoor:zone:aldenmere", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = "aldenmere-palemoor", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("86f6dd3c-1a4d-28e8-2742-5c0ef97de9bb"), MapId = "region:greymoor", FromNodeId = "region:greymoor:zone:soddenfen", ToNodeId = "region:greymoor:zone:pale-moor", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = "palemoor-soddenfen", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("21e7a8f4-f0cc-e860-683c-6d54abb244c9"), MapId = "region:greymoor", FromNodeId = "region:greymoor:zone:soddenfen", ToNodeId = "region:greymoor:exit:cinderplain", Direction = MapDirection.East, EdgeType = MapEdgeType.ExitToRegion, Label = "soddenfen-cinderplain", IsTraversable = true, IsHidden = false },
            ],
        },
        new Map
        {
            Id = "region:saltcliff",
            MapType = MapType.Region,
            OwnerId = "saltcliff",
            Name = "Saltcliff",
            Description = "Thysmara sea cliffs battered by storm winds, home to sailors, smugglers, and the drowned remnants of a sunken empire.",
            EntryNodeId = "region:saltcliff:entry",
            IsActive = true,
            Version = 1,
            UpdatedAt = now,
            Nodes =
            [
                new MapNode { Id = "region:saltcliff:entry", MapId = "region:saltcliff", NodeType = MapNodeType.EntryPoint, RefId = null, DisplayName = "Arrival", Description = "Arrival point for Saltcliff.", IconKey = "login", LayoutX = 24, LayoutY = 2, IsEntryPoint = true, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 0 },
                new MapNode { Id = "region:saltcliff:zone:saltcliff-heights", MapId = "region:saltcliff", NodeType = MapNodeType.Zone, RefId = "saltcliff-heights", DisplayName = "The Saltcliff Heights", Description = "Wind-blasted clifftops above the Thysmara sea, dotted with lighthouse ruins and contested by rival gull-rider clans.", IconKey = "map", LayoutX = 30, LayoutY = 10, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 20 },
                new MapNode { Id = "region:saltcliff:zone:tolvaren", MapId = "region:saltcliff", NodeType = MapNodeType.Zone, RefId = "tolvaren", DisplayName = "Tolvaren", Description = "A salt-caked Thysmara port city clinging to the cliff face, its harbour full of storm-worn ships and bold-faced traders.", IconKey = "map", LayoutX = 12, LayoutY = 22, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 30 },
                new MapNode { Id = "region:saltcliff:zone:tidewrack-flats", MapId = "region:saltcliff", NodeType = MapNodeType.Zone, RefId = "tidewrack-flats", DisplayName = "The Tidewrack Flats", Description = "Tide-scoured mudflats littered with shipwreck timber and kelp-draped bones, hunted by scavengers and wading predators.", IconKey = "map", LayoutX = 34, LayoutY = 28, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 40 },
                new MapNode { Id = "region:saltcliff:zone:sunken-name", MapId = "region:saltcliff", NodeType = MapNodeType.Zone, RefId = "sunken-name", DisplayName = "The Sunken Name", Description = "The drowned heart of a once-great Thysmara empire, now a flooded ruin accessible only to the brave and the waterproofed.", IconKey = "map", LayoutX = 16, LayoutY = 34, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 50 },
                new MapNode { Id = "region:saltcliff:exit:greymoor", MapId = "region:saltcliff", NodeType = MapNodeType.RegionExit, RefId = "greymoor", DisplayName = "Greymoor", Description = "Fog-wreathed Dravan highlands where ancient burial mounds dot the heather and wandering spirits trouble the living.", IconKey = "public", LayoutX = 24, LayoutY = 0, IsEntryPoint = false, IsExitPoint = true, TargetMapId = "region:greymoor", TargetNodeId = "region:greymoor:entry", IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 60 },
                new MapNode { Id = "region:saltcliff:exit:cinderplain", MapId = "region:saltcliff", NodeType = MapNodeType.RegionExit, RefId = "cinderplain", DisplayName = "Cinderplain", Description = "Kaldrek's scorched expanse of ash and cooled lava, where forge-tempered warbands and fire-touched creatures stake their claim.", IconKey = "public", LayoutX = 36, LayoutY = 0, IsEntryPoint = false, IsExitPoint = true, TargetMapId = "region:cinderplain", TargetNodeId = "region:cinderplain:entry", IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 70 },
            ],
            Edges =
            [
                new MapEdge { Id = new Guid("456c4916-2ef9-d3df-bfdd-cb12e7c90dcf"), MapId = "region:saltcliff", FromNodeId = "region:saltcliff:entry", ToNodeId = "region:saltcliff:exit:greymoor", Direction = MapDirection.North, EdgeType = MapEdgeType.ExitToRegion, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("47b2eafb-2616-4f78-d8b4-700ef6863116"), MapId = "region:saltcliff", FromNodeId = "region:saltcliff:exit:cinderplain", ToNodeId = "region:saltcliff:exit:greymoor", Direction = MapDirection.West, EdgeType = MapEdgeType.ExitToRegion, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("b108d3f4-7b85-c95b-b9b2-7b1d36379d1d"), MapId = "region:saltcliff", FromNodeId = "region:saltcliff:exit:greymoor", ToNodeId = "region:saltcliff:zone:saltcliff-heights", Direction = MapDirection.South, EdgeType = MapEdgeType.ExitToRegion, Label = "saltcliff-heights-greymoor", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("31a6a9bf-126b-b21a-c682-6b791af668ca"), MapId = "region:saltcliff", FromNodeId = "region:saltcliff:exit:greymoor", ToNodeId = "region:saltcliff:exit:cinderplain", Direction = MapDirection.East, EdgeType = MapEdgeType.ExitToRegion, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("3d5d3f29-d3bc-9f47-6bd7-7594e0d44691"), MapId = "region:saltcliff", FromNodeId = "region:saltcliff:zone:saltcliff-heights", ToNodeId = "region:saltcliff:exit:greymoor", Direction = MapDirection.North, EdgeType = MapEdgeType.ExitToRegion, Label = "saltcliff-heights-greymoor", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("869969ba-c512-98d5-1b9d-3c8e0412031d"), MapId = "region:saltcliff", FromNodeId = "region:saltcliff:zone:saltcliff-heights", ToNodeId = "region:saltcliff:zone:tolvaren", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = "saltcliff-heights-tolvaren", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("1a0eddad-dd01-059a-9305-e907d395a07d"), MapId = "region:saltcliff", FromNodeId = "region:saltcliff:zone:sunken-name", ToNodeId = "region:saltcliff:zone:tolvaren", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = "tolvaren-sunken-name", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("ff1fed40-1d2f-5ab3-a68c-01f2c45c5412"), MapId = "region:saltcliff", FromNodeId = "region:saltcliff:zone:sunken-name", ToNodeId = "region:saltcliff:zone:tidewrack-flats", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = "tidewrack-flats-sunken-name", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("e7eb772f-2198-3f05-cc51-d3e5a78f12ee"), MapId = "region:saltcliff", FromNodeId = "region:saltcliff:zone:tidewrack-flats", ToNodeId = "region:saltcliff:zone:tolvaren", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = "tolvaren-tidewrack-flats", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("5ded1629-9734-2e34-197e-b3d5055b5049"), MapId = "region:saltcliff", FromNodeId = "region:saltcliff:zone:tolvaren", ToNodeId = "region:saltcliff:zone:sunken-name", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = "tolvaren-sunken-name", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("fb08e048-07b0-c520-4fa4-0aa09c56cfe5"), MapId = "region:saltcliff", FromNodeId = "region:saltcliff:zone:tolvaren", ToNodeId = "region:saltcliff:zone:saltcliff-heights", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = "saltcliff-heights-tolvaren", IsTraversable = true, IsHidden = false },
            ],
        },
        new Map
        {
            Id = "region:varenmark",
            MapType = MapType.Region,
            OwnerId = "varenmark",
            Name = "Varenmark",
            Description = "The oldest settled march in the lowlands. Farmsteads, drove roads and market crossings gave way to wilder land. Whatever was here before the settlers is still here.",
            EntryNodeId = "region:varenmark:entry",
            IsActive = true,
            Version = 1,
            UpdatedAt = now,
            Nodes =
            [
                new MapNode { Id = "region:varenmark:entry", MapId = "region:varenmark", NodeType = MapNodeType.EntryPoint, RefId = null, DisplayName = "Arrival", Description = "Arrival point for Varenmark.", IconKey = "login", LayoutX = 13, LayoutY = 12, IsEntryPoint = true, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 0 },
                new MapNode { Id = "region:varenmark:zone:the-droveway", MapId = "region:varenmark", NodeType = MapNodeType.Zone, RefId = "the-droveway", DisplayName = "The Droveway", Description = "An old cattle track heading north from Crestfall. Dry-stone walls, pastures gone to seed, and a quiet that feels like it is waiting for something.", IconKey = "map", LayoutX = 14, LayoutY = 4, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 20 },
                new MapNode { Id = "region:varenmark:zone:the-halrow", MapId = "region:varenmark", NodeType = MapNodeType.Zone, RefId = "the-halrow", DisplayName = "The Halrow", Description = "A buried estate at the end of the Droveway, walls still standing but roof long collapsed. Something has lived in the cellars for a very long time.", IconKey = "map", LayoutX = 22, LayoutY = 7, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 30 },
                new MapNode { Id = "region:varenmark:zone:ashlen-wood", MapId = "region:varenmark", NodeType = MapNodeType.Zone, RefId = "ashlen-wood", DisplayName = "The Ashlen Wood", Description = "Old-growth forest south of Crestfall. The light is wrong here. Animals behave strangely and old clearings refuse to grow back.", IconKey = "map", LayoutX = 5, LayoutY = 11, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 40 },
                new MapNode { Id = "region:varenmark:zone:crestfall", MapId = "region:varenmark", NodeType = MapNodeType.Zone, RefId = "crestfall", DisplayName = "Crestfall", Description = "A prosperous crossroads market town where three old drove roads meet. Warm lanterns, merchant stalls, and the last comfortable beds for many miles.", IconKey = "map", LayoutX = 14, LayoutY = 11, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 50 },
                new MapNode { Id = "region:varenmark:zone:grevenmire", MapId = "region:varenmark", NodeType = MapNodeType.Zone, RefId = "grevenmire", DisplayName = "Grevenmire", Description = "Deep boggy country beyond the Ashlen Wood. Half-sunken ruins, fog that does not lift, and a hostility that feels personal.", IconKey = "map", LayoutX = 23, LayoutY = 11, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 60 },
                new MapNode { Id = "region:varenmark:zone:drowning-pits", MapId = "region:varenmark", NodeType = MapNodeType.Zone, RefId = "drowning-pits", DisplayName = "The Drowning Pits", Description = "Subterranean chambers beneath Grevenmire, half-flooded and ancient. Whatever built this was not building for humans.", IconKey = "map", LayoutX = 22, LayoutY = 16, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 70 },
                new MapNode { Id = "region:varenmark:exit:greymoor", MapId = "region:varenmark", NodeType = MapNodeType.RegionExit, RefId = "greymoor", DisplayName = "Greymoor", Description = "Fog-wreathed Dravan highlands where ancient burial mounds dot the heather and wandering spirits trouble the living.", IconKey = "public", LayoutX = 29, LayoutY = 11, IsEntryPoint = false, IsExitPoint = true, TargetMapId = "region:greymoor", TargetNodeId = "region:greymoor:entry", IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 80 },
            ],
            Edges =
            [
                new MapEdge { Id = new Guid("fd07a578-2c28-82a5-1fcc-0cacc796b0d4"), MapId = "region:varenmark", FromNodeId = "region:varenmark:entry", ToNodeId = "region:varenmark:zone:crestfall", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("638d9226-431d-860d-4751-b0bf4aaf9b61"), MapId = "region:varenmark", FromNodeId = "region:varenmark:exit:greymoor", ToNodeId = "region:varenmark:zone:grevenmire", Direction = MapDirection.West, EdgeType = MapEdgeType.ExitToRegion, Label = "greymoor-road", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("cf8bea1d-74f2-3a0d-4c90-31f4addd35f4"), MapId = "region:varenmark", FromNodeId = "region:varenmark:zone:ashlen-wood", ToNodeId = "region:varenmark:zone:crestfall", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = "ashlenwood-road", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("5621cb00-20b6-32fc-175f-861486c20663"), MapId = "region:varenmark", FromNodeId = "region:varenmark:zone:crestfall", ToNodeId = "region:varenmark:zone:the-droveway", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = "droveway-road", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("f4d0f922-fb54-9919-4f52-a6b908a79620"), MapId = "region:varenmark", FromNodeId = "region:varenmark:zone:crestfall", ToNodeId = "region:varenmark:entry", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("b601dfd0-df53-f2c6-8751-b8ca50a5da22"), MapId = "region:varenmark", FromNodeId = "region:varenmark:zone:crestfall", ToNodeId = "region:varenmark:zone:grevenmire", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = "grevenmire-road", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("07d7a2d7-b6f3-4924-99fb-cd58409881d4"), MapId = "region:varenmark", FromNodeId = "region:varenmark:zone:crestfall", ToNodeId = "region:varenmark:zone:ashlen-wood", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = "ashlenwood-road", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("4271843e-8f0b-77a1-3f19-d24b20708ef2"), MapId = "region:varenmark", FromNodeId = "region:varenmark:zone:drowning-pits", ToNodeId = "region:varenmark:zone:grevenmire", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = "drowningpits-road", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("2d490ad1-8d7d-20d5-732b-39126096cc2d"), MapId = "region:varenmark", FromNodeId = "region:varenmark:zone:grevenmire", ToNodeId = "region:varenmark:zone:the-halrow", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = "halrow-road", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("375cc27d-da64-3baf-9804-34977da8d736"), MapId = "region:varenmark", FromNodeId = "region:varenmark:zone:grevenmire", ToNodeId = "region:varenmark:zone:drowning-pits", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = "drowningpits-road", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("46832fe1-539b-bcdf-8d00-1ec3614b0cef"), MapId = "region:varenmark", FromNodeId = "region:varenmark:zone:grevenmire", ToNodeId = "region:varenmark:exit:greymoor", Direction = MapDirection.East, EdgeType = MapEdgeType.ExitToRegion, Label = "greymoor-road", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("42702f68-f1e0-0fee-20ec-868e14262297"), MapId = "region:varenmark", FromNodeId = "region:varenmark:zone:grevenmire", ToNodeId = "region:varenmark:zone:crestfall", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = "grevenmire-road", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("e691ade0-9217-8657-3caa-f61d7a438041"), MapId = "region:varenmark", FromNodeId = "region:varenmark:zone:the-droveway", ToNodeId = "region:varenmark:zone:crestfall", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = "droveway-road", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("70af47c3-e293-1275-affa-0d2ffbd545cb"), MapId = "region:varenmark", FromNodeId = "region:varenmark:zone:the-halrow", ToNodeId = "region:varenmark:zone:grevenmire", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = "halrow-road", IsTraversable = true, IsHidden = false },
            ],
        },
        new Map
        {
            Id = "zone:aldenmere",
            MapType = MapType.Zone,
            OwnerId = "aldenmere",
            Name = "Aldenmere",
            Description = "A grey-stoned Dravan waytown built atop old foundations, known for its mead, its mercenaries, and its many secrets.",
            EntryNodeId = "zone:aldenmere:entry",
            IsActive = true,
            Version = 1,
            UpdatedAt = now,
            Nodes =
            [
                new MapNode { Id = "zone:aldenmere:entry", MapId = "zone:aldenmere", NodeType = MapNodeType.EntryPoint, RefId = null, DisplayName = "Arrival", Description = "Arrival point for Aldenmere.", IconKey = "login", LayoutX = 24, LayoutY = 20, IsEntryPoint = true, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 0 },
                new MapNode { Id = "zone:aldenmere:location:aldenmere-marketplace", MapId = "zone:aldenmere", NodeType = MapNodeType.Location, RefId = "aldenmere-marketplace", DisplayName = "Aldenmere Marketplace", Description = "Aldenmere Marketplace in Aldenmere.", IconKey = "place", LayoutX = 35, LayoutY = 10, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 20 },
                new MapNode { Id = "zone:aldenmere:location:grey-cup", MapId = "zone:aldenmere", NodeType = MapNodeType.Location, RefId = "grey-cup", DisplayName = "The Grey Cup", Description = "The Grey Cup in Aldenmere.", IconKey = "place", LayoutX = 15, LayoutY = 30, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 30 },
                new MapNode { Id = "zone:aldenmere:location:ironhollow-keep", MapId = "zone:aldenmere", NodeType = MapNodeType.Location, RefId = "ironhollow-keep", DisplayName = "Ironhollow Keep", Description = "Ironhollow Keep in Aldenmere.", IconKey = "place", LayoutX = 10, LayoutY = 7, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 40 },
            ],
            Edges =
            [
                new MapEdge { Id = new Guid("c7bae21b-58bd-92ae-30d2-5fbb0a8a52fa"), MapId = "zone:aldenmere", FromNodeId = "zone:aldenmere:entry", ToNodeId = "zone:aldenmere:location:grey-cup", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("7e683292-d9db-444a-2ee8-39c152f23d96"), MapId = "zone:aldenmere", FromNodeId = "zone:aldenmere:entry", ToNodeId = "zone:aldenmere:location:aldenmere-marketplace", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("cf812307-d596-97b0-c362-453dc790aae3"), MapId = "zone:aldenmere", FromNodeId = "zone:aldenmere:entry", ToNodeId = "zone:aldenmere:location:ironhollow-keep", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("7764f681-8fd6-dc0c-ccea-8c2914f5408e"), MapId = "zone:aldenmere", FromNodeId = "zone:aldenmere:location:aldenmere-marketplace", ToNodeId = "zone:aldenmere:entry", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("fbd22c08-96b3-cc80-23a7-8ca4b655f330"), MapId = "zone:aldenmere", FromNodeId = "zone:aldenmere:location:grey-cup", ToNodeId = "zone:aldenmere:entry", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("115f90f4-57e0-9316-a59e-0c86dbf1723d"), MapId = "zone:aldenmere", FromNodeId = "zone:aldenmere:location:ironhollow-keep", ToNodeId = "zone:aldenmere:entry", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
            ],
        },
        new Map
        {
            Id = "zone:ashfields",
            MapType = MapType.Zone,
            OwnerId = "ashfields",
            Name = "The Ashfields",
            Description = "Grey ash plains stretching to the horizon, dotted with fused obsidian trees and the scorched skulls of fallen armies.",
            EntryNodeId = "zone:ashfields:entry",
            IsActive = true,
            Version = 1,
            UpdatedAt = now,
            Nodes =
            [
                new MapNode { Id = "zone:ashfields:entry", MapId = "zone:ashfields", NodeType = MapNodeType.EntryPoint, RefId = null, DisplayName = "Arrival", Description = "Arrival point for The Ashfields.", IconKey = "login", LayoutX = 32, LayoutY = 24, IsEntryPoint = true, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 0 },
                new MapNode { Id = "zone:ashfields:location:ash-shrine", MapId = "zone:ashfields", NodeType = MapNodeType.Location, RefId = "ash-shrine", DisplayName = "The Ash Shrine", Description = "The Ash Shrine in The Ashfields. Hidden until discovered. Unlock: quest.", IconKey = "place", LayoutX = 20, LayoutY = 40, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = true, UnlockType = "quest", UnlockKey = null, DiscoverThreshold = null, SortOrder = 20 },
                new MapNode { Id = "zone:ashfields:location:obsidian-grove", MapId = "zone:ashfields", NodeType = MapNodeType.Location, RefId = "obsidian-grove", DisplayName = "The Obsidian Grove", Description = "The Obsidian Grove in The Ashfields.", IconKey = "place", LayoutX = 30, LayoutY = 20, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 30 },
                new MapNode { Id = "zone:ashfields:location:scorched-battlefield", MapId = "zone:ashfields", NodeType = MapNodeType.Location, RefId = "scorched-battlefield", DisplayName = "The Scorched Battlefield", Description = "The Scorched Battlefield in The Ashfields.", IconKey = "place", LayoutX = 45, LayoutY = 35, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 40 },
            ],
            Edges =
            [
                new MapEdge { Id = new Guid("8d08398c-e42a-0f8d-98b7-4337e0a74cb9"), MapId = "zone:ashfields", FromNodeId = "zone:ashfields:entry", ToNodeId = "zone:ashfields:location:obsidian-grove", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("e1e0c12e-ed9e-1aeb-c899-9e75db088e16"), MapId = "zone:ashfields", FromNodeId = "zone:ashfields:entry", ToNodeId = "zone:ashfields:location:ash-shrine", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("d9788113-32c7-be89-3614-90b4456f21e5"), MapId = "zone:ashfields", FromNodeId = "zone:ashfields:entry", ToNodeId = "zone:ashfields:location:scorched-battlefield", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("5f9d1106-e73b-a2f5-5867-6195f5f9ece2"), MapId = "zone:ashfields", FromNodeId = "zone:ashfields:location:ash-shrine", ToNodeId = "zone:ashfields:entry", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("1df0c731-389a-7d00-6bc7-cbf74ccbb6e0"), MapId = "zone:ashfields", FromNodeId = "zone:ashfields:location:obsidian-grove", ToNodeId = "zone:ashfields:entry", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("d0f55968-46ab-720b-95ad-6c959610a491"), MapId = "zone:ashfields", FromNodeId = "zone:ashfields:location:scorched-battlefield", ToNodeId = "zone:ashfields:entry", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
            ],
        },
        new Map
        {
            Id = "zone:ashlen-wood",
            MapType = MapType.Zone,
            OwnerId = "ashlen-wood",
            Name = "The Ashlen Wood",
            Description = "Old-growth forest south of Crestfall. The light is wrong here. Animals behave strangely and old clearings refuse to grow back.",
            EntryNodeId = "zone:ashlen-wood:entry",
            IsActive = true,
            Version = 1,
            UpdatedAt = now,
            Nodes =
            [
                new MapNode { Id = "zone:ashlen-wood:entry", MapId = "zone:ashlen-wood", NodeType = MapNodeType.EntryPoint, RefId = null, DisplayName = "Arrival", Description = "Arrival point for The Ashlen Wood.", IconKey = "login", LayoutX = 39, LayoutY = 3, IsEntryPoint = true, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 0 },
                new MapNode { Id = "zone:ashlen-wood:location:old-clearing", MapId = "zone:ashlen-wood", NodeType = MapNodeType.Location, RefId = "old-clearing", DisplayName = "The Old Clearing", Description = "The Old Clearing in The Ashlen Wood.", IconKey = "place", LayoutX = 21, LayoutY = 43, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 20 },
                new MapNode { Id = "zone:ashlen-wood:location:strangled-oak", MapId = "zone:ashlen-wood", NodeType = MapNodeType.Location, RefId = "strangled-oak", DisplayName = "The Strangled Oak", Description = "The Strangled Oak in The Ashlen Wood. Hidden until discovered. Unlock: skill_check_passive.", IconKey = "place", LayoutX = 57, LayoutY = 24, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = true, UnlockType = "skill_check_passive", UnlockKey = null, DiscoverThreshold = 5, SortOrder = 30 },
                new MapNode { Id = "zone:ashlen-wood:location:wood-road", MapId = "zone:ashlen-wood", NodeType = MapNodeType.Location, RefId = "wood-road", DisplayName = "The Wood Road", Description = "The Wood Road in The Ashlen Wood.", IconKey = "place", LayoutX = 39, LayoutY = 29, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 40 },
                new MapNode { Id = "zone:ashlen-wood:exit:region:varenmark", MapId = "zone:ashlen-wood", NodeType = MapNodeType.RegionExit, RefId = "varenmark", DisplayName = "Return to Varenmark", Description = "The oldest settled march in the lowlands. Farmsteads, drove roads and market crossings gave way to wilder land. Whatever was here before the settlers is still here.", IconKey = "public", LayoutX = 39, LayoutY = 0, IsEntryPoint = false, IsExitPoint = true, TargetMapId = "region:varenmark", TargetNodeId = "region:varenmark:entry", IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 50 },
                new MapNode { Id = "zone:ashlen-wood:exit:zone:crestfall", MapId = "zone:ashlen-wood", NodeType = MapNodeType.ZoneExit, RefId = "crestfall", DisplayName = "Road to Crestfall", Description = "A prosperous crossroads market town where three old drove roads meet. Warm lanterns, merchant stalls, and the last comfortable beds for many miles.", IconKey = "exit_to_app", LayoutX = 39, LayoutY = 4, IsEntryPoint = false, IsExitPoint = true, TargetMapId = "zone:crestfall", TargetNodeId = "zone:crestfall:entry", IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 60 },
                new MapNode { Id = "zone:ashlen-wood:exit:zone:the-droveway", MapId = "zone:ashlen-wood", NodeType = MapNodeType.ZoneExit, RefId = "the-droveway", DisplayName = "Track to The Droveway", Description = "An old cattle track heading north from Crestfall. Dry-stone walls, pastures gone to seed, and a quiet that feels like it is waiting for something.", IconKey = "exit_to_app", LayoutX = 58, LayoutY = 24, IsEntryPoint = false, IsExitPoint = true, TargetMapId = "zone:the-droveway", TargetNodeId = "zone:the-droveway:entry", IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 70 },
                new MapNode { Id = "zone:ashlen-wood:exit:zone:grevenmire", MapId = "zone:ashlen-wood", NodeType = MapNodeType.ZoneExit, RefId = "grevenmire", DisplayName = "Path to Grevenmire", Description = "Deep boggy country beyond the Ashlen Wood. Half-sunken ruins, fog that does not lift, and a hostility that feels personal.", IconKey = "exit_to_app", LayoutX = 21, LayoutY = 44, IsEntryPoint = false, IsExitPoint = true, TargetMapId = "zone:grevenmire", TargetNodeId = "zone:grevenmire:entry", IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 80 },
            ],
            Edges =
            [
                new MapEdge { Id = new Guid("cabec665-9730-e1e0-1e8b-a5f7073bd250"), MapId = "zone:ashlen-wood", FromNodeId = "zone:ashlen-wood:entry", ToNodeId = "zone:ashlen-wood:exit:region:varenmark", Direction = MapDirection.North, EdgeType = MapEdgeType.ExitToRegion, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("37900ccb-802f-0921-f0d7-5f4af70b87e8"), MapId = "zone:ashlen-wood", FromNodeId = "zone:ashlen-wood:entry", ToNodeId = "zone:ashlen-wood:exit:zone:crestfall", Direction = MapDirection.South, EdgeType = MapEdgeType.ExitToZone, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("6bc74131-8c34-6b41-9c48-dc72833a36bc"), MapId = "zone:ashlen-wood", FromNodeId = "zone:ashlen-wood:exit:region:varenmark", ToNodeId = "zone:ashlen-wood:entry", Direction = MapDirection.South, EdgeType = MapEdgeType.ExitToRegion, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("f46ecf90-5832-4760-c2ce-c34b45a7be5e"), MapId = "zone:ashlen-wood", FromNodeId = "zone:ashlen-wood:exit:zone:crestfall", ToNodeId = "zone:ashlen-wood:entry", Direction = MapDirection.North, EdgeType = MapEdgeType.ExitToZone, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("24e86883-7922-a6dc-9c0b-14976985ca10"), MapId = "zone:ashlen-wood", FromNodeId = "zone:ashlen-wood:exit:zone:grevenmire", ToNodeId = "zone:ashlen-wood:location:old-clearing", Direction = MapDirection.North, EdgeType = MapEdgeType.ExitToZone, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("8ab06199-2379-574c-954b-35fd9c025f6f"), MapId = "zone:ashlen-wood", FromNodeId = "zone:ashlen-wood:exit:zone:the-droveway", ToNodeId = "zone:ashlen-wood:location:strangled-oak", Direction = MapDirection.West, EdgeType = MapEdgeType.ExitToZone, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("f49b003b-7f2c-7b3a-225d-fbc598e32d91"), MapId = "zone:ashlen-wood", FromNodeId = "zone:ashlen-wood:location:old-clearing", ToNodeId = "zone:ashlen-wood:exit:zone:grevenmire", Direction = MapDirection.South, EdgeType = MapEdgeType.ExitToZone, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("6173f0fd-78cb-6450-a796-a831114ed465"), MapId = "zone:ashlen-wood", FromNodeId = "zone:ashlen-wood:location:strangled-oak", ToNodeId = "zone:ashlen-wood:exit:zone:the-droveway", Direction = MapDirection.East, EdgeType = MapEdgeType.ExitToZone, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("cdd63a10-f143-6d2b-32db-f6424e4fc36a"), MapId = "zone:ashlen-wood", FromNodeId = "zone:ashlen-wood:location:strangled-oak", ToNodeId = "zone:ashlen-wood:location:wood-road", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("18a6fc92-0801-7b71-12c3-3d6a4b151aaa"), MapId = "zone:ashlen-wood", FromNodeId = "zone:ashlen-wood:location:wood-road", ToNodeId = "zone:ashlen-wood:location:strangled-oak", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
            ],
        },
        new Map
        {
            Id = "zone:barrow-deeps",
            MapType = MapType.Zone,
            OwnerId = "barrow-deeps",
            Name = "The Barrow Deeps",
            Description = "A sprawling underground burial network carved through the moorland, where ancestor-beasts guard ancient Dravan relics.",
            EntryNodeId = "zone:barrow-deeps:entry",
            IsActive = true,
            Version = 1,
            UpdatedAt = now,
            Nodes =
            [
                new MapNode { Id = "zone:barrow-deeps:entry", MapId = "zone:barrow-deeps", NodeType = MapNodeType.EntryPoint, RefId = null, DisplayName = "Arrival", Description = "Arrival point for The Barrow Deeps.", IconKey = "login", LayoutX = 20, LayoutY = 15, IsEntryPoint = true, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 0 },
                new MapNode { Id = "zone:barrow-deeps:location:ancestor-vault", MapId = "zone:barrow-deeps", NodeType = MapNodeType.Location, RefId = "ancestor-vault", DisplayName = "The Ancestor Vault", Description = "The Ancestor Vault in The Barrow Deeps.", IconKey = "place", LayoutX = 30, LayoutY = 15, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 20 },
                new MapNode { Id = "zone:barrow-deeps:location:deeps-entrance", MapId = "zone:barrow-deeps", NodeType = MapNodeType.Location, RefId = "deeps-entrance", DisplayName = "Deeps Entrance", Description = "Deeps Entrance in The Barrow Deeps.", IconKey = "place", LayoutX = 10, LayoutY = 7, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 30 },
                new MapNode { Id = "zone:barrow-deeps:location:relic-chamber", MapId = "zone:barrow-deeps", NodeType = MapNodeType.Location, RefId = "relic-chamber", DisplayName = "The Relic Chamber", Description = "The Relic Chamber in The Barrow Deeps. Hidden until discovered. Unlock: quest.", IconKey = "place", LayoutX = 20, LayoutY = 25, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = true, UnlockType = "quest", UnlockKey = null, DiscoverThreshold = null, SortOrder = 40 },
            ],
            Edges =
            [
                new MapEdge { Id = new Guid("687070d8-5d60-3ce2-0a6f-0690ca8b5162"), MapId = "zone:barrow-deeps", FromNodeId = "zone:barrow-deeps:entry", ToNodeId = "zone:barrow-deeps:location:relic-chamber", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("fe4b4c1b-fd7e-bd0d-9aec-39fbe98a5c59"), MapId = "zone:barrow-deeps", FromNodeId = "zone:barrow-deeps:entry", ToNodeId = "zone:barrow-deeps:location:ancestor-vault", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("210e643f-b055-0484-db59-d9168db86a73"), MapId = "zone:barrow-deeps", FromNodeId = "zone:barrow-deeps:entry", ToNodeId = "zone:barrow-deeps:location:deeps-entrance", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("53d5ac68-8ab8-1caf-e751-62cc745f4d8d"), MapId = "zone:barrow-deeps", FromNodeId = "zone:barrow-deeps:location:ancestor-vault", ToNodeId = "zone:barrow-deeps:entry", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("45c50328-205b-fb54-b5eb-3369c8735d69"), MapId = "zone:barrow-deeps", FromNodeId = "zone:barrow-deeps:location:deeps-entrance", ToNodeId = "zone:barrow-deeps:entry", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("e4be2d95-a09a-b149-afdb-64a1a2d423d6"), MapId = "zone:barrow-deeps", FromNodeId = "zone:barrow-deeps:location:relic-chamber", ToNodeId = "zone:barrow-deeps:entry", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
            ],
        },
        new Map
        {
            Id = "zone:crestfall",
            MapType = MapType.Zone,
            OwnerId = "crestfall",
            Name = "Crestfall",
            Description = "A prosperous crossroads market town where three old drove roads meet. Warm lanterns, merchant stalls, and the last comfortable beds for many miles.",
            EntryNodeId = "zone:crestfall:entry",
            IsActive = true,
            Version = 1,
            UpdatedAt = now,
            Nodes =
            [
                new MapNode { Id = "zone:crestfall:entry", MapId = "zone:crestfall", NodeType = MapNodeType.EntryPoint, RefId = null, DisplayName = "Arrival", Description = "Arrival point for Crestfall.", IconKey = "login", LayoutX = 30, LayoutY = 18, IsEntryPoint = true, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 0 },
                new MapNode { Id = "zone:crestfall:location:aurelian-market", MapId = "zone:crestfall", NodeType = MapNodeType.Location, RefId = "aurelian-market", DisplayName = "The Aurelian Market", Description = "The Aurelian Market in Crestfall.", IconKey = "place", LayoutX = 42, LayoutY = 24, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 20 },
                new MapNode { Id = "zone:crestfall:location:crestfall-square", MapId = "zone:crestfall", NodeType = MapNodeType.Location, RefId = "crestfall-square", DisplayName = "The Crestfall Square", Description = "The Crestfall Square in Crestfall.", IconKey = "place", LayoutX = 30, LayoutY = 22, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 30 },
                new MapNode { Id = "zone:crestfall:location:hearthbound-inn", MapId = "zone:crestfall", NodeType = MapNodeType.Location, RefId = "hearthbound-inn", DisplayName = "The Hearthbound Inn", Description = "The Hearthbound Inn in Crestfall.", IconKey = "place", LayoutX = 17, LayoutY = 28, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 40 },
                new MapNode { Id = "zone:crestfall:location:low-quarter", MapId = "zone:crestfall", NodeType = MapNodeType.Location, RefId = "low-quarter", DisplayName = "The Low Quarter", Description = "The Low Quarter in Crestfall.", IconKey = "place", LayoutX = 30, LayoutY = 40, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 50 },
                new MapNode { Id = "zone:crestfall:exit:region:varenmark", MapId = "zone:crestfall", NodeType = MapNodeType.RegionExit, RefId = "varenmark", DisplayName = "Road to Varenmark", Description = "The oldest settled march in the lowlands. Farmsteads, drove roads and market crossings gave way to wilder land. Whatever was here before the settlers is still here.", IconKey = "public", LayoutX = 30, LayoutY = 0, IsEntryPoint = false, IsExitPoint = true, TargetMapId = "region:varenmark", TargetNodeId = "region:varenmark:entry", IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 60 },
                new MapNode { Id = "zone:crestfall:exit:zone:ashlen-wood", MapId = "zone:crestfall", NodeType = MapNodeType.ZoneExit, RefId = "ashlen-wood", DisplayName = "Road to The Ashlen Wood", Description = "Old-growth forest south of Crestfall. The light is wrong here. Animals behave strangely and old clearings refuse to grow back.", IconKey = "exit_to_app", LayoutX = 0, LayoutY = 22, IsEntryPoint = false, IsExitPoint = true, TargetMapId = "zone:ashlen-wood", TargetNodeId = "zone:ashlen-wood:entry", IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 70 },
                new MapNode { Id = "zone:crestfall:exit:zone:the-droveway", MapId = "zone:crestfall", NodeType = MapNodeType.ZoneExit, RefId = "the-droveway", DisplayName = "Road to The Droveway", Description = "An old cattle track heading north from Crestfall. Dry-stone walls, pastures gone to seed, and a quiet that feels like it is waiting for something.", IconKey = "exit_to_app", LayoutX = 63, LayoutY = 22, IsEntryPoint = false, IsExitPoint = true, TargetMapId = "zone:the-droveway", TargetNodeId = "zone:the-droveway:entry", IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 80 },
            ],
            Edges =
            [
                new MapEdge { Id = new Guid("1c24662c-67e4-f647-40b8-b6deda6b9846"), MapId = "zone:crestfall", FromNodeId = "zone:crestfall:entry", ToNodeId = "zone:crestfall:location:crestfall-square", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("5595ae0b-634b-bb7d-086e-6583dd38df22"), MapId = "zone:crestfall", FromNodeId = "zone:crestfall:exit:region:varenmark", ToNodeId = "zone:crestfall:location:crestfall-square", Direction = MapDirection.South, EdgeType = MapEdgeType.ExitToRegion, Label = "north-road", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("c887df9b-03cc-7ed9-a353-701e03bfaa48"), MapId = "zone:crestfall", FromNodeId = "zone:crestfall:exit:zone:ashlen-wood", ToNodeId = "zone:crestfall:location:crestfall-square", Direction = MapDirection.East, EdgeType = MapEdgeType.ExitToZone, Label = "west-street", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("cf1ac654-4b57-ac40-979d-269ade2583a7"), MapId = "zone:crestfall", FromNodeId = "zone:crestfall:exit:zone:the-droveway", ToNodeId = "zone:crestfall:location:crestfall-square", Direction = MapDirection.West, EdgeType = MapEdgeType.ExitToZone, Label = "east-street", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("d541419f-96ed-d1b8-c1df-47d3d5fa2825"), MapId = "zone:crestfall", FromNodeId = "zone:crestfall:location:aurelian-market", ToNodeId = "zone:crestfall:location:crestfall-square", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("cd23bc26-3004-1a76-14c6-b76080cdf0be"), MapId = "zone:crestfall", FromNodeId = "zone:crestfall:location:crestfall-square", ToNodeId = "zone:crestfall:exit:region:varenmark", Direction = MapDirection.North, EdgeType = MapEdgeType.ExitToRegion, Label = "north-road", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("a4421b1c-3724-c04a-0d91-a4674acb8c46"), MapId = "zone:crestfall", FromNodeId = "zone:crestfall:location:crestfall-square", ToNodeId = "zone:crestfall:location:low-quarter", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("9a50d3d1-1493-c7cf-9a66-c3febc04be6c"), MapId = "zone:crestfall", FromNodeId = "zone:crestfall:location:crestfall-square", ToNodeId = "zone:crestfall:exit:zone:the-droveway", Direction = MapDirection.East, EdgeType = MapEdgeType.ExitToZone, Label = "east-street", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("22c08dac-a9cc-ff7d-9ed6-b228cc57424e"), MapId = "zone:crestfall", FromNodeId = "zone:crestfall:location:crestfall-square", ToNodeId = "zone:crestfall:exit:zone:ashlen-wood", Direction = MapDirection.West, EdgeType = MapEdgeType.ExitToZone, Label = "west-street", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("61515615-abdc-274e-04cf-f09201796e3e"), MapId = "zone:crestfall", FromNodeId = "zone:crestfall:location:hearthbound-inn", ToNodeId = "zone:crestfall:location:aurelian-market", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("5b7292ea-7c32-7fe2-d08d-eacf08d40fd2"), MapId = "zone:crestfall", FromNodeId = "zone:crestfall:location:low-quarter", ToNodeId = "zone:crestfall:location:crestfall-square", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
            ],
        },
        new Map
        {
            Id = "zone:drowning-pits",
            MapType = MapType.Zone,
            OwnerId = "drowning-pits",
            Name = "The Drowning Pits",
            Description = "Subterranean chambers beneath Grevenmire, half-flooded and ancient. Whatever built this was not building for humans.",
            EntryNodeId = "zone:drowning-pits:entry",
            IsActive = true,
            Version = 1,
            UpdatedAt = now,
            Nodes =
            [
                new MapNode { Id = "zone:drowning-pits:entry", MapId = "zone:drowning-pits", NodeType = MapNodeType.EntryPoint, RefId = null, DisplayName = "Arrival", Description = "Arrival point for The Drowning Pits.", IconKey = "login", LayoutX = 24, LayoutY = 2, IsEntryPoint = true, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 0 },
                new MapNode { Id = "zone:drowning-pits:location:deepest-chamber", MapId = "zone:drowning-pits", NodeType = MapNodeType.Location, RefId = "deepest-chamber", DisplayName = "The Deepest Chamber", Description = "The Deepest Chamber in The Drowning Pits. Hidden until discovered. Unlock: quest.", IconKey = "place", LayoutX = 33, LayoutY = 35, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = true, UnlockType = "quest", UnlockKey = null, DiscoverThreshold = null, SortOrder = 20 },
                new MapNode { Id = "zone:drowning-pits:location:flooded-workings", MapId = "zone:drowning-pits", NodeType = MapNodeType.Location, RefId = "flooded-workings", DisplayName = "The Flooded Workings", Description = "The Flooded Workings in The Drowning Pits.", IconKey = "place", LayoutX = 15, LayoutY = 27, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 30 },
                new MapNode { Id = "zone:drowning-pits:location:pit-descent", MapId = "zone:drowning-pits", NodeType = MapNodeType.Location, RefId = "pit-descent", DisplayName = "The Pit Descent", Description = "The Pit Descent in The Drowning Pits.", IconKey = "place", LayoutX = 24, LayoutY = 12, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 40 },
                new MapNode { Id = "zone:drowning-pits:exit:region:varenmark", MapId = "zone:drowning-pits", NodeType = MapNodeType.RegionExit, RefId = "varenmark", DisplayName = "Return to Varenmark", Description = "The oldest settled march in the lowlands. Farmsteads, drove roads and market crossings gave way to wilder land. Whatever was here before the settlers is still here.", IconKey = "public", LayoutX = 24, LayoutY = 1, IsEntryPoint = false, IsExitPoint = true, TargetMapId = "region:varenmark", TargetNodeId = "region:varenmark:entry", IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 50 },
            ],
            Edges =
            [
                new MapEdge { Id = new Guid("d4d0a58c-04ae-51db-1c42-2e6e7077e2b6"), MapId = "zone:drowning-pits", FromNodeId = "zone:drowning-pits:entry", ToNodeId = "zone:drowning-pits:exit:region:varenmark", Direction = MapDirection.North, EdgeType = MapEdgeType.ExitToRegion, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("da9bdcc5-e249-b2f9-9ea6-315eb98f0987"), MapId = "zone:drowning-pits", FromNodeId = "zone:drowning-pits:entry", ToNodeId = "zone:drowning-pits:location:pit-descent", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = "descent-passage", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("770a4401-0293-542c-245c-1d7d03c7958a"), MapId = "zone:drowning-pits", FromNodeId = "zone:drowning-pits:exit:region:varenmark", ToNodeId = "zone:drowning-pits:entry", Direction = MapDirection.South, EdgeType = MapEdgeType.ExitToRegion, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("53939a26-992e-ece7-1511-3678c45906d5"), MapId = "zone:drowning-pits", FromNodeId = "zone:drowning-pits:location:deepest-chamber", ToNodeId = "zone:drowning-pits:location:flooded-workings", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("275995b9-4ac2-5145-57cc-a314db882752"), MapId = "zone:drowning-pits", FromNodeId = "zone:drowning-pits:location:flooded-workings", ToNodeId = "zone:drowning-pits:location:deepest-chamber", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("931d7355-488c-6daa-9926-33005c1c4e17"), MapId = "zone:drowning-pits", FromNodeId = "zone:drowning-pits:location:pit-descent", ToNodeId = "zone:drowning-pits:entry", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = "descent-passage", IsTraversable = true, IsHidden = false },
            ],
        },
        new Map
        {
            Id = "zone:grevenmire",
            MapType = MapType.Zone,
            OwnerId = "grevenmire",
            Name = "Grevenmire",
            Description = "Deep boggy country beyond the Ashlen Wood. Half-sunken ruins, fog that does not lift, and a hostility that feels personal.",
            EntryNodeId = "zone:grevenmire:entry",
            IsActive = true,
            Version = 1,
            UpdatedAt = now,
            Nodes =
            [
                new MapNode { Id = "zone:grevenmire:entry", MapId = "zone:grevenmire", NodeType = MapNodeType.EntryPoint, RefId = null, DisplayName = "Arrival", Description = "Arrival point for Grevenmire.", IconKey = "login", LayoutX = 1, LayoutY = 27, IsEntryPoint = true, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 0 },
                new MapNode { Id = "zone:grevenmire:location:bone-hollow", MapId = "zone:grevenmire", NodeType = MapNodeType.Location, RefId = "bone-hollow", DisplayName = "The Bone Hollow", Description = "The Bone Hollow in Grevenmire. Hidden until discovered. Unlock: skill_check_passive.", IconKey = "place", LayoutX = 18, LayoutY = 42, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = true, UnlockType = "skill_check_passive", UnlockKey = null, DiscoverThreshold = 8, SortOrder = 20 },
                new MapNode { Id = "zone:grevenmire:location:mire-crossing", MapId = "zone:grevenmire", NodeType = MapNodeType.Location, RefId = "mire-crossing", DisplayName = "The Mire Crossing", Description = "The Mire Crossing in Grevenmire.", IconKey = "place", LayoutX = 39, LayoutY = 27, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 30 },
                new MapNode { Id = "zone:grevenmire:location:sunken-estate", MapId = "zone:grevenmire", NodeType = MapNodeType.Location, RefId = "sunken-estate", DisplayName = "The Sunken Estate", Description = "The Sunken Estate in Grevenmire.", IconKey = "place", LayoutX = 60, LayoutY = 16, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 40 },
                new MapNode { Id = "zone:grevenmire:exit:region:varenmark", MapId = "zone:grevenmire", NodeType = MapNodeType.RegionExit, RefId = "varenmark", DisplayName = "Return to Varenmark", Description = "The oldest settled march in the lowlands. Farmsteads, drove roads and market crossings gave way to wilder land. Whatever was here before the settlers is still here.", IconKey = "public", LayoutX = 0, LayoutY = 27, IsEntryPoint = false, IsExitPoint = true, TargetMapId = "region:varenmark", TargetNodeId = "region:varenmark:entry", IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 50 },
                new MapNode { Id = "zone:grevenmire:exit:zone:crestfall", MapId = "zone:grevenmire", NodeType = MapNodeType.ZoneExit, RefId = "crestfall", DisplayName = "Path to Crestfall", Description = "A prosperous crossroads market town where three old drove roads meet. Warm lanterns, merchant stalls, and the last comfortable beds for many miles.", IconKey = "exit_to_app", LayoutX = 2, LayoutY = 27, IsEntryPoint = false, IsExitPoint = true, TargetMapId = "zone:crestfall", TargetNodeId = "zone:crestfall:entry", IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 60 },
                new MapNode { Id = "zone:grevenmire:exit:zone:ashlen-wood", MapId = "zone:grevenmire", NodeType = MapNodeType.ZoneExit, RefId = "ashlen-wood", DisplayName = "Track to Ashlen Wood", Description = "Old-growth forest south of Crestfall. The light is wrong here. Animals behave strangely and old clearings refuse to grow back.", IconKey = "exit_to_app", LayoutX = 39, LayoutY = 55, IsEntryPoint = false, IsExitPoint = true, TargetMapId = "zone:ashlen-wood", TargetNodeId = "zone:ashlen-wood:entry", IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 70 },
            ],
            Edges =
            [
                new MapEdge { Id = new Guid("8daf59f1-04b2-60eb-8e5a-6a0a9edf6eb0"), MapId = "zone:grevenmire", FromNodeId = "zone:grevenmire:entry", ToNodeId = "zone:grevenmire:exit:zone:crestfall", Direction = MapDirection.East, EdgeType = MapEdgeType.ExitToZone, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("8874863b-7d27-9727-cc48-fc91f5f19076"), MapId = "zone:grevenmire", FromNodeId = "zone:grevenmire:entry", ToNodeId = "zone:grevenmire:exit:region:varenmark", Direction = MapDirection.West, EdgeType = MapEdgeType.ExitToRegion, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("0a79aef2-a6de-4ef5-00b6-591fa9ff4a47"), MapId = "zone:grevenmire", FromNodeId = "zone:grevenmire:exit:region:varenmark", ToNodeId = "zone:grevenmire:entry", Direction = MapDirection.East, EdgeType = MapEdgeType.ExitToRegion, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("ce866870-7b8e-d4c1-464c-88fe25ac728f"), MapId = "zone:grevenmire", FromNodeId = "zone:grevenmire:exit:zone:ashlen-wood", ToNodeId = "zone:grevenmire:location:mire-crossing", Direction = MapDirection.North, EdgeType = MapEdgeType.ExitToZone, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("9f005bef-2d9d-c976-83b9-d296c919bfaf"), MapId = "zone:grevenmire", FromNodeId = "zone:grevenmire:exit:zone:crestfall", ToNodeId = "zone:grevenmire:location:bone-hollow", Direction = MapDirection.East, EdgeType = MapEdgeType.ExitToZone, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("6f850921-42eb-9884-d980-aefb61d63e43"), MapId = "zone:grevenmire", FromNodeId = "zone:grevenmire:exit:zone:crestfall", ToNodeId = "zone:grevenmire:entry", Direction = MapDirection.West, EdgeType = MapEdgeType.ExitToZone, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("d6194a3a-0071-d220-66a7-91beb8599b13"), MapId = "zone:grevenmire", FromNodeId = "zone:grevenmire:location:bone-hollow", ToNodeId = "zone:grevenmire:exit:zone:crestfall", Direction = MapDirection.West, EdgeType = MapEdgeType.ExitToZone, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("8067939d-12bd-79a8-35d9-05891c73bf78"), MapId = "zone:grevenmire", FromNodeId = "zone:grevenmire:location:mire-crossing", ToNodeId = "zone:grevenmire:exit:zone:ashlen-wood", Direction = MapDirection.South, EdgeType = MapEdgeType.ExitToZone, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("e45d516d-964c-df2c-9eee-d789462ae77e"), MapId = "zone:grevenmire", FromNodeId = "zone:grevenmire:location:mire-crossing", ToNodeId = "zone:grevenmire:location:sunken-estate", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = "estate-track", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("55a3dcfe-be15-2ccb-799d-c1e53512bf7f"), MapId = "zone:grevenmire", FromNodeId = "zone:grevenmire:location:sunken-estate", ToNodeId = "zone:grevenmire:location:mire-crossing", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = "estate-track", IsTraversable = true, IsHidden = false },
            ],
        },
        new Map
        {
            Id = "zone:kaldrek-maw",
            MapType = MapType.Zone,
            OwnerId = "kaldrek-maw",
            Name = "Kaldrek's Maw",
            Description = "A vast volcanic vent complex known as the Maw, lair of Kaldrek's fire-bound ancient and final test of the worthy.",
            EntryNodeId = "zone:kaldrek-maw:entry",
            IsActive = true,
            Version = 1,
            UpdatedAt = now,
            Nodes =
            [
                new MapNode { Id = "zone:kaldrek-maw:entry", MapId = "zone:kaldrek-maw", NodeType = MapNodeType.EntryPoint, RefId = null, DisplayName = "Arrival", Description = "Arrival point for Kaldrek's Maw.", IconKey = "login", LayoutX = 20, LayoutY = 15, IsEntryPoint = true, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 0 },
                new MapNode { Id = "zone:kaldrek-maw:location:fire-ancients-chamber", MapId = "zone:kaldrek-maw", NodeType = MapNodeType.Location, RefId = "fire-ancients-chamber", DisplayName = "The Fire-Ancient's Chamber", Description = "The Fire-Ancient's Chamber in Kaldrek's Maw.", IconKey = "place", LayoutX = 30, LayoutY = 15, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 20 },
                new MapNode { Id = "zone:kaldrek-maw:location:kaldreks-heart", MapId = "zone:kaldrek-maw", NodeType = MapNodeType.Location, RefId = "kaldreks-heart", DisplayName = "Kaldrek's Heart", Description = "Kaldrek's Heart in Kaldrek's Maw. Hidden until discovered. Unlock: quest.", IconKey = "place", LayoutX = 20, LayoutY = 25, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = true, UnlockType = "quest", UnlockKey = null, DiscoverThreshold = null, SortOrder = 30 },
                new MapNode { Id = "zone:kaldrek-maw:location:maw-descent", MapId = "zone:kaldrek-maw", NodeType = MapNodeType.Location, RefId = "maw-descent", DisplayName = "The Maw Descent", Description = "The Maw Descent in Kaldrek's Maw.", IconKey = "place", LayoutX = 10, LayoutY = 7, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 40 },
            ],
            Edges =
            [
                new MapEdge { Id = new Guid("911841c7-0e62-a1ef-2ccd-b8545fd5b718"), MapId = "zone:kaldrek-maw", FromNodeId = "zone:kaldrek-maw:entry", ToNodeId = "zone:kaldrek-maw:location:kaldreks-heart", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("51cf6cf5-f736-e474-fac4-23ca766f1683"), MapId = "zone:kaldrek-maw", FromNodeId = "zone:kaldrek-maw:entry", ToNodeId = "zone:kaldrek-maw:location:fire-ancients-chamber", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("c8344b1e-b06b-025f-3c46-e213cf5957ff"), MapId = "zone:kaldrek-maw", FromNodeId = "zone:kaldrek-maw:entry", ToNodeId = "zone:kaldrek-maw:location:maw-descent", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("28c3e8ae-d519-8234-ece4-934af0b0d102"), MapId = "zone:kaldrek-maw", FromNodeId = "zone:kaldrek-maw:location:fire-ancients-chamber", ToNodeId = "zone:kaldrek-maw:entry", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("d126b106-2bcd-ddb9-305e-22ec45bed95d"), MapId = "zone:kaldrek-maw", FromNodeId = "zone:kaldrek-maw:location:kaldreks-heart", ToNodeId = "zone:kaldrek-maw:entry", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("3daa0a64-7ae8-fcb6-408e-06b995d89c02"), MapId = "zone:kaldrek-maw", FromNodeId = "zone:kaldrek-maw:location:maw-descent", ToNodeId = "zone:kaldrek-maw:entry", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
            ],
        },
        new Map
        {
            Id = "zone:pale-moor",
            MapType = MapType.Zone,
            OwnerId = "pale-moor",
            Name = "The Pale Moor",
            Description = "Endless fog-blanketed moors where waymarks shift overnight and travellers learn quickly to trust local guides.",
            EntryNodeId = "zone:pale-moor:entry",
            IsActive = true,
            Version = 1,
            UpdatedAt = now,
            Nodes =
            [
                new MapNode { Id = "zone:pale-moor:entry", MapId = "zone:pale-moor", NodeType = MapNodeType.EntryPoint, RefId = null, DisplayName = "Arrival", Description = "Arrival point for The Pale Moor.", IconKey = "login", LayoutX = 32, LayoutY = 24, IsEntryPoint = true, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 0 },
                new MapNode { Id = "zone:pale-moor:location:ashveil-highlands", MapId = "zone:pale-moor", NodeType = MapNodeType.Location, RefId = "ashveil-highlands", DisplayName = "Ashveil Highlands", Description = "Ashveil Highlands in The Pale Moor.", IconKey = "place", LayoutX = 30, LayoutY = 20, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 20 },
                new MapNode { Id = "zone:pale-moor:location:moorstone-cairns", MapId = "zone:pale-moor", NodeType = MapNodeType.Location, RefId = "moorstone-cairns", DisplayName = "The Moorstone Cairns", Description = "The Moorstone Cairns in The Pale Moor.", IconKey = "place", LayoutX = 45, LayoutY = 35, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 30 },
                new MapNode { Id = "zone:pale-moor:location:shifting-waymark", MapId = "zone:pale-moor", NodeType = MapNodeType.Location, RefId = "shifting-waymark", DisplayName = "The Shifting Waymark", Description = "The Shifting Waymark in The Pale Moor. Hidden until discovered. Unlock: skill_check_passive.", IconKey = "place", LayoutX = 20, LayoutY = 40, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = true, UnlockType = "skill_check_passive", UnlockKey = null, DiscoverThreshold = 10, SortOrder = 40 },
            ],
            Edges =
            [
                new MapEdge { Id = new Guid("05cc3120-c5ae-a147-8c43-4bbbc061abba"), MapId = "zone:pale-moor", FromNodeId = "zone:pale-moor:entry", ToNodeId = "zone:pale-moor:location:ashveil-highlands", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("f07cc514-3e88-2a96-edc9-d2356896fa87"), MapId = "zone:pale-moor", FromNodeId = "zone:pale-moor:entry", ToNodeId = "zone:pale-moor:location:shifting-waymark", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("720bf8cc-0b46-956f-2db1-9a00dce9fe78"), MapId = "zone:pale-moor", FromNodeId = "zone:pale-moor:entry", ToNodeId = "zone:pale-moor:location:moorstone-cairns", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("8650b1d2-4d46-7500-77c2-2ed21343cf05"), MapId = "zone:pale-moor", FromNodeId = "zone:pale-moor:location:ashveil-highlands", ToNodeId = "zone:pale-moor:entry", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("6de73413-44df-9a2d-949a-94c0c85b03d6"), MapId = "zone:pale-moor", FromNodeId = "zone:pale-moor:location:moorstone-cairns", ToNodeId = "zone:pale-moor:entry", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("80db21a4-cd86-3bc6-6af5-84358c464c47"), MapId = "zone:pale-moor", FromNodeId = "zone:pale-moor:location:shifting-waymark", ToNodeId = "zone:pale-moor:entry", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
            ],
        },
        new Map
        {
            Id = "zone:playground",
            MapType = MapType.Zone,
            OwnerId = "playground",
            Name = "Dev Playground",
            Description = "An unreleased sandbox zone seeded with one representative location for every game mechanic. Not accessible to players.",
            EntryNodeId = "zone:playground:entry",
            IsActive = true,
            Version = 1,
            UpdatedAt = now,
            Nodes =
            [
                new MapNode { Id = "zone:playground:entry", MapId = "zone:playground", NodeType = MapNodeType.EntryPoint, RefId = null, DisplayName = "Arrival", Description = "Arrival point for Dev Playground.", IconKey = "login", LayoutX = 1, LayoutY = -2, IsEntryPoint = true, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 0 },
                new MapNode { Id = "zone:playground:location:playground-clearing", MapId = "zone:playground", NodeType = MapNodeType.Location, RefId = "playground-clearing", DisplayName = "Dev Clearing", Description = "Dev Clearing in Dev Playground.", IconKey = "place", LayoutX = 0, LayoutY = 0, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 20 },
                new MapNode { Id = "zone:playground:location:playground-dungeon-gate", MapId = "zone:playground", NodeType = MapNodeType.Location, RefId = "playground-dungeon-gate", DisplayName = "Dev Dungeon Gate", Description = "Dev Dungeon Gate in Dev Playground.", IconKey = "place", LayoutX = -1, LayoutY = 0, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 30 },
                new MapNode { Id = "zone:playground:location:playground-forge", MapId = "zone:playground", NodeType = MapNodeType.Location, RefId = "playground-forge", DisplayName = "Dev Forge", Description = "Dev Forge in Dev Playground.", IconKey = "place", LayoutX = 0, LayoutY = 1, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 40 },
                new MapNode { Id = "zone:playground:location:playground-grove", MapId = "zone:playground", NodeType = MapNodeType.Location, RefId = "playground-grove", DisplayName = "Dev Grove", Description = "Dev Grove in Dev Playground.", IconKey = "place", LayoutX = 0, LayoutY = -1, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 50 },
                new MapNode { Id = "zone:playground:location:playground-hidden-achievement", MapId = "zone:playground", NodeType = MapNodeType.Location, RefId = "playground-hidden-achievement", DisplayName = "Hidden: Achievement", Description = "Hidden: Achievement in Dev Playground. Hidden until discovered. Unlock: achievement.", IconKey = "place", LayoutX = 1, LayoutY = 0, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = true, UnlockType = "achievement", UnlockKey = "dev-achieve", DiscoverThreshold = null, SortOrder = 60 },
                new MapNode { Id = "zone:playground:location:playground-hidden-active", MapId = "zone:playground", NodeType = MapNodeType.Location, RefId = "playground-hidden-active", DisplayName = "Hidden: Active Skill Check", Description = "Hidden: Active Skill Check in Dev Playground. Hidden until discovered. Unlock: skill_check_active.", IconKey = "place", LayoutX = -2, LayoutY = 0, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = true, UnlockType = "skill_check_active", UnlockKey = "perception", DiscoverThreshold = 8, SortOrder = 70 },
                new MapNode { Id = "zone:playground:location:playground-hidden-item", MapId = "zone:playground", NodeType = MapNodeType.Location, RefId = "playground-hidden-item", DisplayName = "Hidden: Item Check", Description = "Hidden: Item Check in Dev Playground. Hidden until discovered. Unlock: item.", IconKey = "place", LayoutX = -1, LayoutY = 1, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = true, UnlockType = "item", UnlockKey = "skeleton-key", DiscoverThreshold = null, SortOrder = 80 },
                new MapNode { Id = "zone:playground:location:playground-hidden-quest", MapId = "zone:playground", NodeType = MapNodeType.Location, RefId = "playground-hidden-quest", DisplayName = "Hidden: Quest Gate", Description = "Hidden: Quest Gate in Dev Playground. Hidden until discovered. Unlock: quest.", IconKey = "place", LayoutX = -1, LayoutY = -1, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = true, UnlockType = "quest", UnlockKey = "dev-quest", DiscoverThreshold = null, SortOrder = 90 },
                new MapNode { Id = "zone:playground:location:playground-hidden-skill", MapId = "zone:playground", NodeType = MapNodeType.Location, RefId = "playground-hidden-skill", DisplayName = "Hidden: Passive Skill Check", Description = "Hidden: Passive Skill Check in Dev Playground. Hidden until discovered. Unlock: skill_check_passive.", IconKey = "place", LayoutX = 0, LayoutY = 2, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = true, UnlockType = "skill_check_passive", UnlockKey = null, DiscoverThreshold = 5, SortOrder = 100 },
                new MapNode { Id = "zone:playground:location:playground-npc-hall", MapId = "zone:playground", NodeType = MapNodeType.Location, RefId = "playground-npc-hall", DisplayName = "Dev NPC Hall", Description = "Dev NPC Hall in Dev Playground.", IconKey = "place", LayoutX = 0, LayoutY = -2, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 110 },
                new MapNode { Id = "zone:playground:location:playground-shop", MapId = "zone:playground", NodeType = MapNodeType.Location, RefId = "playground-shop", DisplayName = "Dev Shop", Description = "Dev Shop in Dev Playground.", IconKey = "place", LayoutX = 1, LayoutY = 1, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 120 },
                new MapNode { Id = "zone:playground:location:playground-square", MapId = "zone:playground", NodeType = MapNodeType.Location, RefId = "playground-square", DisplayName = "Dev Square", Description = "Dev Square in Dev Playground.", IconKey = "place", LayoutX = 1, LayoutY = -1, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 130 },
                new MapNode { Id = "zone:playground:location:playground-tavern", MapId = "zone:playground", NodeType = MapNodeType.Location, RefId = "playground-tavern", DisplayName = "Dev Tavern", Description = "Dev Tavern in Dev Playground.", IconKey = "place", LayoutX = 2, LayoutY = 0, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 140 },
            ],
            Edges =
            [
                new MapEdge { Id = new Guid("64648507-2d9b-2ee2-4a32-ef3381d65d5a"), MapId = "zone:playground", FromNodeId = "zone:playground:entry", ToNodeId = "zone:playground:location:playground-square", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("2740df1c-66de-248a-b47f-0fde1d0fa6b8"), MapId = "zone:playground", FromNodeId = "zone:playground:entry", ToNodeId = "zone:playground:location:playground-npc-hall", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("570be3e8-3d3c-0bb7-fd31-4c42392ef28a"), MapId = "zone:playground", FromNodeId = "zone:playground:location:playground-clearing", ToNodeId = "zone:playground:location:playground-grove", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("751a782e-7817-e7b0-38ca-dbdd61bb9f89"), MapId = "zone:playground", FromNodeId = "zone:playground:location:playground-clearing", ToNodeId = "zone:playground:location:playground-forge", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("f878797e-d611-59cd-4a8b-740cfa230d28"), MapId = "zone:playground", FromNodeId = "zone:playground:location:playground-clearing", ToNodeId = "zone:playground:location:playground-hidden-achievement", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("f9233f10-bd60-bcf0-914c-505626bba770"), MapId = "zone:playground", FromNodeId = "zone:playground:location:playground-clearing", ToNodeId = "zone:playground:location:playground-dungeon-gate", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("a00d96a5-1b59-9e23-351e-2338a3e53cd8"), MapId = "zone:playground", FromNodeId = "zone:playground:location:playground-dungeon-gate", ToNodeId = "zone:playground:location:playground-hidden-quest", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("ce095fc3-7ea9-bc84-b37c-35f62783664a"), MapId = "zone:playground", FromNodeId = "zone:playground:location:playground-dungeon-gate", ToNodeId = "zone:playground:location:playground-hidden-item", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("82597657-bba4-6ec6-aff6-83ffa564e33e"), MapId = "zone:playground", FromNodeId = "zone:playground:location:playground-dungeon-gate", ToNodeId = "zone:playground:location:playground-clearing", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("e050de70-d86d-8197-4310-bb803cb98fe6"), MapId = "zone:playground", FromNodeId = "zone:playground:location:playground-dungeon-gate", ToNodeId = "zone:playground:location:playground-hidden-active", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("b70c6dd0-f0a4-bd21-cf2a-d1a84b7129a8"), MapId = "zone:playground", FromNodeId = "zone:playground:location:playground-forge", ToNodeId = "zone:playground:location:playground-clearing", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("6e5a790d-40b5-6d21-5b82-824314497e6f"), MapId = "zone:playground", FromNodeId = "zone:playground:location:playground-forge", ToNodeId = "zone:playground:location:playground-hidden-skill", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("4fdc626e-c3d6-9d16-0120-ecfed57d5c52"), MapId = "zone:playground", FromNodeId = "zone:playground:location:playground-forge", ToNodeId = "zone:playground:location:playground-shop", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("5df35a3b-78fc-9e3c-9f3f-fa70b98e0138"), MapId = "zone:playground", FromNodeId = "zone:playground:location:playground-grove", ToNodeId = "zone:playground:location:playground-clearing", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("23fb75db-3aeb-c7a9-fdfa-db2aa5c0188b"), MapId = "zone:playground", FromNodeId = "zone:playground:location:playground-hidden-achievement", ToNodeId = "zone:playground:location:playground-tavern", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("5ab1debf-11dd-6e42-27d5-c468cd49843f"), MapId = "zone:playground", FromNodeId = "zone:playground:location:playground-hidden-achievement", ToNodeId = "zone:playground:location:playground-clearing", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("1718f33c-195c-b8d2-a9bc-e98a2449a06e"), MapId = "zone:playground", FromNodeId = "zone:playground:location:playground-hidden-active", ToNodeId = "zone:playground:location:playground-dungeon-gate", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("232fcdc4-0c02-4f2b-c48a-d453bf44ac46"), MapId = "zone:playground", FromNodeId = "zone:playground:location:playground-hidden-item", ToNodeId = "zone:playground:location:playground-dungeon-gate", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("a27c998f-9083-5a4d-a57e-b37900f18dc8"), MapId = "zone:playground", FromNodeId = "zone:playground:location:playground-hidden-quest", ToNodeId = "zone:playground:location:playground-dungeon-gate", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("a433b96c-f9fd-37eb-4b26-c34f2ccbe306"), MapId = "zone:playground", FromNodeId = "zone:playground:location:playground-hidden-skill", ToNodeId = "zone:playground:location:playground-forge", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("db7c18eb-292b-e774-d7d0-5f6f03b7ecea"), MapId = "zone:playground", FromNodeId = "zone:playground:location:playground-npc-hall", ToNodeId = "zone:playground:entry", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("0d2a9bb2-1516-d60b-0e98-efef1359ae25"), MapId = "zone:playground", FromNodeId = "zone:playground:location:playground-shop", ToNodeId = "zone:playground:location:playground-forge", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("d003da3d-49da-0dcb-1ba0-9b7d5c97cd28"), MapId = "zone:playground", FromNodeId = "zone:playground:location:playground-square", ToNodeId = "zone:playground:entry", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("a4fa3a4a-7e70-33d2-0ef3-421f06dfdfbd"), MapId = "zone:playground", FromNodeId = "zone:playground:location:playground-tavern", ToNodeId = "zone:playground:location:playground-hidden-achievement", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
            ],
        },
        new Map
        {
            Id = "zone:saltcliff-heights",
            MapType = MapType.Zone,
            OwnerId = "saltcliff-heights",
            Name = "The Saltcliff Heights",
            Description = "Wind-blasted clifftops above the Thysmara sea, dotted with lighthouse ruins and contested by rival gull-rider clans.",
            EntryNodeId = "zone:saltcliff-heights:entry",
            IsActive = true,
            Version = 1,
            UpdatedAt = now,
            Nodes =
            [
                new MapNode { Id = "zone:saltcliff-heights:entry", MapId = "zone:saltcliff-heights", NodeType = MapNodeType.EntryPoint, RefId = null, DisplayName = "Arrival", Description = "Arrival point for The Saltcliff Heights.", IconKey = "login", LayoutX = 24, LayoutY = 20, IsEntryPoint = true, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 0 },
                new MapNode { Id = "zone:saltcliff-heights:location:clifftop-ruins", MapId = "zone:saltcliff-heights", NodeType = MapNodeType.Location, RefId = "clifftop-ruins", DisplayName = "The Clifftop Ruins", Description = "The Clifftop Ruins in The Saltcliff Heights.", IconKey = "place", LayoutX = 24, LayoutY = 10, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 20 },
                new MapNode { Id = "zone:saltcliff-heights:location:gull-rider-camp", MapId = "zone:saltcliff-heights", NodeType = MapNodeType.Location, RefId = "gull-rider-camp", DisplayName = "Gull-Rider Camp", Description = "Gull-Rider Camp in The Saltcliff Heights.", IconKey = "place", LayoutX = 10, LayoutY = 30, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 30 },
                new MapNode { Id = "zone:saltcliff-heights:location:storm-watch-peak", MapId = "zone:saltcliff-heights", NodeType = MapNodeType.Location, RefId = "storm-watch-peak", DisplayName = "Storm-Watch Peak", Description = "Storm-Watch Peak in The Saltcliff Heights. Hidden until discovered. Unlock: achievement.", IconKey = "place", LayoutX = 35, LayoutY = 20, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = true, UnlockType = "achievement", UnlockKey = null, DiscoverThreshold = null, SortOrder = 40 },
            ],
            Edges =
            [
                new MapEdge { Id = new Guid("6ebbbcc8-a2e8-cec5-1652-38724b9dbd88"), MapId = "zone:saltcliff-heights", FromNodeId = "zone:saltcliff-heights:entry", ToNodeId = "zone:saltcliff-heights:location:clifftop-ruins", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("a7ca7ea9-50fb-5501-d04b-32ce4424f648"), MapId = "zone:saltcliff-heights", FromNodeId = "zone:saltcliff-heights:entry", ToNodeId = "zone:saltcliff-heights:location:storm-watch-peak", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("990ff54b-a0de-2180-f51c-4192c8fe5c54"), MapId = "zone:saltcliff-heights", FromNodeId = "zone:saltcliff-heights:entry", ToNodeId = "zone:saltcliff-heights:location:gull-rider-camp", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("b7f5bf40-ed73-9250-de01-92a75f76386f"), MapId = "zone:saltcliff-heights", FromNodeId = "zone:saltcliff-heights:location:clifftop-ruins", ToNodeId = "zone:saltcliff-heights:entry", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("7c07cbff-2058-51d7-6834-8ff6564582f0"), MapId = "zone:saltcliff-heights", FromNodeId = "zone:saltcliff-heights:location:gull-rider-camp", ToNodeId = "zone:saltcliff-heights:entry", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("68d3a13f-a269-1689-a0aa-f231699403fd"), MapId = "zone:saltcliff-heights", FromNodeId = "zone:saltcliff-heights:location:storm-watch-peak", ToNodeId = "zone:saltcliff-heights:entry", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
            ],
        },
        new Map
        {
            Id = "zone:skarhold",
            MapType = MapType.Zone,
            OwnerId = "skarhold",
            Name = "Skarhold",
            Description = "A Kaldrek forge-city built inside a dormant caldera, where the best smiths in Veldrath ply their trade under sulphur skies.",
            EntryNodeId = "zone:skarhold:entry",
            IsActive = true,
            Version = 1,
            UpdatedAt = now,
            Nodes =
            [
                new MapNode { Id = "zone:skarhold:entry", MapId = "zone:skarhold", NodeType = MapNodeType.EntryPoint, RefId = null, DisplayName = "Arrival", Description = "Arrival point for Skarhold.", IconKey = "login", LayoutX = 24, LayoutY = 20, IsEntryPoint = true, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 0 },
                new MapNode { Id = "zone:skarhold:location:ashbrand-lodge", MapId = "zone:skarhold", NodeType = MapNodeType.Location, RefId = "ashbrand-lodge", DisplayName = "The Ashbrand Lodge", Description = "The Ashbrand Lodge in Skarhold.", IconKey = "place", LayoutX = 15, LayoutY = 30, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 20 },
                new MapNode { Id = "zone:skarhold:location:caldera-market", MapId = "zone:skarhold", NodeType = MapNodeType.Location, RefId = "caldera-market", DisplayName = "Caldera Market", Description = "Caldera Market in Skarhold.", IconKey = "place", LayoutX = 35, LayoutY = 10, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 30 },
                new MapNode { Id = "zone:skarhold:location:forge-quarter", MapId = "zone:skarhold", NodeType = MapNodeType.Location, RefId = "forge-quarter", DisplayName = "The Forge Quarter", Description = "The Forge Quarter in Skarhold.", IconKey = "place", LayoutX = 10, LayoutY = 7, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 40 },
            ],
            Edges =
            [
                new MapEdge { Id = new Guid("84040962-f355-3c7b-f7e7-c5a9e92b5687"), MapId = "zone:skarhold", FromNodeId = "zone:skarhold:entry", ToNodeId = "zone:skarhold:location:ashbrand-lodge", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("c1e7fda3-afbc-d07e-41ca-f532a2d7b0b4"), MapId = "zone:skarhold", FromNodeId = "zone:skarhold:entry", ToNodeId = "zone:skarhold:location:caldera-market", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("cac2dc7d-69cd-6a7d-073e-6cff60a15d43"), MapId = "zone:skarhold", FromNodeId = "zone:skarhold:entry", ToNodeId = "zone:skarhold:location:forge-quarter", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("ae18c691-4e60-de50-34c0-20da1243b085"), MapId = "zone:skarhold", FromNodeId = "zone:skarhold:location:ashbrand-lodge", ToNodeId = "zone:skarhold:entry", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("14e82395-fcfa-1226-3813-91fbc1fceefd"), MapId = "zone:skarhold", FromNodeId = "zone:skarhold:location:caldera-market", ToNodeId = "zone:skarhold:entry", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("750a3a42-1fc7-85fa-9926-b7cd37ab19a6"), MapId = "zone:skarhold", FromNodeId = "zone:skarhold:location:forge-quarter", ToNodeId = "zone:skarhold:entry", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
            ],
        },
        new Map
        {
            Id = "zone:smoldering-reach",
            MapType = MapType.Zone,
            OwnerId = "smoldering-reach",
            Name = "The Smoldering Reach",
            Description = "A cracked lava field where vents of superheated gas erupt without warning and heat-adapted predators hunt at dusk.",
            EntryNodeId = "zone:smoldering-reach:entry",
            IsActive = true,
            Version = 1,
            UpdatedAt = now,
            Nodes =
            [
                new MapNode { Id = "zone:smoldering-reach:entry", MapId = "zone:smoldering-reach", NodeType = MapNodeType.EntryPoint, RefId = null, DisplayName = "Arrival", Description = "Arrival point for The Smoldering Reach.", IconKey = "login", LayoutX = 24, LayoutY = 20, IsEntryPoint = true, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 0 },
                new MapNode { Id = "zone:smoldering-reach:location:lava-bridge", MapId = "zone:smoldering-reach", NodeType = MapNodeType.Location, RefId = "lava-bridge", DisplayName = "The Lava Bridge", Description = "The Lava Bridge in The Smoldering Reach. Hidden until discovered. Unlock: skill_check_active.", IconKey = "place", LayoutX = 35, LayoutY = 20, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = true, UnlockType = "skill_check_active", UnlockKey = null, DiscoverThreshold = 18, SortOrder = 20 },
                new MapNode { Id = "zone:smoldering-reach:location:pyreling-den", MapId = "zone:smoldering-reach", NodeType = MapNodeType.Location, RefId = "pyreling-den", DisplayName = "The Pyreling Den", Description = "The Pyreling Den in The Smoldering Reach.", IconKey = "place", LayoutX = 10, LayoutY = 30, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 30 },
                new MapNode { Id = "zone:smoldering-reach:location:vent-fields", MapId = "zone:smoldering-reach", NodeType = MapNodeType.Location, RefId = "vent-fields", DisplayName = "The Vent Fields", Description = "The Vent Fields in The Smoldering Reach.", IconKey = "place", LayoutX = 24, LayoutY = 10, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 40 },
            ],
            Edges =
            [
                new MapEdge { Id = new Guid("4b63555a-76f6-e805-b14f-3dc92174fc5d"), MapId = "zone:smoldering-reach", FromNodeId = "zone:smoldering-reach:entry", ToNodeId = "zone:smoldering-reach:location:vent-fields", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("5a3c3be9-0467-38cc-d624-7c822a863763"), MapId = "zone:smoldering-reach", FromNodeId = "zone:smoldering-reach:entry", ToNodeId = "zone:smoldering-reach:location:lava-bridge", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("d310113d-7f93-fc09-dcfd-dad8c3b21294"), MapId = "zone:smoldering-reach", FromNodeId = "zone:smoldering-reach:entry", ToNodeId = "zone:smoldering-reach:location:pyreling-den", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("8f3d5a6a-61e2-133f-ab3f-2d6ba2623e4f"), MapId = "zone:smoldering-reach", FromNodeId = "zone:smoldering-reach:location:lava-bridge", ToNodeId = "zone:smoldering-reach:entry", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("28e0fb35-61df-8af4-13cd-0f5870677888"), MapId = "zone:smoldering-reach", FromNodeId = "zone:smoldering-reach:location:pyreling-den", ToNodeId = "zone:smoldering-reach:entry", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("90880228-e585-e24a-5aea-9463356f246e"), MapId = "zone:smoldering-reach", FromNodeId = "zone:smoldering-reach:location:vent-fields", ToNodeId = "zone:smoldering-reach:entry", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
            ],
        },
        new Map
        {
            Id = "zone:soddenfen",
            MapType = MapType.Zone,
            OwnerId = "soddenfen",
            Name = "Soddenfen",
            Description = "A waterlogged fenland buzzing with plague insects and half-submerged ruins, avoided by all with good sense.",
            EntryNodeId = "zone:soddenfen:entry",
            IsActive = true,
            Version = 1,
            UpdatedAt = now,
            Nodes =
            [
                new MapNode { Id = "zone:soddenfen:entry", MapId = "zone:soddenfen", NodeType = MapNodeType.EntryPoint, RefId = null, DisplayName = "Arrival", Description = "Arrival point for Soddenfen.", IconKey = "login", LayoutX = 24, LayoutY = 20, IsEntryPoint = true, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 0 },
                new MapNode { Id = "zone:soddenfen:location:fenland-crossing", MapId = "zone:soddenfen", NodeType = MapNodeType.Location, RefId = "fenland-crossing", DisplayName = "Fenland Crossing", Description = "Fenland Crossing in Soddenfen.", IconKey = "place", LayoutX = 24, LayoutY = 10, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 20 },
                new MapNode { Id = "zone:soddenfen:location:submerged-ruins", MapId = "zone:soddenfen", NodeType = MapNodeType.Location, RefId = "submerged-ruins", DisplayName = "The Submerged Ruins", Description = "The Submerged Ruins in Soddenfen. Hidden until discovered. Unlock: skill_check_active.", IconKey = "place", LayoutX = 10, LayoutY = 30, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = true, UnlockType = "skill_check_active", UnlockKey = null, DiscoverThreshold = 12, SortOrder = 30 },
            ],
            Edges =
            [
                new MapEdge { Id = new Guid("a0b36a40-812a-864f-d653-ab2268fe5d47"), MapId = "zone:soddenfen", FromNodeId = "zone:soddenfen:entry", ToNodeId = "zone:soddenfen:location:fenland-crossing", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("13fcb650-7846-50b4-035a-64a046052410"), MapId = "zone:soddenfen", FromNodeId = "zone:soddenfen:entry", ToNodeId = "zone:soddenfen:location:submerged-ruins", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("182e52be-ef35-a478-064c-2a72ebc58f20"), MapId = "zone:soddenfen", FromNodeId = "zone:soddenfen:location:fenland-crossing", ToNodeId = "zone:soddenfen:entry", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("349d4576-03f4-3b4c-8fac-10fe3f2f6933"), MapId = "zone:soddenfen", FromNodeId = "zone:soddenfen:location:submerged-ruins", ToNodeId = "zone:soddenfen:entry", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
            ],
        },
        new Map
        {
            Id = "zone:sunken-name",
            MapType = MapType.Zone,
            OwnerId = "sunken-name",
            Name = "The Sunken Name",
            Description = "The drowned heart of a once-great Thysmara empire, now a flooded ruin accessible only to the brave and the waterproofed.",
            EntryNodeId = "zone:sunken-name:entry",
            IsActive = true,
            Version = 1,
            UpdatedAt = now,
            Nodes =
            [
                new MapNode { Id = "zone:sunken-name:entry", MapId = "zone:sunken-name", NodeType = MapNodeType.EntryPoint, RefId = null, DisplayName = "Arrival", Description = "Arrival point for The Sunken Name.", IconKey = "login", LayoutX = 20, LayoutY = 15, IsEntryPoint = true, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 0 },
                new MapNode { Id = "zone:sunken-name:location:drowned-threshold", MapId = "zone:sunken-name", NodeType = MapNodeType.Location, RefId = "drowned-threshold", DisplayName = "The Drowned Threshold", Description = "The Drowned Threshold in The Sunken Name.", IconKey = "place", LayoutX = 10, LayoutY = 7, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 20 },
                new MapNode { Id = "zone:sunken-name:location:flooded-throne-room", MapId = "zone:sunken-name", NodeType = MapNodeType.Location, RefId = "flooded-throne-room", DisplayName = "The Flooded Throne Room", Description = "The Flooded Throne Room in The Sunken Name.", IconKey = "place", LayoutX = 30, LayoutY = 15, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 30 },
                new MapNode { Id = "zone:sunken-name:location:tidelocked-vault", MapId = "zone:sunken-name", NodeType = MapNodeType.Location, RefId = "tidelocked-vault", DisplayName = "The Tidelocked Vault", Description = "The Tidelocked Vault in The Sunken Name. Hidden until discovered. Unlock: quest.", IconKey = "place", LayoutX = 20, LayoutY = 25, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = true, UnlockType = "quest", UnlockKey = null, DiscoverThreshold = null, SortOrder = 40 },
            ],
            Edges =
            [
                new MapEdge { Id = new Guid("bd78d749-dc25-3585-5232-6ee78e619ba7"), MapId = "zone:sunken-name", FromNodeId = "zone:sunken-name:entry", ToNodeId = "zone:sunken-name:location:tidelocked-vault", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("7d0a9780-a639-8993-2e6d-43baf4d46f9f"), MapId = "zone:sunken-name", FromNodeId = "zone:sunken-name:entry", ToNodeId = "zone:sunken-name:location:flooded-throne-room", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("d103dabc-741d-a611-2292-27b6d3ec8fe9"), MapId = "zone:sunken-name", FromNodeId = "zone:sunken-name:entry", ToNodeId = "zone:sunken-name:location:drowned-threshold", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("8cb6b6e5-ed8c-f6c4-1428-382d8d413d6d"), MapId = "zone:sunken-name", FromNodeId = "zone:sunken-name:location:drowned-threshold", ToNodeId = "zone:sunken-name:entry", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("7b280e19-cc92-3a40-2ac8-e9c413481939"), MapId = "zone:sunken-name", FromNodeId = "zone:sunken-name:location:flooded-throne-room", ToNodeId = "zone:sunken-name:entry", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("ec100bd0-a015-e241-b76b-d97f3be223b2"), MapId = "zone:sunken-name", FromNodeId = "zone:sunken-name:location:tidelocked-vault", ToNodeId = "zone:sunken-name:entry", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
            ],
        },
        new Map
        {
            Id = "zone:the-droveway",
            MapType = MapType.Zone,
            OwnerId = "the-droveway",
            Name = "The Droveway",
            Description = "An old cattle track heading north from Crestfall. Dry-stone walls, pastures gone to seed, and a quiet that feels like it is waiting for something.",
            EntryNodeId = "zone:the-droveway:entry",
            IsActive = true,
            Version = 1,
            UpdatedAt = now,
            Nodes =
            [
                new MapNode { Id = "zone:the-droveway:entry", MapId = "zone:the-droveway", NodeType = MapNodeType.EntryPoint, RefId = null, DisplayName = "Arrival", Description = "Arrival point for The Droveway.", IconKey = "login", LayoutX = 47, LayoutY = 37, IsEntryPoint = true, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 0 },
                new MapNode { Id = "zone:the-droveway:location:drove-road", MapId = "zone:the-droveway", NodeType = MapNodeType.Location, RefId = "drove-road", DisplayName = "The Drove Road", Description = "The Drove Road in The Droveway.", IconKey = "place", LayoutX = 47, LayoutY = 20, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 20 },
                new MapNode { Id = "zone:the-droveway:location:sunken-fields", MapId = "zone:the-droveway", NodeType = MapNodeType.Location, RefId = "sunken-fields", DisplayName = "The Sunken Fields", Description = "The Sunken Fields in The Droveway. Hidden until discovered. Unlock: skill_check_passive.", IconKey = "place", LayoutX = 16, LayoutY = 25, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = true, UnlockType = "skill_check_passive", UnlockKey = null, DiscoverThreshold = 5, SortOrder = 30 },
                new MapNode { Id = "zone:the-droveway:location:weathered-waypost", MapId = "zone:the-droveway", NodeType = MapNodeType.Location, RefId = "weathered-waypost", DisplayName = "The Weathered Waypost", Description = "The Weathered Waypost in The Droveway.", IconKey = "place", LayoutX = 69, LayoutY = 12, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 40 },
                new MapNode { Id = "zone:the-droveway:exit:region:varenmark", MapId = "zone:the-droveway", NodeType = MapNodeType.RegionExit, RefId = "varenmark", DisplayName = "Return to Varenmark", Description = "The oldest settled march in the lowlands. Farmsteads, drove roads and market crossings gave way to wilder land. Whatever was here before the settlers is still here.", IconKey = "public", LayoutX = 47, LayoutY = 0, IsEntryPoint = false, IsExitPoint = true, TargetMapId = "region:varenmark", TargetNodeId = "region:varenmark:entry", IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 50 },
                new MapNode { Id = "zone:the-droveway:exit:zone:ashlen-wood", MapId = "zone:the-droveway", NodeType = MapNodeType.ZoneExit, RefId = "ashlen-wood", DisplayName = "Path to Ashlen Wood", Description = "Old-growth forest south of Crestfall. The light is wrong here. Animals behave strangely and old clearings refuse to grow back.", IconKey = "exit_to_app", LayoutX = 0, LayoutY = 20, IsEntryPoint = false, IsExitPoint = true, TargetMapId = "zone:ashlen-wood", TargetNodeId = "zone:ashlen-wood:entry", IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 60 },
                new MapNode { Id = "zone:the-droveway:exit:zone:crestfall", MapId = "zone:the-droveway", NodeType = MapNodeType.ZoneExit, RefId = "crestfall", DisplayName = "Road to Crestfall", Description = "A prosperous crossroads market town where three old drove roads meet. Warm lanterns, merchant stalls, and the last comfortable beds for many miles.", IconKey = "exit_to_app", LayoutX = 47, LayoutY = 39, IsEntryPoint = false, IsExitPoint = true, TargetMapId = "zone:crestfall", TargetNodeId = "zone:crestfall:entry", IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 70 },
            ],
            Edges =
            [
                new MapEdge { Id = new Guid("46356d04-36aa-8e60-ed22-0d70ad2d9032"), MapId = "zone:the-droveway", FromNodeId = "zone:the-droveway:entry", ToNodeId = "zone:the-droveway:exit:zone:crestfall", Direction = MapDirection.South, EdgeType = MapEdgeType.ExitToZone, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("78ca91bc-d8e8-945a-2a52-f01fa232594a"), MapId = "zone:the-droveway", FromNodeId = "zone:the-droveway:exit:region:varenmark", ToNodeId = "zone:the-droveway:exit:zone:crestfall", Direction = MapDirection.South, EdgeType = MapEdgeType.ExitToZone, Label = "drove-road", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("81b9f9eb-01ca-1cb7-a136-39169af33275"), MapId = "zone:the-droveway", FromNodeId = "zone:the-droveway:exit:zone:ashlen-wood", ToNodeId = "zone:the-droveway:location:sunken-fields", Direction = MapDirection.East, EdgeType = MapEdgeType.ExitToZone, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("c4f3eb50-961b-a8af-6086-659e9d543dd6"), MapId = "zone:the-droveway", FromNodeId = "zone:the-droveway:exit:zone:crestfall", ToNodeId = "zone:the-droveway:exit:region:varenmark", Direction = MapDirection.North, EdgeType = MapEdgeType.ExitToZone, Label = "drove-road", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("7b4f39d1-367c-9950-ee6f-b9c014ab733b"), MapId = "zone:the-droveway", FromNodeId = "zone:the-droveway:location:drove-road", ToNodeId = "zone:the-droveway:location:weathered-waypost", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("cc96744b-5cc1-f775-d59a-ae2b5e825718"), MapId = "zone:the-droveway", FromNodeId = "zone:the-droveway:location:drove-road", ToNodeId = "zone:the-droveway:location:sunken-fields", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = "field-track", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("cfa95525-bc2e-a319-4a20-1d63f914d11c"), MapId = "zone:the-droveway", FromNodeId = "zone:the-droveway:location:sunken-fields", ToNodeId = "zone:the-droveway:location:drove-road", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = "field-track", IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("f60aca6a-4f20-671f-e8ac-847ecf5901ea"), MapId = "zone:the-droveway", FromNodeId = "zone:the-droveway:location:sunken-fields", ToNodeId = "zone:the-droveway:exit:zone:ashlen-wood", Direction = MapDirection.West, EdgeType = MapEdgeType.ExitToZone, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("c43b18d8-714e-73cb-c9da-2e8316cc7e86"), MapId = "zone:the-droveway", FromNodeId = "zone:the-droveway:location:weathered-waypost", ToNodeId = "zone:the-droveway:location:drove-road", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
            ],
        },
        new Map
        {
            Id = "zone:the-halrow",
            MapType = MapType.Zone,
            OwnerId = "the-halrow",
            Name = "The Halrow",
            Description = "A buried estate at the end of the Droveway, walls still standing but roof long collapsed. Something has lived in the cellars for a very long time.",
            EntryNodeId = "zone:the-halrow:entry",
            IsActive = true,
            Version = 1,
            UpdatedAt = now,
            Nodes =
            [
                new MapNode { Id = "zone:the-halrow:entry", MapId = "zone:the-halrow", NodeType = MapNodeType.EntryPoint, RefId = null, DisplayName = "Arrival", Description = "Arrival point for The Halrow.", IconKey = "login", LayoutX = 31, LayoutY = 45, IsEntryPoint = true, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 0 },
                new MapNode { Id = "zone:the-halrow:location:collapsed-vault", MapId = "zone:the-halrow", NodeType = MapNodeType.Location, RefId = "collapsed-vault", DisplayName = "The Collapsed Vault", Description = "The Collapsed Vault in The Halrow. Hidden until discovered. Unlock: quest.", IconKey = "place", LayoutX = 42, LayoutY = 20, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = true, UnlockType = "quest", UnlockKey = null, DiscoverThreshold = null, SortOrder = 20 },
                new MapNode { Id = "zone:the-halrow:location:halrow-threshold", MapId = "zone:the-halrow", NodeType = MapNodeType.Location, RefId = "halrow-threshold", DisplayName = "The Halrow Threshold", Description = "The Halrow Threshold in The Halrow.", IconKey = "place", LayoutX = 31, LayoutY = 31, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 30 },
                new MapNode { Id = "zone:the-halrow:location:rootbound-cellars", MapId = "zone:the-halrow", NodeType = MapNodeType.Location, RefId = "rootbound-cellars", DisplayName = "The Rootbound Cellars", Description = "The Rootbound Cellars in The Halrow.", IconKey = "place", LayoutX = 21, LayoutY = 20, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 40 },
                new MapNode { Id = "zone:the-halrow:exit:region:varenmark", MapId = "zone:the-halrow", NodeType = MapNodeType.RegionExit, RefId = "varenmark", DisplayName = "Return to Varenmark", Description = "The oldest settled march in the lowlands. Farmsteads, drove roads and market crossings gave way to wilder land. Whatever was here before the settlers is still here.", IconKey = "public", LayoutX = 31, LayoutY = 47, IsEntryPoint = false, IsExitPoint = true, TargetMapId = "region:varenmark", TargetNodeId = "region:varenmark:entry", IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 50 },
            ],
            Edges =
            [
                new MapEdge { Id = new Guid("7aba638c-5896-c0e3-6170-a8b85f56d830"), MapId = "zone:the-halrow", FromNodeId = "zone:the-halrow:entry", ToNodeId = "zone:the-halrow:location:halrow-threshold", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("bc13a766-d4e8-96ba-ed89-9b3d7abb1b8c"), MapId = "zone:the-halrow", FromNodeId = "zone:the-halrow:entry", ToNodeId = "zone:the-halrow:exit:region:varenmark", Direction = MapDirection.South, EdgeType = MapEdgeType.ExitToRegion, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("5e8da43f-2bb5-c823-ab52-a5339783a763"), MapId = "zone:the-halrow", FromNodeId = "zone:the-halrow:exit:region:varenmark", ToNodeId = "zone:the-halrow:entry", Direction = MapDirection.North, EdgeType = MapEdgeType.ExitToRegion, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("dc1ce88c-516d-e951-6769-8fedefe794fd"), MapId = "zone:the-halrow", FromNodeId = "zone:the-halrow:location:collapsed-vault", ToNodeId = "zone:the-halrow:location:rootbound-cellars", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("4104b148-d483-68b2-14ab-70ef01487c40"), MapId = "zone:the-halrow", FromNodeId = "zone:the-halrow:location:halrow-threshold", ToNodeId = "zone:the-halrow:entry", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("23ab5eb0-0017-091d-0114-fe6b0deadf42"), MapId = "zone:the-halrow", FromNodeId = "zone:the-halrow:location:rootbound-cellars", ToNodeId = "zone:the-halrow:location:collapsed-vault", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
            ],
        },
        new Map
        {
            Id = "zone:tidewrack-flats",
            MapType = MapType.Zone,
            OwnerId = "tidewrack-flats",
            Name = "The Tidewrack Flats",
            Description = "Tide-scoured mudflats littered with shipwreck timber and kelp-draped bones, hunted by scavengers and wading predators.",
            EntryNodeId = "zone:tidewrack-flats:entry",
            IsActive = true,
            Version = 1,
            UpdatedAt = now,
            Nodes =
            [
                new MapNode { Id = "zone:tidewrack-flats:entry", MapId = "zone:tidewrack-flats", NodeType = MapNodeType.EntryPoint, RefId = null, DisplayName = "Arrival", Description = "Arrival point for The Tidewrack Flats.", IconKey = "login", LayoutX = 32, LayoutY = 24, IsEntryPoint = true, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 0 },
                new MapNode { Id = "zone:tidewrack-flats:location:bone-strand", MapId = "zone:tidewrack-flats", NodeType = MapNodeType.Location, RefId = "bone-strand", DisplayName = "The Bone Strand", Description = "The Bone Strand in The Tidewrack Flats.", IconKey = "place", LayoutX = 45, LayoutY = 35, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 20 },
                new MapNode { Id = "zone:tidewrack-flats:location:tidal-grotto", MapId = "zone:tidewrack-flats", NodeType = MapNodeType.Location, RefId = "tidal-grotto", DisplayName = "The Tidal Grotto", Description = "The Tidal Grotto in The Tidewrack Flats. Hidden until discovered. Unlock: skill_check_active.", IconKey = "place", LayoutX = 20, LayoutY = 40, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = true, UnlockType = "skill_check_active", UnlockKey = null, DiscoverThreshold = 14, SortOrder = 30 },
                new MapNode { Id = "zone:tidewrack-flats:location:wrack-shore", MapId = "zone:tidewrack-flats", NodeType = MapNodeType.Location, RefId = "wrack-shore", DisplayName = "Wrack Shore", Description = "Wrack Shore in The Tidewrack Flats.", IconKey = "place", LayoutX = 30, LayoutY = 20, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 40 },
            ],
            Edges =
            [
                new MapEdge { Id = new Guid("f46becf5-770d-c939-4a67-35178de356a0"), MapId = "zone:tidewrack-flats", FromNodeId = "zone:tidewrack-flats:entry", ToNodeId = "zone:tidewrack-flats:location:wrack-shore", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("843d1e04-3918-86b5-d73a-e4e4cf55914a"), MapId = "zone:tidewrack-flats", FromNodeId = "zone:tidewrack-flats:entry", ToNodeId = "zone:tidewrack-flats:location:tidal-grotto", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("cf0a48f4-9f11-fa22-be9c-035187dab71e"), MapId = "zone:tidewrack-flats", FromNodeId = "zone:tidewrack-flats:entry", ToNodeId = "zone:tidewrack-flats:location:bone-strand", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("8d2a7676-9f31-cfef-d21f-c7983ec93b43"), MapId = "zone:tidewrack-flats", FromNodeId = "zone:tidewrack-flats:location:bone-strand", ToNodeId = "zone:tidewrack-flats:entry", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("b8e73028-2708-0041-6eda-5ac3f7498475"), MapId = "zone:tidewrack-flats", FromNodeId = "zone:tidewrack-flats:location:tidal-grotto", ToNodeId = "zone:tidewrack-flats:entry", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("52e9149a-d25f-0ad3-d849-5e8f45ac709e"), MapId = "zone:tidewrack-flats", FromNodeId = "zone:tidewrack-flats:location:wrack-shore", ToNodeId = "zone:tidewrack-flats:entry", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
            ],
        },
        new Map
        {
            Id = "zone:tolvaren",
            MapType = MapType.Zone,
            OwnerId = "tolvaren",
            Name = "Tolvaren",
            Description = "A salt-caked Thysmara port city clinging to the cliff face, its harbour full of storm-worn ships and bold-faced traders.",
            EntryNodeId = "zone:tolvaren:entry",
            IsActive = true,
            Version = 1,
            UpdatedAt = now,
            Nodes =
            [
                new MapNode { Id = "zone:tolvaren:entry", MapId = "zone:tolvaren", NodeType = MapNodeType.EntryPoint, RefId = null, DisplayName = "Arrival", Description = "Arrival point for Tolvaren.", IconKey = "login", LayoutX = 24, LayoutY = 20, IsEntryPoint = true, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 0 },
                new MapNode { Id = "zone:tolvaren:location:cliff-road-market", MapId = "zone:tolvaren", NodeType = MapNodeType.Location, RefId = "cliff-road-market", DisplayName = "Cliff Road Market", Description = "Cliff Road Market in Tolvaren.", IconKey = "place", LayoutX = 35, LayoutY = 10, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 20 },
                new MapNode { Id = "zone:tolvaren:location:saltcrow-inn", MapId = "zone:tolvaren", NodeType = MapNodeType.Location, RefId = "saltcrow-inn", DisplayName = "The Saltcrow Inn", Description = "The Saltcrow Inn in Tolvaren.", IconKey = "place", LayoutX = 15, LayoutY = 30, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 30 },
                new MapNode { Id = "zone:tolvaren:location:tolvaren-harbour", MapId = "zone:tolvaren", NodeType = MapNodeType.Location, RefId = "tolvaren-harbour", DisplayName = "Tolvaren Harbour", Description = "Tolvaren Harbour in Tolvaren.", IconKey = "place", LayoutX = 10, LayoutY = 7, IsEntryPoint = false, IsExitPoint = false, TargetMapId = null, TargetNodeId = null, IsHidden = false, UnlockType = null, UnlockKey = null, DiscoverThreshold = null, SortOrder = 40 },
            ],
            Edges =
            [
                new MapEdge { Id = new Guid("f5f342f9-50fc-7c8a-2420-948bfde6e31f"), MapId = "zone:tolvaren", FromNodeId = "zone:tolvaren:entry", ToNodeId = "zone:tolvaren:location:saltcrow-inn", Direction = MapDirection.South, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("4efafd2d-cf19-13cf-f784-596659d6febd"), MapId = "zone:tolvaren", FromNodeId = "zone:tolvaren:entry", ToNodeId = "zone:tolvaren:location:cliff-road-market", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("ea4141c4-d390-ba9a-3797-ea4c83c273b0"), MapId = "zone:tolvaren", FromNodeId = "zone:tolvaren:entry", ToNodeId = "zone:tolvaren:location:tolvaren-harbour", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("2bcc70be-6d83-e249-03f1-89e61ed331d8"), MapId = "zone:tolvaren", FromNodeId = "zone:tolvaren:location:cliff-road-market", ToNodeId = "zone:tolvaren:entry", Direction = MapDirection.West, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("1abad397-24f9-3a1f-5394-10e615b3a7e4"), MapId = "zone:tolvaren", FromNodeId = "zone:tolvaren:location:saltcrow-inn", ToNodeId = "zone:tolvaren:entry", Direction = MapDirection.North, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
                new MapEdge { Id = new Guid("5990e5c3-ed6d-961d-ff4a-8124bda7b340"), MapId = "zone:tolvaren", FromNodeId = "zone:tolvaren:location:tolvaren-harbour", ToNodeId = "zone:tolvaren:entry", Direction = MapDirection.East, EdgeType = MapEdgeType.Walk, Label = null, IsTraversable = true, IsHidden = false },
            ],
        },
    ];
}
