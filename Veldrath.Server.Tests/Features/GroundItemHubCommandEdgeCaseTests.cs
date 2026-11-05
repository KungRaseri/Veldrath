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
/// Edge-case tests for the ground-item commands: a missing character, a vanished ground item, a catalog fault while
/// resolving names, stacking onto an existing bag entry, and dropping the last unit of a stack.
/// </summary>
public class GroundItemHubCommandEdgeCaseTests
{
    private const string Zone = "crestfall";
    private const string Location = "grey-cup";

    private static Character Character(string location = Location, string? inventory = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            Name = "TestHero",
            ClassName = "Warrior",
            SlotIndex = 1,
            CurrentZoneId = Zone,
            CurrentZoneLocationSlug = location,
            Attributes = "{}",
            InventoryBlob = inventory ?? JsonSerializer.Serialize(new[]
            {
                new { ItemRef = "iron_sword", Quantity = 2, Durability = (int?)75 },
            }),
        };

    private static Mock<ICharacterRepository> CharacterRepo(Character? character)
    {
        var repo = new Mock<ICharacterRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(character);
        repo.Setup(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return repo;
    }

    private static Mock<IItemRepository> ItemRepo()
    {
        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetBySlugAsync("iron_sword")).ReturnsAsync(new Item
        {
            Slug = "iron_sword",
            Name = "Iron Sword",
            Type = ItemType.Weapon,
            Rarity = ItemRarity.Rare,
        });
        repo.Setup(r => r.GetBySlugAsync("ore")).ReturnsAsync(new Item
        {
            Slug = "ore",
            Name = "Iron Ore",
            Type = ItemType.Consumable,
            Rarity = ItemRarity.Common,
            IsStackable = true,
            StackSize = 20,
        });
        return repo;
    }

