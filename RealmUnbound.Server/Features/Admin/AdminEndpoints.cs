using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RealmUnbound.Contracts.Admin;
using RealmUnbound.Contracts.Connection;
using RealmUnbound.Server.Data;
using RealmUnbound.Server.Data.Entities;
using RealmUnbound.Server.Hubs;

namespace RealmUnbound.Server.Features.Admin;

/// <summary>
/// Minimal API endpoints for server administration.
/// All endpoints in this group require the <c>manage_users</c> permission claim,
/// which is granted by default to the <c>Admin</c> role.
///
/// GET    /api/admin/users                            — list accounts (paginated, searchable)
/// GET    /api/admin/users/{id}                       — account detail with roles + effective permissions
/// POST   /api/admin/users/{id}/roles                 — assign a role to an account
/// DELETE /api/admin/users/{id}/roles/{role}          — revoke a role from an account
/// POST   /api/admin/users/{id}/permissions           — grant a per-user permission claim
/// DELETE /api/admin/users/{id}/permissions/{perm}    — revoke a per-user permission claim
/// POST   /api/admin/players/kick                     — forcibly disconnect an active session
/// POST   /api/admin/players/ban                      — ban an account and kick active sessions
/// POST   /api/admin/players/unban                    — lift an existing ban
/// POST   /api/admin/broadcast                        — send announcement to all connected clients
/// </summary>
public static class AdminEndpoints
{
    /// <summary>Registers all admin endpoints on the provided route builder.</summary>
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin")
            .WithTags("Admin")
            .RequireAuthorization(Auth.Permissions.ManageUsers);

        group.MapGet("/users",                                       ListUsersAsync);
        group.MapGet("/users/{id:guid}",                             GetUserAsync);
        group.MapPost("/users/{id:guid}/roles",                      AssignRoleAsync);
        group.MapDelete("/users/{id:guid}/roles/{role}",             RevokeRoleAsync);
        group.MapPost("/users/{id:guid}/permissions",                GrantPermissionAsync);
        group.MapDelete("/users/{id:guid}/permissions/{permission}", RevokePermissionAsync);
        group.MapPost("/players/kick",                               KickPlayerAsync).RequireAuthorization(Auth.Permissions.KickPlayers);
        group.MapPost("/players/ban",                                BanPlayerAsync).RequireAuthorization(Auth.Permissions.BanPlayers);
        group.MapPost("/players/unban",                              UnbanPlayerAsync).RequireAuthorization(Auth.Permissions.BanPlayers);
        group.MapPost("/broadcast",                                  BroadcastAsync).RequireAuthorization(Auth.Permissions.SendAnnouncements);

        return app;
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private static async Task<IResult> ListUsersAsync(
        [FromServices] UserManager<PlayerAccount> userManager,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var skip = (Math.Max(1, page) - 1) * pageSize;

        var query = userManager.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u =>
                u.UserName!.Contains(search) || (u.Email != null && u.Email.Contains(search)));

        var total = await query.CountAsync();
        var users = await query.OrderBy(u => u.UserName).Skip(skip).Take(pageSize).ToListAsync();

