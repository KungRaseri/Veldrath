using Veldrath.GameClient.Core.Models;
using Veldrath.GameClient.Core.Tests.Infrastructure;

namespace Veldrath.GameClient.Core.Tests;

/// <summary>
/// Tests for the <see cref="IGameHubConnectionService"/> contract using
/// <see cref="FakeGameHubConnectionService"/> as the test double.
/// </summary>
public sealed class GameHubConnectionServiceTests
{
    // ── Connection lifecycle ───────────────────────────────────────────────────

    /// <summary>Verifies ConnectAsync changes state to Connected.</summary>
    [Fact]
    public async Task ConnectAsync_Sets_IsConnected_True()
    {
        var service = new FakeGameHubConnectionService();
        service.IsConnected.Should().BeFalse();

        await service.ConnectAsync("http://localhost:5000", "test-token");

        service.ConnectCalled.Should().BeTrue();
        service.IsConnected.Should().BeTrue();
        service.State.Should().Be(ConnectionState.Connected);
    }

    /// <summary>Verifies DisconnectAsync changes state to Disconnected.</summary>
    [Fact]
    public async Task DisconnectAsync_Sets_IsConnected_False()
    {
        var service = new FakeGameHubConnectionService();
        await service.ConnectAsync("http://localhost:5000", "test-token");
        service.IsConnected.Should().BeTrue();

        await service.DisconnectAsync();

        service.DisconnectCalled.Should().BeTrue();
        service.IsConnected.Should().BeFalse();
        service.State.Should().Be(ConnectionState.Disconnected);
    }

    /// <summary>Verifies StateChanged event fires on connect.</summary>
    [Fact]
    public async Task ConnectAsync_Fires_StateChanged_Event()
    {
        var service = new FakeGameHubConnectionService();
        ConnectionState? capturedState = null;
        service.StateChanged += (_, state) => capturedState = state;

        await service.ConnectAsync("http://localhost:5000", "test-token");

        capturedState.Should().Be(ConnectionState.Connected);
    }

    /// <summary>Verifies StateChanged event fires on disconnect.</summary>
    [Fact]
    public async Task DisconnectAsync_Fires_StateChanged_Event()
    {
        var service = new FakeGameHubConnectionService();
        await service.ConnectAsync("http://localhost:5000", "test-token");

        ConnectionState? capturedState = null;
        service.StateChanged += (_, state) => capturedState = state;

        await service.DisconnectAsync();

        capturedState.Should().Be(ConnectionState.Disconnected);
    }

    // ── Send ───────────────────────────────────────────────────────────────────

    /// <summary>Verifies SendAsync records the method name and argument.</summary>
    [Fact]
    public async Task SendAsync_Records_Method_And_Arg()
    {
        var service = new FakeGameHubConnectionService();
        await service.SendAsync("TestMethod", "arg1");

        service.SentCommands.Should().ContainSingle();
        service.SentCommands[0].MethodName.Should().Be("TestMethod");
        service.SentCommands[0].Arg1.Should().Be("arg1");
    }

    /// <summary>Verifies two-argument SendAsync records correctly.</summary>
    [Fact]
    public async Task SendAsync_TwoArg_Records_Method_And_Args()
    {
        var service = new FakeGameHubConnectionService();
        await service.SendAsync("TestMethod", "arg1", 42);

        service.SentCommandsTwoArg.Should().ContainSingle();
        service.SentCommandsTwoArg[0].MethodName.Should().Be("TestMethod");
        service.SentCommandsTwoArg[0].Arg1.Should().Be("arg1");
        service.SentCommandsTwoArg[0].Arg2.Should().Be(42);
    }

    // ── Handler registration ───────────────────────────────────────────────────

    /// <summary>Verifies On{T} registers a handler for the given method name.</summary>
    [Fact]
    public void On_Generic_Registers_Handler()
    {
        var service = new FakeGameHubConnectionService();
        var sub = service.On<string>("TestEvent", msg => Task.CompletedTask);

        service.RegisteredHandlers.Should().ContainKey("TestEvent");
        sub.Should().NotBeNull();
    }

    /// <summary>Verifies On{T1,T2} registers a handler for the given method name.</summary>
    [Fact]
    public void On_TwoArg_Registers_Handler()
    {
        var service = new FakeGameHubConnectionService();
        var sub = service.On<string, int>("TestEvent", (msg, val) => Task.CompletedTask);

        service.RegisteredHandlers.Should().ContainKey("TestEvent");
        sub.Should().NotBeNull();
    }

    /// <summary>Verifies SimulateEvent invokes the registered handler with the correct payload.</summary>
    [Fact]
    public async Task SimulateEvent_Invokes_Registered_Handler()
    {
        var service = new FakeGameHubConnectionService();
        string? received = null;
        service.On<string>("TestEvent", msg =>
        {
            received = msg;
            return Task.CompletedTask;
        });

        await service.SimulateEvent("TestEvent", "hello");

        received.Should().Be("hello");
    }

    // ── Multiple commands ──────────────────────────────────────────────────────

    /// <summary>Verifies multiple SendAsync calls are recorded in order.</summary>
    [Fact]
    public async Task SendAsync_Multiple_Calls_Are_Ordered()
    {
        var service = new FakeGameHubConnectionService();
        await service.SendAsync("First", "a");
        await service.SendAsync("Second", "b");

        service.SentCommands.Should().HaveCount(2);
        service.SentCommands[0].MethodName.Should().Be("First");
        service.SentCommands[1].MethodName.Should().Be("Second");
    }
}
