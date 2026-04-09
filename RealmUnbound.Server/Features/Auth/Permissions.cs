namespace RealmUnbound.Server.Features.Auth;

/// <summary>
/// Defines all fine-grained permission strings used in the RBAC system.
/// Permissions are stored as <c>permission</c> claims on ASP.NET Identity roles
/// (<see cref="Microsoft.AspNetCore.Identity.IdentityRoleClaim{TKey}"/>) and on
/// individual user accounts for per-user overrides.
/// </summary>
public static class Permissions
{
    // ── Player Moderation ───────────────────────────────────────────────────
    /// <summary>Grant: permanently or temporarily ban a player account.</summary>
    public const string BanPlayers = "ban_players";

    /// <summary>Grant: forcibly disconnect an active player session.</summary>
    public const string KickPlayers = "kick_players";

    /// <summary>Grant: issue an in-game warning to a player.</summary>
    public const string WarnPlayers = "warn_players";

    /// <summary>Grant: view player account details and session history.</summary>
    public const string ViewPlayers = "view_players";

    // ── World / GameMaster Actions ───────────────────────────────────────────
    /// <summary>Grant: teleport any connected player to a different zone.</summary>
    public const string TeleportPlayers = "teleport_players";

    /// <summary>Grant: give items directly to a player's inventory.</summary>
    public const string GiveItems = "give_items";

    /// <summary>Grant: start or end server-wide world events.</summary>
    public const string RunEvents = "run_events";

    // ── Content ──────────────────────────────────────────────────────────────
    /// <summary>Grant: create, update, or delete canonical game content in the database.</summary>
    public const string ManageContent = "manage_content";

    /// <summary>Grant: view canonical content (read-only).</summary>
    public const string ViewContent = "view_content";

    // ── Administration ───────────────────────────────────────────────────────
    /// <summary>Grant: manage user roles and per-user permission overrides.</summary>
    public const string ManageRoles = "manage_roles";

    /// <summary>Grant: list, search, and view all user accounts.</summary>
    public const string ManageUsers = "manage_users";

    /// <summary>Grant: access diagnostic and debug endpoints.</summary>
    public const string DebugAccess = "debug_access";

    /// <summary>Grant: send server-wide announcements to all connected clients.</summary>
    public const string SendAnnouncements = "send_announcements";

    /// <summary>Grant: approve or reject community content submissions.</summary>
    public const string ReviewSubmissions = "review_submissions";

    /// <summary>Returns all defined permission strings.</summary>
    public static IReadOnlyList<string> All =>
    [
        BanPlayers, KickPlayers, WarnPlayers, ViewPlayers,
        TeleportPlayers, GiveItems, RunEvents,
        ManageContent, ViewContent,
        ManageRoles, ManageUsers, DebugAccess, SendAnnouncements,
        ReviewSubmissions,
    ];
}
