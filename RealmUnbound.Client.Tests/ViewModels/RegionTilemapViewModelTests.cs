using RealmUnbound.Client.ViewModels;
using RealmUnbound.Contracts.Tilemap;

namespace RealmUnbound.Client.Tests.ViewModels;

/// <summary>Unit tests for <see cref="RegionTilemapViewModel"/>.</summary>
public class RegionTilemapViewModelTests : TestBase
{
    private static RegionTilemapViewModel MakeVm() =>
        new((_, _, _) => System.Threading.Tasks.Task.CompletedTask);

    private static RegionMapDto MakeRegionMap(int width, int height,
        IEnumerable<(int x, int y)>? blockedTiles = null)
    {
        var collision = new bool[width * height];
        if (blockedTiles is not null)
            foreach (var (x, y) in blockedTiles)
                collision[y * width + x] = true;

        return new RegionMapDto(
            RegionId:      "thornveil",
            TilesetKey:    "overworld",
            Width:         width,
            Height:        height,
            TileSize:      16,
            Layers:        [],
            CollisionMask: collision,
            ZoneEntries:   [],
            RegionExits:   [],
            Labels:        [],
            Paths:         []);
    }

    // ── HasMap ─────────────────────────────────────────────────────────────────

    [Fact]
    public void HasMap_Is_False_Before_RegionMapData_Is_Set()
    {
        var vm = MakeVm();
        vm.HasMap.Should().BeFalse();
    }

    [Fact]
    public void HasMap_Is_True_After_RegionMapData_Is_Assigned()
    {
        var vm = MakeVm();
        vm.RegionMapData = MakeRegionMap(10, 10);
        vm.HasMap.Should().BeTrue();
    }

    [Fact]
    public void HasMap_Becomes_False_When_RegionMapData_Is_Cleared()
    {
        var vm = MakeVm();
        vm.RegionMapData = MakeRegionMap(10, 10);
        vm.RegionMapData = null;
        vm.HasMap.Should().BeFalse();
    }

    // ── IsBlocked — no map ────────────────────────────────────────────────────

    [Fact]
    public void IsBlocked_Returns_False_When_No_Map_Loaded()
    {
        var vm = MakeVm();
        vm.IsBlocked(0, 0).Should().BeFalse();
        vm.IsBlocked(5, 5).Should().BeFalse();
    }

    // ── IsBlocked — out-of-bounds ─────────────────────────────────────────────

    [Theory]
    [InlineData(-1,  0)]
    [InlineData( 0, -1)]
    [InlineData(10,  0)]
    [InlineData( 0, 10)]
    [InlineData(99, 99)]
    public void IsBlocked_Returns_True_For_Out_Of_Bounds_Tiles(int x, int y)
    {
        var vm = MakeVm();
        vm.RegionMapData = MakeRegionMap(10, 10);
        vm.IsBlocked(x, y).Should().BeTrue();
    }

    // ── IsBlocked — collision mask ────────────────────────────────────────────

    [Fact]
    public void IsBlocked_Returns_True_For_Solid_Tile()
    {
        var vm = MakeVm();
        vm.RegionMapData = MakeRegionMap(5, 5, [(2, 3)]);
        vm.IsBlocked(2, 3).Should().BeTrue();
    }

    [Fact]
    public void IsBlocked_Returns_False_For_Open_Tile()
    {
        var vm = MakeVm();
        vm.RegionMapData = MakeRegionMap(5, 5, [(2, 3)]);
        vm.IsBlocked(1, 1).Should().BeFalse();
    }

    [Fact]
    public void IsBlocked_Correctly_Indexes_Using_Row_Major_Order()
    {
        var vm = MakeVm();
        vm.RegionMapData = MakeRegionMap(4, 4, [(0, 1), (3, 0)]);
        vm.IsBlocked(0, 1).Should().BeTrue();
        vm.IsBlocked(3, 0).Should().BeTrue();
        vm.IsBlocked(1, 0).Should().BeFalse();
        vm.IsBlocked(0, 0).Should().BeFalse();
    }

    // ── CenterCameraOn ─────────────────────────────────────────────────────────

    [Fact]
    public void CenterCameraOn_Does_Nothing_When_No_Map_Loaded()
    {
        var vm = MakeVm();
        vm.CenterCameraOn(5, 5, 10, 8);
        vm.CameraX.Should().Be(0);
        vm.CameraY.Should().Be(0);
    }

    [Fact]
    public void CenterCameraOn_Clamps_To_Zero_When_Center_Near_Origin()
    {
        var vm = MakeVm();
        vm.RegionMapData = MakeRegionMap(40, 30);
        vm.CenterCameraOn(1, 1, 20, 15);
        vm.CameraX.Should().Be(0);
        vm.CameraY.Should().Be(0);
    }

