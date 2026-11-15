using Veldrath.Server.Features.Zones;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Covers the one place the SignalR group format is composed and read. The public shapes must stay
/// byte-identical to what the hub has always produced, because every stored enemy, ground item and
/// combat row is keyed by them.
/// </summary>
public class ZoneGroupNameTests
{
    [Fact]
    public void ForZone_MatchesTheGroupTheHubHasAlwaysComputed()
    {
        ZoneGroupName.ForZone("crestfall").Value.Should().Be("zone:crestfall");
        ZoneGroupName.ForZone("dark-forest", "hardcore").Value.Should().Be("zone:dark-forest_hardcore");
    }

    [Fact]
    public void ForInstance_AppendsTheVersionedRunMarker()
    {
        var runId = Guid.Parse("2f9c4f3e-1a2b-4c5d-8e9f-0a1b2c3d4e5f");

        var group = ZoneGroupName.ForInstance("the-halrow", null, runId);

        group.Value.Should().Be("zone:the-halrow#i1:2f9c4f3e-1a2b-4c5d-8e9f-0a1b2c3d4e5f");
        group.IsInstance.Should().BeTrue();
        group.InstanceId.Should().Be(runId);
    }

    [Fact]
    public void ForInstance_CarriesTheDifficultyModeBeforeTheRunMarker()
    {
        var runId = Guid.NewGuid();

        var group = ZoneGroupName.ForInstance("dark-forest", "hardcore", runId);

        group.Value.Should().StartWith("zone:dark-forest_hardcore#i1:");
        group.InstanceId.Should().Be(runId);
    }

    [Fact]
    public void A_PublicScope_ReportsNoRun()
    {
        var group = ZoneGroupName.ForZone("crestfall");

        group.IsInstance.Should().BeFalse();
        group.InstanceId.Should().BeNull();
    }

    [Fact]
    public void ForInstance_RefusesAnEmptyRunId()
    {
        var act = () => ZoneGroupName.ForInstance("the-halrow", null, Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("the#halrow")]
    [InlineData("the halrow")]
    public void ForZone_RefusesAZoneIdTheFormatCannotCarry(string zoneId)
    {
        var act = () => ZoneGroupName.ForZone(zoneId);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("crestfall")]
    [InlineData("zone:")]
    [InlineData("zone:crestfall#i1:not-a-guid")]
    [InlineData("zone:crestfall#i1:00000000-0000-0000-0000-000000000000")]
    [InlineData("zone:crestfall#i1:2f9c4f3e-1a2b-4c5d-8e9f-0a1b2c3d4e5f#i1:2f9c4f3e-1a2b-4c5d-8e9f-0a1b2c3d4e5f")]
    [InlineData("zone:the halrow")]
    public void TryParse_RejectsAnythingThatIsNotOneOfTheTwoShapes(string? value)
    {
        ZoneGroupName.TryParse(value, out _).Should().BeFalse();
    }

    [Fact]
    public void TryParse_AcceptsBothShapes()
    {
        ZoneGroupName.TryParse("zone:crestfall", out var publicScope).Should().BeTrue();
        publicScope.IsInstance.Should().BeFalse();

        ZoneGroupName.TryParse("zone:the-halrow#i1:2f9c4f3e-1a2b-4c5d-8e9f-0a1b2c3d4e5f", out var run).Should().BeTrue();
        run.IsInstance.Should().BeTrue();
        run.InstanceId.Should().Be(Guid.Parse("2f9c4f3e-1a2b-4c5d-8e9f-0a1b2c3d4e5f"));
    }
}
