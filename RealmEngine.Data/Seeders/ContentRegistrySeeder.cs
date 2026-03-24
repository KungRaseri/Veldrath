using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Data.Seeders;

/// <summary>Registers all seeded content entities into <see cref="ContentRegistry"/> (idempotent).</summary>
public static class ContentRegistrySeeder
{
    /// <summary>Registers all known content entity IDs that are not yet present in the registry.</summary>
    public static async Task SeedAsync(ContentDbContext db)
    {
        // Collect all entity IDs already registered so we can skip them.
        var registered = await db.ContentRegistry.Select(r => r.EntityId).ToHashSetAsync();

        var entries = new List<ContentRegistry>();

        void Register(Guid id, string tableName, string domain, string typeKey, string slug)
        {
            if (!registered.Contains(id))
                entries.Add(new ContentRegistry { EntityId = id, TableName = tableName, Domain = domain, TypeKey = typeKey, Slug = slug });
        }

        foreach (var e in await db.ActorClasses.AsNoTracking().ToListAsync())
            Register(e.Id, "ActorClasses", "actors/classes", e.TypeKey, e.Slug);

        foreach (var e in await db.Powers.AsNoTracking().ToListAsync())
            Register(e.Id, "Powers", "powers", e.TypeKey, e.Slug);

        foreach (var e in await db.Skills.AsNoTracking().ToListAsync())
            Register(e.Id, "Skills", "actors/skills", e.TypeKey, e.Slug);

        foreach (var e in await db.Backgrounds.AsNoTracking().ToListAsync())
            Register(e.Id, "Backgrounds", "actors/backgrounds", e.TypeKey, e.Slug);

        foreach (var e in await db.Species.AsNoTracking().ToListAsync())
            Register(e.Id, "Species", "actors/species", e.TypeKey, e.Slug);

        foreach (var e in await db.Materials.AsNoTracking().ToListAsync())
            Register(e.Id, "Materials", "items/materials", e.TypeKey, e.Slug);

        foreach (var e in await db.MaterialProperties.AsNoTracking().ToListAsync())
            Register(e.Id, "MaterialProperties", "items/material-properties", e.TypeKey, e.Slug);

        foreach (var e in await db.Items.AsNoTracking().ToListAsync())
            Register(e.Id, "Items", "items/general", e.TypeKey, e.Slug);

        foreach (var e in await db.Enchantments.AsNoTracking().ToListAsync())
            Register(e.Id, "Enchantments", "items/enchantments", e.TypeKey, e.Slug);

        foreach (var e in await db.ActorArchetypes.AsNoTracking().ToListAsync())
            Register(e.Id, "ActorArchetypes", "actors/archetypes", e.TypeKey, e.Slug);

        foreach (var e in await db.Recipes.AsNoTracking().ToListAsync())
            Register(e.Id, "Recipes", "crafting/recipes", e.TypeKey, e.Slug);

        foreach (var e in await db.Organizations.AsNoTracking().ToListAsync())
            Register(e.Id, "Organizations", "world/organizations", e.TypeKey, e.Slug);

        foreach (var e in await db.Dialogues.AsNoTracking().ToListAsync())
            Register(e.Id, "Dialogues", "world/dialogue", e.TypeKey, e.Slug);

        foreach (var e in await db.ZoneLocations.AsNoTracking().ToListAsync())
            Register(e.Id, "ZoneLocations", "world/locations", e.TypeKey, e.Slug);

        foreach (var e in await db.ActorInstances.AsNoTracking().ToListAsync())
            Register(e.Id, "ActorInstances", "actors/instances", e.TypeKey, e.Slug);

        foreach (var e in await db.Quests.AsNoTracking().ToListAsync())
            Register(e.Id, "Quests", "quests", e.TypeKey, e.Slug);

        foreach (var e in await db.LootTables.AsNoTracking().ToListAsync())
            Register(e.Id, "LootTables", "loot/tables", e.TypeKey, e.Slug);

        if (entries.Count > 0)
        {
            db.ContentRegistry.AddRange(entries);
            await db.SaveChangesAsync();
        }
    }
}
