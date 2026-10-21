using RealmEngine.Data.Validation;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Import;

/// <summary>
/// Catalog metadata for a region or zone that owns a map graph.
/// </summary>
/// <param name="Id">Stable region or zone slug.</param>
/// <param name="Name">Human-readable name.</param>
/// <param name="Description">Optional description shown in the map header.</param>
/// <param name="ParentRegionId">Owning region slug for zone entries; <see langword="null"/> for regions.</param>
public sealed record MapCatalogEntry(
    string Id,
    string Name,
    string? Description = null,
    string? ParentRegionId = null);

/// <summary>
/// Flattened snapshot of a <c>ZoneLocation</c> row used by the TMX importer.
/// </summary>
/// <param name="Slug">Stable location slug.</param>
/// <param name="ZoneId">Zone that owns the location.</param>
/// <param name="DisplayName">Human-readable location name.</param>
/// <param name="TileX">Authored tile column, when known.</param>
/// <param name="TileY">Authored tile row, when known.</param>
/// <param name="IsHidden">Whether the location starts hidden.</param>
/// <param name="UnlockType">Discovery mechanism for hidden locations.</param>
/// <param name="UnlockKey">Quest, item, or achievement key required to reveal the location.</param>
/// <param name="DiscoverThreshold">Passive discovery level threshold.</param>
/// <param name="DominantLanguageSlug">Language predominantly spoken at the location.</param>
/// <param name="SortOrder">Stable ordering hint supplied by the caller.</param>
public sealed record ZoneLocationSnapshot(
    string Slug,
    string ZoneId,
    string DisplayName,
    int? TileX,
    int? TileY,
    bool IsHidden = false,
    string? UnlockType = null,
    string? UnlockKey = null,
    int? DiscoverThreshold = null,
    string? DominantLanguageSlug = null,
    int SortOrder = 0);

/// <summary>
/// Flattened snapshot of a directed <c>ZoneLocationConnection</c> row used by the TMX importer.
/// </summary>
/// <param name="FromLocationSlug">Origin location slug.</param>
/// <param name="ToLocationSlug">Destination location slug for walk connections.</param>
/// <param name="ToZoneId">Destination zone slug for cross-zone exits.</param>
/// <param name="ToRegionId">Destination region slug for region-map exits.</param>
/// <param name="ConnectionType">Authored connection type (<c>walk</c>, <c>exit</c>, or <c>region_exit</c>).</param>
/// <param name="IsTraversable">Whether the connection is currently usable.</param>
public sealed record ZoneConnectionSnapshot(
    string FromLocationSlug,
    string? ToLocationSlug = null,
    string? ToZoneId = null,
    string? ToRegionId = null,
    string ConnectionType = "walk",
    bool IsTraversable = true);

/// <summary>
/// Flattened snapshot of a directed <c>RegionConnection</c> row used by the TMX importer.
/// </summary>
/// <param name="FromRegionId">Origin region slug.</param>
/// <param name="ToRegionId">Destination region slug.</param>
public sealed record RegionConnectionSnapshot(string FromRegionId, string ToRegionId);

/// <summary>
/// All authored content required to convert TMX maps and location rows into map graphs.
/// </summary>
/// <param name="Regions">Region catalog entries.</param>
/// <param name="Zones">Zone catalog entries, including their owning region.</param>
/// <param name="Locations">Zone-location snapshots.</param>
/// <param name="ZoneConnections">Directed zone-location connections.</param>
/// <param name="RegionConnections">Directed region adjacencies.</param>
public sealed record MapGraphImportCatalog(
    IReadOnlyList<MapCatalogEntry> Regions,
    IReadOnlyList<MapCatalogEntry> Zones,
    IReadOnlyList<ZoneLocationSnapshot> Locations,
    IReadOnlyList<ZoneConnectionSnapshot> ZoneConnections,
    IReadOnlyList<RegionConnectionSnapshot> RegionConnections);

/// <summary>Severity of an issue found while importing a map graph.</summary>
public enum MapGraphImportIssueSeverity
{
    /// <summary>The import can proceed; the issue should be reviewed.</summary>
    Warning,

    /// <summary>The imported graph is incomplete or inconsistent until the issue is fixed.</summary>
    Error
}

/// <summary>
/// An issue discovered while converting authored content into a map graph.
/// </summary>
/// <param name="Code">Stable machine-readable issue code.</param>
/// <param name="Message">Human-readable description.</param>
/// <param name="MapId">Map involved in the issue, when applicable.</param>
/// <param name="NodeId">Node involved in the issue, when applicable.</param>
/// <param name="Severity">Whether the issue blocks a clean import.</param>
public sealed record MapGraphImportIssue(
    string Code,
    string Message,
    string? MapId = null,
    string? NodeId = null,
    MapGraphImportIssueSeverity Severity = MapGraphImportIssueSeverity.Error);

/// <summary>
/// Verification summary for a single imported map.
/// </summary>
/// <param name="MapId">Imported map identifier.</param>
/// <param name="MapType">Whether the map is a region or zone map.</param>
/// <param name="NodeCount">Number of imported nodes.</param>
/// <param name="EdgeCount">Number of imported edges.</param>
/// <param name="IsValid">Whether the map passed error-level validation.</param>
/// <param name="ValidationIssues">Issues reported by <see cref="MapGraphValidator"/>.</param>
public sealed record MapGraphImportMapReport(
    string MapId,
    MapType MapType,
    int NodeCount,
    int EdgeCount,
    bool IsValid,
    IReadOnlyList<MapGraphValidationIssue> ValidationIssues);

/// <summary>
/// Aggregate verification report for an import run.
/// </summary>
/// <param name="Maps">Per-map verification summaries.</param>
/// <param name="Issues">Import-level issues such as missing TMX input or unresolved targets.</param>
public sealed record MapGraphImportReport(
    IReadOnlyList<MapGraphImportMapReport> Maps,
    IReadOnlyList<MapGraphImportIssue> Issues)
{
    /// <summary>Gets the number of imported region maps.</summary>
    public int RegionMapCount => Maps.Count(m => m.MapType == MapType.Region);

    /// <summary>Gets the number of imported zone maps.</summary>
    public int ZoneMapCount => Maps.Count(m => m.MapType == MapType.Zone);

    /// <summary>Gets the total number of imported nodes.</summary>
    public int NodeCount => Maps.Sum(m => m.NodeCount);

    /// <summary>Gets the total number of imported edges.</summary>
    public int EdgeCount => Maps.Sum(m => m.EdgeCount);

    /// <summary>
    /// Gets a value indicating whether every map passed validation and no error-level
    /// import issues were reported. Warnings do not invalidate the import.
    /// </summary>
    public bool IsValid =>
        Issues.All(i => i.Severity != MapGraphImportIssueSeverity.Error) &&
        Maps.All(m => m.IsValid);
}

/// <summary>
/// Result of importing TMX maps and content rows into map graphs.
/// </summary>
/// <param name="Maps">Imported region and zone graphs.</param>
/// <param name="Report">Verification report for the run.</param>
public sealed record MapGraphImportResult(
    IReadOnlyList<MapGraph> Maps,
    MapGraphImportReport Report);
