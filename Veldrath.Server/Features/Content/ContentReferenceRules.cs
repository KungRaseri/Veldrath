using Veldrath.Server.Features.Quest;

namespace Veldrath.Server.Features.Content;

/// <summary>
/// One finding from the content reference audit: which rule failed and what it was looking at.
/// </summary>
/// <param name="Rule">Stable rule identifier, e.g. <c>missing-spawn-archetype</c>.</param>
/// <param name="Message">Human-readable description naming both sides of the dangling reference.</param>
public sealed record ContentIssue(string Rule, string Message);

/// <summary>A zone location and the archetype slugs its spawn pool references.</summary>
/// <param name="LocationSlug">Zone-location slug.</param>
/// <param name="ArchetypeSlugs">Slugs the pool points at.</param>
public sealed record SpawnPoolReference(string LocationSlug, IReadOnlyList<string> ArchetypeSlugs);

/// <summary>One quest objective and the slug it targets.</summary>
/// <param name="QuestSlug">Quest slug.</param>
/// <param name="Type">Objective type (<c>kill</c>, <c>collect</c>, <c>reach</c>, …).</param>
/// <param name="Target">Slug the objective points at.</param>
public sealed record QuestObjectiveReference(string QuestSlug, string Type, string Target);

/// <summary>One map edge and the node ids it connects.</summary>
/// <param name="MapId">Map that owns the edge.</param>
/// <param name="FromNodeId">Origin node id.</param>
/// <param name="ToNodeId">Destination node id.</param>
public sealed record MapEdgeReference(string MapId, string FromNodeId, string ToNodeId);

/// <summary>
/// Pure reference rules behind the startup content audit.
/// </summary>
/// <remarks>
/// These are deliberately separate from the database query so they can be unit-tested with plain data, and so
/// Milestone B's authoring-time validators can reuse them. Every rule returns findings instead of throwing: the
/// audit must never be able to keep the host from booting.
/// </remarks>
public static class ContentReferenceRules
{
    /// <summary>Rule id for a spawn pool pointing at an actor archetype that does not exist.</summary>
    public const string MissingSpawnArchetype = "missing-spawn-archetype";

    /// <summary>Rule id for a quest objective pointing at content that does not exist.</summary>
    public const string MissingQuestTarget = "missing-quest-target";

    /// <summary>Rule id for a quest objective using a kind that no server call site implements.</summary>
    public const string UnsupportedQuestKind = "unsupported-quest-kind";

    /// <summary>Rule id for a map edge pointing at a node that is not on its map.</summary>
    public const string MissingMapNode = "missing-map-node";

    /// <summary>
    /// Checks every spawn pool against the set of actor archetype slugs that actually exist.
    /// </summary>
    /// <param name="pools">Spawn pools to check.</param>
    /// <param name="knownArchetypes">Archetype slugs present in content.</param>
    /// <returns>One issue per dangling reference.</returns>
    public static IReadOnlyList<ContentIssue> AuditSpawnPools(
        IEnumerable<SpawnPoolReference> pools,
        IReadOnlySet<string> knownArchetypes)
    {
        var issues = new List<ContentIssue>();

        foreach (var pool in pools)
        {
            foreach (var slug in pool.ArchetypeSlugs)
            {
                if (string.IsNullOrWhiteSpace(slug) || knownArchetypes.Contains(slug))
                {
                    continue;
                }

                issues.Add(new ContentIssue(
                    MissingSpawnArchetype,
                    $"Zone location '{pool.LocationSlug}' spawns unknown actor archetype '{slug}'."));
            }
        }

        return issues;
    }

    /// <summary>
    /// Checks quest objectives against content: <c>kill</c> → actor archetypes, <c>collect</c> → items,
    /// <c>reach</c>/<c>visit</c> → zone locations. Other objective types carry no content reference and are skipped.
    /// </summary>
    /// <param name="objectives">Objectives to check.</param>
    /// <param name="knownArchetypes">Archetype slugs present in content.</param>
    /// <param name="knownItems">Item slugs present in content.</param>
    /// <param name="knownLocations">Zone-location slugs present in content.</param>
    /// <returns>One issue per dangling reference.</returns>
    public static IReadOnlyList<ContentIssue> AuditQuestObjectives(
        IEnumerable<QuestObjectiveReference> objectives,
        IReadOnlySet<string> knownArchetypes,
        IReadOnlySet<string> knownItems,
        IReadOnlySet<string> knownLocations)
    {
        var issues = new List<ContentIssue>();

        foreach (var objective in objectives)
        {
            if (string.IsNullOrWhiteSpace(objective.Target))
            {
                continue;
            }

            var type = objective.Type?.ToLowerInvariant() ?? string.Empty;

            // A reserved kind would never progress, so it is reported rather than silently accepted.
            if (type == QuestObjectiveKinds.Deliver)
            {
                issues.Add(new ContentIssue(
                    UnsupportedQuestKind,
                    $"Quest '{objective.QuestSlug}' objective '{objective.Type}:{objective.Target}' uses a reserved objective kind: hand-over is not implemented, so it would never progress."));
                continue;
            }

            // A level objective names a number, not a slug, so it is validated as one.
            if (type == QuestObjectiveKinds.ReachLevel)
            {
                if (!int.TryParse(objective.Target, out var level) || level <= 0)
                {
                    issues.Add(new ContentIssue(
                        MissingQuestTarget,
                        $"Quest '{objective.QuestSlug}' objective '{objective.Type}:{objective.Target}' needs a positive level number."));
                }

                continue;
            }

            var (known, collection) = type switch
            {
                QuestObjectiveKinds.Kill => (knownArchetypes, "actor archetypes"),
                QuestObjectiveKinds.Collect => (knownItems, "items"),
                QuestObjectiveKinds.Craft => (knownItems, "items"),
                "reach" or QuestObjectiveKinds.Visit => (knownLocations, "zone locations"),
                // `talk:` has no NPC catalog to validate against yet; add it when one exists.
                _ => ((IReadOnlySet<string>?)null, string.Empty),
            };

            if (known is null || known.Contains(objective.Target))
            {
                continue;
            }

            issues.Add(new ContentIssue(
                MissingQuestTarget,
                $"Quest '{objective.QuestSlug}' objective '{objective.Type}:{objective.Target}' points at unknown {collection}."));
        }

        return issues;
    }

    /// <summary>
    /// Checks that both ends of every map edge belong to the edge's own map.
    /// </summary>
    /// <param name="edges">Edges to check.</param>
    /// <param name="nodeIdsByMap">Node ids present per map id.</param>
    /// <returns>One issue per edge end that is missing.</returns>
    public static IReadOnlyList<ContentIssue> AuditMapEdges(
        IEnumerable<MapEdgeReference> edges,
        IReadOnlyDictionary<string, IReadOnlySet<string>> nodeIdsByMap)
    {
        var issues = new List<ContentIssue>();

        foreach (var edge in edges)
        {
            nodeIdsByMap.TryGetValue(edge.MapId, out var nodes);

            foreach (var (label, nodeId) in new[] { ("from", edge.FromNodeId), ("to", edge.ToNodeId) })
            {
                if (nodes is not null && nodes.Contains(nodeId))
                {
                    continue;
                }

                issues.Add(new ContentIssue(
                    MissingMapNode,
                    $"Map '{edge.MapId}' edge {label} node '{nodeId}' does not exist on that map."));
            }
        }

        return issues;
    }
}
