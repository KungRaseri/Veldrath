using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Veldrath.Server.Infrastructure.Email;

/// <summary>
/// Production email sender backed by a standard SMTP relay.
/// Reads server settings from the <c>Email</c> configuration section:
/// <list type="bullet">
///   <item><c>Email:SmtpHost</c> — SMTP server hostname (required; if empty the message is dropped).</item>
///   <item><c>Email:SmtpPort</c> — SMTP port, default 587.</item>
///   <item><c>Email:User</c> — SMTP username (optional for relay-only servers).</item>
///   <item><c>Email:Password</c> — SMTP password (optional).</item>
///   <item><c>Email:SenderAddress</c> — From address, default <c>noreply@veldrath.com</c>.</item>
///   <item><c>Email:SenderName</c> — From display name, default <c>Veldrath</c>.</item>
/// </list>
/// STARTTLS (<c>EnableSsl = true</c>) is always requested.
/// </summary>
public class SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    /// <inheritdoc />
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var host = config["Email:SmtpHost"];
        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogWarning("SmtpEmailSender: Email:SmtpHost is not configured — message to {To} dropped.", to);
            return;
        }

        var port           = int.TryParse(config["Email:SmtpPort"], out var p) ? p : 587;
        var user           = config["Email:User"];
        var password       = config["Email:Password"];
        var senderAddress  = config["Email:SenderAddress"] ?? "noreply@veldrath.com";
        var senderName     = config["Email:SenderName"]    ?? "Veldrath";

        using var client = new SmtpClient(host, port) { EnableSsl = true };
        if (!string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(password))
            client.Credentials = new NetworkCredential(user, password);

        using var message = new MailMessage
        {
            From       = new MailAddress(senderAddress, senderName),
            Subject    = subject,
            Body       = htmlBody,
            IsBodyHtml = true,
        };
        message.To.Add(to);

        try
        {
            await client.SendMailAsync(message, ct);
            logger.LogInformation("SmtpEmailSender: sent '{Subject}' to {To}.", subject, to);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SmtpEmailSender: failed to send '{Subject}' to {To}.", subject, to);
            throw;
        }
    }
}
