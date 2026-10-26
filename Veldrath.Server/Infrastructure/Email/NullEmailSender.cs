namespace Veldrath.Server.Infrastructure.Email;

/// <summary>
/// Fallback sender used when no SMTP host is configured: it records who was mailed and what about, and delivers
/// nothing.
/// </summary>
/// <remarks>
/// The message body is never logged, in any environment. Reset and verification mails carry one-time links that
/// grant account access, and the server writes logs to a rolling file sink in clear text, so a body in a log is a
/// credential in a log. Development inspects real mail through Mailpit (see <c>docker-compose.dev.yml</c>) instead
/// of reading it out of the console.
/// </remarks>
/// <param name="environment">Host environment, used only to point a developer at Mailpit.</param>
/// <param name="logger">Logger instance.</param>
public class NullEmailSender(IHostEnvironment environment, ILogger<NullEmailSender> logger) : IEmailSender
{
    /// <inheritdoc />
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var hint = environment.IsDevelopment()
            ? " Start the dev stack and read it in Mailpit (http://localhost:8025)."
            : string.Empty;

        logger.LogInformation(
            "[NullEmailSender] Suppressed a message to {To} with subject {Subject}; no SMTP host is configured.{Hint}",
            EmailAddressMask.Mask(to),
            subject,
            hint);

        return Task.CompletedTask;
    }
}
