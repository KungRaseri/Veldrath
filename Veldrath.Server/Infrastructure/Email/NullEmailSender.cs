using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Veldrath.Server.Infrastructure.Email;

/// <summary>
/// No-operation email sender used when no SMTP host is configured.
/// <para>
/// In development the full body is written to the logger so a developer can copy the
/// confirmation or password-reset link out of the console instead of running a mail server.
/// Outside development only the envelope is logged: the body carries single-use tokens, which
/// must not end up in production logs (CodeQL cs/cleartext-storage).
/// </para>
/// </summary>
public class NullEmailSender(IHostEnvironment environment, ILogger<NullEmailSender> logger) : IEmailSender
{
    /// <inheritdoc />
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (environment.IsDevelopment())
        {
            logger.LogInformation(
                "[NullEmailSender] To: {To} | Subject: {Subject}\n{Body}",
                to, subject, htmlBody);
        }
        else
        {
            logger.LogInformation(
                "[NullEmailSender] To: {To} | Subject: {Subject} (body withheld outside development).",
                EmailAddressMask.Mask(to), subject);
        }

        return Task.CompletedTask;
    }
}
