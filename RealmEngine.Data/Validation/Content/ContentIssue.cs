namespace RealmEngine.Data.Validation.Content;

/// <summary>Whether a content issue blocks the write or merely asks for review.</summary>
public enum ContentIssueSeverity
{
    /// <summary>The content is usable, but the issue should be reviewed.</summary>
    Warning,

    /// <summary>The content is invalid and must not be written.</summary>
    Error
}

/// <summary>
/// One finding from content validation, shared by every caller: the startup audit, the <c>/api/content</c> write
/// path, and the CI content guard.
/// </summary>
/// <param name="Rule">Stable machine-readable rule id, e.g. <c>missing-quest-target</c>. Never localised.</param>
/// <param name="Message">Human-readable description naming both sides of the problem. Shown to authors as-is.</param>
/// <param name="Severity">Whether the issue blocks a write.</param>
/// <param name="Field">Dotted field path the issue belongs to, when it points at one — e.g. <c>stats.damageMin</c>.</param>
/// <param name="Identifier">Slug (or other stable id) of the entity that owns the issue.</param>
public sealed record ContentIssue(
    string Rule,
    string Message,
    ContentIssueSeverity Severity = ContentIssueSeverity.Error,
    string? Field = null,
    string? Identifier = null);

/// <summary>The issues produced by validating one content entity.</summary>
/// <param name="Issues">Every issue found, errors and warnings together.</param>
public sealed record ContentValidationResult(IReadOnlyList<ContentIssue> Issues)
{
    /// <summary>A result with no issues.</summary>
    public static ContentValidationResult Empty { get; } = new([]);

    /// <summary>Gets a value indicating whether the content has no error-level issues.</summary>
    public bool IsValid => Issues.All(issue => issue.Severity != ContentIssueSeverity.Error);

    /// <summary>Gets the error-level issues.</summary>
    public IReadOnlyList<ContentIssue> Errors =>
        [.. Issues.Where(issue => issue.Severity == ContentIssueSeverity.Error)];

    /// <summary>Gets the warning-level issues.</summary>
    public IReadOnlyList<ContentIssue> Warnings =>
        [.. Issues.Where(issue => issue.Severity == ContentIssueSeverity.Warning)];
}
