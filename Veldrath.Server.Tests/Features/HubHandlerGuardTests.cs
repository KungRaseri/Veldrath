using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Characters;
using Veldrath.Server.Features.Zones;
using Character = Veldrath.Server.Data.Entities.Character;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Tests for hub-handler guards and blob fallbacks that sit outside the happy path: the unlock command's two
/// rejections, the allocation command's value guard and corrupt blob fallback, and the ability command's corrupt
/// blob fallback.
/// </summary>
public class HubHandlerGuardTests
{
    private static Character Character(string attributes = "{}") =>
        new()
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            Name = "TestHero",
            ClassName = "Warrior",
            SlotIndex = 1,
            Level = 1,
            Attributes = attributes,
            InventoryBlob = "[]",
            AbilitiesBlob = """["fireball"]""",
        };

    private static Mock<ICharacterRepository> CharacterRepo(Character character)
    {
        var repo = new Mock<ICharacterRepository>();
        repo.Setup(r => r.GetByIdAsync(character.Id, It.IsAny<CancellationToken>())).ReturnsAsync(character);
        repo.Setup(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return repo;
    }

    // ── UnlockZoneLocation ─────────────────────────────────────────────────────

    [Fact]
    public async Task UnlockZoneLocation_RejectsABlankSlug()
    {
        var locations = new Mock<IZoneLocationRepository>();
        var handler = new UnlockZoneLocationHubCommandHandler(
            locations.Object,
            Mock.Of<ICharacterUnlockedLocationRepository>(),
            NullLogger<UnlockZoneLocationHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new UnlockZoneLocationHubCommand(Guid.NewGuid(), "  ", "quest"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Location slug cannot be empty");
        locations.Verify(r => r.GetBySlugAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UnlockZoneLocation_RejectsAnUnknownLocation()
    {
        var locations = new Mock<IZoneLocationRepository>();
        locations.Setup(r => r.GetBySlugAsync("nowhere")).ReturnsAsync((ZoneLocationEntry?)null);
        var handler = new UnlockZoneLocationHubCommandHandler(
            locations.Object,
            Mock.Of<ICharacterUnlockedLocationRepository>(),
            NullLogger<UnlockZoneLocationHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new UnlockZoneLocationHubCommand(Guid.NewGuid(), "nowhere", "quest"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Location 'nowhere' does not exist");
        result.LocationSlug.Should().BeNull();
    }

    // ── AllocateAttributePoints ────────────────────────────────────────────────

    [Fact]
    public async Task AllocateAttributePoints_RejectsNonPositiveValues()
    {
        var handler = new AllocateAttributePointsHubCommandHandler(
            Mock.Of<ICharacterRepository>(),
            NullLogger<AllocateAttributePointsHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new AllocateAttributePointsHubCommand
            {
                CharacterId = Guid.NewGuid(),
                Allocations = new Dictionary<string, int> { ["Strength"] = 0 },
            },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("All attribute allocation values must be positive.");
    }

    [Fact]
    public async Task AllocateAttributePoints_TreatsACorruptAttributesBlobAsEmpty()
    {
        var character = Character(attributes: "{not json");
        var handler = new AllocateAttributePointsHubCommandHandler(
            CharacterRepo(character).Object,
            NullLogger<AllocateAttributePointsHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new AllocateAttributePointsHubCommand
            {
                CharacterId = character.Id,
                Allocations = new Dictionary<string, int> { ["Strength"] = 1 },
            },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Not enough unspent attribute points");
        character.Attributes.Should().Be("{not json", "a rejected allocation must not rewrite the blob");
    }

    // ── UseAbility ────────────────────────────────────────────────────────────

    [Fact]
    public async Task UseAbility_TreatsACorruptAttributesBlobAsEmpty()
    {
        var character = Character(attributes: "{not json");
        var handler = new UseAbilityHubCommandHandler(
            CharacterRepo(character).Object,
            NullLogger<UseAbilityHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new UseAbilityHubCommand { CharacterId = character.Id, AbilityId = "fireball" },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("mana");
    }
}
