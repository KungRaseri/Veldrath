using System.Text.Json;
using FluentAssertions;
using RealmEngine.Shared.Models;
using RealmEngine.Shared.Models.Tiled;

namespace RealmEngine.Shared.Tests.Models;

/// <summary>Unit tests for region-map extension methods on <see cref="TiledMapGameExtensions"/>.</summary>
public class TiledMapGameExtensionsRegionTests
{
    private static TiledProperty StringProp(string name, string value) =>
        new() { Name = name, Type = "string", Value = JsonSerializer.SerializeToElement(value) };

    private static TiledProperty IntProp(string name, int value) =>
        new() { Name = name, Type = "int", Value = JsonSerializer.SerializeToElement(value) };

    private static TiledMap MakeMap(string? regionId = "thornveil", int tileW = 16, int tileH = 16) =>
        new()
        {
            Width = 30, Height = 20, TileWidth = tileW, TileHeight = tileH,
            Properties = regionId is null ? [] :
            [
                StringProp("regionId",   regionId),
                StringProp("tilesetKey", "overworld"),
            ],
            Tilesets = [new TiledTileset { FirstGid = 1, Columns = 49, TileWidth = 16, TileHeight = 16 }],
            Layers = [],
        };

    // ── GetRegionId ────────────────────────────────────────────────────────────

    [Fact]
    public void GetRegionId_Returns_Correct_Value()
    {
        var map = MakeMap(regionId: "thornveil");
        map.GetRegionId().Should().Be("thornveil");
    }

    [Fact]
    public void GetRegionId_Returns_Empty_String_When_Property_Missing()
    {
        var map = MakeMap(regionId: null);
        map.GetRegionId().Should().BeEmpty();
    }

    // ── GetZoneEntries ─────────────────────────────────────────────────────────

    [Fact]
    public void GetZoneEntries_Returns_Empty_When_Zones_Layer_Absent()
    {
        var map = MakeMap();
        map.GetZoneEntries().Should().BeEmpty();
    }

    [Fact]
    public void GetZoneEntries_Returns_Correct_Entries()
    {
        var map = MakeMap();
        map.Layers =
        [
            new TiledLayer
            {
                Id = 1, Type = "objectgroup", Name = "zones",
                Objects =
                [
                    new TiledObject
                    {
                        Id = 1, Name = "fenwick-crossing",
                        X  = 5 * 16, Y = 8 * 16, Width = 16, Height = 16,
                        Properties = [StringProp("displayName", "Fenwick Crossing"), IntProp("minLevel", 1), IntProp("maxLevel", 5)],
                    },
                    new TiledObject
                    {
                        Id = 2, Name = "ashwood-hollow",
                        X  = 10 * 16, Y = 3 * 16, Width = 16, Height = 16,
                        Properties = [StringProp("displayName", "Ashwood Hollow"), IntProp("minLevel", 3), IntProp("maxLevel", 8)],
                    },
                ],
            },
        ];

        var entries = map.GetZoneEntries();
        entries.Should().HaveCount(2);

        var fc = entries.Single(e => e.ZoneSlug == "fenwick-crossing");
        fc.TileX.Should().Be(5);
        fc.TileY.Should().Be(8);
        fc.DisplayName.Should().Be("Fenwick Crossing");
        fc.MinLevel.Should().Be(1);
        fc.MaxLevel.Should().Be(5);

        var ah = entries.Single(e => e.ZoneSlug == "ashwood-hollow");
        ah.TileX.Should().Be(10);
        ah.TileY.Should().Be(3);
    }

    [Fact]
    public void GetZoneEntries_Skips_Objects_With_Empty_Name()
    {
        var map = MakeMap();
        map.Layers =
        [
            new TiledLayer
            {
                Id = 1, Type = "objectgroup", Name = "zones",
                Objects =
                [
                    new TiledObject { Id = 1, Name = "",              X = 0, Y = 0, Width = 16, Height = 16 },
                    new TiledObject { Id = 2, Name = "valid-zone",    X = 16, Y = 0, Width = 16, Height = 16 },
                ],
            },
        ];

        map.GetZoneEntries().Should().ContainSingle(e => e.ZoneSlug == "valid-zone");
    }

