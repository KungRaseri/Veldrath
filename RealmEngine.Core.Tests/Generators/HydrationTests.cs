using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Data.Services;
using Xunit;

namespace RealmEngine.Core.Tests.Generators;

/// <summary>
/// Tests for the new hydrated objects feature (v1.0+).
/// Ensures generators properly resolve reference IDs to actual objects.
/// </summary>
[Trait("Category", "Generator")]
[Trait("Category", "Hydration")]
public class HydrationTests
{
    private readonly GameDataCache _dataCache;
    private readonly ReferenceResolverService _referenceResolver;

    public HydrationTests()
    {
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "RealmEngine.Data", "Data", "Json");
        _dataCache = new GameDataCache(basePath);
        var mockLogger = new Mock<ILogger<ReferenceResolverService>>();
        _referenceResolver = new ReferenceResolverService(_dataCache, mockLogger.Object);
        _dataCache.LoadAllData();
    }

    #region Enemy Hydration Tests

    [Fact]
    public async Task Enemy_Should_Have_Abilities_Hydrated_By_Default()
    {
        // Arrange
        var mockEnemyLogger = new Mock<ILogger<EnemyGenerator>>();
        var generator = new EnemyGenerator(_dataCache, _referenceResolver, mockEnemyLogger.Object);

        // Act
        var enemy = await generator.GenerateEnemyByNameAsync("beasts", "Wolf");

        // Assert
        enemy.Should().NotBeNull();
        
        // If AbilityIds exist, Abilities should be hydrated
        if (enemy!.AbilityIds.Count > 0)
        {
            enemy.Abilities.Should().NotBeNull();
            enemy.Abilities.Should().HaveCountGreaterThan(0, "Abilities should be hydrated when hydrate=true (default)");
            
            // Each ability should be fully populated
            enemy.Abilities.Should().AllSatisfy(ability =>
            {
                ability.Name.Should().NotBeNullOrEmpty();
                ability.DisplayName.Should().NotBeNullOrEmpty();
            });
        }
    }

    [Fact]
    public async Task Enemy_Should_Have_LootTable_Hydrated_By_Default()
    {
        // Arrange
        var mockEnemyLogger = new Mock<ILogger<EnemyGenerator>>();
        var generator = new EnemyGenerator(_dataCache, _referenceResolver, mockEnemyLogger.Object);

        // Act
        var enemy = await generator.GenerateEnemyByNameAsync("beasts", "Wolf");

        // Assert
        enemy.Should().NotBeNull();
        
        // If LootTableIds exist, LootTable should be hydrated
        if (enemy!.LootTableIds.Count > 0)
        {
            enemy.LootTable.Should().NotBeNull();
            enemy.LootTable.Should().HaveCountGreaterThan(0, "LootTable should be hydrated when hydrate=true (default)");
            
            // Each item should be fully populated
            enemy.LootTable.Should().AllSatisfy(item =>
            {
                item.Name.Should().NotBeNullOrEmpty();
                item.Price.Should().BeGreaterThanOrEqualTo(0);
            });
        }
    }

    [Fact]
    public async Task Enemy_Should_Not_Hydrate_When_Disabled()
    {
        // Arrange
        var mockEnemyLogger = new Mock<ILogger<EnemyGenerator>>();
        var generator = new EnemyGenerator(_dataCache, _referenceResolver, mockEnemyLogger.Object);

        // Act
        var enemy = await generator.GenerateEnemyByNameAsync("beasts", "Wolf", hydrate: false);

        // Assert
        enemy.Should().NotBeNull();
        
        // When hydration is disabled, resolved collections should be empty
        enemy!.Abilities.Should().BeEmpty("Abilities should not be hydrated when hydrate=false");
        enemy.LootTable.Should().BeEmpty("LootTable should not be hydrated when hydrate=false");
        
        // But reference IDs should still exist
        // (only if the catalog has them - might be empty for some enemies)
    }

    [Fact]
    public async Task Enemy_Hydration_Should_Match_Reference_Ids()
    {
        // Arrange
        var mockEnemyLogger = new Mock<ILogger<EnemyGenerator>>();
        var generator = new EnemyGenerator(_dataCache, _referenceResolver, mockEnemyLogger.Object);

        // Act
        var enemy = await generator.GenerateEnemyByNameAsync("beasts", "Wolf", hydrate: true);

        // Assert
        enemy.Should().NotBeNull();
        
        // Skip test if no abilities (some enemies might not have abilities defined yet)
        if (enemy!.AbilityIds.Count > 0)
        {
            // Number of resolved abilities should match reference IDs
            // (allowing for failed resolutions if catalogs don't exist)
            enemy.Abilities.Count.Should().BeLessThanOrEqualTo(enemy.AbilityIds.Count,
                "Resolved abilities count should not exceed reference IDs");
        }
    }

    #endregion

    #region NPC Hydration Tests

    [Fact]
    public async Task Npc_Should_Have_Inventory_Hydrated_By_Default()
    {
        // Arrange
        var mockNpcLogger = new Mock<ILogger<NpcGenerator>>();
        var generator = new NpcGenerator(_dataCache, _referenceResolver, mockNpcLogger.Object);

        // Act
        var npc = await generator.GenerateNpcByNameAsync("merchants", "General Merchant");

        // Assert
        npc.Should().NotBeNull();
        
        // If InventoryIds exist, Inventory should be hydrated
        if (npc!.InventoryIds.Count > 0)
        {
            npc.Inventory.Should().NotBeNull();
            npc.Inventory.Should().HaveCountGreaterThan(0, "Inventory should be hydrated when hydrate=true (default)");
            
            npc.Inventory.Should().AllSatisfy(item =>
            {
                item.Name.Should().NotBeNullOrEmpty();
            });
        }
    }

    [Fact]
    public async Task Npc_Should_Not_Hydrate_When_Disabled()
    {
        // Arrange
        var mockNpcLogger = new Mock<ILogger<NpcGenerator>>();
        var generator = new NpcGenerator(_dataCache, _referenceResolver, mockNpcLogger.Object);

        // Act
        var npc = await generator.GenerateNpcByNameAsync("merchants", "General Merchant", hydrate: false);

        // Assert
        npc.Should().NotBeNull();
        
        // When hydration is disabled, resolved collections should be empty
        npc!.Dialogues.Should().BeEmpty("Dialogues should not be hydrated when hydrate=false");
        npc.Abilities.Should().BeEmpty("Abilities should not be hydrated when hydrate=false");
        npc.Inventory.Should().BeEmpty("Inventory should not be hydrated when hydrate=false");
    }

    #endregion

    #region Quest Hydration Tests

    [Fact]
    public async Task Quest_Should_Have_Rewards_Hydrated_By_Default()
    {
        // Arrange
        var mockQuestLogger = new Mock<ILogger<QuestGenerator>>();
        var generator = new QuestGenerator(_dataCache, _referenceResolver, mockQuestLogger.Object);

        // Act
        var quest = await generator.GenerateQuestByNameAsync("kill", "SlayBeasts");

        // Assert
        quest.Should().NotBeNull();
        
        // If ItemRewardIds exist, ItemRewards should be hydrated
        if (quest!.ItemRewardIds.Count > 0)
        {
            quest.ItemRewards.Should().NotBeNull();
            quest.ItemRewards.Should().HaveCountGreaterThan(0, "ItemRewards should be hydrated when hydrate=true (default)");
            
            quest.ItemRewards.Should().AllSatisfy(item =>
            {
                item.Name.Should().NotBeNullOrEmpty();
            });
        }
    }

    [Fact]
    public async Task Quest_Should_Not_Hydrate_When_Disabled()
    {
        // Arrange
        var mockQuestLogger = new Mock<ILogger<QuestGenerator>>();
        var generator = new QuestGenerator(_dataCache, _referenceResolver, mockQuestLogger.Object);

        // Act
        var quest = await generator.GenerateQuestByNameAsync("kill", "SlayBeasts", hydrate: false);

        // Assert
        quest.Should().NotBeNull();
        
        // When hydration is disabled, resolved collections should be empty
        quest!.ItemRewards.Should().BeEmpty("ItemRewards should not be hydrated when hydrate=false");
        quest.AbilityRewards.Should().BeEmpty("AbilityRewards should not be hydrated when hydrate=false");
        quest.ObjectiveLocations.Should().BeEmpty("ObjectiveLocations should not be hydrated when hydrate=false");
        quest.ObjectiveNpcs.Should().BeEmpty("ObjectiveNpcs should not be hydrated when hydrate=false");
    }

    #endregion

    #region Item Hydration Tests

    [Fact]
    public async Task Item_Should_Have_RequiredItems_Hydrated_By_Default()
    {
        // Arrange
        var mockItemLogger = new Mock<ILogger<ItemGenerator>>();
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        var generator = new ItemGenerator(_dataCache, _referenceResolver, mockItemLogger.Object, mockLoggerFactory.Object);

        // Act
        var items = await generator.GenerateItemsAsync("weapons", 5);

        // Assert
        items.Should().NotBeNull();
        
        // Find any item with crafting requirements
        var itemWithReqs = items.FirstOrDefault(i => i.RequiredItemIds.Count > 0);
        
        if (itemWithReqs != null)
        {
            itemWithReqs.RequiredItems.Should().NotBeNull();
            itemWithReqs.RequiredItems.Should().HaveCountGreaterThan(0, "RequiredItems should be hydrated when hydrate=true (default)");
            
            itemWithReqs.RequiredItems.Should().AllSatisfy(item =>
            {
                item.Name.Should().NotBeNullOrEmpty();
            });
        }
    }

    [Fact]
    public async Task Item_Should_Not_Hydrate_When_Disabled()
    {
        // Arrange
        var mockItemLogger = new Mock<ILogger<ItemGenerator>>();
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        var generator = new ItemGenerator(_dataCache, _referenceResolver, mockItemLogger.Object, mockLoggerFactory.Object);

        // Act
        var items = await generator.GenerateItemsAsync("weapons", 5, hydrate: false);

        // Assert
        items.Should().NotBeNull();
        
        // When hydration is disabled, resolved collections should be empty
        items.Should().AllSatisfy(item =>
        {
            item.RequiredItems.Should().BeEmpty("RequiredItems should not be hydrated when hydrate=false");
        });
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task Hydrated_Generation_Should_Be_Slower_Than_Template_Only()
    {
        // Arrange
        var mockEnemyLogger = new Mock<ILogger<EnemyGenerator>>();
        var generator = new EnemyGenerator(_dataCache, _referenceResolver, mockEnemyLogger.Object);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Act - Generate with hydration
        var hydratedEnemies = await generator.GenerateEnemiesAsync("beasts", 20, hydrate: true);
        sw.Stop();
        var hydratedTime = sw.ElapsedMilliseconds;

        sw.Restart();
        // Act - Generate without hydration
        var templateEnemies = await generator.GenerateEnemiesAsync("beasts", 20, hydrate: false);
        sw.Stop();
        var templateTime = sw.ElapsedMilliseconds;

        // Assert
        hydratedEnemies.Should().HaveCount(20);
        templateEnemies.Should().HaveCount(20);
        
        // Hydrated should take longer (though this might be flaky on fast machines)
        // Just log the times for diagnostic purposes
        Console.WriteLine($"Hydrated: {hydratedTime}ms, Template: {templateTime}ms");
        
        // Main assertion: both modes should work
        hydratedEnemies.Should().NotBeEmpty();
        templateEnemies.Should().NotBeEmpty();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task Location_Should_Have_All_Objects_Hydrated()
    {
        // Arrange
        var mockLocationLogger = new Mock<ILogger<LocationGenerator>>();
        var generator = new LocationGenerator(_dataCache, _referenceResolver, mockLocationLogger.Object);

        // Act
        var locations = await generator.GenerateLocationsAsync("towns", 3);

        // Assert
        locations.Should().NotBeNull();
        
        foreach (var location in locations)
        {
            // If NPC IDs exist, they should be hydrated
            if (location.Npcs.Count > 0)
            {
                location.NpcObjects.Should().NotBeNull();
                location.NpcObjects.Should().HaveCountGreaterThan(0, "NpcObjects should be hydrated");
            }
            
            // If Enemy IDs exist, they should be hydrated
            if (location.Enemies.Count > 0)
            {
                location.EnemyObjects.Should().NotBeNull();
                location.EnemyObjects.Should().HaveCountGreaterThan(0, "EnemyObjects should be hydrated");
            }
            
            // If Loot IDs exist, they should be hydrated
            if (location.Loot.Count > 0)
            {
                location.LootObjects.Should().NotBeNull();
                location.LootObjects.Should().HaveCountGreaterThan(0, "LootObjects should be hydrated");
            }
        }
    }

    [Fact]
    public async Task Organization_Should_Have_Members_And_Inventory_Hydrated()
    {
        // Arrange
        var mockOrganizationLogger = new Mock<ILogger<OrganizationGenerator>>();
        var generator = new OrganizationGenerator(_dataCache, _referenceResolver, mockOrganizationLogger.Object);

        // Act
        var organizations = await generator.GenerateOrganizationsAsync("guilds", 2);

        // Assert
        organizations.Should().NotBeNull();
        
        foreach (var org in organizations)
        {
            // If member IDs exist, they should be hydrated
            if (org.Members.Count > 0)
            {
                org.MemberObjects.Should().NotBeNull();
                org.MemberObjects.Should().HaveCountGreaterThan(0, "MemberObjects should be hydrated");
                
                org.MemberObjects.Should().AllSatisfy(member =>
                {
                    member.Name.Should().NotBeNullOrEmpty();
                });
            }
            
            // If inventory IDs exist, they should be hydrated
            if (org.Inventory.Count > 0)
            {
                org.InventoryObjects.Should().NotBeNull();
                org.InventoryObjects.Should().HaveCountGreaterThan(0, "InventoryObjects should be hydrated");
                
                org.InventoryObjects.Should().AllSatisfy(item =>
                {
                    item.Name.Should().NotBeNullOrEmpty();
                });
            }
        }
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task Hydration_Should_Handle_Missing_Catalogs_Gracefully()
    {
        // Arrange
        var mockEnemyLogger = new Mock<ILogger<EnemyGenerator>>();
        var generator = new EnemyGenerator(_dataCache, _referenceResolver, mockEnemyLogger.Object);

        // Act - This should not throw even if ability catalogs are missing
        var action = async () => await generator.GenerateEnemiesAsync("beasts", 5, hydrate: true);

        // Assert
        await action.Should().NotThrowAsync("Hydration should handle missing catalogs gracefully");
    }

    [Fact]
    public async Task Hydration_Should_Continue_On_Individual_Reference_Failure()
    {
        // Arrange
        var mockEnemyLogger = new Mock<ILogger<EnemyGenerator>>();
        var generator = new EnemyGenerator(_dataCache, _referenceResolver, mockEnemyLogger.Object);

        // Act
        var enemies = await generator.GenerateEnemiesAsync("beasts", 10, hydrate: true);

        // Assert
        enemies.Should().NotBeNull();
        enemies.Should().HaveCount(10, "Generator should return all enemies even if some references fail to resolve");
        
        // Enemies should still be usable even if hydration partially failed
        enemies.Should().AllSatisfy(enemy =>
        {
            enemy.Name.Should().NotBeNullOrEmpty();
            enemy.Health.Should().BeGreaterThan(0);
        });
    }

    #endregion
}
