using System.Reactive.Threading.Tasks;
using Veldrath.Client.Services;
using Veldrath.Client.Tests.Infrastructure;
using Veldrath.Client.ViewModels;
using Veldrath.Contracts.Tilemap;

namespace Veldrath.Client.Tests.ViewModels;

/// <summary>Unit tests for region-map hub handler methods on <see cref="GameViewModel"/>.</summary>
public class GameViewModelRegionTests : TestBase
{
    private static readonly Guid CharId = Guid.NewGuid();

    private static GameViewModel MakeVm(Guid? characterId = null)
    {
        var vm = new GameViewModel(
            new FakeServerConnectionService(),
            new FakeZoneService(),
            new TokenStore(),
            new FakeNavigationService());

        if (characterId.HasValue)
        {
            vm.SeedInitialStats(new SeedInitialStatsArgs(
                Level: 1, Experience: 0,
                CurrentHealth: 100, MaxHealth: 100,
                CurrentMana: 50, MaxMana: 50,
                Gold: 0, UnspentAttributePoints: 0,
                CharacterId: characterId));
        }

        return vm;
    }

    private static RegionMapDto MakeRegionMap() =>
        new(RegionId:      "thornveil",
            TilesetKey:    "overworld",
            Width:         40,
            Height:        30,
            TileSize:      16,
            Layers:        [],
            CollisionMask: new bool[40 * 30],
            ZoneEntries:   [],
            RegionExits:   [],
            Labels:        [],
            Paths:         []);

    // ── Construction ───────────────────────────────────────────────────────────

    [Fact]
    public void RegionTilemap_Is_Not_Null_After_Construction()
    {
        var vm = MakeVm();
        vm.RegionTilemap.Should().NotBeNull();
    }

    [Fact]
    public void PendingZoneEntry_Is_Null_Initially()
    {
        var vm = MakeVm();
        vm.PendingZoneEntry.Should().BeNull();
    }

    [Fact]
    public void PendingRegionExit_Is_Null_Initially()
    {
        var vm = MakeVm();
        vm.PendingRegionExit.Should().BeNull();
    }

    // ── SeedInitialStats sets RegionTilemap.SelfEntityId ─────────────────────

    [Fact]
    public void SeedInitialStats_Sets_RegionTilemap_SelfEntityId()
    {
        var vm = MakeVm(characterId: CharId);
        vm.RegionTilemap!.SelfEntityId.Should().Be(CharId);
    }

    // ── OnRegionMapReceived ────────────────────────────────────────────────────

    [Fact]
    public void OnRegionMapReceived_Sets_RegionTilemap_MapData()
    {
        var vm  = MakeVm();
        var map = MakeRegionMap();
        vm.OnRegionMapReceived(map);
        vm.RegionTilemap!.RegionMapData.Should().Be(map);
    }

    [Fact]
    public void OnRegionMapReceived_Clears_RevealedTiles()
    {
        var vm = MakeVm();
        vm.RegionTilemap!.RevealedTiles.Add("1:1");
        vm.OnRegionMapReceived(MakeRegionMap());
        vm.RegionTilemap.RevealedTiles.Should().BeEmpty();
    }

    // ── OnRegionPlayerMoved ────────────────────────────────────────────────────

    [Fact]
    public void OnRegionPlayerMoved_Upserts_Entity_On_Region_Map()
    {
        var vm    = MakeVm();
        var otherId = Guid.NewGuid();
        vm.OnRegionPlayerMoved(otherId, 5, 7, "right");
        vm.RegionTilemap!.Entities.Should().ContainSingle(e =>
            e.EntityId == otherId && e.TileX == 5 && e.TileY == 7);
    }

