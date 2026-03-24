using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Data.Seeders;

/// <summary>
/// Seeds the <see cref="TraitDefinition"/> vocabulary table — the canonical registry of every trait
/// key used across entity Traits columns. Adding a new trait requires: one row here + a nullable
/// property on the relevant owned class. No EF migration is required for the entity table itself.
/// </summary>
public static class TraitDefinitionsSeeder
{
    /// <summary>Seeds all trait definition rows (idempotent).</summary>
    public static async Task SeedAsync(ContentDbContext db)
    {
        if (await db.TraitDefinitions.AnyAsync())
            return;

        db.TraitDefinitions.AddRange(

            // ── Enemy / NPC archetype traits ──────────────────────────────────
            new TraitDefinition { Key = "hostile",          ValueType = "bool", AppliesTo = "enemies,npcs",                 Description = "True if this actor attacks the player on sight." },
            new TraitDefinition { Key = "aggressive",       ValueType = "bool", AppliesTo = "enemies",                      Description = "True if this actor charges the player without warning." },
            new TraitDefinition { Key = "packHunter",       ValueType = "bool", AppliesTo = "enemies",                      Description = "True if this actor calls nearby allies when aggroed." },
            new TraitDefinition { Key = "shopkeeper",       ValueType = "bool", AppliesTo = "npcs",                         Description = "True if this NPC operates a shop inventory." },
            new TraitDefinition { Key = "questGiver",       ValueType = "bool", AppliesTo = "npcs,organizations",           Description = "True if this actor can offer quests to the player." },
            new TraitDefinition { Key = "hasDialogue",      ValueType = "bool", AppliesTo = "npcs",                         Description = "True if this NPC has scripted dialogue lines." },
            new TraitDefinition { Key = "immortal",         ValueType = "bool", AppliesTo = "npcs",                         Description = "True if this NPC cannot be killed." },
            new TraitDefinition { Key = "wanderer",         ValueType = "bool", AppliesTo = "enemies,npcs",                 Description = "True if this actor patrols or moves unpredictably." },
            new TraitDefinition { Key = "boss",             ValueType = "bool", AppliesTo = "enemies,loottables",           Description = "True if this is a boss-tier encounter or loot table." },
            new TraitDefinition { Key = "elite",            ValueType = "bool", AppliesTo = "enemies,loottables",           Description = "True if this is an elite-tier encounter or loot table." },
            new TraitDefinition { Key = "ranged",           ValueType = "bool", AppliesTo = "enemies,npcs",                 Description = "True if this actor prefers ranged attacks." },
            new TraitDefinition { Key = "caster",           ValueType = "bool", AppliesTo = "enemies,npcs",                 Description = "True if this actor uses spells or magical abilities." },
            new TraitDefinition { Key = "fireImmune",       ValueType = "bool", AppliesTo = "enemies",                      Description = "True if this actor takes no damage from fire." },
            new TraitDefinition { Key = "coldImmune",       ValueType = "bool", AppliesTo = "enemies",                      Description = "True if this actor takes no damage from cold." },
            new TraitDefinition { Key = "poisonImmune",     ValueType = "bool", AppliesTo = "enemies",                      Description = "True if this actor takes no damage from poison." },

            // ── Power traits ─────────────────────────────────────────────────
            new TraitDefinition { Key = "requiresTarget",  ValueType = "bool", AppliesTo = "powers",       Description = "True if the power requires a selected target to activate." },
            new TraitDefinition { Key = "isAoe",           ValueType = "bool", AppliesTo = "powers",       Description = "True if the power affects an area rather than a single target." },
            new TraitDefinition { Key = "hasCooldown",     ValueType = "bool", AppliesTo = "powers",       Description = "True if the power has a cooldown period between uses." },
            new TraitDefinition { Key = "isInstant",       ValueType = "bool", AppliesTo = "powers",       Description = "True if the power activates with no cast time." },
            new TraitDefinition { Key = "isChanneled",     ValueType = "bool", AppliesTo = "powers",       Description = "True if the power requires continuous channel time to maintain." },
            new TraitDefinition { Key = "canCrit",         ValueType = "bool", AppliesTo = "powers",       Description = "True if the power is eligible for a critical hit." },
            new TraitDefinition { Key = "isPassive",       ValueType = "bool", AppliesTo = "powers",       Description = "True if the power is a passive bonus with no activation." },

            // ── Item traits ───────────────────────────────────────────────────
            new TraitDefinition { Key = "stackable",       ValueType = "bool", AppliesTo = "items",        Description = "True if multiple copies of this item occupy a single inventory slot." },
            new TraitDefinition { Key = "questItem",       ValueType = "bool", AppliesTo = "items",        Description = "True if the item is required by an active quest and cannot be discarded." },
            new TraitDefinition { Key = "unique",          ValueType = "bool", AppliesTo = "items",        Description = "True if only one copy can exist in inventory at a time." },
            new TraitDefinition { Key = "soulbound",       ValueType = "bool", AppliesTo = "items",        Description = "True if the item binds to the character on pickup." },
            new TraitDefinition { Key = "consumable",      ValueType = "bool", AppliesTo = "items",        Description = "True if the item is destroyed on use." },
            new TraitDefinition { Key = "magical",         ValueType = "bool", AppliesTo = "items",        Description = "True if the item has magical properties or effects." },

            // ── Enchantment traits ────────────────────────────────────────────
            new TraitDefinition { Key = "exclusive",           ValueType = "bool", AppliesTo = "enchantments", Description = "True if this enchantment cannot stack with others of the same type." },
            new TraitDefinition { Key = "requiresMagicItem",   ValueType = "bool", AppliesTo = "enchantments", Description = "True if this enchantment can only be applied to already-magical items." },
            new TraitDefinition { Key = "cursed",              ValueType = "bool", AppliesTo = "enchantments", Description = "True if this enchantment has a negative drawback." },
            new TraitDefinition { Key = "permanent",           ValueType = "bool", AppliesTo = "enchantments", Description = "True if the enchantment cannot be removed once applied." },

            // ── Loot table traits ─────────────────────────────────────────────
            new TraitDefinition { Key = "rare",           ValueType = "bool", AppliesTo = "loottables",   Description = "True if this loot table produces rare-quality drops." },
            new TraitDefinition { Key = "common",         ValueType = "bool", AppliesTo = "loottables",   Description = "True if this loot table produces common-quality drops." },
            new TraitDefinition { Key = "isChest",        ValueType = "bool", AppliesTo = "loottables",   Description = "True if this table is used for container/chest loot." },
            new TraitDefinition { Key = "isHarvesting",   ValueType = "bool", AppliesTo = "loottables",   Description = "True if this table is used for resource node harvesting." },

            // ── Recipe traits ──────────────────────────────────────────────────
            new TraitDefinition { Key = "discoverable",      ValueType = "bool", AppliesTo = "recipes",      Description = "True if the recipe can be learned by experimentation." },
            new TraitDefinition { Key = "requiresStation",   ValueType = "bool", AppliesTo = "recipes",      Description = "True if the recipe requires an in-world crafting station." },
            new TraitDefinition { Key = "requiresFire",      ValueType = "bool", AppliesTo = "recipes",      Description = "True if the recipe requires an open flame (forge, campfire)." },
            new TraitDefinition { Key = "isAlchemy",         ValueType = "bool", AppliesTo = "recipes",      Description = "True if the recipe is an alchemy formula." },
            new TraitDefinition { Key = "isBlacksmithing",   ValueType = "bool", AppliesTo = "recipes",      Description = "True if the recipe is a blacksmithing schematic." },
            new TraitDefinition { Key = "isLeatherworking",  ValueType = "bool", AppliesTo = "recipes",      Description = "True if the recipe is a leatherworking pattern." },

            // ── Organization traits ────────────────────────────────────────────
            new TraitDefinition { Key = "joinable",          ValueType = "bool", AppliesTo = "organizations", Description = "True if players can join this organization." },
            new TraitDefinition { Key = "hasShop",           ValueType = "bool", AppliesTo = "organizations", Description = "True if the organization runs a player-accessible shop." },
            new TraitDefinition { Key = "politicalFaction",  ValueType = "bool", AppliesTo = "organizations", Description = "True if the organization controls or contests territory." },

            // ── Dialogue traits ────────────────────────────────────────────────
            new TraitDefinition { Key = "friendly",      ValueType = "bool", AppliesTo = "dialogues",     Description = "True if the dialogue has a friendly tone." },
            new TraitDefinition { Key = "merchant",      ValueType = "bool", AppliesTo = "dialogues",     Description = "True if the dialogue line is spoken by a merchant NPC." },
            new TraitDefinition { Key = "questRelated",  ValueType = "bool", AppliesTo = "dialogues",     Description = "True if the dialogue line is related to a quest." },
            new TraitDefinition { Key = "greeting",      ValueType = "bool", AppliesTo = "dialogues",     Description = "True if this is a greeting dialogue shown on first interaction." },
            new TraitDefinition { Key = "farewell",      ValueType = "bool", AppliesTo = "dialogues",     Description = "True if this is a farewell dialogue shown when leaving an NPC." },

            // ── World location traits ──────────────────────────────────────────
            new TraitDefinition { Key = "isIndoor",       ValueType = "bool", AppliesTo = "worldlocations", Description = "True if the location is inside a building, cave, or enclosed space." },
            new TraitDefinition { Key = "hasMerchant",    ValueType = "bool", AppliesTo = "worldlocations", Description = "True if a merchant NPC can be found at this location." },
            new TraitDefinition { Key = "pvpEnabled",     ValueType = "bool", AppliesTo = "worldlocations", Description = "True if player-versus-player combat is allowed here." },
            new TraitDefinition { Key = "isDiscoverable", ValueType = "bool", AppliesTo = "worldlocations", Description = "True if the location is hidden on the map until first visited." },
            new TraitDefinition { Key = "isDungeon",      ValueType = "bool", AppliesTo = "worldlocations", Description = "True if this location is a structured dungeon encounter." },
            new TraitDefinition { Key = "isTown",         ValueType = "bool", AppliesTo = "worldlocations", Description = "True if this location is a populated town or settlement." },

            // ── Wildcard traits (apply to all entity types) ────────────────────
            new TraitDefinition { Key = "active",   ValueType = "bool", AppliesTo = "*", Description = "True if the record is published and visible in-game." }
        );

        await db.SaveChangesAsync();
    }
}
