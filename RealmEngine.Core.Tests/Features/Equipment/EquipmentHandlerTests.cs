using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Equipment.Commands;
using RealmEngine.Core.Features.Equipment.Queries;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Equipment;

[Trait("Category", "Feature")]
public class EquipItemHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsFailure_WithSaveSystemIntegrationMessage()
    {
        var handler = new EquipItemHandler();
        var result = await handler.Handle(new EquipItemCommand("char-01", "sword-01", EquipmentSlot.MainHand), default);

        result.Success.Should().BeFalse();
        result.Message.Should().NotBeNullOrWhiteSpace();
    }
}

[Trait("Category", "Feature")]
public class GetEquipmentForClassHandlerTests
{
    private static ContentDbContext CreateInMemoryDb()
    {
        var opts = new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase($"equipment-test-{Guid.NewGuid()}")
            .Options;
        return new ContentDbContext(opts);
    }

    private static GetEquipmentForClassHandler CreateHandler(
        CharacterClass? returnedClass,
        ContentDbContext? db = null)
    {
        var mockClassRepo = new Mock<ICharacterClassRepository>();
        mockClassRepo.Setup(r => r.GetById(It.IsAny<string>())).Returns(returnedClass);

        var mockDbFactory = new Mock<IDbContextFactory<ContentDbContext>>();
        if (db is not null)
            mockDbFactory.Setup(f => f.CreateDbContext()).Returns(db);

        return new GetEquipmentForClassHandler(
            mockClassRepo.Object,
            mockDbFactory.Object,
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
    public async Task Handle_ReturnsSuccess_WithEmptyLists_WhenDbHasNoItems()
    {
        var charClass = new CharacterClass
        {
            Id = "warrior:fighter",
            Name = "Fighter",
            DisplayName = "Fighter",
            WeaponProficiency = ["swords", "axes"],
            ArmorProficiency = ["heavy"]
        };
        using var db = CreateInMemoryDb();
        var handler = CreateHandler(charClass, db);

        var result = await handler.Handle(new GetEquipmentForClassQuery { ClassId = "warrior:fighter" }, default);

        result.Success.Should().BeTrue();
        result.ClassName.Should().Be("Fighter");
        result.WeaponProficiencies.Should().BeEquivalentTo(["swords", "axes"]);
        result.ArmorProficiencies.Should().BeEquivalentTo(["heavy"]);
        result.Weapons.Should().BeEmpty();
        result.Armor.Should().BeEmpty();
    }
}
