using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Persistence;

namespace Veldrath.Server.Features.Content;

/// <summary>
/// Runs the content reference audit once at startup and logs every dangling reference at <c>Error</c>.
/// </summary>
/// <remarks>
/// <para>
/// This is the runtime half of the failsafe rule: missing content is reported loudly and never silently replaced by
/// a default. It deliberately does <em>not</em> block the host — staff must still be able to boot and fix content —
/// and it cannot throw: a broken audit must not become a broken server.
/// </para>
/// <para>
/// It reads <see cref="ContentDbContext"/> (the content catalog lives in its own context), while the gameplay
/// contexts stay untouched. Milestone B's authoring-time validators are the other half of the rule: they stop the
/// dangling reference from being written at all, using the same <see cref="ContentReferenceRules"/>.
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

        var locations = await db.ZoneLocations.AsNoTracking()
            .Select(location => new
            {
                location.Slug,
                ArchetypeSlugs = location.ActorPool.Select(entry => entry.ArchetypeSlug).ToList(),
            })
            .ToListAsync(cancellationToken);

        var locationSlugs = Slugs(locations.Select(location => location.Slug));
        var pools = locations
            .Select(location => new SpawnPoolReference(location.Slug, location.ArchetypeSlugs))
            .ToList();

        var questObjectives = (await db.Quests.AsNoTracking()
                .Select(quest => new
                {
                    quest.Slug,
                    Objectives = quest.Objectives.Items
                        .Select(objective => new { objective.Type, objective.Target })
                        .ToList(),
                })
                .ToListAsync(cancellationToken))
            .SelectMany(quest => quest.Objectives.Select(objective =>
                new QuestObjectiveReference(quest.Slug, objective.Type, objective.Target)))
            .ToList();

        var edges = await db.MapEdges.AsNoTracking()
            .Select(edge => new MapEdgeReference(edge.MapId, edge.FromNodeId, edge.ToNodeId))
            .ToListAsync(cancellationToken);

        var nodeIdsByMap = (await db.Maps.AsNoTracking()
                .Select(map => new
                {
                    map.Id,
                    NodeIds = map.Nodes.Select(node => node.Id).ToList(),
                })
                .ToListAsync(cancellationToken))
            .ToDictionary(
                map => map.Id,
                map => (IReadOnlySet<string>)map.NodeIds.ToHashSet(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

        var issues = new List<ContentIssue>();
        issues.AddRange(ContentReferenceRules.AuditSpawnPools(pools, archetypes));
        issues.AddRange(ContentReferenceRules.AuditQuestObjectives(questObjectives, archetypes, items, locationSlugs));
        issues.AddRange(ContentReferenceRules.AuditMapEdges(edges, nodeIdsByMap));

        return issues;
    }

    /// <summary>Logs one consolidated report: a summary line plus one <c>Error</c> per finding.</summary>
    /// <param name="issues">Findings from the audit.</param>
    private void Report(IReadOnlyList<ContentIssue> issues)
    {
        if (issues.Count == 0)
        {
            _logger.LogInformation("Content reference audit found no dangling references");
            return;
        }

        _logger.LogError(
            "Content reference audit found {Count} dangling reference(s); fix the content before players hit them",
            issues.Count);

        foreach (var issue in issues)
        {
            _logger.LogError("Content reference audit [{Rule}] {Message}", issue.Rule, issue.Message);
        }
    }

    private static HashSet<string> Slugs(IEnumerable<string> slugs) =>
        new(slugs.Where(slug => !string.IsNullOrWhiteSpace(slug)), StringComparer.OrdinalIgnoreCase);
}
