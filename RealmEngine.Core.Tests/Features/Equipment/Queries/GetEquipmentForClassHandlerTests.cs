using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Equipment.Queries;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Equipment.Queries;

[Trait("Category", "Feature")]
public class GetEquipmentForClassHandlerTests
{
    private static Item MakeWeapon(string typeKey, string slug) => new()
    {
        Id = $"weapon:{typeKey}:{slug}",
        Slug = slug,
        Name = slug,
        Type = ItemType.Weapon,
        TypeKey = typeKey,
    };

    private static Item MakeArmor(string typeKey, string slug) => new()
    {
        Id = $"armor:{typeKey}:{slug}",
        Slug = slug,
        Name = slug,
        Type = typeKey.Equals("shield", StringComparison.OrdinalIgnoreCase) ? ItemType.Shield : ItemType.Chest,
        TypeKey = typeKey,
    };

    private static CharacterClass MakeClass(string id, List<string> weaponProfs, List<string> armorProfs) =>
        new() { Id = id, Name = $"{id}-class", WeaponProficiency = weaponProfs, ArmorProficiency = armorProfs };

    private static GetEquipmentForClassHandler BuildHandler(
        Mock<ICharacterClassRepository> classRepo,
        Mock<IWeaponRepository> weaponRepo,
        Mock<IArmorRepository> armorRepo) =>
        new(classRepo.Object, weaponRepo.Object, armorRepo.Object,
            NullLogger<GetEquipmentForClassHandler>.Instance);

