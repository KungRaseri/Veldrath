using Veldrath.Server.Data.Entities;

namespace Veldrath.Server.Data.Seeders;

/// <summary>Fixture stand-in for the server's world-text seeder: the checker reads Slug and DisplayName.</summary>
public static class WorldSeeder
{
    private static IEnumerable<WorldLocation> Rows() => new[]
    {
        new WorldLocation
        {
            Slug = "crestfall-market",
            DisplayName = "Crestfall Market",
        },
    };
}
