using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Validation.Content;

namespace RealmEngine.Data.Authoring.ContentPacks;

/// <summary>
/// The <c>validate</c> and <c>import</c> commands behind <c>Veldrath.ContentPack</c>.
/// </summary>
/// <remarks>
/// The command surface lives here rather than in the executable so the exit codes are covered by tests: a CI guard is
/// only useful if "any Error means a non-zero exit" is asserted, and asserting it through a spawned process would be
/// slower and flakier than calling <see cref="Run"/>.
/// <para>
/// Exit codes: <c>0</c> no error-severity issue, <c>1</c> the pack has errors, <c>2</c> the command line or the
/// environment is wrong (missing directory, missing connection string, unmigrated database).
/// </para>
/// </remarks>
public static class ContentPackConsole
{
    /// <summary>Environment variable read when <c>--database</c> is not supplied.</summary>
    public const string DatabaseEnvironmentVariable = "CONTENT_DATABASE";

    /// <summary>Exit code for a pack that validates clean.</summary>
    public const int Success = 0;

    /// <summary>Exit code for a pack with at least one error-severity issue.</summary>
    public const int InvalidContent = 1;

    /// <summary>Exit code for a command line or environment problem.</summary>
    public const int UsageError = 2;

    private const string Usage = """
        usage: veldrath-content <command> <pack-directory> [options]

        commands:
          validate <dir>   Run every content validator over a pack directory and print the issues.
                           Exits 1 when any issue is an Error, 0 otherwise. Reads (never writes) the catalog;
                           without --database it stages the directory into its in-process seed, family by family,
                           so a reference to a row the same directory authors still resolves.
          import   <dir>   Validate, then create/update every entity by slug. Writes ContentDbContext only.

        options:
          --database <connection-string>  Postgres content catalog. Defaults to $CONTENT_DATABASE.
                                          validate falls back to the seeded catalogue when it is absent.
          --no-baseline                   validate: do not seed the baseline catalogue; only the pack's own
                                          slugs are known, so every external reference is reported.
          --dry-run                       import: validate and report what would change, then roll back.
          --help                          Show this text.

        examples:
          dotnet run --project Veldrath.ContentPack -- validate content/
          dotnet run --project Veldrath.ContentPack -- import content/ --database "Host=localhost;Database=veldrath;Username=veldrath"
        """;

    /// <summary>
    /// Runs one command.
    /// </summary>
    /// <param name="args">Command-line arguments, without the executable name.</param>
    /// <param name="output">Where normal output goes.</param>
    /// <param name="error">Where usage and environment failures go.</param>
    /// <param name="connectionString">
    /// Connection string to use when the arguments do not carry one. Defaults to
    /// <see cref="DatabaseEnvironmentVariable"/>.
    /// </param>
    /// <returns>The process exit code.</returns>
    public static int Run(
        string[] args,
        TextWriter output,
        TextWriter error,
        string? connectionString = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
        {
            output.WriteLine(Usage);
            return Success;
        }

        var command = args[0];
        var options = Parse(args.Skip(1).ToArray());

        if (options.Help)
        {
            output.WriteLine(Usage);
            return Success;
        }

        if (options.Positional.Count != 1)
        {
            error.WriteLine("error: exactly one pack directory is required.");
            error.WriteLine(Usage);
            return UsageError;
        }

        var directory = options.Positional[0];
        var database = options.Database ?? connectionString ?? Environment.GetEnvironmentVariable(DatabaseEnvironmentVariable);

        if (!Directory.Exists(directory))
        {
            error.WriteLine($"error: the pack directory '{directory}' does not exist.");
            return UsageError;
        }

        var read = ContentPackReader.Read(directory);

        return command switch
        {
            "validate" => RunValidateAsync(read, database, options.NoBaseline, output, error).GetAwaiter().GetResult(),
            "import" => RunImportAsync(read, database, options.DryRun, output, error).GetAwaiter().GetResult(),
            _ => UnknownCommand(command, error),
        };
    }

    private static int UnknownCommand(string command, TextWriter error)
    {
        error.WriteLine($"error: unknown command '{command}'.");
        error.WriteLine(Usage);
        return UsageError;
    }

