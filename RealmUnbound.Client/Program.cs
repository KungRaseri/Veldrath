using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.ReactiveUI;
using Veldrath.Client;

[ExcludeFromCodeCoverage]
class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}
