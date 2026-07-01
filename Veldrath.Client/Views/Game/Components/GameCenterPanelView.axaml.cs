using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Veldrath.Client.Controls;

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

    /// <summary>
    /// Initializes the WebView2 control and places it inside <see cref="WebViewContainer"/>.
    /// Called by the ViewModel when the embedded server has started.
    /// </summary>
    /// <param name="embeddedServerUrl">The URL of the embedded Blazor server.</param>
    public async Task InitializeWebViewAsync(string embeddedServerUrl)
    {
        if (_webView is not null)
            return;

        if (!GameWebView.IsAvailable)
            return;

        _webView = new GameWebView();

        // Wait for the control to be created.
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
