using RealmUnbound.Client.ViewModels;
using RealmUnbound.Contracts.Tilemap;

namespace RealmUnbound.Client.Tests.ViewModels;

/// <summary>Unit tests for <see cref="TilemapViewModel"/>.</summary>
public class TilemapViewModelTests : TestBase
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TilemapViewModel MakeVm() =>
        new((_, _, _) => System.Threading.Tasks.Task.CompletedTask);

    /// <summary>
    /// Returns a minimal <see cref="TileMapDto"/> width × height tiles.
    /// <paramref name="blockedTiles"/> lists (x, y) pairs that are solid.
    /// </summary>
    private static TileMapDto MakeMap(int width, int height,
        IEnumerable<(int x, int y)>? blockedTiles = null)
    {
        var collision = new bool[width * height];
        if (blockedTiles is not null)
            foreach (var (x, y) in blockedTiles)
                collision[y * width + x] = true;

        return new TileMapDto(
            ZoneId:        "test-zone",
            TilesetKey:    "roguelike_base",
            Width:         width,
            Height:        height,
            TileSize:      16,
            Layers:        [],
            CollisionMask: collision,
            FogMask:       new bool[width * height],
            ExitTiles:     [],
            SpawnPoints:   []);
    }

    // ── TileMapData / HasMap ───────────────────────────────────────────────────

    [Fact]
    public void HasMap_Is_False_Before_TileMapData_Is_Set()
    {
        var vm = MakeVm();
        vm.HasMap.Should().BeFalse();
    }

    [Fact]
    public void HasMap_Is_True_After_TileMapData_Is_Assigned()
    {
        var vm  = MakeVm();
        vm.TileMapData = MakeMap(10, 10);
        vm.HasMap.Should().BeTrue();
    }

    [Fact]
    public void HasMap_Becomes_False_When_TileMapData_Is_Cleared()
    {
        var vm = MakeVm();
        vm.TileMapData = MakeMap(10, 10);
        vm.TileMapData = null;
        vm.HasMap.Should().BeFalse();
    }

    // ── IsBlocked — no map ────────────────────────────────────────────────────

    [Fact]
    public void IsBlocked_Returns_False_When_No_Map_Loaded()
    {
        var vm = MakeVm();
        // The method must return false (not blocked) so the server can decide
        vm.IsBlocked(0, 0).Should().BeFalse();
        vm.IsBlocked(5, 5).Should().BeFalse();
    }

    // ── IsBlocked — out-of-bounds ─────────────────────────────────────────────

    [Theory]
    [InlineData(-1,  0)]
    [InlineData( 0, -1)]
    [InlineData(10,  0)]   // exactly at width
    [InlineData( 0, 10)]   // exactly at height
    [InlineData(99, 99)]
    public void IsBlocked_Returns_True_For_Out_Of_Bounds_Tiles(int x, int y)
    {
        var vm = MakeVm();
        vm.TileMapData = MakeMap(10, 10);
        vm.IsBlocked(x, y).Should().BeTrue();
    }

    // ── IsBlocked — collision mask ────────────────────────────────────────────

    [Fact]
    public void IsBlocked_Returns_True_For_Solid_Tile()
    {
        var vm = MakeVm();
        vm.TileMapData = MakeMap(5, 5, [(2, 3)]);
        vm.IsBlocked(2, 3).Should().BeTrue();
    }

    [Fact]
    public void IsBlocked_Returns_False_For_Open_Tile()
    {
        var vm = MakeVm();
        vm.TileMapData = MakeMap(5, 5, [(2, 3)]);
        vm.IsBlocked(1, 1).Should().BeFalse();
    }

    [Fact]
    public void IsBlocked_Correctly_Indexes_Using_Row_Major_Order()
    {
        // tile (0,1) is at idx = 1 * 4 + 0 = 4; tile (3,0) is at idx = 0 * 4 + 3 = 3
        var vm = MakeVm();
        vm.TileMapData = MakeMap(4, 4, [(0, 1), (3, 0)]);
        vm.IsBlocked(0, 1).Should().BeTrue();
        vm.IsBlocked(3, 0).Should().BeTrue();
        vm.IsBlocked(1, 0).Should().BeFalse();
        vm.IsBlocked(0, 0).Should().BeFalse();
    }

    // ── CenterCameraOn ────────────────────────────────────────────────────────

    [Fact]
    public void CenterCameraOn_Does_Nothing_When_No_Map_Loaded()
    {
        var vm = MakeVm();
        vm.CenterCameraOn(10, 10, 5, 5);
        vm.CameraX.Should().Be(0);
        vm.CameraY.Should().Be(0);
    }

    [Fact]
    public void CenterCameraOn_Positions_Camera_At_Tile_Centre()
    {
        var vm = MakeVm();
        vm.TileMapData = MakeMap(20, 20);

        vm.CenterCameraOn(10, 10, 5, 5);

        // expected = 10 - 5/2 = 8 (integer division)
        vm.CameraX.Should().Be(8);
        vm.CameraY.Should().Be(8);
    }

    [Fact]
    public void CenterCameraOn_Clamps_Camera_To_Left_Top_Edge()
    {
        var vm = MakeVm();
        vm.TileMapData = MakeMap(20, 20);

        vm.CenterCameraOn(0, 0, 5, 5);

        vm.CameraX.Should().Be(0);
        vm.CameraY.Should().Be(0);
    }

    [Fact]
    public void CenterCameraOn_Clamps_Camera_To_Right_Bottom_Edge()
    {
        var vm = MakeVm();
        vm.TileMapData = MakeMap(20, 20);

        // player is at tile (19,19); max camera = 20 - 5 = 15
        vm.CenterCameraOn(19, 19, 5, 5);

        vm.CameraX.Should().Be(15);
        vm.CameraY.Should().Be(15);
    }

    [Fact]
    public void CenterCameraOn_Clamps_To_Zero_When_Viewport_Wider_Than_Map()
    {
        var vm = MakeVm();
        vm.TileMapData = MakeMap(4, 4);

        // viewport (10 tiles) is larger than the map (4 tiles) — camera must stay at 0
        vm.CenterCameraOn(2, 2, 10, 10);

        vm.CameraX.Should().Be(0);
        vm.CameraY.Should().Be(0);
    }

    [Fact]
    public void CenterCameraOn_Uses_ViewModel_Viewport_Dimensions_When_No_Explicit_Overload()
    {
        var vm = MakeVm();
        vm.TileMapData = MakeMap(20, 20);
        vm.ViewportWidthTiles  = 4;
        vm.ViewportHeightTiles = 4;

        vm.CenterCameraOn(10, 10);

        // expected = 10 - 4/2 = 8
        vm.CameraX.Should().Be(8);
        vm.CameraY.Should().Be(8);
    }

    // ── UpsertEntity ──────────────────────────────────────────────────────────

    [Fact]
    public void UpsertEntity_Adds_New_Entity()
    {
        var vm = MakeVm();
        var id = Guid.NewGuid();

        vm.UpsertEntity(id, "player", "player", 3, 5, "down");

        vm.Entities.Should().ContainSingle(e =>
            e.EntityId   == id   &&
            e.EntityType == "player" &&
            e.TileX      == 3    &&
            e.TileY      == 5    &&
            e.Direction  == "down");
    }

    [Fact]
    public void UpsertEntity_Replaces_Existing_Entity()
    {
        var vm = MakeVm();
        var id = Guid.NewGuid();

        vm.UpsertEntity(id, "player", "player", 3, 5, "down");
        vm.UpsertEntity(id, "player", "player", 7, 2, "up");

        vm.Entities.Should().ContainSingle()
            .Which.Should().Match<TileEntityState>(e =>
                e.TileX == 7 && e.TileY == 2 && e.Direction == "up");
    }

    [Fact]
    public void UpsertEntity_Multiple_Different_Entities_All_Present()
    {
        var vm  = MakeVm();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        vm.UpsertEntity(id1, "player", "player", 1, 1, "down");
        vm.UpsertEntity(id2, "enemy",  "goblin-scout", 5, 5, "left");

        vm.Entities.Should().HaveCount(2);
        vm.Entities.Should().ContainSingle(e => e.EntityId == id1);
        vm.Entities.Should().ContainSingle(e => e.EntityId == id2);
    }

    // ── RemoveEntity ──────────────────────────────────────────────────────────

    [Fact]
    public void RemoveEntity_Removes_Present_Entity()
    {
        var vm = MakeVm();
        var id = Guid.NewGuid();

        vm.UpsertEntity(id, "enemy", "goblin-scout", 3, 4, "right");
        vm.RemoveEntity(id);

        vm.Entities.Should().BeEmpty();
    }

    [Fact]
    public void RemoveEntity_Is_Idempotent_When_Entity_Not_Present()
    {
        var vm = MakeVm();
        var id = Guid.NewGuid();

        var act = () => vm.RemoveEntity(id);

        act.Should().NotThrow();
        vm.Entities.Should().BeEmpty();
    }

    [Fact]
    public void RemoveEntity_Only_Removes_Target_Entity()
    {
        var vm  = MakeVm();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        vm.UpsertEntity(id1, "player", "player", 1, 1, "down");
        vm.UpsertEntity(id2, "enemy",  "goblin-scout", 5, 5, "left");
        vm.RemoveEntity(id1);

        vm.Entities.Should().ContainSingle(e => e.EntityId == id2);
    }

    // ── RevealAround ─────────────────────────────────────────────────────────

    [Fact]
    public void RevealAround_Marks_Centre_Tile_As_Revealed()
    {
        var vm = MakeVm();
        vm.RevealAround(5, 5, radius: 0);
        vm.RevealedTiles.Should().Contain("5:5");
    }

    [Fact]
    public void RevealAround_Default_Radius_Reveals_Circle_Of_4()
    {
        var vm = MakeVm();
        vm.RevealAround(0, 0);

        // All tiles within radius 4 of (0,0) should be present
        vm.RevealedTiles.Should().Contain("0:0");
        vm.RevealedTiles.Should().Contain("4:0");
        vm.RevealedTiles.Should().Contain("0:4");
        vm.RevealedTiles.Should().Contain("-4:0");
        vm.RevealedTiles.Should().Contain("0:-4");
    }

    [Fact]
    public void RevealAround_Does_Not_Reveal_Tiles_Outside_Radius()
    {
        var vm = MakeVm();
        vm.RevealAround(0, 0, radius: 2);

        // (3,0) is distance 3 — outside radius 2
        vm.RevealedTiles.Should().NotContain("3:0");
        vm.RevealedTiles.Should().NotContain("0:3");
    }

    [Fact]
    public void RevealAround_Is_Additive_Across_Multiple_Calls()
    {
        var vm = MakeVm();
        vm.RevealAround(0, 0, radius: 1);
        vm.RevealAround(10, 10, radius: 1);

        vm.RevealedTiles.Should().Contain("0:0");
        vm.RevealedTiles.Should().Contain("10:10");
    }

    // ── ToggleMiniMapCommand ───────────────────────────────────────────────────

    [Fact]
    public void ToggleMiniMapCommand_Opens_MiniMap_When_Closed()
    {
        var vm = MakeVm();
        vm.IsMiniMapOpen.Should().BeFalse();

        vm.ToggleMiniMapCommand.Execute(System.Reactive.Unit.Default).Subscribe();

        vm.IsMiniMapOpen.Should().BeTrue();
    }

    [Fact]
    public void ToggleMiniMapCommand_Closes_MiniMap_When_Open()
    {
        var vm = MakeVm();
        vm.ToggleMiniMapCommand.Execute(System.Reactive.Unit.Default).Subscribe();
        vm.ToggleMiniMapCommand.Execute(System.Reactive.Unit.Default).Subscribe();

        vm.IsMiniMapOpen.Should().BeFalse();
    }
}
