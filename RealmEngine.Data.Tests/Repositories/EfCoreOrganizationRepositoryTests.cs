using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Repositories;

namespace RealmEngine.Data.Tests.Repositories;

[Trait("Category", "Repository")]
public class EfCoreOrganizationRepositoryTests
{
    private static ContentDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Organization MakeOrg(string slug, string orgType = "guild", bool active = true) =>
        new() { Slug = slug, OrgType = orgType, IsActive = active, DisplayName = slug };

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyActiveOrganizations()
    {
        await using var db = CreateDbContext();
        db.Organizations.AddRange(
            MakeOrg("merchants-guild", active: true),
            MakeOrg("hidden-society", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreOrganizationRepository(db, NullLogger<EfCoreOrganizationRepository>.Instance);

        var result = await repo.GetAllAsync();

        result.Should().HaveCount(1).And.Contain(o => o.Slug == "merchants-guild");
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsEntry_WhenActive()
    {
        await using var db = CreateDbContext();
        db.Organizations.Add(MakeOrg("thieves-guild", "guild"));
        await db.SaveChangesAsync();
        var repo = new EfCoreOrganizationRepository(db, NullLogger<EfCoreOrganizationRepository>.Instance);

        var result = await repo.GetBySlugAsync("thieves-guild");

        result.Should().NotBeNull();
        result!.Slug.Should().Be("thieves-guild");
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsNull_WhenInactive()
    {
        await using var db = CreateDbContext();
        db.Organizations.Add(MakeOrg("disbanded", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreOrganizationRepository(db, NullLogger<EfCoreOrganizationRepository>.Instance);

        (await repo.GetBySlugAsync("disbanded")).Should().BeNull();
    }

    [Fact]
    public async Task GetByTypeAsync_FiltersOnOrgType()
    {
        await using var db = CreateDbContext();
        db.Organizations.AddRange(
            MakeOrg("bakers-guild", "guild"),
            MakeOrg("royal-court", "faction"),
            MakeOrg("warriors-guild", "guild"));
        await db.SaveChangesAsync();
        var repo = new EfCoreOrganizationRepository(db, NullLogger<EfCoreOrganizationRepository>.Instance);

        var result = await repo.GetByTypeAsync("guild");

        result.Should().HaveCount(2).And.OnlyContain(o => o.OrgType == "guild");
    }
}
