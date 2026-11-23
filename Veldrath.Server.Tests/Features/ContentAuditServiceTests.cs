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

        public void SeedLocations(IEnumerable<ZoneLocation> locations)
        {
            using var context = CreateDbContext();
            context.ZoneLocations.AddRange(locations);
            context.SaveChanges();
        }

        public async Task SeedMapGraphAsync()
        {
            await using var context = CreateDbContext();
            await MapGraphSeeder.SeedAsync(context);
        }

        /// <summary>
        /// Repoints one edge's destination at a node that belongs to a different map — the shape of the dangling
        /// reference the map-edge rule exists to catch. The node exists in the catalog, so the foreign key holds;
        /// it is simply not on the edge's own map.
        /// </summary>
        public async Task RepointOneEdgeEndAtAnotherMapsNodeAsync()
        {
            await using var context = CreateDbContext();

            var edge = await context.MapEdges.AsNoTracking()
                .OrderBy(candidate => candidate.MapId)
                .ThenBy(candidate => candidate.FromNodeId)
                .Select(candidate => new { candidate.Id, candidate.MapId })
                .FirstAsync();

            var foreignNodeId = await context.MapNodes.AsNoTracking()
                .Where(node => node.MapId != edge.MapId)
                .OrderBy(node => node.MapId)
                .ThenBy(node => node.Id)
                .Select(node => node.Id)
                .FirstAsync();

            await context.MapEdges
                .Where(candidate => candidate.Id == edge.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(candidate => candidate.ToNodeId, foreignNodeId));
        }

        public async Task<int> CountMapEdgesAsync()
        {
            await using var context = CreateDbContext();
            return await context.MapEdges.CountAsync();
        }

        public void Dispose() => _connection.Dispose();
    }

    private sealed class FaultingContentDbContextFactory(Exception failure) : IDbContextFactory<ContentDbContext>
    {
        public ContentDbContext CreateDbContext() => throw failure;
    }

    /// <summary>Waits until every expected fragment has been logged, so the assertions read a finished report.</summary>
    /// <param name="logger">Logger the service writes to.</param>
    /// <param name="fragments">Fragments the report must contain before assertions run.</param>
    private static async Task WaitForAsync(CapturingLogger logger, params string[] fragments)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var messages = logger.Entries.Select(entry => entry.Message).ToList();
            if (fragments.All(fragment => messages.Any(message => message.Contains(fragment, StringComparison.Ordinal))))
            {
                return;
            }

            await Task.Delay(25);
        }
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
    public async Task Start_WhenOnlyWarningsAreFound_LogsTheSummaryAndTheFindingAtWarning()
    {
        using var factory = new SqliteContentDbContextFactory();

        // The authored shelves plus the row a forgotten data migration leaves behind. That finding is a Warning:
        // the catalog still serves, so the run must not be levelled as an error.
        factory.Seed(
        [
            .. ZoneStockSeeder.Authors(),
            new ZoneStockItem { ZoneId = "playground", ItemSlug = "health-potion", SortOrder = 10 },
        ]);

        var logger = new CapturingLogger();
        var service = new ContentAuditService(factory, logger);

        await service.StartAsync(CancellationToken.None);
        await WaitForAsync(logger, "worth a review", "[unclaimed-zone-stock]");

        logger.Entries.Should().ContainSingle(entry => entry.Message.Contains("worth a review", StringComparison.Ordinal))
            .Which.Level.Should().Be(LogLevel.Warning);
        logger.Entries.Should().ContainSingle(entry =>
                entry.Level == LogLevel.Warning && entry.Message.Contains("[unclaimed-zone-stock]", StringComparison.Ordinal));
        logger.Entries.Should().NotContain(entry => entry.Level == LogLevel.Error,
            "a warning-only run serves the catalog, so nothing about it is an error");

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Start_WhenAnErrorAndAWarningAreFound_LogsTheSummaryAndEachFindingAtItsOwnLevel()
    {
        using var factory = new SqliteContentDbContextFactory();

        factory.Seed(
        [
            .. ZoneStockSeeder.Authors(),
            new ZoneStockItem { ZoneId = "playground", ItemSlug = "health-potion", SortOrder = 10 },
        ]);

        // A spawn pool naming an archetype no content holds is an Error, so one run reports both severities: the
        // summary must escalate to Error because of it, while the stock finding stays at Warning.
        factory.SeedLocations(
        [
            new ZoneLocation
            {
                Slug = "probe-location",
                TypeKey = "locations",
                ZoneId = "probe-zone",
                ActorPool = [new ActorPoolEntry { ArchetypeSlug = "ghost-archetype" }],
            },
        ]);

        var logger = new CapturingLogger();
        var service = new ContentAuditService(factory, logger);

        await service.StartAsync(CancellationToken.None);
        await WaitForAsync(logger, "issue(s)", "[unclaimed-zone-stock]", "[missing-spawn-archetype]");

        logger.Entries.Should().ContainSingle(entry => entry.Message.Contains("issue(s)", StringComparison.Ordinal))
            .Which.Level.Should().Be(LogLevel.Error, "one error-level finding escalates the consolidated summary");
        logger.Entries.Should().ContainSingle(entry =>
                entry.Level == LogLevel.Warning && entry.Message.Contains("[unclaimed-zone-stock]", StringComparison.Ordinal));
        logger.Entries.Should().ContainSingle(entry =>
                entry.Level == LogLevel.Error && entry.Message.Contains("[missing-spawn-archetype]", StringComparison.Ordinal));
        logger.Entries.Should().NotContain(entry => entry.Message.Contains("worth a review", StringComparison.Ordinal));

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
    public async Task RunAsync_OnTheSeededMapGraph_ReportsNoMissingMapNode()
    {
        using var factory = new SqliteContentDbContextFactory();
        await factory.SeedMapGraphAsync();

        (await factory.CountMapEdgesAsync()).Should().Be(298,
            "the regression only means something if the seeded graph really carries edges");

        var service = new ContentAuditService(factory, new CapturingLogger());

        var issues = await service.RunAsync(CancellationToken.None);

        // The audit used to load Maps without their Nodes navigation. Nothing populated it (the context does not
        // lazy-load), so every map claimed to have no nodes and both ends of all 298 edges were reported missing:
        // 596 false errors on a freshly seeded catalogue, plus a warning telling operators to reseed a database
        // that was in fact correct.
        issues.Should().NotContain(
            issue => issue.Rule == ContentReferenceRules.MissingMapNode,
            "the map graph seeder writes every edge against nodes on the same map, so a freshly seeded catalogue " +
            "holds no dangling edge end");
    }

    [Fact]
    public async Task RunAsync_Reports_AnEdgeEnd_ThatIsNotANodeOnTheEdgesMap()
    {
        using var factory = new SqliteContentDbContextFactory();
        await factory.SeedMapGraphAsync();
        await factory.RepointOneEdgeEndAtAnotherMapsNodeAsync();

        var service = new ContentAuditService(factory, new CapturingLogger());

        var issues = await service.RunAsync(CancellationToken.None);

        // The negative control: fixing the navigation must not become deleting the rule. Exactly one edge end is
        // now off its map, so exactly one issue is reported — one, not both ends of every seeded edge.
        issues.Should().ContainSingle();
        issues[0].Rule.Should().Be(ContentReferenceRules.MissingMapNode);
        issues[0].Severity.Should().Be(ContentIssueSeverity.Error);
        issues[0].Message.Should().Contain("edge to node");
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
