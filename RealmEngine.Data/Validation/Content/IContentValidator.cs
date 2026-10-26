using System.Text.RegularExpressions;
using RealmEngine.Data.Entities;

namespace RealmEngine.Data.Validation.Content;

/// <summary>
/// Validates one content family. Implementations are pure: entity in, issues out, no database access.
/// </summary>
/// <remarks>
/// The untyped <see cref="Validate"/> overload is what the registry exposes, because the write endpoint resolves a
/// validator from a URL segment and the CI guard iterates families. Typed implementations derive from
/// <see cref="ContentValidator{TEntity}"/> so they never deal with <see cref="object"/> themselves.
/// </remarks>
public interface IContentValidator
{
    /// <summary>Gets the content-type key this validator handles, e.g. <c>item</c> or <c>quest</c>.</summary>
    string ContentType { get; }

    /// <summary>Validates an entity of this family.</summary>
    /// <param name="entity">The entity to validate.</param>
    /// <param name="catalog">The slugs that exist, for reference checks.</param>
    /// <returns>The issues found; empty when the entity is valid.</returns>
    ContentValidationResult Validate(object entity, ContentReferenceCatalog catalog);
}

/// <summary>
/// Base class for a strongly typed content validator.
/// </summary>
/// <typeparam name="TEntity">The content entity type validated.</typeparam>
public abstract class ContentValidator<TEntity> : IContentValidator
    where TEntity : class
{
    /// <inheritdoc />
    public abstract string ContentType { get; }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">Thrown when <paramref name="entity"/> is not a <typeparamref name="TEntity"/>.</exception>
    public ContentValidationResult Validate(object entity, ContentReferenceCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(catalog);

        if (entity is not TEntity typed)
        {
            throw new ArgumentException(
                $"Content type '{ContentType}' is validated as {typeof(TEntity).Name}, but {entity.GetType().Name} was passed.",
                nameof(entity));
        }

        return new ContentValidationResult(ValidateEntity(typed, catalog));
    }

    /// <summary>Validates a typed entity.</summary>
    /// <param name="entity">The entity to validate.</param>
    /// <param name="catalog">The slugs that exist, for reference checks.</param>
    /// <returns>The issues found.</returns>
    protected abstract IReadOnlyList<ContentIssue> ValidateEntity(TEntity entity, ContentReferenceCatalog catalog);

    /// <summary>Adds an error-level issue.</summary>
    /// <param name="issues">Issue list to append to.</param>
    /// <param name="rule">Rule id from <see cref="ContentRules"/>.</param>
    /// <param name="message">Human-readable description.</param>
    /// <param name="identifier">Slug of the offending entity.</param>
    /// <param name="field">Dotted field path, when the issue belongs to one.</param>
    protected static void AddError(
        List<ContentIssue> issues,
        string rule,
        string message,
        string? identifier = null,
        string? field = null) =>
        issues.Add(new ContentIssue(rule, message, ContentIssueSeverity.Error, field, identifier));

    /// <summary>Adds a warning-level issue.</summary>
    /// <param name="issues">Issue list to append to.</param>
    /// <param name="rule">Rule id from <see cref="ContentRules"/>.</param>
    /// <param name="message">Human-readable description.</param>
    /// <param name="identifier">Slug of the offending entity.</param>
    /// <param name="field">Dotted field path, when the issue belongs to one.</param>
    protected static void AddWarning(
        List<ContentIssue> issues,
        string rule,
        string message,
        string? identifier = null,
        string? field = null) =>
        issues.Add(new ContentIssue(rule, message, ContentIssueSeverity.Warning, field, identifier));
}

/// <summary>Shape rules every content entity shares, plus the small helpers validators keep re-implementing.</summary>
public static partial class ContentShapeRules
{
    /// <summary>The documented slug format: lowercase segments joined by single hyphens.</summary>
    /// <param name="slug">Candidate slug.</param>
    /// <returns>True when the slug is acceptable.</returns>
    public static bool IsValidSlug(string? slug) => slug is not null && SlugPattern().IsMatch(slug);

