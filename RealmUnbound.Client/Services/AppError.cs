namespace RealmUnbound.Client.Services;

/// <summary>
/// Separates the user-facing message from an optional technical detail string
/// (e.g. the raw server error) that can be shown on request for debugging.
/// </summary>
public record AppError(string Message, string? Details = null);
