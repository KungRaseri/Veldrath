using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Repositories;

namespace RealmEngine.Data.Tests.Repositories;

/// <summary>
/// Tests for the trait-derived weapon proficiencies and disposal of
/// <see cref="EfCoreCharacterClassRepository"/>: a class that can stealth or dual-wield must advertise the
/// light-blade tokens the equipment handler looks up.
/// </summary>
[Trait("Category", "Repository")]
public class EfCoreCharacterClassRepositoryTraitTests
{
    private static ContentDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public void GetAll_DerivesLightBladeProficienciesFromStealthAndDualWieldTraits()
    {
        using var db = CreateDbContext();
        db.ActorClasses.Add(new ActorClass
        {
            Slug = "shadowblade",
            TypeKey = "rogues",
            DisplayName = "Shadowblade",
            IsActive = true,
            PrimaryStat = "dexterity",
            Traits = new ActorClassTraits { Stealth = true, CanDualWield = true, Melee = true },
        });
        db.SaveChanges();
        var repo = new EfCoreCharacterClassRepository(db, NullLogger<EfCoreCharacterClassRepository>.Instance);

        var characterClass = repo.GetAll().Should().ContainSingle().Subject;

        characterClass.WeaponProficiency.Should().Contain(["swords", "shortswords", "rapiers"]);
        characterClass.ArmorProficiency.Should().Contain("light");
        characterClass.Traits["stealth"].Should().Be(true);
    }

    [Fact]
    public void Dispose_ClearsTheCachedClasses()
    {
        using var db = CreateDbContext();
        db.ActorClasses.Add(new ActorClass
        {
            Slug = "warrior",
            TypeKey = "fighters",
            DisplayName = "Warrior",
            IsActive = true,
            PrimaryStat = "strength",
        });
        db.SaveChanges();
        var repo = new EfCoreCharacterClassRepository(db, NullLogger<EfCoreCharacterClassRepository>.Instance);
        repo.GetAll().Should().HaveCount(1);

        var act = () => repo.Dispose();

        act.Should().NotThrow();
    }
}
