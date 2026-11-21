using RealmEngine.Data.Validation.Content;
using Veldrath.Server.Features.Content;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Tests for the D5 content reference rules — the failsafe-not-fallback checks the startup audit reports.
/// </summary>
public class ContentReferenceAuditTests
{
    private static IReadOnlySet<string> Set(params string[] values) =>
        new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);

    // ── Spawn pools ────────────────────────────────────────────────────────────

    [Fact]
    public void AuditSpawnPools_Reports_Nothing_When_Every_Archetype_Exists()
    {
        var pools = new[] { new SpawnPoolReference("woods", ["wolf", "boar"]) };

        var issues = ContentReferenceRules.AuditSpawnPools(pools, Set("wolf", "boar", "goblin"));

        issues.Should().BeEmpty();
    }

    [Fact]
    public void AuditSpawnPools_Names_The_Location_And_The_Missing_Archetype()
    {
        var pools = new[] { new SpawnPoolReference("woods", ["wolf", "spectre"]) };

        var issues = ContentReferenceRules.AuditSpawnPools(pools, Set("wolf"));

        issues.Should().ContainSingle();
        issues[0].Rule.Should().Be(ContentReferenceRules.MissingSpawnArchetype);
        issues[0].Message.Should().Contain("woods").And.Contain("spectre");
    }

    // ── Quest objectives ───────────────────────────────────────────────────────

    [Fact]
    public void AuditQuestObjectives_Checks_Each_Type_Against_Its_Own_Collection()
    {
        var objectives = new[]
        {
            new QuestObjectiveReference("clear-the-woods", "kill", "wolf"),
            new QuestObjectiveReference("clear-the-woods", "collect", "wolf-pelt"),
            new QuestObjectiveReference("clear-the-woods", "reach", "woods"),
        };

        var issues = ContentReferenceRules.AuditQuestObjectives(
            objectives,
            Set("wolf"),
            Set("wolf-pelt"),
            Set("woods"));

        issues.Should().BeEmpty("every target exists in the collection its objective type points at");
    }

    [Fact]
    public void AuditQuestObjectives_Reports_A_Dangling_Target()
    {
        var objectives = new[] { new QuestObjectiveReference("clear-the-woods", "collect", "wolf-pelt") };

        var issues = ContentReferenceRules.AuditQuestObjectives(objectives, Set("wolf"), Set(), Set("woods"));

        issues.Should().ContainSingle();
        issues[0].Rule.Should().Be(ContentReferenceRules.MissingQuestTarget);
        issues[0].Message.Should().Contain("clear-the-woods").And.Contain("wolf-pelt").And.Contain("items");
    }

    [Fact]
    public void AuditQuestObjectives_Reports_An_Unknown_Objective_Kind()
    {
        // `interact` looked harmless because it carried no content reference, but no call site advances it, so the
        // objective could never progress. Unknown kinds are now errors rather than silently skipped — which is how the
        // shipped `the-lost-shipment` quest was caught.
        var objectives = new[] { new QuestObjectiveReference("rest-well", "interact", "not-a-slug") };

        var issues = ContentReferenceRules.AuditQuestObjectives(objectives, Set(), Set(), Set());

        issues.Should().ContainSingle();
        issues[0].Rule.Should().Be(ContentReferenceRules.UnsupportedQuestKind);
        issues[0].Message.Should().Contain("interact").And.Contain("rest-well");
    }

    [Fact]
    public void AuditQuestObjectives_Still_Accepts_Talk_Which_Has_No_Catalog_To_Check()
    {
        var objectives = new[] { new QuestObjectiveReference("greet-the-smith", "talk", "village-blacksmith") };

        var issues = ContentReferenceRules.AuditQuestObjectives(objectives, Set(), Set(), Set());

        issues.Should().BeEmpty();
    }

    // ── Map edges ──────────────────────────────────────────────────────────────

    [Fact]
    public void AuditMapEdges_Reports_An_Edge_Whose_Ends_Are_Not_On_The_Map()
    {
        var edges = new[]
        {
            new MapEdgeReference("zone:woods", "woods:square", "woods:gone"),
            new MapEdgeReference("zone:woods", "woods:missing", "woods:square"),
        };

        var issues = ContentReferenceRules.AuditMapEdges(
            edges,
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["zone:woods"] = Set("woods:square", "woods:inn"),
            });

        issues.Should().HaveCount(2);
        issues.Should().OnlyContain(issue => issue.Rule == ContentReferenceRules.MissingMapNode);
        issues.Should().Contain(issue => issue.Message.Contains("woods:gone"));
        issues.Should().Contain(issue => issue.Message.Contains("woods:missing"));
    }

    [Fact]
    public void AuditMapEdges_Reports_Edges_Of_An_Unknown_Map()
    {
        var issues = ContentReferenceRules.AuditMapEdges(
            [new MapEdgeReference("zone:nowhere", "a", "b")],
            new Dictionary<string, IReadOnlySet<string>>());

        issues.Should().HaveCount(2, "neither end can exist on a map that has no nodes at all");
    }

    // ── Zone stock ─────────────────────────────────────────────────────────────

    [Fact]
    public void AuditZoneStock_Reports_Nothing_When_Stored_Matches_Authored()
    {
        var rows = new[]
        {
            new ZoneStockReference("crestfall", "health-potion"),
            new ZoneStockReference("crestfall", "iron-sword"),
        };

        ContentReferenceRules.AuditZoneStock(rows, rows).Should().BeEmpty();
    }

    [Fact]
    public void AuditZoneStock_Reports_A_Stored_Pair_No_Authored_Shelf_Lists()
    {
        // A shelf edit lands as a data migration (docs/content-packs.md); a row left behind is that migration
        // never having been written.
        var authored = new[] { new ZoneStockReference("crestfall", "health-potion") };
        var stored = new[]
        {
            new ZoneStockReference("crestfall", "health-potion"),
            new ZoneStockReference("crestfall", "hunters-bow"),
        };

        var issues = ContentReferenceRules.AuditZoneStock(stored, authored);

        issues.Should().ContainSingle();
        issues[0].Rule.Should().Be(ContentRules.UnclaimedZoneStock);
        issues[0].Severity.Should().Be(ContentIssueSeverity.Warning, "the shop still serves, so this must not fail a write");
        issues[0].Identifier.Should().Be("crestfall/hunters-bow");
        issues[0].Message.Should().Contain("crestfall").And.Contain("hunters-bow").And.Contain("data migration");
    }

    [Fact]
    public void AuditZoneStock_Ignores_Case_When_Matching_The_Pair()
    {
        var stored = new[] { new ZoneStockReference("Crestfall", "Health-Potion") };
        var authored = new[] { new ZoneStockReference("crestfall", "health-potion") };

        ContentReferenceRules.AuditZoneStock(stored, authored).Should().BeEmpty();
    }

    [Fact]
    public void AuditZoneStock_Does_Not_Report_An_Authored_Row_The_Database_Lacks()
    {
        // The seeder inserts what is missing on the next start, so an absent row is not drift — reporting it would
        // make every dev database noisy.
        var stored = new[] { new ZoneStockReference("crestfall", "health-potion") };
        var authored = new[]
        {
            new ZoneStockReference("crestfall", "health-potion"),
            new ZoneStockReference("tolvaren", "void-shard"),
        };

        ContentReferenceRules.AuditZoneStock(stored, authored).Should().BeEmpty();
    }

    // ── Empty content ──────────────────────────────────────────────────────────

    [Fact]
    public void Every_Rule_Tolerates_Completely_Empty_Content()
    {
        var empty = new Dictionary<string, IReadOnlySet<string>>();

        ContentReferenceRules.AuditSpawnPools([], Set()).Should().BeEmpty();
        ContentReferenceRules.AuditQuestObjectives([], Set(), Set(), Set()).Should().BeEmpty();
        ContentReferenceRules.AuditMapEdges([], empty).Should().BeEmpty();
        ContentReferenceRules.AuditZoneStock([], []).Should().BeEmpty();
    }
}
