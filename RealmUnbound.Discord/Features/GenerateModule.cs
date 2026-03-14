using Discord;
using Discord.Interactions;
using MediatR;
using RealmEngine.Core.Features.ItemGeneration.Commands;

namespace RealmUnbound.Discord.Features;

/// <summary>
/// Slash command group for procedurally generating content straight from the realm's engine.
/// Usage: /generate item | /generate enemy | /generate npc | /generate ability
/// </summary>
[Group("generate", "Conjure content from the realm's engine — forged fresh from the data catalog")]
public sealed class GenerateModule(IMediator mediator) : InteractionModuleBase<SocketInteractionContext>
{
    private static readonly Color ItemColor    = new(0x7B2FBE);
    private static readonly Color EnemyColor   = new(0xAD1414);
    private static readonly Color NpcColor     = new(0xC47A00);
    private static readonly Color AbilityColor = new(0x1E5FA3);

    // ──────────────────────────────────────────────────────────────
    // /generate item
    // ──────────────────────────────────────────────────────────────

    [SlashCommand("item", "Forge a random item from the catalog")]
    public async Task ItemAsync(
        [Summary("category", "Item category to generate from")]
        [Choice("Sword",          "weapons/swords")]
        [Choice("Axe",            "weapons/axes")]
        [Choice("Dagger",         "weapons/daggers")]
        [Choice("Bow",            "weapons/bows")]
        [Choice("Staff",          "weapons/staves")]
        [Choice("Mace",           "weapons/maces")]
        [Choice("Light Armor",    "armor/light")]
        [Choice("Medium Armor",   "armor/medium")]
        [Choice("Heavy Armor",    "armor/heavy")]
        [Choice("Potion",         "consumables/potions")]
        [Choice("Food",           "consumables/food")]
        [Choice("Gem",            "gems")]
        string category = "weapons/swords")
    {
        await DeferAsync();

        var result = await mediator.Send(new GenerateItemCommand { Category = category });

        if (!result.Success || result.Item is null)
        {
            await FollowupAsync($"⚠️ The forge ran cold. Could not generate an item from **{category}**." +
                                (result.ErrorMessage is not null ? $"\n*{result.ErrorMessage}*" : ""));
            return;
        }

        var item  = result.Item;
        var title = item.Name;
        var desc  = string.IsNullOrWhiteSpace(item.Description) ? null
                  : item.Description.Length > 200 ? item.Description[..200] + "…"
                  : item.Description;

        var embed = new EmbedBuilder()
            .WithTitle($"⚒️ {title}")
            .WithColor(RarityColor(item.Rarity.ToString()))
            .WithCurrentTimestamp();

        if (desc is not null)
            embed.WithDescription(desc);

        embed
            .AddField("Type",   item.Type.ToString(),    inline: true)
            .AddField("Rarity", item.Rarity.ToString(),  inline: true)
            .AddField("Price",  $"{item.Price}g",         inline: true);

        if (!string.IsNullOrWhiteSpace(item.WeaponType))
            embed.AddField("Weapon Type", item.WeaponType, inline: true);

        if (!string.IsNullOrWhiteSpace(item.ArmorClass))
            embed.AddField("Armor Class", item.ArmorClass, inline: true);

        if (item.Power > 0)
            embed.AddField("Power", item.Power.ToString(), inline: true);

        if (!string.IsNullOrWhiteSpace(item.Effect))
            embed.AddField("Effect", item.Effect.Length > 100 ? item.Effect[..100] + "…" : item.Effect, inline: false);

        if (!string.IsNullOrWhiteSpace(item.Lore))
        {
            var loreText = item.Lore.Length > 120 ? item.Lore[..120] + "..." : item.Lore;
            embed.WithFooter($"\u201c{loreText}\u201d");
        }

        await FollowupAsync(embed: embed.Build());
    }

    // ──────────────────────────────────────────────────────────────
    // /generate enemy
    // ──────────────────────────────────────────────────────────────

