using Discord;
using FluentAssertions;
using RealmEngine.Shared.Models;
using Veldrath.Discord.Features;
using Xunit;

namespace Veldrath.Discord.Tests.Features;

/// <summary>Tests for <see cref="GenerateModule"/> embed builders.</summary>
public class GenerateModuleTests
{
    // ──────────────────────────────────────────────
    //  BuildItemEmbed
    // ──────────────────────────────────────────────

    [Fact]
    public void BuildItemEmbed_Successful_HasCorrectFields()
    {
        // Arrange
        var item = new Item
        {
            Name = "Iron Longsword",
            Description = "A sturdy iron blade.",
            Type = ItemType.Weapon,
            Rarity = ItemRarity.Uncommon,
            Price = 150,
            WeaponType = "swords",
        };

        // Act
        var embed = GenerateModule.BuildItemEmbed(item, new Color(0x7B2FBE));

        // Assert
        embed.Title.Should().Be("⚒️ Iron Longsword");
        embed.Fields.Should().Contain(f => f.Name == "Rarity" && f.Value == "Uncommon");
        embed.Fields.Should().Contain(f => f.Name == "Price" && f.Value == "150g");
        embed.Fields.Should().Contain(f => f.Name == "Weapon Type" && f.Value == "swords");
        embed.Fields.Should().Contain(f => f.Name == "Type" && f.Value == "Weapon");
    }

    [Fact]
    public void BuildItemEmbed_WithLore_IncludesFooter()
    {
        // Arrange
        var item = new Item
        {
            Name = "Flaming Sword",
            Lore = "Forged in the heart of a dying star.",
            Type = ItemType.Weapon,
            Rarity = ItemRarity.Rare,
            Price = 500,
        };

        // Act
        var embed = GenerateModule.BuildItemEmbed(item, new Color(0x0070DD));

        // Assert
        embed.Footer.Should().NotBeNull();
        embed.Footer.Value.Text.Should().Contain("dying star");
    }

    [Fact]
    public void BuildItemEmbed_WithEffect_IncludesEffectField()
    {
        // Arrange
        var item = new Item
        {
            Name = "Healing Potion",
            Effect = "heal",
            Power = 50,
            Type = ItemType.Consumable,
            Rarity = ItemRarity.Common,
            Price = 25,
        };

        // Act
        var embed = GenerateModule.BuildItemEmbed(item, new Color(0xAAAAAA));

        // Assert
        embed.Fields.Should().Contain(f => f.Name == "Effect" && f.Value == "heal");
        embed.Fields.Should().Contain(f => f.Name == "Power" && f.Value == "50");
    }

    [Fact]
    public void BuildItemEmbed_WithArmorClass_IncludesArmorClassField()
    {
        // Arrange
        var item = new Item
        {
            Name = "Leather Vest",
            ArmorClass = "light",
            Type = ItemType.Chest,
            Rarity = ItemRarity.Common,
            Price = 50,
        };

        // Act
        var embed = GenerateModule.BuildItemEmbed(item, new Color(0xAAAAAA));

        // Assert
        embed.Fields.Should().Contain(f => f.Name == "Armor Class" && f.Value == "light");
    }

    [Fact]
    public void BuildItemEmbed_TruncatesDescription()
    {
        // Arrange
        var item = new Item
        {
            Name = "Longsword",
            Description = new string('A', 250),
            Type = ItemType.Weapon,
            Rarity = ItemRarity.Common,
            Price = 10,
        };

        // Act
        var embed = GenerateModule.BuildItemEmbed(item, new Color(0xAAAAAA));

        // Assert
        embed.Description.Should().NotBeNull();
        embed.Description!.Length.Should().Be(201); // 200 chars + "…" (single U+2026 char)
        embed.Description.Should().EndWith("…");
    }

    // ──────────────────────────────────────────────
    //  BuildEnemyEmbed
    // ──────────────────────────────────────────────

