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

    /// <summary>
    /// Permissions that hand out or widen authority, as opposed to exercising it. Granting one of these to your
    /// own account is a privilege escalation, so <c>GrantPermissionAsync</c> refuses a self-directed grant of any
    /// of them even for a caller who may hand out authority (<c>admin.roles</c>).
    /// </summary>
    /// <remarks>
    /// This is a policy list, not a derivation from <see cref="All"/>: it names the authorities a role manager may
    /// not hand to themselves, and it is deliberately narrower than "anything that is not read-only". Widening it
    /// is a product decision, not a code cleanup. <see cref="NonEscalating"/> classifies the remainder, and
    /// <c>AdminUserManagementEndpointTests.Permissions_EveryDeclaredPermission_IsClassifiedForSelfGranting</c>
    /// fails when <see cref="All"/> grows a permission that neither set names — so adding a permission forces the
    /// author to decide which side of this line it sits on. Compared case-insensitively, matching the endpoints.
    /// </remarks>
    public static IReadOnlySet<string> AuthorityWidening { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ManageRoles,
            ManageUsers,
            BanPlayers,
            ViewPlayers,
        };

    /// <summary>
    /// The permissions classified as safe to grant to one's own account: they let a caller act on players, world
    /// state or content, but they neither hand out authority nor widen what the account can administer.
    /// </summary>
    /// <remarks>
    /// This set exists so the guard's policy is exhaustive rather than a hand-picked subset of <see cref="All"/>.
    /// A permission listed here is self-grantable by design; moving one into <see cref="AuthorityWidening"/>
    /// blocks it. <see cref="All"/> must be exactly the union of the two sets, which the classification test
    /// enforces.
    /// </remarks>
    public static IReadOnlySet<string> NonEscalating { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            KickPlayers,
            WarnPlayers,
            MutePlayers,
            SuspendPlayers,
            TeleportPlayers,
            GiveItems,
            RunEvents,
            ManageContent,
            ViewContent,
            ReviewSubmissions,
            DebugAccess,
            SendAnnouncements,
        };
}