    [Fact]
    public void OnRegionPlayerMoved_Centers_Camera_When_Self_Moves()
    {
        var vm = MakeVm(characterId: CharId);
        vm.OnRegionMapReceived(MakeRegionMap()); // need map loaded so camera clamp works
        vm.OnRegionPlayerMoved(CharId, 20, 15, "right");
        // After centering on (20, 15) with default viewport 26×17, camera should shift from (0,0)
        vm.RegionTilemap!.CameraX.Should().BeGreaterThan(0);
    }

    [Fact]
    public void OnRegionPlayerMoved_Reveals_Around_Self_When_Self_Moves()
    {
        var vm = MakeVm(characterId: CharId);
        vm.OnRegionPlayerMoved(CharId, 10, 10, "down");
        vm.RegionTilemap!.RevealedTiles.Should().Contain("10:10");
    }

    [Fact]
    public void OnRegionPlayerMoved_Does_Not_Reveal_Around_Other_Players()
    {
        var vm = MakeVm(characterId: CharId);
        vm.OnRegionPlayerMoved(Guid.NewGuid(), 10, 10, "down");
        vm.RegionTilemap!.RevealedTiles.Should().BeEmpty();
    }

    // ── OnZoneEntryTriggered ───────────────────────────────────────────────────

    [Fact]
    public void OnZoneEntryTriggered_Sets_PendingZoneEntry()
    {
        var vm    = MakeVm();
        var entry = new ZoneObjectDto(5, 8, "fenwick-crossing", "Fenwick Crossing", 1, 5);
        vm.OnZoneEntryTriggered(entry);
        vm.PendingZoneEntry.Should().Be(entry);
    }

    [Fact]
    public void OnZoneEntryTriggered_Clears_PendingRegionExit()
    {
        var vm   = MakeVm();
        var exit = new RegionExitDto(29, 10, "varenmark");
        vm.OnRegionExitTriggered(exit);
        vm.OnZoneEntryTriggered(new ZoneObjectDto(5, 8, "fenwick-crossing", "Fenwick Crossing", 1, 5));
        vm.PendingRegionExit.Should().BeNull();
    }

    // ── OnRegionExitTriggered ──────────────────────────────────────────────────

    [Fact]
    public void OnRegionExitTriggered_Sets_PendingRegionExit()
    {
        var vm   = MakeVm();
        var exit = new RegionExitDto(29, 10, "varenmark");
        vm.OnRegionExitTriggered(exit);
        vm.PendingRegionExit.Should().Be(exit);
    }

    [Fact]
    public void OnRegionExitTriggered_Clears_PendingZoneEntry()
    {
        var vm    = MakeVm();
        var entry = new ZoneObjectDto(5, 8, "fenwick-crossing", "Fenwick Crossing", 1, 5);
        vm.OnZoneEntryTriggered(entry);
        vm.OnRegionExitTriggered(new RegionExitDto(29, 10, "varenmark"));
        vm.PendingZoneEntry.Should().BeNull();
    }

    // ── OnZoneExited ───────────────────────────────────────────────────────────

    [Fact]
    public void OnZoneExited_Clears_PendingZoneEntry_And_PendingRegionExit()
    {
        var vm = MakeVm(characterId: CharId);
        // Seed both pending values via their handler methods
        vm.OnZoneEntryTriggered(new ZoneObjectDto(5, 8, "fenwick-crossing", "Fenwick Crossing", 1, 5));
        vm.OnRegionExitTriggered(new RegionExitDto(29, 10, "varenmark"));
        // OnRegionExitTriggered clears PendingZoneEntry — re-seed it
        vm.OnZoneEntryTriggered(new ZoneObjectDto(5, 8, "fenwick-crossing", "Fenwick Crossing", 1, 5));

        vm.OnZoneExited("thornveil", 5, 8);

        vm.PendingZoneEntry.Should().BeNull();
        vm.PendingRegionExit.Should().BeNull();
    }

