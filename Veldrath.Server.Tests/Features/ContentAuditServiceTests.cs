using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Seeders;
using RealmEngine.Data.Validation.Content;
using Veldrath.Server.Features.Content;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Tests for <see cref="ContentAuditService"/>'s contract: the startup audit may report problems but must never keep
/// the host from booting, and a shutdown must not surface as a failure.
/// </summary>
/// <remarks>
/// The audit used to project owned-JSON collections, which needs SQL APPLY and so only translated on Npgsql; it now
/// materialises and flattens them in memory, so <see cref="ContentAuditService.RunAsync"/> runs against the SQLite
/// test host too. The tests drive the real service there, and one uses a faulting factory to prove a broken audit
/// cannot stop the host.
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

        public void Seed(IEnumerable<ZoneStockItem> rows)
        {
            using var context = CreateDbContext();
            context.ZoneStockItems.AddRange(rows);
            context.SaveChanges();
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
    public async Task Start_OnSqlite_RunsTheAuditInsteadOfLoggingATranslationFailure()
    {
        using var factory = new SqliteContentDbContextFactory();
        var logger = new CapturingLogger();
        var service = new ContentAuditService(factory, logger);

        await service.StartAsync(CancellationToken.None);

        // The audit used to project nested collections, which needs SQL APPLY — untranslatable on SQLite. It
        // failed on every SQLite host and, because the startup run catches everything, did so silently. It now
        // materialises and flattens in memory, so the run completes and says what it found.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (logger.Entries.Count == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        logger.Entries.Should().Contain(entry =>
            entry.Message.Contains("found no dangling references", StringComparison.Ordinal));
        logger.Entries.Should().NotContain(entry => entry.Level == LogLevel.Error);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RunAsync_Reports_A_StoredStockRow_ThatNoAuthoredShelfLists()
    {
        using var factory = new SqliteContentDbContextFactory();

        // The shelves the seeder authors, as a seeded database holds them — plus the row a forgotten data migration
        // would leave behind. `playground` is the dev sandbox, which the seeder deliberately does not stock.
        factory.Seed(
        [
            .. ZoneStockSeeder.Authors(),
            new ZoneStockItem { ZoneId = "playground", ItemSlug = "health-potion", SortOrder = 10 },
        ]);

        var service = new ContentAuditService(factory, new CapturingLogger());

        var issues = await service.RunAsync(CancellationToken.None);

        issues.Should().ContainSingle();
        issues[0].Rule.Should().Be(ContentRules.UnclaimedZoneStock);
        issues[0].Severity.Should().Be(ContentIssueSeverity.Warning);
        issues[0].Identifier.Should().Be("playground/health-potion");
        issues[0].Message.Should().Contain("playground").And.Contain("health-potion");
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
