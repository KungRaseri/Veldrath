using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using Veldrath.Server.Features.Content;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Tests for <see cref="ContentAuditService"/>'s contract: the startup audit may report problems but must never keep
/// the host from booting, and a shutdown must not surface as a failure.
/// </summary>
/// <remarks>
/// The audit's own queries (owned-JSON collection projections and <c>ILike</c> search) only translate on Npgsql, so
/// on the SQLite test host <see cref="ContentAuditService.RunAsync"/> always faults. That is exactly the situation the
/// service exists to survive, so the tests drive the real handler and assert the logged outcome instead of bypassing
/// it. Full findings coverage needs a Postgres-backed host.
/// </remarks>
public class ContentAuditServiceTests
{
    private sealed class CapturingLogger : ILogger<ContentAuditService>
    {
        public ConcurrentQueue<(LogLevel Level, string Message)> Entries { get; } = new();

        public TaskCompletionSource<string> Failed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            Entries.Enqueue((logLevel, message));
            if (logLevel == LogLevel.Error && message.Contains("failed to run", StringComparison.Ordinal))
                Failed.TrySetResult(message);
        }
    }

    private sealed class SqliteContentDbContextFactory : IDbContextFactory<ContentDbContext>, IDisposable
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");

        public SqliteContentDbContextFactory() => _connection.Open();

        public ContentDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<ContentDbContext>()
                .UseSqlite(_connection)
                .Options;
            var context = new ContentDbContext(options);
            context.Database.EnsureCreated();
            return context;
        }

        public void Dispose() => _connection.Dispose();
    }

    private sealed class FaultingContentDbContextFactory(Exception failure) : IDbContextFactory<ContentDbContext>
    {
        public ContentDbContext CreateDbContext() => throw failure;
    }

    [Fact]
    public async Task Start_WhenTheAuditFaults_LogsTheFailureAndStopsCleanly()
    {
        var logger = new CapturingLogger();
        var service = new ContentAuditService(
            new FaultingContentDbContextFactory(new InvalidOperationException("content catalog unavailable")),
            logger);

        await service.StartAsync(CancellationToken.None);

        var logged = await logger.Failed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        logged.Should().Contain("failed to run");
        logger.Entries.Should().Contain(e => e.Level == LogLevel.Error);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Start_WhenTheQueriesCannotTranslate_LogsInsteadOfCrashingTheHost()
    {
        using var factory = new SqliteContentDbContextFactory();
        var logger = new CapturingLogger();
        var service = new ContentAuditService(factory, logger);

        await service.StartAsync(CancellationToken.None);

        var logged = await logger.Failed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        logged.Should().Contain("Content reference audit failed to run");
        logger.Entries.Should().NotContain(e => e.Message.Contains("dangling reference", StringComparison.Ordinal));
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Start_WhenTheHostIsShuttingDown_StaysSilent()
    {
        var logger = new CapturingLogger();
        var service = new ContentAuditService(
            new FaultingContentDbContextFactory(new OperationCanceledException("host is stopping")),
            logger);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        logger.Entries.Should().BeEmpty("a cancelled audit is a shutdown, not a defect worth an error log");
    }
}