        var items = new List<PlayerSummaryDto>(users.Count);
        foreach (var u in users)
        {
            var roles = await userManager.GetRolesAsync(u);
            var isBanned = u.IsBanned && (u.BannedUntil is null || u.BannedUntil > DateTimeOffset.UtcNow);
            items.Add(new PlayerSummaryDto(u.Id, u.UserName!, u.Email, [.. roles],
                isBanned, u.BannedUntil, u.CreatedAt, u.LastSeenAt));
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

        var roles = (await userManager.GetRolesAsync(user)).ToList();
        var userClaims = await userManager.GetClaimsAsync(user);
        var userPerms = userClaims
            .Where(c => c.Type == "permission")
            .Select(c => c.Value)
            .ToList();

        var allPerms = new HashSet<string>(userPerms, StringComparer.Ordinal);
        foreach (var roleName in roles)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null) continue;
            var rc = await roleManager.GetClaimsAsync(role);
            foreach (var c in rc.Where(c => c.Type == "permission"))
                allPerms.Add(c.Value);
        }

        var isBanned = user.IsBanned && (user.BannedUntil is null || user.BannedUntil > DateTimeOffset.UtcNow);
        var dto = new PlayerDetailDto(
            user.Id, user.UserName!, user.Email,
            roles,
            [.. allPerms],
            userPerms,
            isBanned, user.BannedUntil, user.BanReason,
            user.CreatedAt, user.LastSeenAt);

        return Results.Ok(dto);
    }

    private static async Task<IResult> AssignRoleAsync(
        Guid id,
        [FromBody] AssignRoleRequest request,
        [FromServices] UserManager<PlayerAccount> userManager)
    {
        if (!Auth.Roles.All.Contains(request.Role, StringComparer.OrdinalIgnoreCase))
            return Results.BadRequest(new AdminActionResponse(false, $"Unknown role '{request.Role}'."));

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return Results.NotFound();

        if (await userManager.IsInRoleAsync(user, request.Role))
            return Results.Ok(new AdminActionResponse(true, $"Account already has role '{request.Role}'."));

        var result = await userManager.AddToRoleAsync(user, request.Role);
        return result.Succeeded
            ? Results.Ok(new AdminActionResponse(true, $"Role '{request.Role}' assigned."))
            : Results.BadRequest(new AdminActionResponse(false, string.Join("; ", result.Errors.Select(e => e.Description))));
    }

    private static async Task<IResult> RevokeRoleAsync(
        Guid id,
        string role,
        [FromServices] UserManager<PlayerAccount> userManager)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return Results.NotFound();

        if (!await userManager.IsInRoleAsync(user, role))
            return Results.Ok(new AdminActionResponse(true, $"Account does not have role '{role}'."));

        var result = await userManager.RemoveFromRoleAsync(user, role);
        return result.Succeeded
            ? Results.Ok(new AdminActionResponse(true, $"Role '{role}' revoked."))
            : Results.BadRequest(new AdminActionResponse(false, string.Join("; ", result.Errors.Select(e => e.Description))));
    }

    private static async Task<IResult> GrantPermissionAsync(
        Guid id,
        [FromBody] GrantPermissionRequest request,
        [FromServices] UserManager<PlayerAccount> userManager)
    {
        if (!Auth.Permissions.All.Contains(request.Permission, StringComparer.OrdinalIgnoreCase))
            return Results.BadRequest(new AdminActionResponse(false, $"Unknown permission '{request.Permission}'."));

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return Results.NotFound();

        var existing = await userManager.GetClaimsAsync(user);
        if (existing.Any(c => c.Type == "permission" && c.Value == request.Permission))
            return Results.Ok(new AdminActionResponse(true, "Permission already granted."));

        var result = await userManager.AddClaimAsync(user, new Claim("permission", request.Permission));
        return result.Succeeded
            ? Results.Ok(new AdminActionResponse(true, $"Permission '{request.Permission}' granted."))
            : Results.BadRequest(new AdminActionResponse(false, string.Join("; ", result.Errors.Select(e => e.Description))));
    }

    private static async Task<IResult> RevokePermissionAsync(
        Guid id,
        string permission,
        [FromServices] UserManager<PlayerAccount> userManager)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return Results.NotFound();

        var existing = await userManager.GetClaimsAsync(user);
        var claim = existing.FirstOrDefault(c => c.Type == "permission" && c.Value == permission);
        if (claim is null)
            return Results.Ok(new AdminActionResponse(true, "Permission not found on account."));

        var result = await userManager.RemoveClaimAsync(user, claim);
        return result.Succeeded
            ? Results.Ok(new AdminActionResponse(true, $"Permission '{permission}' revoked."))
            : Results.BadRequest(new AdminActionResponse(false, string.Join("; ", result.Errors.Select(e => e.Description))));
    }

    private static async Task<IResult> KickPlayerAsync(
        [FromBody] KickPlayerRequest request,
        [FromServices] UserManager<PlayerAccount> userManager,
        [FromServices] IHubContext<GameHub> hub)
    {
        var user = await userManager.FindByIdAsync(request.AccountId.ToString());
        if (user is null) return Results.NotFound();

        var reason = request.Reason ?? "You have been disconnected by an administrator.";
        await hub.Clients
            .Group($"account:{request.AccountId}")
            .SendAsync("OnKicked", new KickedPayload(reason));

        return Results.Ok(new AdminActionResponse(true, $"Kick signal sent to account {request.AccountId}."));
    }

    private static async Task<IResult> BanPlayerAsync(
        [FromBody] BanPlayerRequest request,
        [FromServices] UserManager<PlayerAccount> userManager,
        [FromServices] ApplicationDbContext db,
        [FromServices] IHubContext<GameHub> hub)
    {
        var user = await userManager.FindByIdAsync(request.AccountId.ToString());
        if (user is null) return Results.NotFound();

        user.IsBanned   = true;
        user.BanReason  = request.Reason;
        user.BannedUntil = request.DurationMinutes.HasValue
            ? DateTimeOffset.UtcNow.AddMinutes(request.DurationMinutes.Value)
            : null;

        await userManager.UpdateAsync(user);

        var banMessage = request.DurationMinutes.HasValue
            ? $"You have been banned for {request.DurationMinutes} minutes. Reason: {request.Reason}"
            : $"You have been permanently banned. Reason: {request.Reason}";

        await hub.Clients
            .Group($"account:{request.AccountId}")
            .SendAsync("OnKicked", new KickedPayload(banMessage));

        return Results.Ok(new AdminActionResponse(true, $"Account {request.AccountId} banned."));
    }

    private static async Task<IResult> UnbanPlayerAsync(
        Guid id,
        [FromServices] UserManager<PlayerAccount> userManager)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return Results.NotFound();

        user.IsBanned    = false;
        user.BannedUntil = null;
        user.BanReason   = null;
        await userManager.UpdateAsync(user);

        return Results.Ok(new AdminActionResponse(true, $"Account {id} unbanned."));
    }

    private static async Task<IResult> BroadcastAsync(
        [FromBody] BroadcastAnnouncementRequest request,
        [FromServices] IHubContext<GameHub> hub)
    {
        await hub.Clients.All.SendAsync("OnAnnouncement",
            new AnnouncementPayload(request.Message, request.Severity));

        return Results.Ok(new AdminActionResponse(true, "Announcement broadcast to all connected clients."));
    }
}
