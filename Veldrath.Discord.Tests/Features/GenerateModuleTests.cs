using Discord;
using Discord.WebSocket;
using FluentAssertions;
using MediatR;
using NSubstitute;
using RealmEngine.Core.Features.ItemGeneration.Commands;
using RealmEngine.Shared.Models;
using Veldrath.Discord.Features;
using Xunit;

namespace Veldrath.Discord.Tests.Features;

/// <summary>Tests for <see cref="GenerateModule"/> slash commands.</summary>
public class GenerateModuleTests
{
    private static readonly Color ItemColor    = new(0x7B2FBE);
    private static readonly Color EnemyColor   = new(0xAD1414);
    private static readonly Color NpcColor     = new(0xC47A00);
    private static readonly Color AbilityColor = new(0x1E5FA3);

    /// <summary>
    /// Creates a <see cref="GenerateModule"/> wired to a mocked <see cref="IMediator"/>
    /// and a mocked interaction context.
    /// </summary>
    private static (GenerateModule Module, IMediator Mediator, SocketInteraction Interaction) CreateModule()
    {
        var mediator = Substitute.For<IMediator>();

        var interaction = Substitute.For<SocketInteraction>();
        interaction.DeferAsync(Arg.Any<bool>(), Arg.Any<RequestOptions?>())
            .Returns(Task.CompletedTask);
        interaction.FollowupAsync(
                Arg.Any<string?>(),
                Arg.Any<Embed[]>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<AllowedMentions?>(),
                Arg.Any<RequestOptions?>(),
                Arg.Any<MessageComponent?>(),
                Arg.Any<Embed?>())
            .Returns(Task.CompletedTask);

        // Stub abstract members so the proxy doesn't throw
        interaction.User.Returns(Substitute.For<SocketUser>());
        interaction.Channel.Returns(Substitute.For<ISocketMessageChannel>());
        interaction.Data.Returns(Substitute.For<SocketInteractionData>());
        interaction.MessageAuthor.Returns(Substitute.For<SocketUser>());

        var client = Substitute.For<DiscordSocketClient>();
        var context = new SocketInteractionContext(client, interaction);

        var module = new GenerateModule(mediator);
        module.SetContext(context);

        return (module, mediator, interaction);
    }

    // ──────────────────────────────────────────────
    //  /generate item
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ItemAsync_WhenSuccessful_SendsItemEmbed()
    {
        // Arrange
        var (module, mediator, interaction) = CreateModule();

        var item = new Item
        {
            Name = "Iron Longsword",
            Description = "A sturdy iron blade.",
            Type = ItemType.Weapon,
            Rarity = ItemRarity.Uncommon,
            Price = 150,
            WeaponType = "swords",
            Power = 12,
            Lore = "Forged in the fires of Crestfall.",
        };

        mediator.Send(Arg.Any<GenerateItemCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GenerateItemResult { Success = true, Item = item });

        // Act
        await module.ItemAsync("weapons/swords");

        // Assert
        await interaction.Received(1).FollowupAsync(
            Arg.Any<string?>(),
            Arg.Any<Embed[]>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<AllowedMentions?>(),
            Arg.Any<RequestOptions?>(),
            Arg.Any<MessageComponent?>(),
            Arg.Is<Embed?>(e => e != null
                && e.Title!.Contains("Iron Longsword")
                && e.Fields.Any(f => f.Name == "Rarity" && f.Value == "Uncommon")
                && e.Fields.Any(f => f.Name == "Price" && f.Value == "150g")
                && e.Fields.Any(f => f.Name == "Weapon Type" && f.Value == "swords")));
    }

    [Fact]
    public async Task ItemAsync_WhenFailed_SendsErrorMessage()
    {
        // Arrange
        var (module, mediator, interaction) = CreateModule();

        mediator.Send(Arg.Any<GenerateItemCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GenerateItemResult
            {
                Success = false,
                ErrorMessage = "Category not found",
            });

        // Act
        await module.ItemAsync("weapons/swords");

        // Assert
        await interaction.Received(1).FollowupAsync(
            Arg.Is<string?>(s => s != null && s.Contains("forge ran cold") && s.Contains("weapons/swords")),
            Arg.Any<Embed[]>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<AllowedMentions?>(),
            Arg.Any<RequestOptions?>(),
            Arg.Any<MessageComponent?>(),
            Arg.Any<Embed?>());
    }

