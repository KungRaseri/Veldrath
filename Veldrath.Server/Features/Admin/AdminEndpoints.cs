using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Veldrath.Contracts.Account;
using Veldrath.Contracts.Admin;
using Veldrath.Contracts.Connection;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Hubs;
using Veldrath.Server.Settings;

namespace Veldrath.Server.Features.Admin;

/// <summary>
/// Minimal API endpoints for server administration.
/// All endpoints in this group require the <c>manage_users</c> permission claim.
///
/// GET    /api/admin/users                            — list accounts (paginated, filterable)
/// GET    /api/admin/users/{id}                       — account detail with roles + effective permissions
/// POST   /api/admin/users/{id}/roles                 — assign a role to an account
/// DELETE /api/admin/users/{id}/roles/{role}          — revoke a role from an account
/// POST   /api/admin/users/{id}/permissions           — grant a per-user permission claim
/// DELETE /api/admin/users/{id}/permissions/{perm}    — revoke a per-user permission claim
/// POST   /api/admin/players/kick                     — forcibly disconnect an active session
/// POST   /api/admin/players/ban                      — ban an account and kick active sessions
/// POST   /api/admin/players/unban                    — lift an existing ban
/// POST   /api/admin/players/warn                     — issue a formal warning
/// POST   /api/admin/players/mute                     — mute a player's chat
/// POST   /api/admin/players/unmute                   — lift a chat mute
/// POST   /api/admin/broadcast                        — send announcement to all connected clients
/// GET    /api/admin/audit                            — paginated audit trail
/// GET    /api/admin/sessions                         — active player sessions
/// GET    /api/admin/reports                          — player reports
/// PUT    /api/admin/reports/{id}/resolve             — mark a report as resolved
/// GET    /api/admin/status                           — live server health snapshot
/// </summary>
public static class AdminEndpoints
{
    // Captured once at first request; accurate enough for a status display.
    private static readonly DateTimeOffset _serverStartedAt = DateTimeOffset.UtcNow;

    /// <summary>Registers all admin and moderation endpoints on the provided route builder.</summary>
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        // ── Group 1: admin-only operations (manage_users required) ────────────────
        // Keeps user/role/permission management and the audit log out of reach for Moderators.
        var adminGroup = app.MapGroup("/api/admin")
            .WithTags("Admin")
            .RequireAuthorization(Auth.Permissions.ManageUsers);

        adminGroup.MapGet("/users",                                       ListUsersAsync);
        adminGroup.MapGet("/users/{id:guid}",                             GetUserAsync);
        adminGroup.MapPost("/users/{id:guid}/roles",                      AssignRoleAsync);
        adminGroup.MapDelete("/users/{id:guid}/roles/{role}",             RevokeRoleAsync);
        adminGroup.MapPost("/users/{id:guid}/permissions",                GrantPermissionAsync);
        adminGroup.MapDelete("/users/{id:guid}/permissions/{permission}", RevokePermissionAsync);
        adminGroup.MapGet("/audit",                                       GetAuditLogAsync);
        adminGroup.MapGet("/sessions",                                    GetSessionsAsync);
        adminGroup.MapGet("/status",                                      GetServerStatusAsync);

        // ── Group 2: per-permission moderation actions ────────────────────────────
        // No group-level permission requirement — each endpoint enforces its own claim.
        // This makes these endpoints reachable to Moderators who hold the relevant claims
        // without granting them full manage_users access.
        var modGroup = app.MapGroup("/api/admin")
            .WithTags("Admin")
            .RequireAuthorization();