    [Fact]
    public void BuildEnemyEmbed_Successful_HasCorrectFields()
    {
        // Arrange
        var enemy = new Enemy
        {
            Name = "Fire Drake",
            Level = 15,
            MaxHealth = 200,
            Type = EnemyType.Dragon,
            Difficulty = EnemyDifficulty.Hard,
            XP = 450,
            GoldReward = 120,
            BasePhysicalDamage = 25,
            BaseMagicDamage = 15,
            Strength = 18,
            Dexterity = 12,
            Constitution = 16,
            Intelligence = 10,
            Wisdom = 8,
            Charisma = 6,
        };

        // Act
        var embed = GenerateModule.BuildEnemyEmbed(enemy);

        // Assert
        embed.Title.Should().Be("🐉 Fire Drake");
        embed.Fields.Should().Contain(f => f.Name == "Level" && f.Value == "15");
        embed.Fields.Should().Contain(f => f.Name == "HP" && f.Value == "200");
        embed.Fields.Should().Contain(f => f.Name == "Type" && f.Value == "Dragon");
        embed.Fields.Should().Contain(f => f.Name == "Difficulty" && f.Value == "Hard");
        embed.Fields.Should().Contain(f => f.Name == "XP" && f.Value == "450");
        embed.Fields.Should().Contain(f => f.Name == "Gold" && f.Value == "120");
        embed.Fields.Should().Contain(f => f.Name == "Phys. DMG" && f.Value == "25");
        embed.Fields.Should().Contain(f => f.Name == "Magic DMG" && f.Value == "15");
        embed.Fields.Should().Contain(f =>
            f.Name == "STR / DEX / CON" && f.Value == "18 / 12 / 16");
        embed.Fields.Should().Contain(f =>
            f.Name == "INT / WIS / CHA" && f.Value == "10 / 8 / 6");
    }

    [Fact]
    public void BuildEnemyEmbed_TruncatesDescription()
    {
        // Arrange
        var enemy = new Enemy
        {
            Name = "Beast",
            Description = new string('B', 250),
        };

        // Act
        var embed = GenerateModule.BuildEnemyEmbed(enemy);

        // Assert
        embed.Description.Should().NotBeNull();
        embed.Description!.Length.Should().Be(201); // 200 chars + "…" (single U+2026 char)
        embed.Description.Should().EndWith("…");
    }

    // ──────────────────────────────────────────────
    //  BuildNpcEmbed
    // ──────────────────────────────────────────────

    [Fact]
    public void BuildNpcEmbed_Successful_HasCorrectFields()
    {
        // Arrange
        var npc = new NPC
        {
            Name = "Elara",
            DisplayName = "Elara Moonshade",
            Occupation = "Apothecary",
            SocialClass = "merchant",
            Age = 34,
            Dialogue = "Welcome, traveler!",
            BaseGold = "3d10",
        };

        // Act
        var embed = GenerateModule.BuildNpcEmbed(npc);

        // Assert
        embed.Title.Should().Be("🧑 Elara Moonshade");
        embed.Fields.Should().Contain(f => f.Name == "Occupation" && f.Value == "Apothecary");
        embed.Fields.Should().Contain(f => f.Name == "Social Class" && f.Value == "merchant");
        embed.Fields.Should().Contain(f => f.Name == "Age");
        embed.Fields.Should().Contain(f => f.Name == "Gold" && f.Value == "3d10");
    }

    [Fact]
    public void BuildNpcEmbed_UsesDisplayNameWhenAvailable()
    {
        // Arrange
        var npc = new NPC
        {
            Name = "elara",
            DisplayName = "Elara Moonshade",
            Age = 34,
        };

        // Act
        var embed = GenerateModule.BuildNpcEmbed(npc);

        // Assert
        embed.Title.Should().Be("🧑 Elara Moonshade");
    }

    [Fact]
    public void BuildNpcEmbed_FallsBackToNameWhenNoDisplayName()
    {
        // Arrange
        var npc = new NPC
        {
            Name = "Garrick",
            DisplayName = "",
            Age = 45,
        };

        // Act
        var embed = GenerateModule.BuildNpcEmbed(npc);

        // Assert
        embed.Title.Should().Be("🧑 Garrick");
    }

