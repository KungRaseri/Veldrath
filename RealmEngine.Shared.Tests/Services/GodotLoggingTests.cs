using FluentAssertions;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Services;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;
using LogLevel = RealmEngine.Shared.Services.LogLevel;

namespace RealmEngine.Shared.Tests.Services;

/// <summary>
/// Serialises the tests that touch <see cref="GodotSink.LogReceived"/> and the static
/// <see cref="GodotLogger"/> state so they cannot interleave with each other or with other
/// collections that log through Serilog.
/// </summary>
[CollectionDefinition("GodotLogging", DisableParallelization = true)]
public sealed class GodotLoggingCollection
{
    /// <summary>The collection name used by the Godot logging tests.</summary>
    public const string Name = "GodotLogging";
}

/// <summary>
/// Behaviour tests for <see cref="GodotSink"/>: level translation, message formatting and
/// the no-subscriber fast path.
/// </summary>
[Collection("GodotLogging")]
[Trait("Category", "Services")]
public class GodotSinkTests : IDisposable
{
    public GodotSinkTests() => GodotLogger.Shutdown();

    public void Dispose() => GodotLogger.Shutdown();

    private static LogEvent Event(LogEventLevel level, string template, params (string Name, object Value)[] properties)
    {
        var parsed = new MessageTemplateParser().Parse(template);
        var props = properties.Select(p => new LogEventProperty(p.Name, new ScalarValue(p.Value)));
        return new LogEvent(DateTimeOffset.UtcNow, level, null, parsed, props);
    }

    [Fact]
    public void Emit_WithNoSubscribers_DoesNotThrowAndReportsNothing()
    {
        // Arrange
        var sink = new GodotSink();

        // Act
        var act = () => sink.Emit(Event(LogEventLevel.Information, "hello"));

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(LogEventLevel.Verbose, LogLevel.Trace)]
    [InlineData(LogEventLevel.Debug, LogLevel.Debug)]
    [InlineData(LogEventLevel.Information, LogLevel.Info)]
    [InlineData(LogEventLevel.Warning, LogLevel.Warning)]
    [InlineData(LogEventLevel.Error, LogLevel.Error)]
    [InlineData(LogEventLevel.Fatal, LogLevel.Fatal)]
    public void Emit_TranslatesSerilogLevelsToGodotLevels(LogEventLevel serilogLevel, LogLevel expected)
    {
        // Arrange
        var sink = new GodotSink();
        var received = new List<LogLevel>();
        void Callback(LogLevel level, string _) => received.Add(level);
        GodotSink.LogReceived += Callback;

        try
        {
            // Act
            sink.Emit(Event(serilogLevel, "message"));
        }
        finally
        {
            GodotSink.LogReceived -= Callback;
        }

        // Assert
        received.Should().ContainSingle().Which.Should().Be(expected);
    }

    [Fact]
    public void Emit_FormatsTheMessageWithTheDefaultTemplate()
    {
        // Arrange
        var sink = new GodotSink();
        string? formatted = null;
        void Callback(LogLevel _, string message) => formatted = message;
        GodotSink.LogReceived += Callback;

        try
        {
            // Act
            sink.Emit(Event(LogEventLevel.Warning, "Item {Item} could not be equipped", ("Item", "Rusty Sword")));
        }
        finally
        {
            GodotSink.LogReceived -= Callback;
        }

        // Assert
        formatted.Should().NotBeNull();
        formatted.Should().Contain("WRN");
        formatted.Should().Contain("Item Rusty Sword could not be equipped");
    }

    [Fact]
    public void Emit_PropagatesSubscriberExceptions()
    {
        // Arrange
        var sink = new GodotSink();
        void Callback(LogLevel _, string __) => throw new InvalidOperationException("boom");
        GodotSink.LogReceived += Callback;

        try
        {
            // Act
            var act = () => sink.Emit(Event(LogEventLevel.Information, "message"));

            // Assert — the sink does not swallow handler failures
            act.Should().Throw<InvalidOperationException>().WithMessage("boom");
        }
        finally
        {
            GodotSink.LogReceived -= Callback;
        }
    }

    [Fact]
    public void Emit_FansOutToEverySubscriber()
    {
        // Arrange
        var sink = new GodotSink();
        var messages = new List<string>();
        void First(LogLevel _, string message) => messages.Add($"first:{message}");
        void Second(LogLevel _, string message) => messages.Add($"second:{message}");
        GodotSink.LogReceived += First;
        GodotSink.LogReceived += Second;

        try
        {
            // Act
            sink.Emit(Event(LogEventLevel.Information, "ping"));
        }
        finally
        {
            GodotSink.LogReceived -= First;
            GodotSink.LogReceived -= Second;
        }

        // Assert
        messages.Should().HaveCount(2);
        messages.Should().Contain(m => m.StartsWith("first:"));
        messages.Should().Contain(m => m.StartsWith("second:"));
    }
}

/// <summary>
/// Behaviour tests for the static <see cref="GodotLogger"/> lifecycle: initialisation guard,
/// subscribe/unsubscribe ordering, level filtering and reinitialisation after shutdown.
/// </summary>
[Collection(GodotLoggingCollection.Name)]
[Trait("Category", "Services")]
public class GodotLoggerTests : IDisposable
{
    public GodotLoggerTests() => GodotLogger.Shutdown();

    public void Dispose() => GodotLogger.Shutdown();

    [Fact]
    public void IsInitialized_IsFalseBeforeInitialize()
    {
        // Act & Assert
        GodotLogger.IsInitialized.Should().BeFalse();
    }

