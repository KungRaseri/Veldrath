using Discord;
using FluentAssertions;
using RealmEngine.Shared.Models;
using Veldrath.Discord.Features;
using Veldrath.Discord.Tests.Infrastructure;

namespace Veldrath.Discord.Tests.Features;

/// <summary>
/// Tests for the <see cref="LoreModule"/> slash commands, driven through the real command bodies with a recorded
/// Discord transport (see <see cref="DiscordInteractionHarness"/>).
/// </summary>
public class LoreModuleTests
{
    private static readonly Color EnemyColor = new(0xAD1414);
    private static readonly Color AbilityColor = new(0x1E5FA3);
    private static readonly Color ClassColor = new(0x2D8A4E);

    private static Enemy FireDrake() => new()
    {
        Name = "Fire Drake",
        Slug = "fire-drake",
        BaseName = "Drake",
        Description = "A wyrm of the ash wastes.",
        Level = 15,
        MaxHealth = 210,
        Type = EnemyType.Dragon,
        Difficulty = EnemyDifficulty.Hard,
        XP = 480,
        GoldReward = 95,
        Strength = 18,
        Dexterity = 12,
        Constitution = 16,
        Intelligence = 9,
        Wisdom = 7,
        Charisma = 5,
    };

    private static Power Fireball() => new()
    {
        Slug = "fireball",
        Name = "Fireball",
        DisplayName = "Fireball",
        Description = "A blazing sphere.",
        Type = PowerType.Spell,
        Tier = 3,
        IsPassive = false,
        ManaCost = 35,
        Cooldown = 2,
        RequiredLevel = 7,
        BaseDamage = "8d6",
        AllowedClasses = ["Mage", "Warlock"],
    };

    private static CharacterClass Warrior() => new()
    {
        Slug = "warrior",
        Name = "warrior",
        DisplayName = "Warrior",
        Description = "A shield of the realm.",
        StartingHealth = 140,
        StartingMana = 20,
        PrimaryAttributes = ["strength", "constitution"],
        ArmorProficiency = ["heavy"],
        WeaponProficiency = ["swords"],
    };

    private static LoreModule Module(
        Enemy[]? enemies = null,
        Power[]? powers = null,
        CharacterClass[]? classes = null) =>
        new(new FakeEnemyRepository(enemies ?? []),
            new FakePowerRepository(powers ?? []),
            new FakeCharacterClassRepository(classes ?? []));

    // ──────────────────────────────────────────────
    //  /enemy
    // ──────────────────────────────────────────────

    [Fact]
    public async Task EnemyAsync_WhenTheNameMatches_RepliesWithTheBestiaryEmbed()
    {
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(Module(enemies: [FireDrake()]));

        await module.EnemyAsync("fire drake");

        var transcript = harness.Transcript;
        transcript.Operations.Should().Equal("DeferAsync", "FollowupAsync");
        transcript.Deferred.Should().BeTrue();
        transcript.DeferEphemeral.Should().BeTrue();
        transcript.Text.Should().BeNull();

        transcript.Embed.Should().NotBeNull();
        var embed = transcript.Embed!;
        embed.Title.Should().Be("🐉 Fire Drake");
        embed.Description.Should().Be("A wyrm of the ash wastes.");
        embed.Color!.Value.Should().Be(EnemyColor);
        embed.Fields.Should().Contain(f => f.Name == "Level" && f.Value == "15");
        embed.Fields.Should().Contain(f => f.Name == "HP" && f.Value == "210");
        embed.Fields.Should().Contain(f => f.Name == "Type" && f.Value == "Dragon");
        embed.Fields.Should().Contain(f => f.Name == "Difficulty" && f.Value == "Hard");
        embed.Fields.Should().Contain(f => f.Name == "XP" && f.Value == "480");
        embed.Fields.Should().Contain(f => f.Name == "Gold" && f.Value == "95");
        embed.Fields.Should().Contain(f => f.Name == "STR / DEX / CON" && f.Value == "18 / 12 / 16");
        embed.Fields.Should().Contain(f => f.Name == "INT / WIS / CHA" && f.Value == "9 / 7 / 5");
        embed.Footer!.Value.Text.Should().Be("fire-drake");
    }

