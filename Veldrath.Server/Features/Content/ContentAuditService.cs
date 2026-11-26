using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Seeders;
using RealmEngine.Data.Validation.Content;

namespace Veldrath.Server.Features.Content;

/// <summary>
/// Runs the content reference audit once at startup and logs every finding — errors at <c>Error</c>, warnings at
/// <c>Warning</c>.
/// </summary>
/// <remarks>
/// <para>
/// This is the runtime half of the failsafe rule: missing content is reported loudly and never silently replaced by
/// a default. It deliberately does <em>not</em> block the host — staff must still be able to boot and fix content —
/// and it cannot throw: a broken audit must not become a broken server.
/// </para>
/// <para>
/// It reads <see cref="ContentDbContext"/> (the content catalog lives in its own context), while the gameplay
/// contexts stay untouched. The authoring-time validators are the other half of the rule: they stop the dangling
/// reference from being written at all, using the same <see cref="ContentReferenceRules"/> from
/// <c>RealmEngine.Data.Validation.Content</c> — one rule set, three callers (this audit, the write path, CI).
/// </para>
/// </remarks>
public sealed class ContentAuditService : BackgroundService
{
    private readonly IDbContextFactory<ContentDbContext> _contentFactory;
    private readonly ILogger<ContentAuditService> _logger;

    /// <summary>Initializes a new instance of <see cref="ContentAuditService"/>.</summary>
    /// <param name="contentFactory">Factory for the content catalog context.</param>
    /// <param name="logger">Logger instance.</param>
    public ContentAuditService(
        IDbContextFactory<ContentDbContext> contentFactory,
        ILogger<ContentAuditService> logger)
    {
        _contentFactory = contentFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the rest of the host finish starting; startup may still be seeding content.
        await Task.Yield();

        try
        {
            var issues = await RunAsync(stoppingToken);
            Report(issues);
        }
        catch (OperationCanceledException)
        {
            // Host is shutting down.
        }
        catch (Exception ex)
        {
            // Never fatal: a broken audit is a broken diagnostic, not a broken server.
            _logger.LogError(ex, "Content reference audit failed to run; continuing without it");
        }
    }

    /// <summary>
    /// Runs the audit and returns every finding. Public so a test, an operator tool or a health endpoint can reuse it.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The findings, empty when content is consistent.</returns>
    public async Task<IReadOnlyList<ContentIssue>> RunAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _contentFactory.CreateDbContextAsync(cancellationToken);

        var archetypes = Slugs(await db.ActorArchetypes.AsNoTracking()
            .Select(actor => actor.Slug)
            .ToListAsync(cancellationToken));
        var items = Slugs(await db.Items.AsNoTracking()
            .Select(item => item.Slug)
            .ToListAsync(cancellationToken));

        // The entities are materialised and flattened here rather than projected with a nested collection.
        // A nested collection in a projection needs SQL APPLY, which SQLite cannot translate — so the audit
        // used to throw on any SQLite host and be silently skipped, because the startup run catches everything.
        var locationEntities = await db.ZoneLocations.AsNoTracking()
            .ToListAsync(cancellationToken);

        var locationSlugs = Slugs(locationEntities.Select(location => location.Slug));
        var pools = locationEntities
            .Select(location => new SpawnPoolReference(
                location.Slug,
                location.ActorPool.Select(entry => entry.ArchetypeSlug).ToList()))
            .ToList();

        var questEntities = await db.Quests.AsNoTracking()
            .ToListAsync(cancellationToken);

        var questObjectives = questEntities
            .SelectMany(quest => quest.Objectives.Items.Select(objective =>
                new QuestObjectiveReference(quest.Slug, objective.Type, objective.Target)))
            .ToList();

        var edges = await db.MapEdges.AsNoTracking()
            .Select(edge => new MapEdgeReference(edge.MapId, edge.FromNodeId, edge.ToNodeId))
            .ToListAsync(cancellationToken);

