using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Characters;
using Character = Veldrath.Server.Data.Entities.Character;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Unit tests for <see cref="UseItemHubCommandHandler"/>: every consumable effect, the mana/health clamping,
/// stack decrementing, and the guards around a missing character, missing item, or corrupt blobs.
/// </summary>
public class UseItemHubCommandHandlerTests
{
    private const string FullAttributes = """{"CurrentHealth":40,"MaxHealth":100,"CurrentMana":10,"MaxMana":60}""";

    private static Character Character(
        int quantity = 2,
        string? attributes = FullAttributes,
        string? inventory = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            Name = "TestHero",
            ClassName = "Warrior",
            SlotIndex = 1,
            Attributes = attributes ?? FullAttributes,
            InventoryBlob = inventory ?? JsonSerializer.Serialize(new[]
            {
                new { ItemRef = "health_potion", Quantity = quantity, Durability = (int?)null },
            }),
        };

    private static Item Consumable(string? effect, int power, string name = "Health Potion") => new()
    {
        Slug = "health_potion",
        Name = name,
        Type = ItemType.Consumable,
        Effect = effect,
        Power = power,
    };

    private static (UseItemHubCommandHandler Handler, Mock<ICharacterRepository> Characters, Mock<IItemRepository> Items)
        Arrange(Character? character, Item? item = null, Exception? catalogFailure = null)
    {
        var characters = new Mock<ICharacterRepository>();
        characters.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(character);
        characters.Setup(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var items = new Mock<IItemRepository>();
        if (catalogFailure is not null)
            items.Setup(r => r.GetBySlugAsync("health_potion")).ThrowsAsync(catalogFailure);
        else
            items.Setup(r => r.GetBySlugAsync("health_potion")).ReturnsAsync(item ?? Consumable("heal", 25));

        var handler = new UseItemHubCommandHandler(
            characters.Object,
            items.Object,
            NullLogger<UseItemHubCommandHandler>.Instance);

        return (handler, characters, items);
    }

    private static UseItemHubCommand Command(Character character) =>
        new() { CharacterId = character.Id, ItemRef = "health_potion" };

    [Fact]
    public async Task Handle_RejectsABlankItemReference()
    {
        var character = Character();
        var (handler, _, items) = Arrange(character);

        var result = await handler.Handle(
            new UseItemHubCommand { CharacterId = character.Id, ItemRef = "  " },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("An item reference is required.");
        items.Verify(r => r.GetBySlugAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReturnsAnError_WhenTheCharacterDoesNotExist()
    {
        var (handler, _, _) = Arrange(null);

        var result = await handler.Handle(
            new UseItemHubCommand { CharacterId = Guid.NewGuid(), ItemRef = "health_potion" },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().StartWith("Character ").And.EndWith(" not found.");
    }

    [Fact]
    public async Task Handle_ReturnsAnError_WhenTheItemIsNotInTheBag()
    {
        var character = Character(inventory: "[]");
        var (handler, _, _) = Arrange(character);

        var result = await handler.Handle(Command(character), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("You do not have 'health_potion' in your bag.");
    }

    [Fact]
    public async Task Handle_ReturnsAnError_WhenTheCatalogLookupThrows()
    {
        var character = Character();
        var (handler, _, _) = Arrange(character, catalogFailure: new InvalidOperationException("catalog offline"));

        var result = await handler.Handle(Command(character), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("'health_potion' is not a usable item.");
    }

    [Fact]
    public async Task Handle_ReturnsAnError_WhenTheItemIsNotInTheCatalog()
    {
        var character = Character();
        var characters = new Mock<ICharacterRepository>();
        characters.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(character);
        var items = new Mock<IItemRepository>();
        items.Setup(r => r.GetBySlugAsync("health_potion")).ReturnsAsync((Item?)null);
        var handler = new UseItemHubCommandHandler(
            characters.Object, items.Object, NullLogger<UseItemHubCommandHandler>.Instance);

        var result = await handler.Handle(Command(character), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("'health_potion' is not a usable item.");
    }

    [Fact]
    public async Task Handle_HealsTheCharacter_AndKeepsTheStack()
    {
        var character = Character(quantity: 3);
        var (handler, characters, _) = Arrange(character, Consumable("heal", 25));

        var result = await handler.Handle(Command(character), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Message.Should().Be("You drink the Health Potion and recover 25 health.");
        result.CurrentHealth.Should().Be(65);
        result.MaxHealth.Should().Be(100);
        result.RemainingQuantity.Should().Be(2);
        result.ItemRef.Should().Be("health_potion");

        var persisted = JsonSerializer.Deserialize<List<InventoryItemDto>>(character.InventoryBlob)!;
        persisted.Should().ContainSingle().Which.Quantity.Should().Be(2);
        JsonSerializer.Deserialize<Dictionary<string, int>>(character.Attributes)!["CurrentHealth"].Should().Be(65);
        characters.Verify(r => r.UpdateAsync(character, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RefusesToUseAnItem_WhenTheCharacterIsDead()
    {
        // Death is a stored state (CurrentHealth 0); a heal here would leave the client's death banner
        // stuck while RespawnHubCommand refuses anyone above 0 HP.
        var character = Character(attributes: """{"CurrentHealth":0,"MaxHealth":100,"CurrentMana":10,"MaxMana":60}""");
        var (handler, characters, _) = Arrange(character, Consumable("heal", 25));

        var result = await handler.Handle(Command(character), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("You cannot use items while dead.");
        characters.Verify(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()), Times.Never);
        JsonSerializer.Deserialize<List<InventoryItemDto>>(character.InventoryBlob)!
            .Should().ContainSingle().Which.Quantity.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ClampsHealingAtTheMaximumHealth_AndRemovesTheLastUnit()
    {
        var character = Character(quantity: 1, attributes: """{"CurrentHealth":95,"MaxHealth":100}""");
        var (handler, _, _) = Arrange(character, Consumable("heal", 25));

        var result = await handler.Handle(Command(character), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Message.Should().Be("You drink the Health Potion and recover 5 health.");
        result.CurrentHealth.Should().Be(100);
        result.RemainingQuantity.Should().Be(0);
        JsonSerializer.Deserialize<List<InventoryItemDto>>(character.InventoryBlob)!.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_AppliesUncappedHealing_WhenTheCharacterHasNoMaximumHealth()
    {
        var character = Character(attributes: """{"CurrentHealth":10}""");
        var (handler, _, _) = Arrange(character, Consumable("heal", 15));

        var result = await handler.Handle(Command(character), CancellationToken.None);

        result.CurrentHealth.Should().Be(25);
        result.Message.Should().Be("You drink the Health Potion and recover 15 health.");
    }

    [Fact]
    public async Task Handle_AppliesHealingOverTime()
    {
        var character = Character();
        var (handler, _, _) = Arrange(character, Consumable("heal_overtime", 25));

        var result = await handler.Handle(Command(character), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Message.Should().Be("The Health Potion mends you for 12 health and keeps working.");
        result.CurrentHealth.Should().Be(52);
    }

    [Fact]
    public async Task Handle_RestoresMana()
    {
        var character = Character();
        var (handler, _, _) = Arrange(character, Consumable("restore", 20));

        var result = await handler.Handle(Command(character), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Message.Should().Be("You drink the Health Potion and recover 20 mana.");
        result.CurrentMana.Should().Be(30);
        result.CurrentHealth.Should().Be(40);
    }

    [Fact]
    public async Task Handle_ClampsManaAtTheMaximum_AndAppliesUncappedManaWithoutAMaximum()
    {
        var clamped = Character(attributes: """{"CurrentMana":55,"MaxMana":60}""");
        var (clampingHandler, _, _) = Arrange(clamped, Consumable("restore", 20));

        var clampedResult = await clampingHandler.Handle(Command(clamped), CancellationToken.None);
        clampedResult.CurrentMana.Should().Be(60);

        var uncapped = Character(attributes: """{"CurrentMana":5}""");
        var (uncappedHandler, _, _) = Arrange(uncapped, Consumable("restore", 20));
        var uncappedResult = await uncappedHandler.Handle(Command(uncapped), CancellationToken.None);
        uncappedResult.CurrentMana.Should().Be(25);
    }

    [Fact]
    public async Task Handle_CuresPoison()
    {
        var character = Character();
        var (handler, _, _) = Arrange(character, Consumable("cure_poison", 0));

        var result = await handler.Handle(Command(character), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Message.Should().Be("You use the Health Potion; the poison in your veins fades.");
        result.RemainingQuantity.Should().Be(1);
    }

    [Fact]
    public async Task Handle_RejectsAnItemWithAnUnrecognisedEffect()
    {
        var character = Character();
        var (handler, _, _) = Arrange(character, Consumable("teleport", 5));

        var result = await handler.Handle(Command(character), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("The Health Potion cannot be used (teleport).");
        character.InventoryBlob.Should().Contain("health_potion", "nothing is consumed when the effect is unknown");
    }

    [Fact]
    public async Task Handle_RejectsAnItemWithNoEffect()
    {
        var character = Character();
        var (handler, _, _) = Arrange(character, Consumable(null, 5));

        var result = await handler.Handle(Command(character), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("The Health Potion cannot be used.");
    }

    [Theory]
    [InlineData("heal", "The Health Potion has no healing power.")]
    [InlineData("heal_overtime", "The Health Potion has no healing power.")]
    [InlineData("restore", "The Health Potion has no restorative power.")]
    public async Task Handle_RejectsAConsumableWithNoPower(string effect, string expectedMessage)
    {
        var character = Character();
        var (handler, _, _) = Arrange(character, Consumable(effect, 0));

        var result = await handler.Handle(Command(character), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be(expectedMessage);
    }

    [Fact]
    public async Task Handle_FallsBackToTheSlug_WhenTheCatalogItemHasNoName()
    {
        var character = Character();
        var item = Consumable("heal", 5, name: "");
        var (handler, _, _) = Arrange(character, item);

        var result = await handler.Handle(Command(character), CancellationToken.None);

        result.Message.Should().Be("You drink the health_potion and recover 5 health.");
    }

    [Fact]
    public async Task Handle_TreatsAnEmptyAttributesBlobAsFreshAttributes()
    {
        var character = Character(attributes: "{}", inventory: JsonSerializer.Serialize(new[]
        {
            new { ItemRef = "health_potion", Quantity = 1, Durability = (int?)null },
        }));
        var (handler, _, _) = Arrange(character, Consumable("heal", 25));

        var result = await handler.Handle(Command(character), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.CurrentHealth.Should().Be(25, "a character with no recorded pools starts from zero");
        result.MaxHealth.Should().Be(0);
        result.MaxMana.Should().Be(0);
    }

    [Fact]
    public async Task Handle_TreatsACorruptInventoryBlobAsEmpty()
    {
        var character = Character(inventory: "not-json");
        var (handler, _, _) = Arrange(character);

        var result = await handler.Handle(Command(character), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("You do not have 'health_potion' in your bag.");
    }

    [Fact]
    public async Task Handle_TreatsACorruptAttributesBlobAsEmpty()
    {
        var character = Character(attributes: "not-json");
        var (handler, _, _) = Arrange(character, Consumable("heal", 25));

        var result = await handler.Handle(Command(character), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.CurrentHealth.Should().Be(25);
        result.MaxHealth.Should().Be(0);
    }

    [Fact]
    public async Task Handle_TreatsEmptyBlobsAsEmptyCollections()
    {
        var character = Character(attributes: "{}", inventory: "[]");
        var (handler, _, _) = Arrange(character);

        var result = await handler.Handle(Command(character), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("You do not have 'health_potion' in your bag.");
    }
}
