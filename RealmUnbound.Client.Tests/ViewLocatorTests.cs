using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using RealmUnbound.Client.Services;
using RealmUnbound.Client.Tests.Infrastructure;
using RealmUnbound.Client.ViewModels;

namespace RealmUnbound.Client.Tests;

public class ViewLocatorTests
{
    private readonly ViewLocator _sut = new();

    // ── Match ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Match_Should_Return_True_For_ViewModelBase()
    {
        _sut.Match(new MinimalViewModel()).Should().BeTrue();
    }

    [Fact]
    public void Match_Should_Return_False_For_Non_ViewModel()
    {
        _sut.Match("a plain string").Should().BeFalse();
    }

    [Fact]
    public void Match_Should_Return_False_For_Null()
    {
        _sut.Match(null).Should().BeFalse();
    }

    // ── Build (null – no Avalonia runtime needed) ─────────────────────────────

    [Fact]
    public void Build_Should_Return_Null_When_Data_Is_Null()
    {
        _sut.Build(null).Should().BeNull();
    }

    // ── Build (requires Avalonia runtime) ─────────────────────────────────────

    [AvaloniaFact]
    public void Build_Should_Return_TextBlock_When_View_Not_Found()
    {
        var result = _sut.Build(new MinimalViewModel());

        var textBlock = result.Should().BeOfType<TextBlock>().Subject;
        textBlock.Text.Should().Contain("View not found");
    }

    [AvaloniaFact]
    public void Build_Should_Return_SplashView_For_SplashViewModel()
    {
        var vm = new SplashViewModel(new FakeNavigationService(), new FakeAssetStore(), new TokenStore(), new FakeAuthService());

        var result = _sut.Build(vm);

        result.Should().BeOfType<Views.SplashView>();
    }

    [AvaloniaFact]
    public void Build_Should_Return_LoginView_For_LoginViewModel()
    {
        var vm = new LoginViewModel(new FakeAuthService(), new FakeNavigationService(), SessionStoreFactory.Create());

        var result = _sut.Build(vm);

        result.Should().BeOfType<Views.LoginView>();
    }
}

/// <summary>
/// Minimal concrete ViewModel whose type name has no matching View.
/// Used to exercise the "View not found" path in ViewLocator.
/// </summary>
file class MinimalViewModel : ViewModelBase { }
