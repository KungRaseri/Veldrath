using System.Net;

namespace Veldrath.Discord.Tests.Services;

/// <summary>A delegating handler that returns a fixed response without making real HTTP calls.</summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly string _content;
    private readonly HttpStatusCode _statusCode;

    public FakeHttpMessageHandler(string content, HttpStatusCode statusCode)
    {
        _content = content;
        _statusCode = statusCode;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_content, System.Text.Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}