    [SlashCommand("enemy", "Summon a random creature from the bestiary")]
    public async Task EnemyAsync(
        [Summary("family", "Enemy family to generate from")]
        [Choice("Beasts",      "beasts")]
        [Choice("Undead",      "undead")]
        [Choice("Dragons",     "dragons")]
        [Choice("Elementals",  "elementals")]
        [Choice("Goblinoids",  "goblinoids")]
        [Choice("Humanoids",   "humanoids")]
        [Choice("Insects",     "insects")]
        [Choice("Orcs",        "orcs")]
        [Choice("Plants",      "plants")]
        [Choice("Reptilians",  "reptilians")]
        [Choice("Trolls",      "trolls")]
        [Choice("Vampires",    "vampires")]
        string family = "beasts",
        [Summary("level", "Override enemy level (1–100)")] int? level = null)
    {
        await DeferAsync();

        var result = await mediator.Send(new GenerateEnemyCommand { Category = family, Level = level });

        if (!result.Success || result.Enemy is null)
        {
            await FollowupAsync($"⚠️ The void returned nothing. Could not conjure a **{family}**." +
                                (result.ErrorMessage is not null ? $"\n*{result.ErrorMessage}*" : ""));
            return;
        }

        var e    = result.Enemy;
        var desc = string.IsNullOrWhiteSpace(e.Description) ? null
                 : e.Description.Length > 200 ? e.Description[..200] + "…"
                 : e.Description;

        var embed = new EmbedBuilder()
            .WithTitle($"🐉 {e.Name}")
            .WithColor(EnemyColor)
            .WithCurrentTimestamp();

        if (desc is not null)
            embed.WithDescription(desc);

        embed
            .AddField("Level",      e.Level.ToString(),      inline: true)
            .AddField("HP",         e.MaxHealth.ToString(),  inline: true)
            .AddField("Type",       e.Type.ToString(),       inline: true)
            .AddField("Difficulty", e.Difficulty.ToString(), inline: true)
            .AddField("XP",         e.XP.ToString(),         inline: true)
            .AddField("Gold",       e.GoldReward.ToString(), inline: true)
            .AddField("Phys. DMG",  e.BasePhysicalDamage.ToString(), inline: true)
            .AddField("Magic DMG",  e.BaseMagicDamage.ToString(),    inline: true)
            .AddField("\u200b",     "\u200b",                        inline: true) // padding
            .AddField("STR / DEX / CON",
                $"{e.Strength} / {e.Dexterity} / {e.Constitution}", inline: true)
            .AddField("INT / WIS / CHA",
                $"{e.Intelligence} / {e.Wisdom} / {e.Charisma}",    inline: true);

        await FollowupAsync(embed: embed.Build());
    }

    // ──────────────────────────────────────────────────────────────
    // /generate npc
    // ──────────────────────────────────────────────────────────────

    [SlashCommand("npc", "Conjure a random denizen of the realm")]
    public async Task NpcAsync(
        [Summary("category", "NPC profession to generate")]
        [Choice("Merchant",    "merchants")]
        [Choice("Craftsman",   "craftsmen")]
        [Choice("Soldier",     "military")]
        [Choice("Professional","professionals")]
        [Choice("Clergy",      "religious")]
        [Choice("Noble",       "noble")]
        [Choice("Criminal",    "criminal")]
        [Choice("Mage",        "magical")]
        [Choice("Common Folk", "common")]
        string category = "common")
    {
        await DeferAsync();

        var result = await mediator.Send(new GenerateNPCCommand { Category = category });

        if (!result.Success || result.NPC is null)
        {
            await FollowupAsync($"⚠️ No one answered the call. Could not generate a **{category}** NPC." +
                                (result.ErrorMessage is not null ? $"\n*{result.ErrorMessage}*" : ""));
            return;
        }

        var npc   = result.NPC;
        var title = string.IsNullOrWhiteSpace(npc.DisplayName) ? npc.Name : npc.DisplayName;

        var embed = new EmbedBuilder()
            .WithTitle($"🧑 {title}")
            .WithColor(NpcColor)
            .WithCurrentTimestamp();

        if (!string.IsNullOrWhiteSpace(npc.Dialogue))
        {
            var line = npc.Dialogue.Length > 150 ? npc.Dialogue[..150] + "…" : npc.Dialogue;
            embed.WithDescription($"*\"{line}\"*");
        }

        if (!string.IsNullOrWhiteSpace(npc.Occupation))
            embed.AddField("Occupation",    npc.Occupation,   inline: true);

        if (!string.IsNullOrWhiteSpace(npc.SocialClass))
            embed.AddField("Social Class",  npc.SocialClass,  inline: true);

        embed.AddField("Age", npc.Age.ToString(), inline: true);

        if (!string.IsNullOrWhiteSpace(npc.BaseGold))
            embed.AddField("Gold", npc.BaseGold, inline: true);

        await FollowupAsync(embed: embed.Build());
    }

