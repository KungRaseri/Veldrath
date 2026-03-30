using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RealmEngine.Core.Features.Combat;
using RealmEngine.Core.Features.Combat.Commands.AttackEnemy;
using RealmEngine.Core.Features.Quests.Commands;
using RealmEngine.Core.Features.Quests.Services;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Features.Progression.Services;
using RealmEngine.Core.Services;
using RealmEngine.Core.Abstractions;
using RealmEngine.Data.Repositories;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Xunit;

namespace RealmEngine.Core.Tests.Features.Quest.Integration;

/// <summary>
/// Integration tests for the complete quest system workflow.
/// Tests: Quest initialization → Start quest → Kill enemies → Auto-complete → Rewards.
/// </summary>
public class QuestIntegrationTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IMediator _mediator;
    private readonly SaveGameService _saveGameService;
    private readonly MainQuestService _mainQuestService;
    private readonly SaveGame _testSaveGame;

    public QuestIntegrationTests()
    {
        var services = new ServiceCollection();

        SaveGameService? saveGameService = null;
        
        // Create mock timer that returns the value from the current save game
        // This prevents SaveGame() from overwriting quest reward bonuses
        var mockTimer = new Mock<IApocalypseTimer>();
        mockTimer.Setup(t => t.GetBonusMinutes()).Returns(() => 
            saveGameService?.GetCurrentSave()?.ApocalypseBonusMinutes ?? 0);
        
        var mockRepository = new Mock<ISaveGameRepository>();
        mockRepository.Setup(r => r.SaveGame(It.IsAny<SaveGame>())).Callback<SaveGame>(s => {
            // Do nothing for tests
        });

        // Mock SkillCatalogService to avoid database dependencies
        var mockSkillCatalog = new Mock<SkillDataService>(new InMemorySkillRepository(), null!);
        mockSkillCatalog.Setup(s => s.GetSkillDefinition(It.IsAny<string>()))
            .Returns(new SkillDefinition 
            { 
                SkillId = "test_skill",
                Name = "Test Skill",
                DisplayName = "Test Skill",
                Description = "Test Description",
                Category = "combat",
                MaxRank = 10
            });

        // Register logging for handlers
        services.AddLogging();

        // Register all required services
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(StartQuestCommand).Assembly));
        services.AddSingleton(mockTimer.Object);
        services.AddSingleton(mockRepository.Object);
        services.AddSingleton<SaveGameService>();
        services.AddSingleton<ISaveGameService>(sp => sp.GetRequiredService<SaveGameService>());
        services.AddSingleton<ICombatSettings>(sp => sp.GetRequiredService<ISaveGameService>().GetDifficultySettings());
        services.AddSingleton<MainQuestService>();
        services.AddSingleton<QuestService>();
        services.AddSingleton<QuestProgressService>();
        services.AddSingleton<QuestRewardService>(sp => new QuestRewardService(
            sp.GetRequiredService<ISaveGameService>(),
            null, // ItemGenerator has ContentDbContext dependencies not available in integration tests
            sp.GetRequiredService<ILogger<QuestRewardService>>()));
        services.AddSingleton<QuestInitializationService>();
        services.AddSingleton<IPowerRepository, InMemoryPowerRepository>();
        services.AddSingleton(sp => new PowerDataService(sp.GetRequiredService<IPowerRepository>()));
        services.AddSingleton<SpellCastingService>();
        services.AddSingleton(mockSkillCatalog.Object);
        services.AddSingleton<SkillProgressionService>();
        services.AddSingleton<CombatService>(sp => new CombatService(
            sp.GetRequiredService<ISaveGameService>(),
            sp.GetRequiredService<IMediator>(),
            sp.GetRequiredService<PowerDataService>(),
            sp.GetRequiredService<ILogger<CombatService>>(),
            sp.GetRequiredService<ILoggerFactory>()));

        _serviceProvider = services.BuildServiceProvider();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();
        _saveGameService = _serviceProvider.GetRequiredService<SaveGameService>();
        _mainQuestService = _serviceProvider.GetRequiredService<MainQuestService>();
        
        // Wire up the saveGameService reference for the timer mock
        saveGameService = _saveGameService;

        // Create test save game
        var character = new Character { Name = "TestHero", ClassName = "Warrior", Level = 1 };
        _testSaveGame = _saveGameService.CreateNewGame(character, DifficultySettings.Normal);
    }

    [Fact]
    public async Task Should_Initialize_Starting_Quest_On_New_Game()
    {
        // Arrange
        var character = new Character { Name = "TestHero", ClassName = "Warrior", Level = 1 };
        var saveGame = _saveGameService.CreateNewGame(character, DifficultySettings.Normal);

        // Act
        var result = await _mediator.Send(new InitializeStartingQuestsCommand(saveGame));

        // Assert
        result.Success.Should().BeTrue();
        result.QuestsInitialized.Should().Be(1);
        saveGame.AvailableQuests.Should().ContainSingle();
        saveGame.AvailableQuests[0].Id.Should().Be("main_01_awakening");
    }

    [Fact]
    public async Task Should_Complete_Quest_When_Enemy_Defeated_With_Matching_Objective()
    {
        // Arrange - Create character and start game
        var character = new Character 
        { 
            Name = "TestHero", 
            ClassName = "Warrior", 
            Level = 5,
            Health = 100,
            MaxHealth = 100,
            Strength = 15
        };
        var saveGame = _saveGameService.CreateNewGame(character, DifficultySettings.Normal);
        _saveGameService.SetCurrentSave(saveGame);

        // Get the second main quest (defeat_shrine_guardian)
        var quest = await _mainQuestService.GetQuestByIdAsync("main_02_first_trial");
        quest.Should().NotBeNull();

        // Start the quest manually (bypass prerequisites for test)
        quest!.IsActive = true;
        saveGame.ActiveQuests.Add(quest);

        // Create enemy matching the quest objective
        var enemy = new Enemy 
        { 
            Name = "Shrine Guardian", 
            Health = 50, 
            MaxHealth = 50,
            XP = 100,
            GoldReward = 50,
            Type = EnemyType.Boss
        };

        // Act - Attack enemy until defeated
        AttackEnemyResult? finalResult = null;
        while (enemy.Health > 0)
        {
            finalResult = await _mediator.Send(new AttackEnemyCommand 
            { 
                Player = character, 
                Enemy = enemy,
                CombatLog = null 
            });
        }

        // Assert
        finalResult.Should().NotBeNull();
        finalResult!.IsEnemyDefeated.Should().BeTrue();

        // Quest should be auto-completed
        saveGame.ActiveQuests.Should().BeEmpty();
        saveGame.CompletedQuests.Should().ContainSingle();
        saveGame.CompletedQuests[0].Id.Should().Be("main_02_first_trial");
        saveGame.QuestsCompleted.Should().Be(1);

        // Character should have received quest rewards
        character.Experience.Should().BeGreaterThan(100); // Enemy XP + Quest XP
        character.Gold.Should().BeGreaterThan(50); // Enemy Gold + Quest Gold
    }

    [Fact]
    public async Task Should_Update_Quest_Progress_For_Multiple_Enemy_Kills()
    {
        // Arrange
        var character = new Character 
        { 
            Name = "TestHero", 
            ClassName = "Warrior", 
            Level = 10,
            Health = 150,
            MaxHealth = 150,
            Strength = 20
        };
        var saveGame = _saveGameService.CreateNewGame(character, DifficultySettings.Normal);
        _saveGameService.SetCurrentSave(saveGame);

        // Get quest that requires multiple kills (main_05_into_abyss - defeat 5 demons)
        var quest = await _mainQuestService.GetQuestByIdAsync("main_05_into_abyss");
        quest.Should().NotBeNull();

        quest!.IsActive = true;
        saveGame.ActiveQuests.Add(quest);

        // Act - Kill 5 demons
        for (int i = 0; i < 5; i++)
        {
            var demon = new Enemy 
            { 
                Name = $"Abyssal Demon {i+1}", 
                Health = 30, 
                MaxHealth = 30,
                XP = 50,
                GoldReward = 25,
                Type = EnemyType.Demon
            };

            while (demon.Health > 0)
            {
                await _mediator.Send(new AttackEnemyCommand 
                { 
                    Player = character, 
                    Enemy = demon, 
                    CombatLog = null 
                });
            }
        }

        // Assert
        var demonObjective = quest.ObjectiveProgress.FirstOrDefault(kvp => kvp.Key.Contains("demon"));
        demonObjective.Should().NotBeNull();
        demonObjective.Value.Should().BeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public async Task Should_Not_Complete_Quest_Until_All_Objectives_Met()
    {
        // Arrange
        var character = new Character 
        { 
            Name = "TestHero", 
            ClassName = "Warrior", 
            Level = 10,
            Health = 150,
            MaxHealth = 150,
            Strength = 20
        };
        var saveGame = _saveGameService.CreateNewGame(character, DifficultySettings.Normal);
        _saveGameService.SetCurrentSave(saveGame);

        // Get quest with multiple objectives (main_05_into_abyss)
        var quest = await _mainQuestService.GetQuestByIdAsync("main_05_into_abyss");
        quest.Should().NotBeNull();

        quest!.IsActive = true;
        saveGame.ActiveQuests.Add(quest);

        // Act - Kill only 3 demons (not enough to complete)
        for (int i = 0; i < 3; i++)
        {
            var demon = new Enemy 
            { 
                Name = "Abyssal Demon", 
                Health = 30, 
                MaxHealth = 30,
                XP = 50,
                GoldReward = 25,
                Type = EnemyType.Demon
            };

            while (demon.Health > 0)
            {
                await _mediator.Send(new AttackEnemyCommand 
                { 
                    Player = character, 
                    Enemy = demon, 
                    CombatLog = null 
                });
            }
        }

        // Assert - Quest should still be active (not enough kills)
        saveGame.ActiveQuests.Should().ContainSingle();
        saveGame.CompletedQuests.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_Unlock_Next_Quest_After_Completing_Prerequisite()
    {
        // Arrange
        var character = new Character 
        { 
            Name = "TestHero", 
            ClassName = "Warrior", 
            Level = 1,
            Health = 100,
            MaxHealth = 100
        };
        var saveGame = _saveGameService.CreateNewGame(character, DifficultySettings.Normal);
        _saveGameService.SetCurrentSave(saveGame);

        // Initialize starting quest
        await _mediator.Send(new InitializeStartingQuestsCommand(saveGame));

        // Accept first quest
        var startResult = await _mediator.Send(new StartQuestCommand("main_01_awakening"));
        startResult.Success.Should().BeTrue();

        // Act - Complete first quest manually (simulating objective completion)
        var firstQuest = saveGame.ActiveQuests.First();
        firstQuest.ObjectiveProgress["reach_ancient_shrine"] = 1; // Mark objective complete

        var completeResult = await _mediator.Send(new CompleteQuestCommand("main_01_awakening"));

        // Assert
        completeResult.Success.Should().BeTrue();
        saveGame.CompletedQuests.Should().ContainSingle(q => q.Id == "main_01_awakening");
        
        // Second quest should now be available
        saveGame.AvailableQuests.Should().Contain(q => q.Id == "main_02_first_trial");
    }

    [Fact]
    public async Task Should_Award_Apocalypse_Bonus_Time_When_Quest_Completed_In_Apocalypse_Mode()
    {
        // Arrange
        var character = new Character 
        { 
            Name = "TestHero", 
            ClassName = "Warrior", 
            Level = 1
        };
        
        var apocalypseDifficulty = new DifficultySettings
        {
            Name = "Normal",
            IsApocalypse = true,
            PlayerDamageMultiplier = 1.0,
            EnemyHealthMultiplier = 1.0,
            GoldXPMultiplier = 1.0
        };

        var saveGame = _saveGameService.CreateNewGame(character, apocalypseDifficulty);
        _saveGameService.SetCurrentSave(saveGame);
        saveGame.ApocalypseMode.Should().BeTrue();

        // Get quest with apocalypse bonus
        var quest = await _mainQuestService.GetQuestByIdAsync("main_01_awakening");
        quest.Should().NotBeNull();
        quest!.ApocalypseBonusMinutes.Should().Be(15);

        quest.IsActive = true;
        saveGame.ActiveQuests.Add(quest);

        // Mark objective complete
        quest.ObjectiveProgress["reach_ancient_shrine"] = 1;

        var initialBonusMinutes = saveGame.ApocalypseBonusMinutes;

        // Act
        var result = await _mediator.Send(new CompleteQuestCommand("main_01_awakening"));

        // Assert
        result.Success.Should().BeTrue();
        
        // Get the current save game from the service (in case the reference changed)
        var currentSave = _saveGameService.GetCurrentSave();
        currentSave.Should().NotBeNull();
        currentSave!.ApocalypseBonusMinutes.Should().Be(initialBonusMinutes + 15);
    }

    [Fact]
    public async Task Should_Track_Enemies_Defeated_By_Type()
    {
        // Arrange
        var character = new Character 
        { 
            Name = "TestHero", 
            ClassName = "Warrior", 
            Level = 5,
            Health = 100,
            MaxHealth = 100,
            Strength = 15
        };
        var saveGame = _saveGameService.CreateNewGame(character, DifficultySettings.Normal);
        _saveGameService.SetCurrentSave(saveGame);

        var goblin = new Enemy 
        { 
            Name = "Goblin", 
            Health = 20, 
            MaxHealth = 20,
            Type = EnemyType.Humanoid
        };

        // Act
        while (goblin.Health > 0)
        {
            await _mediator.Send(new AttackEnemyCommand 
            { 
                Player = character, 
                Enemy = goblin, 
                CombatLog = null 
            });
        }

        // Assert
        saveGame.TotalEnemiesDefeated.Should().Be(1);
        saveGame.EnemiesDefeatedByType.Should().ContainKey("humanoid");
        saveGame.EnemiesDefeatedByType["humanoid"].Should().Be(1);
    }

    [Fact]
    public async Task Should_Include_Quest_Progress_In_Combat_Outcome_When_Enemy_Defeated()
    {
        // Arrange - Create character and start game
        var character = new Character 
        { 
            Name = "TestHero", 
            ClassName = "Warrior", 
            Level = 5,
            Health = 100,
            MaxHealth = 100,
            Strength = 15
        };
        var saveGame = _saveGameService.CreateNewGame(character, DifficultySettings.Normal);
        _saveGameService.SetCurrentSave(saveGame);

        // Get the second main quest (defeat_shrine_guardian)
        var quest = await _mainQuestService.GetQuestByIdAsync("main_02_first_trial");
        quest.Should().NotBeNull();

        // Start the quest manually (bypass prerequisites for test)
        quest!.IsActive = true;
        saveGame.ActiveQuests.Add(quest);

        // Create enemy matching the quest objective
        var enemy = new Enemy 
        { 
            Id = "shrine_guardian",
            Name = "Shrine Guardian", 
            Health = 0, // Set to 0 to simulate defeated state
            MaxHealth = 50,
            XP = 100,
            GoldReward = 50,
            Type = EnemyType.Boss
        };

        // Get CombatService from the service provider
        var combatService = _serviceProvider.GetRequiredService<CombatService>();

        // Act - Generate victory outcome for the defeated enemy
        var outcome = await combatService.GenerateVictoryOutcome(character, enemy);

        // Assert - CombatOutcome should include quest progress information
        outcome.Should().NotBeNull();
        outcome.DefeatedEnemyId.Should().Be("shrine_guardian");
        outcome.DefeatedEnemyType.Should().Be("Boss");
        outcome.QuestObjectivesCompleted.Should().NotBeEmpty();
        outcome.QuestObjectivesCompleted.Should().Contain(msg => msg.Contains("The First Trial"));
        outcome.QuestsCompleted.Should().NotBeEmpty();
        outcome.QuestsCompleted.Should().Contain("The First Trial");
    }
}