    [Fact]
    public void GetZoneEntries_Uses_Object_Name_As_DisplayName_When_Property_Missing()
    {
        var map = MakeMap();
        map.Layers =
        [
            new TiledLayer
            {
                Id = 1, Type = "objectgroup", Name = "zones",
                Objects =
                [
                    new TiledObject { Id = 1, Name = "dungeon-cave", X = 16, Y = 16, Width = 16, Height = 16 },
                ],
            },
        ];

        map.GetZoneEntries().Single().DisplayName.Should().Be("dungeon-cave");
    }

    [Fact]
    public void GetZoneEntries_Pixel_Coords_Converted_To_Tile_Coords()
    {
        var map = MakeMap(tileW: 32, tileH: 32); // non-default tile size
        map.Layers =
        [
            new TiledLayer
            {
                Id = 1, Type = "objectgroup", Name = "zones",
                Objects =
                [
                    new TiledObject
                    {
                        Id = 1, Name = "test-zone",
                        X  = 3 * 32, Y = 7 * 32, Width = 32, Height = 32,
                    },
                ],
            },
        ];

        var entry = map.GetZoneEntries().Single();
        entry.TileX.Should().Be(3);
        entry.TileY.Should().Be(7);
    }

    // ── GetRegionExits ─────────────────────────────────────────────────────────

    [Fact]
    public void GetRegionExits_Returns_Empty_When_Layer_Absent()
    {
        var map = MakeMap();
        map.GetRegionExits().Should().BeEmpty();
    }

    [Fact]
    public void GetRegionExits_Returns_Correct_Exits()
    {
        var map = MakeMap();
        map.Layers =
        [
            new TiledLayer
            {
                Id = 1, Type = "objectgroup", Name = "region_exits",
                Objects =
                [
                    new TiledObject { Id = 1, Name = "varenmark",  X = 29 * 16, Y = 10 * 16, Width = 16, Height = 16 },
                    new TiledObject { Id = 2, Name = "greymoor",   X = 0,       Y = 10 * 16, Width = 16, Height = 16 },
                ],
            },
        ];

        var exits = map.GetRegionExits();
        exits.Should().HaveCount(2);

        exits.Single(e => e.TargetRegionId == "varenmark").TileX.Should().Be(29);
        exits.Single(e => e.TargetRegionId == "greymoor").TileX.Should().Be(0);
    }

    [Fact]
    public void GetRegionExits_Skips_Objects_With_Empty_Name()
    {
        var map = MakeMap();
        map.Layers =
        [
            new TiledLayer
            {
                Id = 1, Type = "objectgroup", Name = "region_exits",
                Objects =
                [
                    new TiledObject { Id = 1, Name = "",         X = 0,      Y = 0, Width = 16, Height = 16 },
                    new TiledObject { Id = 2, Name = "greymoor", X = 16, Y = 16, Width = 16, Height = 16 },
                ],
            },
        ];

        map.GetRegionExits().Should().ContainSingle(e => e.TargetRegionId == "greymoor");
    }

    [Fact]
    public void GetRegionExits_Pixel_Coords_Converted_To_Tile_Coords()
    {
        var map = MakeMap(tileW: 32, tileH: 32);
        map.Layers =
        [
            new TiledLayer
            {
                Id = 1, Type = "objectgroup", Name = "region_exits",
                Objects =
                [
                    new TiledObject { Id = 1, Name = "varenmark", X = 5 * 32, Y = 12 * 32, Width = 32, Height = 32 },
                ],
            },
        ];

        var exit = map.GetRegionExits().Single();
        exit.TileX.Should().Be(5);
        exit.TileY.Should().Be(12);
    }
}
