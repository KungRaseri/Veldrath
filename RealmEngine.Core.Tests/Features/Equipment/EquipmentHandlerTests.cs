using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Equipment.Commands;
using RealmEngine.Core.Features.Equipment.Queries;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Equipment;

[Trait("Category", "Feature")]
public class EquipItemHandlerTests
{
    private static EquipItemHandler CreateHandler(ISaveGameService? saveGameService = null) =>
        new(saveGameService ?? Mock.Of<ISaveGameService>(), NullLogger<EquipItemHandler>.Instance);

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoActiveSave()
    {
        var mockSaveSvc = new Mock<ISaveGameService>();
        mockSaveSvc.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);

        var result = await CreateHandler(mockSaveSvc.Object).Handle(
            new EquipItemCommand("char-01", "sword-01", EquipmentSlot.MainHand), default);

        result.Success.Should().BeFalse();
        result.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenItemNotInInventory()
    {
        var save = new SaveGame { Character = new Character { Name = "Hero" } };
        var mockSaveSvc = new Mock<ISaveGameService>();
        mockSaveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(mockSaveSvc.Object).Handle(
            new EquipItemCommand("char-01", "nonexistent-sword", EquipmentSlot.MainHand), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("nonexistent-sword");
    }
}

[Trait("Category", "Feature")]
public class GetEquipmentForClassHandlerTests
{
    private static GetEquipmentForClassHandler CreateHandler(
        CharacterClass? returnedClass,
        IItemRepository? itemRepo = null)
    {
        var mockClassRepo = new Mock<ICharacterClassRepository>();
        mockClassRepo.Setup(r => r.GetById(It.IsAny<string>())).Returns(returnedClass);

        var mockItemRepo = itemRepo is not null
            ? Mock.Get(itemRepo)
            : new Mock<IItemRepository>();
        mockItemRepo.Setup(r => r.GetByTypeAsync(It.IsAny<string>())).ReturnsAsync([]);

        return new GetEquipmentForClassHandler(
            mockClassRepo.Object,
            mockItemRepo.Object,
            NullLogger<GetEquipmentForClassHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenClassNotFound()
    {
        var handler = CreateHandler(returnedClass: null);

        var result = await handler.Handle(new GetEquipmentForClassQuery { ClassId = "warrior:fighter" }, default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("warrior:fighter");
    }

    [Fact]
    public async Task Handle_ReturnsSuccess_WithEmptyLists_WhenRepositoriesEmpty()
    {
        var charClass = new CharacterClass
        {
            Id = "warrior:fighter",
            Name = "Fighter",
            DisplayName = "Fighter",
            WeaponProficiency = ["swords", "axes"],
            ArmorProficiency = ["heavy"]
        };
        var handler = CreateHandler(charClass);

        var result = await handler.Handle(new GetEquipmentForClassQuery { ClassId = "warrior:fighter" }, default);

        result.Success.Should().BeTrue();
        result.ClassName.Should().Be("Fighter");
        result.WeaponProficiencies.Should().BeEquivalentTo(["swords", "axes"]);
        result.ArmorProficiencies.Should().BeEquivalentTo(["heavy"]);
        result.Weapons.Should().BeEmpty();
        result.Armor.Should().BeEmpty();
    }
}
