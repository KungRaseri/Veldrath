using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RealmForge.Services;
using RealmForge.ViewModels;

namespace RealmForge.Tests;

/// <summary>Infrastructure smoke tests — verifies DI setup and core types compile/instantiate.</summary>
public class InfrastructureTests
{
    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<EditorSettingsService>();
        services.AddSingleton<ContentEditorService>();
        services.AddSingleton<ContentTreeService>();
        services.AddSingleton<MainWindowViewModel>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void ServiceCollection_Should_Build_Without_Errors()
    {
        var act = BuildServices;
        act.Should().NotThrow();
    }

    [Fact]
    public void EditorSettingsService_Should_Resolve()
    {
        var sp = BuildServices();
        var svc = sp.GetRequiredService<EditorSettingsService>();
        svc.Should().NotBeNull();
    }

    [Fact]
    public void HomeViewModel_Should_Have_Expected_Properties()
    {
        var vm = new HomeViewModel();
        vm.Title.Should().Be("RealmForge");
        vm.Subtitle.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void FileTreeNodeViewModel_IsDirectory_Formats_Icon_Correctly()
    {
        var dir = new FileTreeNodeViewModel { IsDirectory = true };
        var file = new FileTreeNodeViewModel { IsDirectory = false };
        dir.Icon.Should().Be("▶");
        file.Icon.Should().Be("·");
    }

}
