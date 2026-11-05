using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Reputation.Services;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Models;

using DataOrganization = RealmEngine.Data.Entities.Organization;
using FactionModel = RealmEngine.Shared.Models.Faction;

namespace RealmEngine.Core.Tests.Features.Reputation.Services;

/// <summary>
/// Tests for <see cref="FactionDataService"/>: mapping content-database organizations of type
/// "faction" onto <see cref="FactionModel"/> models, the type-key to <see cref="FactionType"/> mapping,
/// caching, lookup by slug or display name, and the database-failure fallback.
/// </summary>
[Trait("Category", "Service")]
public class FactionDataServiceTests
{
    private sealed class StubDbContextFactory : IDbContextFactory<ContentDbContext>
    {
        private readonly string _databaseName;
        private readonly Exception? _failure;

        private StubDbContextFactory(string databaseName, Exception? failure = null)
        {
            _databaseName = databaseName;
            _failure = failure;
        }

        public static StubDbContextFactory WithOrganizations(params DataOrganization[] organizations)
        {
            var databaseName = Guid.NewGuid().ToString();
            using var db = CreateDb(databaseName);
            db.Organizations.AddRange(organizations);
            db.SaveChanges();
            return new StubDbContextFactory(databaseName);
        }

        public static StubDbContextFactory Failing() =>
            new(Guid.NewGuid().ToString(), new InvalidOperationException("database offline"));

        public ContentDbContext CreateDbContext()
        {
            if (_failure is not null) throw _failure;
            return CreateDb(_databaseName);
        }

        private static ContentDbContext CreateDb(string databaseName) =>
            new(new DbContextOptionsBuilder<ContentDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options);
    }

    private static DataOrganization MakeOrganization(
        string slug,
        string typeKey,
        string? displayName = null,
        bool isActive = true,
        string orgType = "faction") => new()
        {
            Slug = slug,
            TypeKey = typeKey,
            DisplayName = displayName,
            IsActive = isActive,
            OrgType = orgType
        };

    private static FactionDataService CreateService(IDbContextFactory<ContentDbContext> factory) =>
        new(factory, NullLogger<FactionDataService>.Instance);

    private static FactionDataService CreateSeededService() =>
        CreateService(StubDbContextFactory.WithOrganizations(
            MakeOrganization("thieves-guild", "criminal", "Thieves Guild"),
            MakeOrganization("order-of-light", "religious", "Order of Light"),
            MakeOrganization("north-kingdom", "political", "North Kingdom"),
            MakeOrganization("old-kingdom", "kingdom", "Old Kingdom"),
            MakeOrganization("merchants", "trade", "Merchants Guild"),
            MakeOrganization("inactive-faction", "criminal", "Retired", isActive: false),
            MakeOrganization("smithy", "trade", orgType: "business")));

    [Fact]
    public void LoadFactions_ReturnsOnlyActiveFactionOrganizations()
    {
        var service = CreateSeededService();

        var factions = service.LoadFactions();

        factions.Select(f => f.Id).Should().BeEquivalentTo(
            ["thieves-guild", "order-of-light", "north-kingdom", "old-kingdom", "merchants"]);
        factions.Should().NotContain(f => f.Id == "inactive-faction", "inactive organizations are excluded");
        factions.Should().NotContain(f => f.Id == "smithy", "non-faction organizations are excluded");
    }

    [Fact]
    public void LoadFactions_MapsNamesAndTypeKeys()
    {
        var service = CreateSeededService();

        var factions = service.LoadFactions();

        var thieves = factions.Single(f => f.Id == "thieves-guild");
        thieves.Name.Should().Be("Thieves Guild");
        thieves.Type.Should().Be(FactionType.Criminal);
        thieves.Description.Should().BeEmpty();
        thieves.HomeLocation.Should().BeEmpty();
        thieves.AllyFactionIds.Should().BeEmpty();
        thieves.EnemyFactionIds.Should().BeEmpty();

        factions.Single(f => f.Id == "order-of-light").Type.Should().Be(FactionType.Religious);
        factions.Single(f => f.Id == "north-kingdom").Type.Should().Be(FactionType.Kingdom);
        factions.Single(f => f.Id == "old-kingdom").Type.Should().Be(FactionType.Kingdom);
        factions.Single(f => f.Id == "merchants").Type.Should().Be(FactionType.Guild);
    }

    [Fact]
    public void LoadFactions_FallsBackToSlug_WhenDisplayNameMissing()
    {
        var service = CreateService(StubDbContextFactory.WithOrganizations(
            MakeOrganization("nameless-cult", "religious")));

        service.LoadFactions().Single().Name.Should().Be("nameless-cult");
    }

    [Fact]
    public void LoadFactions_CachesResults_AcrossCalls()
    {
        var service = CreateSeededService();

        var first = service.LoadFactions();
        var second = service.LoadFactions();

        second.Should().BeSameAs(first, "the loaded faction list is cached on the service");
    }

    [Fact]
    public void LoadFactions_ReturnsEmpty_WhenTheDatabaseThrows()
    {
        var service = CreateService(StubDbContextFactory.Failing());

        service.LoadFactions().Should().BeEmpty();
    }

    [Fact]
    public void GetFactionBySlug_MatchesSlugOrName_CaseInsensitively()
    {
        var service = CreateSeededService();

        service.GetFactionBySlug("THIEVES-GUILD")!.Name.Should().Be("Thieves Guild");
        service.GetFactionBySlug("order of light")!.Id.Should().Be("order-of-light");
        service.GetFactionBySlug("does-not-exist").Should().BeNull();
    }

    [Fact]
    public void GetFactionsByType_FiltersByMappedFactionType()
    {
        var service = CreateSeededService();

        service.GetFactionsByType(FactionType.Kingdom).Select(f => f.Id)
            .Should().BeEquivalentTo(["north-kingdom", "old-kingdom"]);
        service.GetFactionsByType(FactionType.Criminal).Select(f => f.Id)
            .Should().BeEquivalentTo(["thieves-guild"]);
        service.GetFactionsByType(FactionType.Monster).Should().BeEmpty();
    }

    [Fact]
    public void LoadFactions_WithNoOrganizations_ReturnsEmpty()
    {
        var service = CreateService(StubDbContextFactory.WithOrganizations());

        service.LoadFactions().Should().BeEmpty();
    }

    [Fact]
    public void LoadFactions_ReturnsEmpty_WhenTheContextFactoryThrows()
    {
        var factory = new Mock<IDbContextFactory<ContentDbContext>>();
        factory.Setup(f => f.CreateDbContext()).Throws(new ObjectDisposedException("context"));
        var service = CreateService(factory.Object);

        service.LoadFactions().Should().BeEmpty();
        service.GetFactionBySlug("anything").Should().BeNull();
    }
}
