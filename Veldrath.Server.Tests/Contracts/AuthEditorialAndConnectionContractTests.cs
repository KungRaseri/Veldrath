using FluentAssertions;
using Veldrath.Contracts.Auth;
using Veldrath.Contracts.Characters;
using Veldrath.Contracts.Connection;
using Veldrath.Contracts.Editorial;

namespace Veldrath.Server.Tests.Contracts;

/// <summary>
/// Value-shape tests for the auth responses, editorial update requests, chat payload and the
/// last-session DTO — the contracts the web client binds to directly, so their defaults and
/// copy semantics are part of the wire format.
/// </summary>
[Trait("Category", "Contracts")]
public class AuthEditorialAndConnectionContractTests
{
    private static readonly Guid AccountId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid SessionId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly DateTimeOffset Expiry = new(2026, 11, 4, 13, 0, 0, TimeSpan.Zero);

    // ── AuthResponse ────────────────────────────────────────────────────────

    [Fact]
    public void AuthResponse_DefaultsTheCuratorFlagAndSessionId()
    {
        // Arrange
        IReadOnlyList<string> roles = ["Player"];
        IReadOnlyList<string> permissions = ["players.read"];

        // Act
        var response = new AuthResponse("access", "refresh", Expiry, AccountId, "aria", roles, permissions);

        // Assert
        response.AccessToken.Should().Be("access");
        response.RefreshToken.Should().Be("refresh");
        response.AccessTokenExpiry.Should().Be(Expiry);
        response.Roles.Should().BeSameAs(roles);
        response.Permissions.Should().BeSameAs(permissions);
        response.IsCurator.Should().BeFalse();
        response.SessionId.Should().BeNull();
    }

    [Fact]
    public void AuthResponse_CarriesTheCuratorFlagAndSessionIdWhenSupplied()
    {
        // Act
        var response = new AuthResponse(
            "access", "refresh", Expiry, AccountId, "aria",
            ["Curator"], ["content.write"], IsCurator: true, SessionId: SessionId);

        // Assert
        response.IsCurator.Should().BeTrue();
        response.SessionId.Should().Be(SessionId);
    }

    [Fact]
    public void AuthResponse_With_ProducesARotatedTokenWithoutMutatingTheOriginal()
    {
        // Arrange
        var original = new AuthResponse("old", "refresh", Expiry, AccountId, "aria", [], []);

        // Act
        var renewed = original with { AccessToken = "new", AccessTokenExpiry = Expiry.AddHours(1) };

        // Assert
        renewed.AccessToken.Should().Be("new");
        renewed.RefreshToken.Should().Be("refresh"); // refresh token is not rotated
        original.AccessToken.Should().Be("old");
    }

    // ── RenewJwtResponse ────────────────────────────────────────────────────

    [Fact]
    public void RenewJwtResponse_DefaultsTheCuratorFlagAndSessionId()
    {
        // Act
        var response = new RenewJwtResponse("access", Expiry, AccountId, "aria", ["Player"], []);

        // Assert
        response.AccessToken.Should().Be("access");
        response.AccessTokenExpiry.Should().Be(Expiry);
        response.AccountId.Should().Be(AccountId);
        response.Username.Should().Be("aria");
        response.IsCurator.Should().BeFalse();
        response.SessionId.Should().BeNull();
    }

    [Fact]
    public void RenewJwtResponse_CarriesNoRefreshTokenAtAll()
    {
        // Assert — the renewal contract deliberately has no refresh member
        typeof(RenewJwtResponse).GetProperty("RefreshToken").Should().BeNull();
    }

    [Fact]
    public void RenewJwtResponse_ComparesByValue()
    {
        // Arrange
        var left = new RenewJwtResponse("access", Expiry, AccountId, "aria", [], []);
        var right = new RenewJwtResponse("access", Expiry, AccountId, "aria", [], []);

        // Act & Assert
        left.Should().Be(right);
    }

    [Fact]
    public void ExchangeCodeRequest_CarriesTheCodeAndAccount()
    {
        // Act
        var request = new ExchangeCodeRequest("abc123", AccountId);

        // Assert
        request.Code.Should().Be("abc123");
        request.AccountId.Should().Be(AccountId);
    }

    [Fact]
    public void CreateExchangeCodeResponse_ReturnsTheCodeForTheAccount()
    {
        // Act
        var response = new CreateExchangeCodeResponse("abc123", AccountId);

        // Assert
        response.Code.Should().Be("abc123");
        response.AccountId.Should().Be(AccountId);
    }

    // ── Editorial update requests ───────────────────────────────────────────

    [Fact]
    public void UpdatePatchNoteRequest_CarriesTheEditableFields()
    {
        // Act
        var request = new UpdatePatchNoteRequest("Title", "Body", "Summary", "1.2.0", "Published");

        // Assert
        request.Title.Should().Be("Title");
        request.Content.Should().Be("Body");
        request.Summary.Should().Be("Summary");
        request.Version.Should().Be("1.2.0");
        request.Status.Should().Be("Published");
    }

