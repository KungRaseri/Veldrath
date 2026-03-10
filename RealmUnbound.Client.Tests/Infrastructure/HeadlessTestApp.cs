using Avalonia;
using Avalonia.Headless;
using Avalonia.ReactiveUI;

namespace RealmUnbound.Client.Tests.Infrastructure;

/// <summary>
/// Minimal Avalonia application used to initialise the Avalonia runtime during
/// headless unit tests.  The static <see cref="BuildAvaloniaApp"/> method is
/// discovered automatically by <see cref="Avalonia.Headless.XUnit.AvaloniaFactAttribute"/>
/// via reflection — no assembly-level attribute is required.
/// </summary>
public class HeadlessTestApp : Application
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<HeadlessTestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .UseReactiveUI();

    public override void Initialize() { }

    public override void OnFrameworkInitializationCompleted()
        => base.OnFrameworkInitializationCompleted();
}
