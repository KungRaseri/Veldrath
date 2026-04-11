namespace Veldrath.Server.Infrastructure.Email;

/// <summary>
/// Contract for sending outgoing email messages from the server.
/// Implementations include <see cref="SmtpEmailSender"/> (production) and
/// <see cref="NullEmailSender"/> (development / testing).
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends a single HTML email to the specified recipient.
    /// Implementations may silently drop the message (e.g. <see cref="NullEmailSender"/>)
    /// or forward it via SMTP.
    /// </summary>
    /// <param name="to">Recipient email address.</param>
    /// <param name="subject">Message subject line.</param>
    /// <param name="htmlBody">Full HTML content of the message body.</param>
    /// <param name="ct">Optional cancellation token.</param>
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}