    // ── Class not found ─────────────────────────────────────────────

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Class_Not_Found()
    {
        var classRepo = new Mock<ICharacterClassRepository>();
        classRepo.Setup(r => r.GetById("unknown")).Returns((CharacterClass?)null);
        var handler = BuildHandler(classRepo, new Mock<IWeaponRepository>(), new Mock<IArmorRepository>());

        var result = await handler.Handle(
            new GetEquipmentForClassQuery { ClassId = "unknown" }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("unknown");
    }

    // ── Result shape ─────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Should_Return_ClassName_And_Proficiencies_In_Result()
    {
        var classRepo = new Mock<ICharacterClassRepository>();
        classRepo.Setup(r => r.GetById("warrior")).Returns(
            MakeClass("warrior", ["swords"], ["heavy"]));
        var weaponRepo = new Mock<IWeaponRepository>();
        weaponRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([]);
        var armorRepo = new Mock<IArmorRepository>();
        armorRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([]);
        var handler = BuildHandler(classRepo, weaponRepo, armorRepo);

        var result = await handler.Handle(
            new GetEquipmentForClassQuery { ClassId = "warrior" }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ClassName.Should().Be("warrior-class");
        result.WeaponProficiencies.Should().Contain("swords");
        result.ArmorProficiencies.Should().Contain("heavy");
    }

    // ── Weapon proficiency filtering ─────────────────────────────────

    [Fact]
    public async Task Handle_Should_Return_Weapons_Matching_Proficiency()
    {
        var classRepo = new Mock<ICharacterClassRepository>();
        classRepo.Setup(r => r.GetById("warrior")).Returns(
            MakeClass("warrior", ["swords"], []));
        var weaponRepo = new Mock<IWeaponRepository>();
        weaponRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([
            MakeWeapon("heavy-blades", "longsword"),   // "heavy-blades" → swords ✓
            MakeWeapon("bows",         "shortbow"),    // "bows" → bows  ✗
            MakeWeapon("axes",         "battleaxe"),   // "axes" → axes  ✗
        ]);
        var armorRepo = new Mock<IArmorRepository>();
        armorRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([]);
        var handler = BuildHandler(classRepo, weaponRepo, armorRepo);

        var result = await handler.Handle(
            new GetEquipmentForClassQuery { ClassId = "warrior", EquipmentType = "weapons" }, CancellationToken.None);

        result.Weapons.Should().ContainSingle();
        result.Weapons[0].Slug.Should().Be("longsword");
    }

    [Fact]
    public async Task Handle_Should_Return_All_Weapons_When_Class_Has_All_Proficiency()
    {
        var classRepo = new Mock<ICharacterClassRepository>();
        classRepo.Setup(r => r.GetById("godclass")).Returns(
            MakeClass("godclass", ["all"], []));
        var weaponRepo = new Mock<IWeaponRepository>();
        weaponRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([
            MakeWeapon("heavy-blades", "longsword"),
            MakeWeapon("bows",         "shortbow"),
            MakeWeapon("staves",       "oak-staff"),
        ]);
        var armorRepo = new Mock<IArmorRepository>();
        armorRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([]);
        var handler = BuildHandler(classRepo, weaponRepo, armorRepo);

        var result = await handler.Handle(
            new GetEquipmentForClassQuery { ClassId = "godclass", EquipmentType = "weapons" }, CancellationToken.None);

        result.Weapons.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_Should_Exclude_Weapons_With_Null_TypeKey()
    {
        var classRepo = new Mock<ICharacterClassRepository>();
        classRepo.Setup(r => r.GetById("warrior")).Returns(
            MakeClass("warrior", ["swords"], []));
        var weaponRepo = new Mock<IWeaponRepository>();
        weaponRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([
            MakeWeapon("heavy-blades", "longsword"),
            new Item { TypeKey = null, Slug = "mystery-blade", Type = ItemType.Weapon },
        ]);
        var armorRepo = new Mock<IArmorRepository>();
        armorRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([]);
        var handler = BuildHandler(classRepo, weaponRepo, armorRepo);

        var result = await handler.Handle(
            new GetEquipmentForClassQuery { ClassId = "warrior", EquipmentType = "weapons" }, CancellationToken.None);

        result.Weapons.Should().ContainSingle(w => w.Slug == "longsword");
    }

    // ── Armor proficiency filtering ───────────────────────────────────

    [Fact]
    public async Task Handle_Should_Return_Armor_Matching_Proficiency()
    {
        var classRepo = new Mock<ICharacterClassRepository>();
        classRepo.Setup(r => r.GetById("rogue")).Returns(
            MakeClass("rogue", [], ["light"]));
        var weaponRepo = new Mock<IWeaponRepository>();
        weaponRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([]);
        var armorRepo = new Mock<IArmorRepository>();
        armorRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([
            MakeArmor("light",  "leather-armor"),  // "light" → light ✓
            MakeArmor("heavy",  "plate-mail"),     // "heavy" → heavy ✗
            MakeArmor("shield", "buckler"),        // "shield" → shields ✗
        ]);
        var handler = BuildHandler(classRepo, weaponRepo, armorRepo);

        var result = await handler.Handle(
            new GetEquipmentForClassQuery { ClassId = "rogue", EquipmentType = "armor" }, CancellationToken.None);

        result.Armor.Should().ContainSingle();
        result.Armor[0].Slug.Should().Be("leather-armor");
    }

    [Fact]
    public async Task Handle_Should_Match_Shield_Proficiency_Via_Shields_Key()
    {
        // "shield" TypeKey maps to proficiency key "shields" (plural)
        var classRepo = new Mock<ICharacterClassRepository>();
        classRepo.Setup(r => r.GetById("paladin")).Returns(
            MakeClass("paladin", [], ["shields"]));
        var weaponRepo = new Mock<IWeaponRepository>();
        weaponRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([]);
        var armorRepo = new Mock<IArmorRepository>();
        armorRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([
            MakeArmor("shield", "tower-shield"),
            MakeArmor("heavy",  "plate-mail"),
        ]);
        var handler = BuildHandler(classRepo, weaponRepo, armorRepo);

        var result = await handler.Handle(
            new GetEquipmentForClassQuery { ClassId = "paladin", EquipmentType = "armor" }, CancellationToken.None);

        result.Armor.Should().ContainSingle(a => a.Slug == "tower-shield");
    }

    // ── EquipmentType routing ─────────────────────────────────────────

    [Fact]
    public async Task Handle_Should_Query_Both_Repos_When_EquipmentType_Is_Null()
    {
        var classRepo = new Mock<ICharacterClassRepository>();
        classRepo.Setup(r => r.GetById("paladin")).Returns(
            MakeClass("paladin", ["swords"], ["heavy"]));
        var weaponRepo = new Mock<IWeaponRepository>();
        weaponRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([MakeWeapon("heavy-blades", "longsword")]);
        var armorRepo = new Mock<IArmorRepository>();
        armorRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([MakeArmor("heavy", "plate-mail")]);
        var handler = BuildHandler(classRepo, weaponRepo, armorRepo);

        var result = await handler.Handle(
            new GetEquipmentForClassQuery { ClassId = "paladin", EquipmentType = null }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Weapons.Should().ContainSingle();
        result.Armor.Should().ContainSingle();
        weaponRepo.Verify(r => r.GetAllAsync(), Times.Once);
        armorRepo.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Skip_Armor_Repo_When_EquipmentType_Is_Weapons()
    {
        var classRepo = new Mock<ICharacterClassRepository>();
        classRepo.Setup(r => r.GetById("archer")).Returns(
            MakeClass("archer", ["bows"], ["light"]));
        var weaponRepo = new Mock<IWeaponRepository>();
        weaponRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([MakeWeapon("bows", "longbow")]);
        var armorRepo = new Mock<IArmorRepository>();
        var handler = BuildHandler(classRepo, weaponRepo, armorRepo);

        var result = await handler.Handle(
            new GetEquipmentForClassQuery { ClassId = "archer", EquipmentType = "weapons" }, CancellationToken.None);

        result.Weapons.Should().ContainSingle();
        result.Armor.Should().BeEmpty();
        armorRepo.Verify(r => r.GetAllAsync(), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Skip_Weapon_Repo_When_EquipmentType_Is_Armor()
    {
        var classRepo = new Mock<ICharacterClassRepository>();
        classRepo.Setup(r => r.GetById("guardian")).Returns(
            MakeClass("guardian", ["swords"], ["heavy", "shields"]));
        var weaponRepo = new Mock<IWeaponRepository>();
        var armorRepo = new Mock<IArmorRepository>();
        armorRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([
            MakeArmor("heavy",  "plate-mail"),
            MakeArmor("shield", "tower-shield"),
        ]);
        var handler = BuildHandler(classRepo, weaponRepo, armorRepo);

        var result = await handler.Handle(
            new GetEquipmentForClassQuery { ClassId = "guardian", EquipmentType = "armor" }, CancellationToken.None);

        result.Armor.Should().HaveCount(2);
        result.Weapons.Should().BeEmpty();
        weaponRepo.Verify(r => r.GetAllAsync(), Times.Never);
    }

    // ── MaxItemsPerCategory ───────────────────────────────────────────

    [Fact]
    public async Task Handle_Should_Respect_MaxItemsPerCategory_Limit()
    {
        var classRepo = new Mock<ICharacterClassRepository>();
        classRepo.Setup(r => r.GetById("mage")).Returns(
            MakeClass("mage", ["all"], []));
        var weaponRepo = new Mock<IWeaponRepository>();
        weaponRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([
            MakeWeapon("staves", "oak-staff"),
            MakeWeapon("wands",  "oak-wand"),
            MakeWeapon("staves", "golden-staff"),
            MakeWeapon("staves", "silver-staff"),
            MakeWeapon("wands",  "crystal-wand"),
        ]);
        var armorRepo = new Mock<IArmorRepository>();
        armorRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([]);
        var handler = BuildHandler(classRepo, weaponRepo, armorRepo);

        var result = await handler.Handle(
            new GetEquipmentForClassQuery { ClassId = "mage", EquipmentType = "weapons", MaxItemsPerCategory = 2 },
            CancellationToken.None);

        result.Weapons.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_Should_Return_All_Items_When_MaxItemsPerCategory_Is_Zero()
    {
        var classRepo = new Mock<ICharacterClassRepository>();
        classRepo.Setup(r => r.GetById("mage")).Returns(
            MakeClass("mage", ["all"], []));
        var weaponRepo = new Mock<IWeaponRepository>();
        weaponRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([
            MakeWeapon("staves", "oak-staff"),
            MakeWeapon("wands",  "oak-wand"),
            MakeWeapon("staves", "golden-staff"),
        ]);
        var armorRepo = new Mock<IArmorRepository>();
        armorRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([]);
        var handler = BuildHandler(classRepo, weaponRepo, armorRepo);

        var result = await handler.Handle(
            new GetEquipmentForClassQuery { ClassId = "mage", EquipmentType = "weapons", MaxItemsPerCategory = 0 },
            CancellationToken.None);

        result.Weapons.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_Should_Respect_MaxItemsPerCategory_When_RandomizeSelection_Is_True()
    {
        var classRepo = new Mock<ICharacterClassRepository>();
        classRepo.Setup(r => r.GetById("ranger")).Returns(
            MakeClass("ranger", ["all"], []));
        var weaponRepo = new Mock<IWeaponRepository>();
        weaponRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([
            MakeWeapon("bows",       "longbow"),
            MakeWeapon("crossbows",  "light-crossbow"),
            MakeWeapon("light-blades", "shortsword"),
            MakeWeapon("bows",       "shortbow"),
            MakeWeapon("crossbows",  "heavy-crossbow"),
        ]);
        var armorRepo = new Mock<IArmorRepository>();
        armorRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([]);
        var handler = BuildHandler(classRepo, weaponRepo, armorRepo);

        var result = await handler.Handle(
            new GetEquipmentForClassQuery
            {
                ClassId = "ranger",
                EquipmentType = "weapons",
                MaxItemsPerCategory = 3,
                RandomizeSelection = true,
            },
            CancellationToken.None);

        result.Weapons.Should().HaveCount(3);
    }

    // ── Exception handling ────────────────────────────────────────────

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Repository_Throws()
    {
        var classRepo = new Mock<ICharacterClassRepository>();
        classRepo.Setup(r => r.GetById("warrior")).Returns(
            MakeClass("warrior", ["swords"], []));
        var weaponRepo = new Mock<IWeaponRepository>();
        weaponRepo.Setup(r => r.GetAllAsync()).ThrowsAsync(new InvalidOperationException("DB unavailable"));
        var armorRepo = new Mock<IArmorRepository>();
        armorRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([]);
        var handler = BuildHandler(classRepo, weaponRepo, armorRepo);

        var result = await handler.Handle(
            new GetEquipmentForClassQuery { ClassId = "warrior" }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("DB unavailable");
    }

    [Fact]
    public async Task Handle_Should_Return_Empty_Lists_When_No_Items_Match_Proficiencies()
    {
        var classRepo = new Mock<ICharacterClassRepository>();
        classRepo.Setup(r => r.GetById("monk")).Returns(
            MakeClass("monk", ["unarmed"], ["cloth"]));   // proficiences not in any TypeKey mapping
        var weaponRepo = new Mock<IWeaponRepository>();
        weaponRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([
            MakeWeapon("heavy-blades", "longsword"),
            MakeWeapon("bows",         "shortbow"),
        ]);
        var armorRepo = new Mock<IArmorRepository>();
        armorRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([
            MakeArmor("heavy", "plate-mail"),
        ]);
        var handler = BuildHandler(classRepo, weaponRepo, armorRepo);

        var result = await handler.Handle(
            new GetEquipmentForClassQuery { ClassId = "monk" }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Weapons.Should().BeEmpty();
        result.Armor.Should().BeEmpty();
    }
}
