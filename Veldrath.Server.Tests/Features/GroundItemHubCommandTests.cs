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
/// Unit tests for the ground-item commands: dropping a bag item, picking one up, and listing what is
/// lying at the character's current location.
/// </summary>
public class GroundItemHubCommandTests
{
    private const string Zone = "crestfall";
    private const string Location = "grey-cup";

    private static Character MakeCharacter(string? locationSlug = Location, string? inventoryBlob = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            Name = "TestHero",
            ClassName = "Warrior",
            SlotIndex = 1,
            CurrentZoneId = Zone,
            CurrentZoneLocationSlug = locationSlug,
            Attributes = "{}",
            InventoryBlob = inventoryBlob ?? JsonSerializer.Serialize(new[]
            {
                new { ItemRef = "iron_sword", Quantity = 2, Durability = (int?)75 },
            }),
        };

    private static Mock<ICharacterRepository> CharacterRepo(Character character)
    {
        var repo = new Mock<ICharacterRepository>();
        repo.Setup(r => r.GetByIdAsync(character.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(character);
        repo.Setup(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return repo;
    }

    private static Mock<IItemRepository> ItemRepo()
    {
        var repo = new Mock<IItemRepository>();
        repo.Setup(r => r.GetBySlugAsync("iron_sword"))
            .ReturnsAsync(new Item
            {
                Slug = "iron_sword",
                Name = "Iron Sword",
                Type = ItemType.Weapon,
                Rarity = ItemRarity.Rare,
            });
        return repo;
    }

    private static DropItemToGroundHubCommandHandler DropHandler(
        Mock<ICharacterRepository> charRepo,
        Mock<IGroundItemRepository> groundRepo,
        Mock<IItemRepository> itemRepo) =>
        new(charRepo.Object, groundRepo.Object, itemRepo.Object,
            NullLogger<DropItemToGroundHubCommandHandler>.Instance);

    private static PickUpGroundItemHubCommandHandler PickUpHandler(
        Mock<ICharacterRepository> charRepo,
        Mock<IGroundItemRepository> groundRepo,
        Mock<IItemRepository> itemRepo) =>
        new(charRepo.Object, groundRepo.Object, itemRepo.Object,
            NullLogger<PickUpGroundItemHubCommandHandler>.Instance);

    [Fact]
    public async Task Drop_Should_Move_One_Unit_To_The_Ground()
    {
        var character = MakeCharacter();
        var charRepo = CharacterRepo(character);
        var groundRepo = new Mock<IGroundItemRepository>();
        GroundItem? added = null;
        groundRepo
            .Setup(r => r.AddAsync(It.IsAny<GroundItem>(), It.IsAny<CancellationToken>()))
            .Callback<GroundItem, CancellationToken>((item, _) => added = item)
            .Returns(Task.CompletedTask);
        groundRepo
            .Setup(r => r.GetAtLocationAsync(Zone, Location, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new GroundItem
            {
                ZoneId = Zone,
                LocationSlug = Location,
                ItemRef = "iron_sword",
                Quantity = 1,
            }]);

        var result = await DropHandler(charRepo, groundRepo, ItemRepo())
            .Handle(new DropItemToGroundHubCommand(character.Id, "iron_sword"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.RemainingQuantity.Should().Be(1);
        result.Items.Should().ContainSingle().Which.Name.Should().Be("Iron Sword");
        added.Should().NotBeNull();
        added!.ItemRef.Should().Be("iron_sword");
        added.LocationSlug.Should().Be(Location);
        added.Durability.Should().Be(75);
        character.InventoryBlob.Should().Contain("\"Quantity\":1");
    }

    [Fact]
    public async Task Drop_Should_Fail_Without_A_Location()
    {
        var character = MakeCharacter(locationSlug: null);

        var result = await DropHandler(CharacterRepo(character), new Mock<IGroundItemRepository>(), ItemRepo())
            .Handle(new DropItemToGroundHubCommand(character.Id, "iron_sword"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Move to a location");
    }

    [Fact]
    public async Task Drop_Should_Fail_When_Item_Is_Not_In_The_Bag()
    {
        var character = MakeCharacter();

        var result = await DropHandler(CharacterRepo(character), new Mock<IGroundItemRepository>(), ItemRepo())
            .Handle(new DropItemToGroundHubCommand(character.Id, "health_potion"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("do not have");
    }

    [Fact]
    public async Task PickUp_Should_Add_The_Item_To_The_Bag_And_Clear_The_Ground()
    {
        var character = MakeCharacter(inventoryBlob: "[]");
        var charRepo = CharacterRepo(character);
        var groundItem = new GroundItem
        {
            ZoneId = Zone,
            LocationSlug = Location,
            ItemRef = "iron_sword",
            Quantity = 1,
            Durability = 40,
        };
        var groundRepo = new Mock<IGroundItemRepository>();
        groundRepo.Setup(r => r.GetByIdAsync(groundItem.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(groundItem);
        groundRepo.Setup(r => r.RemoveAsync(groundItem, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        groundRepo.Setup(r => r.GetAtLocationAsync(Zone, Location, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await PickUpHandler(charRepo, groundRepo, ItemRepo())
            .Handle(new PickUpGroundItemHubCommand(character.Id, groundItem.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ItemRef.Should().Be("iron_sword");
        result.Items.Should().BeEmpty();
        character.InventoryBlob.Should().Contain("iron_sword");
        groundRepo.Verify(r => r.RemoveAsync(groundItem, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PickUp_Should_Reject_Items_Dropped_Elsewhere()
    {
        var character = MakeCharacter();
        var groundItem = new GroundItem
        {
            ZoneId = Zone,
            LocationSlug = "the-grey-cup-cellar",
            ItemRef = "iron_sword",
            Quantity = 1,
        };
        var groundRepo = new Mock<IGroundItemRepository>();
        groundRepo.Setup(r => r.GetByIdAsync(groundItem.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(groundItem);

        var result = await PickUpHandler(CharacterRepo(character), groundRepo, ItemRepo())
            .Handle(new PickUpGroundItemHubCommand(character.Id, groundItem.Id), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not standing");
    }

    [Fact]
    public async Task GetGroundItems_Should_Project_Catalog_Details()
    {
        var character = MakeCharacter();
        var groundRepo = new Mock<IGroundItemRepository>();
        groundRepo.Setup(r => r.GetAtLocationAsync(Zone, Location, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new GroundItem
            {
                ZoneId = Zone,
                LocationSlug = Location,
                ItemRef = "iron_sword",
                Quantity = 3,
            }]);

        var handler = new GetGroundItemsHubCommandHandler(
            CharacterRepo(character).Object,
            groundRepo.Object,
            ItemRepo().Object,
            NullLogger<GetGroundItemsHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new GetGroundItemsHubCommand(character.Id),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        var view = result.Items.Should().ContainSingle().Subject;
        view.Name.Should().Be("Iron Sword");
        view.Rarity.Should().Be("Rare");
        view.Quantity.Should().Be(3);
    }

    [Fact]
    public async Task GetGroundItems_Should_Return_Empty_When_Not_At_A_Location()
    {
        var character = MakeCharacter(locationSlug: null);

        var handler = new GetGroundItemsHubCommandHandler(
            CharacterRepo(character).Object,
            new Mock<IGroundItemRepository>().Object,
            ItemRepo().Object,
            NullLogger<GetGroundItemsHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new GetGroundItemsHubCommand(character.Id),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Items.Should().BeEmpty();
    }
}
