using Microsoft.Extensions.Logging;

namespace RealmForge.Services;

/// <summary>
/// Seeds a handful of representative sample entities across key domains for local development.
/// Only runs when ContentRegistry is empty — safe to call unconditionally on startup.
/// </summary>
public class DevDataSeederService(ContentEditorService editorService, ILogger<DevDataSeederService> logger)
{
    private static readonly (string Table, string Domain, string TypeKey, string Slug, string Name)[] SeedData =
    [
        // Abilities — active offensive
        ("Abilities", "abilities", "active/offensive", "basic-attack",    "Basic Attack"),
        ("Abilities", "abilities", "active/offensive", "power-strike",    "Power Strike"),
        ("Abilities", "abilities", "active/offensive", "fireball",        "Fireball"),

        // Abilities — passive
        ("Abilities", "abilities", "passive/defensive", "toughness",      "Toughness"),
        ("Abilities", "abilities", "passive/offensive", "bloodlust",      "Bloodlust"),

        // Enemies — humanoids
        ("Enemies", "enemies", "humanoids",   "goblin-warrior",  "Goblin Warrior"),
        ("Enemies", "enemies", "humanoids",   "bandit-archer",   "Bandit Archer"),
        ("Enemies", "enemies", "humanoids",   "orc-shaman",      "Orc Shaman"),

        // Enemies — undead
        ("Enemies", "enemies", "undead",      "skeleton-soldier",  "Skeleton Soldier"),
        ("Enemies", "enemies", "undead",      "zombie-brute",      "Zombie Brute"),

        // Enemies — beasts
        ("Enemies", "enemies", "beasts/wolves",  "timber-wolf",  "Timber Wolf"),
        ("Enemies", "enemies", "beasts/wolves",  "dire-wolf",    "Dire Wolf"),

        // Weapons — swords
        ("Weapons", "items/weapons", "swords",  "iron-longsword",    "Iron Longsword"),
        ("Weapons", "items/weapons", "swords",  "steel-shortsword",  "Steel Shortsword"),

        // Weapons — bows
        ("Weapons", "items/weapons", "bows",  "hunting-bow",    "Hunting Bow"),
        ("Weapons", "items/weapons", "bows",  "elven-longbow",  "Elven Longbow"),

        // Character classes
        ("CharacterClasses", "classes", "warriors",  "fighter",  "Fighter"),
        ("CharacterClasses", "classes", "warriors",  "barbarian","Barbarian"),
        ("CharacterClasses", "classes", "rogues",     "rogue",    "Rogue"),
        ("CharacterClasses", "classes", "casters",    "wizard",   "Wizard"),
    ];

    public async Task<bool> SeedIfEmptyAsync()
    {
        try
        {
            var created = 0;
            foreach (var (table, domain, typeKey, slug, name) in SeedData)
            {
                var result = await editorService.CreateEntityAsync(table, domain, typeKey, slug, name);
                if (result is not null) created++;
            }

            if (created > 0)
                logger.LogInformation("DevDataSeederService seeded {Count} sample entities", created);

            return created > 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DevDataSeederService failed to seed sample data");
            return false;
        }
    }
}
