using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.CharacterCreation.Commands;
using RealmEngine.Core.Features.Equipment.Queries;
using RealmEngine.Core.Features.Exploration.Queries;
using RealmEngine.Core.Features.Progression.Services;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

using SpeciesModel = RealmEngine.Shared.Models.Species;

namespace RealmEngine.Core.Tests.Features.CharacterCreation.Commands;

/// <summary>
/// Covers the optional halves of <see cref="CreateCharacterHandler"/> the base suite does not reach:
/// background/species bonuses and their failure fallbacks, starting-location assignment, the armour
/// slot mapper, the preferred weapon/armour selection and a failed starting-spell initialisation.
/// </summary>
[Trait("Category", "Feature")]
public class CreateCharacterHandlerExtendedTests
{
    private static (CreateCharacterHandler Handler, Mock<IMediator> Mediator, Mock<IBackgroundRepository> Backgrounds, Mock<ISpeciesRepository> Species) CreateHandler()
    {
        var mediator = new Mock<IMediator>();
        var backgrounds = new Mock<IBackgroundRepository>();
        var species = new Mock<ISpeciesRepository>();

        mediator.Setup(m => m.Send(It.IsAny<InitializeStartingAbilitiesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitializeStartingAbilitiesResult
            {
                Success = true,
                AbilitiesLearned = 2,
                AbilityIds = ["strike", "guard"],
                Message = "ok"
            });
        mediator.Setup(m => m.Send(It.IsAny<InitializeStartingPowersCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitializeStartingPowersResult
            {
                Success = true,
                PowersLearned = 1,
                PowerIds = ["spark"],
                Message = "ok"
            });
        mediator.Setup(m => m.Send(It.IsAny<GetEquipmentForClassQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetEquipmentForClassResult { Success = true });
        mediator.Setup(m => m.Send(It.IsAny<GetStartingLocationsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Location>());

        var skillRepository = new Mock<ISkillRepository>();
        skillRepository.Setup(r => r.GetAllAsync()).ReturnsAsync([]);
        var skillDataService = new SkillDataService(skillRepository.Object);
        skillDataService.InitializeAsync().GetAwaiter().GetResult();
        var skillProgression = new SkillProgressionService(skillDataService);

        return (
            new CreateCharacterHandler(
                mediator.Object,
                backgrounds.Object,
                species.Object,
                skillProgression,
                NullLogger<CreateCharacterHandler>.Instance),
            mediator,
            backgrounds,
            species);
    }

    private static CharacterClass MakeClass() => new()
    {
        Name = "Warrior",
        Slug = "warriors:fighter",
        StartingHealth = 12,
        StartingMana = 4
    };

    private static CreateCharacterCommand MakeCommand(
        string? backgroundId = null,
        string? speciesSlug = null,
        string? startingLocationId = null) => new()
        {
            CharacterName = "TestHero",
            CharacterClass = MakeClass(),
            BackgroundId = backgroundId,
            SpeciesSlug = speciesSlug,
            StartingLocationId = startingLocationId
        };

    private static Background MakeBackground() => new()
    {
        Slug = "soldier",
        Name = "Soldier",
        PrimaryAttribute = "strength",
        PrimaryBonus = 2,
        SecondaryAttribute = "constitution",
        SecondaryBonus = 1
    };

    private static SpeciesModel MakeSpecies() => new()
    {
        Slug = "elf",
        DisplayName = "Elf",
        BonusDexterity = 3,
        BonusIntelligence = 1
    };

    [Fact]
    public async Task Handle_AppliesBackgroundAndSpeciesBonuses()
    {
        var (handler, _, backgrounds, species) = CreateHandler();
        backgrounds.Setup(r => r.GetBackgroundByIdAsync("soldier")).ReturnsAsync(MakeBackground());
        species.Setup(r => r.GetSpeciesBySlugAsync("elf")).ReturnsAsync(MakeSpecies());

        var result = await handler.Handle(MakeCommand(backgroundId: "soldier", speciesSlug: "elf"), default);

        result.Success.Should().BeTrue();
        result.BackgroundApplied.Should().NotBeNull();
        result.Character!.BackgroundId.Should().Be("backgrounds/strength:soldier");
        result.Character.SpeciesSlug.Should().Be("elf");
        result.Character.Strength.Should().Be(10, "8 point-buy base + 2 background");
        result.Character.Constitution.Should().Be(9, "8 point-buy base + 1 background");
        result.Character.Dexterity.Should().Be(11, "8 point-buy base + 3 species");
        result.Character.Intelligence.Should().Be(9, "8 point-buy base + 1 species");
    }

    [Fact]
    public async Task Handle_WhenBackgroundIsMissing_StillCreatesTheCharacter()
    {
        var (handler, _, backgrounds, _) = CreateHandler();
        backgrounds.Setup(r => r.GetBackgroundByIdAsync(It.IsAny<string>())).ReturnsAsync((Background?)null);

        var result = await handler.Handle(MakeCommand(backgroundId: "unknown"), default);

        result.Success.Should().BeTrue();
        result.BackgroundApplied.Should().BeNull();
        result.Character!.BackgroundId.Should().BeNull();
        result.Character.Strength.Should().Be(8);
    }

    [Fact]
    public async Task Handle_WhenBackgroundRepositoryThrows_SwallowsTheError()
    {
        var (handler, _, backgrounds, _) = CreateHandler();
        backgrounds.Setup(r => r.GetBackgroundByIdAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("bg store down"));

        var result = await handler.Handle(MakeCommand(backgroundId: "soldier"), default);

        result.Success.Should().BeTrue();
        result.BackgroundApplied.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenSpeciesIsMissingOrFails_StillCreatesTheCharacter()
    {
        var (handler, _, _, species) = CreateHandler();
        species.Setup(r => r.GetSpeciesBySlugAsync("missing")).ReturnsAsync((SpeciesModel?)null);
        species.Setup(r => r.GetSpeciesBySlugAsync("broken")).ThrowsAsync(new InvalidOperationException("species store down"));

        var missing = await handler.Handle(MakeCommand(speciesSlug: "missing"), default);
        var broken = await handler.Handle(MakeCommand(speciesSlug: "broken"), default);

        missing.Success.Should().BeTrue();
        missing.Character!.SpeciesSlug.Should().BeNull();
        missing.Character.Dexterity.Should().Be(8);
        broken.Success.Should().BeTrue();
        broken.Character!.SpeciesSlug.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenSpellInitializationFails_LogsAndContinues()
    {
        var (handler, mediator, _, _) = CreateHandler();
        mediator.Setup(m => m.Send(It.IsAny<InitializeStartingPowersCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitializeStartingPowersResult { Success = false, Message = "no tradition" });

        var result = await handler.Handle(MakeCommand(), default);

        result.Success.Should().BeTrue();
        result.SpellsLearned.Should().Be(0);
        result.AbilitiesLearned.Should().Be(2);
    }

    [Fact]
    public async Task Handle_AssignsStartingLocationByIdOrName()
    {
        var (handler, mediator, _, _) = CreateHandler();
        mediator.Setup(m => m.Send(It.IsAny<GetStartingLocationsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new Location { Id = "towns:oakvale", Name = "Oakvale", Type = "towns", Description = "" },
                new Location { Id = "dungeons:crypt", Name = "Old Crypt", Type = "dungeons", Description = "" }
            ]);

        var result = await handler.Handle(MakeCommand(startingLocationId: "OAKVALE"), default);

        result.Success.Should().BeTrue();
        result.StartingLocation!.Id.Should().Be("towns:oakvale");
        result.Character!.CurrentLocationId.Should().Be("towns:oakvale");
        result.Character.CurrentZone.Should().Be("Oakvale");
    }

    [Fact]
    public async Task Handle_WithUnknownStartingLocation_LeavesLocationUnset()
    {
        var (handler, mediator, _, _) = CreateHandler();
        mediator.Setup(m => m.Send(It.IsAny<GetStartingLocationsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Location { Id = "towns:oakvale", Name = "Oakvale", Type = "towns", Description = "" }]);

        var result = await handler.Handle(MakeCommand(startingLocationId: "nowhere"), default);

        result.Success.Should().BeTrue();
        result.StartingLocation.Should().BeNull();
        result.Character!.CurrentLocationId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenLocationQueryThrows_LeavesLocationUnset()
    {
        var (handler, mediator, _, _) = CreateHandler();
        mediator.Setup(m => m.Send(It.IsAny<GetStartingLocationsQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("location store down"));

        var result = await handler.Handle(MakeCommand(startingLocationId: "oakvale"), default);

        result.Success.Should().BeTrue();
        result.StartingLocation.Should().BeNull();
    }

    public static TheoryData<string, string> ArmorSlots => new()
    {
        { "helmet", nameof(Character.EquippedHelmet) },
        { "chest", nameof(Character.EquippedChest) },
        { "legs", nameof(Character.EquippedLegs) },
        { "boots", nameof(Character.EquippedBoots) },
        { "gloves", nameof(Character.EquippedGloves) },
        { "shoulders", nameof(Character.EquippedShoulders) },
        { "belt", nameof(Character.EquippedBelt) },
        { "bracers", nameof(Character.EquippedBracers) },
        { "mystery-piece", nameof(Character.EquippedChest) }
    };

    [Theory]
    [MemberData(nameof(ArmorSlots))]
    public async Task Handle_EquipsStartingArmorIntoTheMatchingSlot(string armorType, string expectedSlot)
    {
        var (handler, mediator, _, _) = CreateHandler();
        var armor = new Item { Name = "Starter Armor", ArmorType = armorType, Type = ItemType.Chest };
        mediator.Setup(m => m.Send(It.IsAny<GetEquipmentForClassQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetEquipmentForClassResult { Success = true, Armor = [armor] });

        var result = await handler.Handle(MakeCommand(), default);

        result.Success.Should().BeTrue();
        var character = result.Character!;
        var equipped = expectedSlot switch
        {
            nameof(Character.EquippedHelmet) => character.EquippedHelmet,
            nameof(Character.EquippedChest) => character.EquippedChest,
            nameof(Character.EquippedLegs) => character.EquippedLegs,
            nameof(Character.EquippedBoots) => character.EquippedBoots,
            nameof(Character.EquippedGloves) => character.EquippedGloves,
            nameof(Character.EquippedShoulders) => character.EquippedShoulders,
            nameof(Character.EquippedBelt) => character.EquippedBelt,
            nameof(Character.EquippedBracers) => character.EquippedBracers,
            _ => null
        };
        equipped.Should().BeSameAs(armor);
        character.Inventory.Should().Contain(armor);
        result.EquipmentSelected.Should().Contain(armor);
    }

    [Fact]
    public async Task Handle_PrefersRequestedWeaponAndArmorTypes()
    {
        var (handler, mediator, _, _) = CreateHandler();
        var axe = new Item { Name = "Battle Axe", WeaponType = "axe", Type = ItemType.Weapon };
        var sword = new Item { Name = "Short Sword", WeaponType = "sword", Type = ItemType.Weapon };
        var plate = new Item { Name = "Plate", ArmorType = "chest", TypeKey = "plate", Type = ItemType.Chest };
        var robe = new Item { Name = "Robe", ArmorType = "robe", TypeKey = "cloth", Type = ItemType.Chest };
        mediator.Setup(m => m.Send(It.IsAny<GetEquipmentForClassQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetEquipmentForClassResult
            {
                Success = true,
                Weapons = [axe, sword],
                Armor = [robe, plate]
            });

        var command = MakeCommand();
        command = command with { PreferredWeaponType = "sword", PreferredArmorType = "plate" };
        var result = await handler.Handle(command, default);

        result.Success.Should().BeTrue();
        result.Character!.EquippedMainHand.Should().BeSameAs(sword);
        result.Character.EquippedChest.Should().BeSameAs(plate);
    }

    [Fact]
    public async Task Handle_FallsBackToFirstWeaponAndArmor_WhenPreferencesDoNotMatch()
    {
        var (handler, mediator, _, _) = CreateHandler();
        var axe = new Item { Name = "Battle Axe", WeaponType = "axe", Type = ItemType.Weapon };
        var robe = new Item { Name = "Robe", ArmorType = "robe", Type = ItemType.Chest };
        mediator.Setup(m => m.Send(It.IsAny<GetEquipmentForClassQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetEquipmentForClassResult { Success = true, Weapons = [axe], Armor = [robe] });

        var command = MakeCommand() with { PreferredWeaponType = "halberd", PreferredArmorType = "plate" };
        var result = await handler.Handle(command, default);

        result.Character!.EquippedMainHand.Should().BeSameAs(axe);
        result.Character.EquippedChest.Should().BeSameAs(robe);
    }

    [Fact]
    public async Task Handle_GrantsShieldInOffHand_WhenOnlyShieldsAreInTheArmorList()
    {
        var (handler, mediator, _, _) = CreateHandler();
        var shield = new Item { Name = "Buckler", ArmorType = "shield", TypeKey = "shield", Type = ItemType.Shield };
        mediator.Setup(m => m.Send(It.IsAny<GetEquipmentForClassQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetEquipmentForClassResult { Success = true, Armor = [shield] });

        var command = MakeCommand() with { IncludeShield = true };
        var result = await handler.Handle(command, default);

        result.Character!.EquippedOffHand.Should().BeSameAs(shield);
        result.Character.EquippedChest.Should().BeNull("shields must never fill an armour slot");
    }

    [Fact]
    public async Task Handle_WhenEquipmentQueryFails_ReturnsNoEquipment()
    {
        var (handler, mediator, _, _) = CreateHandler();
        mediator.Setup(m => m.Send(It.IsAny<GetEquipmentForClassQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetEquipmentForClassResult { Success = false, ErrorMessage = "no catalog" });

        var result = await handler.Handle(MakeCommand(), default);

        result.Success.Should().BeTrue();
        result.EquipmentSelected.Should().BeEmpty();
        result.Character!.EquippedMainHand.Should().BeNull();
    }
}
