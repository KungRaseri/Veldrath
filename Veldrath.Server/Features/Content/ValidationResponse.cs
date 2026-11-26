using RealmEngine.Data.Entities;
using RealmEngine.Data.Validation.Content;

namespace Veldrath.Server.Features.Content;

/// <summary>
/// Shapes the response the write path returns: the saved entity plus every issue the validator produced.
/// </summary>
/// <remarks>
/// The type-key map this file used to own now lives in <see cref="RealmEngine.Data.Content.ContentEntityTypes"/>, so
/// the offline content-pack importer and the HTTP write path resolve a family the same way.
/// </remarks>
internal static class ValidationResponse
{
    /// <summary>Builds the response body shared by <c>validate</c>, <c>POST</c> and <c>PUT</c>.</summary>
    /// <param name="entity">The entity that was validated or saved.</param>
    /// <param name="result">The validation result.</param>
    /// <returns>An anonymous object serialised as the response body.</returns>
    internal static object For(ContentBase entity, ContentValidationResult result) => new
    {
        entity,
        isValid = result.IsValid,
        errors = result.Errors,
        warnings = result.Warnings,
    };
}
