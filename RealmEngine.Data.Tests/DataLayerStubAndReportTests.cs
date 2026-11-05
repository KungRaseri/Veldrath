using RealmEngine.Data.Import;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Repositories;
using RealmEngine.Data.Validation;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Tests;

/// <summary>
/// Value and contract tests for the pieces the data layer exposes without a database:
/// the empty stub repository, the persistence-mode options object, and the map-graph
/// import report/record shapes the TMX importer publishes.
/// </summary>
[Trait("Category", "Unit")]
public class DataLayerStubAndReportTests
{
    // ── InMemoryZoneLocationRepository ───────────────────────────────────────

    [Fact]
    public async Task ZoneLocationStub_ReturnsNoRowsForEveryQuery()
    {
        // Arrange
        var repository = new InMemoryZoneLocationRepository();

        // Act & Assert
        (await repository.GetAllAsync()).Should().BeEmpty();
        (await repository.GetBySlugAsync("grey-cup")).Should().BeNull();
        (await repository.GetByTypeKeyAsync("dungeons")).Should().BeEmpty();
        (await repository.GetByZoneIdAsync("crestfall")).Should().BeEmpty();
        (await repository.GetByZoneIdAsync("crestfall", ["grey-cup"])).Should().BeEmpty();
        (await repository.GetHiddenByZoneIdAsync("crestfall")).Should().BeEmpty();
        (await repository.GetAllByZoneIdAsync("crestfall")).Should().BeEmpty();
    }

    // ── PersistenceOptions ──────────────────────────────────────────────────

    [Fact]
    public void PersistenceOptions_DefaultsToInMemory()
    {
        // Act
        var options = new PersistenceOptions();

        // Assert
        options.IsInMemory.Should().BeTrue();
        options.IsNpgsql.Should().BeFalse();
        options.IsSqlite.Should().BeFalse();
        options.IsExternal.Should().BeFalse();
        options.ConnectionString.Should().BeEmpty();
    }

    [Fact]
    public void UseNpgsql_SwitchesToPostgresAndKeepsTheConnectionString()
    {
        // Arrange
        var options = new PersistenceOptions();

        // Act
        options.UseNpgsql("Host=localhost;Database=realm");

        // Assert
        options.IsNpgsql.Should().BeTrue();
        options.IsInMemory.Should().BeFalse();
        options.ConnectionString.Should().Be("Host=localhost;Database=realm");
    }

    [Fact]
    public void UseSqlite_DefaultsToTheLocalSaveFile()
    {
        // Arrange
        var options = new PersistenceOptions();

        // Act
        options.UseSqlite();

        // Assert
        options.IsSqlite.Should().BeTrue();
        options.ConnectionString.Should().Be("Data Source=savegames.db");
    }

    [Fact]
    public void UseSqlite_AcceptsAnExplicitConnectionString()
    {
        // Arrange
        var options = new PersistenceOptions();

        // Act
        options.UseSqlite("Data Source=:memory:");

        // Assert
        options.IsSqlite.Should().BeTrue();
        options.ConnectionString.Should().Be("Data Source=:memory:");
    }

    [Fact]
    public void UseExternal_MarksTheHostAsOwningPersistence()
    {
        // Arrange
        var options = new PersistenceOptions();

        // Act
        options.UseExternal();

        // Assert
        options.IsExternal.Should().BeTrue();
        options.IsInMemory.Should().BeFalse();
    }

    [Fact]
    public void UseInMemory_RestoresTheDefaultAfterAnotherMode()
    {
        // Arrange
        var options = new PersistenceOptions();
        options.UseNpgsql("Host=localhost");

        // Act
        options.UseInMemory();

        // Assert
        options.IsInMemory.Should().BeTrue();
        options.IsNpgsql.Should().BeFalse();
        // the connection string is retained for diagnostics
        options.ConnectionString.Should().Be("Host=localhost");
    }

    // ── Import models ───────────────────────────────────────────────────────

    [Fact]
    public void MapCatalogEntry_DefaultsTheOptionalDescriptions()
    {
        // Act
        var entry = new MapCatalogEntry("varenmark", "Varenmark");

        // Assert
        entry.Description.Should().BeNull();
        entry.ParentRegionId.Should().BeNull();
    }

    [Fact]
    public void MapCatalogEntry_IsAValueRecord()
    {
        // Arrange
        var left = new MapCatalogEntry("crestfall", "Crestfall", "A town", "varenmark");
        var right = new MapCatalogEntry("crestfall", "Crestfall", "A town", "varenmark");

        // Act & Assert
        left.Should().Be(right);
        (left with { ParentRegionId = null }).ParentRegionId.Should().BeNull();
    }

    [Fact]
    public void ZoneLocationSnapshot_DefaultsToVisibleAtTheStartOfTheOrder()
    {
        // Act
        var snapshot = new ZoneLocationSnapshot("grey-cup", "crestfall", "Grey Cup", 4, 7);

        // Assert
        snapshot.IsHidden.Should().BeFalse();
        snapshot.UnlockType.Should().BeNull();
        snapshot.UnlockKey.Should().BeNull();
        snapshot.DiscoverThreshold.Should().BeNull();
        snapshot.DominantLanguageSlug.Should().BeNull();
        snapshot.SortOrder.Should().Be(0);
    }

