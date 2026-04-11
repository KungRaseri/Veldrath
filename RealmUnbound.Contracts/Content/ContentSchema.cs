namespace Veldrath.Contracts.Content;

/// <summary>Describes the type of UI control to render for a content field.</summary>
public enum ContentFieldType
{
    Text,
    Slug,
    LongText,
    Integer,
    Decimal,
    Boolean,
    EnumString,
}

/// <summary>
/// Describes a single editable or displayable field on a content entity.
/// </summary>
/// <param name="Name">
/// Dot-separated JSON path (camelCase) used as the key in the submission payload.
/// Nested paths (e.g. <c>stats.cooldown</c>) map into owned JSONB sub-objects.
/// </param>
/// <param name="Label">Human-readable label shown in the UI.</param>
/// <param name="FieldType">The kind of input control to render.</param>
/// <param name="Required">Whether the field must have a value.</param>
/// <param name="Default">Optional string representation of the default value.</param>
/// <param name="Min">Minimum allowable numeric value.</param>
/// <param name="Max">Maximum allowable numeric value.</param>
/// <param name="EnumValues">Allowed string values when <see cref="FieldType"/> is <see cref="ContentFieldType.EnumString"/>.</param>
/// <param name="Hint">Optional informational hint shown beneath the input.</param>
public record ContentFieldDescriptor(
    string Name,
    string Label,
    ContentFieldType FieldType,
    bool Required = false,
    string? Default = null,
    double? Min = null,
    double? Max = null,
    string[]? EnumValues = null,
    string? Hint = null);

/// <summary>A named group of fields mapping to a Stats, Traits, or Effects section.</summary>
public record ContentFieldGroup(string Label, IReadOnlyList<ContentFieldDescriptor> Fields);

/// <summary>Complete field schema for one content type.</summary>
/// <param name="ContentType">Canonical type key matching the server's browse endpoint segment.</param>
/// <param name="DisplayLabel">Human-friendly plural label (e.g. "Abilities").</param>
/// <param name="Description">One-sentence description used in submission hints.</param>
/// <param name="Groups">Ordered field groups that define the full form.</param>
public record ContentTypeSchema(
    string ContentType,
    string DisplayLabel,
    string Description,
    IReadOnlyList<ContentFieldGroup> Groups);

