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
        services.AddSingleton<ReferenceResolverService>();
        services.AddSingleton<ContentTreeService>();
        services.AddSingleton<ContentEditorService>();
        services.AddSingleton<JsonEditorViewModel>();
        services.AddSingleton<MainWindowViewModel>(sp => new MainWindowViewModel(
            sp.GetRequiredService<JsonEditorViewModel>(),
            sp.GetRequiredService<EditorSettingsService>()));
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
        dir.Icon.Should().Be("📁");
        file.Icon.Should().Be("📄");
    }

    [Fact]
    public void JsonPropertyViewModel_TypeChecks_Are_Mutually_Exclusive()
    {
        var stringProp = new JsonPropertyViewModel { ValueType = JsonValueType.String };
        var boolProp = new JsonPropertyViewModel { ValueType = JsonValueType.Boolean };
        var refProp = new JsonPropertyViewModel { ValueType = JsonValueType.Reference };

        stringProp.IsEditable.Should().BeTrue();
        stringProp.IsBool.Should().BeFalse();
        stringProp.IsReference.Should().BeFalse();

        boolProp.IsBool.Should().BeTrue();
        boolProp.IsEditable.Should().BeFalse();

        refProp.IsReference.Should().BeTrue();
        refProp.IsEditable.Should().BeFalse();
    }
}
