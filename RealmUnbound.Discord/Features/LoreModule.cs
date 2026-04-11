using Discord;
using Discord.Interactions;
using RealmEngine.Shared.Abstractions;

namespace Veldrath.Discord.Features;

/// <summary>Lore lookup commands for enemies, powers, and character classes.</summary>
public sealed class LoreModule(
    IEnemyRepository enemies,
    IPowerRepository powers,
    ICharacterClassRepository classes) : InteractionModuleBase<SocketInteractionContext>
{
    private static readonly Color EnemyColor   = new(0xAD1414);
    private static readonly Color AbilityColor = new(0x1E5FA3);
    private static readonly Color ClassColor   = new(0x2D8A4E);

    // // /enemy
    // [SlashCommand("enemy", "Look up a creature from the realm's bestiary")]
    public async Task EnemyAsync(
        [Summary("name", "Creature name or keyword to search for")] string name)
    {
        await DeferAsync(ephemeral: true);

        var all    = await enemies.GetAllAsync();
        var needle = name.Trim().ToLowerInvariant();
        var match  = all.FirstOrDefault(e =>
            e.Name.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
            e.Slug.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
            e.BaseName.Contains(needle, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            await FollowupAsync($"No creature matching **{name}** was found in the bestiary.", ephemeral: true);
            return;
        }

        var desc = string.IsNullOrWhiteSpace(match.Description)
            ? "*No lore recorded.*"
            : (match.Description.Length > 200 ? match.Description[..200] + "…" : match.Description);

        var embed = new EmbedBuilder()
            .WithTitle($"🐉 {match.Name}")
            .WithDescription(desc)
            .WithColor(EnemyColor)
            .AddField("Level",      match.Level.ToString(),              inline: true)
            .AddField("HP",         match.MaxHealth.ToString(),          inline: true)
            .AddField("Type",       match.Type.ToString(),               inline: true)
            .AddField("Difficulty", match.Difficulty.ToString(),         inline: true)
            .AddField("XP",         match.XP.ToString(),                 inline: true)
            .AddField("Gold",       match.GoldReward.ToString(),         inline: true)
            .AddField("STR / DEX / CON",
                $"{match.Strength} / {match.Dexterity} / {match.Constitution}", inline: true)
            .AddField("INT / WIS / CHA",
                $"{match.Intelligence} / {match.Wisdom} / {match.Charisma}",    inline: true)
            .WithFooter(match.Slug)
            .Build();

        await FollowupAsync(embed: embed);
    }

    // // /power
    // [SlashCommand("power", "Look up a power from the arcane codex")]
    public async Task PowerAsync(
        [Summary("name", "Power name or keyword to search for")] string name)
    {
        await DeferAsync(ephemeral: true);

        var all    = await powers.GetAllAsync();
        var needle = name.Trim().ToLowerInvariant();
        var match  = all.FirstOrDefault(a =>
            a.DisplayName.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
            a.Name.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
            a.Slug.Contains(needle, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            await FollowupAsync($"No ability matching **{name}** was found in the codex.", ephemeral: true);
            return;
        }

        var desc = string.IsNullOrWhiteSpace(match.Description)
            ? "*No description recorded.*"
            : (match.Description.Length > 200 ? match.Description[..200] + "…" : match.Description);

        var embed = new EmbedBuilder()
            .WithTitle($"✨ {(string.IsNullOrWhiteSpace(match.DisplayName) ? match.Name : match.DisplayName)}")
            .WithDescription(desc)
            .WithColor(AbilityColor)
            .AddField("Type",          match.Type.ToString(),                       inline: true)
            .AddField("Tier",          match.Tier.ToString(),                       inline: true)
            .AddField("Passive",       match.IsPassive ? "Yes" : "No",             inline: true)
            .AddField("Mana Cost",     match.ManaCost.ToString(),                   inline: true)
            .AddField("Cooldown",      match.Cooldown == 0 ? "None" : $"{match.Cooldown}t", inline: true)
            .AddField("Required Lvl", match.RequiredLevel.ToString(),               inline: true);

        if (!string.IsNullOrWhiteSpace(match.BaseDamage))
            embed.AddField("Damage", match.BaseDamage, inline: true);

        if (match.AllowedClasses.Count > 0)
            embed.AddField("Classes", string.Join(", ", match.AllowedClasses), inline: false);

        embed.WithFooter(match.Slug);

        await FollowupAsync(embed: embed.Build());
    }

    // // /class
    // [SlashCommand("class", "Browse the character classes of Realm Unbound")]
    public async Task ClassAsync(
        [Summary("name", "Class name to look up (leave blank to list all base classes)")] string? name = null)
    {
        await DeferAsync(ephemeral: true);

        if (string.IsNullOrWhiteSpace(name))
        {
            // List all base classes
            var all   = classes.GetBaseClasses();
            var lines = all.Select(c =>
                $"**{(string.IsNullOrWhiteSpace(c.DisplayName) ? c.Name : c.DisplayName)}**" +
                (string.IsNullOrWhiteSpace(c.Description) ? "" : $" — {c.Description[..Math.Min(c.Description.Length, 60)]}…"));

            var embed = new EmbedBuilder()
                .WithTitle("📖 Character Classes")
                .WithDescription(string.Join("\n", lines))
                .WithColor(ClassColor)
                .WithFooter($"{all.Count} base classes · Use /class <name> for full details")
                .Build();

            await FollowupAsync(embed: embed);
            return;
        }

        var needle = name.Trim();
        var cls    = classes.GetByName(needle);

        if (cls is null)
        {
            // Fuzzy fallback
            cls = classes.GetAll().FirstOrDefault(c =>
                c.Name.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                c.DisplayName.Contains(needle, StringComparison.OrdinalIgnoreCase));
        }

        if (cls is null)
        {
            await FollowupAsync($"No class matching **{name}** was found.", ephemeral: true);
            return;
        }

        var desc = string.IsNullOrWhiteSpace(cls.Description)
            ? "*No lore recorded.*"
            : (cls.Description.Length > 250 ? cls.Description[..250] + "…" : cls.Description);

        var embed2 = new EmbedBuilder()
            .WithTitle($"🛡️ {(string.IsNullOrWhiteSpace(cls.DisplayName) ? cls.Name : cls.DisplayName)}")
            .WithDescription(desc)
            .WithColor(ClassColor)
            .AddField("Starting HP / MP",
                $"{cls.StartingHealth} / {cls.StartingMana}", inline: true)
            .AddField("Subclass",
                cls.IsSubclass ? "Yes" : "No", inline: true);

        if (cls.PrimaryAttributes.Count > 0)
            embed2.AddField("Primary Attrs", string.Join(", ", cls.PrimaryAttributes), inline: false);

        if (cls.ArmorProficiency.Count > 0)
            embed2.AddField("Armor", string.Join(", ", cls.ArmorProficiency), inline: true);

        if (cls.WeaponProficiency.Count > 0)
            embed2.AddField("Weapons", string.Join(", ", cls.WeaponProficiency), inline: true);

        embed2.WithFooter(cls.Slug);

        await FollowupAsync(embed: embed2.Build());
    }
}
