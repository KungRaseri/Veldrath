using Microsoft.Extensions.Logging;

namespace RealmUnbound.Server.Infrastructure.Email;

/// <summary>
/// No-operation email sender used in development and test environments.
/// Instead of transmitting the message over SMTP the full email body is
/// written to the configured logger so developers can inspect the content
/// (e.g. confirmation links) from the console or log file without an
/// outgoing mail server.
/// </summary>
public class NullEmailSender(ILogger<NullEmailSender> logger) : IEmailSender
{
    /// <inheritdoc />
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[NullEmailSender] To: {To} | Subject: {Subject}\n{Body}",
            to, subject, htmlBody);

        return Task.CompletedTask;
    }
}