    [Fact]
    public async Task EnemyAsync_WhenOnlyTheSlugMatches_StillFindsTheCreature()
    {
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(Module(enemies:
        [
            new Enemy { Name = "Cinder Wyrm", Slug = "ash-wyrm", BaseName = "Wyrm" },
        ]));

        await module.EnemyAsync("  ash-wyrm  ");

        harness.Transcript.Embed!.Title.Should().Be("🐉 Cinder Wyrm");
    }

    [Fact]
    public async Task EnemyAsync_WhenOnlyTheBaseNameMatches_StillFindsTheCreature()
    {
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(Module(enemies:
        [
            new Enemy { Name = "Grey Fang", Slug = "grey-fang", BaseName = "Wolf" },
        ]));

        await module.EnemyAsync("WOLF");

        harness.Transcript.Embed!.Title.Should().Be("🐉 Grey Fang");
    }

    [Fact]
    public async Task EnemyAsync_WhenNothingMatches_RepliesWithAPlainEphemeralNotice()
    {
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(Module(enemies: [FireDrake()]));

        await module.EnemyAsync("basilisk");

        var transcript = harness.Transcript;
        transcript.Operations.Should().Equal("DeferAsync", "FollowupAsync");
        transcript.Embed.Should().BeNull();
        transcript.Text.Should().Be("No creature matching **basilisk** was found in the bestiary.");
        transcript.Ephemeral.Should().BeTrue();
    }

    [Fact]
    public async Task EnemyAsync_WhenTheDescriptionIsMissing_UsesThePlaceholderText()
    {
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(Module(enemies: [new Enemy { Name = "Thing", Slug = "thing", Description = " " }]));

        await module.EnemyAsync("thing");

        harness.Transcript.Embed!.Description.Should().Be("*No lore recorded.*");
    }

    [Fact]
    public async Task EnemyAsync_WhenTheDescriptionIsLong_TruncatesItToTwoHundredCharacters()
    {
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(Module(enemies:
        [
            new Enemy { Name = "Long", Slug = "long", Description = new string('L', 260) },
        ]));

        await module.EnemyAsync("long");

        var description = harness.Transcript.Embed!.Description!;
        description.Should().HaveLength(201);
        description.Should().EndWith("…");
    }

    // ──────────────────────────────────────────────
    //  /power
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PowerAsync_WhenTheDisplayNameMatches_RepliesWithTheCodexEmbed()
    {
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(Module(powers: [Fireball()]));

        await module.PowerAsync("fire");

        var transcript = harness.Transcript;
        transcript.Operations.Should().Equal("DeferAsync", "FollowupAsync");
        transcript.Deferred.Should().BeTrue();
        transcript.DeferEphemeral.Should().BeTrue();
        transcript.Text.Should().BeNull();

        transcript.Embed.Should().NotBeNull();
        var embed = transcript.Embed!;
        embed.Title.Should().Be("✨ Fireball");
        embed.Description.Should().Be("A blazing sphere.");
        embed.Color!.Value.Should().Be(AbilityColor);
        embed.Fields.Should().Contain(f => f.Name == "Type" && f.Value == "Spell");
        embed.Fields.Should().Contain(f => f.Name == "Tier" && f.Value == "3");
        embed.Fields.Should().Contain(f => f.Name == "Passive" && f.Value == "No");
        embed.Fields.Should().Contain(f => f.Name == "Mana Cost" && f.Value == "35");
        embed.Fields.Should().Contain(f => f.Name == "Cooldown" && f.Value == "2t");
        embed.Fields.Should().Contain(f => f.Name == "Required Lvl" && f.Value == "7");
        embed.Fields.Should().Contain(f => f.Name == "Damage" && f.Value == "8d6");
        embed.Fields.Should().Contain(f => f.Name == "Classes" && f.Value == "Mage, Warlock");
        embed.Footer!.Value.Text.Should().Be("fireball");
    }

