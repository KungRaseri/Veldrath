using System.Text.Json.Nodes;
using RealmEngine.Data.Content;
using RealmEngine.Data.Validation.Content;

namespace RealmEngine.Data.Authoring.ContentPacks;

/// <summary>
/// Reads a content-pack directory into entity payloads.
/// </summary>
/// <remarks>
/// The convention is one family per file, named after the family directory:
/// <c>content/&lt;family&gt;/&lt;zone-or-batch&gt;.yaml</c>. The directory name is the family, because that is the one
/// fact a reader can check for every file without opening it; a <c>family:</c> header is allowed and must agree, so a
/// file that gets moved between directories fails loudly instead of being imported into the wrong table.
/// <para>
/// Reading never touches a database and never binds an entity: it produces payloads plus file-level issues, which
/// <see cref="ContentPackValidator"/> then runs through the same registry the write path uses.
/// </para>
/// </remarks>
public static class ContentPackReader
{
    /// <summary>The YAML file extensions a pack directory accepts.</summary>
    private static readonly string[] _extensions = [".yaml", ".yml"];

    /// <summary>
    /// Reads every YAML file under <paramref name="directory"/>.
    /// </summary>
    /// <param name="directory">
    /// Pack root, normally the repository's <c>content/</c>. Family directories live directly beneath it.
    /// </param>
    /// <returns>The files that could be read, and every layout problem found.</returns>
    public static ContentPackReadResult Read(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var issues = new List<ContentIssue>();
        var files = new List<ContentPackFile>();

        if (!Directory.Exists(directory))
        {
            issues.Add(new ContentIssue(
                ContentPackRules.DirectoryMissing,
                $"The pack directory '{directory}' does not exist.",
                ContentIssueSeverity.Error,
                Field: directory));

            return new ContentPackReadResult(files, issues);
        }

        var paths = Directory
            .EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(path => _extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToList();

        var seen = new Dictionary<string, ContentPackEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            var file = ReadFile(directory, path, issues);

            foreach (var entry in file.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Slug))
                {
                    // The entity validator reports a missing slug; there is nothing to de-duplicate yet.
                    continue;
                }

                var key = $"{file.CanonicalFamily}:{entry.Slug}";
                if (seen.TryAdd(key, entry))
                {
                    continue;
                }

                var first = seen[key];
                issues.Add(new ContentIssue(
                    ContentPackRules.DuplicateSlug,
                    $"{file.Family} slug '{entry.Slug}' is declared twice: {first.SourcePath}#{first.Position} and {entry.SourcePath}#{entry.Position}. A slug is the identity of one row.",
                    ContentIssueSeverity.Error,
                    Field: "slug",
                    Identifier: entry.Slug));
            }