        // Nodes are projected as flat (map, node) pairs instead of being read through each map's `Nodes`
        // navigation. A navigation is only populated by Include, and this context does not lazy-load, so reading
        // `map.Nodes` off a plain Maps query yields an empty collection for every map — that made the edge rule
        // report both ends of every edge as missing on a perfectly seeded database. A flat pair projection also
        // needs no SQL APPLY, so it translates on SQLite (see the remark above).
        var nodePairs = await db.MapNodes.AsNoTracking()
            .Select(node => new { node.MapId, node.Id })
            .ToListAsync(cancellationToken);

        var nodeIdsByMap = nodePairs
            .GroupBy(node => node.MapId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlySet<string>)group
                    .Select(node => node.Id)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

        // Stock rows are authored by the seeder, which is insert-only, so a stored pair the authored shelves no
        // longer list is a data migration that never landed (docs/content-packs.md, "Changing what a town sells").
        // The authored side is projected the same way so the rule compares like with like.
        var storedStock = await db.ZoneStockItems.AsNoTracking()
            .Select(row => new ZoneStockReference(row.ZoneId, row.ItemSlug))
            .ToListAsync(cancellationToken);

        var authoredStock = ZoneStockSeeder.Authors()
            .Select(row => new ZoneStockReference(row.ZoneId, row.ItemSlug))
            .ToList();

        var issues = new List<ContentIssue>();
        issues.AddRange(ContentReferenceRules.AuditSpawnPools(pools, archetypes));
        issues.AddRange(ContentReferenceRules.AuditQuestObjectives(questObjectives, archetypes, items, locationSlugs));
        issues.AddRange(ContentReferenceRules.AuditMapEdges(edges, nodeIdsByMap));
        issues.AddRange(ContentReferenceRules.AuditZoneStock(storedStock, authoredStock));

        return issues;
    }

    /// <summary>Logs one consolidated report: a summary line plus one entry per finding at its own severity.</summary>
    /// <param name="issues">Findings from the audit.</param>
    private void Report(IReadOnlyList<ContentIssue> issues)
    {
        if (issues.Count == 0)
        {
            _logger.LogInformation("Content reference audit found no dangling references");
            return;
        }

        // Warning-only findings (an empty spawn pool, a repeatable dialogue grant, a stock row no shelf lists) are
        // legitimate content the audit wants reviewed, so they must not be logged as errors.
        if (issues.Any(issue => issue.Severity == ContentIssueSeverity.Error))
        {
            _logger.LogError(
                "Content reference audit found {Count} issue(s); fix the content before players hit them",
                issues.Count);
        }
        else
        {
            _logger.LogWarning(
                "Content reference audit found {Count} warning(s) worth a review; the catalog still serves",
                issues.Count);
        }

        // The seeders are insert-only by slug and the map graph seeder skips a database that already has maps, so a
        // database seeded by an older build keeps its old shape forever. A dangling map edge is the signature of
        // that: the remedy is a reseed, not a content edit, and saying so here saves the next operator the hunt.
        if (issues.Any(issue => issue.Rule == ContentRules.MissingMapNode))
        {
            _logger.LogWarning(
                "The map graph in this database is inconsistent (edges reference nodes its maps do not have). " +
                "The seeded catalog does not contain a dangling edge, so this database was seeded by an older build: " +
                "reseed it — `scripts/e2e-stack.sh start --fresh` in this repo, or `down -v` on the compose volume " +
                "and let the server seed on boot.");
        }

        foreach (var issue in issues)
        {
            if (issue.Severity == ContentIssueSeverity.Warning)
            {
                _logger.LogWarning("Content reference audit [{Rule}] {Message}", issue.Rule, issue.Message);
            }
            else
            {
                _logger.LogError("Content reference audit [{Rule}] {Message}", issue.Rule, issue.Message);
            }
        }
    }

    private static HashSet<string> Slugs(IEnumerable<string> slugs) =>
        new(slugs.Where(slug => !string.IsNullOrWhiteSpace(slug)), StringComparer.OrdinalIgnoreCase);
}