    [Fact]
    public async Task PowerAsync_WhenOnlyTheInternalNameMatches_StillFindsTheAbility()
    {
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(Module(powers:
        [
            new Power { Name = "ember-bolt", DisplayName = "Cinder Lance", Slug = "cinder-lance" },
        ]));

        await module.PowerAsync("ember");

        harness.Transcript.Embed!.Title.Should().Be("✨ Cinder Lance");
    }

    [Fact]
    public async Task PowerAsync_WhenOnlyTheSlugMatches_StillFindsTheAbility()
    {
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(Module(powers:
        [
            new Power { Name = "Cinder Lance", DisplayName = "Cinder Lance", Slug = "ember-bolt" },
        ]));

        await module.PowerAsync("ember");

        harness.Transcript.Embed!.Title.Should().Be("✨ Cinder Lance");
    }

    [Fact]
    public async Task PowerAsync_WhenTheDisplayNameIsBlank_FallsBackToTheInternalName()
    {
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(Module(powers:
        [
            new Power { Name = "arcane-missile", DisplayName = " ", Slug = "arcane-missile" },
        ]));

        await module.PowerAsync("arcane");

        harness.Transcript.Embed!.Title.Should().Be("✨ arcane-missile");
    }

    [Fact]
    public async Task PowerAsync_WhenOptionalDetailsAreMissing_OmitsTheDamageAndClassesFields()
    {
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(Module(powers:
        [
            new Power
            {
                Slug = "iron-will",
                Name = "Iron Will",
                DisplayName = "Iron Will",
                Type = PowerType.Passive,
                Tier = 2,
                IsPassive = true,
                RequiredLevel = 4,
                Cooldown = 0,
            },
        ]));

        await module.PowerAsync("iron");

        var embed = harness.Transcript.Embed!;
        embed.Fields.Should().NotContain(f => f.Name == "Damage");
        embed.Fields.Should().NotContain(f => f.Name == "Classes");
        embed.Fields.Should().Contain(f => f.Name == "Passive" && f.Value == "Yes");
        embed.Fields.Should().Contain(f => f.Name == "Cooldown" && f.Value == "None");
    }

    [Fact]
    public async Task PowerAsync_WhenNothingMatches_RepliesWithAPlainEphemeralNotice()
    {
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(Module(powers: [Fireball()]));

        await module.PowerAsync("meteor");

        harness.Transcript.Embed.Should().BeNull();
        harness.Transcript.Text.Should().Be("No ability matching **meteor** was found in the codex.");
        harness.Transcript.Ephemeral.Should().BeTrue();
    }

    [Fact]
    public async Task PowerAsync_WhenTheDescriptionIsMissing_UsesThePlaceholderText()
    {
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(Module(powers:
        [
            new Power { Slug = "blink", Name = "Blink", DisplayName = "Blink", Description = "" },
        ]));

        await module.PowerAsync("blink");

        harness.Transcript.Embed!.Description.Should().Be("*No description recorded.*");
    }

    [Fact]
    public async Task PowerAsync_WhenTheDescriptionIsLong_TruncatesItToTwoHundredCharacters()
    {
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(Module(powers:
        [
            new Power { Slug = "tome", Name = "Tome", DisplayName = "Tome", Description = new string('T', 260) },
        ]));

        await module.PowerAsync("tome");

        var description = harness.Transcript.Embed!.Description!;
        description.Should().HaveLength(201);
        description.Should().EndWith("…");
    }

    // ──────────────────────────────────────────────
    //  /class
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ClassAsync_WhenNoNameIsGiven_ListsTheBaseClasses()
    {
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(Module(classes:
        [
            Warrior(),
            new CharacterClass { Slug = "mage", Name = "mage", DisplayName = "", Description = " " },
            new CharacterClass { Slug = "blademaster", Name = "blademaster", IsSubclass = true, ParentClassId = "warrior" },
        ]));

        await module.ClassAsync(null);

        harness.Transcript.Embed.Should().NotBeNull();
        var embed = harness.Transcript.Embed!;
        embed.Title.Should().Be("📖 Character Classes");
        embed.Color!.Value.Should().Be(ClassColor);
        embed.Description.Should().Be(
            "**Warrior** — A shield of the realm.…\n**mage**");
        embed.Footer!.Value.Text.Should().Be("2 base classes · Use /class <name> for full details");
    }