    [Fact]
    public void ZoneConnectionSnapshot_DefaultsToAWalkableWalkConnection()
    {
        // Act
        var connection = new ZoneConnectionSnapshot("grey-cup", ToLocationSlug: "square");

        // Assert
        connection.ConnectionType.Should().Be("walk");
        connection.IsTraversable.Should().BeTrue();
        connection.ToZoneId.Should().BeNull();
        connection.ToRegionId.Should().BeNull();
    }

    [Fact]
    public void RegionConnectionSnapshot_PairsTheTwoRegions()
    {
        // Act
        var connection = new RegionConnectionSnapshot("varenmark", "solenne");

        // Assert
        connection.FromRegionId.Should().Be("varenmark");
        connection.ToRegionId.Should().Be("solenne");
    }

    [Fact]
    public void MapGraphImportIssue_DefaultsToErrorSeverity()
    {
        // Act
        var issue = new MapGraphImportIssue("tmx_missing", "No TMX file was found.");

        // Assert
        issue.Severity.Should().Be(MapGraphImportIssueSeverity.Error);
        issue.MapId.Should().BeNull();
        issue.NodeId.Should().BeNull();
    }

    [Fact]
    public void MapGraphImportCatalog_CarriesEverySnapshotCollection()
    {
        // Arrange
        IReadOnlyList<MapCatalogEntry> regions = [new("varenmark", "Varenmark")];
        IReadOnlyList<ZoneLocationSnapshot> locations = [new("grey-cup", "crestfall", "Grey Cup", null, null)];

        // Act
        var catalog = new MapGraphImportCatalog(regions, [], locations, [], []);

        // Assert
        catalog.Regions.Should().BeSameAs(regions);
        catalog.Zones.Should().BeEmpty();
        catalog.Locations.Should().BeSameAs(locations);
        catalog.ZoneConnections.Should().BeEmpty();
        catalog.RegionConnections.Should().BeEmpty();
    }

    // ── Import report aggregates ────────────────────────────────────────────

    private static MapGraphImportMapReport MapReport(
        string mapId,
        MapType mapType,
        int nodeCount,
        int edgeCount,
        bool isValid = true) =>
        new(mapId, mapType, nodeCount, edgeCount, isValid, []);

    [Fact]
    public void ImportReport_CountsRegionAndZoneMapsSeparately()
    {
        // Arrange
        var report = new MapGraphImportReport(
            [
                MapReport("region:varenmark", MapType.Region, 4, 3),
                MapReport("zone:crestfall", MapType.Zone, 6, 5),
                MapReport("zone:droveway", MapType.Zone, 2, 1),
            ],
            []);

        // Act & Assert
        report.RegionMapCount.Should().Be(1);
        report.ZoneMapCount.Should().Be(2);
    }

    [Fact]
    public void ImportReport_SumsNodesAndEdgesAcrossEveryMap()
    {
        // Arrange
        var report = new MapGraphImportReport(
            [
                MapReport("zone:a", MapType.Zone, 6, 5),
                MapReport("zone:b", MapType.Zone, 2, 1),
            ],
            []);

        // Act & Assert
        report.NodeCount.Should().Be(8);
        report.EdgeCount.Should().Be(6);
    }

    [Fact]
    public void ImportReport_WithNoMaps_ReportsZeroTotals()
    {
        // Act
        var report = new MapGraphImportReport([], []);

        // Assert
        report.RegionMapCount.Should().Be(0);
        report.ZoneMapCount.Should().Be(0);
        report.NodeCount.Should().Be(0);
        report.EdgeCount.Should().Be(0);
        report.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ImportReport_IsInvalidWhenAMapFailedValidation()
    {
        // Arrange
        var report = new MapGraphImportReport([MapReport("zone:a", MapType.Zone, 1, 0, isValid: false)], []);

        // Act & Assert
        report.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ImportReport_IsInvalidWhenAnErrorIssueWasRaised()
    {
        // Arrange
        var report = new MapGraphImportReport(
            [MapReport("zone:a", MapType.Zone, 1, 0)],
            [new MapGraphImportIssue("tmx_missing", "No TMX file was found.")]);

        // Act & Assert
        report.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ImportReport_StaysValidWithWarningsOnly()
    {
        // Arrange
        var report = new MapGraphImportReport(
            [MapReport("zone:a", MapType.Zone, 1, 0)],
            [new MapGraphImportIssue("node_orphan", "Unused node.", Severity: MapGraphImportIssueSeverity.Warning)]);

        // Act & Assert
        report.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ImportResult_PairsTheGraphsWithTheReport()
    {
        // Arrange
        var report = new MapGraphImportReport([], []);

        // Act
        var result = new MapGraphImportResult([], report);

        // Assert
        result.Maps.Should().BeEmpty();
        result.Report.Should().BeSameAs(report);
    }
}
