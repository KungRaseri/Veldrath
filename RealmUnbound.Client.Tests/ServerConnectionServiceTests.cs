using Microsoft.Extensions.Logging.Abstractions;
using RealmUnbound.Client.Services;
using RealmUnbound.Client.Tests.Infrastructure;

namespace RealmUnbound.Client.Tests;

public class ServerConnectionServiceTests : TestBase
{
    private static (ServerConnectionService Svc, FakeHubConnectionFactory Factory) MakeSut()
    {
        var factory = new FakeHubConnectionFactory();
        var tokens  = new TokenStore();
        var svc     = new ServerConnectionService(NullLogger<ServerConnectionService>.Instance, tokens, factory);
        return (svc, factory);
    }

    // ── Initial state ─────────────────────────────────────────────────────────

    [Fact]
    public void State_Should_Start_As_Disconnected()
    {
        var (svc, _) = MakeSut();

        svc.State.Should().Be(ConnectionState.Disconnected);
    }

    // ── ConnectAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ConnectAsync_Should_Set_State_To_Connected_On_Success()
    {
        var (svc, _) = MakeSut();

        await svc.ConnectAsync("http://localhost");

        svc.State.Should().Be(ConnectionState.Connected);
    }

    [Fact]
    public async Task ConnectAsync_Should_Append_Hub_Path_To_Url()
    {
        var (svc, factory) = MakeSut();

        await svc.ConnectAsync("http://localhost:8080");

        factory.LastCreatedUrl.Should().Be("http://localhost:8080/hubs/game");
    }

    [Fact]
    public async Task ConnectAsync_Should_Call_StartAsync_On_Connection()
    {
        var (svc, factory) = MakeSut();

        await svc.ConnectAsync("http://localhost");

        factory.Connection.StartCallCount.Should().Be(1);
    }

    [Fact]
    public async Task ConnectAsync_Should_Fire_StateChanged_Twice_Connecting_Then_Connected()
    {
        var (svc, _) = MakeSut();
        var states = new List<ConnectionState>();
        svc.StateChanged += s => states.Add(s);

        await svc.ConnectAsync("http://localhost");

        states.Should().ContainInOrder(ConnectionState.Connecting, ConnectionState.Connected);
    }

    [Fact]
    public async Task ConnectAsync_Should_Not_Reconnect_If_Already_Connected()
    {
        var (svc, factory) = MakeSut();
        await svc.ConnectAsync("http://localhost");

        // Second call should be a no-op
        await svc.ConnectAsync("http://localhost");

        factory.Connection.StartCallCount.Should().Be(1);
    }

    [Fact]
    public async Task ConnectAsync_Should_Set_State_To_Failed_When_StartAsync_Throws()
    {
        var (svc, factory) = MakeSut();
        factory.Connection.StartShouldThrow = true;

        var act = async () => await svc.ConnectAsync("http://localhost");

        await act.Should().ThrowAsync<InvalidOperationException>();
        svc.State.Should().Be(ConnectionState.Failed);
    }

    [Fact]
    public async Task ConnectAsync_Should_Rethrow_Exception_When_StartAsync_Throws()
    {
        var (svc, factory) = MakeSut();
        factory.Connection.StartShouldThrow = true;
        factory.Connection.StartException = new InvalidOperationException("bang");

        var act = async () => await svc.ConnectAsync("http://localhost");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("bang");
    }

    // ── Closed / Reconnected event callbacks ──────────────────────────────────

    [Fact]
    public async Task State_Should_Become_Disconnected_When_Connection_Closes()
    {
        var (svc, factory) = MakeSut();
        await svc.ConnectAsync("http://localhost");

        await factory.Connection.SimulateClosedAsync();

        svc.State.Should().Be(ConnectionState.Disconnected);
    }

    [Fact]
    public async Task State_Should_Become_Connected_When_Reconnected_Fires()
    {
        var (svc, factory) = MakeSut();
        await svc.ConnectAsync("http://localhost");
        await factory.Connection.SimulateClosedAsync(); // go disconnected first

        await factory.Connection.SimulateReconnectedAsync();

        svc.State.Should().Be(ConnectionState.Connected);
    }

    // ── DisconnectAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task DisconnectAsync_Should_Call_StopAsync_On_Connection()
    {
        var (svc, factory) = MakeSut();
        await svc.ConnectAsync("http://localhost");

        await svc.DisconnectAsync();

        factory.Connection.StopCallCount.Should().Be(1);
    }

    [Fact]
    public async Task DisconnectAsync_Should_Set_State_To_Disconnected()
    {
        var (svc, _) = MakeSut();
        await svc.ConnectAsync("http://localhost");

        await svc.DisconnectAsync();

        svc.State.Should().Be(ConnectionState.Disconnected);
    }

    [Fact]
    public async Task DisconnectAsync_Should_Not_Throw_When_Never_Connected()
    {
        var (svc, _) = MakeSut();

        var act = async () => await svc.DisconnectAsync();

        await act.Should().NotThrowAsync();
    }

    // ── SendCommandAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task SendCommandAsync_Should_Throw_When_Not_Connected()
    {
        var (svc, _) = MakeSut();

        var act = async () => await svc.SendCommandAsync<string>("SomeMethod", new { });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SendCommandAsync_Should_Not_Throw_When_Connected()
    {
        var (svc, _) = MakeSut();
        await svc.ConnectAsync("http://localhost");

        var act = async () => await svc.SendCommandAsync<string>("SomeMethod", new { });

        await act.Should().NotThrowAsync();
    }

    // ── On<T> ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task On_Should_Throw_When_Not_Connected()
    {
        var (svc, _) = MakeSut();

        var act = () => svc.On<string>("event", _ => { });

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task On_Should_Return_Disposable_When_Connected()
    {
        var (svc, _) = MakeSut();
        await svc.ConnectAsync("http://localhost");

        var registration = svc.On<string>("event", _ => { });

        registration.Should().NotBeNull();
    }

    // ── DisposeAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DisposeAsync_Should_Dispose_Underlying_Connection()
    {
        var (svc, factory) = MakeSut();
        await svc.ConnectAsync("http://localhost");

        await svc.DisposeAsync();

        factory.Connection.DisposeCallCount.Should().Be(1);
    }

    [Fact]
    public async Task DisposeAsync_Should_Not_Throw_When_Never_Connected()
    {
        var (svc, _) = MakeSut();

        var act = async () => await svc.DisposeAsync();

        await act.Should().NotThrowAsync();
    }
}
