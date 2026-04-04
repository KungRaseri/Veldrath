using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmUnbound.Server.Data.Repositories;
using RealmUnbound.Server.Features.Shop;
using Character = RealmUnbound.Server.Data.Entities.Character;

namespace RealmUnbound.Server.Tests.Features;

/// <summary>Unit tests for <see cref="DropItemHubCommandHandler"/>.</summary>
public class DropItemHubCommandHandlerTests
{
    private static DropItemHubCommandHandler MakeHandler(ICharacterRepository charRepo) =>
        new(charRepo, NullLogger<DropItemHubCommandHandler>.Instance);

    private static Character MakeCharacter(Guid id, string? inventoryBlob = null)
    {
        return new Character
        {
            Id            = id,
            AccountId     = Guid.NewGuid(),
            Name          = "TestHero",
            ClassName     = "Warrior",
            SlotIndex     = 1,
            Attributes    = "{}",
            InventoryBlob = inventoryBlob ?? "[]",
        };
    }

    private static string InvWith(string slug, int qty) =>
        JsonSerializer.Serialize(new[] { new { itemRef = slug, quantity = qty, durability = (int?)null } });

    // ── Guard tests ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsError_WhenItemRefIsEmpty()
    {
        var result = await MakeHandler(Mock.Of<ICharacterRepository>())
            .Handle(new DropItemHubCommand(Guid.NewGuid(), ""), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("ItemRef is required");
    }

    [Fact]
    public async Task Handle_ReturnsError_WhenCharacterNotFound()
    {
        var charRepo = new Mock<ICharacterRepository>();
        charRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Character?)null);

        var result = await MakeHandler(charRepo.Object)
            .Handle(new DropItemHubCommand(Guid.NewGuid(), "sword"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_ReturnsError_WhenItemNotInInventory()
    {
        var charId   = Guid.NewGuid();
        var charRepo = new Mock<ICharacterRepository>();
        charRepo.Setup(r => r.GetByIdAsync(charId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(MakeCharacter(charId));  // empty inventory

        var result = await MakeHandler(charRepo.Object)
            .Handle(new DropItemHubCommand(charId, "sword"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found in inventory");
    }

    // ── Success tests ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_RemovesSlot_WhenLastUnitDropped()
    {
        var charId    = Guid.NewGuid();
        Character? saved = null;
        var charRepo  = new Mock<ICharacterRepository>();
        charRepo.Setup(r => r.GetByIdAsync(charId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(MakeCharacter(charId, inventoryBlob: InvWith("sword", qty: 1)));
        charRepo.Setup(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()))
                .Callback<Character, CancellationToken>((c, _) => saved = c)
                .Returns(Task.CompletedTask);

        var result = await MakeHandler(charRepo.Object)
            .Handle(new DropItemHubCommand(charId, "sword"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ItemRef.Should().Be("sword");
        result.RemainingQuantity.Should().Be(0);

        var inv = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(saved!.InventoryBlob);
        inv.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_DecrementsQuantity_WhenMoreThanOneUnit()
    {
        var charId    = Guid.NewGuid();
        Character? saved = null;
        var charRepo  = new Mock<ICharacterRepository>();
        charRepo.Setup(r => r.GetByIdAsync(charId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(MakeCharacter(charId, inventoryBlob: InvWith("potion", qty: 3)));
        charRepo.Setup(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()))
                .Callback<Character, CancellationToken>((c, _) => saved = c)
                .Returns(Task.CompletedTask);

        var result = await MakeHandler(charRepo.Object)
            .Handle(new DropItemHubCommand(charId, "potion"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.RemainingQuantity.Should().Be(2);

        var inv = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(saved!.InventoryBlob);
        inv.Should().HaveCount(1);
        inv![0]["Quantity"].GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Handle_IsCaseInsensitive_OnItemRef()
    {
        var charId   = Guid.NewGuid();
        var charRepo = new Mock<ICharacterRepository>();
        charRepo.Setup(r => r.GetByIdAsync(charId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(MakeCharacter(charId, inventoryBlob: InvWith("IronSword", qty: 1)));
        charRepo.Setup(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        var result = await MakeHandler(charRepo.Object)
            .Handle(new DropItemHubCommand(charId, "ironsword"), CancellationToken.None);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_PersistsCharacter_OnSuccess()
    {
        var charId   = Guid.NewGuid();
        var charRepo = new Mock<ICharacterRepository>();
        charRepo.Setup(r => r.GetByIdAsync(charId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(MakeCharacter(charId, inventoryBlob: InvWith("helm", qty: 1)));
        charRepo.Setup(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        await MakeHandler(charRepo.Object)
            .Handle(new DropItemHubCommand(charId, "helm"), CancellationToken.None);

        charRepo.Verify(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