    [Fact]
    public async Task ClassAsync_WhenTheNameIsWhitespace_ListsTheBaseClasses()
    {
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(Module(classes: [Warrior()]));

        await module.ClassAsync("   ");

        harness.Transcript.Text.Should().BeNull();
        harness.Transcript.Embed!.Title.Should().Be("📖 Character Classes");
        harness.Transcript.Embed!.Footer!.Value.Text.Should().StartWith("1 base classes");
    }

    [Fact]
    public async Task ClassAsync_WhenTheNameMatchesExactly_RepliesWithTheClassEmbed()
    {
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(Module(classes: [Warrior()]));

        await module.ClassAsync("Warrior");

        harness.Transcript.Embed.Should().NotBeNull();
        var embed = harness.Transcript.Embed!;
        embed.Title.Should().Be("🛡️ Warrior");
        embed.Description.Should().Be("A shield of the realm.");
        embed.Color!.Value.Should().Be(ClassColor);
        embed.Fields.Should().Contain(f => f.Name == "Starting HP / MP" && f.Value == "140 / 20");
        embed.Fields.Should().Contain(f => f.Name == "Subclass" && f.Value == "No");
        embed.Fields.Should().Contain(f => f.Name == "Primary Attrs" && f.Value == "strength, constitution");
        embed.Fields.Should().Contain(f => f.Name == "Armor" && f.Value == "heavy");
        embed.Fields.Should().Contain(f => f.Name == "Weapons" && f.Value == "swords");
        embed.Footer!.Value.Text.Should().Be("warrior");
    }

    [Fact]
    public async Task ClassAsync_WhenTheExactNameMisses_FallsBackToAFuzzyMatch()
    {
        var classes = new FakeCharacterClassRepository(Warrior());
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(new LoreModule(
            new FakeEnemyRepository(),
            new FakePowerRepository(),
            classes));

        await module.ClassAsync("warr");

        classes.GetByNameCalls.Should().Be(1);
        harness.Transcript.Embed!.Title.Should().Be("🛡️ Warrior");
    }

    [Fact]
    public async Task ClassAsync_WhenTheClassIsASubclassAndHasNoOptionalDetails_ReportsYesAndOmitsTheProficiencies()
    {
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(Module(classes:
        [
            new CharacterClass
            {
                Slug = "blademaster",
                Name = "blademaster",
                DisplayName = "",
                IsSubclass = true,
                StartingHealth = 120,
                StartingMana = 30,
            },
        ]));

        await module.ClassAsync("blademaster");

        var embed = harness.Transcript.Embed!;
        embed.Title.Should().Be("🛡️ blademaster");
        embed.Description.Should().Be("*No lore recorded.*");
        embed.Fields.Should().Contain(f => f.Name == "Subclass" && f.Value == "Yes");
        embed.Fields.Should().NotContain(f => f.Name == "Primary Attrs" || f.Name == "Armor" || f.Name == "Weapons");
    }

    [Fact]
    public async Task ClassAsync_WhenTheDescriptionIsLong_TruncatesItToTwoHundredAndFiftyCharacters()
    {
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(Module(classes:
        [
            new CharacterClass { Slug = "sage", Name = "sage", Description = new string('S', 300) },
        ]));

        await module.ClassAsync("sage");

        var description = harness.Transcript.Embed!.Description!;
        description.Should().HaveLength(251);
        description.Should().EndWith("…");
    }

    [Fact]
    public async Task ClassAsync_WhenNothingMatches_RepliesWithAPlainEphemeralNotice()
    {
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(Module(classes: [Warrior()]));

        await module.ClassAsync("necromancer");

        harness.Transcript.Embed.Should().BeNull();
        harness.Transcript.Text.Should().Be("No class matching **necromancer** was found.");
        harness.Transcript.Ephemeral.Should().BeTrue();
    }
}