        modGroup.MapPost("/players/kick",   KickPlayerAsync)
            .RequireAuthorization(Auth.Permissions.KickPlayers)
            .RequireRateLimiting("admin-actions");
        modGroup.MapPost("/players/ban",    BanPlayerAsync)
            .RequireAuthorization(Auth.Permissions.BanPlayers)
            .RequireRateLimiting("admin-actions");
        modGroup.MapPost("/players/unban",  UnbanPlayerAsync)
            .RequireAuthorization(Auth.Permissions.BanPlayers)
            .RequireRateLimiting("admin-actions");
        modGroup.MapPost("/players/warn",   WarnPlayerAsync)
            .RequireAuthorization(Auth.Permissions.WarnPlayers)
            .RequireRateLimiting("admin-actions");
        modGroup.MapPost("/players/mute",   MutePlayerAsync)
            .RequireAuthorization(Auth.Permissions.KickPlayers)
            .RequireRateLimiting("admin-actions");
        modGroup.MapPost("/players/unmute", UnmutePlayerAsync)
            .RequireAuthorization(Auth.Permissions.KickPlayers)
            .RequireRateLimiting("admin-actions");
        modGroup.MapPost("/broadcast",      BroadcastAsync)
            .RequireAuthorization(Auth.Permissions.SendAnnouncements)
            .RequireRateLimiting("admin-actions");
        modGroup.MapGet("/reports",         GetReportsAsync)
            .RequireAuthorization(Auth.Permissions.ViewPlayers);
        modGroup.MapPut("/reports/{id:guid}/resolve", ResolveReportAsync)
            .RequireAuthorization(Auth.Permissions.ViewPlayers)
            .RequireRateLimiting("admin-actions");

        // ── Group 3: moderator read-only views ────────────────────────────────────
        // A separate /api/mod prefix accessible to any role that holds view_players.
        // Reuses the same handler implementations as the admin group above.
        var modReadGroup = app.MapGroup("/api/mod")
            .WithTags("Moderation")
            .RequireAuthorization(Auth.Permissions.ViewPlayers);

        modReadGroup.MapGet("/users",              ListUsersAsync);
        modReadGroup.MapGet("/users/{id:guid}",    GetUserAsync);
        modReadGroup.MapGet("/reports",            GetReportsAsync);
        modReadGroup.MapPut("/reports/{id:guid}/resolve", ResolveReportAsync);

