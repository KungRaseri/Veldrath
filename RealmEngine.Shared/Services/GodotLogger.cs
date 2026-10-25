using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

namespace RealmEngine.Shared.Services;

/// <summary>
/// Static service for integrating RealmEngine logging with Godot.
/// Provides easy setup and event subscription for forwarding logs to GD.Print().
/// </summary>
public static class GodotLogger
{
    private static bool _isInitialized = false;
    private static GodotSink? _godotSink;

    /// <summary>
    /// Initializes the Godot logging integration with Serilog.
    /// Call this once at startup before using any RealmEngine services.
    /// </summary>
    /// <param name="minimumLevel">Minimum log level to forward to Godot (default: Information)</param>
    /// <param name="includeConsole">Whether to also log to console (default: true)</param>
    /// <returns>ILoggerFactory configured for Godot integration</returns>
    public static ILoggerFactory Initialize(
        Serilog.Events.LogEventLevel minimumLevel = Serilog.Events.LogEventLevel.Information,
        bool includeConsole = true)
    {
        if (_isInitialized)
        {
            throw new InvalidOperationException("GodotLogger is already initialized. Call Shutdown() first if you need to reinitialize.");
        }

        _godotSink = new GodotSink();

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .WriteTo.Sink(_godotSink);

        if (includeConsole)
        {
            loggerConfig.WriteTo.Console();
        }

        Log.Logger = loggerConfig.CreateLogger();

        var loggerFactory = new SerilogLoggerFactory(Log.Logger);
        _isInitialized = true;

        return loggerFactory;
    }

    /// <summary>
    /// Subscribes to log events and forwards them to a Godot callback.
    /// </summary>
    /// <param name="callback">Function to call when logs are received. Should use GD.Print() or similar.</param>
    /// <example>
    /// // In your Godot C# script:
    /// GodotLogger.Subscribe((level, message) => 
    /// {
    ///     switch (level)
    ///     {
    ///         case LogLevel.Error:
    ///         case LogLevel.Fatal:
    ///             GD.PrintErr($"[RealmEngine] {message}");
    ///             break;
    ///         case LogLevel.Warning:
    ///             GD.Print($"[RealmEngine] ⚠️ {message}");
    ///             break;
    ///         default:
    ///             GD.Print($"[RealmEngine] {message}");
    ///             break;
    ///     }
    /// });
    /// </example>
    public static void Subscribe(Action<LogLevel, string> callback)
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("GodotLogger must be initialized before subscribing. Call Initialize() first.");
        }

        GodotSink.LogReceived += callback;
    }

    /// <summary>
    /// Unsubscribes from log events.
    /// </summary>
    /// <param name="callback">The same callback function that was used in Subscribe()</param>
    public static void Unsubscribe(Action<LogLevel, string> callback)
    {
        GodotSink.LogReceived -= callback;
    }

    /// <summary>
    /// Removes all subscribers and shuts down the Godot logger.
    /// Call this when your Godot scene is being destroyed.
    /// </summary>
    public static void Shutdown()
    {
        if (_isInitialized)
        {
            // Clear all subscribers using internal method
            GodotSink.ClearSubscribers();

            Log.CloseAndFlush();
            _isInitialized = false;
            _godotSink = null;
        }
    }

    /// <summary>
    /// Gets whether the Godot logger has been initialized.
    /// </summary>
    public static bool IsInitialized => _isInitialized;

    /// <summary>
    /// Convenience method to create a logger for a specific type.
    /// Only works after Initialize() has been called.
    /// </summary>
    /// <typeparam name="T">Type to create logger for</typeparam>
    /// <returns>Logger instance</returns>
    public static Microsoft.Extensions.Logging.ILogger<T> CreateLogger<T>()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("GodotLogger must be initialized before creating loggers. Call Initialize() first.");
        }

        var factory = new SerilogLoggerFactory(Log.Logger);
        return factory.CreateLogger<T>();
    }

    /// <summary>
    /// Quick setup method that initializes logging and provides a pre-configured Godot callback.
    /// This is the easiest way to get started.
    /// </summary>
    /// <param name="minimumLevel">Minimum log level to show in Godot</param>
    /// <returns>ILoggerFactory for dependency injection</returns>
    /// <example>
    /// // In your Godot _Ready() method:
    /// var loggerFactory = GodotLogger.QuickSetup();
    /// 
    /// // Now all RealmEngine logs will appear in Godot console automatically
    /// </example>
    public static ILoggerFactory QuickSetup(Serilog.Events.LogEventLevel minimumLevel = Serilog.Events.LogEventLevel.Information)
    {
        var loggerFactory = Initialize(minimumLevel, includeConsole: false);

        // Subscribe with a default Godot-friendly callback
        Subscribe((level, message) =>
        {
            var prefix = level switch
            {
                LogLevel.Error => "❌",
                LogLevel.Fatal => "💀",
                LogLevel.Warning => "⚠️",
                LogLevel.Info => "ℹ️",
                LogLevel.Debug => "🐛",
                LogLevel.Trace => "🔍",
                _ => "📝"
            };

            // This would need to be called from Godot context
            // The actual GD.Print call should be made by Godot code
            System.Console.WriteLine($"[RealmEngine] {prefix} {message}");
        });

        return loggerFactory;
    }
}
