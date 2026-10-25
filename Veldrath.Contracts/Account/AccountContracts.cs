namespace Veldrath.Contracts.Account;

// ── RBAC profile ─────────────────────────────────────────────────────────────

/// <summary>
/// The server's view of an authenticated account: identity plus the roles and permissions that drive
/// authorisation.
/// </summary>
/// <remarks>
/// Better Auth owns everything a player can change about their own account (email, password,
/// username, linked providers, sessions), so this payload deliberately carries only what
/// <c>Veldrath.Server</c> is authoritative for — the Identity user row and its RBAC claims.
/// </remarks>
/// <param name="AccountId">Unique account identifier (the Better Auth user id / JWT <c>sub</c>).</param>
/// <param name="Username">Login username.</param>
/// <param name="DisplayName">Optional public display name distinct from the login username.</param>
/// <param name="Email">Account email address.</param>
/// <param name="Roles">All Identity role names currently assigned to this account.</param>
/// <param name="Permissions">Effective permission set (union of role and per-user grants).</param>
public record AccountProfileDto(
    Guid AccountId,
    string Username,
    string? DisplayName,
    string? Email,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

// ── Admin utilities ──────────────────────────────────────────────────────────

/// <summary>Status of a single health check reported by the server.</summary>
/// <param name="Name">Health-check name as registered in the DI container.</param>
/// <param name="Status">
/// Textual status: <c>"Healthy"</c>, <c>"Degraded"</c>, or <c>"Unhealthy"</c>.
/// </param>
/// <param name="Description">Optional description or error message for this check.</param>
public record HealthCheckEntryDto(string Name, string Status, string? Description);