    [Fact]
    public void BuildNpcEmbed_TruncatesDialogue()
    {
        // Arrange
        var npc = new NPC
        {
            Name = "Chatter",
            Dialogue = new string('C', 200),
            Age = 30,
        };

        // Act
        var embed = GenerateModule.BuildNpcEmbed(npc);

        // Assert
        embed.Description.Should().NotBeNull();
        embed.Description!.Length.Should().Be(155); // *" (2) + 150 chars + "…" (1) + "* (2) = 155
        embed.Description.Should().Contain("…");
    }

    // ──────────────────────────────────────────────
    //  BuildAbilityEmbed
    // ──────────────────────────────────────────────

    [Fact]
    public void BuildAbilityEmbed_Successful_HasCorrectFields()
    {
        // Arrange
        var ability = new Power
        {
            Slug = "fireball",
            Name = "Fireball",
            DisplayName = "Fireball",
            Description = "A blazing sphere of fire.",
            Type = PowerType.Spell,
            Tier = 3,
            IsPassive = false,
            ManaCost = 50,
            Cooldown = 3,
            RequiredLevel = 10,
            BaseDamage = "8d6",
        };

        // Act
        var embed = GenerateModule.BuildAbilityEmbed(ability);

        // Assert
        embed.Title.Should().Be("✨ Fireball");
        embed.Fields.Should().Contain(f => f.Name == "Type" && f.Value == "Spell");
        embed.Fields.Should().Contain(f => f.Name == "Tier" && f.Value == "3");
        embed.Fields.Should().Contain(f => f.Name == "Passive" && f.Value == "No");
        embed.Fields.Should().Contain(f => f.Name == "Mana Cost" && f.Value == "50");
        embed.Fields.Should().Contain(f => f.Name == "Cooldown" && f.Value == "3t");
        embed.Fields.Should().Contain(f => f.Name == "Req. Level" && f.Value == "10");
        embed.Fields.Should().Contain(f => f.Name == "Damage" && f.Value == "8d6");
        embed.Footer.Should().NotBeNull();
        embed.Footer.Value.Text.Should().Be("fireball");
    }

    [Fact]
    public void BuildAbilityEmbed_WithNoCooldown_ShowsNone()
    {
        // Arrange
        var ability = new Power
        {
            Name = "Quick Strike",
            Cooldown = 0,
            Type = PowerType.Talent,
            Tier = 1,
            RequiredLevel = 1,
        };

        // Act
        var embed = GenerateModule.BuildAbilityEmbed(ability);

        // Assert
        embed.Fields.Should().Contain(f => f.Name == "Cooldown" && f.Value == "None");
    }

    [Fact]
    public void BuildAbilityEmbed_WhenPassive_ShowsYes()
    {
        // Arrange
        var ability = new Power
        {
            Name = "Iron Will",
            IsPassive = true,
            Type = PowerType.Passive,
            Tier = 2,
            RequiredLevel = 5,
        };

        // Act
        var embed = GenerateModule.BuildAbilityEmbed(ability);

        // Assert
        embed.Fields.Should().Contain(f => f.Name == "Passive" && f.Value == "Yes");
    }

    [Fact]
    public void BuildAbilityEmbed_UsesDisplayNameWhenAvailable()
    {
        // Arrange
        var ability = new Power
        {
            Name = "fireball",
            DisplayName = "Fireball",
            Type = PowerType.Spell,
            Tier = 1,
            RequiredLevel = 1,
        };

        // Act
        var embed = GenerateModule.BuildAbilityEmbed(ability);

        // Assert
        embed.Title.Should().Be("✨ Fireball");
    }

    [Fact]
    public void BuildAbilityEmbed_FallsBackToNameWhenNoDisplayName()
    {
        // Arrange
        var ability = new Power
        {
            Name = "arcane-missile",
            DisplayName = "",
            Type = PowerType.Spell,
            Tier = 1,
            RequiredLevel = 1,
        };

        // Act
        var embed = GenerateModule.BuildAbilityEmbed(ability);

        // Assert
        embed.Title.Should().Be("✨ arcane-missile");
    }
}
