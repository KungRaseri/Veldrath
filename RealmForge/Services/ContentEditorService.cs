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
    /// <summary>
    /// Creates a new entity in <paramref name="tableName"/>, registers it in ContentRegistry,
    /// and returns the new entity's ID with its serialised JSON. Returns null if the DB is unavailable.
    /// </summary>
    public async Task<(Guid EntityId, string Json)?> CreateEntityAsync(
        string tableName, string domain, string typeKey, string slug, string? displayName)
    {
        if (!KnownTables.Contains(tableName)) return null;
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<ContentDbContext>();
            if (db is null) return null;

            var entity = NewEntityInstance(tableName);
            entity.TypeKey = typeKey;
            entity.Slug = slug;
            entity.DisplayName = displayName;
            entity.IsActive = true;

            AddToDbSet(db, tableName, entity);
            db.ContentRegistry.Add(new ContentRegistry
            {
                EntityId = entity.Id,
                TableName = tableName,
                Domain = domain,
                TypeKey = typeKey,
                Slug = slug
            });

            await db.SaveChangesAsync();
            return (entity.Id, JsonConvert.SerializeObject(entity, SerializeSettings));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create entity in {TableName}", tableName);
            return null;
        }
    }

    /// <summary>
    /// Deletes the entity and removes its ContentRegistry entry. Returns false if not found or DB unavailable.
    /// </summary>
    public async Task<bool> DeleteEntityAsync(Guid entityId, string tableName)
    {
        if (!KnownTables.Contains(tableName)) return false;
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<ContentDbContext>();
            if (db is null) return false;

            var removed = tableName switch
            {
                "Abilities"          => await RemoveFromSet<Ability>(db, entityId),
                "Enemies"            => await RemoveFromSet<Enemy>(db, entityId),
                "Weapons"            => await RemoveFromSet<Weapon>(db, entityId),
                "Armors"             => await RemoveFromSet<Armor>(db, entityId),
                "Items"              => await RemoveFromSet<Item>(db, entityId),
                "Materials"          => await RemoveFromSet<Material>(db, entityId),
                "Enchantments"       => await RemoveFromSet<Enchantment>(db, entityId),
                "Skills"             => await RemoveFromSet<Skill>(db, entityId),
                "Spells"             => await RemoveFromSet<Spell>(db, entityId),
                "CharacterClasses"   => await RemoveFromSet<CharacterClass>(db, entityId),
                "Backgrounds"        => await RemoveFromSet<Background>(db, entityId),
                "Npcs"               => await RemoveFromSet<Npc>(db, entityId),
                "Quests"             => await RemoveFromSet<Quest>(db, entityId),
                "Recipes"            => await RemoveFromSet<Recipe>(db, entityId),
                "LootTables"         => await RemoveFromSet<LootTable>(db, entityId),
                "Organizations"      => await RemoveFromSet<Organization>(db, entityId),
                "MaterialProperties" => await RemoveFromSet<MaterialProperty>(db, entityId),
                "WorldLocations"     => await RemoveFromSet<WorldLocation>(db, entityId),
                "Dialogues"          => await RemoveFromSet<Dialogue>(db, entityId),
                _                    => false
            };

            if (removed)
            {
                var reg = await db.ContentRegistry
                    .Where(r => r.EntityId == entityId)
                    .FirstOrDefaultAsync();
                if (reg is not null) db.ContentRegistry.Remove(reg);
                await db.SaveChangesAsync();
            }

            return removed;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete entity {EntityId} from {TableName}", entityId, tableName);
            return false;
        }
    }

    private static ContentBase NewEntityInstance(string tableName) => tableName switch
    {
        "Abilities"          => new Ability(),
        "Enemies"            => new Enemy(),
        "Weapons"            => new Weapon(),
        "Armors"             => new Armor(),
        "Items"              => new Item(),
        "Materials"          => new Material(),
        "Enchantments"       => new Enchantment(),
        "Skills"             => new Skill(),
        "Spells"             => new Spell(),
        "CharacterClasses"   => new CharacterClass(),
        "Backgrounds"        => new Background(),
        "Npcs"               => new Npc(),
        "Quests"             => new Quest(),
        "Recipes"            => new Recipe(),
        "LootTables"         => new LootTable(),
        "Organizations"      => new Organization(),
        "MaterialProperties" => new MaterialProperty(),
        "WorldLocations"     => new WorldLocation(),
        "Dialogues"          => new Dialogue(),
        _                    => throw new ArgumentException($"Unknown table: {tableName}")
    };

    private static void AddToDbSet(ContentDbContext db, string tableName, ContentBase entity)
    {
        switch (tableName)
        {
            case "Abilities":          db.Abilities.Add((Ability)entity);                    break;
            case "Enemies":            db.Enemies.Add((Enemy)entity);                        break;
            case "Weapons":            db.Weapons.Add((Weapon)entity);                       break;
            case "Armors":             db.Armors.Add((Armor)entity);                         break;
            case "Items":              db.Items.Add((Item)entity);                            break;
            case "Materials":          db.Materials.Add((Material)entity);                   break;
            case "Enchantments":       db.Enchantments.Add((Enchantment)entity);             break;
            case "Skills":             db.Skills.Add((Skill)entity);                         break;
            case "Spells":             db.Spells.Add((Spell)entity);                         break;
            case "CharacterClasses":   db.CharacterClasses.Add((CharacterClass)entity);      break;
            case "Backgrounds":        db.Backgrounds.Add((Background)entity);               break;
            case "Npcs":               db.Npcs.Add((Npc)entity);                             break;
            case "Quests":             db.Quests.Add((Quest)entity);                         break;
            case "Recipes":            db.Recipes.Add((Recipe)entity);                       break;
            case "LootTables":         db.LootTables.Add((LootTable)entity);                 break;
            case "Organizations":      db.Organizations.Add((Organization)entity);           break;
            case "MaterialProperties": db.MaterialProperties.Add((MaterialProperty)entity);  break;
            case "WorldLocations":     db.WorldLocations.Add((WorldLocation)entity);         break;
            case "Dialogues":          db.Dialogues.Add((Dialogue)entity);                   break;
        }
    }

    private static async Task<bool> RemoveFromSet<T>(ContentDbContext db, Guid entityId)
        where T : ContentBase
    {
        var entity = await db.Set<T>().FindAsync(entityId);
        if (entity is null) return false;
        db.Set<T>().Remove(entity);
        return true;
    }

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
