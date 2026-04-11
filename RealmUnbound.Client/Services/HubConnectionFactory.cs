using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.SignalR.Client;

namespace Veldrath.Client.Services;

/// <summary>
/// Abstracts <see cref="HubConnection"/> construction so that
/// <see cref="ServerConnectionService"/> can be unit-tested without a real WebSocket server.
/// </summary>
public interface IHubConnectionFactory
{
    IHubConnection CreateConnection(string hubUrl, Func<Task<string?>> accessTokenProvider);
}

/// <summary>
/// Production implementation — builds a real SignalR <see cref="HubConnection"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public class HubConnectionFactory : IHubConnectionFactory
{
    public IHubConnection CreateConnection(string hubUrl, Func<Task<string?>> accessTokenProvider)
    {
        var inner = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = accessTokenProvider;
            })
            .WithAutomaticReconnect()
            .Build();
        return new HubConnectionWrapper(inner);
    }
}
