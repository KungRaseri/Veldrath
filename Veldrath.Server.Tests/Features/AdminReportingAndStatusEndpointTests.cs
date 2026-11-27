using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>One page of the anonymous payload returned by the admin audit and report lists.</summary>
internal sealed record AdminPagedItems<T>(int Total, int Page, int PageSize, List<T> Items);

/// <summary>
/// Integration tests for the read-only moderation surfaces of
/// <see cref="Veldrath.Server.Features.Admin.AdminEndpoints"/> — the audit trail, the live session list, the player
/// report queue with its resolve transition, and the server status snapshot.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class AdminReportingAndStatusEndpointTests(WebAppFactory factory)
{
    private async Task<HttpClient> AuthedClientAsync(string username, params string[] roles)
    {
        var (token, _) = await factory.CreateAccountTokenAsync(username, roles);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private Guid CreateAuditEntry(string action, Guid? targetId, string actor, string? details, DateTimeOffset? occurredAt = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entry = new AdminAuditEntry
        {
            ActorAccountId = Guid.NewGuid(),
            ActorUsername = actor,
            TargetAccountId = targetId,
            TargetUsername = targetId is null ? null : "targeted-player",
            Action = action,
            Details = details,
            OccurredAt = occurredAt ?? DateTimeOffset.UtcNow,
        };
        db.AdminAuditEntries.Add(entry);
        db.SaveChanges();
        return entry.Id;
    }

    private async Task<Guid> SeedReportAsync(string reporter, string target, bool resolved = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var report = new PlayerReport
        {
            ReporterName = reporter,
            TargetName = target,
            Reason = "abusive language",
            SubmittedAt = DateTimeOffset.UtcNow,
            IsResolved = resolved,
            ResolvedAt = resolved ? DateTimeOffset.UtcNow : null,
        };
        db.PlayerReports.Add(report);
        await db.SaveChangesAsync();
        return report.Id;
    }

    private async Task<Character> SeedSessionAsync(
        string characterName, string? zoneId, string regionId = "varenmark")
    {
        var (_, accountId) = await factory.CreateAccountTokenAsync($"sess_{characterName}");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var character = new Character
        {
            AccountId = accountId,
            Name = characterName,
            ClassName = "Warrior",
            SlotIndex = 1,
            Attributes = "{}",
        };
        db.Characters.Add(character);
        db.PlayerSessions.Add(new PlayerSession
        {
            CharacterId = character.Id,
            CharacterName = characterName,
            ConnectionId = $"conn-{Guid.NewGuid():N}",
            RegionId = regionId,
            ZoneId = zoneId,
        });
        await db.SaveChangesAsync();
        return character;
    }

    // ── GET /api/admin/audit ──────────────────────────────────────────────────

    [Fact]
    public async Task Audit_PlainUser_ReturnsForbidden()
    {
        using var client = await AuthedClientAsync("AdmAud_Plain");

        var response = await client.GetAsync("/api/admin/audit");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Audit_Admin_ReturnsNewestFirstWithTheRecordedFields()
    {
        var targetId = Guid.NewGuid();
        var first = CreateAuditEntry("audit-order-first", targetId, "AuditorOne", "first",
            DateTimeOffset.UtcNow.AddMinutes(-5));
        var second = CreateAuditEntry("audit-order-second", targetId, "AuditorTwo", "second");
        using var client = await AuthedClientAsync("AdmAud_Reader", "Admin");

        var page = await client.GetFromJsonAsync<AdminPagedItems<AuditEntryDto>>(
            $"/api/admin/audit?targetId={targetId}");

        page!.Total.Should().Be(2);
        page.Items.Should().HaveCount(2);
        page.Items.Select(i => i.Id).Should().ContainInOrder(second, first);
        page.Items[0].ActorUsername.Should().Be("AuditorTwo");
        page.Items[0].Action.Should().Be("audit-order-second");
        page.Items[0].Details.Should().Be("second");
    }

    [Fact]
    public async Task Audit_TargetFilter_ExcludesOtherAccounts()
    {
        var wanted = Guid.NewGuid();
        CreateAuditEntry("audit-filter-wanted", wanted, "Auditor", null);
        CreateAuditEntry("audit-filter-other", Guid.NewGuid(), "Auditor", null);
        using var client = await AuthedClientAsync("AdmAud_FilterReader", "Admin");

        var page = await client.GetFromJsonAsync<AdminPagedItems<AuditEntryDto>>(
            $"/api/admin/audit?targetId={wanted}");

        page!.Items.Should().ContainSingle().Which.TargetAccountId.Should().Be(wanted);
    }

    [Fact]
    public async Task Audit_PageSize_IsClampedToOneHundredAndFifty()
    {
        using var client = await AuthedClientAsync("AdmAud_ClampReader", "Admin");

        var small = await client.GetFromJsonAsync<AdminPagedItems<AuditEntryDto>>("/api/admin/audit?pageSize=0");
        var large = await client.GetFromJsonAsync<AdminPagedItems<AuditEntryDto>>("/api/admin/audit?pageSize=1000");

        small!.PageSize.Should().Be(1);
        large!.PageSize.Should().Be(200);
    }

    [Fact]
    public async Task Audit_WithoutToken_ReturnsUnauthorized()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/audit");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/admin/sessions ───────────────────────────────────────────────

    [Fact]
    public async Task Sessions_ReturnsTheActiveSessionWithItsOwner()
    {
        await SeedSessionAsync("AdmSess_Hero", "crestfall");
        using var client = await AuthedClientAsync("AdmSess_Reader", "Admin");

        var sessions = await client.GetFromJsonAsync<List<ActiveSessionDto>>("/api/admin/sessions");

        var session = sessions!.Should().ContainSingle(s => s.CharacterName == "AdmSess_Hero").Subject;
        session.ZoneId.Should().Be("crestfall");
        session.RegionId.Should().Be("varenmark");
        session.AccountUsername.Should().Be("sess_AdmSess_Hero");
        session.CharacterId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Sessions_ZoneFilter_ExcludesCharactersOnTheRegionMap()
    {
        await SeedSessionAsync("AdmSess_InZone", "crestfall");
        await SeedSessionAsync("AdmSess_OnMap", null!);
        using var client = await AuthedClientAsync("AdmSess_ZoneFilter", "Admin");

        var zoned = await client.GetFromJsonAsync<List<ActiveSessionDto>>("/api/admin/sessions?zoneId=crestfall");
        var region = await client.GetFromJsonAsync<List<ActiveSessionDto>>("/api/admin/sessions?regionId=varenmark");

        zoned!.Should().Contain(s => s.CharacterName == "AdmSess_InZone");
        zoned.Should().NotContain(s => s.CharacterName == "AdmSess_OnMap");
        region!.Should().Contain(s => s.CharacterName == "AdmSess_OnMap")
            .And.Contain(s => s.CharacterName == "AdmSess_InZone");
    }

    [Fact]
    public async Task Sessions_PlainUser_ReturnsForbidden()
    {
        using var client = await AuthedClientAsync("AdmSess_Plain");

        var response = await client.GetAsync("/api/admin/sessions");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── GET /api/admin/reports + PUT /api/admin/reports/{id}/resolve ──────────

    [Fact]
    public async Task Reports_ResolvedFilter_SplitsOpenAndClosed()
    {
        await SeedReportAsync("Reporter_Open", "Target_Open");
        await SeedReportAsync("Reporter_Closed", "Target_Closed", resolved: true);
        using var client = await AuthedClientAsync("AdmRep_FilterReader", "Moderator");

        var open = await client.GetFromJsonAsync<AdminPagedItems<PlayerReportDto>>("/api/admin/reports?resolved=false");
        var closed = await client.GetFromJsonAsync<AdminPagedItems<PlayerReportDto>>("/api/admin/reports?resolved=true");

        open!.Items.Should().Contain(r => r.ReporterName == "Reporter_Open");
        open.Items.Should().NotContain(r => r.ReporterName == "Reporter_Closed");
        closed!.Items.Should().Contain(r => r.ReporterName == "Reporter_Closed");
    }

    [Fact]
    public async Task Reports_ModerationPrefix_IsReadableByModerators()
    {
        await SeedReportAsync("Reporter_ModView", "Target_ModView");
        using var client = await AuthedClientAsync("AdmRep_ModReader", "Moderator");

        var page = await client.GetFromJsonAsync<AdminPagedItems<PlayerReportDto>>("/api/mod/reports");

        page!.Items.Should().Contain(r => r.ReporterName == "Reporter_ModView");
    }

    [Fact]
    public async Task Reports_PlainUser_ReturnsForbidden()
    {
        using var client = await AuthedClientAsync("AdmRep_Plain");

        var response = await client.GetAsync("/api/admin/reports");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ResolveReport_UnknownReport_ReturnsNotFound()
    {
        using var client = await AuthedClientAsync("AdmRep_ResolveMissing", "Moderator");

        var response = await client.PutAsync($"/api/admin/reports/{Guid.NewGuid()}/resolve", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ResolveReport_OpenReport_MarksItResolvedWithTheActor()
    {
        var reportId = await SeedReportAsync("Reporter_Resolve", "Target_Resolve");
        var (_, moderatorId) = await factory.CreateAccountTokenAsync("AdmRep_Resolver", "Moderator");
        using var client = await AuthedClientAsync("AdmRep_Resolver", "Moderator");

        var response = await client.PutAsync($"/api/admin/reports/{reportId}/resolve", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AdminActionResponse>();
        body!.Message.Should().Be("Report resolved.");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var report = await db.PlayerReports.AsNoTracking().SingleAsync(r => r.Id == reportId);
        report.IsResolved.Should().BeTrue();
        report.ResolvedAt.Should().NotBeNull();
        report.ResolvedByAccountId.Should().Be(moderatorId);
    }

    [Fact]
    public async Task ResolveReport_AlreadyResolved_IsIdempotent()
    {
        var reportId = await SeedReportAsync("Reporter_Already", "Target_Already", resolved: true);
        using var client = await AuthedClientAsync("AdmRep_AlreadyResolver", "Moderator");

        var response = await client.PutAsync($"/api/admin/reports/{reportId}/resolve", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AdminActionResponse>();
        body!.Message.Should().Be("Already resolved.");
    }

    [Fact]
    public async Task ResolveReport_ModerationPrefix_ResolvesToo()
    {
        var reportId = await SeedReportAsync("Reporter_ModResolve", "Target_ModResolve");
        using var client = await AuthedClientAsync("AdmRep_ModResolver", "Moderator");

        var response = await client.PutAsync($"/api/mod/reports/{reportId}/resolve", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<string> SeedZoneAsync(string name)
    {
        var zoneId = $"statuszone-{Guid.NewGuid():N}";
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Zones.Add(new Zone
        {
            Id = zoneId,
            Name = name,
            Description = "fixture zone for the admin status tests",
            RegionId = "varenmark",
        });
        await db.SaveChangesAsync();
        return zoneId;
    }

    // ── GET /api/admin/status ─────────────────────────────────────────────────
    //
    // The snapshot body is exercised end to end: the zone-population aggregate used to fail translation on
    // every provider (a DTO-constructor projection cannot be ordered by), which the authorization-only tests
    // below could not see. The Npgsql translation path is pinned separately in AdminStatusQueryTests.

    [Fact]
    public async Task Status_Admin_ReportsOccupiedZonesOrderedByPopulation()
    {
        var crowded = await SeedZoneAsync("Status Crowded");
        var quiet = await SeedZoneAsync("Status Quiet");
        await SeedSessionAsync("AdmStat_CrowdOne", crowded);
        await SeedSessionAsync("AdmStat_CrowdTwo", crowded);
        await SeedSessionAsync("AdmStat_Quiet", quiet);
        // A character on the region map (no zone) must not create a row of its own.
        await SeedSessionAsync("AdmStat_OnMap", null);
        using var client = await AuthedClientAsync("AdmStatus_Admin", "Admin");

        var response = await client.GetAsync("/api/admin/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = await response.Content.ReadFromJsonAsync<ServerStatusDto>();
        var ours = status!.ZonePopulations.Where(z => z.ZoneId == crowded || z.ZoneId == quiet).ToList();
        ours.Should().Equal(
            new ZonePopulationDto(crowded, "Status Crowded", 2),
            new ZonePopulationDto(quiet, "Status Quiet", 1));
        status.ConnectedPlayers.Should().BeGreaterThanOrEqualTo(3);
        status.ZonePopulations.Should().OnlyContain(z => z.ZoneId != null && z.PlayerCount > 0);
    }

    [Fact]
    public async Task Status_PlainUser_ReturnsForbidden()
    {
        using var client = await AuthedClientAsync("AdmStatus_Plain");

        var response = await client.GetAsync("/api/admin/status");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Status_WithoutToken_ReturnsUnauthorized()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/status");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
