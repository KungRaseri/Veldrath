using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Features.Auth;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Integration tests for the moderation action endpoints in
/// <see cref="Veldrath.Server.Features.Admin.AdminEndpoints"/>: kick, ban, unban, mute, unmute and broadcast.
/// Each test checks the status code, the resulting account state and the audit entry the action must leave behind,
/// because the audit trail is the endpoint's second deliverable.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class AdminModerationActionEndpointTests(WebAppFactory factory)
{
    private async Task<(HttpClient Client, Guid AccountId)> ClientAsync(string username, params string[] roles)
    {
        var (token, accountId) = await factory.CreateAccountTokenAsync(username, roles);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, accountId);
    }

    /// <summary>
    /// Builds a client for an account that holds only the supplied <c>permission</c> claims and no role, so the
    /// moderation tests can show that splitting role mutation onto <c>admin.roles</c> left moderation intact.
    /// </summary>
    private async Task<HttpClient> PermissionOnlyClientAsync(string username, params string[] permissions)
    {
        var (_, accountId) = await factory.CreateAccountTokenAsync(username);

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PlayerAccount>>();
            var user = await userManager.FindByNameAsync(username);
            user.Should().NotBeNull($"the test account {username} should exist");

            foreach (var permission in permissions)
            {
                var added = await userManager.AddClaimAsync(
                    user!,
                    new Claim(RolePermissionResolver.PermissionClaimType, permission));
                added.Succeeded.Should().BeTrue(string.Join("; ", added.Errors.Select(e => e.Description)));
            }
        }

        // The account's access snapshot may already be cached from an earlier request.
        AccountProvisioningService.Invalidate(accountId);

        var (token, _) = await factory.CreateAccountTokenAsync(username);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<Guid> RegisterTargetAsync(string username) =>
        (await factory.CreateAccountTokenAsync(username)).AccountId;

    private async Task<PlayerAccount> LoadAsync(string username)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PlayerAccount>>();
        var user = await userManager.FindByNameAsync(username);
        user.Should().NotBeNull($"the test account {username} should exist");
        return user!;
    }

    private async Task<List<AdminAuditEntry>> AuditEntriesAsync(string action, Guid targetAccountId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.AdminAuditEntries.AsNoTracking()
            .Where(e => e.Action == action && e.TargetAccountId == targetAccountId)
            .ToListAsync();
    }

    // ── POST /api/admin/players/kick ──────────────────────────────────────────

    [Fact]
    public async Task Kick_WithoutToken_ReturnsUnauthorized()
    {
        var response = await factory.CreateClient()
            .PostAsJsonAsync("/api/admin/players/kick", new KickPlayerRequest(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Kick_PlainUser_ReturnsForbidden()
    {
        var (client, _) = await ClientAsync("AdmMod_KickPlain");

        var response = await client.PostAsJsonAsync("/api/admin/players/kick",
            new KickPlayerRequest(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Kick_UnknownAccount_ReturnsNotFound()
    {
        var (client, _) = await ClientAsync("AdmMod_KickMissing", "Moderator");

        var response = await client.PostAsJsonAsync("/api/admin/players/kick",
            new KickPlayerRequest(Guid.NewGuid(), "griefing"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Kick_ValidTarget_ReturnsOkAndAuditsTheSuppliedReason()
    {
        var targetId = await RegisterTargetAsync("AdmMod_KickTarget");
        var (client, _) = await ClientAsync("AdmMod_Kicker", "Moderator");

        var response = await client.PostAsJsonAsync("/api/admin/players/kick",
            new KickPlayerRequest(targetId, "spamming chat"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AdminActionResponse>();
        body!.Success.Should().BeTrue();
        body.Message.Should().Contain(targetId.ToString());

        var audit = await AuditEntriesAsync("kick", targetId);
        audit.Should().ContainSingle();
        audit[0].ActorUsername.Should().Be("AdmMod_Kicker");
        audit[0].TargetUsername.Should().Be("AdmMod_KickTarget");
        audit[0].Details.Should().Be("spamming chat");
    }

    [Fact]
    public async Task Kick_WithoutReason_UsesTheDefaultDisconnectMessage()
    {
        var targetId = await RegisterTargetAsync("AdmMod_KickNoReason");
        var (client, _) = await ClientAsync("AdmMod_KickerDefault", "Moderator");

        var response = await client.PostAsJsonAsync("/api/admin/players/kick",
            new KickPlayerRequest(targetId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var audit = await AuditEntriesAsync("kick", targetId);
        audit.Should().ContainSingle();
        audit[0].Details.Should().Be("You have been disconnected by an administrator.");
    }

    // ── POST /api/admin/players/ban ───────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(525601)]
    public async Task Ban_DurationOutOfRange_ReturnsBadRequest(int duration)
    {
        var (client, _) = await ClientAsync($"AdmMod_BanBadDuration{duration}", "Admin");

        var response = await client.PostAsJsonAsync("/api/admin/players/ban",
            new BanPlayerRequest(Guid.NewGuid(), "out of range", duration));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("DurationMinutes");
    }

    [Fact]
    public async Task Ban_UnknownAccount_ReturnsNotFound()
    {
        var (client, _) = await ClientAsync("AdmMod_BanMissing", "Admin");

        var response = await client.PostAsJsonAsync("/api/admin/players/ban",
            new BanPlayerRequest(Guid.NewGuid(), "no such player"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Ban_ModeratorWithoutBanPermission_ReturnsForbidden()
    {
        // Moderators hold players.kick/warn but not players.ban.
        var (client, _) = await ClientAsync("AdmMod_BanAsModerator", "Moderator");

        var response = await client.PostAsJsonAsync("/api/admin/players/ban",
            new BanPlayerRequest(Guid.NewGuid(), "not allowed"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task BanPlayer_StillWorks_WithManageUsersOnly()
    {
        // An account whose admin permission is admin.users alone (no admin.roles) must still moderate:
        // gating role mutation behind admin.roles must not narrow anything else.
        var targetId = await RegisterTargetAsync("AdmMod_BanUsersOnlyTarget");
        using var client = await PermissionOnlyClientAsync(
            "AdmMod_UsersOnlyBanner",
            Permissions.ManageUsers,
            Permissions.BanPlayers);

        var response = await client.PostAsJsonAsync("/api/admin/players/ban",
            new BanPlayerRequest(targetId, "still moderated"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var account = await LoadAsync("AdmMod_BanUsersOnlyTarget");
        account.IsBanned.Should().BeTrue();
        account.BanReason.Should().Be("still moderated");
        (await AuditEntriesAsync("ban", targetId)).Should().ContainSingle();
    }

    [Fact]
    public async Task Ban_Permanent_SetsBanFieldsWithoutExpiry()
    {
        var targetId = await RegisterTargetAsync("AdmMod_BanPermanent");
        var (client, _) = await ClientAsync("AdmMod_Banner", "Admin");

        var response = await client.PostAsJsonAsync("/api/admin/players/ban",
            new BanPlayerRequest(targetId, "cheating"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AdminActionResponse>();
        body!.Message.Should().Be($"Account {targetId} banned.");

        var account = await LoadAsync("AdmMod_BanPermanent");
        account.IsBanned.Should().BeTrue();
        account.BannedUntil.Should().BeNull("no duration means a permanent ban");
        account.BanReason.Should().Be("cheating");

        var audit = await AuditEntriesAsync("ban", targetId);
        audit.Should().ContainSingle();
        audit[0].Details.Should().Be("cheating (duration: permanent)");
    }

    [Fact]
    public async Task Ban_Temporary_SetsAnExpiryFromTheDuration()
    {
        var targetId = await RegisterTargetAsync("AdmMod_BanTemporary");
        var (client, _) = await ClientAsync("AdmMod_BannerTemp", "Admin");
        var before = DateTimeOffset.UtcNow;

        var response = await client.PostAsJsonAsync("/api/admin/players/ban",
            new BanPlayerRequest(targetId, "cool off", 30));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var account = await LoadAsync("AdmMod_BanTemporary");
        account.IsBanned.Should().BeTrue();
        account.BannedUntil.Should().NotBeNull();
        account.BannedUntil!.Value.Should().BeCloseTo(before.AddMinutes(30), TimeSpan.FromMinutes(1));

        var audit = await AuditEntriesAsync("ban", targetId);
        audit.Should().ContainSingle();
        audit[0].Details.Should().Be("cool off (duration: 30m)");
    }

    // ── POST /api/admin/players/unban ─────────────────────────────────────────

    [Fact]
    public async Task Unban_UnknownAccount_ReturnsNotFound()
    {
        var (client, _) = await ClientAsync("AdmMod_UnbanMissing", "Admin");

        var response = await client.PostAsJsonAsync("/api/admin/players/unban",
            new UnbanPlayerRequest(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unban_BannedAccount_ClearsEveryBanField()
    {
        var targetId = await RegisterTargetAsync("AdmMod_UnbanTarget");
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PlayerAccount>>();
            var user = await userManager.FindByNameAsync("AdmMod_UnbanTarget");
            user!.IsBanned = true;
            user.BannedUntil = DateTimeOffset.UtcNow.AddDays(3);
            user.BanReason = "old ban";
            (await userManager.UpdateAsync(user)).Succeeded.Should().BeTrue();
        }

        var (client, _) = await ClientAsync("AdmMod_Unbanner", "Admin");
        var response = await client.PostAsJsonAsync("/api/admin/players/unban",
            new UnbanPlayerRequest(targetId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var account = await LoadAsync("AdmMod_UnbanTarget");
        account.IsBanned.Should().BeFalse();
        account.BannedUntil.Should().BeNull();
        account.BanReason.Should().BeNull();

        var audit = await AuditEntriesAsync("unban", targetId);
        audit.Should().ContainSingle();
        audit[0].Details.Should().BeNull("an unban carries no reason");
    }

    // ── POST /api/admin/players/mute ──────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(525601)]
    public async Task Mute_DurationOutOfRange_ReturnsBadRequest(int duration)
    {
        var (client, _) = await ClientAsync($"AdmMod_MuteBadDuration{duration}", "Moderator");

        var response = await client.PostAsJsonAsync("/api/admin/players/mute",
            new MutePlayerRequest(Guid.NewGuid(), "too long", duration));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("DurationMinutes");
    }

    [Fact]
    public async Task Mute_UnknownAccount_ReturnsNotFound()
    {
        var (client, _) = await ClientAsync("AdmMod_MuteMissing", "Moderator");

        var response = await client.PostAsJsonAsync("/api/admin/players/mute",
            new MutePlayerRequest(Guid.NewGuid(), "no such player"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Mute_PlainUser_ReturnsForbidden()
    {
        var (client, _) = await ClientAsync("AdmMod_MutePlain");

        var response = await client.PostAsJsonAsync("/api/admin/players/mute",
            new MutePlayerRequest(Guid.NewGuid(), "not allowed"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Mute_Permanent_SetsMuteFieldsWithoutExpiry()
    {
        var targetId = await RegisterTargetAsync("AdmMod_MutePermanent");
        var (client, _) = await ClientAsync("AdmMod_Muter", "Moderator");

        var response = await client.PostAsJsonAsync("/api/admin/players/mute",
            new MutePlayerRequest(targetId, "slurs"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var account = await LoadAsync("AdmMod_MutePermanent");
        account.IsMuted.Should().BeTrue();
        account.MutedUntil.Should().BeNull();
        account.MuteReason.Should().Be("slurs");

        var audit = await AuditEntriesAsync("mute", targetId);
        audit.Should().ContainSingle();
        audit[0].Details.Should().Be("slurs (duration: permanent)");
    }

    [Fact]
    public async Task Mute_Temporary_SetsAnExpiryAndAuditsTheDuration()
    {
        var targetId = await RegisterTargetAsync("AdmMod_MuteTemporary");
        var (client, _) = await ClientAsync("AdmMod_MuterTemp", "Moderator");
        var before = DateTimeOffset.UtcNow;

        var response = await client.PostAsJsonAsync("/api/admin/players/mute",
            new MutePlayerRequest(targetId, null, 45));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var account = await LoadAsync("AdmMod_MuteTemporary");
        account.IsMuted.Should().BeTrue();
        account.MutedUntil.Should().NotBeNull();
        account.MutedUntil!.Value.Should().BeCloseTo(before.AddMinutes(45), TimeSpan.FromMinutes(1));

        var audit = await AuditEntriesAsync("mute", targetId);
        audit.Should().ContainSingle();
        audit[0].Details.Should().Be("(no reason) (duration: 45m)");
    }

    // ── POST /api/admin/players/unmute ────────────────────────────────────────

    [Fact]
    public async Task Unmute_UnknownAccount_ReturnsNotFound()
    {
        var (client, _) = await ClientAsync("AdmMod_UnmuteMissing", "Moderator");

        var response = await client.PostAsJsonAsync("/api/admin/players/unmute",
            new UnmutePlayerRequest(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unmute_MutedAccount_ClearsEveryMuteField()
    {
        var targetId = await RegisterTargetAsync("AdmMod_UnmuteTarget");
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PlayerAccount>>();
            var user = await userManager.FindByNameAsync("AdmMod_UnmuteTarget");
            user!.IsMuted = true;
            user.MutedUntil = DateTimeOffset.UtcNow.AddHours(1);
            user.MuteReason = "old mute";
            (await userManager.UpdateAsync(user)).Succeeded.Should().BeTrue();
        }

        var (client, _) = await ClientAsync("AdmMod_Unmuter", "Moderator");
        var response = await client.PostAsJsonAsync("/api/admin/players/unmute",
            new UnmutePlayerRequest(targetId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var account = await LoadAsync("AdmMod_UnmuteTarget");
        account.IsMuted.Should().BeFalse();
        account.MutedUntil.Should().BeNull();
        account.MuteReason.Should().BeNull();

        var audit = await AuditEntriesAsync("unmute", targetId);
        audit.Should().ContainSingle();
        audit[0].Details.Should().BeNull();
    }

    // ── POST /api/admin/broadcast ─────────────────────────────────────────────

    [Fact]
    public async Task Broadcast_PlainUser_ReturnsForbidden()
    {
        var (client, _) = await ClientAsync("AdmMod_BroadcastPlain");

        var response = await client.PostAsJsonAsync("/api/admin/broadcast",
            new BroadcastAnnouncementRequest("Server restarting"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Broadcast_WithSendAnnouncements_ReturnsOkAndAuditsTheSeverity()
    {
        // Moderators hold admin.announce.
        var (client, _) = await ClientAsync("AdmMod_Broadcaster", "Moderator");

        var response = await client.PostAsJsonAsync("/api/admin/broadcast",
            new BroadcastAnnouncementRequest("Maintenance at 02:00 UTC", "critical"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AdminActionResponse>();
        body!.Success.Should().BeTrue();
        body.Message.Should().Contain("broadcast");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entry = db.AdminAuditEntries.AsNoTracking()
            .Where(e => e.Action == "broadcast" && e.ActorUsername == "AdmMod_Broadcaster")
            .ToList()
            .Should().ContainSingle().Subject;
        entry.TargetAccountId.Should().BeNull("a broadcast has no target account");
        entry.TargetUsername.Should().BeNull();
        entry.Details.Should().Be("[critical] Maintenance at 02:00 UTC");
    }
}