    [Fact]
    public void CenterCameraOn_Calculates_Correct_Camera_Origin()
    {
        var vm = MakeVm();
        vm.RegionMapData = MakeRegionMap(40, 30);
        vm.CenterCameraOn(20, 15, 10, 8);
        vm.CameraX.Should().Be(15); // 20 - 10/2 = 15
        vm.CameraY.Should().Be(11); // 15 - 8/2  = 11
    }

    [Fact]
    public void CenterCameraOn_Clamps_To_Map_Boundary_When_Near_Edge()
    {
        var vm = MakeVm();
        vm.RegionMapData = MakeRegionMap(20, 15);
        vm.CenterCameraOn(19, 14, 10, 8);
        // max CameraX = 20 - 10 = 10; max CameraY = 15 - 8 = 7
        vm.CameraX.Should().Be(10);
        vm.CameraY.Should().Be(7);
    }

    // ── RevealAround ───────────────────────────────────────────────────────────

    [Fact]
    public void RevealAround_Adds_Tiles_Within_Radius()
    {
        var vm = MakeVm();
        vm.RevealAround(5, 5, radius: 1);
        vm.RevealedTiles.Should().Contain("5:5");
        vm.RevealedTiles.Should().Contain("4:5");
        vm.RevealedTiles.Should().Contain("6:5");
        vm.RevealedTiles.Should().Contain("5:4");
        vm.RevealedTiles.Should().Contain("5:6");
    }

    [Fact]
    public void RevealAround_Does_Not_Add_Corners_Outside_Circle_Radius()
    {
        var vm = MakeVm();
        vm.RevealAround(5, 5, radius: 1);
        // diagonals at distance √2 ≈ 1.41 > 1 — not added
        vm.RevealedTiles.Should().NotContain("4:4");
        vm.RevealedTiles.Should().NotContain("6:6");
    }

    [Fact]
    public void RevealAround_Accumulates_Across_Multiple_Calls()
    {
        var vm = MakeVm();
        vm.RevealAround(0, 0, radius: 0);
        vm.RevealAround(10, 10, radius: 0);
        vm.RevealedTiles.Should().Contain("0:0");
        vm.RevealedTiles.Should().Contain("10:10");
    }

    // ── UpsertEntity / RemoveEntity ────────────────────────────────────────────

    [Fact]
    public void UpsertEntity_Adds_New_Entity()
    {
        var id = Guid.NewGuid();
        var vm = MakeVm();
        vm.UpsertEntity(id, "player", "hero", 3, 4, "down");
        vm.Entities.Should().ContainSingle(e => e.EntityId == id && e.TileX == 3 && e.TileY == 4);
    }

    [Fact]
    public void UpsertEntity_Replaces_Existing_Entity_With_Updated_Position()
    {
        var id = Guid.NewGuid();
        var vm = MakeVm();
        vm.UpsertEntity(id, "player", "hero", 3, 4, "down");
        vm.UpsertEntity(id, "player", "hero", 5, 6, "right");
        vm.Entities.Should().ContainSingle(e => e.EntityId == id);
        vm.Entities.Single().TileX.Should().Be(5);
        vm.Entities.Single().TileY.Should().Be(6);
    }

    [Fact]
    public void RemoveEntity_Removes_Existing_Entity()
    {
        var id = Guid.NewGuid();
        var vm = MakeVm();
        vm.UpsertEntity(id, "player", "hero", 1, 1, "down");
        vm.RemoveEntity(id);
        vm.Entities.Should().BeEmpty();
    }

    [Fact]
    public void RemoveEntity_Is_Idempotent_For_Unknown_Id()
    {
        var vm = MakeVm();
        var act = () => vm.RemoveEntity(Guid.NewGuid());
        act.Should().NotThrow();
    }

    // ── SelfEntityId ───────────────────────────────────────────────────────────

    [Fact]
    public void SelfEntityId_Is_Null_Initially()
    {
        var vm = MakeVm();
        vm.SelfEntityId.Should().BeNull();
    }

    [Fact]
    public void SelfEntityId_Can_Be_Set()
    {
        var id = Guid.NewGuid();
        var vm = MakeVm();
        vm.SelfEntityId = id;
        vm.SelfEntityId.Should().Be(id);
    }

    // ── ToggleMiniMapCommand ───────────────────────────────────────────────────

    [Fact]
    public void ToggleMiniMapCommand_Toggles_IsMiniMapOpen()
    {
        var vm = MakeVm();
        vm.IsMiniMapOpen.Should().BeFalse();
        vm.ToggleMiniMapCommand.Execute().Subscribe();
        vm.IsMiniMapOpen.Should().BeTrue();
        vm.ToggleMiniMapCommand.Execute().Subscribe();
        vm.IsMiniMapOpen.Should().BeFalse();
    }
}
