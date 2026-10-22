using System.Text.Json;
using System.Text.Json.Serialization;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Authoring;

/// <summary>
/// Root document used for bulk map-graph authoring. The file is intentionally
/// independent of the database and can be committed, diffed, and reviewed.
/// </summary>
/// <param name="FormatVersion">Schema version of the authoring file. Currently <c>1</c>.</param>
/// <param name="ExportedAt">UTC timestamp when the file was produced.</param>
/// <param name="Maps">All map graphs contained in the file.</param>
public sealed record MapGraphFile(
    int FormatVersion,
    DateTimeOffset ExportedAt,
    IReadOnlyList<MapGraph> Maps)
{
    /// <summary>The only supported authoring-file schema version.</summary>
    public const int CurrentFormatVersion = 1;
}

/// <summary>
/// Serializes and deserializes <see cref="MapGraph"/> authoring files as JSON.
/// Maps, nodes, and edges are ordered deterministically so exported files diff cleanly.
/// </summary>
public static class MapGraphJsonSerializer
{
    /// <summary>Options used for both import and export.</summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Exports one or more map graphs as a JSON document.</summary>
    /// <param name="maps">The maps to export.</param>
    /// <returns>Indented JSON using camelCase property names and string enums.</returns>
    public static string Export(IEnumerable<MapGraph> maps)
    {
        ArgumentNullException.ThrowIfNull(maps);

        var document = new MapGraphFile(
            MapGraphFile.CurrentFormatVersion,
            DateTimeOffset.UtcNow,
            [.. maps.Select(Normalize).OrderBy(m => m.Id, StringComparer.Ordinal)]);

        return JsonSerializer.Serialize(document, Options);
    }

    /// <summary>Imports a map-graph authoring document from JSON.</summary>
    /// <param name="json">The JSON document to parse.</param>
    /// <returns>The parsed document with validated map identifiers.</returns>
    /// <exception cref="InvalidDataException">
    /// Thrown when the document is empty, uses an unsupported format version,
    /// or contains duplicate map identifiers.
    /// </exception>
    public static MapGraphFile Import(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidDataException("Map graph JSON is empty.");

        MapGraphFile? document;
        try
        {
            document = JsonSerializer.Deserialize<MapGraphFile>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Map graph JSON is not valid: {ex.Message}", ex);
        }

        if (document is null || document.Maps is null)
            throw new InvalidDataException("Map graph JSON does not contain a maps array.");

        if (document.FormatVersion != MapGraphFile.CurrentFormatVersion)
        {
            throw new InvalidDataException(
                $"Unsupported map graph format version {document.FormatVersion}. " +
                $"Expected {MapGraphFile.CurrentFormatVersion}.");
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var map in document.Maps)
        {
            if (string.IsNullOrWhiteSpace(map.Id))
                throw new InvalidDataException("A map in the document has no id.");

            if (!seen.Add(map.Id))
                throw new InvalidDataException($"Duplicate map id '{map.Id}' in the document.");
        }

        return document with
        {
            Maps = [.. document.Maps.Select(Normalize).OrderBy(m => m.Id, StringComparer.Ordinal)],
        };
    }

    /// <summary>Exports map graphs to a UTF-8 JSON file, creating parent directories as needed.</summary>
    /// <param name="path">Destination file path.</param>
    /// <param name="maps">The maps to export.</param>
    public static void ExportToFile(string path, IEnumerable<MapGraph> maps)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, Export(maps));
    }

    /// <summary>Imports map graphs from a UTF-8 JSON file.</summary>
    /// <param name="path">Source file path.</param>
    /// <returns>The parsed document.</returns>
    public static MapGraphFile ImportFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
            throw new FileNotFoundException("Map graph JSON file was not found.", path);

        return Import(File.ReadAllText(path));
    }

    private static MapGraph Normalize(MapGraph map) => map with
    {
        Nodes = [.. map.Nodes.OrderBy(n => n.SortOrder).ThenBy(n => n.Id, StringComparer.Ordinal)],
        Edges = [.. map.Edges.OrderBy(e => e.Id)],
    };
}