            files.Add(file);
        }

        return new ContentPackReadResult(files, issues);
    }

    private static ContentPackFile ReadFile(string root, string path, List<ContentIssue> issues)
    {
        var relative = Path.GetRelativePath(root, path)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace('\\', '/');
        var sourcePath = $"{root.Replace(Path.DirectorySeparatorChar, '/').TrimEnd('/')}/{relative}";

        var segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string? directoryFamily = segments.Length > 1 ? segments[0] : null;

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            issues.Add(new ContentIssue(
                ContentPackRules.FileUnreadable,
                $"'{sourcePath}' could not be read: {ex.Message}",
                ContentIssueSeverity.Error,
                Field: sourcePath));

            return new ContentPackFile(
                directoryFamily ?? string.Empty,
                directoryFamily ?? string.Empty,
                sourcePath,
                []);
        }

        var root_ = ContentPackYaml.ToJson(text, out var error);
        if (root_ is null)
        {
            issues.Add(new ContentIssue(
                ContentPackRules.FileInvalid,
                $"'{sourcePath}': {error}",
                ContentIssueSeverity.Error,
                Field: sourcePath));

            return new ContentPackFile(directoryFamily ?? string.Empty, directoryFamily ?? string.Empty, sourcePath, []);
        }

        if (root_ is not JsonObject document)
        {
            issues.Add(new ContentIssue(
                ContentPackRules.EnvelopeInvalid,
                $"'{sourcePath}': a pack file must be a mapping with an 'entities' list at the top level.",
                ContentIssueSeverity.Error,
                Field: sourcePath));

            return new ContentPackFile(directoryFamily ?? string.Empty, directoryFamily ?? string.Empty, sourcePath, []);
        }

        var headerFamily = document["family"] is JsonValue familyValue
            && familyValue.TryGetValue<string>(out var declared)
            && !string.IsNullOrWhiteSpace(declared)
                ? declared.Trim()
                : null;

        var declaredFamily = directoryFamily ?? headerFamily;
        if (declaredFamily is null)
        {
            issues.Add(new ContentIssue(
                ContentPackRules.FamilyDirectoryRequired,
                $"'{sourcePath}' sits directly in the pack root. A pack file belongs in content/<family>/, e.g. content/quests/{Path.GetFileName(path)}, or must declare a 'family:' header.",
                ContentIssueSeverity.Error,
                Field: sourcePath));

            return new ContentPackFile(string.Empty, string.Empty, sourcePath, []);
        }

        var family = ResolveFamily(declaredFamily);
        if (family is null)
        {
            issues.Add(new ContentIssue(
                ContentPackRules.FamilyUnknown,
                $"'{sourcePath}' belongs to family '{declaredFamily}', which is not a content type the engine stores. A pack file belongs in a directory named after its family, e.g. content/item/ or content/quests/. Known families: {string.Join(", ", ContentEntityTypes.KnownTypes)}.",
                ContentIssueSeverity.Error,
                Field: "family"));

            return new ContentPackFile(declaredFamily, declaredFamily, sourcePath, []);
        }

        if (directoryFamily is not null && headerFamily is not null)
        {
            var declaredHeader = ResolveFamily(headerFamily);
            if (!string.Equals(declaredHeader, family, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ContentIssue(
                    ContentPackRules.FamilyMismatch,
                    $"'{sourcePath}' declares family '{headerFamily}' but sits in the '{directoryFamily}' directory. The directory is authoritative; drop the header or move the file.",
                    ContentIssueSeverity.Error,
                    Field: "family"));
            }
        }

        if (!ContentEntityTypes.TryResolve(family, out var resolver))
        {
            return new ContentPackFile(family, family, sourcePath, []);
        }

        if (document["entities"] is not JsonArray entities)
        {
            issues.Add(new ContentIssue(
                ContentPackRules.EnvelopeInvalid,
                $"'{sourcePath}': the top level needs an 'entities' list of entity mappings.",
                ContentIssueSeverity.Error,
                Field: "entities"));

            return new ContentPackFile(family, resolver.ContentType, sourcePath, []);
        }

        var entries = new List<ContentPackEntry>();
        for (var index = 0; index < entities.Count; index++)
        {
            if (entities[index] is not JsonObject payload)
            {
                issues.Add(new ContentIssue(
                    ContentPackRules.EnvelopeInvalid,
                    $"'{sourcePath}': entity {index + 1} is not a mapping, so it cannot describe a {family}.",
                    ContentIssueSeverity.Error,
                    Field: $"entities[{index}]"));

                continue;
            }

            var slug = payload["slug"] is JsonValue slugValue && slugValue.TryGetValue<string>(out var written)
                ? written.Trim()
                : string.Empty;

            entries.Add(new ContentPackEntry(slug, payload, sourcePath, index + 1));
        }

        return new ContentPackFile(family, ContentEntityTypes.CanonicalFamily(resolver.ContentType), sourcePath, entries);
    }

    /// <summary>
    /// Folds a directory or header name onto the content family it names.
    /// </summary>
    /// <remarks>
    /// A plural directory is accepted — <c>content/quests/</c> is what an author types — and resolves to the family
    /// key the engine registers, which is singular. <c>species</c> resolves as itself, not by shedding a letter.
    /// </remarks>
    /// <param name="name">Directory name or <c>family:</c> header value.</param>
    /// <returns>The registered family key, or <c>null</c> when no candidate is a content type.</returns>
    private static string? ResolveFamily(string name)
    {
        foreach (var candidate in Candidates(name))
        {
            if (ContentEntityTypes.TryResolve(candidate, out _))
            {
                return candidate;
            }
        }

        return null;

        static IEnumerable<string> Candidates(string name)
        {
            yield return name;

            if (name.EndsWith("es", StringComparison.OrdinalIgnoreCase))
            {
                yield return name[..^2];
            }

            if (name.EndsWith('s'))
            {
                yield return name[..^1];
            }
        }
    }
}
