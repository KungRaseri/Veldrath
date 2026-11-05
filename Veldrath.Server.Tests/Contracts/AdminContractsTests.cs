using FluentAssertions;

namespace Veldrath.Server.Tests.Contracts;

/// <summary>
/// Value-shape tests for the admin DTOs and request records: how the defaults a client relies on
/// resolve, and that each record behaves as an immutable value (structural equality plus
/// <c>with</c> copies that leave the original untouched).
/// </summary>
[Trait("Category", "Contracts")]
public class AdminContractsTests
{
    private static readonly Guid AccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CharacterId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    // ── PlayerSummaryDto ────────────────────────────────────────────────────

    [Fact]
    public void PlayerSummaryDto_ExposesEveryListedField()
    {
        // Arrange
        IReadOnlyList<string> roles = ["Player", "Curator"];

        // Act
        var dto = new PlayerSummaryDto(
            AccountId, "aria", "aria@example.com", roles,
            IsBanned: true, BannedUntil: Now, WarnCount: 2, IsMuted: false,
            CreatedAt: Now, LastSeenAt: null);

        // Assert
        dto.Id.Should().Be(AccountId);
        dto.Username.Should().Be("aria");
        dto.Email.Should().Be("aria@example.com");
        dto.Roles.Should().BeSameAs(roles);
        dto.IsBanned.Should().BeTrue();
        dto.BannedUntil.Should().Be(Now);
        dto.WarnCount.Should().Be(2);
        dto.IsMuted.Should().BeFalse();
        dto.CreatedAt.Should().Be(Now);
        dto.LastSeenAt.Should().BeNull();
    }

    [Fact]
    public void PlayerSummaryDto_IsAValueRecord()
    {
        // Arrange
        IReadOnlyList<string> roles = ["Player"];
        var dto = new PlayerSummaryDto(AccountId, "aria", null, roles, false, null, 0, false, Now, null);

        // Act
        var copy = dto with { WarnCount = 3 };

        // Assert
        copy.Should().Be(new PlayerSummaryDto(AccountId, "aria", null, roles, false, null, 3, false, Now, null));
        copy.WarnCount.Should().Be(3);
        dto.WarnCount.Should().Be(0); // the original is unchanged
    }

    // ── PlayerDetailDto ─────────────────────────────────────────────────────

    [Fact]
    public void PlayerDetailDto_CarriesPermissionsBanAndMuteState()
    {
        // Arrange
        IReadOnlyList<string> roles = ["Moderator"];
        IReadOnlyList<string> effective = ["players.read", "players.ban"];
        IReadOnlyList<string> userOnly = ["players.ban"];

        // Act
        var dto = new PlayerDetailDto(
            AccountId, "aria", "aria@example.com", roles, effective, userOnly,
            IsBanned: true, BannedUntil: null, BanReason: "cheating",
            WarnCount: 4, IsMuted: true, MutedUntil: Now, MuteReason: "spam",
            CreatedAt: Now, LastSeenAt: Now);

        // Assert
        dto.Roles.Should().BeSameAs(roles);
        dto.EffectivePermissions.Should().BeSameAs(effective);
        dto.UserPermissions.Should().BeSameAs(userOnly);
        dto.BanReason.Should().Be("cheating");
        dto.BannedUntil.Should().BeNull(); // permanent
        dto.MutedUntil.Should().Be(Now);
        dto.MuteReason.Should().Be("spam");
        dto.WarnCount.Should().Be(4);
    }

    [Fact]
    public void PlayerDetailDto_EqualsWhenEveryMemberMatches()
    {
        // Arrange
        IReadOnlyList<string> roles = ["Player"];
        var left = new PlayerDetailDto(AccountId, "aria", null, roles, [], [], false, null, null, 0, false, null, null, Now, null);
        var right = new PlayerDetailDto(AccountId, "aria", null, roles, [], [], false, null, null, 0, false, null, null, Now, null);

        // Act & Assert
        left.Should().Be(right);
        left.GetHashCode().Should().Be(right.GetHashCode());
    }

    // ── AuditEntryDto ───────────────────────────────────────────────────────

    [Fact]
    public void AuditEntryDto_DescribesAStaffActionAgainstAPlayer()
    {
        // Act
        var dto = new AuditEntryDto(Guid.NewGuid(), "mod-sarah", AccountId, "aria", "ban", "cheating", Now);

        // Assert
        dto.ActorUsername.Should().Be("mod-sarah");
        dto.TargetAccountId.Should().Be(AccountId);
        dto.TargetUsername.Should().Be("aria");
        dto.Action.Should().Be("ban");
        dto.Details.Should().Be("cheating");
        dto.OccurredAt.Should().Be(Now);
    }

    [Fact]
    public void AuditEntryDto_AllowsServerWideActionsWithoutATarget()
    {
        // Act
        var dto = new AuditEntryDto(Guid.NewGuid(), "mod-sarah", null, null, "broadcast", null, Now);

        // Assert
        dto.TargetAccountId.Should().BeNull();
        dto.TargetUsername.Should().BeNull();
        dto.Details.Should().BeNull();
    }

    [Fact]
    public void AuditEntryDto_DiffersWhenTheActionDiffers()
    {
        // Arrange
        var id = Guid.NewGuid();
        var ban = new AuditEntryDto(id, "mod", AccountId, "aria", "ban", null, Now);
        var warn = new AuditEntryDto(id, "mod", AccountId, "aria", "warn", null, Now);

        // Act & Assert
        ban.Should().NotBe(warn);
    }

    // ── PlayerReportDto ─────────────────────────────────────────────────────

