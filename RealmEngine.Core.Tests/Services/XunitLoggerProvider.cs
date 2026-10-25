using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace RealmEngine.Core.Tests.Services;

/// <summary>
/// Custom logger provider that outputs to XUnit test output
/// </summary>
public class XunitLoggerProvider : ILoggerProvider
{
    private readonly ITestOutputHelper _output;

    public XunitLoggerProvider(ITestOutputHelper output)
    {
        _output = output;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new XunitLogger(_output, categoryName);
    }

    public void Dispose() { }
}

/// <summary>
/// Logger that writes to XUnit test output
/// </summary>
public class XunitLogger : ILogger
{
    private readonly ITestOutputHelper _output;
    private readonly string _categoryName;

    public XunitLogger(ITestOutputHelper output, string categoryName)
    {
        _output = output;
        _categoryName = categoryName;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        try
        {
            var message = formatter(state, exception);
            var level = logLevel switch
            {
                LogLevel.Trace => "TRC",
                LogLevel.Debug => "DBG",
                LogLevel.Information => "INF",
                LogLevel.Warning => "WRN",
                LogLevel.Error => "ERR",
                LogLevel.Critical => "CRT",
                _ => "???"
            };

            var shortCategory = _categoryName.Split('.').Last();
            _output.WriteLine($"[{level}] {shortCategory}: {message}");

            if (exception != null)
            {
                _output.WriteLine($"      Exception: {exception.Message}");
                _output.WriteLine($"      {exception.StackTrace}");
            }
        }
        catch
        {
            // Ignore errors writing to test output
        }
    }
}
