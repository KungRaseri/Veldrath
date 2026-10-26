using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Veldrath.Server.Infrastructure.Email;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// The fallback mail sender must never put a message body in a log: reset and verification mails carry one-time
/// links, and the server writes logs to a rolling file in clear text.
/// </summary>
public class NullEmailSenderTests
{
    private sealed class CapturingLogger : ILogger<NullEmailSender>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }

    private static NullEmailSender MakeSender(string environmentName, out CapturingLogger logger)
    {
        logger = new CapturingLogger();
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(candidate => candidate.EnvironmentName).Returns(environmentName);
        return new NullEmailSender(environment.Object, logger);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task The_message_body_is_never_logged(string environmentName)
    {
        const string secretBody = "<a href=\"https://veldrath.test/reset?token=super-secret-token\">reset</a>";
        var sender = MakeSender(environmentName, out var logger);

        await sender.SendAsync("player@example.com", "Reset your password", secretBody);

        logger.Messages.Should().NotBeEmpty("the send should still be observable");
        logger.Messages.Should().OnlyContain(message => !message.Contains("super-secret-token"));
        logger.Messages.Should().OnlyContain(message => !message.Contains("https://veldrath.test"));
        logger.Messages.Should().Contain(message => message.Contains("Reset your password"));
    }

    [Fact]
    public async Task Development_points_the_developer_at_mailpit()
    {
        var sender = MakeSender("Development", out var logger);

        await sender.SendAsync("player@example.com", "Verify your email", "<p>body</p>");

        logger.Messages.Should().Contain(message => message.Contains("Mailpit"));
    }
}
