using Veldrath.GameClient.Core.Models;

namespace Veldrath.GameClient.Core.Tests;

/// <summary>
/// Tests for the <see cref="ConnectionState"/> enum — verifies values and expected
/// order-of-transition semantics.
/// </summary>
public sealed class ConnectionStateTests
{
    /// <summary>Verifies the enum has exactly the expected member count.</summary>
    [Fact]
    public void ConnectionState_Has_Expected_Count()
    {
        var values = Enum.GetValues<ConnectionState>();
        values.Should().HaveCount(6);
    }

    /// <summary>Verifies the enum values are in the expected order.</summary>
    [Fact]
    public void ConnectionState_Values_Are_In_Expected_Order()
    {
        var values = Enum.GetValues<ConnectionState>();
        values.Should().ContainInOrder(
            ConnectionState.Disconnected,
            ConnectionState.Connecting,
            ConnectionState.Connected,
            ConnectionState.Degraded,
            ConnectionState.Reconnecting,
            ConnectionState.Failed);
    }

    /// <summary>Verifies <see cref="ConnectionState.Disconnected"/> is the default (zero) value.</summary>
    [Fact]
    public void ConnectionState_Disconnected_Is_Default()
    {
        var state = default(ConnectionState);
        state.Should().Be(ConnectionState.Disconnected);
    }

    /// <summary>Verifies all enum members have distinct integer values.</summary>
    [Fact]
    public void ConnectionState_All_Members_Have_Distinct_Values()
    {
        var values = Enum.GetValues<ConnectionState>();
        var distinctCount = values.Cast<int>().Distinct().Count();
        distinctCount.Should().Be(values.Length);
    }

    /// <summary>Verifies the string representation of each member matches the member name.</summary>
    [Fact]
    public void ConnectionState_ToString_Matches_Member_Name()
    {
        foreach (ConnectionState state in Enum.GetValues<ConnectionState>())
        {
            state.ToString().Should().Be(state switch
            {
                ConnectionState.Disconnected => "Disconnected",
                ConnectionState.Connecting => "Connecting",
                ConnectionState.Connected => "Connected",
                ConnectionState.Degraded => "Degraded",
                ConnectionState.Reconnecting => "Reconnecting",
                ConnectionState.Failed => "Failed",
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
            });
        }
    }
}