    private static async Task<int> RunValidateAsync(
        ContentPackReadResult read,
        string? database,
        bool noBaseline,
        TextWriter output,
        TextWriter error)
    {
        if (database is not null)
        {
            await using var context = CreatePostgresContext(database, error);
            if (context is null)
            {
                return UsageError;
            }

            return await ValidateAsync(read, context, "the database", output, error);
        }

        if (noBaseline)
        {
            await using var empty = new ContentDbContext(
                new DbContextOptionsBuilder<ContentDbContext>()
                    .UseInMemoryDatabase($"content-pack-empty-{Guid.NewGuid()}")
                    .Options);

            return await ValidateAsync(read, empty, "the pack only (no baseline)", output, error);
        }

        await using var baseline = await SeededContentCatalog.CreateAsync();
        return await ValidateAgainstSeededCatalogueAsync(read, baseline.Context, output, error);
    }

    /// <summary>
    /// Validates a directory against the in-process seeded catalogue, family by family, staging each family into that
    /// throwaway catalogue before the next one is checked.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A directory can declare both sides of a reference: <c>content/archetypes/</c> names the loot table it drops and
    /// <c>content/loottables/</c> authors that table. A GUID foreign key written as a slug has to resolve against
    /// stored content, so a single read-only pass over the whole directory reports every one of those references as
    /// unresolved — and the entity that carries it as unreadable.
    /// </para>
    /// <para>
    /// Landing that directory is two imports in dependency order (<c>docs/content-packs.md</c>, *Two runs, in
    /// dependency order*), so validate replays the order instead of refusing it: each family is validated and staged,
    /// and a later family resolves the rows the earlier ones created. The only catalogue that is written is the
    /// in-process baseline, which is discarded; <c>--database</c> keeps the single read-only pass, because that
    /// catalogue is the live one.
    /// </para>
    /// </remarks>
    private static async Task<int> ValidateAgainstSeededCatalogueAsync(
        ContentPackReadResult read,
        ContentDbContext context,
        TextWriter output,
        TextWriter error)
    {
        try
        {
            var issues = new List<ContentIssue>(read.Issues);
            var importer = new ContentPackImporter(context);

            foreach (var family in read.Files
                         .GroupBy(file => file.CanonicalFamily, StringComparer.OrdinalIgnoreCase)
                         .OrderBy(group => StagingRank(group.Key)))
            {
                // A family that has errors writes nothing, so the families after it see the truth: their own
                // references to the missing rows are reported rather than papered over.
                var staged = new ContentPackReadResult([.. family], []);
                var result = await importer.ImportAsync(staged);
                issues.AddRange(result.Validation.Issues);
            }

            PrintIssues(issues, output);
            PrintTally(read, issues, "the seeded catalogue, staged family by family", output);

            return issues.Any(issue => issue.Severity == ContentIssueSeverity.Error) ? InvalidContent : Success;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            error.WriteLine($"error: validation could not run: {ex.Message}");
            return UsageError;
        }
    }

    /// <summary>
    /// The order families are staged in: a family appears after every family a GUID foreign key of it can point at.
    /// </summary>
    /// <remarks>
    /// Only the six slug-valued GUID foreign keys documented in <c>docs/content-packs.md</c> create an ordering
    /// constraint — a loot table before the archetype that names one, an archetype before the instance that overrides
    /// one. Everything else resolves through the reference catalog, which already knows every slug the pack declares,
    /// so the remaining families sit after those two in a fixed order that keeps the output readable.
    /// </remarks>
    private static readonly string[] _stagingOrder =
    [
        "materialproperty", "material", "item", "species", "class", "background", "skill", "enchantment",
        "ability", "loottable", "archetype", "instance", "zonelocation", "organization", "recipe", "quest",
        "dialogue", "language",
    ];

    /// <summary>Returns the staging position of a canonical family; unknown families stage last, in file order.</summary>
    private static int StagingRank(string canonicalFamily)
    {
        var index = Array.FindIndex(
            _stagingOrder,
            known => string.Equals(known, canonicalFamily, StringComparison.OrdinalIgnoreCase));

        return index < 0 ? _stagingOrder.Length : index;
    }

    private static async Task<int> ValidateAsync(
        ContentPackReadResult read,
        ContentDbContext context,
        string source,
        TextWriter output,
        TextWriter error)
    {
        try
        {
            var result = await new ContentPackValidator(context).ValidateAsync(read);
            PrintIssues(result.Issues, output);
            PrintTally(read, result.Issues, source, output);

            return result.IsValid ? Success : InvalidContent;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            error.WriteLine($"error: validation could not run: {ex.Message}");
            return UsageError;
        }
    }