    [Fact]
    public void PlayerReportDto_IsOpenUntilResolved()
    {
        // Act
        var open = new PlayerReportDto(Guid.NewGuid(), "aria", "brann", "griefing", Now, false, null);

        // Assert
        open.IsResolved.Should().BeFalse();
        open.ResolvedAt.Should().BeNull();
        open.Reason.Should().Be("griefing");
    }

    [Fact]
    public void PlayerReportDto_RecordsWhenItWasResolved()
    {
        // Act
        var resolved = new PlayerReportDto(Guid.NewGuid(), "aria", "brann", "griefing", Now, true, Now.AddHours(2));

        // Assert
        resolved.IsResolved.Should().BeTrue();
        resolved.ResolvedAt.Should().Be(Now.AddHours(2));
    }

    // ── ServerStatusDto ─────────────────────────────────────────────────────

    [Fact]
    public void ServerStatusDto_DefaultsTheOptionalCountersAndHealthChecks()
    {
        // Arrange
        IReadOnlyList<ZonePopulationDto> zones = [new("crestfall", "Crestfall", 3)];

        // Act
        var dto = new ServerStatusDto(3, zones, Now);

        // Assert
        dto.ConnectedPlayers.Should().Be(3);
        dto.ZonePopulations.Should().BeSameAs(zones);
        dto.ServerStartedAt.Should().Be(Now);
        dto.RegisteredAccounts.Should().Be(0);
        dto.TotalCharacters.Should().Be(0);
        dto.HealthChecks.Should().BeNull();
    }

    [Fact]
    public void ServerStatusDto_AcceptsExplicitCountersAndHealthChecks()
    {
        // Arrange
        IReadOnlyList<Veldrath.Contracts.Account.HealthCheckEntryDto> checks =
            [new("database", "Healthy", null), new("smtp", "Degraded", "slow")];

        // Act
        var dto = new ServerStatusDto(5, [], Now, RegisteredAccounts: 12, TotalCharacters: 20, HealthChecks: checks);

        // Assert
        dto.RegisteredAccounts.Should().Be(12);
        dto.TotalCharacters.Should().Be(20);
        dto.HealthChecks.Should().BeSameAs(checks);
    }

    [Fact]
    public void ZonePopulationDto_CarriesTheZoneAndCount()
    {
        // Act
        var dto = new ZonePopulationDto("crestfall", "Crestfall", 7);

        // Assert
        dto.ZoneId.Should().Be("crestfall");
        dto.ZoneName.Should().Be("Crestfall");
        dto.PlayerCount.Should().Be(7);
    }

    // ── ActiveSessionDto ────────────────────────────────────────────────────

    [Fact]
    public void ActiveSessionDto_DescribesALiveSession()
    {
        // Act
        var dto = new ActiveSessionDto(CharacterId, "Aria", AccountId, "aria", "varenmark", "crestfall", Now);

        // Assert
        dto.CharacterId.Should().Be(CharacterId);
        dto.CharacterName.Should().Be("Aria");
        dto.AccountId.Should().Be(AccountId);
        dto.AccountUsername.Should().Be("aria");
        dto.RegionId.Should().Be("varenmark");
        dto.ZoneId.Should().Be("crestfall");
        dto.EnteredAt.Should().Be(Now);
    }

    [Fact]
    public void ActiveSessionDto_AllowsAZoneOnlyRegionMapSession()
    {
        // Act
        var dto = new ActiveSessionDto(CharacterId, "Aria", AccountId, "aria", "varenmark", null, Now);

        // Assert
        dto.ZoneId.Should().BeNull();
    }

    [Fact]
    public void ActiveSessionDto_ComparesByValue()
    {
        // Arrange
        var left = new ActiveSessionDto(CharacterId, "Aria", AccountId, "aria", "varenmark", null, Now);
        var right = new ActiveSessionDto(CharacterId, "Aria", AccountId, "aria", "varenmark", null, Now);

        // Act & Assert
        left.Should().Be(right);
    }

    // ── Request records: option defaults ────────────────────────────────────

    [Fact]
    public void KickPlayerRequest_DefaultsToNoReason()
    {
        // Act
        var request = new KickPlayerRequest(AccountId);

        // Assert
        request.AccountId.Should().Be(AccountId);
        request.Reason.Should().BeNull();
    }

    [Fact]
    public void BanPlayerRequest_WithoutADuration_MeansPermanent()
    {
        // Act
        var request = new BanPlayerRequest(AccountId, "cheating");

        // Assert
        request.DurationMinutes.Should().BeNull();

        var timed = request with { DurationMinutes = 60 };
        timed.DurationMinutes.Should().Be(60);
    }

    [Fact]
    public void MutePlayerRequest_DefaultsToNoReasonAndPermanent()
    {
        // Act
        var request = new MutePlayerRequest(AccountId);

        // Assert
        request.Reason.Should().BeNull();
        request.DurationMinutes.Should().BeNull();
    }

    [Fact]
    public void BroadcastAnnouncementRequest_DefaultsToInfoSeverity()
    {
        // Act
        var request = new BroadcastAnnouncementRequest("Server restart in 5 minutes");

        // Assert
        request.Message.Should().Be("Server restart in 5 minutes");
        request.Severity.Should().Be("info");
    }

    [Fact]
    public void AdminActionResponse_ReportsSuccessAndMessage()
    {
        // Act
        var response = new AdminActionResponse(true, "Banned");

        // Assert
        response.Success.Should().BeTrue();
        response.Message.Should().Be("Banned");
    }

    [Fact]
    public void AdminRequests_AreDistinctValuesForDistinctPayloads()
    {
        // Act
        var warn = new WarnPlayerRequest(AccountId, "spam");
        var otherWarn = new WarnPlayerRequest(AccountId, "abuse");

        // Assert
        warn.Should().NotBe(otherWarn);
        warn.Should().Be(new WarnPlayerRequest(AccountId, "spam"));
    }
}
