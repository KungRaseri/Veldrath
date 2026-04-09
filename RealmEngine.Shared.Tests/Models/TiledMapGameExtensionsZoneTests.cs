using System.Text.Json;
using FluentAssertions;
using RealmEngine.Shared.Models;
using RealmEngine.Shared.Models.Tiled;

namespace RealmEngine.Shared.Tests.Models;

/// <summary>Unit tests for zone-map extension methods on <see cref="TiledMapGameExtensions"/>.</summary>
public class TiledMapGameExtensionsZoneTests
{
    private static TiledProperty StringProp(string name, string value) =>
        new() { Name = name, Type = "string", Value = JsonSerializer.SerializeToElement(value) };

    private static TiledMap MakeMap(int tileW = 16, int tileH = 16) =>
        new()
        {
            Width = 31, Height = 25, TileWidth = tileW, TileHeight = tileH,
            Properties = [],
            Tilesets   = [new TiledTileset { FirstGid = 1, Columns = 49, TileWidth = 16, TileHeight = 16 }],
            Layers     = [],
        };

    // ── GetExitTiles ───────────────────────────────────────────────────────────

    [Fact]
    public void GetExitTiles_Returns_Empty_When_Exits_Layer_Absent()
    {
        var map = MakeMap();
        map.GetExitTiles().Should().BeEmpty();
    }

    [Fact]
    public void GetExitTiles_Returns_Exit_With_ToZoneId()
    {
        var map = MakeMap();
        map.Layers =
        [
            new TiledLayer
            {
                Id = 1, Type = "objectgroup", Name = "exits",
                Objects =
                [
                    new TiledObject
                    {
                        Id = 1, Type = "exit",
                        X  = 5 * 16, Y = 3 * 16, Width = 16, Height = 16,
                        Properties = [StringProp("toZoneId", "crestfall")],
                    },
                ],
            },
        ];

        var exits = map.GetExitTiles();
        exits.Should().ContainSingle();
        exits[0].TileX.Should().Be(5);
        exits[0].TileY.Should().Be(3);
        exits[0].ToZoneId.Should().Be("crestfall");
    }

    [Fact]
    public void GetExitTiles_Returns_Exit_Without_ToZoneId_As_Empty_String()
    {
        // An exit with no toZoneId property means "return to region map".
        var map = MakeMap();
        map.Layers =
        [
            new TiledLayer
            {
                Id = 1, Type = "objectgroup", Name = "exits",
                Objects =
                [
                    new TiledObject
                    {
                        Id = 1, Type = "exit",
                        X  = 0, Y = 0, Width = 16, Height = 16,
                        Properties = [], // no toZoneId
                    },
                ],
            },
        ];

        var exits = map.GetExitTiles();
        exits.Should().ContainSingle();
        exits[0].TileX.Should().Be(0);
        exits[0].TileY.Should().Be(0);
        exits[0].ToZoneId.Should().BeEmpty();
    }

    [Fact]
    public void GetExitTiles_Skips_Objects_Without_Exit_Type()
    {
        var map = MakeMap();
        map.Layers =
        [
            new TiledLayer
            {
                Id = 1, Type = "objectgroup", Name = "exits",
                Objects =
                [
                    new TiledObject { Id = 1, Type = "spawn", X = 0, Y = 0, Width = 16, Height = 16 },
                    new TiledObject
                    {
                        Id = 2, Type = "exit",
                        X  = 16, Y = 0, Width = 16, Height = 16,
                        Properties = [StringProp("toZoneId", "some-zone")],
                    },
                ],
            },
        ];

        map.GetExitTiles().Should().ContainSingle(e => e.ToZoneId == "some-zone");
    }

    [Fact]
    public void GetExitTiles_Pixel_Coords_Converted_To_Tile_Coords()
    {
        var map = MakeMap(tileW: 32, tileH: 32);
        map.Layers =
        [
            new TiledLayer
            {
                Id = 1, Type = "objectgroup", Name = "exits",
                Objects =
                [
                    new TiledObject
                    {
                        Id = 1, Type = "exit",
                        X  = 4 * 32, Y = 7 * 32, Width = 32, Height = 32,
                        Properties = [],
                    },
                ],
            },
        ];

        var exit = map.GetExitTiles().Single();
        exit.TileX.Should().Be(4);
        exit.TileY.Should().Be(7);
    }

    [Fact]
    public void GetExitTiles_Returns_Multiple_Exits()
    {
        var map = MakeMap();
        map.Layers =
        [
            new TiledLayer
            {
                Id = 1, Type = "objectgroup", Name = "exits",
                Objects =
                [
                    new TiledObject
                    {
                        Id = 1, Type = "exit",
                        X  = 0, Y = 0, Width = 16, Height = 16,
                        Properties = [], // region-map exit
                    },
                    new TiledObject
                    {
                        Id = 2, Type = "exit",
                        X  = 16, Y = 0, Width = 16, Height = 16,
                        Properties = [StringProp("toZoneId", "dungeon-of-dawn")],
                    },
                ],
            },
        ];

        var exits = map.GetExitTiles();
        exits.Should().HaveCount(2);
        exits.Should().ContainSingle(e => string.IsNullOrEmpty(e.ToZoneId));
        exits.Should().ContainSingle(e => e.ToZoneId == "dungeon-of-dawn");
    }
}
