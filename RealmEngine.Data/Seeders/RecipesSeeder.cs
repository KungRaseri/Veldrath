using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Data.Seeders;

/// <summary>Seeds baseline <see cref="Recipe"/> rows (with their ingredients) into <see cref="ContentDbContext"/>.</summary>
public static class RecipesSeeder
{
    /// <summary>Seeds all recipe rows (idempotent).</summary>
    public static async Task SeedAsync(ContentDbContext db)
    {
        if (await db.Recipes.AnyAsync())
            return;

        var now = DateTimeOffset.UtcNow;

        db.Recipes.AddRange(
            // Weapon recipes
            new Recipe
            {
                Slug             = "iron-sword-recipe",
                TypeKey          = "blacksmithing",
                DisplayName      = "Iron Sword",
                RarityWeight     = 80,
                IsActive         = true,
                Version          = 1,
                UpdatedAt        = now,
                OutputItemDomain = "items/general",
                OutputItemSlug   = "iron-sword",
                OutputQuantity   = 1,
                CraftingSkill    = "blacksmithing",
                CraftingLevel    = 1,
                Traits = new RecipeTraits
                {
                    Discoverable     = true,
                    RequiresStation  = true,
                    RequiresFire     = true,
                    IsAlchemy        = false,
                    IsBlacksmithing  = true,
                    IsLeatherworking = false,
                },
                Ingredients = new List<RecipeIngredient>
                {
                    new() { ItemDomain = "items/materials", ItemSlug = "iron", Quantity = 3, IsOptional = false },
                    new() { ItemDomain = "items/materials", ItemSlug = "pine", Quantity = 1, IsOptional = false },
                },
            },
            new Recipe
            {
                Slug             = "hunters-bow-recipe",
                TypeKey          = "woodworking",
                DisplayName      = "Hunter's Bow",
                RarityWeight     = 75,
                IsActive         = true,
                Version          = 1,
                UpdatedAt        = now,
                OutputItemDomain = "items/general",
                OutputItemSlug   = "hunters-bow",
                OutputQuantity   = 1,
                CraftingSkill    = "woodworking",
                CraftingLevel    = 1,
                Traits = new RecipeTraits
                {
                    Discoverable     = true,
                    RequiresStation  = false,
                    RequiresFire     = false,
                    IsAlchemy        = false,
                    IsBlacksmithing  = false,
                    IsLeatherworking = false,
                },
                Ingredients = new List<RecipeIngredient>
                {
                    new() { ItemDomain = "items/materials", ItemSlug = "oak",   Quantity = 2, IsOptional = false },
                    new() { ItemDomain = "items/materials", ItemSlug = "linen", Quantity = 1, IsOptional = false },
                    new() { ItemDomain = "items/materials", ItemSlug = "iron",  Quantity = 1, IsOptional = false },
                },
            },

            // Armor recipes
            new Recipe
            {
                Slug             = "leather-cap-recipe",
                TypeKey          = "leatherworking",
                DisplayName      = "Leather Cap",
                RarityWeight     = 85,
                IsActive         = true,
                Version          = 1,
                UpdatedAt        = now,
                OutputItemDomain = "items/general",
                OutputItemSlug   = "leather-cap",
                OutputQuantity   = 1,
                CraftingSkill    = "leatherworking",
                CraftingLevel    = 1,
                Traits = new RecipeTraits
                {
                    Discoverable     = true,
                    RequiresStation  = false,
                    RequiresFire     = false,
                    IsAlchemy        = false,
                    IsBlacksmithing  = false,
                    IsLeatherworking = true,
                },
                Ingredients = new List<RecipeIngredient>
                {
                    new() { ItemDomain = "items/materials", ItemSlug = "leather",       Quantity = 1, IsOptional = false },
                    new() { ItemDomain = "items/materials", ItemSlug = "thick-leather", Quantity = 1, IsOptional = true  },
                },
            },
            new Recipe
            {
                Slug             = "iron-chestplate-recipe",
                TypeKey          = "blacksmithing",
                DisplayName      = "Iron Chestplate",
                RarityWeight     = 70,
                IsActive         = true,
                Version          = 1,
                UpdatedAt        = now,
                OutputItemDomain = "items/general",
                OutputItemSlug   = "iron-chestplate",
                OutputQuantity   = 1,
                CraftingSkill    = "blacksmithing",
                CraftingLevel    = 2,
                Traits = new RecipeTraits
                {
                    Discoverable     = true,
                    RequiresStation  = true,
                    RequiresFire     = true,
                    IsAlchemy        = false,
                    IsBlacksmithing  = true,
                    IsLeatherworking = false,
                },
                Ingredients = new List<RecipeIngredient>
                {
                    new() { ItemDomain = "items/materials", ItemSlug = "iron",    Quantity = 5, IsOptional = false },
                    new() { ItemDomain = "items/materials", ItemSlug = "leather", Quantity = 1, IsOptional = true  },
                },
            },

            // Item recipes
            new Recipe
            {
                Slug             = "scroll-of-fireball-recipe",
                TypeKey          = "alchemy",
                DisplayName      = "Scroll of Fireball",
                RarityWeight     = 40,
                IsActive         = true,
                Version          = 1,
                UpdatedAt        = now,
                OutputItemDomain = "items/general",
                OutputItemSlug   = "scroll-of-fireball",
                OutputQuantity   = 1,
                CraftingSkill    = "arcanology",
                CraftingLevel    = 2,
                Traits = new RecipeTraits
                {
                    Discoverable     = true,
                    RequiresStation  = false,
                    RequiresFire     = false,
                    IsAlchemy        = true,
                    IsBlacksmithing  = false,
                    IsLeatherworking = false,
                },
                Ingredients = new List<RecipeIngredient>
                {
                    new() { ItemDomain = "items/materials", ItemSlug = "linen",           Quantity = 1, IsOptional = false },
                    new() { ItemDomain = "items/general",   ItemSlug = "essence-of-fire", Quantity = 1, IsOptional = false },
                    new() { ItemDomain = "items/general",   ItemSlug = "soul-crystal",    Quantity = 1, IsOptional = true  },
                },
            },
            new Recipe
            {
                Slug             = "iron-ingot-recipe",
                TypeKey          = "blacksmithing",
                DisplayName      = "Iron Ingot",
                RarityWeight     = 80,
                IsActive         = true,
                Version          = 1,
                UpdatedAt        = now,
                OutputItemDomain = "items/general",
                OutputItemSlug   = "iron-ingot",
                OutputQuantity   = 2,
                CraftingSkill    = "blacksmithing",
                CraftingLevel    = 1,
                Traits = new RecipeTraits
                {
                    Discoverable     = true,
                    RequiresStation  = true,
                    RequiresFire     = true,
                    IsAlchemy        = false,
                    IsBlacksmithing  = true,
                    IsLeatherworking = false,
                },
                Ingredients = new List<RecipeIngredient>
                {
                    new() { ItemDomain = "items/materials", ItemSlug = "iron", Quantity = 2, IsOptional = false },
                },
            });

        await db.SaveChangesAsync();
    }
}
