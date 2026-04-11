using Veldrath.Client.Services;
using Veldrath.Client.Tests.Infrastructure;
using Veldrath.Client.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Veldrath.Client.Tests;

public class NavigationServiceTests : TestBase
{
    [Fact]
    public void NavigateTo_Direct_Should_Fire_CurrentPageChanged_Event()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var nav      = new NavigationService(services);
        var vm       = new MainMenuViewModel(nav, new TokenStore(), new FakeAuthService());

        ViewModelBase? received = null;
        nav.CurrentPageChanged += vw => received = vw;

        nav.NavigateTo(vm);

        received.Should().BeSameAs(vm);
    }

    [Fact]
    public void NavigateTo_Direct_Can_Be_Called_Multiple_Times()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var nav      = new NavigationService(services);
        var vm1      = new MainMenuViewModel(nav, new TokenStore(), new FakeAuthService());
        var vm2      = new MainMenuViewModel(nav, new TokenStore(), new FakeAuthService());

        var navigations = new List<ViewModelBase>();
        nav.CurrentPageChanged += vw => navigations.Add(vw);

        nav.NavigateTo(vm1);
        nav.NavigateTo(vm2);

        navigations.Should().HaveCount(2);
        navigations[0].Should().BeSameAs(vm1);
        navigations[1].Should().BeSameAs(vm2);
    }

    [Fact]
    public void NavigateTo_Generic_Should_Throw_When_ViewModel_Not_Registered()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var nav      = new NavigationService(services);

        // LoginViewModel is not registered — should throw
        Action act = () => nav.NavigateTo<LoginViewModel>();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*LoginViewModel*");
    }

    [Fact]
    public void NavigateTo_Generic_Should_Resolve_And_Navigate_When_Registered()
    {
        var services = new ServiceCollection();
        var fakeNav  = new FakeNavigationService();
        var fakeAuth = new FakeAuthService();

        services.AddSingleton<INavigationService>(fakeNav);
        services.AddSingleton<IAuthService>(fakeAuth);
        services.AddSingleton<SessionStore>(_ => SessionStoreFactory.Create());
        services.AddSingleton<LoginViewModel>();
        var provider = services.BuildServiceProvider();

        var nav = new NavigationService(provider);

        ViewModelBase? received = null;
        nav.CurrentPageChanged += vw => received = vw;

        nav.NavigateTo<LoginViewModel>();

        received.Should().NotBeNull();
        received.Should().BeOfType<LoginViewModel>();
    }
}
