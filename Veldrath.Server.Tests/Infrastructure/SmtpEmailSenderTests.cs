using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Veldrath.Server.Infrastructure.Email;

namespace Veldrath.Server.Tests.Infrastructure;

/// <summary>
/// Tests for <see cref="SmtpEmailSender"/>: the "no SMTP host configured" drop path, a real send against a
/// throwaway in-process SMTP responder, and the failure path where the relay refuses the message. The relay is faked
/// at the socket level so the real <see cref="SmtpClient"/> protocol code runs.
/// </summary>
public class SmtpEmailSenderTests
{
    /// <summary>
    /// A minimal one-shot SMTP responder: enough of RFC 5321 for <see cref="SmtpClient"/> to deliver one message.
    /// </summary>
    private sealed class FakeSmtpServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly Task _serve;
        private readonly bool _rejectMailFrom;

        public List<string> Received { get; } = [];
        public string Body { get; private set; } = string.Empty;

        public FakeSmtpServer(bool rejectMailFrom = false)
        {
            _rejectMailFrom = rejectMailFrom;
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _serve = Task.Run(ServeAsync);
        }

        public int Port { get; }

        private async Task ServeAsync()
        {
            try
            {
                using var client = await _listener.AcceptTcpClientAsync();
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII);
                await using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true, NewLine = "\r\n" };

                await writer.WriteLineAsync("220 fake-smtp ready");

                string? line;
                while ((line = await reader.ReadLineAsync()) is not null)
                {
                    Received.Add(line);

                    if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase) ||
                        line.StartsWith("HELO", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync("250-fake-smtp");
                        await writer.WriteLineAsync("250 OK");
                    }
                    else if (line.StartsWith("MAIL FROM", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync(_rejectMailFrom ? "550 sender rejected" : "250 OK");
                    }
                    else if (line.StartsWith("RCPT TO", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync("250 OK");
                    }
                    else if (line.StartsWith("DATA", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync("354 end with <CRLF>.<CRLF>");
                        var body = new StringBuilder();
                        string? dataLine;
                        while ((dataLine = await reader.ReadLineAsync()) is not null && dataLine != ".")
                            body.AppendLine(dataLine);
                        Body = body.ToString();
                        await writer.WriteLineAsync("250 OK queued");
                    }
                    else if (line.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync("221 Bye");
                        return;
                    }
                    else
                    {
                        await writer.WriteLineAsync("250 OK");
                    }
                }
            }
            catch (IOException)
            {
                // The client hung up; nothing to assert.
            }
            catch (ObjectDisposedException)
            {
                // The listener was torn down with the test.
            }
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            await Task.WhenAny(_serve, Task.Delay(TimeSpan.FromSeconds(5)));
        }
    }

    private static SmtpEmailSender Sender(Dictionary<string, string?> settings) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
            NullLogger<SmtpEmailSender>.Instance);

    [Fact]
    public async Task SendAsync_WithoutSmtpHost_DropsTheMessageWithoutThrowing()
    {
        var sender = Sender(new() { ["Email:SenderAddress"] = "noreply@veldrath.test" });

        var act = () => sender.SendAsync("player@example.com", "Welcome", "<p>hi</p>");

        await act.Should().NotThrowAsync("a missing relay must not break the request that triggered the mail");
    }

    [Fact]
    public async Task SendAsync_ConfiguredRelay_DeliversTheMessage()
    {
        await using var server = new FakeSmtpServer();
        var sender = Sender(new()
        {
            ["Email:SmtpHost"] = "127.0.0.1",
            ["Email:SmtpPort"] = server.Port.ToString(),
            ["Email:EnableTls"] = "false",
            ["Email:SenderAddress"] = "noreply@veldrath.test",
            ["Email:SenderName"] = "Veldrath Test",
        });

        await sender.SendAsync("player@example.com", "Welcome to Veldrath", "<p>Your account is ready.</p>");

        server.Received.Should().Contain(l => l.StartsWith("MAIL FROM", StringComparison.OrdinalIgnoreCase));
        server.Received.Should().Contain(l => l.Contains("player@example.com", StringComparison.Ordinal));
        server.Body.Should().Contain("Subject: Welcome to Veldrath");
        server.Body.Should().Contain("Your account is ready.", "the HTML body is sent verbatim");
    }

    [Fact]
    public async Task SendAsync_WithCredentials_StillDelivers()
    {
        await using var server = new FakeSmtpServer();
        var sender = Sender(new()
        {
            ["Email:SmtpHost"] = "127.0.0.1",
            ["Email:SmtpPort"] = server.Port.ToString(),
            ["Email:EnableTls"] = "false",
            ["Email:User"] = "relay-user",
            ["Email:Password"] = "relay-secret",
        });

        await sender.SendAsync("player@example.com", "Credentials", "<p>body</p>");

        server.Received.Should().Contain(l => l.StartsWith("MAIL FROM", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SendAsync_RelayRejectsTheSender_RethrowsSoTheCallerSeesTheFailure()
    {
        await using var server = new FakeSmtpServer(rejectMailFrom: true);
        var sender = Sender(new()
        {
            ["Email:SmtpHost"] = "127.0.0.1",
            ["Email:SmtpPort"] = server.Port.ToString(),
            ["Email:EnableTls"] = "false",
        });

        var act = () => sender.SendAsync("player@example.com", "Rejected", "<p>body</p>");

        await act.Should().ThrowAsync<SmtpException>(
            "the failure is logged and then surfaced — email delivery is not silently lost");
        server.Received.Should().Contain(l => l.StartsWith("MAIL FROM", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SendAsync_UnparseablePort_FallsBackToTheDefaultAndFailsToConnect()
    {
        // Nothing listens on the default relay port of this loopback address, so the send must fail loudly
        // rather than pretend the mail went out.
        var sender = Sender(new()
        {
            ["Email:SmtpHost"] = "127.0.0.1",
            ["Email:SmtpPort"] = "not-a-port",
            ["Email:EnableTls"] = "false",
        });

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var act = () => sender.SendAsync("player@example.com", "No relay", "<p>body</p>", timeout.Token);

        await act.Should().ThrowAsync<Exception>();
    }
}
