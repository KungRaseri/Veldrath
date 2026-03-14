using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmForge.Services;

/// <summary>
/// Loads and saves individual content entities as JSON via ContentDbContext.
/// All entity navigation collections are excluded from serialisation so the
/// editor only shows the entity's own scalar + JSONB fields.
/// </summary>
public class ContentEditorService(IServiceScopeFactory scopeFactory, ILogger<ContentEditorService> logger)
{
    private static readonly HashSet<string> KnownTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "Abilities", "Enemies", "Weapons", "Armors", "Items", "Materials", "Enchantments",
        "Skills", "Spells", "CharacterClasses", "Backgrounds", "Npcs", "Quests", "Recipes",
        "LootTables", "Organizations", "MaterialProperties", "WorldLocations", "Dialogues"
    };

    private static readonly JsonSerializerSettings SerializeSettings = new()
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        ContractResolver = new NavCollectionIgnoringResolver()
    };

    /// <summary>
    /// Loads the entity with <paramref name="entityId"/> from <paramref name="tableName"/>
    /// and returns it serialised as indented JSON, or null if not found / DB unavailable.
    /// </summary>
    public async Task<string?> GetEntityJsonAsync(Guid entityId, string tableName)
    {
        if (!KnownTables.Contains(tableName)) return null;
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<ContentDbContext>();
            if (db is null) return null;

            ContentBase? entity = tableName switch
            {
                "Abilities"         => await db.Abilities.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "Enemies"           => await db.Enemies.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "Weapons"           => await db.Weapons.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "Armors"            => await db.Armors.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "Items"             => await db.Items.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "Materials"         => await db.Materials.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "Enchantments"      => await db.Enchantments.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "Skills"            => await db.Skills.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "Spells"            => await db.Spells.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "CharacterClasses"  => await db.CharacterClasses.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "Backgrounds"       => await db.Backgrounds.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "Npcs"              => await db.Npcs.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "Quests"            => await db.Quests.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "Recipes"           => await db.Recipes.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "LootTables"        => await db.LootTables.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "Organizations"     => await db.Organizations.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "MaterialProperties"=> await db.MaterialProperties.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "WorldLocations"    => await db.WorldLocations.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "Dialogues"         => await db.Dialogues.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                _                   => null
            };

            return entity is null ? null : JsonConvert.SerializeObject(entity, SerializeSettings);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load entity {EntityId} from {TableName}", entityId, tableName);
            return null;
        }
    }

    /// <summary>
    /// Deserialises <paramref name="json"/> back into the correct entity type and
    /// persists it to the database.  Navigation collections are nulled before the
    /// update so EF Core does not interpret empty arrays as "delete all relations".
    /// </summary>
    public async Task<bool> SaveEntityJsonAsync(Guid entityId, string tableName, string json)
    {
        if (!KnownTables.Contains(tableName)) return false;
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<ContentDbContext>();
            if (db is null) return false;

            return tableName switch
            {
                "Abilities"         => await UpdateEntity<Ability>(db, entityId, json),
                "Enemies"           => await UpdateEntity<Enemy>(db, entityId, json),
                "Weapons"           => await UpdateEntity<Weapon>(db, entityId, json),
                "Armors"            => await UpdateEntity<Armor>(db, entityId, json),
                "Items"             => await UpdateEntity<Item>(db, entityId, json),
                "Materials"         => await UpdateEntity<Material>(db, entityId, json),
                "Enchantments"      => await UpdateEntity<Enchantment>(db, entityId, json),
                "Skills"            => await UpdateEntity<Skill>(db, entityId, json),
                "Spells"            => await UpdateEntity<Spell>(db, entityId, json),
                "CharacterClasses"  => await UpdateEntity<CharacterClass>(db, entityId, json),
                "Backgrounds"       => await UpdateEntity<Background>(db, entityId, json),
                "Npcs"              => await UpdateEntity<Npc>(db, entityId, json),
                "Quests"            => await UpdateEntity<Quest>(db, entityId, json),
                "Recipes"           => await UpdateEntity<Recipe>(db, entityId, json),
                "LootTables"        => await UpdateEntity<LootTable>(db, entityId, json),
                "Organizations"     => await UpdateEntity<Organization>(db, entityId, json),
                "MaterialProperties"=> await UpdateEntity<MaterialProperty>(db, entityId, json),
                "WorldLocations"    => await UpdateEntity<WorldLocation>(db, entityId, json),
                "Dialogues"         => await UpdateEntity<Dialogue>(db, entityId, json),
                _                   => false
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save entity {EntityId} to {TableName}", entityId, tableName);
            return false;
        }
    }

    private static async Task<bool> UpdateEntity<T>(ContentDbContext db, Guid entityId, string json)
        where T : ContentBase
    {
        var entity = JsonConvert.DeserializeObject<T>(json);
        if (entity is null || entity.Id != entityId) return false;

        // Null out ICollection<> nav properties so EF Core does not reconcile junction rows
        foreach (var prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.PropertyType is { IsGenericType: true } t
                                 && t.GetGenericTypeDefinition() == typeof(ICollection<>)))
        {
            prop.SetValue(entity, null);
        }

        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.Version++;

        db.Update(entity);
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Excludes ICollection navigation properties from JSON serialisation so the
    /// editor only presents the entity's own data columns.
    /// </summary>
    private sealed class NavCollectionIgnoringResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var prop = base.CreateProperty(member, memberSerialization);
            if (prop.PropertyType is { IsGenericType: true } t
                && t.GetGenericTypeDefinition() == typeof(ICollection<>))
                prop.Ignored = true;
            return prop;
        }
    }
}
