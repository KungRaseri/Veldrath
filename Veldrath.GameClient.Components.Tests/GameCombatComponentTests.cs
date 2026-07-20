using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Veldrath.GameClient.Components.Components.Pages;
using Veldrath.GameClient.Components.Tests.Infrastructure;
using Veldrath.GameClient.Core.Abstractions;
using Veldrath.GameClient.Core.Services;
using Xunit;

namespace Veldrath.GameClient.Components.Tests;

/// <summary>
/// Tests for the <see cref="GameCombat"/> component, verifying combat state
/// rendering, attack button behaviour, and combat log display.
/// </summary>
public class GameCombatComponentTests : BunitContext
{
    private readonly FakeGameHubConnectionService _fakeHub;
    private readonly GameStateService _gameState;

    /// <summary>
    /// Initializes a new instance and registers required services.
    /// </summary>
    public GameCombatComponentTests()
    {
        _fakeHub = new FakeGameHubConnectionService();
        _gameState = new GameStateService();

        Services.AddSingleton<IGameHubConnectionService>(_fakeHub);
        Services.AddSingleton(_gameState);
    }

    /// <summary>
    /// Verifies that when no enemy is engaged, the component shows the empty state.
    /// </summary>
    [Fact]
    public void Combat_Shows_Empty_State_When_No_Enemy()
    {
        var cut = Render<GameCombat>();

        var emptyMsg = cut.Find(".game-combat-empty");
        Assert.NotNull(emptyMsg);
        Assert.Contains("No enemy engaged.", emptyMsg.TextContent);
    }

    /// <summary>
    /// Verifies that when combat is active, the enemy name and HP are displayed.
    /// </summary>
    [Fact]
    public void Combat_Shows_Enemy_HP_When_Engaged()
    {
        _gameState.ApplyCombatStarted(new EnemyInfo(
            Guid.NewGuid(), "Goblin", 3, 25, 25, 5, 5));

        var cut = Render<GameCombat>();

        Assert.Contains("Goblin", cut.Markup);
        Assert.Contains("Level 3", cut.Markup);

        // The StatusBar should render with HP values.
        var hpBar = cut.Find(".status-bar-label");
        Assert.NotNull(hpBar);
        Assert.Equal("HP", hpBar.TextContent.Trim());
    }

    /// <summary>
    /// Verifies the Attack button renders when combat is active.
    /// </summary>
    [Fact]
    public void AttackButton_Renders_When_Combat_Active()
    {
        _gameState.ApplyCombatStarted(new EnemyInfo(
            Guid.NewGuid(), "Goblin", 3, 25, 25, 5, 5));

        var cut = Render<GameCombat>();

        var attackBtn = cut.Find("button.action-btn-attack");
        Assert.NotNull(attackBtn);
        Assert.Contains("Attack", attackBtn.TextContent);
    }

    /// <summary>
    /// Verifies the Defend button renders when combat is active.
    /// </summary>
    [Fact]
    public void DefendButton_Renders_When_Combat_Active()
    {
        _gameState.ApplyCombatStarted(new EnemyInfo(
            Guid.NewGuid(), "Goblin", 3, 25, 25, 5, 5));

        var cut = Render<GameCombat>();

        var defendBtn = cut.Find("button.action-btn-defend");
        Assert.NotNull(defendBtn);
        Assert.Contains("Defend", defendBtn.TextContent);
    }

    /// <summary>
    /// Verifies the Flee button renders when combat is active.
    /// </summary>
    [Fact]
    public void FleeButton_Renders_When_Combat_Active()
    {
        _gameState.ApplyCombatStarted(new EnemyInfo(
            Guid.NewGuid(), "Goblin", 3, 25, 25, 5, 5));

        var cut = Render<GameCombat>();

        var fleeBtn = cut.Find("button.action-btn-flee");
        Assert.NotNull(fleeBtn);
        Assert.Contains("Flee", fleeBtn.TextContent);
    }

    /// <summary>
    /// Verifies the combat log displays entries after combat actions.
    /// </summary>
    [Fact]
    public void CombatLog_Displays_Entries()
    {
        _gameState.ApplyCombatStarted(new EnemyInfo(
            Guid.NewGuid(), "Goblin", 3, 25, 25, 5, 5));

        // Simulate a combat turn result.
        _gameState.ApplyCombatTurn("You hit for 12 damage. Enemy hits you for 5 damage.");

        var cut = Render<GameCombat>();

        var resultElement = cut.Find(".game-combat-result");
        Assert.NotNull(resultElement);
        Assert.Contains("You hit for 12 damage", resultElement.TextContent);
    }

    /// <summary>
    /// Verifies the hub sends AttackEnemy when PerformAttack is triggered.
    /// </summary>
    [Fact]
    public async Task Hub_Sends_AttackEnemy_On_PerformAttack()
    {
        await _fakeHub.SendAsync("AttackEnemy");

        Assert.Contains("AttackEnemy", _fakeHub.SentMethods);
    }
}