/// <summary>
/// Static registry mapping every content type key to its <see cref="ContentTypeSchema"/>.
/// Used by RealmFoundry to render structured submission forms, and by RealmForge to provide
/// explicit field metadata (labels, constraints) in place of raw reflection.
/// </summary>
public static class ContentSchemaRegistry
{
    private static readonly Dictionary<string, ContentTypeSchema> _schemas =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ability"]          = AbilitySchema(),
            ["species"]          = SpeciesSchema(),
            ["class"]            = ClassSchema(),
            ["archetype"]        = ArchetypeSchema(),
            ["instance"]         = InstanceSchema(),
            ["background"]       = BackgroundSchema(),
            ["skill"]            = SkillSchema(),
            ["weapon"]           = WeaponSchema(),
            ["armor"]            = ArmorSchema(),
            ["item"]             = ItemSchema(),
            ["material"]         = MaterialSchema(),
            ["materialproperty"] = MaterialPropertySchema(),
            ["enchantment"]      = EnchantmentSchema(),
            ["spell"]            = SpellSchema(),
            ["quest"]            = QuestSchema(),
            ["recipe"]           = RecipeSchema(),
            ["loottable"]        = LootTableSchema(),
            ["organization"]     = OrganizationSchema(),
            ["zonelocation"]    = ZoneLocationSchema(),
            ["dialogue"]         = DialogueSchema(),
        };

    /// <summary>All registered schemas, keyed by content type (case-insensitive).</summary>
    public static IReadOnlyDictionary<string, ContentTypeSchema> All => _schemas;

    /// <summary>Returns the schema for the given content type, or <c>null</c> if not found.</summary>
    public static ContentTypeSchema? Get(string contentType) =>
        _schemas.TryGetValue(contentType, out var s) ? s : null;

    /// <summary>All registered content type keys in alphabetical order.</summary>
    public static IReadOnlyList<string> AllTypes => [.. _schemas.Keys.Order()];

    // Shared helpers
    private static ContentFieldGroup Identity() => new("Identity", [
        new("displayName", "Display Name", ContentFieldType.Text,    Required: true),
        new("slug",        "Slug",         ContentFieldType.Slug,    Required: true,
            Hint: "Lowercase letters, numbers, and hyphens only (e.g. fire-bolt)"),
        new("rarityWeight","Rarity Weight", ContentFieldType.Integer, Default: "50", Min: 1, Max: 100,
            Hint: "1 = extremely rare, 100 = very common"),
        new("isActive",    "Active",        ContentFieldType.Boolean, Default: "true"),
    ]);

    // Ability
    private static ContentTypeSchema AbilitySchema() => new(
        "ability", "Abilities",
        "An active, passive, reactive, or ultimate ability that actors can use.",
        [
            Identity(),
            new("Ability", [
                new("abilityType", "Ability Type", ContentFieldType.EnumString, Required: true,
                    EnumValues: ["active", "passive", "reactive", "ultimate"],
                    Hint: "Base classification of when and how the ability is triggered"),
            ]),
            new("Stats", [
                new("stats.cooldown",  "Cooldown (s)",   ContentFieldType.Decimal, Min: 0, Max: 300),
                new("stats.manaCost",  "Mana Cost",      ContentFieldType.Integer, Min: 0, Max: 1000),
                new("stats.castTime",  "Cast Time (s)",  ContentFieldType.Decimal, Min: 0, Max: 60),
                new("stats.range",     "Range",          ContentFieldType.Decimal, Min: 0, Max: 200),
                new("stats.damageMin", "Damage Min",     ContentFieldType.Integer, Min: 0),
                new("stats.damageMax", "Damage Max",     ContentFieldType.Integer, Min: 0),
                new("stats.healMin",   "Heal Min",       ContentFieldType.Integer, Min: 0),
                new("stats.healMax",   "Heal Max",       ContentFieldType.Integer, Min: 0),
                new("stats.duration",  "Duration (s)",   ContentFieldType.Decimal, Min: 0),
                new("stats.radius",    "AoE Radius",     ContentFieldType.Integer, Min: 0, Max: 100),
                new("stats.maxTargets","Max Targets",    ContentFieldType.Integer, Min: 1, Max: 50),
            ]),
            new("Effects", [
                new("effects.damageType",      "Damage Type",       ContentFieldType.Text,
                    Hint: "e.g. fire, frost, arcane, physical, poison"),
                new("effects.conditionApplied","Condition Applied", ContentFieldType.Text,
                    Hint: "e.g. stunned, poisoned, burning"),
                new("effects.conditionChance", "Condition Chance",  ContentFieldType.Decimal, Min: 0, Max: 1,
                    Hint: "Probability 0.0–1.0"),
                new("effects.buffApplied",     "Buff Applied",      ContentFieldType.Text),
                new("effects.debuffApplied",   "Debuff Applied",    ContentFieldType.Text),
            ]),
            new("Traits", [
                new("traits.requiresTarget", "Requires Target", ContentFieldType.Boolean),
                new("traits.isAoe",          "Is AoE",          ContentFieldType.Boolean),
                new("traits.hasCooldown",    "Has Cooldown",    ContentFieldType.Boolean),
                new("traits.isChanneled",    "Is Channeled",    ContentFieldType.Boolean),
                new("traits.isInstant",      "Is Instant",      ContentFieldType.Boolean),
                new("traits.canCrit",        "Can Crit",        ContentFieldType.Boolean),
                new("traits.isPassive",      "Is Passive",      ContentFieldType.Boolean),
                new("traits.requiresWeapon", "Requires Weapon", ContentFieldType.Boolean),
            ]),
        ]);

    // Species
    private static ContentTypeSchema SpeciesSchema() => new(
        "species", "Species",
        "A biological species definition providing innate stat ranges and trait flags.",
        [
            Identity(),
            new("Stats", [
                new("stats.baseStrength",    "Base Strength",    ContentFieldType.Integer, Min: 1, Max: 30),
                new("stats.baseAgility",     "Base Agility",     ContentFieldType.Integer, Min: 1, Max: 30),
                new("stats.baseIntelligence","Base Intelligence",ContentFieldType.Integer, Min: 1, Max: 30),
                new("stats.baseConstitution","Base Constitution",ContentFieldType.Integer, Min: 1, Max: 30),
                new("stats.baseHealth",      "Base Health",      ContentFieldType.Integer, Min: 1, Max: 10000),
                new("stats.naturalArmor",    "Natural Armor",    ContentFieldType.Integer, Min: 0, Max: 100),
                new("stats.movementSpeed",   "Movement Speed",   ContentFieldType.Decimal, Min: 0.1, Max: 20),
                new("stats.sizeCategory",    "Size Category",    ContentFieldType.EnumString,
                    EnumValues: ["tiny", "small", "medium", "large", "huge", "gargantuan"]),
            ]),
            new("Traits", [
                new("traits.undead",    "Undead",    ContentFieldType.Boolean),
                new("traits.beast",     "Beast",     ContentFieldType.Boolean),
                new("traits.humanoid",  "Humanoid",  ContentFieldType.Boolean),
                new("traits.demon",     "Demon",     ContentFieldType.Boolean),
                new("traits.dragon",    "Dragon",    ContentFieldType.Boolean),
                new("traits.elemental", "Elemental", ContentFieldType.Boolean),
                new("traits.construct", "Construct", ContentFieldType.Boolean),
                new("traits.darkvision","Darkvision",ContentFieldType.Boolean),
                new("traits.aquatic",   "Aquatic",   ContentFieldType.Boolean),
                new("traits.flying",    "Flying",    ContentFieldType.Boolean),
            ]),
        ]);

    // ActorClass
    private static ContentTypeSchema ClassSchema() => new(
        "class", "Classes",
        "A class definition shaping an actor's combat style and stat growth curves.",
        [
            Identity(),
            new("Class", [
                new("hitDie",      "Hit Die",      ContentFieldType.Integer, Required: true, Min: 4, Max: 20,
                    Hint: "Sides on the health die per level (e.g. 10 = Fighter, 6 = Wizard)"),
                new("primaryStat", "Primary Stat", ContentFieldType.EnumString, Required: true,
                    EnumValues: ["strength", "dexterity", "intelligence", "constitution"]),
            ]),
            new("Stats", [
                new("stats.baseHealth",        "Base Health",      ContentFieldType.Integer, Min: 1),
                new("stats.baseMana",          "Base Mana",        ContentFieldType.Integer, Min: 0),
                new("stats.healthGrowth",      "Health Growth/Lvl",ContentFieldType.Decimal, Min: 0),
                new("stats.manaGrowth",        "Mana Growth/Lvl",  ContentFieldType.Decimal, Min: 0),
                new("stats.strengthGrowth",    "Str Growth/Lvl",   ContentFieldType.Decimal, Min: 0),
                new("stats.dexterityGrowth",   "Dex Growth/Lvl",   ContentFieldType.Decimal, Min: 0),
                new("stats.intelligenceGrowth","Int Growth/Lvl",   ContentFieldType.Decimal, Min: 0),
                new("stats.constitutionGrowth","Con Growth/Lvl",   ContentFieldType.Decimal, Min: 0),
            ]),
            new("Traits", [
                new("traits.canDualWield",  "Can Dual-Wield",  ContentFieldType.Boolean),
                new("traits.canWearHeavy",  "Can Wear Heavy",  ContentFieldType.Boolean),
                new("traits.spellcaster",   "Spellcaster",     ContentFieldType.Boolean),
                new("traits.canWearShield", "Can Wear Shield", ContentFieldType.Boolean),
                new("traits.melee",         "Melee",           ContentFieldType.Boolean),
                new("traits.ranged",        "Ranged",          ContentFieldType.Boolean),
                new("traits.stealth",       "Stealth",         ContentFieldType.Boolean),
            ]),
        ]);

    // ActorArchetype
    private static ContentTypeSchema ArchetypeSchema() => new(
        "archetype", "Actor Archetypes",
        "A composed actor template covering both enemies and NPCs. Combines species, class, and authored flat stats.",
        [
            Identity(),
            new("Archetype", [
                new("minLevel", "Min Level", ContentFieldType.Integer, Required: true, Min: 1, Max: 100),
                new("maxLevel", "Max Level", ContentFieldType.Integer, Required: true, Min: 1, Max: 100),
            ]),
            new("Stats", [
                new("stats.health",          "Health",           ContentFieldType.Integer, Min: 1),
                new("stats.mana",            "Mana",             ContentFieldType.Integer, Min: 0),
                new("stats.strength",        "Strength",         ContentFieldType.Integer, Min: 1, Max: 30),
                new("stats.agility",         "Agility",          ContentFieldType.Integer, Min: 1, Max: 30),
                new("stats.intelligence",    "Intelligence",     ContentFieldType.Integer, Min: 1, Max: 30),
                new("stats.constitution",    "Constitution",     ContentFieldType.Integer, Min: 1, Max: 30),
                new("stats.armorClass",      "Armor Class",      ContentFieldType.Integer, Min: 0),
                new("stats.attackBonus",     "Attack Bonus",     ContentFieldType.Integer),
                new("stats.damage",          "Damage",           ContentFieldType.Integer, Min: 0),
                new("stats.experienceReward","XP Reward",        ContentFieldType.Integer, Min: 0),
                new("stats.goldRewardMin",   "Gold Reward Min",  ContentFieldType.Integer, Min: 0),
                new("stats.goldRewardMax",   "Gold Reward Max",  ContentFieldType.Integer, Min: 0),
                new("stats.tradeSkill",      "Trade Skill",      ContentFieldType.Integer, Min: 0),
                new("stats.tradeGold",       "Trade Gold",       ContentFieldType.Integer, Min: 0),
            ]),
            new("Traits", [
                new("traits.hostile",    "Hostile",      ContentFieldType.Boolean),
                new("traits.aggressive", "Aggressive",   ContentFieldType.Boolean),
                new("traits.packHunter", "Pack Hunter",  ContentFieldType.Boolean),
                new("traits.shopkeeper", "Shopkeeper",   ContentFieldType.Boolean),
                new("traits.questGiver", "Quest Giver",  ContentFieldType.Boolean),
                new("traits.hasDialogue","Has Dialogue", ContentFieldType.Boolean),
                new("traits.immortal",   "Immortal",     ContentFieldType.Boolean),
                new("traits.wanderer",   "Wanderer",     ContentFieldType.Boolean),
                new("traits.boss",       "Boss",         ContentFieldType.Boolean),
                new("traits.elite",      "Elite",        ContentFieldType.Boolean),
                new("traits.ranged",     "Ranged",       ContentFieldType.Boolean),
                new("traits.caster",     "Caster",       ContentFieldType.Boolean),
                new("traits.fireImmune", "Fire Immune",  ContentFieldType.Boolean),
                new("traits.coldImmune", "Cold Immune",  ContentFieldType.Boolean),
                new("traits.poisonImmune","Poison Immune",ContentFieldType.Boolean),
            ]),
        ]);

    // ActorInstance
    private static ContentTypeSchema InstanceSchema() => new(
        "instance", "Actor Instances",
        "A named unique actor that overrides a base archetype for quest-critical or boss encounters.",
        [
            Identity(),
            new("Instance", [
                new("archetypeId",    "Base Archetype ID", ContentFieldType.Text, Required: true,
                    Hint: "GUID of the ActorArchetype this instance overrides"),
                new("levelOverride",  "Level Override",    ContentFieldType.Integer, Min: 1, Max: 100,
                    Hint: "Leave blank to inherit the archetype's MinLevel"),
                new("factionOverride","Faction Override",  ContentFieldType.Text,
                    Hint: "Faction slug override (optional)"),
            ]),
            new("Stat Overrides", [
                new("statOverrides.health",         "Health",          ContentFieldType.Integer, Min: 1),
                new("statOverrides.mana",           "Mana",            ContentFieldType.Integer, Min: 0),
                new("statOverrides.strength",       "Strength",        ContentFieldType.Integer, Min: 1),
                new("statOverrides.agility",        "Agility",         ContentFieldType.Integer, Min: 1),
                new("statOverrides.intelligence",   "Intelligence",    ContentFieldType.Integer, Min: 1),
                new("statOverrides.constitution",   "Constitution",    ContentFieldType.Integer, Min: 1),
                new("statOverrides.armorClass",     "Armor Class",     ContentFieldType.Integer, Min: 0),
                new("statOverrides.attackBonus",    "Attack Bonus",    ContentFieldType.Integer),
                new("statOverrides.damage",         "Damage",          ContentFieldType.Integer, Min: 0),
                new("statOverrides.experienceReward","XP Reward",      ContentFieldType.Integer, Min: 0),
                new("statOverrides.goldRewardMin",  "Gold Reward Min", ContentFieldType.Integer, Min: 0),
                new("statOverrides.goldRewardMax",  "Gold Reward Max", ContentFieldType.Integer, Min: 0),
            ]),
        ]);

    // Background
    private static ContentTypeSchema BackgroundSchema() => new(
        "background", "Backgrounds",
        "A character background providing origin bonuses to stats and starting conditions.",
        [
            Identity(),
            new("Stats", [
                new("stats.startingGold",      "Starting Gold",      ContentFieldType.Integer, Min: 0),
                new("stats.bonusStrength",     "Bonus Strength",     ContentFieldType.Integer),
                new("stats.bonusDexterity",    "Bonus Dexterity",    ContentFieldType.Integer),
                new("stats.bonusIntelligence", "Bonus Intelligence", ContentFieldType.Integer),
                new("stats.bonusConstitution", "Bonus Constitution", ContentFieldType.Integer),
                new("stats.startingSkillBonus","Starting Skill Bonus (slug)", ContentFieldType.Text,
                    Hint: "Slug of the skill that receives a starting bonus rank"),
                new("stats.skillBonusValue",   "Skill Bonus Value",  ContentFieldType.Integer, Min: 0),
            ]),
            new("Traits", [
                new("traits.regional", "Regional", ContentFieldType.Boolean),
                new("traits.noble",    "Noble",    ContentFieldType.Boolean),
                new("traits.criminal", "Criminal", ContentFieldType.Boolean),
                new("traits.merchant", "Merchant", ContentFieldType.Boolean),
                new("traits.military", "Military", ContentFieldType.Boolean),
                new("traits.scholar",  "Scholar",  ContentFieldType.Boolean),
                new("traits.religious","Religious",ContentFieldType.Boolean),
            ]),
        ]);

    // Skill
    private static ContentTypeSchema SkillSchema() => new(
        "skill", "Skills",
        "A learnable skill that grants bonuses as rank increases.",
        [
            Identity(),
            new("Skill", [
                new("maxRank",           "Max Rank",            ContentFieldType.Integer, Required: true, Min: 1, Max: 10),
                new("governingAttribute","Governing Attribute", ContentFieldType.EnumString,
                    EnumValues: ["strength", "dexterity", "intelligence", "constitution", ""],
                    Hint: "Attribute that scales this skill; leave blank if ungoverned"),
            ]),
            new("Stats", [
                new("stats.xpPerRank",   "XP Per Rank",   ContentFieldType.Integer, Min: 0),
                new("stats.bonusPerRank","Bonus Per Rank", ContentFieldType.Decimal, Min: 0),
                new("stats.baseValue",   "Base Value",    ContentFieldType.Integer, Min: 0),
            ]),
            new("Traits", [
                new("traits.passive",    "Passive",    ContentFieldType.Boolean),
                new("traits.combat",     "Combat",     ContentFieldType.Boolean),
                new("traits.crafting",   "Crafting",   ContentFieldType.Boolean),
                new("traits.social",     "Social",     ContentFieldType.Boolean),
                new("traits.exploration","Exploration",ContentFieldType.Boolean),
            ]),
        ]);

    // Weapon
    private static ContentTypeSchema WeaponSchema() => new(
        "weapon", "Weapons",
        "A weapon item with damage, speed, and critical hit characteristics.",
        [
            Identity(),
            new("Weapon", [
                new("weaponType",   "Weapon Type",   ContentFieldType.EnumString, Required: true,
                    EnumValues: ["sword", "axe", "mace", "bow", "staff", "dagger", "spear", "crossbow", "shield"]),
                new("damageType",   "Damage Type",   ContentFieldType.EnumString, Required: true,
                    EnumValues: ["physical", "fire", "frost", "lightning", "poison", "arcane", "holy", "shadow"]),
                new("handsRequired","Hands Required",ContentFieldType.Integer, Required: true, Min: 1, Max: 2),
            ]),
            new("Stats", [
                new("stats.damageMin",  "Damage Min",   ContentFieldType.Integer, Min: 0),
                new("stats.damageMax",  "Damage Max",   ContentFieldType.Integer, Min: 0),
                new("stats.attackSpeed","Attack Speed", ContentFieldType.Decimal, Min: 0.1, Max: 10,
                    Hint: "Attacks per second"),
                new("stats.critChance", "Crit Chance",  ContentFieldType.Decimal, Min: 0, Max: 1,
                    Hint: "Probability 0.0–1.0"),
                new("stats.range",      "Range",        ContentFieldType.Decimal, Min: 0),
                new("stats.weight",     "Weight (lbs)", ContentFieldType.Decimal, Min: 0),
                new("stats.durability", "Durability",   ContentFieldType.Integer, Min: 0),
                new("stats.value",      "Value (gold)", ContentFieldType.Integer, Min: 0),
            ]),
            new("Traits", [
                new("traits.twoHanded","Two-Handed",ContentFieldType.Boolean),
                new("traits.throwable","Throwable",  ContentFieldType.Boolean),
                new("traits.silvered", "Silvered",   ContentFieldType.Boolean),
                new("traits.magical",  "Magical",    ContentFieldType.Boolean),
                new("traits.finesse",  "Finesse",    ContentFieldType.Boolean),
                new("traits.reach",    "Reach",      ContentFieldType.Boolean),
                new("traits.versatile","Versatile",  ContentFieldType.Boolean),
            ]),
        ]);

    // Armor
    private static ContentTypeSchema ArmorSchema() => new(
        "armor", "Armors",
        "A piece of armor providing physical and magical damage reduction.",
        [
            Identity(),
            new("Armor", [
                new("armorType","Armor Type", ContentFieldType.EnumString, Required: true,
                    EnumValues: ["light", "medium", "heavy", "shield"]),
                new("equipSlot","Equip Slot", ContentFieldType.EnumString, Required: true,
                    EnumValues: ["head", "chest", "legs", "feet", "hands", "offhand"]),
            ]),
            new("Stats", [
                new("stats.armorRating",     "Armor Rating",     ContentFieldType.Integer, Min: 0),
                new("stats.magicResist",     "Magic Resist",     ContentFieldType.Integer, Min: 0),
                new("stats.weight",          "Weight (lbs)",     ContentFieldType.Decimal, Min: 0),
                new("stats.durability",      "Durability",       ContentFieldType.Integer, Min: 0),
                new("stats.movementPenalty", "Movement Penalty", ContentFieldType.Decimal, Min: 0, Max: 1),
                new("stats.value",           "Value (gold)",     ContentFieldType.Integer, Min: 0),
            ]),
            new("Traits", [
                new("traits.stealthPenalty", "Stealth Penalty", ContentFieldType.Boolean),
                new("traits.fireResist",     "Fire Resist",     ContentFieldType.Boolean),
                new("traits.coldResist",     "Cold Resist",     ContentFieldType.Boolean),
                new("traits.lightningResist","Lightning Resist",ContentFieldType.Boolean),
                new("traits.cursed",         "Cursed",          ContentFieldType.Boolean),
                new("traits.magical",        "Magical",         ContentFieldType.Boolean),
            ]),
        ]);

    // Item
    private static ContentTypeSchema ItemSchema() => new(
        "item", "Items",
        "A consumable, gem, rune, crystal, or general inventory item.",
        [
            Identity(),
            new("Item", [
                new("itemType","Item Type",ContentFieldType.EnumString, Required: true,
                    EnumValues: ["consumable", "crystal", "gem", "rune", "essence", "orb"]),
            ]),
            new("Stats", [
                new("stats.weight",      "Weight (lbs)",  ContentFieldType.Decimal, Min: 0),
                new("stats.stackSize",   "Stack Size",    ContentFieldType.Integer, Min: 1, Max: 999),
                new("stats.value",       "Value (gold)",  ContentFieldType.Integer, Min: 0),
                new("stats.effectPower", "Effect Power",  ContentFieldType.Decimal, Min: 0),
                new("stats.duration",    "Duration (s)",  ContentFieldType.Decimal, Min: 0),
            ]),
            new("Traits", [
                new("traits.stackable", "Stackable",  ContentFieldType.Boolean),
                new("traits.questItem", "Quest Item", ContentFieldType.Boolean),
                new("traits.unique",    "Unique",     ContentFieldType.Boolean),
                new("traits.soulbound", "Soulbound",  ContentFieldType.Boolean),
                new("traits.consumable","Consumable", ContentFieldType.Boolean),
                new("traits.magical",   "Magical",    ContentFieldType.Boolean),
            ]),
        ]);

    // Material
    private static ContentTypeSchema MaterialSchema() => new(
        "material", "Materials",
        "A craftable material used in item recipes, with physical and magical properties.",
        [
            Identity(),
            new("Material", [
                new("materialFamily","Material Family",ContentFieldType.EnumString, Required: true,
                    EnumValues: ["metal", "wood", "leather", "gem", "fabric", "bone", "stone"]),
                new("costScale",     "Cost Scale",     ContentFieldType.Decimal, Required: true, Min: 0.1, Max: 10,
                    Hint: "Budget multiplier: cost = (6000 / rarityWeight) × costScale"),
            ]),
            new("Stats", [
                new("stats.hardness",    "Hardness",      ContentFieldType.Decimal, Min: 0, Max: 10),
                new("stats.weight",      "Weight",        ContentFieldType.Decimal, Min: 0),
                new("stats.conductivity","Conductivity",  ContentFieldType.Decimal, Min: 0, Max: 10),
                new("stats.magicAffinity","Magic Affinity",ContentFieldType.Decimal, Min: 0, Max: 10),
                new("stats.value",       "Value (gold)",  ContentFieldType.Integer, Min: 0),
            ]),
            new("Traits", [
                new("traits.fireResist", "Fire Resist",ContentFieldType.Boolean),
                new("traits.flexible",   "Flexible",   ContentFieldType.Boolean),
                new("traits.brittle",    "Brittle",    ContentFieldType.Boolean),
                new("traits.enchantable","Enchantable",ContentFieldType.Boolean),
                new("traits.magical",    "Magical",    ContentFieldType.Boolean),
                new("traits.conductive", "Conductive", ContentFieldType.Boolean),
            ]),
        ]);

    // MaterialProperty
    private static ContentTypeSchema MaterialPropertySchema() => new(
        "materialproperty", "Material Properties",
        "A material property variant defining additional physical and magical characteristics.",
        [
            Identity(),
            new("Material Property", [
                new("materialFamily","Material Family",ContentFieldType.EnumString, Required: true,
                    EnumValues: ["metal", "wood", "leather", "gem", "fabric", "bone", "stone"]),
                new("costScale",     "Cost Scale",     ContentFieldType.Decimal, Required: true, Min: 0.1, Max: 10),
            ]),
            new("Stats", [
                new("stats.hardness",    "Hardness",      ContentFieldType.Decimal, Min: 0, Max: 10),
                new("stats.weight",      "Weight",        ContentFieldType.Decimal, Min: 0),
                new("stats.conductivity","Conductivity",  ContentFieldType.Decimal, Min: 0, Max: 10),
                new("stats.magicAffinity","Magic Affinity",ContentFieldType.Decimal, Min: 0, Max: 10),
                new("stats.durability",  "Durability",    ContentFieldType.Decimal, Min: 0, Max: 1),
                new("stats.value",       "Value (gold)",  ContentFieldType.Integer, Min: 0),
            ]),
            new("Traits", [
                new("traits.conducting", "Conducting", ContentFieldType.Boolean),
                new("traits.brittle",    "Brittle",    ContentFieldType.Boolean),
                new("traits.magical",    "Magical",    ContentFieldType.Boolean),
                new("traits.flexible",   "Flexible",   ContentFieldType.Boolean),
                new("traits.transparent","Transparent",ContentFieldType.Boolean),
                new("traits.fireproof",  "Fireproof",  ContentFieldType.Boolean),
                new("traits.enchantable","Enchantable",ContentFieldType.Boolean),
            ]),
        ]);

    // Enchantment
    private static ContentTypeSchema EnchantmentSchema() => new(
        "enchantment", "Enchantments",
        "An enchantment applied to weapons or armor, granting bonus stats.",
        [
            Identity(),
            new("Enchantment", [
                new("targetSlot","Target Slot",ContentFieldType.EnumString,
                    EnumValues: ["weapon", "armor", "any", ""],
                    Hint: "Which item slot this enchantment applies to; blank = unrestricted"),
            ]),
            new("Stats", [
                new("stats.bonusDamage",      "Bonus Damage",       ContentFieldType.Integer),
                new("stats.bonusArmor",       "Bonus Armor",        ContentFieldType.Integer),
                new("stats.bonusStrength",    "Bonus Strength",     ContentFieldType.Integer),
                new("stats.bonusDexterity",   "Bonus Dexterity",    ContentFieldType.Integer),
                new("stats.bonusIntelligence","Bonus Intelligence",  ContentFieldType.Integer),
                new("stats.manaCostReduction","Mana Cost Reduction", ContentFieldType.Decimal, Min: 0, Max: 1),
                new("stats.attackSpeedBonus", "Attack Speed Bonus", ContentFieldType.Decimal, Min: 0, Max: 2),
                new("stats.value",            "Value (gold)",       ContentFieldType.Integer, Min: 0),
            ]),
            new("Traits", [
                new("traits.stackable",        "Stackable",          ContentFieldType.Boolean),
                new("traits.exclusive",        "Exclusive",          ContentFieldType.Boolean),
                new("traits.requiresMagicItem","Requires Magic Item",ContentFieldType.Boolean),
                new("traits.cursed",           "Cursed",             ContentFieldType.Boolean),
                new("traits.permanent",        "Permanent",          ContentFieldType.Boolean),
            ]),
        ]);

    // Spell
    private static ContentTypeSchema SpellSchema() => new(
        "spell", "Spells",
        "A learnable magic spell cast using mana, associated with a school of magic.",
        [
            Identity(),
            new("Spell", [
                new("school","School",ContentFieldType.EnumString, Required: true,
                    EnumValues: ["fire", "frost", "arcane", "holy", "shadow", "nature", "lightning", "earth"]),
            ]),
            new("Stats", [
                new("stats.manaCost",  "Mana Cost",     ContentFieldType.Integer, Min: 0, Max: 1000),
                new("stats.castTime",  "Cast Time (s)", ContentFieldType.Decimal, Min: 0, Max: 60),
                new("stats.cooldown",  "Cooldown (s)",  ContentFieldType.Decimal, Min: 0, Max: 300),
                new("stats.range",     "Range",         ContentFieldType.Decimal, Min: 0, Max: 200),
                new("stats.damageMin", "Damage Min",    ContentFieldType.Integer, Min: 0),
                new("stats.damageMax", "Damage Max",    ContentFieldType.Integer, Min: 0),
                new("stats.healMin",   "Heal Min",      ContentFieldType.Integer, Min: 0),
                new("stats.healMax",   "Heal Max",      ContentFieldType.Integer, Min: 0),
                new("stats.duration",  "Duration (s)",  ContentFieldType.Decimal, Min: 0),
            ]),
            new("Traits", [
                new("traits.requiresStaff",        "Requires Staff",        ContentFieldType.Boolean),
                new("traits.isAoe",                "Is AoE",                ContentFieldType.Boolean),
                new("traits.isChanneled",          "Is Channeled",          ContentFieldType.Boolean),
                new("traits.instant",              "Instant",               ContentFieldType.Boolean),
                new("traits.canCrit",              "Can Crit",              ContentFieldType.Boolean),
                new("traits.requiresConcentration","Requires Concentration",ContentFieldType.Boolean),
            ]),
        ]);

    // Quest
    private static ContentTypeSchema QuestSchema() => new(
        "quest", "Quests",
        "A quest with objectives and rewards that drives player progression.",
        [
            Identity(),
            new("Quest", [
                new("minLevel","Min Level",ContentFieldType.Integer, Required: true, Min: 1, Max: 100),
            ]),
            new("Stats", [
                new("stats.xpReward",        "XP Reward",         ContentFieldType.Integer, Min: 0),
                new("stats.goldReward",       "Gold Reward",       ContentFieldType.Integer, Min: 0),
                new("stats.reputationReward", "Reputation Reward", ContentFieldType.Integer, Min: 0),
            ]),
            new("Traits", [
                new("traits.repeatable",            "Repeatable",              ContentFieldType.Boolean),
                new("traits.mainStory",             "Main Story",              ContentFieldType.Boolean),
                new("traits.timed",                 "Timed",                   ContentFieldType.Boolean),
                new("traits.groupQuest",            "Group Quest",             ContentFieldType.Boolean),
                new("traits.hiddenUntilDiscovered", "Hidden Until Discovered", ContentFieldType.Boolean),
            ]),
            new("Objectives & Rewards", [
                new("objectives", "Objectives (JSON)", ContentFieldType.LongText,
                    Hint: "JSON array: [{\"type\":\"kill\",\"target\":\"wolf\",\"quantity\":5,\"description\":\"Kill wolves\"}]"),
                new("rewards", "Rewards (JSON)", ContentFieldType.LongText,
                    Hint: "JSON array: [{\"type\":\"item\",\"itemDomain\":\"items/weapons\",\"itemSlug\":\"iron-sword\",\"quantity\":1}]"),
            ]),
        ]);

    // Recipe
    private static ContentTypeSchema RecipeSchema() => new(
        "recipe", "Recipes",
        "A crafting recipe that consumes ingredients to produce a single output item.",
        [
            Identity(),
            new("Recipe", [
                new("outputItemDomain","Output Item Domain",ContentFieldType.Text, Required: true,
                    Hint: "Content domain of the output item (e.g. items/weapons)"),
                new("outputItemSlug",  "Output Item Slug",  ContentFieldType.Text, Required: true),
                new("outputQuantity",  "Output Quantity",   ContentFieldType.Integer, Required: true, Min: 1, Max: 100),
                new("craftingSkill",   "Crafting Skill",    ContentFieldType.Text, Required: true,
                    Hint: "Slug of the required skill (e.g. blacksmithing)"),
                new("craftingLevel",   "Crafting Level",    ContentFieldType.Integer, Required: true, Min: 1, Max: 100),
            ]),
            new("Traits", [
                new("traits.discoverable",    "Discoverable",    ContentFieldType.Boolean),
                new("traits.requiresStation", "Requires Station",ContentFieldType.Boolean),
                new("traits.requiresFire",    "Requires Fire",   ContentFieldType.Boolean),
                new("traits.isAlchemy",       "Alchemy",         ContentFieldType.Boolean),
                new("traits.isBlacksmithing", "Blacksmithing",   ContentFieldType.Boolean),
                new("traits.isLeatherworking","Leatherworking",  ContentFieldType.Boolean),
            ]),
            new("Ingredients", [
                new("ingredients", "Ingredients (JSON)", ContentFieldType.LongText,
                    Hint: "JSON array: [{\"itemDomain\":\"items/materials\",\"itemSlug\":\"iron-ingot\",\"quantity\":3}]"),
            ]),
        ]);

    // LootTable
    private static ContentTypeSchema LootTableSchema() => new(
        "loottable", "Loot Tables",
        "A loot table defining what items drop from enemies, chests, or resource nodes.",
        [
            Identity(),
            new("Traits", [
                new("traits.boss",       "Boss",        ContentFieldType.Boolean),
                new("traits.elite",      "Elite",       ContentFieldType.Boolean),
                new("traits.common",     "Common",      ContentFieldType.Boolean),
                new("traits.rare",       "Rare",        ContentFieldType.Boolean),
                new("traits.isChest",    "Is Chest",    ContentFieldType.Boolean),
                new("traits.isHarvesting","Is Harvesting",ContentFieldType.Boolean),
            ]),
            new("Entries", [
                new("entries", "Entries (JSON)", ContentFieldType.LongText,
                    Hint: "JSON array: [{\"itemDomain\":\"items/weapons\",\"itemSlug\":\"iron-sword\",\"dropWeight\":10,\"quantityMin\":1,\"quantityMax\":1,\"isGuaranteed\":false}]"),
            ]),
        ]);

    // Organization
    private static ContentTypeSchema OrganizationSchema() => new(
        "organization", "Organizations",
        "A faction, guild, business, or shop that affects world politics and player reputation.",
        [
            Identity(),
            new("Organization", [
                new("orgType","Org Type",ContentFieldType.EnumString, Required: true,
                    EnumValues: ["faction", "guild", "business", "shop"]),
            ]),
            new("Stats", [
                new("stats.memberCount",                "Member Count",               ContentFieldType.Integer, Min: 0),
                new("stats.wealth",                     "Wealth",                     ContentFieldType.Integer, Min: 0),
                new("stats.reputationThresholdFriendly","Rep Threshold (Friendly)",   ContentFieldType.Integer),
                new("stats.reputationThresholdHostile", "Rep Threshold (Hostile)",    ContentFieldType.Integer),
            ]),
            new("Traits", [
                new("traits.hostile",         "Hostile",          ContentFieldType.Boolean),
                new("traits.joinable",         "Joinable",         ContentFieldType.Boolean),
                new("traits.hasShop",          "Has Shop",         ContentFieldType.Boolean),
                new("traits.questGiver",       "Quest Giver",      ContentFieldType.Boolean),
                new("traits.politicalFaction", "Political Faction",ContentFieldType.Boolean),
            ]),
        ]);

    // ZoneLocation
    private static ContentTypeSchema ZoneLocationSchema() => new(
        "zonelocation", "Zone Locations",
        "A named location within a zone — dungeon, settlement, or environment used by the content pipeline.",
        [
            Identity(),
            new("Location", [
                new("zoneId",      "Zone ID",      ContentFieldType.Text, Required: true,
                    Hint: "Slug of the owning Zone (e.g. \"fenwick-crossing\")"),
            ]),
            new("Stats", [
                new("stats.size",       "Size (1–10)",       ContentFieldType.Integer, Min: 1, Max: 10),
                new("stats.dangerLevel","Danger Level (1–10)",ContentFieldType.Integer, Min: 1, Max: 10),
                new("stats.population", "Population",        ContentFieldType.Integer, Min: 0),
                new("stats.minLevel",   "Min Level",         ContentFieldType.Integer, Min: 1, Max: 100),
                new("stats.maxLevel",   "Max Level",         ContentFieldType.Integer, Min: 1, Max: 100),
            ]),
            new("Traits", [
                new("traits.isIndoor",      "Indoor",       ContentFieldType.Boolean),
                new("traits.hasMerchant",   "Has Merchant", ContentFieldType.Boolean),
                new("traits.isDiscoverable","Discoverable", ContentFieldType.Boolean),
                new("traits.isDungeon",     "Dungeon",      ContentFieldType.Boolean),
                new("traits.isTown",        "Town",         ContentFieldType.Boolean),
            ]),
        ]);

    // Dialogue
    private static ContentTypeSchema DialogueSchema() => new(
        "dialogue", "Dialogues",
        "A dialogue entry defining lines, tone, and context for NPC speech.",
        [
            Identity(),
            new("Dialogue", [
                new("speaker","Speaker",ContentFieldType.Text,
                    Hint: "NPC type or slug; leave blank for any speaker"),
            ]),
            new("Stats", [
                new("stats.tone",     "Tone",     ContentFieldType.Integer, Min: 0, Max: 5,
                    Hint: "0 = neutral, 1 = stern, 2 = cheerful, 3 = menacing, 4 = sad, 5 = mysterious"),
                new("stats.formality","Formality",ContentFieldType.Integer, Min: 0, Max: 2,
                    Hint: "0 = casual, 1 = formal, 2 = archaic"),
            ]),
            new("Content", [
                new("stats.lines","Dialogue Lines (JSON)",ContentFieldType.LongText,
                    Hint: "JSON array of strings: [\"Hello, traveller.\",\"What brings you here?\"]"),
            ]),
            new("Traits", [
                new("traits.hostile",    "Hostile",     ContentFieldType.Boolean),
                new("traits.friendly",   "Friendly",    ContentFieldType.Boolean),
                new("traits.merchant",   "Merchant",    ContentFieldType.Boolean),
                new("traits.questRelated","Quest Related",ContentFieldType.Boolean),
                new("traits.greeting",   "Greeting",    ContentFieldType.Boolean),
                new("traits.farewell",   "Farewell",    ContentFieldType.Boolean),
            ]),
        ]);
}