    /// <summary>
    /// Checks the <see cref="ContentBase"/> fields every family shares: slug presence and format, display name, type
    /// key and rarity weight.
    /// </summary>
    /// <param name="entity">Entity to check.</param>
    /// <param name="issues">Issue list to append to.</param>
    public static void ValidateShared(ContentBase entity, List<ContentIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(issues);

        var identifier = string.IsNullOrWhiteSpace(entity.Slug) ? null : entity.Slug;

        if (string.IsNullOrWhiteSpace(entity.Slug))
        {
            issues.Add(new ContentIssue(
                ContentRules.SlugMissing,
                $"{entity.GetType().Name} has no slug.",
                ContentIssueSeverity.Error,
                "slug",
                identifier));
        }
        else if (!IsValidSlug(entity.Slug))
        {
            issues.Add(new ContentIssue(
                ContentRules.SlugFormat,
                $"Slug '{entity.Slug}' is not lowercase-hyphenated (expected pattern: lowercase words joined by single hyphens).",
                ContentIssueSeverity.Error,
                "slug",
                identifier));
        }

        if (string.IsNullOrWhiteSpace(entity.DisplayName))
        {
            issues.Add(new ContentIssue(
                ContentRules.DisplayNameMissing,
                $"{entity.GetType().Name} '{identifier ?? "(no slug)"}' has no display name.",
                ContentIssueSeverity.Error,
                "displayName",
                identifier));
        }

        if (string.IsNullOrWhiteSpace(entity.TypeKey))
        {
            issues.Add(new ContentIssue(
                ContentRules.TypeKeyMissing,
                $"{entity.GetType().Name} '{identifier ?? "(no slug)"}' has no type key, so content may not load it.",
                ContentIssueSeverity.Error,
                "typeKey",
                identifier));
        }

        if (entity.RarityWeight is < 1 or > 100)
        {
            issues.Add(new ContentIssue(
                ContentRules.RarityWeightOutOfRange,
                $"Rarity weight {entity.RarityWeight} is outside the documented 1–100 range.",
                ContentIssueSeverity.Warning,
                "rarityWeight",
                identifier));
        }
    }

    /// <summary>
    /// Reports a min/max pair that is inverted or negative.
    /// </summary>
    /// <param name="issues">Issue list to append to.</param>
    /// <param name="identifier">Slug of the offending entity.</param>
    /// <param name="field">Dotted field path of the minimum value.</param>
    /// <param name="min">Minimum value, when present.</param>
    /// <param name="max">Maximum value, when present.</param>
    public static void ValidateRange(
        List<ContentIssue> issues,
        string? identifier,
        string field,
        int? min,
        int? max)
    {
        ArgumentNullException.ThrowIfNull(issues);

        if (min is null && max is null)
        {
            return;
        }

        if (min < 0 || max < 0)
        {
            issues.Add(new ContentIssue(
                ContentRules.RangeInvalid,
                $"{field} must not be negative (min {min?.ToString() ?? "unset"}, max {max?.ToString() ?? "unset"}).",
                ContentIssueSeverity.Error,
                field,
                identifier));
            return;
        }

        if (min is not null && max is not null && min > max)
        {
            issues.Add(new ContentIssue(
                ContentRules.RangeInvalid,
                $"{field} is inverted: min {min} is greater than max {max}.",
                ContentIssueSeverity.Error,
                field,
                identifier));
        }
    }

    /// <summary>Reports a reference that does not resolve, naming both sides.</summary>
    /// <param name="issues">Issue list to append to.</param>
    /// <param name="identifier">Slug of the referencing entity.</param>
    /// <param name="field">Field holding the reference.</param>
    /// <param name="rule">Rule id to report.</param>
    /// <param name="target">The slug that was looked for.</param>
    /// <param name="family">Human-readable name of the target family, e.g. <c>item</c>.</param>
    public static void AddMissingReference(
        List<ContentIssue> issues,
        string? identifier,
        string field,
        string rule,
        string? target,
        string family)
    {
        ArgumentNullException.ThrowIfNull(issues);

        issues.Add(new ContentIssue(
            rule,
            $"{identifier ?? "content"} field '{field}' points at unknown {family} '{target}'.",
            ContentIssueSeverity.Error,
            field,
            identifier));
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();
}
