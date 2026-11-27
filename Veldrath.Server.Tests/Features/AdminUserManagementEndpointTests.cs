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

/// <summary>One page of the anonymous payload returned by <c>GET /api/admin/users</c>.</summary>
internal sealed record AdminUserPage(int Total, int Page, int PageSize, List<PlayerSummaryDto> Items);

/// <summary>
/// Integration tests for the account, role and permission management endpoints in
/// <see cref="Veldrath.Server.Features.Admin.AdminEndpoints"/>: listing/filtering accounts, reading one account's
/// effective permissions, and the four role/permission mutations — including their audit-trail side effects and
/// the RBAC boundary between <c>/api/admin</c> (manage_users) and <c>/api/mod</c> (view_players).
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class AdminUserManagementEndpointTests(WebAppFactory factory)
{
    private const string AdminUsers = "/api/admin/users";
    private const string AdminRoles = "/api/admin/roles/users";

    private async Task<HttpClient> AdminClientAsync(string username) =>
        await AuthedClientAsync(username, "Admin");

    private async Task<HttpClient> ModeratorClientAsync(string username) =>
        await AuthedClientAsync(username, "Moderator");

    private async Task<HttpClient> PlainClientAsync(string username) =>
        await AuthedClientAsync(username);

    private async Task<HttpClient> AuthedClientAsync(string username, params string[] roles)
    {
        var (token, _) = await factory.CreateAccountTokenAsync(username, roles);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Builds a client for an account that holds only the supplied <c>permission</c> claims and no role, so a test
    /// can prove that the ability to administer accounts (<c>admin.users</c>) and the ability to hand out
    /// authority (<c>admin.roles</c>) are separate grants.
    /// </summary>
    private async Task<(HttpClient Client, Guid AccountId)> PermissionOnlyClientAsync(
        string username,
        params string[] permissions)
    {
        var (_, accountId) = await factory.CreateAccountTokenAsync(username);

        await WithAccountAsync(username, async (userManager, user) =>
        {
            foreach (var permission in permissions)
            {
                var added = await userManager.AddClaimAsync(
                    user,
                    new Claim(RolePermissionResolver.PermissionClaimType, permission));
                added.Succeeded.Should().BeTrue(string.Join("; ", added.Errors.Select(e => e.Description)));
            }
        });

        // The account's access snapshot may already be cached from an earlier request.
        AccountProvisioningService.Invalidate(accountId);

        var (token, _) = await factory.CreateAccountTokenAsync(username);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, accountId);
    }

    private async Task<List<string>> PermissionsOfAsync(string username)
    {
        var permissions = new List<string>();
        await WithAccountAsync(username, async (um, user) =>
            permissions.AddRange((await um.GetClaimsAsync(user))
                .Where(c => c.Type == RolePermissionResolver.PermissionClaimType)
                .Select(c => c.Value)));
        return permissions;
    }

    private async Task<Guid> RegisterAsync(string username, params string[] roles) =>
        (await factory.CreateAccountTokenAsync(username, roles)).AccountId;

    /// <summary>Runs <paramref name="act"/> against a fresh scope's UserManager for the named account.</summary>
    private async Task WithAccountAsync(string username, Func<UserManager<PlayerAccount>, PlayerAccount, Task> act)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PlayerAccount>>();
        var user = await userManager.FindByNameAsync(username);
        user.Should().NotBeNull($"the test account {username} should exist");
        await act(userManager, user!);
    }

    private async Task<List<string>> RolesOfAsync(string username)
    {
        var roles = new List<string>();
        await WithAccountAsync(username, async (um, user) => roles.AddRange(await um.GetRolesAsync(user)));
        return roles;
    }

    private async Task<List<AdminAuditEntry>> AuditEntriesAsync(string action, Guid targetAccountId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.AdminAuditEntries.AsNoTracking()
            .Where(e => e.Action == action && e.TargetAccountId == targetAccountId)
            .ToListAsync();
    }

    // ── GET /api/admin/users ───────────────────────────────────────────────────

    [Fact]
    public async Task ListUsers_WithoutToken_ReturnsUnauthorized()
    {
        var response = await factory.CreateClient().GetAsync(AdminUsers);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListUsers_PlainUser_ReturnsForbidden()
    {
        using var client = await PlainClientAsync("AdmUsr_ListPlain");

        var response = await client.GetAsync(AdminUsers);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListUsers_Moderator_IsForbidden_ByTheManageUsersGroup()
    {
        // Moderators hold view_players, not admin.users — the admin group must stay closed to them.
        using var client = await ModeratorClientAsync("AdmUsr_ListModerator");

        var response = await client.GetAsync(AdminUsers);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListUsers_Admin_ReturnsPageContainingTheAccount()
    {
        await RegisterAsync("AdmUsr_Listed");
        using var client = await AdminClientAsync("AdmUsr_ListAdmin");

        var page = await client.GetFromJsonAsync<AdminUserPage>($"{AdminUsers}?search=AdmUsr_Listed");

        page.Should().NotBeNull();
        page!.Total.Should().Be(1);
        page.Page.Should().Be(1);
        page.PageSize.Should().Be(25, "25 is the documented page size default");
        var item = page.Items.Should().ContainSingle().Subject;
        item.Username.Should().Be("AdmUsr_Listed");
        item.Email.Should().Be("admusr_listed@test.com");
        item.IsBanned.Should().BeFalse();
        item.IsMuted.Should().BeFalse();
        item.Roles.Should().BeEmpty();
    }

    [Fact]
    public async Task ListUsers_Search_MatchesTheEmailAddressToo()
    {
        await RegisterAsync("AdmUsr_EmailMatch");
        using var client = await AdminClientAsync("AdmUsr_EmailAdmin");

        var page = await client.GetFromJsonAsync<AdminUserPage>($"{AdminUsers}?search=admusr_emailmatch@test");

        page!.Items.Should().ContainSingle(i => i.Username == "AdmUsr_EmailMatch");
    }

    [Fact]
    public async Task ListUsers_Search_IsCaseInsensitive()
    {
        await RegisterAsync("AdmUsr_CaseSearch");
        using var client = await AdminClientAsync("AdmUsr_CaseAdmin");

        var page = await client.GetFromJsonAsync<AdminUserPage>($"{AdminUsers}?search=admusr_casesearch");

        page!.Total.Should().Be(1);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(500, 100)]
    public async Task ListUsers_PageSize_IsClamped(int requested, int expected)
    {
        using var client = await AdminClientAsync($"AdmUsr_Clamp{expected}_{requested}");

        var page = await client.GetFromJsonAsync<AdminUserPage>($"{AdminUsers}?pageSize={requested}");

        page!.PageSize.Should().Be(expected);
    }

    [Fact]
    public async Task ListUsers_PageBeyondTheEnd_ReturnsAnEmptySliceWithTheRealTotal()
    {
        await RegisterAsync("AdmUsr_DeepPage");
        using var client = await AdminClientAsync("AdmUsr_DeepPageAdmin");

        var page = await client.GetFromJsonAsync<AdminUserPage>(
            $"{AdminUsers}?search=AdmUsr_DeepPage&page=50&pageSize=10");

        page!.Page.Should().Be(50);
        page.Total.Should().BeGreaterThanOrEqualTo(1);
        page.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ListUsers_RoleFilter_ReturnsOnlyMembersOfThatRole()
    {
        await RegisterAsync("AdmUsr_RoleFilter", "Curator");
        using var client = await AdminClientAsync("AdmUsr_RoleFilterAdmin");

        var page = await client.GetFromJsonAsync<AdminUserPage>($"{AdminUsers}?role=Curator&search=AdmUsr_RoleFilter");

        var item = page!.Items.Should().ContainSingle().Subject;
        item.Roles.Should().Contain("Curator");
    }

    [Fact]
    public async Task ListUsers_BannedFilter_ReturnsTheBannedAccount()
    {
        await RegisterAsync("AdmUsr_Banned");
        await WithAccountAsync("AdmUsr_Banned", async (um, user) =>
        {
            user.IsBanned = true;
            user.BannedUntil = null;
            user.BanReason = "list filter test";
            (await um.UpdateAsync(user)).Succeeded.Should().BeTrue();
        });
        using var client = await AdminClientAsync("AdmUsr_BannedAdmin");

        var banned = await client.GetFromJsonAsync<AdminUserPage>($"{AdminUsers}?status=banned&search=AdmUsr_Banned");
        var active = await client.GetFromJsonAsync<AdminUserPage>($"{AdminUsers}?status=active&search=AdmUsr_Banned");

        banned!.Total.Should().Be(1);
        banned.Items[0].IsBanned.Should().BeTrue();
        active!.Items.Should().NotContain(i => i.Username == "AdmUsr_Banned",
            "a permanently banned account is not active");
    }

    [Fact]
    public async Task ListUsers_BannedFilter_IgnoresAnExpiredBan()
    {
        await RegisterAsync("AdmUsr_ExpiredBan");
        await WithAccountAsync("AdmUsr_ExpiredBan", async (um, user) =>
        {
            user.IsBanned = true;
            user.BannedUntil = DateTimeOffset.UtcNow.AddMinutes(-30);
            (await um.UpdateAsync(user)).Succeeded.Should().BeTrue();
        });
        using var client = await AdminClientAsync("AdmUsr_ExpiredBanAdmin");

        var banned = await client.GetFromJsonAsync<AdminUserPage>(
            $"{AdminUsers}?status=banned&search=AdmUsr_ExpiredBan");
        var active = await client.GetFromJsonAsync<AdminUserPage>(
            $"{AdminUsers}?status=active&search=AdmUsr_ExpiredBan");

        banned!.Items.Should().NotContain(i => i.Username == "AdmUsr_ExpiredBan",
            "a ban whose BannedUntil has passed is no longer in force");
        active!.Items.Should().Contain(i => i.Username == "AdmUsr_ExpiredBan");
        active.Items.Single(i => i.Username == "AdmUsr_ExpiredBan").IsBanned.Should().BeFalse();
    }

    [Fact]
    public async Task ListUsers_MutedFilter_ReturnsTheMutedAccountWithItsExpiry()
    {
        await RegisterAsync("AdmUsr_Muted");
        var until = DateTimeOffset.UtcNow.AddHours(2);
        await WithAccountAsync("AdmUsr_Muted", async (um, user) =>
        {
            user.IsMuted = true;
            user.MutedUntil = until;
            user.MuteReason = "list filter test";
            (await um.UpdateAsync(user)).Succeeded.Should().BeTrue();
        });
        using var client = await AdminClientAsync("AdmUsr_MutedAdmin");

        var page = await client.GetFromJsonAsync<AdminUserPage>($"{AdminUsers}?status=muted&search=AdmUsr_Muted");

        var item = page!.Items.Should().ContainSingle().Subject;
        item.IsMuted.Should().BeTrue();
        item.Username.Should().Be("AdmUsr_Muted");
    }

    [Fact]
    public async Task ListUsers_SortByWarnCount_OrdersDescending()
    {
        await RegisterAsync("AdmUsr_SortWarn");
        await WithAccountAsync("AdmUsr_SortWarn", async (um, user) =>
        {
            user.WarnCount = 7;
            (await um.UpdateAsync(user)).Succeeded.Should().BeTrue();
        });
        await RegisterAsync("AdmUsr_SortWarnLow");
        using var client = await AdminClientAsync("AdmUsr_SortWarnAdmin");

        var page = await client.GetFromJsonAsync<AdminUserPage>($"{AdminUsers}?sort=warncount&search=AdmUsr_SortWarn");

        page!.Items.Should().HaveCountGreaterThanOrEqualTo(2);
        page.Items[0].Username.Should().Be("AdmUsr_SortWarn");
        page.Items.Select(i => i.WarnCount).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task ListUsers_SortByLastSeen_OrdersDescendingAndNullsLast()
    {
        await RegisterAsync("AdmUsr_SeenRecent");
        await WithAccountAsync("AdmUsr_SeenRecent", async (um, user) =>
        {
            user.LastSeenAt = DateTimeOffset.UtcNow;
            (await um.UpdateAsync(user)).Succeeded.Should().BeTrue();
        });
        await RegisterAsync("AdmUsr_SeenOld");
        await WithAccountAsync("AdmUsr_SeenOld", async (um, user) =>
        {
            user.LastSeenAt = DateTimeOffset.UtcNow.AddDays(-10);
            (await um.UpdateAsync(user)).Succeeded.Should().BeTrue();
        });
        using var client = await AdminClientAsync("AdmUsr_SeenAdmin");

        var page = await client.GetFromJsonAsync<AdminUserPage>($"{AdminUsers}?sort=lastseen&search=AdmUsr_Seen");

        page!.Items.Select(i => i.Username).Should().ContainInOrder("AdmUsr_SeenRecent", "AdmUsr_SeenOld");
    }

    // ── GET /api/admin/users/{id} ─────────────────────────────────────────────

    [Fact]
    public async Task GetUser_UnknownAccount_ReturnsNotFound()
    {
        using var client = await AdminClientAsync("AdmUsr_GetUnknown");

        var response = await client.GetAsync($"{AdminUsers}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetUser_PlainUser_ReturnsForbidden()
    {
        var (_, accountId) = await factory.CreateAccountTokenAsync("AdmUsr_GetPlain");
        using var client = await PlainClientAsync("AdmUsr_GetPlain");

        var response = await client.GetAsync($"{AdminUsers}/{accountId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetUser_ReturnsRolesAndRoleDerivedEffectivePermissions()
    {
        var accountId = await RegisterAsync("AdmUsr_DetailModerator", "Moderator");
        using var client = await AdminClientAsync("AdmUsr_DetailAdmin");

        var detail = await client.GetFromJsonAsync<PlayerDetailDto>($"{AdminUsers}/{accountId}");

        detail.Should().NotBeNull();
        detail!.Id.Should().Be(accountId);
        detail.Username.Should().Be("AdmUsr_DetailModerator");
        detail.Roles.Should().Contain("Moderator");
        detail.EffectivePermissions.Should().Contain("players.kick").And.Contain("players.view");
        detail.UserPermissions.Should().BeEmpty("nothing was granted to the account directly");
        detail.IsBanned.Should().BeFalse();
        detail.WarnCount.Should().Be(0);
    }

    [Fact]
    public async Task GetUser_UnionsDirectPermissionClaimsIntoEffectivePermissions()
    {
        var accountId = await RegisterAsync("AdmUsr_DetailDirectPerm", "Moderator");
        await WithAccountAsync("AdmUsr_DetailDirectPerm", async (um, user) =>
            (await um.AddClaimAsync(user, new Claim("permission", "content.review"))).Succeeded.Should().BeTrue());
        using var client = await AdminClientAsync("AdmUsr_DetailDirectAdmin");

        var detail = await client.GetFromJsonAsync<PlayerDetailDto>($"{AdminUsers}/{accountId}");

        detail!.UserPermissions.Should().ContainSingle().Which.Should().Be("content.review");
        detail.EffectivePermissions.Should().Contain("content.review")
            .And.Contain("players.kick", "role-derived grants stay in the effective set");
    }

    [Fact]
    public async Task GetUser_ReportsBanAndMuteState()
    {
        var accountId = await RegisterAsync("AdmUsr_DetailModState");
        var bannedUntil = DateTimeOffset.UtcNow.AddDays(1);
        var mutedUntil = DateTimeOffset.UtcNow.AddHours(1);
        await WithAccountAsync("AdmUsr_DetailModState", async (um, user) =>
        {
            user.IsBanned = true;
            user.BannedUntil = bannedUntil;
            user.BanReason = "detail state";
            user.IsMuted = true;
            user.MutedUntil = mutedUntil;
            user.MuteReason = "chat spam";
            (await um.UpdateAsync(user)).Succeeded.Should().BeTrue();
        });
        using var client = await AdminClientAsync("AdmUsr_DetailModStateAdmin");

        var detail = await client.GetFromJsonAsync<PlayerDetailDto>($"{AdminUsers}/{accountId}");

        detail!.IsBanned.Should().BeTrue();
        detail.BanReason.Should().Be("detail state");
        detail.BannedUntil.Should().BeCloseTo(bannedUntil, TimeSpan.FromSeconds(1));
        detail.IsMuted.Should().BeTrue();
        detail.MuteReason.Should().Be("chat spam");
        detail.MutedUntil.Should().BeCloseTo(mutedUntil, TimeSpan.FromSeconds(1));
    }

    // ── POST /api/admin/roles/users/{id}/roles ────────────────────────────────

    [Fact]
    public async Task AssignRole_UnknownRole_ReturnsBadRequest()
    {
        var accountId = await RegisterAsync("AdmUsr_AssignBadRole");
        using var client = await AdminClientAsync("AdmUsr_AssignBadRoleAdmin");

        var response = await client.PostAsJsonAsync($"{AdminRoles}/{accountId}/roles",
            new AssignRoleRequest("SupremeOverlord"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<AdminActionResponse>();
        body!.Success.Should().BeFalse();
        body.Message.Should().Contain("SupremeOverlord");
    }

    [Fact]
    public async Task AssignRole_UnknownAccount_ReturnsNotFound()
    {
        using var client = await AdminClientAsync("AdmUsr_AssignNoAccount");

        var response = await client.PostAsJsonAsync($"{AdminRoles}/{Guid.NewGuid()}/roles",
            new AssignRoleRequest("Curator"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AssignRole_NewRole_PersistsTheRoleAndWritesAudit()
    {
        var accountId = await RegisterAsync("AdmUsr_AssignNew");
        using var client = await AdminClientAsync("AdmUsr_AssignNewAdmin");

        var response = await client.PostAsJsonAsync($"{AdminRoles}/{accountId}/roles",
            new AssignRoleRequest("Curator"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AdminActionResponse>();
        body!.Success.Should().BeTrue();
        body.Message.Should().Be("Role 'Curator' assigned.");

        (await RolesOfAsync("AdmUsr_AssignNew")).Should().Contain("Curator");

        var audit = await AuditEntriesAsync("assign_role", accountId);
        audit.Should().ContainSingle();
        audit[0].ActorUsername.Should().Be("AdmUsr_AssignNewAdmin");
        audit[0].TargetUsername.Should().Be("AdmUsr_AssignNew");
        audit[0].Details.Should().Be("Curator");
    }

    [Fact]
    public async Task AssignRole_AlreadyAssigned_IsIdempotent()
    {
        var accountId = await RegisterAsync("AdmUsr_AssignAgain", "Curator");
        using var client = await AdminClientAsync("AdmUsr_AssignAgainAdmin");

        var response = await client.PostAsJsonAsync($"{AdminRoles}/{accountId}/roles",
            new AssignRoleRequest("Curator"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AdminActionResponse>();
        body!.Success.Should().BeTrue();
        body.Message.Should().Contain("already has role");
        (await RolesOfAsync("AdmUsr_AssignAgain")).Count(r => r == "Curator").Should().Be(1);
        (await AuditEntriesAsync("assign_role", accountId)).Should().BeEmpty("a no-op must not write an audit entry");
    }

    [Fact]
    public async Task AssignRole_PlainUser_ReturnsForbidden()
    {
        using var client = await PlainClientAsync("AdmUsr_AssignPlain");

        var response = await client.PostAsJsonAsync($"{AdminRoles}/{Guid.NewGuid()}/roles",
            new AssignRoleRequest("Admin"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AssignRole_IsForbidden_ForAnAccountWithManageUsersOnly()
    {
        var (client, _) = await PermissionOnlyClientAsync("AdmUsr_UsersOnly", Permissions.ManageUsers);
        using var ownedClient = client;
        var targetId = await RegisterAsync("AdmUsr_UsersOnlyTarget");

        var response = await client.PostAsJsonAsync($"{AdminRoles}/{targetId}/roles",
            new AssignRoleRequest("Admin"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "admin.users administers accounts, it does not hand out authority");
        (await RolesOfAsync("AdmUsr_UsersOnlyTarget")).Should().BeEmpty();
        (await AuditEntriesAsync("assign_role", targetId)).Should().BeEmpty();
    }

    [Fact]
    public async Task AssignRole_IsAllowed_ForAnAccountWithManageRoles()
    {
        var (client, _) = await PermissionOnlyClientAsync("AdmUsr_RolesOnly", Permissions.ManageRoles);
        using var ownedClient = client;
        var targetId = await RegisterAsync("AdmUsr_RolesOnlyTarget");

        var response = await client.PostAsJsonAsync($"{AdminRoles}/{targetId}/roles",
            new AssignRoleRequest("Curator"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await RolesOfAsync("AdmUsr_RolesOnlyTarget")).Should().Contain("Curator");
    }

    // ── GET /api/admin/roles/users/{id} ───────────────────────────────────────

    [Fact]
    public async Task GetUserAuthority_IsAllowed_ForAnAccountWithManageRoles()
    {
        var (client, _) = await PermissionOnlyClientAsync("AdmUsr_AuthorityRead", Permissions.ManageRoles);
        using var ownedClient = client;
        var targetId = await RegisterAsync("AdmUsr_AuthorityTarget", "Curator");

        var response = await client.GetAsync($"{AdminRoles}/{targetId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "a role manager has to see what an account already holds before changing it");
        var dto = await response.Content.ReadFromJsonAsync<AccountAuthorityDto>();
        dto.Should().NotBeNull();
        dto!.Id.Should().Be(targetId);
        dto.Username.Should().Be("AdmUsr_AuthorityTarget");
        dto.Roles.Should().Contain("Curator");
        dto.UserPermissions.Should().BeEmpty();
        dto.EffectivePermissions.Should().Contain(Permissions.ReviewSubmissions,
            "the read reports the role's permission claims, not only the account's own");
    }

    [Fact]
    public async Task GetUserAuthority_IsForbidden_ForAnAccountWithManageUsersOnly()
    {
        var (client, _) = await PermissionOnlyClientAsync("AdmUsr_AuthorityUsersOnly", Permissions.ManageUsers);
        using var ownedClient = client;
        var targetId = await RegisterAsync("AdmUsr_AuthorityUsersTarget");

        var response = await client.GetAsync($"{AdminRoles}/{targetId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "reading authority is part of the role-management surface, not account administration");
    }

    [Fact]
    public async Task GetUserAuthority_UnknownAccount_ReturnsNotFound()
    {
        var (client, _) = await PermissionOnlyClientAsync("AdmUsr_AuthorityUnknown", Permissions.ManageRoles);
        using var ownedClient = client;

        var response = await client.GetAsync($"{AdminRoles}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/admin/roles/users/by-username/{username} ─────────────────────

    [Fact]
    public async Task GetUserAuthorityByUsername_IsAllowed_ForAnAccountWithManageRoles()
    {
        var (client, _) = await PermissionOnlyClientAsync("AdmUsr_AuthorityByName", Permissions.ManageRoles);
        using var ownedClient = client;
        var targetId = await RegisterAsync("AdmUsr_AuthorityByNameTarget", "Curator");

        // Lower case on purpose: a role manager types a name, not a normalised identifier.
        var response = await client.GetAsync($"{AdminRoles}/by-username/admusr_authoritybynametarget");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the role page opens an account by username because the account list is admin.users");
        var dto = await response.Content.ReadFromJsonAsync<AccountAuthorityDto>();
        dto.Should().NotBeNull();
        dto!.Id.Should().Be(targetId);
        dto.Roles.Should().Contain("Curator");
        dto.EffectivePermissions.Should().Contain(Permissions.ReviewSubmissions);
    }

    [Fact]
    public async Task GetUserAuthorityByUsername_IsForbidden_ForAnAccountWithManageUsersOnly()
    {
        var (client, _) = await PermissionOnlyClientAsync("AdmUsr_AuthorityByNameUsers", Permissions.ManageUsers);
        using var ownedClient = client;
        await RegisterAsync("AdmUsr_AuthorityByNameUsersTarget");

        var response = await client.GetAsync($"{AdminRoles}/by-username/AdmUsr_AuthorityByNameUsersTarget");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "opening an account's authority is role management, not account administration");
    }

    [Fact]
    public async Task GetUserAuthorityByUsername_UnknownAccount_ReturnsNotFound()
    {
        var (client, _) = await PermissionOnlyClientAsync("AdmUsr_AuthorityByNameUnknown", Permissions.ManageRoles);
        using var ownedClient = client;

        var response = await client.GetAsync($"{AdminRoles}/by-username/AdmUsr_NoSuchAccount");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RoleMutation_WritesAuditRow()
    {
        var (client, actorId) = await PermissionOnlyClientAsync("AdmUsr_AuditRole", Permissions.ManageRoles);
        using var ownedClient = client;
        var targetId = await RegisterAsync("AdmUsr_AuditRoleTarget");

        var response = await client.PostAsJsonAsync($"{AdminRoles}/{targetId}/roles",
            new AssignRoleRequest("GameMaster"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var audit = await AuditEntriesAsync("assign_role", targetId);
        audit.Should().ContainSingle();
        audit[0].ActorAccountId.Should().Be(actorId);
        audit[0].ActorUsername.Should().Be("AdmUsr_AuditRole");
        audit[0].Details.Should().Be("GameMaster");
    }

    // ── DELETE /api/admin/roles/users/{id}/roles/{role} ──────────────────────

    [Fact]
    public async Task RevokeRole_UnknownAccount_ReturnsNotFound()
    {
        using var client = await AdminClientAsync("AdmUsr_RevokeNoAccount");

        var response = await client.DeleteAsync($"{AdminRoles}/{Guid.NewGuid()}/roles/Curator");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RevokeRole_NotAssigned_ReturnsOkWithoutAudit()
    {
        var accountId = await RegisterAsync("AdmUsr_RevokeMissing");
        using var client = await AdminClientAsync("AdmUsr_RevokeMissingAdmin");

        var response = await client.DeleteAsync($"{AdminRoles}/{accountId}/roles/Developer");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AdminActionResponse>();
        body!.Success.Should().BeTrue();
        body.Message.Should().Contain("does not have role");
        (await AuditEntriesAsync("revoke_role", accountId)).Should().BeEmpty();
    }

    [Fact]
    public async Task RevokeRole_Assigned_RemovesTheRoleAndWritesAudit()
    {
        var accountId = await RegisterAsync("AdmUsr_RevokeReal", "GameMaster");
        using var client = await AdminClientAsync("AdmUsr_RevokeRealAdmin");

        var response = await client.DeleteAsync($"{AdminRoles}/{accountId}/roles/GameMaster");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AdminActionResponse>();
        body!.Message.Should().Be("Role 'GameMaster' revoked.");
        (await RolesOfAsync("AdmUsr_RevokeReal")).Should().NotContain("GameMaster");

        var audit = await AuditEntriesAsync("revoke_role", accountId);
        audit.Should().ContainSingle();
        audit[0].Details.Should().Be("GameMaster");
    }

    [Fact]
    public async Task RevokeRole_WithoutToken_ReturnsUnauthorized()
    {
        var response = await factory.CreateClient()
            .DeleteAsync($"{AdminRoles}/{Guid.NewGuid()}/roles/Curator");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── POST /api/admin/roles/users/{id}/permissions ─────────────────────────

    [Fact]
    public async Task GrantPermission_UnknownPermission_ReturnsBadRequest()
    {
        var accountId = await RegisterAsync("AdmUsr_GrantBad");
        using var client = await AdminClientAsync("AdmUsr_GrantBadAdmin");

        var response = await client.PostAsJsonAsync($"{AdminRoles}/{accountId}/permissions",
            new GrantPermissionRequest("world.nuke"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<AdminActionResponse>();
        body!.Success.Should().BeFalse();
        body.Message.Should().Contain("world.nuke");
    }

    [Fact]
    public async Task GrantPermission_UnknownAccount_ReturnsNotFound()
    {
        using var client = await AdminClientAsync("AdmUsr_GrantNoAccount");

        var response = await client.PostAsJsonAsync($"{AdminRoles}/{Guid.NewGuid()}/permissions",
            new GrantPermissionRequest("content.view"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GrantPermission_NewClaim_PersistsAndWritesAudit()
    {
        var accountId = await RegisterAsync("AdmUsr_GrantNew");
        using var client = await AdminClientAsync("AdmUsr_GrantNewAdmin");

        var response = await client.PostAsJsonAsync($"{AdminRoles}/{accountId}/permissions",
            new GrantPermissionRequest("content.review"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AdminActionResponse>();
        body!.Message.Should().Be("Permission 'content.review' granted.");

        var detail = await client.GetFromJsonAsync<PlayerDetailDto>($"{AdminUsers}/{accountId}");
        detail!.UserPermissions.Should().Contain("content.review");

        var audit = await AuditEntriesAsync("grant_permission", accountId);
        audit.Should().ContainSingle();
        audit[0].Details.Should().Be("content.review");
    }

    [Fact]
    public async Task GrantPermission_AlreadyGranted_IsIdempotent()
    {
        var accountId = await RegisterAsync("AdmUsr_GrantAgain");
        using var client = await AdminClientAsync("AdmUsr_GrantAgainAdmin");
        await client.PostAsJsonAsync($"{AdminRoles}/{accountId}/permissions",
            new GrantPermissionRequest("world.give"));

        var second = await client.PostAsJsonAsync($"{AdminRoles}/{accountId}/permissions",
            new GrantPermissionRequest("world.give"));

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await second.Content.ReadFromJsonAsync<AdminActionResponse>();
        body!.Message.Should().Be("Permission already granted.");
        (await AuditEntriesAsync("grant_permission", accountId)).Should().ContainSingle(
            "only the first grant is an auditable change");
    }

    [Fact]
    public async Task GrantPermission_RefusesSelfEscalation()
    {
        var (client, accountId) = await PermissionOnlyClientAsync("AdmUsr_SelfEscalation", Permissions.ManageRoles);
        using var ownedClient = client;

        var response = await client.PostAsJsonAsync($"{AdminRoles}/{accountId}/permissions",
            new GrantPermissionRequest(Permissions.ManageUsers));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "even admin.roles must not be used to widen the caller's own authority");
        (await PermissionsOfAsync("AdmUsr_SelfEscalation")).Should().NotContain(Permissions.ManageUsers);
        (await AuditEntriesAsync("grant_permission", accountId)).Should().BeEmpty(
            "a refused escalation is not an auditable change");
    }

    // ── DELETE /api/admin/roles/users/{id}/permissions/{permission} ───────────

    [Fact]
    public async Task RevokePermission_UnknownAccount_ReturnsNotFound()
    {
        using var client = await AdminClientAsync("AdmUsr_RevokePermNoAccount");

        var response = await client.DeleteAsync($"{AdminRoles}/{Guid.NewGuid()}/permissions/content.view");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RevokePermission_NotPresent_ReturnsOkWithoutAudit()
    {
        var accountId = await RegisterAsync("AdmUsr_RevokePermMissing");
        using var client = await AdminClientAsync("AdmUsr_RevokePermMissingAdmin");

        var response = await client.DeleteAsync($"{AdminRoles}/{accountId}/permissions/content.view");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AdminActionResponse>();
        body!.Message.Should().Be("Permission not found on account.");
        (await AuditEntriesAsync("revoke_permission", accountId)).Should().BeEmpty();
    }

    [Fact]
    public async Task RevokePermission_Present_RemovesTheClaimAndWritesAudit()
    {
        var accountId = await RegisterAsync("AdmUsr_RevokePermReal");
        using var client = await AdminClientAsync("AdmUsr_RevokePermRealAdmin");
        await client.PostAsJsonAsync($"{AdminRoles}/{accountId}/permissions",
            new GrantPermissionRequest("players.suspend"));

        var response = await client.DeleteAsync($"{AdminRoles}/{accountId}/permissions/players.suspend");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AdminActionResponse>();
        body!.Message.Should().Be("Permission 'players.suspend' revoked.");

        var detail = await client.GetFromJsonAsync<PlayerDetailDto>($"{AdminUsers}/{accountId}");
        detail!.UserPermissions.Should().NotContain("players.suspend");

        var audit = await AuditEntriesAsync("revoke_permission", accountId);
        audit.Should().ContainSingle();
        audit[0].Details.Should().Be("players.suspend");
    }

    // ── /api/mod read-only views ──────────────────────────────────────────────

    [Fact]
    public async Task ModUsers_ModeratorWithViewPlayers_CanListAccounts()
    {
        await RegisterAsync("AdmUsr_ModVisible");
        using var client = await ModeratorClientAsync("AdmUsr_ModReader");

        var page = await client.GetFromJsonAsync<AdminUserPage>("/api/mod/users?search=AdmUsr_ModVisible");

        page!.Items.Should().ContainSingle(i => i.Username == "AdmUsr_ModVisible");
    }

    [Fact]
    public async Task ModUsers_PlainUser_ReturnsForbidden()
    {
        using var client = await PlainClientAsync("AdmUsr_ModPlain");

        var response = await client.GetAsync("/api/mod/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ModUserDetail_Moderator_CanReadAnAccount()
    {
        var accountId = await RegisterAsync("AdmUsr_ModDetailTarget");
        using var client = await ModeratorClientAsync("AdmUsr_ModDetailReader");

        var detail = await client.GetFromJsonAsync<PlayerDetailDto>($"/api/mod/users/{accountId}");

        detail!.Username.Should().Be("AdmUsr_ModDetailTarget");
    }

    [Fact]
    public async Task ModUserDetail_UnknownAccount_ReturnsNotFound()
    {
        using var client = await ModeratorClientAsync("AdmUsr_ModDetailMissing");

        var response = await client.GetAsync($"/api/mod/users/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