    private static async Task<int> RunImportAsync(
        ContentPackReadResult read,
        string? database,
        bool dryRun,
        TextWriter output,
        TextWriter error)
    {
        if (string.IsNullOrWhiteSpace(database))
        {
            error.WriteLine(
                $"error: import needs a content database. Pass --database \"Host=...\" or set ${DatabaseEnvironmentVariable}.");
            return UsageError;
        }

        await using var context = CreatePostgresContext(database, error);
        if (context is null)
        {
            return UsageError;
        }

        try
        {
            var result = await new ContentPackImporter(context).ImportAsync(read, dryRun);
            PrintIssues(result.Validation.Issues, output);

            if (!result.Succeeded)
            {
                output.WriteLine();
                output.WriteLine("Nothing was written: the pack has errors.");
                return InvalidContent;
            }

            output.WriteLine();
            foreach (var family in result.Families)
            {
                output.WriteLine(
                    $"{family.Family,-18} created {family.Created,4}  updated {family.Updated,4}  unchanged {family.Unchanged,4}");
            }

            output.WriteLine(
                $"{string.Empty,-18} created {result.Created,4}  updated {result.Updated,4}  unchanged {result.Unchanged,4}");

            if (dryRun)
            {
                output.WriteLine("Dry run: nothing was written.");
            }
            else if (result.Created + result.Updated > 0)
            {
                output.WriteLine("Import complete.");
            }
            else
            {
                output.WriteLine("Import complete: the catalog already matched the pack.");
            }

            return Success;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            error.WriteLine($"error: import failed: {ex.Message}");
            return UsageError;
        }
    }

    private static ContentDbContext? CreatePostgresContext(string connectionString, TextWriter error)
    {
        try
        {
            var builder = new DbContextOptionsBuilder<ContentDbContext>().UseNpgsql(connectionString);
            return new ContentDbContext(builder.Options);
        }
        catch (ArgumentException ex)
        {
            error.WriteLine($"error: '{connectionString}' is not a usable connection string: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Prints the issue list the way an author reads it: severity, rule, slug, message and field.
    /// </summary>
    /// <param name="issues">Issues to print.</param>
    /// <param name="output">Where to print them.</param>
    public static void PrintIssues(IEnumerable<ContentIssue> issues, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(issues);
        ArgumentNullException.ThrowIfNull(output);

        var ordered = issues
            .OrderBy(issue => issue.Severity == ContentIssueSeverity.Error ? 0 : 1)
            .ThenBy(issue => issue.Identifier ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(issue => issue.Rule, StringComparer.Ordinal)
            .ToList();

        foreach (var issue in ordered)
        {
            var subject = issue.Identifier is null ? string.Empty : $" [{issue.Identifier}]";
            var field = issue.Field is null ? string.Empty : $" (field: {issue.Field})";
            output.WriteLine($"{issue.Severity,-7} {issue.Rule}{subject}: {issue.Message}{field}");
        }
    }

    private static void PrintTally(
        ContentPackReadResult read,
        IReadOnlyList<ContentIssue> issues,
        string source,
        TextWriter output)
    {
        var entities = read.Entries.Count();
        var errors = issues.Count(issue => issue.Severity == ContentIssueSeverity.Error);
        var warnings = issues.Count(issue => issue.Severity == ContentIssueSeverity.Warning);

        output.WriteLine();
        output.WriteLine(
            $"{entities} entities in {read.Files.Count} pack file(s), checked against {source}: {errors} error(s), {warnings} warning(s).");
    }

    private sealed record ParsedOptions(
        IReadOnlyList<string> Positional,
        string? Database,
        bool DryRun,
        bool NoBaseline,
        bool Help);

    private static ParsedOptions Parse(string[] args)
    {
        var positional = new List<string>();
        string? database = null;
        var dryRun = false;
        var noBaseline = false;
        var help = false;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--database":
                    if (index + 1 < args.Length)
                    {
                        database = args[++index];
                    }

                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--no-baseline":
                    noBaseline = true;
                    break;
                case "--help" or "-h":
                    help = true;
                    break;
                default:
                    if (!args[index].StartsWith("--", StringComparison.Ordinal))
                    {
                        positional.Add(args[index]);
                    }

                    break;
            }
        }

        return new ParsedOptions(positional, database, dryRun, noBaseline, help);
    }
}
