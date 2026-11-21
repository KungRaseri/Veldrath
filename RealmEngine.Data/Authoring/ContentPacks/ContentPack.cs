using System.Text.Json.Nodes;
using RealmEngine.Data.Validation.Content;

namespace RealmEngine.Data.Authoring.ContentPacks;

/// <summary>
/// Rule ids the pack reader reports. Content rules live in <see cref="ContentRules"/>; these cover the file layout
/// and the pack envelope, which the entity validators never see.
/// </summary>
public static class ContentPackRules
{
    /// <summary>The pack directory does not exist.</summary>
    public const string DirectoryMissing = "pack-directory-missing";

    /// <summary>A file in the pack directory could not be read from disk.</summary>
    public const string FileUnreadable = "pack-file-unreadable";

    /// <summary>A file is not one YAML document, or its YAML could not be parsed.</summary>
    public const string FileInvalid = "pack-file-invalid";

    /// <summary>A file's shape is wrong: no <c>entities</c> list, or an entity that is not a mapping.</summary>
    public const string EnvelopeInvalid = "pack-envelope-invalid";

    /// <summary>A pack file sits outside a family directory, e.g. <c>content/items.yaml</c>.</summary>
    public const string FamilyDirectoryRequired = "pack-family-directory-required";

    /// <summary>The directory name is not a content type the engine stores.</summary>
    public const string FamilyUnknown = "pack-family-unknown";

    /// <summary>The file's <c>family:</c> header disagrees with the directory it sits in.</summary>
    public const string FamilyMismatch = "pack-family-mismatch";

    /// <summary>Two entities in the same family declare the same slug.</summary>
    public const string DuplicateSlug = "pack-duplicate-slug";

    /// <summary>The entity payload could not be bound to the entity the family maps to.</summary>
    public const string EntityUnreadable = "pack-entity-unreadable";

    /// <summary>A GUID foreign key written as a slug names content that does not exist.</summary>
    public const string ReferenceUnresolved = "pack-reference-unresolved";
}

/// <summary>
/// One entity inside a content-pack file.
/// </summary>
/// <param name="Slug">The entity slug as written, or an empty string when the payload has none.</param>
/// <param name="Payload">The entity payload, in the same JSON shape the content write endpoints accept.</param>
/// <param name="SourcePath">Repository-relative path of the file it came from, for error messages.</param>
/// <param name="Position">1-based position of the entity inside the file's <c>entities</c> list.</param>
public sealed record ContentPackEntry(
    string Slug,
    JsonObject Payload,
    string SourcePath,
    int Position)
{
    /// <summary>Gets a label addressing this entry in an issue message.</summary>
    /// <value>The slug when the payload has one, otherwise the file and position.</value>
    public string Label => string.IsNullOrWhiteSpace(Slug) ? $"{SourcePath}#{Position}" : Slug;
}

/// <summary>
/// One pack file: a single family, read from one YAML document.
/// </summary>
/// <param name="Family">
/// Content-type key, taken from the file's directory name. Empty when the file has no family at all — it sits in the
/// pack root, its directory is not a content type, or the file failed before a family could be read — in which case
/// the reader has already reported why and the file has no entries. Consumers must skip a file whose family does not
/// resolve rather than pass the empty string on.
/// </param>
/// <param name="CanonicalFamily">The family with aliases folded away, e.g. <c>weapon</c> → <c>item</c>; empty when <paramref name="Family"/> is.</param>
/// <param name="SourcePath">Repository-relative path of the file.</param>
/// <param name="Entries">The entities declared in the file, in file order.</param>
public sealed record ContentPackFile(
    string Family,
    string CanonicalFamily,
    string SourcePath,
    IReadOnlyList<ContentPackEntry> Entries);

/// <summary>
/// The outcome of reading a pack directory: what it declared and what was wrong with it.
/// </summary>
/// <param name="Files">Every file that could be read, including files that also carry issues.</param>
/// <param name="Issues">File-level findings: layout, family, duplicates and unparseable YAML.</param>
public sealed record ContentPackReadResult(
    IReadOnlyList<ContentPackFile> Files,
    IReadOnlyList<ContentIssue> Issues)
{
    /// <summary>Gets a value indicating whether reading found any error-severity issue.</summary>
    public bool HasErrors => Issues.Any(issue => issue.Severity == ContentIssueSeverity.Error);

    /// <summary>Gets every entity declared across every readable file, in file order.</summary>
    public IEnumerable<ContentPackEntry> Entries => Files.SelectMany(file => file.Entries);
}
