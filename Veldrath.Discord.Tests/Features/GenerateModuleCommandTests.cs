using Discord;
using FluentAssertions;
using MediatR;
using NSubstitute;
using RealmEngine.Core.Features.ItemGeneration.Commands;
using RealmEngine.Shared.Models;
using Veldrath.Discord.Features;
using Veldrath.Discord.Tests.Infrastructure;

namespace Veldrath.Discord.Tests.Features;

/// <summary>
/// Tests for the <see cref="GenerateModule"/> slash commands, driven through the real command bodies with a
/// substituted mediator and a recorded Discord transport (see <see cref="DiscordInteractionHarness"/>).
/// </summary>
public class GenerateModuleCommandTests
{
    private static Item Sword(ItemRarity rarity = ItemRarity.Uncommon) => new()
    {
        Name = "Cinder Brand",
        Slug = "cinder-brand",
        Description = "A blade that remembers the forge.",
        Type = ItemType.Weapon,
        Rarity = rarity,
        Price = 240,
        WeaponType = "swords",
        Power = 12,
        Effect = "burn",
        Lore = "Quenched in dragonfire.",
    };

    // ──────────────────────────────────────────────
    //  /generate item
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ItemAsync_WhenTheForgeProducesAnItem_RepliesWithTheItemEmbed()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GenerateItemCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GenerateItemResult { Success = true, Item = Sword() });
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(new GenerateModule(mediator));

        await module.ItemAsync("weapons/swords");

        var transcript = harness.Transcript;
        transcript.Operations.Should().Equal("DeferAsync", "FollowupAsync");
        transcript.Deferred.Should().BeTrue();
        transcript.Text.Should().BeNull();

        transcript.Embed.Should().NotBeNull();
        var embed = transcript.Embed!;
        embed.Title.Should().Be("⚒️ Cinder Brand");
        embed.Description.Should().Be("A blade that remembers the forge.");
        embed.Color!.Value.Should().Be(new Color(0x1EFF00), "uncommon items are highlighted in green");
        embed.Fields.Should().Contain(f => f.Name == "Type" && f.Value == "Weapon");
        embed.Fields.Should().Contain(f => f.Name == "Rarity" && f.Value == "Uncommon");
        embed.Fields.Should().Contain(f => f.Name == "Price" && f.Value == "240g");
        embed.Fields.Should().Contain(f => f.Name == "Weapon Type" && f.Value == "swords");
        embed.Fields.Should().Contain(f => f.Name == "Power" && f.Value == "12");
        embed.Fields.Should().Contain(f => f.Name == "Effect" && f.Value == "burn");
        embed.Footer!.Value.Text.Should().Contain("dragonfire");

        await mediator.Received(1).Send(
            Arg.Is<GenerateItemCommand>(command => command.Category == "weapons/swords"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItemAsync_WhenTheForgeFails_RepliesWithTheReason()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GenerateItemCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GenerateItemResult { Success = false, ErrorMessage = "catalog is empty" });
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(new GenerateModule(mediator));

        await module.ItemAsync("gems");

        harness.Transcript.Embed.Should().BeNull();
        harness.Transcript.Text.Should().Be(
            "⚠️ The forge ran cold. Could not generate an item from **gems**.\n*catalog is empty*");
    }

    [Fact]
    public async Task ItemAsync_WhenTheForgeFailsWithoutAReason_OmitsTheReasonLine()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GenerateItemCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GenerateItemResult { Success = false });
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(new GenerateModule(mediator));

        await module.ItemAsync();

        harness.Transcript.Text.Should().Be("⚠️ The forge ran cold. Could not generate an item from **weapons/swords**.");
    }

    [Theory]
    [InlineData(ItemRarity.Common, 0xAA, 0xAA, 0xAA)]
    [InlineData(ItemRarity.Uncommon, 0x1E, 0xFF, 0x00)]
    [InlineData(ItemRarity.Rare, 0x00, 0x70, 0xDD)]
    [InlineData(ItemRarity.Epic, 0xA3, 0x35, 0xEE)]
    [InlineData(ItemRarity.Legendary, 0xFF, 0x80, 0x00)]
    public async Task ItemAsync_ColoursTheEmbedByRarity(ItemRarity rarity, int red, int green, int blue)
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GenerateItemCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GenerateItemResult { Success = true, Item = Sword(rarity) });
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(new GenerateModule(mediator));

        await module.ItemAsync();

        var colour = harness.Transcript.Embed!.Color!.Value;
        colour.R.Should().Be((byte)red);
        colour.G.Should().Be((byte)green);
        colour.B.Should().Be((byte)blue);
    }

    // ──────────────────────────────────────────────
    //  /generate enemy
    // ──────────────────────────────────────────────

    [Fact]
    public async Task EnemyAsync_WhenTheVoidAnswers_RepliesWithTheEnemyEmbed()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GenerateEnemyCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GenerateEnemyResult
            {
                Success = true,
                Enemy = new Enemy
                {
                    Name = "Ash Wyrm",
                    Level = 40,
                    MaxHealth = 900,
                    Type = EnemyType.Dragon,
                    Difficulty = EnemyDifficulty.Elite,
                    XP = 1200,
                    GoldReward = 300,
                    BasePhysicalDamage = 65,
                    BaseMagicDamage = 40,
                    Strength = 30,
                    Dexterity = 18,
                    Constitution = 28,
                    Intelligence = 14,
                    Wisdom = 11,
                    Charisma = 9,
                },
            });
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(new GenerateModule(mediator));

        await module.EnemyAsync("dragons", level: 40);

        var transcript = harness.Transcript;
        transcript.Operations.Should().Equal("DeferAsync", "FollowupAsync");
        transcript.Embed.Should().NotBeNull();
        var embed = transcript.Embed!;
        embed.Title.Should().Be("🐉 Ash Wyrm");
        embed.Color!.Value.Should().Be(new Color(0xAD1414));
        embed.Fields.Should().Contain(f => f.Name == "Level" && f.Value == "40");
        embed.Fields.Should().Contain(f => f.Name == "HP" && f.Value == "900");
        embed.Fields.Should().Contain(f => f.Name == "Type" && f.Value == "Dragon");
        embed.Fields.Should().Contain(f => f.Name == "Phys. DMG" && f.Value == "65");
        embed.Fields.Should().Contain(f => f.Name == "Magic DMG" && f.Value == "40");
        embed.Fields.Should().Contain(f => f.Name == "STR / DEX / CON" && f.Value == "30 / 18 / 28");
        embed.Fields.Should().Contain(f => f.Name == "INT / WIS / CHA" && f.Value == "14 / 11 / 9");

        await mediator.Received(1).Send(
            Arg.Is<GenerateEnemyCommand>(command => command.Category == "dragons" && command.Level == 40),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnemyAsync_WhenTheVoidReturnsNothing_RepliesWithTheReason()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GenerateEnemyCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GenerateEnemyResult { Success = true, Enemy = null });
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(new GenerateModule(mediator));

        await module.EnemyAsync("undead");

        harness.Transcript.Embed.Should().BeNull();
        harness.Transcript.Text.Should().Be("⚠️ The void returned nothing. Could not conjure a **undead**.");
    }

    // ──────────────────────────────────────────────
    //  /generate npc
    // ──────────────────────────────────────────────

    [Fact]
    public async Task NpcAsync_WhenSomeoneAnswers_RepliesWithTheNpcEmbed()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GenerateNPCCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GenerateNPCResult
            {
                Success = true,
                NPC = new NPC
                {
                    Name = "elara",
                    DisplayName = "Elara Moonshade",
                    Occupation = "Apothecary",
                    SocialClass = "merchant",
                    Age = 34,
                    Dialogue = "Welcome, traveler!",
                },
            });
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(new GenerateModule(mediator));

        await module.NpcAsync("merchants");

        var transcript = harness.Transcript;
        transcript.Operations.Should().Equal("DeferAsync", "FollowupAsync");
        transcript.Embed.Should().NotBeNull();
        var embed = transcript.Embed!;
        embed.Title.Should().Be("🧑 Elara Moonshade");
        embed.Description.Should().Be("*\"Welcome, traveler!\"*");
        embed.Color!.Value.Should().Be(new Color(0xC47A00));
        embed.Fields.Should().Contain(f => f.Name == "Occupation" && f.Value == "Apothecary");
        embed.Fields.Should().Contain(f => f.Name == "Social Class" && f.Value == "merchant");
        embed.Fields.Should().Contain(f => f.Name == "Age" && f.Value == "34");

        await mediator.Received(1).Send(
            Arg.Is<GenerateNPCCommand>(command => command.Category == "merchants"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NpcAsync_WhenNobodyAnswers_RepliesWithTheReason()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GenerateNPCCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GenerateNPCResult { Success = false, NPC = null, ErrorMessage = "no npc catalog" });
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(new GenerateModule(mediator));

        await module.NpcAsync("noble");

        harness.Transcript.Text.Should().Be(
            "⚠️ No one answered the call. Could not generate a **noble** NPC.\n*no npc catalog*");
    }

    // ──────────────────────────────────────────────
    //  /generate ability
    // ──────────────────────────────────────────────

    [Fact]
    public async Task AbilityAsync_WhenTheScrollRevealsAPower_RepliesWithTheAbilityEmbed()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GeneratePowerCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GeneratePowerResult
            {
                Success = true,
                Powers =
                [
                    new Power
                    {
                        Slug = "cinder-lance",
                        Name = "Cinder Lance",
                        DisplayName = "Cinder Lance",
                        Description = "A spear of living flame.",
                        Type = PowerType.Spell,
                        Tier = 4,
                        IsPassive = false,
                        ManaCost = 45,
                        Cooldown = 3,
                        RequiredLevel = 12,
                        BaseDamage = "10d8",
                    },
                ],
            });
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(new GenerateModule(mediator));

        await module.AbilityAsync("active/offensive");

        var transcript = harness.Transcript;
        transcript.Operations.Should().Equal("DeferAsync", "FollowupAsync");
        transcript.Embed.Should().NotBeNull();
        var embed = transcript.Embed!;
        embed.Title.Should().Be("✨ Cinder Lance");
        embed.Description.Should().Be("A spear of living flame.");
        embed.Color!.Value.Should().Be(new Color(0x1E5FA3));
        embed.Fields.Should().Contain(f => f.Name == "Type" && f.Value == "Spell");
        embed.Fields.Should().Contain(f => f.Name == "Tier" && f.Value == "4");
        embed.Fields.Should().Contain(f => f.Name == "Mana Cost" && f.Value == "45");
        embed.Fields.Should().Contain(f => f.Name == "Cooldown" && f.Value == "3t");
        embed.Fields.Should().Contain(f => f.Name == "Damage" && f.Value == "10d8");
        embed.Footer!.Value.Text.Should().Be("cinder-lance");

        await mediator.Received(1).Send(
            Arg.Is<GeneratePowerCommand>(command =>
                command.Category == "active" && command.Subcategory == "offensive" && command.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AbilityAsync_WhenTheChoiceHasNoSubcategory_SendsAnEmptySubcategory()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GeneratePowerCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GeneratePowerResult { Success = true, Powers = [new Power { Name = "Iron Will" }] });
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(new GenerateModule(mediator));

        await module.AbilityAsync("passive");

        await mediator.Received(1).Send(
            Arg.Is<GeneratePowerCommand>(command =>
                command.Category == "passive" && command.Subcategory == string.Empty),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AbilityAsync_WhenNoPowerIsGenerated_RepliesWithTheReason()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GeneratePowerCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GeneratePowerResult { Success = false, ErrorMessage = "no powers in catalog" });
        var harness = DiscordInteractionHarness.Create();
        var module = harness.Attach(new GenerateModule(mediator));

        await module.AbilityAsync("ultimate");

        harness.Transcript.Embed.Should().BeNull();
        harness.Transcript.Text.Should().Be(
            "⚠️ The arcane scroll was blank. Could not generate a **ultimate** ability.\n*no powers in catalog*");
    }
}
