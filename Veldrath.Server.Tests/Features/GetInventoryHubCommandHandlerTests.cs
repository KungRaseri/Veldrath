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
/// Unit tests for <see cref="GetInventoryHubCommandHandler"/>: the flat inventory projection, equipment
/// top-up, catalog-resolution fallbacks, and corrupt-blob tolerance.
/// </summary>
public class GetInventoryHubCommandHandlerTests
{
    private const string SwordBlob =
        """[{"ItemRef":"iron_sword","Quantity":2,"Durability":75},{"ItemRef":"health_potion","Quantity":3,"Durability":null}]""";

    private static Character Character(string inventory = SwordBlob, string equipment = """{"MainHand":"iron_sword"}""") =>
        new()
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            Name = "TestHero",
            ClassName = "Warrior",
            SlotIndex = 1,
            InventoryBlob = inventory,
            EquipmentBlob = equipment,
        };

    private static Item Sword() => new()
    {
        Slug = "iron_sword",
        Name = "Iron Sword",
        Description = "A dependable blade.",
        Type = ItemType.Weapon,
        Rarity = ItemRarity.Uncommon,
        TypeKey = "heavy-blades",
        StackSize = 1,
        Weight = 4.5,
        Price = 120,
        WeaponType = "swords",
        Damage = new ItemDamage { Min = 6, Max = 11 },
        Requirements = new ItemRequirements { Level = 3, Attributes = new() { ["strength"] = 12 } },
        BaseAttributes = new() { ["strength"] = 2 },
    };

    private static Item Potion() => new()
    {
        Slug = "health_potion",
        Name = "Health Potion",
        Type = ItemType.Consumable,
        Rarity = ItemRarity.Common,
        StackSize = 20,
        IsStackable = true,
        Price = 25,
        Effect = "heal",
        Power = 40,
    };

    private static (GetInventoryHubCommandHandler Handler, Mock<ICharacterRepository> Characters, Mock<IItemRepository> Items)
        Arrange(Character? character)
    {
        var characters = new Mock<ICharacterRepository>();
        characters.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(character);

        var items = new Mock<IItemRepository>();
        items.Setup(r => r.GetBySlugAsync("iron_sword")).ReturnsAsync(Sword());
        items.Setup(r => r.GetBySlugAsync("health_potion")).ReturnsAsync(Potion());

        var handler = new GetInventoryHubCommandHandler(
            characters.Object,
            items.Object,
            NullLogger<GetInventoryHubCommandHandler>.Instance);

        return (handler, characters, items);
    }

    [Fact]
    public async Task Handle_ReturnsAnError_WhenTheCharacterDoesNotExist()
    {
        var (handler, _, _) = Arrange(null);

        var result = await handler.Handle(new GetInventoryHubCommand(Guid.NewGuid()), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().StartWith("Character ").And.EndWith(" not found.");
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ProjectsBagItemsWithCatalogDetailsAndEquipmentFlags()
    {
        var character = Character();
        var (handler, _, _) = Arrange(character);

        var result = await handler.Handle(new GetInventoryHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Items.Should().HaveCount(2);

        var sword = result.Items.Single(i => i.ItemRef == "iron_sword");
        sword.Name.Should().Be("Iron Sword");
        sword.Description.Should().Be("A dependable blade.");
        sword.Rarity.Should().Be("Uncommon");
        sword.Type.Should().Be("Weapon");
        sword.TypeKey.Should().Be("heavy-blades");
        sword.Slot.Should().Be(EquipmentSlots.MainHand);
        sword.Quantity.Should().Be(2);
        sword.Durability.Should().Be(75);
        sword.IsEquipped.Should().BeTrue();
        sword.Price.Should().Be(120);
        sword.Weight.Should().Be(4.5);
        sword.DamageMin.Should().Be(6);
        sword.DamageMax.Should().Be(11);
        sword.RequirementsLevel.Should().Be(3);
        sword.Requirements["strength"].Should().Be(12);
        sword.Bonuses["strength"].Should().Be(2);

        var potion = result.Items.Single(i => i.ItemRef == "health_potion");
        potion.IsEquipped.Should().BeFalse();
        potion.Slot.Should().BeNull("consumables are not equippable");
        potion.IsStackable.Should().BeTrue();
        potion.StackSize.Should().Be(20);
        potion.Effect.Should().Be("heal");
        potion.EffectPower.Should().Be(40);

        result.Equipped.Should().ContainKey(EquipmentSlots.MainHand);
        result.Equipped[EquipmentSlots.MainHand].ItemRef.Should().Be("iron_sword");
        result.Equipped[EquipmentSlots.MainHand].IsEquipped.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AddsStartingEquipmentThatIsMissingFromTheBag()
    {
        // Starting equipment is written to the equipment blob only, so the item list has to be topped up.
        var character = Character(inventory: "[]", equipment: """{"MainHand":"iron_sword","OffHand":"buckler-shield"}""");
        var (handler, _, items) = Arrange(character);
        items.Setup(r => r.GetBySlugAsync("buckler-shield")).ReturnsAsync((Item?)null);

        var result = await handler.Handle(new GetInventoryHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Items.Should().HaveCount(2, "the equipped sword and shield are added even though the bag is empty");
        result.Items.Should().Contain(i => i.ItemRef == "iron_sword" && i.IsEquipped);
        result.Equipped.Keys.Should().BeEquivalentTo([EquipmentSlots.MainHand, EquipmentSlots.OffHand]);
        result.Items.Should().Contain(i => i.ItemRef == "buckler-shield" && i.Quantity == 1 && i.Durability == null);
    }

    [Fact]
    public async Task Handle_FallsBackToSlugDetails_WhenTheCatalogLookupThrows()
    {
        var character = Character(inventory: """[{"ItemRef":"mystery-tonic","Quantity":1,"Durability":null}]""", equipment: "{}");
        var (handler, _, items) = Arrange(character);
        items.Setup(r => r.GetBySlugAsync("mystery-tonic"))
            .ThrowsAsync(new InvalidOperationException("catalog offline"));

        var result = await handler.Handle(new GetInventoryHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        var view = result.Items.Should().ContainSingle().Which;
        view.Name.Should().Be("Mystery Tonic");
        view.Rarity.Should().Be("Common");
        view.Type.Should().Be("Consumable");
        view.Price.Should().Be(0);
    }

    [Fact]
    public async Task Handle_TreatsACorruptInventoryBlobAsEmpty()
    {
        var character = Character(inventory: "not-json", equipment: """{"MainHand":"iron_sword"}""");
        var (handler, _, _) = Arrange(character);

        var result = await handler.Handle(new GetInventoryHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Items.Should().ContainSingle().Which.ItemRef.Should().Be("iron_sword");
    }

    [Fact]
    public async Task Handle_TreatsACorruptEquipmentBlobAsEmpty()
    {
        var character = Character(inventory: SwordBlob, equipment: "not-json");
        var (handler, _, _) = Arrange(character);

        var result = await handler.Handle(new GetInventoryHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Equipped.Should().BeEmpty();
        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(i => !i.IsEquipped);
    }

    [Theory]
    [InlineData("iron-shield", "Shield", "shield")]
    [InlineData("oak-sword", "Weapon", null)]
    [InlineData("battle-axe", "Weapon", null)]
    [InlineData("hunting-bow", "Weapon", null)]
    [InlineData("rusty-dagger", "Weapon", null)]
    [InlineData("gnarled-staff", "Weapon", null)]
    [InlineData("iron-mace", "Weapon", null)]
    [InlineData("war-hammer", "Weapon", null)]
    [InlineData("long-spear", "Weapon", null)]
    [InlineData("silver-ring", "Consumable", null)]
    public async Task Handle_InfersTheItemTypeFromTheSlug_WhenTheCatalogCannotResolveIt(
        string itemRef,
        string expectedType,
        string? expectedArmorType)
    {
        var character = Character(inventory: $$"""[{"ItemRef":"{{itemRef}}","Quantity":1,"Durability":null}]""", equipment: "{}");
        var (handler, _, items) = Arrange(character);
        items.Setup(r => r.GetBySlugAsync(itemRef)).ReturnsAsync((Item?)null);

        var result = await handler.Handle(new GetInventoryHubCommand(character.Id), CancellationToken.None);

        var view = result.Items.Should().ContainSingle().Which;
        view.Type.Should().Be(expectedType);
        view.ArmorType.Should().Be(expectedArmorType);
        view.Name.Should().Be(string.Join(' ', itemRef.Split('-').Select(w => char.ToUpperInvariant(w[0]) + w[1..])));
    }

    [Theory]
    [InlineData("", "[]")]
    [InlineData("   ", "{}")]
    [InlineData("[]", "{}")]
    public async Task Handle_TreatsEmptyBlobsAsEmptyCollections(string inventory, string equipment)
    {
        var character = Character(inventory: inventory, equipment: equipment);
        var (handler, characters, items) = Arrange(character);

        var result = await handler.Handle(new GetInventoryHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Items.Should().BeEmpty();
        result.Equipped.Should().BeEmpty();
        characters.Verify(r => r.GetByIdAsync(character.Id, It.IsAny<CancellationToken>()), Times.Once);
        items.Verify(r => r.GetBySlugAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SkipsBlankEquipmentSlots()
    {
        var character = Character(inventory: "[]", equipment: """{"OffHand":"   "}""");
        var (handler, _, items) = Arrange(character);

        var result = await handler.Handle(new GetInventoryHubCommand(character.Id), CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.Equipped.Should().BeEmpty();
        items.Verify(r => r.GetBySlugAsync(It.IsAny<string>()), Times.Never);
    }
}
