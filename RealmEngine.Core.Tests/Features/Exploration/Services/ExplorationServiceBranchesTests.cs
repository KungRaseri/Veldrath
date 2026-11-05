using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Abstractions;
using RealmEngine.Core.Features.Death.Queries;
using RealmEngine.Core.Features.Exploration;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Generators;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Core.Services;
using RealmEngine.Core.Services.Budget;
using RealmEngine.Core.Tests.Infrastructure;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Services;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using NamePatternSet = RealmEngine.Data.Entities.NamePatternSet;

namespace RealmEngine.Core.Tests.Features.Exploration.Services;

/// <summary>
/// Covers the peaceful-exploration half of <see cref="ExplorationService"/>: XP/gold/level rewards,
/// the item-find branch (including the real budget item generator), travel failure, dropped-item
/// recovery and the cached known-location list.
/// </summary>
[Trait("Category", "Service")]
public class ExplorationServiceBranchesTests
{
    private sealed class StubLocationGenerator : LocationGenerator
    {
        private readonly List<Location> _locations;

        public StubLocationGenerator(List<Location>? locations = null) : base()
        {
            _locations = locations ??
            [
                new Location { Id = "towns:oakvale", Name = "Oakvale", Type = "towns", Description = "", DangerRating = 1 },
                new Location { Id = "dungeons:crypt", Name = "Old Crypt", Type = "dungeons", Description = "", DangerRating = 4 }
            ];
        }

        public int GenerateCalls { get; private set; }

        public override Task<List<Location>> GenerateLocationsAsync(string locationType, int count = 5, bool hydrate = true)
        {
            GenerateCalls++;
            return Task.FromResult(_locations);
        }
    }

    private sealed class StubDbContextFactory : IDbContextFactory<ContentDbContext>
    {
        private readonly string _databaseName = Guid.NewGuid().ToString();

        public ContentDbContext CreateDbContext() => Create();

        public ContentDbContext Create() =>
            new(new DbContextOptionsBuilder<ContentDbContext>()
                .UseInMemoryDatabase(_databaseName)
                .Options);
    }

    private sealed class StubNamePatternRepository : INamePatternRepository
    {
        public Task<IEnumerable<NamePatternSet>> GetAllAsync() => Task.FromResult<IEnumerable<NamePatternSet>>([]);

        public Task<NamePatternSet?> GetByEntityPathAsync(string entityPath) => Task.FromResult<NamePatternSet?>(null);
    }

    private static ItemGenerator CreateItemGenerator()
    {
        var factory = new StubDbContextFactory();
        var db = factory.Create();
        var configFactory = new BudgetConfigFactory(new NullGameConfigService(), NullLogger<BudgetConfigFactory>.Instance);
        var calculator = new BudgetCalculator(configFactory.GetBudgetConfig(), NullLogger<BudgetCalculator>.Instance);
        var namePatterns = new NamePatternService(new StubNamePatternRepository(), NullLogger<NamePatternService>.Instance);
        namePatterns.InitializeAsync().GetAwaiter().GetResult();
        var budgetGenerator = new BudgetItemGenerationService(
            calculator,
            new MaterialPoolService(
                factory,
                calculator,
                configFactory.GetMaterialPools(),
                configFactory.GetEnemyTypes(),
                NullLogger<MaterialPoolService>.Instance,
                FakeRandomSource.Returning(new int[64])),
            configFactory,
            namePatterns,
            new ItemDataService(factory, NullLogger<ItemDataService>.Instance),
            NullLogger<BudgetItemGenerationService>.Instance);

        return new ItemGenerator(
            db,
            new EnchantmentGenerator(db, NullLogger<EnchantmentGenerator>.Instance),
            new SocketGenerator(configFactory.GetSocketConfig(), NullLogger<SocketGenerator>.Instance),
            budgetGenerator,
            NullLogger<ItemGenerator>.Instance);
    }

