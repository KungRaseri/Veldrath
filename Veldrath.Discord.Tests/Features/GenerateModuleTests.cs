using System.Reflection;
using Discord;
using Discord.Interactions;
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
    /// <summary>
    /// Creates a substitute <see cref="SocketInteraction"/> with abstract members stubbed
    /// and <see cref="IDiscordInteraction.DeferAsync"/> / <see cref="IDiscordInteraction.FollowupAsync"/>
    /// set up as no-ops.
    /// </summary>
    private static SocketInteraction CreateMockInteraction()
    {
        var interaction = Substitute.For<SocketInteraction>();
        interaction.DeferAsync(Arg.Any<bool>(), Arg.Any<RequestOptions?>())
            .Returns(Task.CompletedTask);
        interaction.FollowupAsync(
                Arg.Any<string?>(),
                Arg.Any<Embed[]>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<AllowedMentions?>(),
                Arg.Any<MessageComponent?>(),
                Arg.Any<Embed?>(),
                Arg.Any<RequestOptions?>(),
                Arg.Any<PollProperties?>())
            .Returns(Task.CompletedTask);

        // Stub abstract members so the proxy doesn't throw
        interaction.User.Returns(Substitute.For<SocketUser>());
        interaction.Channel.Returns(Substitute.For<ISocketMessageChannel>());
        interaction.Data.Returns(Substitute.For<IDiscordInteractionData>());

        return interaction;
    }

    /// <summary>
    /// Uses reflection to set the <see cref="InteractionModuleBase{T}.Context"/>
    /// property, which has an <c>internal</c> setter.
    /// </summary>
    private static void SetModuleContext<TModule>(TModule module, SocketInteractionContext context)
        where TModule : InteractionModuleBase<SocketInteractionContext>
    {
        var prop = typeof(InteractionModuleBase<SocketInteractionContext>)
            .GetProperty("Context", BindingFlags.Public | BindingFlags.Instance)!;
        var setter = prop.GetSetMethod(true); // non-public
        setter!.Invoke(module, [context]);
    }

    /// <summary>
    /// Sets up the module with a mocked <see cref="IMediator"/> and a mocked interaction.
    /// Returns the tuple (module, mediator, interaction) so tests can arrange mediator results
    /// and assert on interaction calls.
    /// </summary>
    private static (GenerateModule Module, IMediator Mediator, SocketInteraction Interaction) ArrangeModule()
    {
        var mediator = Substitute.For<IMediator>();
        var interaction = CreateMockInteraction();

        var client = Substitute.For<DiscordSocketClient>();
        var context = new SocketInteractionContext(client, interaction);
        var module = new GenerateModule(mediator);
        SetModuleContext(module, context);

        return (module, mediator, interaction);
    }

    // ──────────────────────────────────────────────
    //  /generate item
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ItemAsync_WhenSuccessful_SendsItemEmbed()
    {
        // Arrange
        var (module, mediator, interaction) = ArrangeModule();

        var item = new Item
        {
            Name = "Iron Longsword",
            Description = "A sturdy iron blade.",
            Type = ItemType.Weapon,
            Rarity = ItemRarity.Uncommon,
            Price = 150,
            WeaponType = "swords",
        };

        mediator.Send(Arg.Any<GenerateItemCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GenerateItemResult { Success = true, Item = item });

        Embed? captured = null;
        interaction.When(x => x.FollowupAsync(
                Arg.Any<string?>(),
                Arg.Any<Embed[]>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<AllowedMentions?>(),
                Arg.Any<MessageComponent?>(),
                Arg.Do<Embed?>(e => captured = e),
                Arg.Any<RequestOptions?>(),
                Arg.Any<PollProperties?>()))
            .DoNotCallBase();

        // Act
        await module.ItemAsync("weapons/swords");

        // Assert
        captured.Should().NotBeNull();
        captured!.Title.Should().Contain("Iron Longsword");
        captured.Fields.Should().Contain(f => f.Name == "Rarity" && f.Value == "Uncommon");
        captured.Fields.Should().Contain(f => f.Name == "Price" && f.Value == "150g");
        captured.Fields.Should().Contain(f => f.Name == "Weapon Type" && f.Value == "swords");
    }

    [Fact]
    public async Task ItemAsync_WhenFailed_SendsErrorMessage()
    {
        // Arrange
        var (module, mediator, interaction) = ArrangeModule();

        mediator.Send(Arg.Any<GenerateItemCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GenerateItemResult
            {
                Success = false,
                ErrorMessage = "Category not found",
            });

        string? capturedText = null;
        interaction.When(x => x.FollowupAsync(
                Arg.Do<string?>(t => capturedText = t),
                Arg.Any<Embed[]>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<AllowedMentions?>(),
                Arg.Any<MessageComponent?>(),
                Arg.Any<Embed?>(),
                Arg.Any<RequestOptions?>(),
                Arg.Any<PollProperties?>()))
            .DoNotCallBase();

        // Act
        await module.ItemAsync("weapons/swords");

        // Assert
        capturedText.Should().NotBeNull();
        capturedText!.Should().Contain("forge ran cold");
        capturedText.Should().Contain("weapons/swords");
    }

    [Fact]
    public async Task ItemAsync_WhenItemIsNull_SendsErrorMessage()
    {
        // Arrange
        var (module, mediator, interaction) = ArrangeModule();

        mediator.Send(Arg.Any<GenerateItemCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GenerateItemResult { Success = true, Item = null });

        string? capturedText = null;
        interaction.When(x => x.FollowupAsync(
                Arg.Do<string?>(t => capturedText = t),
                Arg.Any<Embed[]>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<AllowedMentions?>(),
                Arg.Any<MessageComponent?>(),
                Arg.Any<Embed?>(),
                Arg.Any<RequestOptions?>(),
                Arg.Any<PollProperties?>()))
            .DoNotCallBase();

        // Act
        await module.ItemAsync("weapons/swords");

        // Assert
        capturedText.Should().NotBeNull();
        capturedText!.Should().Contain("forge ran cold");
    }

    // ──────────────────────────────────────────────
    //  /generate enemy
    // ──────────────────────────────────────────────

    [Fact]
    public async Task EnemyAsync_WhenSuccessful_SendsEnemyEmbed()
    {
        // Arrange
        var (module, mediator, interaction) = ArrangeModule();

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

        mediator.Send(Arg.Any<GenerateEnemyCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GenerateEnemyResult { Success = true, Enemy = enemy });

        Embed? captured = null;
        interaction.When(x => x.FollowupAsync(
                Arg.Any<string?>(),
                Arg.Any<Embed[]>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<AllowedMentions?>(),
                Arg.Any<MessageComponent?>(),
                Arg.Do<Embed?>(e => captured = e),
                Arg.Any<RequestOptions?>(),
                Arg.Any<PollProperties?>()))
            .DoNotCallBase();

        // Act
        await module.EnemyAsync("dragons");

        // Assert
        captured.Should().NotBeNull();
        captured!.Title.Should().Contain("Fire Drake");
        captured.Fields.Should().Contain(f => f.Name == "Level" && f.Value == "15");
        captured.Fields.Should().Contain(f => f.Name == "HP" && f.Value == "200");
        captured.Fields.Should().Contain(f => f.Name == "XP" && f.Value == "450");
        captured.Fields.Should().Contain(f => f.Name == "Gold" && f.Value == "120");
    }

    [Fact]
    public async Task EnemyAsync_WhenFailed_SendsErrorMessage()
    {
        // Arrange
        var (module, mediator, interaction) = ArrangeModule();

        mediator.Send(Arg.Any<GenerateEnemyCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GenerateEnemyResult
            {
                Success = false,
                ErrorMessage = "No creatures found",
            });

        string? capturedText = null;
        interaction.When(x => x.FollowupAsync(
                Arg.Do<string?>(t => capturedText = t),
                Arg.Any<Embed[]>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<AllowedMentions?>(),
                Arg.Any<MessageComponent?>(),
                Arg.Any<Embed?>(),
                Arg.Any<RequestOptions?>(),
                Arg.Any<PollProperties?>()))
            .DoNotCallBase();

        // Act
        await module.EnemyAsync("dragons");

        // Assert
        capturedText.Should().NotBeNull();
        capturedText!.Should().Contain("void returned nothing");
        capturedText.Should().Contain("dragons");
    }

    [Fact]
    public async Task EnemyAsync_WhenEnemyIsNull_SendsErrorMessage()
    {
        // Arrange
        var (module, mediator, interaction) = ArrangeModule();

        mediator.Send(Arg.Any<GenerateEnemyCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GenerateEnemyResult { Success = true, Enemy = null });

        string? capturedText = null;
        interaction.When(x => x.FollowupAsync(
                Arg.Do<string?>(t => capturedText = t),
                Arg.Any<Embed[]>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<AllowedMentions?>(),
                Arg.Any<MessageComponent?>(),
                Arg.Any<Embed?>(),
                Arg.Any<RequestOptions?>(),
                Arg.Any<PollProperties?>()))
            .DoNotCallBase();

        // Act
        await module.EnemyAsync("dragons");

        // Assert
        capturedText.Should().NotBeNull();
        capturedText!.Should().Contain("void returned nothing");
    }

    // ──────────────────────────────────────────────
    //  /generate npc
    // ──────────────────────────────────────────────

    [Fact]
    public async Task NpcAsync_WhenSuccessful_SendsNpcEmbed()
    {
        // Arrange
        var (module, mediator, interaction) = ArrangeModule();

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

        mediator.Send(Arg.Any<GenerateNPCCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GenerateNPCResult { Success = true, NPC = npc });

        Embed? captured = null;
        interaction.When(x => x.FollowupAsync(
                Arg.Any<string?>(),
                Arg.Any<Embed[]>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<AllowedMentions?>(),
                Arg.Any<MessageComponent?>(),
                Arg.Do<Embed?>(e => captured = e),
                Arg.Any<RequestOptions?>(),
                Arg.Any<PollProperties?>()))
            .DoNotCallBase();

        // Act
        await module.NpcAsync("merchants");

        // Assert
        captured.Should().NotBeNull();
        captured!.Title.Should().Contain("Elara Moonshade");
        captured.Fields.Should().Contain(f => f.Name == "Occupation" && f.Value == "Apothecary");
        captured.Fields.Should().Contain(f => f.Name == "Social Class" && f.Value == "merchant");
        captured.Fields.Should().Contain(f => f.Name == "Age");
        captured.Fields.Should().Contain(f => f.Name == "Gold" && f.Value == "3d10");
    }

    [Fact]
    public async Task NpcAsync_WhenFailed_SendsErrorMessage()
    {
        // Arrange
        var (module, mediator, interaction) = ArrangeModule();

        mediator.Send(Arg.Any<GenerateNPCCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GenerateNPCResult
            {
                Success = false,
                ErrorMessage = "No NPCs available",
            });

        string? capturedText = null;
        interaction.When(x => x.FollowupAsync(
                Arg.Do<string?>(t => capturedText = t),
                Arg.Any<Embed[]>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<AllowedMentions?>(),
                Arg.Any<MessageComponent?>(),
                Arg.Any<Embed?>(),
                Arg.Any<RequestOptions?>(),
                Arg.Any<PollProperties?>()))
            .DoNotCallBase();

        // Act
        await module.NpcAsync("merchants");

        // Assert
        capturedText.Should().NotBeNull();
        capturedText!.Should().Contain("No one answered");
        capturedText.Should().Contain("merchants");
    }

    [Fact]
    public async Task NpcAsync_WhenNpcIsNull_SendsErrorMessage()
    {
        // Arrange
        var (module, mediator, interaction) = ArrangeModule();

        mediator.Send(Arg.Any<GenerateNPCCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GenerateNPCResult { Success = true, NPC = null });

        string? capturedText = null;
        interaction.When(x => x.FollowupAsync(
                Arg.Do<string?>(t => capturedText = t),
                Arg.Any<Embed[]>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<AllowedMentions?>(),
                Arg.Any<MessageComponent?>(),
                Arg.Any<Embed?>(),
                Arg.Any<RequestOptions?>(),
                Arg.Any<PollProperties?>()))
            .DoNotCallBase();

        // Act
        await module.NpcAsync("merchants");

        // Assert
        capturedText.Should().NotBeNull();
        capturedText!.Should().Contain("No one answered");
    }

    // ──────────────────────────────────────────────
    //  /generate ability
    // ──────────────────────────────────────────────

    [Fact]
    public async Task AbilityAsync_WhenSuccessful_SendsAbilityEmbed()
    {
        // Arrange
        var (module, mediator, interaction) = ArrangeModule();

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

        mediator.Send(Arg.Any<GeneratePowerCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GeneratePowerResult { Success = true, Powers = [ability] });

        Embed? captured = null;
        interaction.When(x => x.FollowupAsync(
                Arg.Any<string?>(),
                Arg.Any<Embed[]>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<AllowedMentions?>(),
                Arg.Any<MessageComponent?>(),
                Arg.Do<Embed?>(e => captured = e),
                Arg.Any<RequestOptions?>(),
                Arg.Any<PollProperties?>()))
            .DoNotCallBase();

        // Act
        await module.AbilityAsync("active/offensive");

        // Assert
        captured.Should().NotBeNull();
        captured!.Title.Should().Contain("Fireball");
        captured.Fields.Should().Contain(f => f.Name == "Type" && f.Value == "Spell");
        captured.Fields.Should().Contain(f => f.Name == "Tier" && f.Value == "3");
        captured.Fields.Should().Contain(f => f.Name == "Passive" && f.Value == "No");
        captured.Fields.Should().Contain(f => f.Name == "Mana Cost" && f.Value == "50");
        captured.Fields.Should().Contain(f => f.Name == "Cooldown" && f.Value == "3t");
        captured.Fields.Should().Contain(f => f.Name == "Req. Level" && f.Value == "10");
        captured.Fields.Should().Contain(f => f.Name == "Damage" && f.Value == "8d6");
    }

    [Fact]
    public async Task AbilityAsync_WhenFailed_SendsErrorMessage()
    {
        // Arrange
        var (module, mediator, interaction) = ArrangeModule();

        mediator.Send(Arg.Any<GeneratePowerCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GeneratePowerResult
            {
                Success = false,
                ErrorMessage = "Arcane repository depleted",
            });

        string? capturedText = null;
        interaction.When(x => x.FollowupAsync(
                Arg.Do<string?>(t => capturedText = t),
                Arg.Any<Embed[]>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<AllowedMentions?>(),
                Arg.Any<MessageComponent?>(),
                Arg.Any<Embed?>(),
                Arg.Any<RequestOptions?>(),
                Arg.Any<PollProperties?>()))
            .DoNotCallBase();

        // Act
        await module.AbilityAsync("active/offensive");

        // Assert
        capturedText.Should().NotBeNull();
        capturedText!.Should().Contain("arcane scroll was blank");
        capturedText.Should().Contain("active/offensive");
    }

    [Fact]
    public async Task AbilityAsync_WhenPowersIsNull_SendsErrorMessage()
    {
        // Arrange
        var (module, mediator, interaction) = ArrangeModule();

        mediator.Send(Arg.Any<GeneratePowerCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GeneratePowerResult { Success = true, Powers = null! });

        string? capturedText = null;
        interaction.When(x => x.FollowupAsync(
                Arg.Do<string?>(t => capturedText = t),
                Arg.Any<Embed[]>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<AllowedMentions?>(),
                Arg.Any<MessageComponent?>(),
                Arg.Any<Embed?>(),
                Arg.Any<RequestOptions?>(),
                Arg.Any<PollProperties?>()))
            .DoNotCallBase();

        // Act
        await module.AbilityAsync("active/offensive");

        // Assert
        capturedText.Should().NotBeNull();
        capturedText!.Should().Contain("arcane scroll was blank");
    }

    [Fact]
    public async Task AbilityAsync_WhenPowersEmpty_SendsErrorMessage()
    {
        // Arrange
        var (module, mediator, interaction) = ArrangeModule();

        mediator.Send(Arg.Any<GeneratePowerCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GeneratePowerResult { Success = true, Powers = [] });

        string? capturedText = null;
        interaction.When(x => x.FollowupAsync(
                Arg.Do<string?>(t => capturedText = t),
                Arg.Any<Embed[]>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<AllowedMentions?>(),
                Arg.Any<MessageComponent?>(),
                Arg.Any<Embed?>(),
                Arg.Any<RequestOptions?>(),
                Arg.Any<PollProperties?>()))
            .DoNotCallBase();

        // Act
        await module.AbilityAsync("active/offensive");

        // Assert
        capturedText.Should().NotBeNull();
        capturedText!.Should().Contain("arcane scroll was blank");
    }

    [Fact]
    public async Task AbilityAsync_SendsCorrectCommand()
    {
        // Arrange
        var (module, mediator, _) = ArrangeModule();

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
    public async Task AbilityAsync_WithoutSubcategory_SendsCorrectCommand()
    {
        // Arrange
        var (module, mediator, _) = ArrangeModule();

        mediator.Send(Arg.Any<GeneratePowerCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GeneratePowerResult { Success = true, Powers = [new Power()] });

        // Act
        await module.AbilityAsync("passive");

        // Assert
        await mediator.Received(1).Send(
            Arg.Is<GeneratePowerCommand>(c =>
                c.Category == "passive" && c.Subcategory == string.Empty),
            Arg.Any<CancellationToken>());
    }
}
