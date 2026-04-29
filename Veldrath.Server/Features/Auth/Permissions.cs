namespace Veldrath.Server.Features.Auth;

/// <summary>
/// Defines all fine-grained permission strings used in the RBAC system.
/// Permissions are namespaced using dot notation (e.g. <c>players.kick</c>).
/// Permissions are stored as <c>permission</c> claims on ASP.NET Identity roles
/// (<see cref="Microsoft.AspNetCore.Identity.IdentityRoleClaim{TKey}"/>) and on
/// individual user accounts for per-user overrides.
/// </summary>
public static class Permissions
{
    // ── players.* ───────────────────────────────────────────────────────────
    /// <summary>Grant: permanently or temporarily ban a player account.</summary>
    public const string BanPlayers = "players.ban";

    /// <summary>Grant: forcibly disconnect an active player session.</summary>
    public const string KickPlayers = "players.kick";

    /// <summary>Grant: issue an in-game warning to a player.</summary>
    public const string WarnPlayers = "players.warn";

    /// <summary>Grant: silence a player's chat for a duration.</summary>
    public const string MutePlayers = "players.mute";

    /// <summary>Grant: suspend a player account for a duration without a permanent ban.</summary>
    public const string SuspendPlayers = "players.suspend";

    /// <summary>Grant: view player account details and session history.</summary>
    public const string ViewPlayers = "players.view";

    // ── world.* ─────────────────────────────────────────────────────────────
    /// <summary>Grant: teleport any connected player to a different zone.</summary>
    public const string TeleportPlayers = "world.teleport";

    /// <summary>Grant: give items directly to a player's inventory.</summary>
    public const string GiveItems = "world.give";

    /// <summary>Grant: start or end server-wide world events.</summary>
    public const string RunEvents = "world.events";

    // ── content.* ───────────────────────────────────────────────────────────
    /// <summary>Grant: create, update, or delete canonical game content in the database.</summary>
    public const string ManageContent = "content.manage";

    /// <summary>Grant: view canonical content (read-only).</summary>
    public const string ViewContent = "content.view";

    /// <summary>Grant: approve or reject community content submissions.</summary>
    public const string ReviewSubmissions = "content.review";

    // ── admin.* ──────────────────────────────────────────────────────────────
    /// <summary>Grant: manage user roles and per-user permission overrides.</summary>
    public const string ManageRoles = "admin.roles";

    /// <summary>Grant: list, search, and view all user accounts.</summary>
    public const string ManageUsers = "admin.users";

    /// <summary>Grant: access diagnostic and debug endpoints.</summary>
    public const string DebugAccess = "admin.debug";

    /// <summary>Grant: send server-wide announcements to all connected clients.</summary>
    public const string SendAnnouncements = "admin.announce";

    /// <summary>Returns all defined permission strings.</summary>
    public static IReadOnlyList<string> All =>
    [
        BanPlayers, KickPlayers, WarnPlayers, MutePlayers, SuspendPlayers, ViewPlayers,
        TeleportPlayers, GiveItems, RunEvents,
        ManageContent, ViewContent, ReviewSubmissions,
        ManageRoles, ManageUsers, DebugAccess, SendAnnouncements,
    ];
}