    [Fact]
    public void OnZoneExited_Places_Self_Entity_At_Spawn_Tile()
    {
        var vm = MakeVm(characterId: CharId);
        vm.OnZoneExited("thornveil", 5, 8);
        vm.RegionTilemap!.Entities.Should().ContainSingle(e =>
            e.EntityId == CharId && e.TileX == 5 && e.TileY == 8);
    }

    [Fact]
    public void OnZoneExited_Reveals_Around_Spawn_Tile()
    {
        var vm = MakeVm(characterId: CharId);
        vm.OnZoneExited("thornveil", 5, 8);
        vm.RegionTilemap!.RevealedTiles.Should().Contain("5:8");
    }

    [Fact]
    public void OnZoneExited_Does_Not_Throw_When_No_Character_Id_Set()
    {
        var vm  = MakeVm(); // no characterId
        var act = () => vm.OnZoneExited("thornveil", 1, 1);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task ConfirmZoneEntry_After_OnZoneExited_Sends_EnterZone_For_Same_Zone()
    {
        // Re-entering the zone you just exited was silently blocked because
        // _currentZoneId was never cleared on zone exit.
        var conn = new FakeServerConnectionService();
        var vm   = new GameViewModel(conn, new FakeZoneService(), new TokenStore(), new FakeNavigationService());

        // Simulate the character having been inside "crestfall" (sets _currentZoneId)
        await vm.InitializeAsync("TestHero", "crestfall");

        // Player exits Crestfall back to the region map
        vm.OnZoneExited("varenmark", 5, 8);

        // Player steps back onto the Crestfall zone-entry tile and confirms
        conn.SentCommands.Clear();
        vm.OnZoneEntryTriggered(new ZoneObjectDto(5, 8, "crestfall", "Crestfall", 1, 10));
        await vm.ConfirmZoneEntryCommand.Execute().ToTask();

        conn.SentCommands.Should().Contain(c => c.Method == "EnterZone");
    }

    // ── OnRegionChanged ────────────────────────────────────────────────────────

    [Fact]
    public void OnRegionChanged_Clears_PendingRegionExit()
    {
        var vm   = MakeVm();
        var exit = new RegionExitDto(29, 10, "varenmark");
        vm.OnRegionExitTriggered(exit);
        vm.OnRegionChanged("varenmark", 0, 5);
        vm.PendingRegionExit.Should().BeNull();
    }

    [Fact]
    public void OnRegionChanged_Clears_PendingZoneEntry()
    {
        var vm    = MakeVm();
        var entry = new ZoneObjectDto(5, 8, "fenwick-crossing", "Fenwick Crossing", 1, 5);
        vm.OnZoneEntryTriggered(entry);
        vm.OnRegionChanged("varenmark", 0, 5);
        vm.PendingZoneEntry.Should().BeNull();
    }

    [Fact]
    public void OnRegionChanged_Places_Self_Entity_At_New_Tile()
    {
        var vm = MakeVm(characterId: CharId);
        vm.OnRegionChanged("varenmark", 3, 7);
        vm.RegionTilemap!.Entities.Should().ContainSingle(e =>
            e.EntityId == CharId && e.TileX == 3 && e.TileY == 7);
    }

    [Fact]
    public void OnRegionChanged_Reveals_Around_New_Tile()
    {
        var vm = MakeVm(characterId: CharId);
        vm.OnRegionChanged("varenmark", 3, 7);
        vm.RegionTilemap!.RevealedTiles.Should().Contain("3:7");
    }

    [Fact]
    public void OnRegionChanged_Does_Not_Throw_Without_CharacterId()
    {
        var vm  = MakeVm();
        var act = () => vm.OnRegionChanged("varenmark", 0, 0);
        act.Should().NotThrow();
    }

    [Fact]
    public void OnRegionChanged_Appends_Log_Entry()
    {
        var vm = MakeVm();
        vm.OnRegionChanged("varenmark", 2, 4);
        vm.ActionLog.Should().ContainSingle(msg => msg.Contains("varenmark"));
    }
}