    [Fact]
    public void UpdatePatchNoteRequest_With_ChangesOnlyTheNamedField()
    {
        // Arrange
        var request = new UpdatePatchNoteRequest("Title", "Body", "Summary", "1.2.0", "Draft");

        // Act
        var published = request with { Status = "Published" };

        // Assert
        published.Status.Should().Be("Published");
        published.Title.Should().Be("Title");
        request.Status.Should().Be("Draft");
    }

    [Fact]
    public void UpdateLoreArticleRequest_CarriesTheEditableFields()
    {
        // Act
        var request = new UpdateLoreArticleRequest("Title", "Body", "Summary", "History", "Published");

        // Assert
        request.Title.Should().Be("Title");
        request.Content.Should().Be("Body");
        request.Summary.Should().Be("Summary");
        request.Category.Should().Be("History");
        request.Status.Should().Be("Published");
    }

    [Fact]
    public void UpdateLoreArticleRequest_ComparesByValue()
    {
        // Arrange
        var left = new UpdateLoreArticleRequest("Title", "Body", "Summary", "History", "Draft");
        var right = new UpdateLoreArticleRequest("Title", "Body", "Summary", "History", "Draft");
        var different = new UpdateLoreArticleRequest("Other", "Body", "Summary", "History", "Draft");

        // Act & Assert
        left.Should().Be(right);
        left.Should().NotBe(different);
    }

    [Fact]
    public void UpdateAnnouncementRequest_CarriesTheEditableFields()
    {
        // Act
        var request = new UpdateAnnouncementRequest("Maintenance", "Servers restart at noon", "Published");

        // Assert
        request.Title.Should().Be("Maintenance");
        request.Body.Should().Be("Servers restart at noon");
        request.Status.Should().Be("Published");
    }

    [Fact]
    public void UpdateAnnouncementRequest_With_CopiesThePayload()
    {
        // Arrange
        var request = new UpdateAnnouncementRequest("Maintenance", "Body", "Draft");

        // Act
        var copy = request with { };

        // Assert
        copy.Should().Be(request);
        copy.Should().NotBeSameAs(request);
    }

    // ── ChatMessagePayload ──────────────────────────────────────────────────

    [Fact]
    public void ChatMessagePayload_CarriesTheSenderAndTimestamp()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var sentAt = new DateTimeOffset(2026, 11, 4, 12, 30, 0, TimeSpan.Zero);

        // Act
        var payload = new ChatMessagePayload(characterId, "Aria", "well met", sentAt);

        // Assert
        payload.CharacterId.Should().Be(characterId);
        payload.SenderName.Should().Be("Aria");
        payload.Message.Should().Be("well met");
        payload.Timestamp.Should().Be(sentAt);
    }

    [Fact]
    public void ChatMessagePayload_ComparesByValueIncludingTheTimestamp()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var sentAt = new DateTimeOffset(2026, 11, 4, 12, 30, 0, TimeSpan.Zero);
        var left = new ChatMessagePayload(characterId, "Aria", "well met", sentAt);
        var right = new ChatMessagePayload(characterId, "Aria", "well met", sentAt);
        var later = new ChatMessagePayload(characterId, "Aria", "well met", sentAt.AddSeconds(1));

        // Act & Assert
        left.Should().Be(right);
        left.Should().NotBe(later);
    }

    // ── LastSessionDto ──────────────────────────────────────────────────────

    [Fact]
    public void LastSessionDto_DefaultsTheMapAndNodeIdentifiers()
    {
        // Arrange
        var characterId = Guid.NewGuid();

        // Act
        var dto = new LastSessionDto(characterId, "Aria", "crestfall", "varenmark");

        // Assert
        dto.CharacterId.Should().Be(characterId);
        dto.CharacterName.Should().Be("Aria");
        dto.ZoneId.Should().Be("crestfall");
        dto.RegionId.Should().Be("varenmark");
        dto.CurrentMapId.Should().BeNull();
        dto.CurrentNodeId.Should().BeNull();
    }

    [Fact]
    public void LastSessionDto_AcceptsARegionMapPositionWithoutAZone()
    {
        // Arrange
        var dto = new LastSessionDto(Guid.NewGuid(), "Aria", null, "varenmark", "region:varenmark", "junction-4");

        // Assert
        dto.ZoneId.Should().BeNull();
        dto.CurrentMapId.Should().Be("region:varenmark");
        dto.CurrentNodeId.Should().Be("junction-4");
    }

    [Fact]
    public void LastSessionDto_ComparesByValue()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var left = new LastSessionDto(characterId, "Aria", null, null);
        var right = new LastSessionDto(characterId, "Aria", null, null);

        // Act & Assert
        left.Should().Be(right);
    }

    [Fact]
    public void LastSessionDto_IsSealed()
    {
        // Act & Assert
        typeof(LastSessionDto).IsSealed.Should().BeTrue();
    }
}