        return app;
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private static async Task<IResult> ListUsersAsync(
        ClaimsPrincipal actor,
        [FromServices] UserManager<PlayerAccount> userManager,
        [FromQuery] string? search   = null,
        [FromQuery] string? role     = null,
        [FromQuery] string? status   = null,  // "active" | "banned" | "muted"
        [FromQuery] string? sort     = null,  // "username" (default) | "lastseen" | "warncount"
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 25)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);

        // When filtering by role we get the set from Identity then apply further filters.
        IList<PlayerAccount> roleUsers = role is { Length: > 0 }
            ? await userManager.GetUsersInRoleAsync(role)
            : [.. userManager.Users];

        var query = roleUsers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u =>
                (u.UserName != null && u.UserName.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                (u.Email    != null && u.Email.Contains(search, StringComparison.OrdinalIgnoreCase)));

        var now = DateTimeOffset.UtcNow;
        query = status?.ToLowerInvariant() switch
        {
            "banned" => query.Where(u => u.IsBanned && (u.BannedUntil == null || u.BannedUntil > now)),
            "muted"  => query.Where(u => u.IsMuted  && (u.MutedUntil  == null || u.MutedUntil  > now)),
            "active" => query.Where(u =>
                !(u.IsBanned && (u.BannedUntil == null || u.BannedUntil > now))),
            _        => query
        };

        var sorted = sort?.ToLowerInvariant() switch
        {
            "lastseen"  => query.OrderByDescending(u => u.LastSeenAt),
            "warncount" => query.OrderByDescending(u => u.WarnCount),
            _           => query.OrderBy(u => u.UserName)
        };

        var total = sorted.Count();
        var skip  = (Math.Max(1, page) - 1) * pageSize;
        var users = sorted.Skip(skip).Take(pageSize).ToList();

        var items = new List<PlayerSummaryDto>(users.Count);
        foreach (var u in users)
        {
            var roles   = await userManager.GetRolesAsync(u);
            var banned  = u.IsBanned && (u.BannedUntil == null || u.BannedUntil > now);
            var muted   = u.IsMuted  && (u.MutedUntil  == null || u.MutedUntil  > now);
            items.Add(new PlayerSummaryDto(u.Id, u.UserName!, u.Email, [.. roles],
                banned, u.BannedUntil, u.WarnCount, muted, u.CreatedAt, u.LastSeenAt));
        }

        return Results.Ok(new { Total = total, Page = page, PageSize = pageSize, Items = items });
    }

    private static async Task<IResult> GetUserAsync(
        Guid id,
        [FromServices] UserManager<PlayerAccount> userManager,
        [FromServices] RoleManager<IdentityRole<Guid>> roleManager)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return Results.NotFound();

        var roles      = (await userManager.GetRolesAsync(user)).ToList();
        var userClaims = await userManager.GetClaimsAsync(user);
        var userPerms  = userClaims.Where(c => c.Type == "permission").Select(c => c.Value).ToList();

        var allPerms = new HashSet<string>(userPerms, StringComparer.Ordinal);
        foreach (var roleName in roles)
        {
            var roleEntity = await roleManager.FindByNameAsync(roleName);
            if (roleEntity is null) continue;
            foreach (var c in (await roleManager.GetClaimsAsync(roleEntity)).Where(c => c.Type == "permission"))
                allPerms.Add(c.Value);
        }

        var now    = DateTimeOffset.UtcNow;
        var banned = user.IsBanned && (user.BannedUntil == null || user.BannedUntil > now);
        var muted  = user.IsMuted  && (user.MutedUntil  == null || user.MutedUntil  > now);
        var dto    = new PlayerDetailDto(
            user.Id, user.UserName!, user.Email,
            roles, [.. allPerms], userPerms,
            banned, user.BannedUntil, user.BanReason,
            user.WarnCount, muted, user.MutedUntil, user.MuteReason,
            user.CreatedAt, user.LastSeenAt);

        return Results.Ok(dto);
    }

    private static async Task<IResult> AssignRoleAsync(
        Guid id,
        [FromBody] AssignRoleRequest request,
        ClaimsPrincipal actor,
        [FromServices] UserManager<PlayerAccount> userManager,
        [FromServices] ApplicationDbContext db)
    {
        if (!Auth.Roles.All.Contains(request.Role, StringComparer.OrdinalIgnoreCase))
            return Results.BadRequest(new AdminActionResponse(false, $"Unknown role '{request.Role}'."));

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return Results.NotFound();

        if (await userManager.IsInRoleAsync(user, request.Role))
            return Results.Ok(new AdminActionResponse(true, $"Account already has role '{request.Role}'."));

        var result = await userManager.AddToRoleAsync(user, request.Role);
        if (!result.Succeeded)
            return Results.BadRequest(new AdminActionResponse(false, string.Join("; ", result.Errors.Select(e => e.Description))));

        await WriteAuditAsync(db, actor, user.Id, user.UserName, "assign_role", request.Role);
        return Results.Ok(new AdminActionResponse(true, $"Role '{request.Role}' assigned."));
    }

    private static async Task<IResult> RevokeRoleAsync(
        Guid id,
        string role,
        ClaimsPrincipal actor,
        [FromServices] UserManager<PlayerAccount> userManager,
        [FromServices] ApplicationDbContext db)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return Results.NotFound();

        if (!await userManager.IsInRoleAsync(user, role))
            return Results.Ok(new AdminActionResponse(true, $"Account does not have role '{role}'."));

        var result = await userManager.RemoveFromRoleAsync(user, role);
        if (!result.Succeeded)
            return Results.BadRequest(new AdminActionResponse(false, string.Join("; ", result.Errors.Select(e => e.Description))));

        await WriteAuditAsync(db, actor, user.Id, user.UserName, "revoke_role", role);
        return Results.Ok(new AdminActionResponse(true, $"Role '{role}' revoked."));
    }

    private static async Task<IResult> GrantPermissionAsync(
        Guid id,
        [FromBody] GrantPermissionRequest request,
        ClaimsPrincipal actor,
        [FromServices] UserManager<PlayerAccount> userManager,
        [FromServices] ApplicationDbContext db)
    {
        if (!Auth.Permissions.All.Contains(request.Permission, StringComparer.OrdinalIgnoreCase))
            return Results.BadRequest(new AdminActionResponse(false, $"Unknown permission '{request.Permission}'."));

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return Results.NotFound();

        var existing = await userManager.GetClaimsAsync(user);
        if (existing.Any(c => c.Type == "permission" && c.Value == request.Permission))
            return Results.Ok(new AdminActionResponse(true, "Permission already granted."));

        var result = await userManager.AddClaimAsync(user, new Claim("permission", request.Permission));
        if (!result.Succeeded)
            return Results.BadRequest(new AdminActionResponse(false, string.Join("; ", result.Errors.Select(e => e.Description))));

        await WriteAuditAsync(db, actor, user.Id, user.UserName, "grant_permission", request.Permission);
        return Results.Ok(new AdminActionResponse(true, $"Permission '{request.Permission}' granted."));
    }

    private static async Task<IResult> RevokePermissionAsync(
        Guid id,
        string permission,
        ClaimsPrincipal actor,
        [FromServices] UserManager<PlayerAccount> userManager,
        [FromServices] ApplicationDbContext db)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return Results.NotFound();

        var existing = await userManager.GetClaimsAsync(user);
        var claim    = existing.FirstOrDefault(c => c.Type == "permission" && c.Value == permission);
        if (claim is null)
            return Results.Ok(new AdminActionResponse(true, "Permission not found on account."));

        var result = await userManager.RemoveClaimAsync(user, claim);
        if (!result.Succeeded)
            return Results.BadRequest(new AdminActionResponse(false, string.Join("; ", result.Errors.Select(e => e.Description))));

        await WriteAuditAsync(db, actor, user.Id, user.UserName, "revoke_permission", permission);
        return Results.Ok(new AdminActionResponse(true, $"Permission '{permission}' revoked."));
    }

    private static async Task<IResult> KickPlayerAsync(
        [FromBody] KickPlayerRequest request,
        ClaimsPrincipal actor,
        [FromServices] UserManager<PlayerAccount> userManager,
        [FromServices] ApplicationDbContext db,
        [FromServices] IHubContext<GameHub> hub)
    {
        var user = await userManager.FindByIdAsync(request.AccountId.ToString());
        if (user is null) return Results.NotFound();

        var reason = request.Reason ?? "You have been disconnected by an administrator.";
        await hub.Clients.Group($"account:{request.AccountId}").SendAsync("OnKicked", new KickedPayload(reason));
        await WriteAuditAsync(db, actor, user.Id, user.UserName, "kick", reason);

        return Results.Ok(new AdminActionResponse(true, $"Kick signal sent to account {request.AccountId}."));
    }

    private static async Task<IResult> BanPlayerAsync(
        [FromBody] BanPlayerRequest request,
        ClaimsPrincipal actor,
        [FromServices] UserManager<PlayerAccount> userManager,
        [FromServices] ApplicationDbContext db,
        [FromServices] IHubContext<GameHub> hub)
    {
        if (request.DurationMinutes.HasValue && (request.DurationMinutes.Value < 1 || request.DurationMinutes.Value > 525600))
            return Results.BadRequest(new { error = "DurationMinutes must be between 1 and 525600 (1 year)." });

        var user = await userManager.FindByIdAsync(request.AccountId.ToString());
        if (user is null) return Results.NotFound();

        user.IsBanned    = true;
        user.BanReason   = request.Reason;
        user.BannedUntil = request.DurationMinutes.HasValue
            ? DateTimeOffset.UtcNow.AddMinutes(request.DurationMinutes.Value)
            : null;
        await userManager.UpdateAsync(user);

        var msg = request.DurationMinutes.HasValue
            ? $"You have been banned for {request.DurationMinutes} minutes. Reason: {request.Reason}"
            : $"You have been permanently banned. Reason: {request.Reason}";
        await hub.Clients.Group($"account:{request.AccountId}").SendAsync("OnKicked", new KickedPayload(msg));
        await WriteAuditAsync(db, actor, user.Id, user.UserName, "ban",
            $"{request.Reason} (duration: {(request.DurationMinutes.HasValue ? $"{request.DurationMinutes}m" : "permanent")})");

        return Results.Ok(new AdminActionResponse(true, $"Account {request.AccountId} banned."));
    }

    private static async Task<IResult> UnbanPlayerAsync(
        [FromBody] UnbanPlayerRequest request,
        ClaimsPrincipal actor,
        [FromServices] UserManager<PlayerAccount> userManager,
        [FromServices] ApplicationDbContext db)
    {
        var user = await userManager.FindByIdAsync(request.AccountId.ToString());
        if (user is null) return Results.NotFound();

        user.IsBanned    = false;
        user.BannedUntil = null;
        user.BanReason   = null;
        await userManager.UpdateAsync(user);
        await WriteAuditAsync(db, actor, user.Id, user.UserName, "unban", null);

        return Results.Ok(new AdminActionResponse(true, $"Account {request.AccountId} unbanned."));
    }

    private static async Task<IResult> WarnPlayerAsync(
        [FromBody] WarnPlayerRequest request,
        ClaimsPrincipal actor,
        [FromServices] UserManager<PlayerAccount> userManager,
        [FromServices] ApplicationDbContext db,
        [FromServices] IHubContext<GameHub> hub,
        [FromServices] IOptions<ModerationOptions> options)
    {
        var user = await userManager.FindByIdAsync(request.AccountId.ToString());
        if (user is null) return Results.NotFound();

        // Atomic increment — avoids the race condition where two concurrent warns
        // both read the pre-increment WarnCount and apply the auto-ban threshold check twice.
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE \"AspNetUsers\" SET \"WarnCount\" = \"WarnCount\" + 1 WHERE \"Id\" = {0}",
            request.AccountId);

        // Re-fetch the post-increment state from the DB (EF change-tracker holds the stale value).
        await db.Entry(user).ReloadAsync();

        var threshold  = options.Value.AutoBanWarnThreshold;
        var autoBanned = false;

        if (threshold > 0 && user.WarnCount >= threshold && !user.IsBanned)
        {
            user.IsBanned  = true;
            user.BanReason = $"Automatic ban: {user.WarnCount} warnings accumulated.";
            await userManager.UpdateAsync(user);
            autoBanned = true;
        }

        // Notify the player in-game.
        await hub.Clients.Group($"account:{request.AccountId}")
            .SendAsync("OnWarned", new WarnedPayload(request.Reason, user.WarnCount));

        if (autoBanned)
            await hub.Clients.Group($"account:{request.AccountId}")
                .SendAsync("OnKicked", new KickedPayload($"You have been automatically banned after {user.WarnCount} warnings."));

        await WriteAuditAsync(db, actor, user.Id, user.UserName, "warn",
            $"{request.Reason} (warn #{user.WarnCount}{(autoBanned ? " — auto-ban triggered" : "")})");

        var msg = autoBanned
            ? $"Warning issued. Account auto-banned after reaching {user.WarnCount} warnings."
            : $"Warning issued. Account now has {user.WarnCount} warning(s).";
        return Results.Ok(new AdminActionResponse(true, msg));
    }

    private static async Task<IResult> MutePlayerAsync(
        [FromBody] MutePlayerRequest request,
        ClaimsPrincipal actor,
        [FromServices] UserManager<PlayerAccount> userManager,
        [FromServices] ApplicationDbContext db,
        [FromServices] IHubContext<GameHub> hub)
    {
        if (request.DurationMinutes.HasValue && (request.DurationMinutes.Value < 1 || request.DurationMinutes.Value > 525600))
            return Results.BadRequest(new { error = "DurationMinutes must be between 1 and 525600 (1 year)." });

        var user = await userManager.FindByIdAsync(request.AccountId.ToString());
        if (user is null) return Results.NotFound();

        user.IsMuted   = true;
        user.MuteReason = request.Reason;
        user.MutedUntil = request.DurationMinutes.HasValue
            ? DateTimeOffset.UtcNow.AddMinutes(request.DurationMinutes.Value)
            : null;
        await userManager.UpdateAsync(user);

        await hub.Clients.Group($"account:{request.AccountId}")
            .SendAsync("OnMuted", new MutedPayload(request.Reason, user.MutedUntil));
        await WriteAuditAsync(db, actor, user.Id, user.UserName, "mute",
            $"{request.Reason ?? "(no reason)"} (duration: {(request.DurationMinutes.HasValue ? $"{request.DurationMinutes}m" : "permanent")})");

        return Results.Ok(new AdminActionResponse(true, $"Account {request.AccountId} muted."));
    }

    private static async Task<IResult> UnmutePlayerAsync(
        [FromBody] UnmutePlayerRequest request,
        ClaimsPrincipal actor,
        [FromServices] UserManager<PlayerAccount> userManager,
        [FromServices] ApplicationDbContext db)
    {
        var user = await userManager.FindByIdAsync(request.AccountId.ToString());
        if (user is null) return Results.NotFound();

        user.IsMuted    = false;
        user.MutedUntil = null;
        user.MuteReason = null;
        await userManager.UpdateAsync(user);
        await WriteAuditAsync(db, actor, user.Id, user.UserName, "unmute", null);

        return Results.Ok(new AdminActionResponse(true, $"Account {request.AccountId} unmuted."));
    }

    private static async Task<IResult> BroadcastAsync(
        [FromBody] BroadcastAnnouncementRequest request,
        ClaimsPrincipal actor,
        [FromServices] ApplicationDbContext db,
        [FromServices] IHubContext<GameHub> hub)
    {
        await hub.Clients.All.SendAsync("OnAnnouncement", new AnnouncementPayload(request.Message, request.Severity));
        await WriteAuditAsync(db, actor, null, null, "broadcast", $"[{request.Severity}] {request.Message}");

        return Results.Ok(new AdminActionResponse(true, "Announcement broadcast to all connected clients."));
    }

    private static async Task<IResult> GetAuditLogAsync(
        [FromServices] ApplicationDbContext db,
        [FromQuery] Guid?   targetId = null,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        var skip  = (Math.Max(1, page) - 1) * pageSize;

        var query = db.AdminAuditEntries.AsNoTracking().AsQueryable();
        if (targetId.HasValue)
            query = query.Where(e => e.TargetAccountId == targetId.Value);

        var total   = await query.CountAsync();
        var entries = await query.OrderByDescending(e => e.OccurredAt).Skip(skip).Take(pageSize).ToListAsync();

        var items = entries.Select(e => new AuditEntryDto(
            e.Id, e.ActorUsername, e.TargetAccountId, e.TargetUsername,
            e.Action, e.Details, e.OccurredAt)).ToList();

        return Results.Ok(new { Total = total, Page = page, PageSize = pageSize, Items = items });
    }

    private static async Task<IResult> GetSessionsAsync(
        [FromServices] ApplicationDbContext db,
        [FromQuery] string? regionId = null,
        [FromQuery] string? zoneId   = null)
    {
        var query = db.PlayerSessions
            .AsNoTracking()
            .Include(s => s.Character)
                .ThenInclude(c => c.Account)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(regionId)) query = query.Where(s => s.RegionId == regionId);
        if (!string.IsNullOrWhiteSpace(zoneId))   query = query.Where(s => s.ZoneId   == zoneId);

        var sessions = await query.OrderBy(s => s.CharacterName).ToListAsync();

        var items = sessions.Select(s => new ActiveSessionDto(
            s.CharacterId,
            s.CharacterName,
            s.Character.AccountId,
            s.Character.Account.UserName ?? string.Empty,
            s.RegionId,
            s.ZoneId,
            s.EnteredAt)).ToList();

        return Results.Ok(items);
    }

    private static async Task<IResult> GetReportsAsync(
        [FromServices] ApplicationDbContext db,
        [FromQuery] bool?   resolved = null,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        var skip  = (Math.Max(1, page) - 1) * pageSize;

        var query = db.PlayerReports.AsNoTracking().AsQueryable();
        if (resolved.HasValue) query = query.Where(r => r.IsResolved == resolved.Value);

        var total   = await query.CountAsync();
        var reports = await query
            .OrderBy(r => r.IsResolved)
            .ThenByDescending(r => r.SubmittedAt)
            .Skip(skip).Take(pageSize).ToListAsync();

        var items = reports.Select(r => new PlayerReportDto(
            r.Id, r.ReporterName, r.TargetName, r.Reason,
            r.SubmittedAt, r.IsResolved, r.ResolvedAt)).ToList();

        return Results.Ok(new { Total = total, Page = page, PageSize = pageSize, Items = items });
    }

    private static async Task<IResult> ResolveReportAsync(
        Guid id,
        ClaimsPrincipal actor,
        [FromServices] ApplicationDbContext db)
    {
        var report = await db.PlayerReports.FindAsync(id);
        if (report is null) return Results.NotFound();
        if (report.IsResolved) return Results.Ok(new AdminActionResponse(true, "Already resolved."));

        var (actorId, _) = GetActor(actor);
        report.IsResolved          = true;
        report.ResolvedAt          = DateTimeOffset.UtcNow;
        report.ResolvedByAccountId = actorId;
        await db.SaveChangesAsync();

        return Results.Ok(new AdminActionResponse(true, "Report resolved."));
    }

    private static async Task<IResult> GetServerStatusAsync(
        [FromServices] ApplicationDbContext db,
        [FromServices] HealthCheckService healthCheckService)
    {
        var sessions = await db.PlayerSessions
            .AsNoTracking()
            .Include(s => s.Zone)
            .Where(s => s.ZoneId != null)
            .GroupBy(s => new { s.ZoneId, s.Zone!.Name })
            .Select(g => new ZonePopulationDto(g.Key.ZoneId!, g.Key.Name, g.Count()))
            .OrderByDescending(z => z.PlayerCount)
            .ToListAsync();

        var total = await db.PlayerSessions.AsNoTracking().CountAsync();
        var registeredAccounts = await db.Users.AsNoTracking().CountAsync();
        var totalCharacters = await db.Characters.AsNoTracking().CountAsync();

        var healthReport = await healthCheckService.CheckHealthAsync();
        var healthChecks = healthReport.Entries
            .Select(e => new HealthCheckEntryDto(
                e.Key,
                e.Value.Status.ToString(),
                e.Value.Description ?? e.Value.Exception?.Message))
            .ToList();

        return Results.Ok(new ServerStatusDto(total, sessions, _serverStartedAt, registeredAccounts, totalCharacters, healthChecks));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (Guid Id, string Username) GetActor(ClaimsPrincipal user)
    {
        var idStr    = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? Guid.Empty.ToString();
        var username = user.FindFirstValue(ClaimTypes.Name) ?? "unknown";
        return (Guid.TryParse(idStr, out var id) ? id : Guid.Empty, username);
    }

    private static async Task WriteAuditAsync(
        ApplicationDbContext db,
        ClaimsPrincipal actor,
        Guid? targetAccountId,
        string? targetUsername,
        string action,
        string? details)
    {
        var (actorId, actorUsername) = GetActor(actor);
        db.AdminAuditEntries.Add(new AdminAuditEntry
        {
            ActorAccountId  = actorId,
            ActorUsername   = actorUsername,
            TargetAccountId = targetAccountId,
            TargetUsername  = targetUsername,
            Action          = action,
            Details         = details,
            OccurredAt      = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }
}
