using System.Diagnostics.CodeAnalysis;
using System.Reactive.Linq;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Veldrath.Client.Controls;
using Veldrath.Client.Services;
using Veldrath.Client.ViewModels;

namespace Veldrath.Client.Views;

/// <summary>Center panel shell: hosts the WebView2 Blazor game UI or the native fallback.</summary>
[ExcludeFromCodeCoverage]
public partial class GameCenterPanelView : UserControl
{
    private GameWebView? _webView;

    /// <summary>Initializes a new instance of the <see cref="GameCenterPanelView"/> class.</summary>
    public GameCenterPanelView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is GameViewModel vm)
        {
            // When IsWebViewActive becomes true, initialize the WebView2 control
            // and navigate to the embedded Blazor game server.
            vm.WhenAnyValue(x => x.IsWebViewActive)
                .Where(active => active)
                .Take(1)
                .Subscribe(async _ =>
                {
                    if (vm.WebViewUrl is { } url)
                        await InitializeWebViewAsync(url);
                });
        }
    }

    /// <summary>
    /// Initializes the WebView2 control and places it inside <see cref="WebViewContainer"/>.
    /// Called by the view when the ViewModel signals that the embedded server has started.
    /// </summary>
    /// <param name="embeddedServerUrl">The URL of the embedded Blazor server.</param>
    public async Task InitializeWebViewAsync(string embeddedServerUrl)
    {
        if (_webView is not null)
            return;

        if (!GameWebView.IsAvailable)
            return;

        _webView = new GameWebView();

        // Wire up native bridge for audio/notifications via WebView JavaScript interop.
        var bridge = App.Services.GetService<NativeBridgeService>();
        if (bridge is not null)
        {
            bridge.Attach(_webView);
        }

        // Subscribe to navigation completion for diagnostics.
        _webView.NavigationCompleted += (_, _) =>
            System.Diagnostics.Debug.WriteLine("GameWebView navigation completed.");

        // Place the WebView control inside the container.
        WebViewContainer.Child = _webView;

        // Navigate to the embedded server's game UI.
        _webView.Navigate(embeddedServerUrl);
    }

    /// <summary>Gets the WebView2 control, or <c>null</c> if not available or not yet initialized.</summary>
    public GameWebView? WebView => _webView;

    /// <summary>
    /// Sends a JSON message to the JavaScript bridge in the WebView.
    /// </summary>
    /// <param name="jsonPayload">The serialized JSON payload.</param>
    public void PostMessage(string jsonPayload)
    {
        _webView?.PostMessage(jsonPayload);
    }
}
