using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Data.Repositories;

namespace RealmEngine.Data.Tests.Repositories;

/// <summary>
/// Parses TMX content to verify <see cref="TiledFileMapRepository"/> assembles
/// tile data correctly for both finite and infinite (chunked) maps.
/// </summary>
[Trait("Category", "Repository")]
public class TiledFileMapRepositoryTests
{
    private static TiledFileMapRepository CreateRepo(string dir) =>
        new(dir, NullLogger<TiledFileMapRepository>.Instance);

    // Builds a minimal two-chunk TMX (2×1 map, each chunk is 1×1).
    // Chunk at (0,0) has GID 1 and chunk at (1,0) has GID 2.
    // Expected flat data: [0, 1]  →  (index 0 = tile (0,0), index 1 = tile (1,0))
    private const string TwoChunkTmx = """
<?xml version="1.0" encoding="UTF-8"?>
<map version="1.10" tiledversion="1.12.1" orientation="orthogonal" renderorder="right-down"
     width="2" height="1" tilewidth="16" tileheight="16" infinite="1"
     nextlayerid="3" nextobjectid="1">
 <tileset firstgid="1" source="sheets/dummy.tsx"/>
 <layer id="1" name="ground" width="2" height="1">
  <data encoding="csv">
   <chunk x="0" y="0" width="1" height="1">1</chunk>
   <chunk x="1" y="0" width="1" height="1">2</chunk>
  </data>
 </layer>
</map>
""";

    // 3×2 map split into four 2×1 chunks (right column is narrower and padded).
    // Row 0: chunk(0,0) = [10, 20],  chunk(2,0) = [30]
    // Row 1: chunk(0,1) = [40, 50],  chunk(2,1) = [60]
    // Expected flat (row-major, width=3):
    //   [9, 19, 29, 39, 49, 59]  (GID - firstGid = index, but data stores raw GIDs here)
    private const string FourChunkTmx = """
<?xml version="1.0" encoding="UTF-8"?>
<map version="1.10" tiledversion="1.12.1" orientation="orthogonal" renderorder="right-down"
     width="3" height="2" tilewidth="16" tileheight="16" infinite="1"
     nextlayerid="3" nextobjectid="1">
 <tileset firstgid="1" source="sheets/dummy.tsx"/>
 <layer id="1" name="ground" width="3" height="2">
  <data encoding="csv">
   <chunk x="0" y="0" width="2" height="1">10,20</chunk>
   <chunk x="2" y="0" width="1" height="1">30</chunk>
   <chunk x="0" y="1" width="2" height="1">40,50</chunk>
   <chunk x="2" y="1" width="1" height="1">60</chunk>
  </data>
 </layer>
</map>
""";

    [Fact]
    public async Task GetByZoneIdAsync_TwoAdjacentChunks_ProducesCorrectRowMajorData()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "test-zone.tmx"), TwoChunkTmx);
            var repo = CreateRepo(dir);

            var map = await repo.GetByZoneIdAsync("test-zone");

            map.Should().NotBeNull();
            var layer = map!.Layers.Single(l => l.Type == "tilelayer");
            layer.Data.Should().NotBeNull().And.HaveCount(2); // 2×1

            // Tile (0,0) → flat index 0 → raw GID 1
            layer.Data![0].Should().Be(1, "tile at (0,0) should be GID 1 from chunk(0,0)");
            // Tile (1,0) → flat index 1 → raw GID 2
            layer.Data![1].Should().Be(2, "tile at (1,0) should be GID 2 from chunk(1,0)");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task GetByZoneIdAsync_FourChunks_PlacesTilesAtCorrectRowMajorPositions()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "multi-chunk.tmx"), FourChunkTmx);
            var repo = CreateRepo(dir);

            var map = await repo.GetByZoneIdAsync("multi-chunk");

            map.Should().NotBeNull();
            var layer = map!.Layers.Single(l => l.Type == "tilelayer");
            layer.Data.Should().NotBeNull().And.HaveCount(6); // 3×2

            // Row 0
            layer.Data![0].Should().Be(10, "tile (0,0)");
            layer.Data![1].Should().Be(20, "tile (1,0)");
            layer.Data![2].Should().Be(30, "tile (2,0)");
            // Row 1
            layer.Data![3].Should().Be(40, "tile (0,1)");
            layer.Data![4].Should().Be(50, "tile (1,1)");
            layer.Data![5].Should().Be(60, "tile (2,1)");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task GetByZoneIdAsync_FiniteMap_ParsesCsvDataDirectly()
    {
        var tmx = """
<?xml version="1.0" encoding="UTF-8"?>
<map version="1.10" tiledversion="1.12.1" orientation="orthogonal" renderorder="right-down"
     width="2" height="2" tilewidth="16" tileheight="16" infinite="0"
     nextlayerid="3" nextobjectid="1">
 <tileset firstgid="1" source="sheets/dummy.tsx"/>
 <layer id="1" name="ground" width="2" height="2">
  <data encoding="csv">7,64,
579,7
  </data>
 </layer>
</map>
""";

        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "finite.tmx"), tmx);
            var repo = CreateRepo(dir);

            var map = await repo.GetByZoneIdAsync("finite");

            map.Should().NotBeNull();
            var layer = map!.Layers.Single(l => l.Type == "tilelayer");
            layer.Data.Should().Equal([7, 64, 579, 7]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
