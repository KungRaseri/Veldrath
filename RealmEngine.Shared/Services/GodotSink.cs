using System.IO;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;

namespace RealmEngine.Shared.Services;

/// <summary>
/// Custom Serilog sink that forwards log messages to Godot via events.
/// Allows Godot to receive structured log messages from RealmEngine components.
/// </summary>
public class GodotSink : ILogEventSink
{
    private readonly ITextFormatter _textFormatter;

    /// <summary>
    /// Event raised when a log message is written. Godot can subscribe to this.
    /// </summary>
    public static event Action<LogLevel, string>? LogReceived;

    /// <summary>
    /// Initializes a new instance of the <see cref="GodotSink"/> class.
    /// </summary>
    /// <param name="textFormatter">Optional text formatter for log messages.</param>
    public GodotSink(ITextFormatter? textFormatter = null)
    {
        _textFormatter = textFormatter ?? new MessageTemplateTextFormatter(
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
    }

    /// <summary>
    /// Emits a log event to subscribed listeners.
    /// </summary>
    /// <param name="logEvent">The log event to emit.</param>
    public void Emit(LogEvent logEvent)
    {
        if (LogReceived == null)
            return;

        // Convert Serilog LogEventLevel to our LogLevel enum
        var level = logEvent.Level switch
        {
            LogEventLevel.Verbose => LogLevel.Trace,
            LogEventLevel.Debug => LogLevel.Debug,
            LogEventLevel.Information => LogLevel.Info,
            LogEventLevel.Warning => LogLevel.Warning,
            LogEventLevel.Error => LogLevel.Error,
            LogEventLevel.Fatal => LogLevel.Fatal,
            _ => LogLevel.Info
        };

        // Format the message using the text formatter
        using var writer = new StringWriter();
        _textFormatter.Format(logEvent, writer);
        var formattedMessage = writer.ToString().TrimEnd();

        // Raise event for Godot to consume
        LogReceived.Invoke(level, formattedMessage);
    }

    /// <summary>
    /// Internal method to clear all subscribers. Only called from GodotLogger.
    /// </summary>
    internal static void ClearSubscribers()
    {
        LogReceived = null;
    }
}

/// <summary>
/// Log levels that correspond to both Serilog and Godot logging levels.
/// </summary>
public enum LogLevel
{
    /// <summary>Trace level (most verbose).</summary>
    Trace,
    /// <summary>Debug level.</summary>
    Debug,
    /// <summary>Information level.</summary>
    Info,
    /// <summary>Warning level.</summary>
    Warning,
    /// <summary>Error level.</summary>
    Error,
    /// <summary>Fatal level (most severe).</summary>
    Fatal
}
