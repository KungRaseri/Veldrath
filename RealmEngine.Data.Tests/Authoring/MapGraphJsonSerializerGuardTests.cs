using RealmEngine.Data.Authoring;

namespace RealmEngine.Data.Tests.Authoring;

/// <summary>
/// Tests for the remaining <see cref="MapGraphJsonSerializer"/> import guards: a document without a maps array,
/// a map without an id, and a missing file on disk. A bad map file must fail loudly, never load half a world.
/// </summary>
[Trait("Category", "Authoring")]
public class MapGraphJsonSerializerGuardTests
{
    [Fact]
    public void Import_RejectsADocumentWithoutAMapsArray()
    {
        var act = () => MapGraphJsonSerializer.Import("""{"formatVersion":1}""");

        act.Should().Throw<InvalidDataException>().WithMessage("*maps array*");
    }

    [Fact]
    public void Import_RejectsAMapWithoutAnId()
    {
        var json = """
            {
              "formatVersion": 1,
              "maps": [
                { "id": "", "name": "Nameless", "nodes": [], "edges": [] }
              ]
            }
            """;

        var act = () => MapGraphJsonSerializer.Import(json);

        act.Should().Throw<InvalidDataException>().WithMessage("*no id*");
    }

    [Fact]
    public void ImportFromFile_ThrowsWhenTheFileIsMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"missing-map-graph-{Guid.NewGuid():N}.json");

        var act = () => MapGraphJsonSerializer.ImportFromFile(path);

        act.Should().Throw<FileNotFoundException>().WithMessage("*map graph*");
    }

    [Fact]
    public void ImportFromFile_ReadsTheExportedDocument()
    {
        var path = Path.Combine(Path.GetTempPath(), $"map-graph-{Guid.NewGuid():N}.json");
        try
        {
            MapGraphJsonSerializer.ExportToFile(path, []);

            var document = MapGraphJsonSerializer.ImportFromFile(path);

            document.Maps.Should().BeEmpty();
            document.FormatVersion.Should().Be(MapGraphFile.CurrentFormatVersion);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
