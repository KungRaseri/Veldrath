using Avalonia.Headless.XUnit;

namespace RealmUnbound.Client.Tests;

/// <summary>
/// Smoke-tests that every View can be constructed without throwing.
/// These verify that XAML is well-formed and InitializeComponent() works.
/// [AvaloniaFact] spins up the headless Avalonia runtime per test.
/// </summary>
public class ViewConstructionTests
{
    [AvaloniaFact]
    public void SplashView_Should_Construct_Without_Throwing()
    {
        var action = () => new Views.SplashView();
        action.Should().NotThrow();
    }

    [AvaloniaFact]
    public void LoginView_Should_Construct_Without_Throwing()
    {
        var action = () => new Views.LoginView();
        action.Should().NotThrow();
    }

    [AvaloniaFact]
    public void RegisterView_Should_Construct_Without_Throwing()
    {
        var action = () => new Views.RegisterView();
        action.Should().NotThrow();
    }

    [AvaloniaFact]
    public void MainMenuView_Should_Construct_Without_Throwing()
    {
        var action = () => new Views.MainMenuView();
        action.Should().NotThrow();
    }

    [AvaloniaFact]
    public void CharacterSelectView_Should_Construct_Without_Throwing()
    {
        var action = () => new Views.CharacterSelectView();
        action.Should().NotThrow();
    }

    [AvaloniaFact]
    public void GameView_Should_Construct_Without_Throwing()
    {
        var action = () => new Views.GameView();
        action.Should().NotThrow();
    }

    [AvaloniaFact]
    public void MainWindow_Should_Construct_Without_Throwing()
    {
        var action = () => new Views.MainWindow();
        action.Should().NotThrow();
    }
}
