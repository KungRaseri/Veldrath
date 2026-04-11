using Microsoft.Extensions.Logging.Abstractions;
using Veldrath.Client.Services;
using Veldrath.Client.Tests.Infrastructure;

namespace Veldrath.Client.Tests;

public class ServerConnectionServiceTests : TestBase
{
    private static (ServerConnectionService Svc, FakeHubConnectionFactory Factory) MakeSut(
        TokenStore? tokens = null, FakeAuthService? auth = null)
    {
        var factory = new FakeHubConnectionFactory();
        var svc     = new ServerConnectionService(
            NullLogger<ServerConnectionService>.Instance,
            tokens ?? new TokenStore(),
            factory,
            auth   ?? new FakeAuthService());
        return (svc, factory);
    }

    // Initial state
    [Fact]
    public void State_Should_Start_As_Disconnected()
    {
        var (svc, _) = MakeSut();

        svc.State.Should().Be(ConnectionState.Disconnected);
    }

    // ConnectAsync
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

    // Closed / Reconnected event callbacks
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

    // DisconnectAsync
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

    // SendCommandAsync
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

    // On<T>
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

    // DisposeAsync
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

    // AccessTokenProvider — silent refresh
    [Fact]
    public async Task AccessTokenProvider_Should_Call_RefreshAsync_When_Token_Is_Expiring()
    {
        var tokens = new TokenStore();
        var auth   = new FakeAuthService();
        var (svc, factory) = MakeSut(tokens: tokens, auth: auth);

        // Seed an access token that expires in 90 seconds (< the 2-min IsExpiringSoon window)
        tokens.Set("old-token", "my-refresh", "user", Guid.NewGuid(),
                   DateTimeOffset.UtcNow.AddSeconds(90));

        await svc.ConnectAsync("http://localhost");

        // Invoke the provider exactly as SignalR does on reconnect
        await factory.LastAccessTokenProvider!.Invoke();

        auth.RefreshCallCount.Should().Be(1);
    }

    [Fact]
    public async Task AccessTokenProvider_Should_Not_Call_RefreshAsync_When_Token_Is_Valid()
    {
        var tokens = new TokenStore();
        var auth   = new FakeAuthService();
        var (svc, factory) = MakeSut(tokens: tokens, auth: auth);

        // Token is valid for 10 more minutes — not expiring soon
        tokens.Set("valid-token", "my-refresh", "user", Guid.NewGuid(),
                   DateTimeOffset.UtcNow.AddMinutes(10));

        await svc.ConnectAsync("http://localhost");
        await factory.LastAccessTokenProvider!.Invoke();

        auth.RefreshCallCount.Should().Be(0);
    }

    [Fact]
    public async Task AccessTokenProvider_Should_Not_Call_RefreshAsync_When_No_RefreshToken()
    {
        var tokens = new TokenStore();
        var auth   = new FakeAuthService();
        var (svc, factory) = MakeSut(tokens: tokens, auth: auth);

        // Expiring but no refresh token — can't silently re-auth
        tokens.Set("old-token", string.Empty, "user", Guid.NewGuid(),
                   DateTimeOffset.UtcNow.AddSeconds(30));
        // Overwrite RefreshToken with null to simulate missing token
        tokens.RefreshToken = null;

        await svc.ConnectAsync("http://localhost");
        await factory.LastAccessTokenProvider!.Invoke();

        auth.RefreshCallCount.Should().Be(0);
    }

    [Fact]
    public async Task AccessTokenProvider_Should_Return_Null_When_RefreshAsync_Fails()
    {
        var tokens = new TokenStore();
        var auth   = new FakeAuthService { RefreshResult = false };
        var (svc, factory) = MakeSut(tokens: tokens, auth: auth);

        // Token is expiring so refresh will be attempted
        tokens.Set("old-token", "refresh-token", "user", Guid.NewGuid(),
                   DateTimeOffset.UtcNow.AddSeconds(90));

        await svc.ConnectAsync("http://localhost");

        var result = await factory.LastAccessTokenProvider!.Invoke();

        result.Should().BeNull();
    }

    [Fact]
    public async Task AccessTokenProvider_Should_Fire_ConnectionLost_Via_Closed_When_Refresh_Fails()
    {
        var tokens = new TokenStore();
        var auth   = new FakeAuthService { RefreshResult = false };
        var (svc, factory) = MakeSut(tokens: tokens, auth: auth);

        tokens.Set("old-token", "refresh-token", "user", Guid.NewGuid(),
                   DateTimeOffset.UtcNow.AddSeconds(90));

        await svc.ConnectAsync("http://localhost");

        var connectionLostFired = false;
        svc.ConnectionLost += () => connectionLostFired = true;

        // Simulate SignalR rejecting the null token and closing the connection
        await factory.Connection.SimulateClosedAsync();

        connectionLostFired.Should().BeTrue();
    }

    // Version compatibility checks (HandleServerInfo)
    [Fact]
    public async Task HandleServerInfo_Should_Not_Fire_VersionMismatch_When_Versions_Are_Compatible()
    {
        var (svc, factory) = MakeSut();
        await svc.ConnectAsync("http://localhost");

        var fired = false;
        svc.VersionMismatch += (_, _) => fired = true;

        // MinCompatibleClientVersion = "0.0" — client assembly is at 0.1 (≥ 0.0), so compatible
        factory.Connection.SimulateReceive("ServerInfo",
            new Veldrath.Contracts.Connection.ServerInfoPayload("conn-1", "0.1", "0.0"));

        fired.Should().BeFalse();
    }

    [Fact]
    public async Task HandleServerInfo_Should_Fire_VersionMismatch_When_Client_Is_Too_Old()
    {
        var (svc, factory) = MakeSut();
        await svc.ConnectAsync("http://localhost");

        string? capturedClient = null;
        string? capturedServer = null;
        svc.VersionMismatch += (c, s) => { capturedClient = c; capturedServer = s; };

        // Server requires minimum 9.99 — current client is well below that
        factory.Connection.SimulateReceive("ServerInfo",
            new Veldrath.Contracts.Connection.ServerInfoPayload("conn-1", "9.99", "9.99"));

        capturedClient.Should().NotBeNullOrEmpty();
        capturedServer.Should().Be("9.99");
    }

    [Fact]
    public async Task HandleServerInfo_Should_Fire_VersionMismatch_When_Server_Is_Too_Old()
    {
        var (svc, factory) = MakeSut();
        await svc.ConnectAsync("http://localhost");

        string? capturedServer = null;
        svc.VersionMismatch += (_, s) => capturedServer = s;

        // Server version is 0.0 — client requires MinCompatibleServerVersion = "0.1"
        // so this fires VersionMismatch only if the assembly attribute is present.
        // In the test assembly the attribute is absent; fall-back minServer is "0.1",
        // so a server at 0.0 is rejected.
        factory.Connection.SimulateReceive("ServerInfo",
            new Veldrath.Contracts.Connection.ServerInfoPayload("conn-1", "0.0", "0.0"));

        capturedServer.Should().Be("0.0");
    }

    [Fact]
    public async Task HandleServerInfo_Compatible_Allows_Higher_Minor_Than_MinCompatible()
    {
        var (svc, factory) = MakeSut();
        await svc.ConnectAsync("http://localhost");

        var fired = false;
        svc.VersionMismatch += (_, _) => fired = true;

        // Server min is 0.0, client is at 0.x (anything ≥ 0) → compatible
        factory.Connection.SimulateReceive("ServerInfo",
            new Veldrath.Contracts.Connection.ServerInfoPayload("conn-1", "0.5", "0.0"));

        fired.Should().BeFalse();
    }
}