    [Fact]
    public void Initialize_SetsTheInitializedFlag()
    {
        // Act
        GodotLogger.Initialize(LogEventLevel.Information, includeConsole: false);

        // Assert
        GodotLogger.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public void Initialize_ReturnsAFactoryThatCreatesWorkingLoggers()
    {
        // Arrange
        var factory = GodotLogger.Initialize(LogEventLevel.Information, includeConsole: false);
        var logger = factory.CreateLogger("Test");
        string? captured = null;
        void Callback(LogLevel _, string message) => captured = message;
        GodotLogger.Subscribe(Callback);

        // Act
        logger.LogInformation("hello from {Source}", "test");

        // Assert
        captured.Should().NotBeNull();
        captured.Should().Contain("hello from test");
    }

    [Fact]
    public void Initialize_WhenAlreadyInitialized_Throws()
    {
        // Arrange
        GodotLogger.Initialize(LogEventLevel.Information, includeConsole: false);

        // Act
        var act = () => GodotLogger.Initialize();

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*already initialized*");
    }

    [Fact]
    public void Initialize_FiltersOutEventsBelowTheMinimumLevel()
    {
        // Arrange
        var factory = GodotLogger.Initialize(LogEventLevel.Warning, includeConsole: false);
        var logger = factory.CreateLogger("Test");
        var received = new List<string>();
        void Callback(LogLevel _, string message) => received.Add(message);
        GodotLogger.Subscribe(Callback);

        // Act
        logger.LogInformation("dropped");
        logger.LogWarning("kept");

        // Assert
        received.Should().ContainSingle().Which.Should().Contain("kept");
    }

    [Fact]
    public void Subscribe_BeforeInitialize_Throws()
    {
        // Act
        var act = () => GodotLogger.Subscribe((_, _) => { });

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*initialized*before subscribing*");
    }

    [Fact]
    public void Unsubscribe_StopsDelivery()
    {
        // Arrange
        var factory = GodotLogger.Initialize(LogEventLevel.Information, includeConsole: false);
        var logger = factory.CreateLogger("Test");
        var received = new List<string>();
        void Callback(LogLevel _, string message) => received.Add(message);
        GodotLogger.Subscribe(Callback);

        // Act
        logger.LogInformation("one");
        GodotLogger.Unsubscribe(Callback);
        logger.LogInformation("two");

        // Assert
        received.Should().ContainSingle().Which.Should().Contain("one");
    }

    [Fact]
    public void Unsubscribe_WithoutBeingSubscribed_IsHarmless()
    {
        // Act
        var act = () => GodotLogger.Unsubscribe((_, _) => { });

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Shutdown_ClearsTheInitializedFlagAndAllSubscribers()
    {
        // Arrange
        var factory = GodotLogger.Initialize(LogEventLevel.Information, includeConsole: false);
        var logger = factory.CreateLogger("Test");
        var received = new List<string>();
        void Callback(LogLevel _, string message) => received.Add(message);
        GodotLogger.Subscribe(Callback);

        // Act
        GodotLogger.Shutdown();
        logger.LogInformation("after shutdown");

        // Assert
        GodotLogger.IsInitialized.Should().BeFalse();
        received.Should().BeEmpty();
    }

    [Fact]
    public void Shutdown_WhenNotInitialized_IsHarmless()
    {
        // Act
        var act = GodotLogger.Shutdown;

        // Assert
        act.Should().NotThrow();
        GodotLogger.IsInitialized.Should().BeFalse();
    }

    [Fact]
    public void Initialize_AfterShutdown_SucceedsAgain()
    {
        // Arrange
        GodotLogger.Initialize(LogEventLevel.Information, includeConsole: false);
        GodotLogger.Shutdown();

        // Act
        GodotLogger.Initialize(LogEventLevel.Information, includeConsole: false);

        // Assert
        GodotLogger.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public void CreateLogger_BeforeInitialize_Throws()
    {
        // Act
        var act = () => GodotLogger.CreateLogger<GodotLoggerTests>();

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*before creating loggers*");
    }

    [Fact]
    public void CreateLogger_AfterInitialize_ReturnsALoggerBoundToTheGodotSink()
    {
        // Arrange
        GodotLogger.Initialize(LogEventLevel.Information, includeConsole: false);
        var received = new List<string>();
        void Callback(LogLevel _, string message) => received.Add(message);
        GodotLogger.Subscribe(Callback);

        // Act
        var logger = GodotLogger.CreateLogger<GodotLoggerTests>();
        logger.LogError("everything is on fire");

        // Assert
        received.Should().ContainSingle().Which.Should().Contain("everything is on fire");
    }

    [Fact]
    public void QuickSetup_InitializesWithoutTheConsoleSink()
    {
        // Arrange
        var factory = GodotLogger.QuickSetup(LogEventLevel.Information);

        // Act
        var logger = factory.CreateLogger("Quick");
        var act = () => logger.LogInformation("quick setup message");

        // Assert
        GodotLogger.IsInitialized.Should().BeTrue();
        act.Should().NotThrow();
    }

    [Fact]
    public void QuickSetup_ForwardsMessagesThroughItsDefaultCallback()
    {
        // Arrange
        GodotLogger.QuickSetup(LogEventLevel.Information);
        var captured = new List<string>();
        void Callback(LogLevel _, string message) => captured.Add(message);
        GodotLogger.Subscribe(Callback);

        // Act
        GodotLogger.CreateLogger<GodotLoggerTests>().LogInformation("debug detail");

        // Assert
        captured.Should().ContainSingle().Which.Should().Contain("debug detail");
    }
}