    private static Mock<IGroundItemRepository> GroundRepo(params GroundItem[] items)
    {
        var repo = new Mock<IGroundItemRepository>();
        repo.Setup(r => r.GetAtLocationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GroundItem?)null);
        repo.Setup(r => r.AddAsync(It.IsAny<GroundItem>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        repo.Setup(r => r.RemoveAsync(It.IsAny<GroundItem>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return repo;
    }

    private static GetGroundItemsHubCommandHandler GetHandler(
        Mock<ICharacterRepository> chars,
        Mock<IGroundItemRepository> ground,
        Mock<IItemRepository> items) =>
        new(chars.Object, ground.Object, items.Object, NullLogger<GetGroundItemsHubCommandHandler>.Instance);

    private static DropItemToGroundHubCommandHandler DropHandler(
        Mock<ICharacterRepository> chars,
        Mock<IGroundItemRepository> ground,
        Mock<IItemRepository> items) =>
        new(chars.Object, ground.Object, items.Object, NullLogger<DropItemToGroundHubCommandHandler>.Instance);

    private static PickUpGroundItemHubCommandHandler PickUpHandler(
        Mock<ICharacterRepository> chars,
        Mock<IGroundItemRepository> ground,
        Mock<IItemRepository> items) =>
        new(chars.Object, ground.Object, items.Object, NullLogger<PickUpGroundItemHubCommandHandler>.Instance);

    [Fact]
    public async Task GetGroundItems_ReturnsAnError_WhenTheCharacterIsMissing()
    {
        var result = await GetHandler(CharacterRepo(null), GroundRepo(), ItemRepo())
            .Handle(new GetGroundItemsHubCommand(Guid.NewGuid()), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().StartWith("Character ").And.EndWith(" not found.");
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetGroundItems_FallsBackToTheSlug_WhenTheCatalogFaults()
    {
        var items = new Mock<IItemRepository>();
        items.Setup(r => r.GetBySlugAsync(It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("offline"));
        var character = Character();

        var result = await GetHandler(CharacterRepo(character), GroundRepo(new GroundItem
        {
            ZoneId = Zone,
            LocationSlug = Location,
            ItemRef = "unlisted-relic",
            Quantity = 2,
        }), items).Handle(new GetGroundItemsHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        var view = result.Items.Should().ContainSingle().Which;
        view.Name.Should().Be("unlisted-relic");
        view.Rarity.Should().Be("Common");
        view.Quantity.Should().Be(2);
    }

    [Fact]
    public async Task GetGroundItems_TreatsACorruptInventoryBlobAsEmpty()
    {
        var character = Character(inventory: "{not json");

        var result = await GetHandler(CharacterRepo(character), GroundRepo(), ItemRepo())
            .Handle(new GetGroundItemsHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Drop_ReturnsAnError_WhenTheCharacterIsMissing()
    {
        var result = await DropHandler(CharacterRepo(null), GroundRepo(), ItemRepo())
            .Handle(new DropItemToGroundHubCommand(Guid.NewGuid(), "iron_sword"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task Drop_RemovesTheStackEntry_WhenTheLastUnitIsDropped()
    {
        var character = Character(inventory: JsonSerializer.Serialize(new[]
        {
            new { ItemRef = "iron_sword", Quantity = 1, Durability = (int?)null },
        }));

        var result = await DropHandler(CharacterRepo(character), GroundRepo(), ItemRepo())
            .Handle(new DropItemToGroundHubCommand(character.Id, "iron_sword"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.RemainingQuantity.Should().Be(0);
        JsonSerializer.Deserialize<List<InventoryItemDto>>(character.InventoryBlob)!.Should().BeEmpty();
    }

    [Fact]
    public async Task PickUp_ReturnsAnError_WhenTheCharacterIsMissing()
    {
        var result = await PickUpHandler(CharacterRepo(null), GroundRepo(), ItemRepo())
            .Handle(new PickUpGroundItemHubCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task PickUp_ReturnsAnError_WhenTheGroundItemIsGone()
    {
        var character = Character();

        var result = await PickUpHandler(CharacterRepo(character), GroundRepo(), ItemRepo())
            .Handle(new PickUpGroundItemHubCommand(character.Id, Guid.NewGuid()), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("That item is no longer on the ground.");
    }

    [Fact]
    public async Task PickUp_StacksOntoAnExistingBagEntry_WhenTheCatalogItemIsStackable()
    {
        var character = Character(inventory: JsonSerializer.Serialize(new[]
        {
            new { ItemRef = "ore", Quantity = 3, Durability = (int?)null },
        }));
        var ground = new GroundItem
        {
            ZoneId = Zone,
            LocationSlug = Location,
            ItemRef = "ore",
            Quantity = 2,
        };
        var groundRepo = GroundRepo(ground);
        groundRepo.Setup(r => r.GetByIdAsync(ground.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ground);

        var result = await PickUpHandler(CharacterRepo(character), groundRepo, ItemRepo())
            .Handle(new PickUpGroundItemHubCommand(character.Id, ground.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ItemRef.Should().Be("ore");
        var bag = JsonSerializer.Deserialize<List<InventoryItemDto>>(character.InventoryBlob)!;
        bag.Should().ContainSingle().Which.Quantity.Should().Be(5);
        groundRepo.Verify(r => r.RemoveAsync(ground, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PickUp_StillAddsTheItem_WhenTheCatalogLookupFaults()
    {
        var character = Character(inventory: "[]");
        var ground = new GroundItem
        {
            ZoneId = Zone,
            LocationSlug = Location,
            ItemRef = "unlisted-relic",
            Quantity = 1,
        };
        var groundRepo = GroundRepo(ground);
        groundRepo.Setup(r => r.GetByIdAsync(ground.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ground);
        var items = new Mock<IItemRepository>();
        items.Setup(r => r.GetBySlugAsync("unlisted-relic")).ThrowsAsync(new InvalidOperationException("offline"));

        var result = await PickUpHandler(CharacterRepo(character), groundRepo, items)
            .Handle(new PickUpGroundItemHubCommand(character.Id, ground.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        JsonSerializer.Deserialize<List<InventoryItemDto>>(character.InventoryBlob)!
            .Should().ContainSingle().Which.ItemRef.Should().Be("unlisted-relic");
    }
}
