using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmForge.ViewModels;

namespace RealmForge.Services;

/// <summary>
/// Loads and saves game content entities via ContentDbContext.
/// All navigation collection properties are excluded from EF Core tracking on save.
/// </summary>
public class ContentEditorService(IServiceScopeFactory scopeFactory, ILogger<ContentEditorService> logger)
{
    private static readonly HashSet<string> KnownTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "Abilities", "Species", "ActorClasses", "ActorArchetypes", "ActorInstances",
        "Items", "Materials", "Enchantments",
        "Skills", "Spells", "Backgrounds", "Quests", "Recipes",
        "LootTables", "Organizations", "MaterialProperties", "WorldLocations", "Dialogues"
    };

    /// <summary>
    /// Loads the entity from the database and returns it as a typed ContentBase.
    /// Returns null if not found or if the database is unavailable.
    /// </summary>
    public async Task<ContentBase?> LoadEntityAsync(Guid entityId, string tableName)
    {
        if (!KnownTables.Contains(tableName)) return null;
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<ContentDbContext>();
            if (db is null) return null;

            return tableName switch
            {
                "Abilities"          => await db.Abilities.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "Species"            => await db.Species.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "ActorClasses"       => await db.ActorClasses.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "ActorArchetypes"    => await db.ActorArchetypes.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "ActorInstances"     => await db.ActorInstances.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "Items"              => await db.Items.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "Materials"          => await db.Materials.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "Enchantments"       => await db.Enchantments.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "Skills"             => await db.Skills.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "Spells"             => await db.Spells.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "Backgrounds"        => await db.Backgrounds.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "Quests"             => await db.Quests.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "Recipes"            => await db.Recipes.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "LootTables"         => await db.LootTables.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "Organizations"      => await db.Organizations.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "MaterialProperties" => await db.MaterialProperties.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "WorldLocations"     => await db.WorldLocations.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                "Dialogues"          => await db.Dialogues.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId),
                _                    => null
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load entity {EntityId} from {TableName}", entityId, tableName);
            return null;
        }
    }

    /// <summary>
    /// Persists all scalar and JSONB fields of the entity back to the database.
    /// Navigation collections are detached before the update so EF Core does not touch junction rows.
    /// </summary>
    public async Task<bool> SaveEntityAsync(ContentBase entity, string tableName)
    {
        if (!KnownTables.Contains(tableName)) return false;
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<ContentDbContext>();
            if (db is null) return false;

            NullNavigationCollections(entity);
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            entity.Version++;

            db.Update(entity);
            await db.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save entity {EntityId} to {TableName}", entity.Id, tableName);
            return false;
        }
    }

    /// <summary>
    /// Creates a new entity, registers it in ContentRegistry, and returns it.
    /// Returns null if the database is unavailable.
    /// </summary>
    public async Task<ContentBase?> CreateEntityAsync(
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
            return entity;
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
                "Species"            => await RemoveFromSet<Species>(db, entityId),
                "ActorClasses"       => await RemoveFromSet<ActorClass>(db, entityId),
                "ActorArchetypes"    => await RemoveFromSet<ActorArchetype>(db, entityId),
                "ActorInstances"     => await RemoveFromSet<ActorInstance>(db, entityId),
                "Items"              => await RemoveFromSet<Item>(db, entityId),
                "Materials"          => await RemoveFromSet<Material>(db, entityId),
                "Enchantments"       => await RemoveFromSet<Enchantment>(db, entityId),
                "Skills"             => await RemoveFromSet<Skill>(db, entityId),
                "Spells"             => await RemoveFromSet<Spell>(db, entityId),
                "Backgrounds"        => await RemoveFromSet<Background>(db, entityId),
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

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static void NullNavigationCollections(ContentBase entity)
    {
        foreach (var prop in entity.GetType()
                     .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.PropertyType is { IsGenericType: true } t
                                 && t.GetGenericTypeDefinition() == typeof(ICollection<>)))
        {
            prop.SetValue(entity, null);
        }
    }

    private static ContentBase NewEntityInstance(string tableName) => tableName switch
    {
        "Abilities"          => new Ability(),
        "Species"            => new Species(),
        "ActorClasses"       => new ActorClass(),
        "ActorArchetypes"    => new ActorArchetype(),
        "ActorInstances"     => new ActorInstance(),
        "Items"              => new Item(),
        "Materials"          => new Material(),
        "Enchantments"       => new Enchantment(),
        "Skills"             => new Skill(),
        "Spells"             => new Spell(),
        "Backgrounds"        => new Background(),
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
            case "Abilities":          db.Abilities.Add((Ability)entity);                       break;
            case "Species":            db.Species.Add((Species)entity);                         break;
            case "ActorClasses":       db.ActorClasses.Add((ActorClass)entity);                 break;
            case "ActorArchetypes":    db.ActorArchetypes.Add((ActorArchetype)entity);           break;
            case "ActorInstances":     db.ActorInstances.Add((ActorInstance)entity);             break;
            case "Items":              db.Items.Add((Item)entity);                              break;
            case "Materials":          db.Materials.Add((Material)entity);                      break;
            case "Enchantments":       db.Enchantments.Add((Enchantment)entity);                break;
            case "Skills":             db.Skills.Add((Skill)entity);                            break;
            case "Spells":             db.Spells.Add((Spell)entity);                            break;
            case "Backgrounds":        db.Backgrounds.Add((Background)entity);                  break;
            case "Quests":             db.Quests.Add((Quest)entity);                            break;
            case "Recipes":            db.Recipes.Add((Recipe)entity);                          break;
            case "LootTables":         db.LootTables.Add((LootTable)entity);                    break;
            case "Organizations":      db.Organizations.Add((Organization)entity);              break;
            case "MaterialProperties": db.MaterialProperties.Add((MaterialProperty)entity);     break;
            case "WorldLocations":     db.WorldLocations.Add((WorldLocation)entity);            break;
            case "Dialogues":          db.Dialogues.Add((Dialogue)entity);                      break;
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

    /// <summary>
    /// Loads a summary list (ContentBase scalars only) for the given table + TypeKey,
    /// sorted by Slug. Commands on each row are wired by EntityListViewModel after load.
    /// </summary>
    public async Task<IReadOnlyList<EntityListRowViewModel>> GetEntityListAsync(
        string tableName, string typeKey)
    {
        if (!KnownTables.Contains(tableName)) return [];
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<ContentDbContext>();
            if (db is null) return [];

            return tableName switch
            {
                "Abilities"          => await SelectRows(db.Abilities, typeKey),
                "Species"            => await SelectRows(db.Species, typeKey),
                "ActorClasses"       => await SelectRows(db.ActorClasses, typeKey),
                "ActorArchetypes"    => await SelectRows(db.ActorArchetypes, typeKey),
                "ActorInstances"     => await SelectRows(db.ActorInstances, typeKey),
                "Items"              => await SelectRows(db.Items, typeKey),
                "Materials"          => await SelectRows(db.Materials, typeKey),
                "Enchantments"       => await SelectRows(db.Enchantments, typeKey),
                "Skills"             => await SelectRows(db.Skills, typeKey),
                "Spells"             => await SelectRows(db.Spells, typeKey),
                "Backgrounds"        => await SelectRows(db.Backgrounds, typeKey),
                "Quests"             => await SelectRows(db.Quests, typeKey),
                "Recipes"            => await SelectRows(db.Recipes, typeKey),
                "LootTables"         => await SelectRows(db.LootTables, typeKey),
                "Organizations"      => await SelectRows(db.Organizations, typeKey),
                "MaterialProperties" => await SelectRows(db.MaterialProperties, typeKey),
                "WorldLocations"     => await SelectRows(db.WorldLocations, typeKey),
                "Dialogues"          => await SelectRows(db.Dialogues, typeKey),
                _                    => []
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load entity list for {TableName}/{TypeKey}", tableName, typeKey);
            return [];
        }
    }

    private static Task<List<EntityListRowViewModel>> SelectRows<T>(
        DbSet<T> dbSet, string typeKey) where T : ContentBase
    {
        return dbSet.AsNoTracking()
            .Where(e => e.TypeKey == typeKey)
            .OrderBy(e => e.Slug)
            .Select(e => new EntityListRowViewModel
            {
                EntityId     = e.Id,
                Slug         = e.Slug,
                DisplayName  = e.DisplayName,
                RarityWeight = e.RarityWeight,
                IsActive     = e.IsActive,
                UpdatedAt    = e.UpdatedAt
            })
            .ToListAsync();
    }

    // ── Junction load methods ────────────────────────────────────────────────

    public async Task<IReadOnlyList<LootTableEntry>> LoadLootTableEntriesAsync(Guid tableId)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<ContentDbContext>();
            if (db is null) return [];
            return await db.LootTableEntries.AsNoTracking()
                .Where(e => e.LootTableId == tableId).ToListAsync();
        }
        catch (Exception ex) { logger.LogError(ex, "LoadLootTableEntries {Id}", tableId); return []; }
    }

    public async Task<IReadOnlyList<RecipeIngredient>> LoadRecipeIngredientsAsync(Guid recipeId)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<ContentDbContext>();
            if (db is null) return [];
            return await db.RecipeIngredients.AsNoTracking()
                .Where(e => e.RecipeId == recipeId).ToListAsync();
        }
        catch (Exception ex) { logger.LogError(ex, "LoadRecipeIngredients {Id}", recipeId); return []; }
    }

    public async Task<IReadOnlyList<SpeciesAbilityPool>> LoadSpeciesAbilityPoolAsync(Guid speciesId)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<ContentDbContext>();
            if (db is null) return [];
            return await db.SpeciesAbilityPools.AsNoTracking()
                .Where(e => e.SpeciesId == speciesId).ToListAsync();
        }
        catch (Exception ex) { logger.LogError(ex, "LoadSpeciesAbilityPool {Id}", speciesId); return []; }
    }

    public async Task<IReadOnlyList<InstanceAbilityPool>> LoadInstanceAbilityPoolAsync(Guid instanceId)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<ContentDbContext>();
            if (db is null) return [];
            return await db.InstanceAbilityPools.AsNoTracking()
                .Where(e => e.InstanceId == instanceId).ToListAsync();
        }
        catch (Exception ex) { logger.LogError(ex, "LoadInstanceAbilityPool {Id}", instanceId); return []; }
    }

    public async Task<IReadOnlyList<ArchetypeAbilityPool>> LoadArchetypeAbilityPoolAsync(Guid archetypeId)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<ContentDbContext>();
            if (db is null) return [];
            return await db.ArchetypeAbilityPools.AsNoTracking()
                .Where(e => e.ArchetypeId == archetypeId).ToListAsync();
        }
        catch (Exception ex) { logger.LogError(ex, "LoadArchetypeAbilityPool {Id}", archetypeId); return []; }
    }

    public async Task<IReadOnlyList<ClassAbilityUnlock>> LoadClassAbilityUnlocksAsync(Guid classId)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<ContentDbContext>();
            if (db is null) return [];
            return await db.ClassAbilityUnlocks.AsNoTracking()
                .Where(e => e.ClassId == classId).ToListAsync();
        }
        catch (Exception ex) { logger.LogError(ex, "LoadClassAbilityUnlocks {Id}", classId); return []; }
    }

    /// <summary>Returns a slug map for a batch of ability IDs — used to display slugs instead of raw GUIDs.</summary>
    public async Task<IReadOnlyDictionary<Guid, string>> GetAbilitySlugsAsync(IEnumerable<Guid> abilityIds)
    {
        try
        {
            var ids = abilityIds.ToHashSet();
            if (ids.Count == 0) return new Dictionary<Guid, string>();
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<ContentDbContext>();
            if (db is null) return new Dictionary<Guid, string>();
            var pairs = await db.Abilities.AsNoTracking()
                .Where(a => ids.Contains(a.Id))
                .Select(a => new { a.Id, a.Slug })
                .ToListAsync();
            return pairs.ToDictionary(x => x.Id, x => x.Slug);
        }
        catch (Exception ex) { logger.LogError(ex, "GetAbilitySlugs"); return new Dictionary<Guid, string>(); }
    }

    // ── Junction save methods (replace-all per owner) ────────────────────────

    public async Task<bool> SaveLootTableEntriesAsync(Guid tableId, IReadOnlyList<LootTableEntry> entries)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<ContentDbContext>();
            if (db is null) return false;
            var existing = await db.LootTableEntries.Where(e => e.LootTableId == tableId).ToListAsync();
            db.LootTableEntries.RemoveRange(existing);
            db.LootTableEntries.AddRange(entries);
            await db.SaveChangesAsync();
            return true;
        }
        catch (Exception ex) { logger.LogError(ex, "SaveLootTableEntries {Id}", tableId); return false; }
    }

    public async Task<bool> SaveRecipeIngredientsAsync(Guid recipeId, IReadOnlyList<RecipeIngredient> rows)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<ContentDbContext>();
            if (db is null) return false;
            var existing = await db.RecipeIngredients.Where(e => e.RecipeId == recipeId).ToListAsync();
            db.RecipeIngredients.RemoveRange(existing);
            db.RecipeIngredients.AddRange(rows);
            await db.SaveChangesAsync();
            return true;
        }
        catch (Exception ex) { logger.LogError(ex, "SaveRecipeIngredients {Id}", recipeId); return false; }
    }

    public async Task<bool> SaveSpeciesAbilityPoolAsync(Guid speciesId, IReadOnlyList<string> abilitySlugs)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<ContentDbContext>();
            if (db is null) return false;
            var existing = await db.SpeciesAbilityPools.Where(e => e.SpeciesId == speciesId).ToListAsync();
            db.SpeciesAbilityPools.RemoveRange(existing);
            foreach (var slug in abilitySlugs)
            {
                var id = await FindAbilityIdAsync(db, slug);
                if (id is not null)
                    db.SpeciesAbilityPools.Add(new SpeciesAbilityPool { SpeciesId = speciesId, AbilityId = id.Value });
            }
            await db.SaveChangesAsync();
            return true;
        }
        catch (Exception ex) { logger.LogError(ex, "SaveSpeciesAbilityPool {Id}", speciesId); return false; }
    }

    public async Task<bool> SaveInstanceAbilityPoolAsync(Guid instanceId, IReadOnlyList<string> abilitySlugs)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<ContentDbContext>();
            if (db is null) return false;
            var existing = await db.InstanceAbilityPools.Where(e => e.InstanceId == instanceId).ToListAsync();
            db.InstanceAbilityPools.RemoveRange(existing);
            foreach (var slug in abilitySlugs)
            {
                var id = await FindAbilityIdAsync(db, slug);
                if (id is not null)
                    db.InstanceAbilityPools.Add(new InstanceAbilityPool { InstanceId = instanceId, AbilityId = id.Value });
            }
            await db.SaveChangesAsync();
            return true;
        }
        catch (Exception ex) { logger.LogError(ex, "SaveInstanceAbilityPool {Id}", instanceId); return false; }
    }

    public async Task<bool> SaveArchetypeAbilityPoolAsync(
        Guid archetypeId, IReadOnlyList<(string Slug, float UseChance)> rows)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<ContentDbContext>();
            if (db is null) return false;
            var existing = await db.ArchetypeAbilityPools.Where(e => e.ArchetypeId == archetypeId).ToListAsync();
            db.ArchetypeAbilityPools.RemoveRange(existing);
            foreach (var (slug, useChance) in rows)
            {
                var id = await FindAbilityIdAsync(db, slug);
                if (id is not null)
                    db.ArchetypeAbilityPools.Add(new ArchetypeAbilityPool
                        { ArchetypeId = archetypeId, AbilityId = id.Value, UseChance = useChance });
            }
            await db.SaveChangesAsync();
            return true;
        }
        catch (Exception ex) { logger.LogError(ex, "SaveArchetypeAbilityPool {Id}", archetypeId); return false; }
    }

    public async Task<bool> SaveClassAbilityUnlocksAsync(
        Guid classId, IReadOnlyList<(string Slug, int LevelRequired, int Rank)> rows)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<ContentDbContext>();
            if (db is null) return false;
            var existing = await db.ClassAbilityUnlocks.Where(e => e.ClassId == classId).ToListAsync();
            db.ClassAbilityUnlocks.RemoveRange(existing);
            foreach (var (slug, level, rank) in rows)
            {
                var id = await FindAbilityIdAsync(db, slug);
                if (id is not null)
                    db.ClassAbilityUnlocks.Add(new ClassAbilityUnlock
                        { ClassId = classId, AbilityId = id.Value, LevelRequired = level, Rank = rank });
            }
            await db.SaveChangesAsync();
            return true;
        }
        catch (Exception ex) { logger.LogError(ex, "SaveClassAbilityUnlocks {Id}", classId); return false; }
    }

    private static async Task<Guid?> FindAbilityIdAsync(ContentDbContext db, string slug) =>
        (await db.Abilities.AsNoTracking().FirstOrDefaultAsync(a => a.Slug == slug))?.Id;
}