    private static ExplorationService CreateService(
        Character? player = null,
        string currentLocation = "Oakvale",
        IRandomSource? random = null,
        ItemGenerator? itemGenerator = null,
        StubLocationGenerator? locationGenerator = null,
        SaveGame? save = null,
        Mock<IMediator>? mediator = null,
        Mock<IGameStateService>? gameState = null,
        Mock<SaveGameService>? saveGameService = null)
    {
        player ??= new Character { Name = "Hero", Level = 1, Gold = 0 };
        mediator ??= new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetDroppedItemsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetDroppedItemsResult { Items = [] });

        gameState ??= new Mock<IGameStateService>();
        gameState.Setup(g => g.Player).Returns(player);
        gameState.SetupProperty(g => g.CurrentLocation, currentLocation);
        gameState.Setup(g => g.UpdateLocation(It.IsAny<string>()))
            .Callback<string>(location => gameState.Object.CurrentLocation = location);

        saveGameService ??= new Mock<SaveGameService>();
        saveGameService.Setup(s => s.GetCurrentSave()).Returns(save ?? new SaveGame { Character = player });

        return new ExplorationService(
            mediator.Object,
            gameState.Object,
            saveGameService.Object,
            locationGenerator ?? new StubLocationGenerator(),
            itemGenerator,
            random ?? FakeRandomSource.Seeded(1));
    }

    [Fact]
    public async Task ExploreAsync_CombatRoll_ReturnsCombatEncounterWithNoRewards()
    {
        var random = new FakeRandomSource(10);
        var service = CreateService(random: random);

        var result = await service.ExploreAsync();

        result.CombatEncounter.Should().BeTrue();
        result.Success.Should().BeTrue();
        result.XpGained.Should().Be(0);
        result.GoldGained.Should().Be(0);
        result.CurrentLocation.Should().Be("Oakvale");
    }

    [Fact]
    public async Task ExploreAsync_PeacefulRoll_AwardsXpAndGoldAndPublishesGoldEvent()
    {
        var player = new Character { Name = "Hero", Level = 1, Experience = 0, Gold = 5 };
        var mediator = new Mock<IMediator>();
        // 99 => no combat, 15 XP, 10 gold, 90 => no item
        var random = new FakeRandomSource(99).Enqueue(15, 10, 90);
        var service = CreateService(player: player, random: random, mediator: mediator);

        var result = await service.ExploreAsync();

        result.CombatEncounter.Should().BeFalse();
        result.XpGained.Should().Be(15);
        result.GoldGained.Should().Be(10);
        result.LeveledUp.Should().BeFalse();
        result.NewLevel.Should().BeNull();
        result.ItemFound.Should().BeNull();
        player.Experience.Should().Be(15);
        player.Gold.Should().Be(15);
        mediator.Verify(m => m.Publish(
            It.Is<GoldGained>(e => e.PlayerName == "Hero" && e.Amount == 10),
            It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task ExploreAsync_LevelUp_PublishesLevelUpEventAndReportsNewLevel()
    {
        var player = new Character { Name = "Hero", Level = 1, Experience = 95, Gold = 0 };
        var mediator = new Mock<IMediator>();
        var random = new FakeRandomSource(99).Enqueue(15, 10, 90);
        var service = CreateService(player: player, random: random, mediator: mediator);

        var result = await service.ExploreAsync();

        result.LeveledUp.Should().BeTrue();
        result.NewLevel.Should().Be(2);
        player.Level.Should().Be(2);
        mediator.Verify(m => m.Publish(
            It.Is<PlayerLeveledUp>(e => e.PlayerName == "Hero" && e.NewLevel == 2),
            It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task ExploreAsync_WhenTheItemRollSucceeds_GeneratesAndStoresAnItem()
    {
        var player = new Character { Name = "Hero", Level = 3, Gold = 0 };
        // 99 => no combat, 15 XP, 10 gold, 5 => item chance
        var random = new FakeRandomSource(99).Enqueue(15, 10, 5);
        var service = CreateService(player: player, random: random, itemGenerator: CreateItemGenerator());
        await service.GetKnownLocationsAsync(); // the item branch needs the location cache populated

        var result = await service.ExploreAsync();

        result.ItemFound.Should().NotBeNull();
        player.Inventory.Should().Contain(result.ItemFound);
        result.ItemFound!.Name.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExploreAsync_WhenTheItemRollSucceeds_ButNoGeneratorIsInjected_FindsNothing()
    {
        var player = new Character { Name = "Hero", Level = 1 };
        var random = new FakeRandomSource(99).Enqueue(15, 10, 5);
        var service = CreateService(player: player, random: random, itemGenerator: null);

        var result = await service.ExploreAsync();

        result.ItemFound.Should().BeNull();
        player.Inventory.Should().BeEmpty();
    }

    [Fact]
    public async Task ExploreAsync_WhenTheCurrentLocationIsUnknown_SkipsItemGeneration()
    {
        var player = new Character { Name = "Hero", Level = 1 };
        var random = new FakeRandomSource(99).Enqueue(15, 10, 5);
        var service = CreateService(
            player: player,
            currentLocation: "Elsewhere",
            random: random,
            itemGenerator: CreateItemGenerator());

        var result = await service.ExploreAsync();

        result.ItemFound.Should().BeNull();
    }

    [Fact]
    public async Task TravelToLocation_WithUnknownDestination_ReturnsFailure()
    {
        var service = CreateService();

        var result = await service.TravelToLocation("Nowhere");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Location not found");
        result.CurrentLocation.Should().Be("Oakvale");
    }

    [Fact]
    public async Task TravelToLocation_ReturnsDroppedItemsAtTheDestination()
    {
        var mediator = new Mock<IMediator>();
        var service = CreateService(mediator: mediator);
        mediator.Setup(m => m.Send(It.IsAny<GetDroppedItemsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetDroppedItemsResult { Items = [new Item { Name = "Lost Sword" }] });

        var result = await service.TravelToLocation("Old Crypt");

        result.Success.Should().BeTrue();
        result.DroppedItemsAtLocation.Should().ContainSingle().Which.Name.Should().Be("Lost Sword");
    }

    [Fact]
    public async Task TravelToLocation_WithNoDroppedItems_ReturnsEmptyList()
    {
        var service = CreateService();

        var result = await service.TravelToLocation("Old Crypt");

        result.Success.Should().BeTrue();
        result.DroppedItemsAtLocation.Should().BeEmpty();
    }

    [Fact]
    public void RecoverDroppedItems_MovesItemsIntoTheInventory_AndClearsTheLocation()
    {
        var player = new Character { Name = "Hero", Inventory = [] };
        var save = new SaveGame { Character = player };
        save.DroppedItemsAtLocations["Old Crypt"] = [new Item { Name = "Lost Sword" }];
        var service = CreateService(player: player, save: save);

        var recovered = service.RecoverDroppedItems("Old Crypt");

        recovered.Should().BeTrue();
        player.Inventory.Should().ContainSingle().Which.Name.Should().Be("Lost Sword");
        save.DroppedItemsAtLocations.Should().NotContainKey("Old Crypt");
    }

    [Fact]
    public void RecoverDroppedItems_ReturnsFalse_WhenNothingWasDroppedThere()
    {
        var service = CreateService();

        service.RecoverDroppedItems("Old Crypt").Should().BeFalse();
    }

    [Fact]
    public void RecoverDroppedItems_ReturnsFalse_WhenThereIsNoActiveSave()
    {
        var saveGameService = new Mock<SaveGameService>();
        saveGameService.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);
        var service = CreateService(saveGameService: saveGameService);

        service.RecoverDroppedItems("Old Crypt").Should().BeFalse();
    }

    [Fact]
    public async Task GetKnownLocationsAsync_InitialisesOnce_AndReturnsGeneratedLocations()
    {
        var locations = new StubLocationGenerator();
        var service = CreateService(locationGenerator: locations);

        var first = await service.GetKnownLocationsAsync();
        var second = await service.GetKnownLocationsAsync();

        first.Should().HaveCount(6, "three location types are generated and aggregated");
        second.Should().HaveCount(6);
        locations.GenerateCalls.Should().Be(3, "locations are generated once and then cached");
    }
}