    [Fact]
    public async Task ItemAsync_WhenItemIsNull_SendsErrorMessage()
    {
        // Arrange
        var (module, mediator, interaction) = CreateModule();

        mediator.Send(Arg.Any<GenerateItemCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GenerateItemResult { Success = true, Item = null });

        // Act
        await module.ItemAsync("weapons/swords");

        // Assert
        await interaction.Received(1).FollowupAsync(
            Arg.Is<string?>(s => s != null && s.Contains("forge ran cold")),
            Arg.Any<Embed[]>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<AllowedMentions?>(),
            Arg.Any<RequestOptions?>(),
            Arg.Any<MessageComponent?>(),
            Arg.Any<Embed?>());
    }

    // ──────────────────────────────────────────────
    //  /generate enemy
    // ──────────────────────────────────────────────

    [Fact]
    public async Task EnemyAsync_WhenSuccessful_SendsEnemyEmbed()
    {
        // Arrange
        var (module, mediator, interaction) = CreateModule();

        var enemy = new Enemy
        {
            Name = "Fire Drake",
            Description = "A blistering drake from the volcanic pits.",
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

        mediator.Send(Arg.Any<GenerateEnemyCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GenerateEnemyResult { Success = true, Enemy = enemy });

        // Act
        await module.EnemyAsync("dragons");

        // Assert
        await interaction.Received(1).FollowupAsync(
            Arg.Any<string?>(),
            Arg.Any<Embed[]>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<AllowedMentions?>(),
            Arg.Any<RequestOptions?>(),
            Arg.Any<MessageComponent?>(),
            Arg.Is<Embed?>(e => e != null
                && e.Title!.Contains("Fire Drake")
                && e.Fields.Any(f => f.Name == "Level" && f.Value == "15")
                && e.Fields.Any(f => f.Name == "HP" && f.Value == "200")
                && e.Fields.Any(f => f.Name == "XP" && f.Value == "450")
                && e.Fields.Any(f => f.Name == "Gold" && f.Value == "120")));
    }

    [Fact]
    public async Task EnemyAsync_WhenFailed_SendsErrorMessage()
    {
        // Arrange
        var (module, mediator, interaction) = CreateModule();

        mediator.Send(Arg.Any<GenerateEnemyCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GenerateEnemyResult
            {
                Success = false,
                ErrorMessage = "No creatures found",
            });

        // Act
        await module.EnemyAsync("dragons");

        // Assert
        await interaction.Received(1).FollowupAsync(
            Arg.Is<string?>(s => s != null && s.Contains("void returned nothing") && s.Contains("dragons")),
            Arg.Any<Embed[]>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<AllowedMentions?>(),
            Arg.Any<RequestOptions?>(),
            Arg.Any<MessageComponent?>(),
            Arg.Any<Embed?>());
    }

    [Fact]
    public async Task EnemyAsync_WhenEnemyIsNull_SendsErrorMessage()
    {
        // Arrange
        var (module, mediator, interaction) = CreateModule();

        mediator.Send(Arg.Any<GenerateEnemyCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GenerateEnemyResult { Success = true, Enemy = null });

        // Act
        await module.EnemyAsync("dragons");

        // Assert
        await interaction.Received(1).FollowupAsync(
            Arg.Is<string?>(s => s != null && s.Contains("void returned nothing")),
            Arg.Any<Embed[]>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<AllowedMentions?>(),
            Arg.Any<RequestOptions?>(),
            Arg.Any<MessageComponent?>(),
            Arg.Any<Embed?>());
    }

    // ──────────────────────────────────────────────
    //  /generate npc
    // ──────────────────────────────────────────────

    [Fact]
    public async Task NpcAsync_WhenSuccessful_SendsNpcEmbed()
    {
        // Arrange
        var (module, mediator, interaction) = CreateModule();

        var npc = new NPC
        {
            Name = "Elara",
            DisplayName = "Elara Moonshade",
            Occupation = "Apothecary",
            SocialClass = "merchant",
            Age = 34,
            Dialogue = "Welcome, traveler! I've got just the thing for what ails you.",
            BaseGold = "3d10",
        };

        mediator.Send(Arg.Any<GenerateNPCCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GenerateNPCResult { Success = true, NPC = npc });

        // Act
        await module.NpcAsync("merchants");

        // Assert
        await interaction.Received(1).FollowupAsync(
            Arg.Any<string?>(),
            Arg.Any<Embed[]>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<AllowedMentions?>(),
            Arg.Any<RequestOptions?>(),
            Arg.Any<MessageComponent?>(),
            Arg.Is<Embed?>(e => e != null
                && e.Title!.Contains("Elara Moonshade")
                && e.Fields.Any(f => f.Name == "Occupation" && f.Value == "Apothecary")
                && e.Fields.Any(f => f.Name == "Social Class" && f.Value == "merchant")
                && e.Fields.Any(f => f.Name == "Age")
                && e.Fields.Any(f => f.Name == "Gold" && f.Value == "3d10")));
    }

    [Fact]
    public async Task NpcAsync_WhenFailed_SendsErrorMessage()
    {
        // Arrange
        var (module, mediator, interaction) = CreateModule();

        mediator.Send(Arg.Any<GenerateNPCCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GenerateNPCResult
            {
                Success = false,
                ErrorMessage = "No NPCs available",
            });

        // Act
        await module.NpcAsync("merchants");

        // Assert
        await interaction.Received(1).FollowupAsync(
            Arg.Is<string?>(s => s != null && s.Contains("No one answered") && s.Contains("merchants")),
            Arg.Any<Embed[]>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<AllowedMentions?>(),
            Arg.Any<RequestOptions?>(),
            Arg.Any<MessageComponent?>(),
            Arg.Any<Embed?>());
    }

    [Fact]
    public async Task NpcAsync_WhenNpcIsNull_SendsErrorMessage()
    {
        // Arrange
        var (module, mediator, interaction) = CreateModule();

        mediator.Send(Arg.Any<GenerateNPCCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GenerateNPCResult { Success = true, NPC = null });

        // Act
        await module.NpcAsync("merchants");

        // Assert
        await interaction.Received(1).FollowupAsync(
            Arg.Is<string?>(s => s != null && s.Contains("No one answered")),
            Arg.Any<Embed[]>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<AllowedMentions?>(),
            Arg.Any<RequestOptions?>(),
            Arg.Any<MessageComponent?>(),
            Arg.Any<Embed?>());
    }

    // ──────────────────────────────────────────────
    //  /generate ability
    // ──────────────────────────────────────────────

    [Fact]
    public async Task AbilityAsync_WhenSuccessful_SendsAbilityEmbed()
    {
        // Arrange
        var (module, mediator, interaction) = CreateModule();

        var ability = new Power
        {
            Slug = "fireball",
            Name = "Fireball",
            DisplayName = "Fireball",
            Description = "A blazing sphere of fire that engulfs your enemies.",
            Type = PowerType.Spell,
            Tier = 3,
            IsPassive = false,
            ManaCost = 50,
            Cooldown = 3,
            RequiredLevel = 10,
            BaseDamage = "8d6",
        };

        mediator.Send(Arg.Any<GeneratePowerCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GeneratePowerResult
            {
                Success = true,
                Powers = [ability],
            });

        // Act
        await module.AbilityAsync("active/offensive");

        // Assert
        await interaction.Received(1).FollowupAsync(
            Arg.Any<string?>(),
            Arg.Any<Embed[]>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<AllowedMentions?>(),
            Arg.Any<RequestOptions?>(),
            Arg.Any<MessageComponent?>(),
            Arg.Is<Embed?>(e => e != null
                && e.Title!.Contains("Fireball")
                && e.Fields.Any(f => f.Name == "Type" && f.Value == "Spell")
                && e.Fields.Any(f => f.Name == "Tier" && f.Value == "3")
                && e.Fields.Any(f => f.Name == "Passive" && f.Value == "No")
                && e.Fields.Any(f => f.Name == "Mana Cost" && f.Value == "50")
                && e.Fields.Any(f => f.Name == "Cooldown" && f.Value == "3t")
                && e.Fields.Any(f => f.Name == "Req. Level" && f.Value == "10")
                && e.Fields.Any(f => f.Name == "Damage" && f.Value == "8d6")));
    }

    [Fact]
    public async Task AbilityAsync_WhenFailed_SendsErrorMessage()
    {
        // Arrange
        var (module, mediator, interaction) = CreateModule();

        mediator.Send(Arg.Any<GeneratePowerCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GeneratePowerResult
            {
                Success = false,
                ErrorMessage = "Arcane repository depleted",
            });

        // Act
        await module.AbilityAsync("active/offensive");

        // Assert
        await interaction.Received(1).FollowupAsync(
            Arg.Is<string?>(s => s != null && s.Contains("arcane scroll was blank") && s.Contains("active/offensive")),
            Arg.Any<Embed[]>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<AllowedMentions?>(),
            Arg.Any<RequestOptions?>(),
            Arg.Any<MessageComponent?>(),
            Arg.Any<Embed?>());
    }

    [Fact]
    public async Task AbilityAsync_WhenPowersIsNull_SendsErrorMessage()
    {
        // Arrange
        var (module, mediator, interaction) = CreateModule();

        mediator.Send(Arg.Any<GeneratePowerCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GeneratePowerResult { Success = true, Powers = null });

        // Act
        await module.AbilityAsync("active/offensive");

        // Assert
        await interaction.Received(1).FollowupAsync(
            Arg.Is<string?>(s => s != null && s.Contains("arcane scroll was blank")),
            Arg.Any<Embed[]>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<AllowedMentions?>(),
            Arg.Any<RequestOptions?>(),
            Arg.Any<MessageComponent?>(),
            Arg.Any<Embed?>());
    }

    [Fact]
    public async Task AbilityAsync_WhenPowersEmpty_SendsErrorMessage()
    {
        // Arrange
        var (module, mediator, interaction) = CreateModule();

        mediator.Send(Arg.Any<GeneratePowerCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GeneratePowerResult { Success = true, Powers = [] });

        // Act
        await module.AbilityAsync("active/offensive");

        // Assert
        await interaction.Received(1).FollowupAsync(
            Arg.Is<string?>(s => s != null && s.Contains("arcane scroll was blank")),
            Arg.Any<Embed[]>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<AllowedMentions?>(),
            Arg.Any<RequestOptions?>(),
            Arg.Any<MessageComponent?>(),
            Arg.Any<Embed?>());
    }

    [Fact]
    public async Task AbilityAsync_SendsCorrectCommand()
    {
        // Arrange
        var (module, mediator, interaction) = CreateModule();

        mediator.Send(Arg.Any<GeneratePowerCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GeneratePowerResult { Success = true, Powers = [new Power()] });

        // Act
        await module.AbilityAsync("active/offensive");

        // Assert
        await mediator.Received(1).Send(
            Arg.Is<GeneratePowerCommand>(c =>
                c.Category == "active" && c.Subcategory == "offensive"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AbilityAsync_SendsCommandWithCorrectParts()
    {
        // Arrange
        var (module, mediator, interaction) = CreateModule();

        mediator.Send(Arg.Any<GeneratePowerCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GeneratePowerResult { Success = true, Powers = [new Power()] });

        // Act
        await module.AbilityAsync("passive");

        // Assert — when no '/' is present, Subcategory is empty
        await mediator.Received(1).Send(
            Arg.Is<GeneratePowerCommand>(c =>
                c.Category == "passive" && c.Subcategory == string.Empty),
            Arg.Any<CancellationToken>());
    }
}
