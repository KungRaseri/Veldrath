using RealmEngine.Data.Entities;

namespace RealmEngine.Data.Seeders;

/// <summary>Fixture stand-in for the real seeder: the checker only reads Slug and DisplayName.</summary>
public static class ArchetypeSeeder
{
    private static IEnumerable<ActorArchetype> Rows() => new[]
    {
        new ActorArchetype
        {
            Slug = "giant-rat",
            DisplayName = "Giant Rat",
        },
        new ActorArchetype
        {
            Slug = "village-blacksmith",
            DisplayName = "Village Blacksmith",
        },
    };
}
