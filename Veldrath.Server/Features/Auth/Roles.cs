namespace Veldrath.Server.Features.Auth;

/// <summary>
/// Defines the canonical role names used throughout the RBAC system.
/// Roles are seeded into ASP.NET Identity at startup. Each role carries a default
/// set of <see cref="Permissions"/> claims stored on
/// <see cref="Microsoft.AspNetCore.Identity.IdentityRoleClaim{TKey}"/>.
/// </summary>
public static class Roles
{
    /// <summary>Full system access — all permissions granted.</summary>
    public const string Admin = "Admin";

    /// <summary>Player-facing moderation: ban, kick, warn, view players.</summary>
    public const string Moderator = "Moderator";

    /// <summary>Community content curation: review and approve/reject Foundry submissions.</summary>
    public const string Curator = "Curator";

    /// <summary>In-game powers: teleport, give items, run events, view players.</summary>
    public const string GameMaster = "GameMaster";

    /// <summary>Debug and diagnostic access plus content read access.</summary>
    public const string Developer = "Developer";

    /// <summary>Manage and view canonical game content in the database.</summary>
    public const string ContentEditor = "ContentEditor";

    /// <summary>Returns all defined role names.</summary>
    public static IReadOnlyList<string> All =>
        [Admin, Moderator, Curator, GameMaster, Developer, ContentEditor];

    /// <summary>Returns the default permission set for a given role name.</summary>
    public static IReadOnlyList<string> DefaultPermissionsFor(string role) => role switch
    {
        Admin => Permissions.All,
        Moderator =>
        [
            Permissions.KickPlayers, Permissions.WarnPlayers, Permissions.MutePlayers,
            Permissions.ViewPlayers, Permissions.SendAnnouncements,
        ],
        Curator =>
        [
            Permissions.ReviewSubmissions, Permissions.ViewContent,
        ],
        GameMaster =>
        [
            Permissions.TeleportPlayers, Permissions.GiveItems, Permissions.RunEvents,
            Permissions.ViewPlayers, Permissions.KickPlayers, Permissions.WarnPlayers,
            Permissions.MutePlayers, Permissions.SuspendPlayers, Permissions.SendAnnouncements,
        ],
        Developer =>
        [
            Permissions.DebugAccess, Permissions.ViewContent, Permissions.ViewPlayers,
        ],
        ContentEditor =>
        [
            Permissions.ManageContent, Permissions.ViewContent,
        ],
        _ => [],
    };
}
