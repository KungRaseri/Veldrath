using Microsoft.EntityFrameworkCore;
using Veldrath.Server.Data;
using Veldrath.Server.Features.Admin;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Pins the provider path of the zone-population aggregate behind <c>GET /api/admin/status</c>.
/// </summary>
/// <remarks>
/// The snapshot used to order its <c>GroupBy</c> by a member of a <see cref="ZonePopulationDto"/> constructor
/// projection, which EF Core cannot translate on any provider: it raised <c>InvalidOperationException</c> at
/// translation time, so the endpoint answered 500 on every request against a real database. Compiling the query to
/// SQL with <see cref="EntityFrameworkQueryableExtensions.ToQueryString"/> needs no live server, which makes the
/// Npgsql (production) path assertable in the ordinary test run instead of only in the docker e2e stack.
/// </remarks>
public class AdminStatusQueryTests
{
    /// <summary>
    /// Production connection settings, never opened: only the provider's SQL generator runs. A wrong password is
    /// deliberate — no test here may quietly depend on a reachable database.
    /// </summary>
    private const string NpgsqlConnectionString =
        "Host=127.0.0.1;Port=1;Database=zone-population-translation;Username=translation-only;Password=translation-only";

    private static ApplicationDbContext Context(string provider)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>();
        switch (provider)
        {
            case "npgsql":
                builder.UseNpgsql(NpgsqlConnectionString);
                break;
            case "sqlite":
                builder.UseSqlite("Data Source=:memory:");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown provider.");
        }

        return new ApplicationDbContext(builder.Options);
    }

    [Theory]
    [InlineData("npgsql")]
    [InlineData("sqlite")]
    public void ZonePopulationQuery_CountsAndOrdersInsideTheDatabase(string provider)
    {
        using var db = Context(provider);

        var sql = AdminEndpoints.ZonePopulationQuery(db).ToQueryString().ToLowerInvariant();

        // The aggregate, the zone name join and the ordering all have to survive translation; if any of them is
        // pushed back to the client, EF Core instead throws and this call never returns.
        sql.Should().Contain("group by");
        sql.Should().Contain("count(");
        sql.Should().Contain("left join");
        sql.Should().Contain("order by");
        sql.Should().Contain("is not null");
    }

    [Fact]
    public void ZonePopulationQuery_NamesTheZoneThroughItsForeignKeyOnNpgsql()
    {
        using var db = Context("npgsql");

        var sql = AdminEndpoints.ZonePopulationQuery(db).ToQueryString();

        sql.Should().Contain("GROUP BY p.\"ZoneId\", z.\"Name\"");
        sql.Should().Contain("LEFT JOIN \"Zones\" AS z ON p.\"ZoneId\" = z.\"Id\"");
        sql.Should().Contain("ORDER BY count(*)::int DESC");
    }
}
