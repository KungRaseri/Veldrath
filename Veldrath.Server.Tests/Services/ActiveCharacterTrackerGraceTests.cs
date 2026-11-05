using Microsoft.Extensions.Options;
using Veldrath.Server.Services;
using Veldrath.Server.Settings;

namespace Veldrath.Server.Tests.Services;

/// <summary>
/// Tests for the grace-period behaviour of <see cref="ActiveCharacterTracker"/>: a refreshed page must be able to
/// reclaim its own character, a claim inside the grace window must stay reserved, and a claim whose window has
/// expired must be stealable.
/// </summary>
public class ActiveCharacterTrackerGraceTests
{
    private static ActiveCharacterTracker WithGraceSeconds(int seconds) =>
        new(Options.Create(new ActiveCharacterTrackerOptions { GracePeriodSeconds = seconds }));

    [Fact]
    public void ReclaimingFromTheSameConnection_ClearsTheDisconnectMarker()
    {
        var tracker = WithGraceSeconds(30);
        var character = Guid.NewGuid();

        tracker.TryClaim(character, "conn-1").Should().BeTrue();
        tracker.MarkDisconnecting(character);
        tracker.IsActive(character).Should().BeFalse("the claim is in the grace period");

        tracker.TryClaim(character, "conn-1").Should().BeTrue();

        tracker.IsActive(character).Should().BeTrue();
        tracker.GetActiveCharacterIds().Should().Contain(character);
    }

    [Fact]
    public void AClaimInsideTheGraceWindow_BlocksOtherConnections()
    {
        var tracker = WithGraceSeconds(30);
        var character = Guid.NewGuid();

        tracker.TryClaim(character, "conn-1").Should().BeTrue();
        tracker.MarkDisconnecting(character);

        tracker.TryClaim(character, "conn-2").Should().BeFalse();
        tracker.GetCharacterForConnection("conn-1").Should().Be(character);
        tracker.GetCharacterForConnection("conn-2").Should().BeNull();
    }

    [Fact]
    public void AnActiveClaim_BlocksOtherConnections()
    {
        var tracker = WithGraceSeconds(30);
        var character = Guid.NewGuid();

        tracker.TryClaim(character, "conn-1").Should().BeTrue();

        tracker.TryClaim(character, "conn-2").Should().BeFalse();
    }

    [Fact]
    public void AGraceWindowBelowOneSecond_FallsBackToThirtySeconds()
    {
        var tracker = WithGraceSeconds(0);
        var character = Guid.NewGuid();

        tracker.TryClaim(character, "conn-1").Should().BeTrue();
        tracker.MarkDisconnecting(character);

        tracker.TryClaim(character, "conn-2").Should().BeFalse(
            "a zero grace period is a misconfiguration; the tracker keeps the documented 30-second default");
    }

    [Fact]
    public async Task AnExpiredClaim_CanBeTakenOverAndTheOldConnectionIsReleased()
    {
        var tracker = WithGraceSeconds(1);
        var character = Guid.NewGuid();
        tracker.TryClaim(character, "conn-1").Should().BeTrue();
        tracker.MarkDisconnecting(character);

        await Task.Delay(TimeSpan.FromMilliseconds(1200));

        tracker.TryClaim(character, "conn-2").Should().BeTrue();
        tracker.GetCharacterForConnection("conn-2").Should().Be(character);
        tracker.GetCharacterForConnection("conn-1").Should().BeNull();
        tracker.IsActive(character).Should().BeTrue();
    }

    [Fact]
    public void MarkDisconnecting_IgnoresAnUnknownCharacter()
    {
        var tracker = WithGraceSeconds(30);

        tracker.MarkDisconnecting(Guid.NewGuid());

        tracker.GetActiveCharacterIds().Should().BeEmpty();
    }

    [Fact]
    public void ReleasingAConnection_FreesTheCharacter()
    {
        var tracker = WithGraceSeconds(30);
        var character = Guid.NewGuid();
        tracker.TryClaim(character, "conn-1");

        tracker.Release("conn-1");

        tracker.GetCharacterForConnection("conn-1").Should().BeNull();
        tracker.TryClaim(character, "conn-2").Should().BeTrue();
    }
}
