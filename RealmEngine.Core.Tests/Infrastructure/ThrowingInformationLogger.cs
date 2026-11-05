using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealmEngine.Core.Tests.Infrastructure;

/// <summary>
/// An <see cref="ILogger{TCategoryName}"/> whose <see cref="LogLevel.Information"/> writes throw,
/// used to prove that handlers report unexpected failures through their error contract instead of
/// letting them escape. Every other level is a no-op.
/// </summary>
/// <typeparam name="TCategoryName">The log category.</typeparam>
public sealed class ThrowingInformationLogger<TCategoryName> : ILogger<TCategoryName>
{
    /// <summary>Returns a category logger that throws when information-level messages are written.</summary>
    public static ThrowingInformationLogger<TCategoryName> Instance { get; } = new();

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
        NullLogger<TCategoryName>.Instance.BeginScope(state);

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (logLevel == LogLevel.Information)
            throw new InvalidOperationException("log sink unavailable");
    }
}