    // ──────────────────────────────────────────────────────────────
    // /generate ability
    // ──────────────────────────────────────────────────────────────

    [SlashCommand("ability", "Discover a random ability from the arcane codex")]
    public async Task AbilityAsync(
        [Summary("type", "Ability category to generate from")]
        [Choice("Offensive",  "active/offensive")]
        [Choice("Defensive",  "active/defensive")]
        [Choice("Support",    "active/support")]
        [Choice("Utility",    "active/utility")]
        [Choice("Passive",    "passive")]
        [Choice("Reactive",   "reactive")]
        [Choice("Ultimate",   "ultimate")]
        string type = "active/offensive")
    {
        await DeferAsync();

        // Split "active/offensive" → Category="active", Subcategory="offensive"
        var parts = type.Split('/', 2);
        var result = await mediator.Send(new GenerateAbilityCommand
        {
            Category    = parts[0],
            Subcategory = parts.Length > 1 ? parts[1] : string.Empty,
            Count       = 1,
        });

        if (!result.Success || result.Abilities is null || result.Abilities.Count == 0)
        {
            await FollowupAsync($"⚠️ The arcane scroll was blank. Could not generate a **{type}** ability." +
                                (result.ErrorMessage is not null ? $"\n*{result.ErrorMessage}*" : ""));
            return;
        }

        var ability = result.Abilities[0];
        var name    = string.IsNullOrWhiteSpace(ability.DisplayName) ? ability.Name : ability.DisplayName;
        var desc    = string.IsNullOrWhiteSpace(ability.Description) ? null
                    : ability.Description.Length > 200 ? ability.Description[..200] + "…"
                    : ability.Description;

        var embed = new EmbedBuilder()
            .WithTitle($"✨ {name}")
            .WithColor(AbilityColor)
            .WithCurrentTimestamp();

        if (desc is not null)
            embed.WithDescription(desc);

        embed
            .AddField("Type",         ability.Type.ToString(),                          inline: true)
            .AddField("Tier",         ability.Tier.ToString(),                          inline: true)
            .AddField("Passive",      ability.IsPassive ? "Yes" : "No",                 inline: true)
            .AddField("Mana Cost",    ability.ManaCost.ToString(),                      inline: true)
            .AddField("Cooldown",     ability.Cooldown == 0 ? "None" : $"{ability.Cooldown}t", inline: true)
            .AddField("Req. Level",   ability.RequiredLevel.ToString(),                 inline: true);

        if (!string.IsNullOrWhiteSpace(ability.BaseDamage))
            embed.AddField("Damage", ability.BaseDamage, inline: true);

        embed.WithFooter(ability.Slug);

        await FollowupAsync(embed: embed.Build());
    }

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────

    private static Color RarityColor(string rarity) => rarity switch
    {
        "Common"    => new Color(0xAAAAAA),
        "Uncommon"  => new Color(0x1EFF00),
        "Rare"      => new Color(0x0070DD),
        "Epic"      => new Color(0xA335EE),
        "Legendary" => new Color(0xFF8000),
        _           => ItemColor,
    };
}
