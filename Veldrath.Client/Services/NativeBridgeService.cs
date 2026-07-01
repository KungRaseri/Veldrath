using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Veldrath.Client.Controls;

namespace Veldrath.Client.Services;

/// <summary>
/// Handles the JavaScript interop bridge between the embedded WebView2 (Blazor game UI)
/// and native Avalonia services (audio, notifications, system tray, clipboard).
/// Incoming messages from the WebView are dispatched to the appropriate service;
/// outgoing messages can be sent back to the WebView via <see cref="PostMessage"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class NativeBridgeService
{
    private readonly IAudioPlayer _audioPlayer;
    private readonly ILogger<NativeBridgeService>? _logger;
    private GameWebView? _webView;

    /// <summary>
    /// Initializes a new instance of the <see cref="NativeBridgeService"/> class.
    /// </summary>
    /// <param name="audioPlayer">The native audio player for music/SFX playback.</param>
    /// <param name="logger">Optional logger.</param>
    public NativeBridgeService(IAudioPlayer audioPlayer, ILogger<NativeBridgeService>? logger = null)
    {
        _audioPlayer = audioPlayer;
        _logger = logger;
    }

    /// <summary>
    /// Attaches the bridge service to a <see cref="GameWebView"/> control and begins
    /// listening for messages from the JavaScript bridge.
    /// </summary>
    /// <param name="webView">The WebView2 control hosting the Blazor game UI.</param>
    public void Attach(GameWebView webView)
    {
        _webView = webView;
        webView.BridgeMessageReceived += OnBridgeMessage;
    }

    /// <summary>
    /// Detaches from the WebView control and stops listening for messages.
    /// </summary>
    public void Detach()
    {
        if (_webView is not null)
        {
            _webView.BridgeMessageReceived -= OnBridgeMessage;
            _webView = null;
        }
    }

    /// <summary>
    /// Sends a message to the JavaScript bridge in the WebView.
    /// </summary>
    /// <param name="type">The message type (e.g. <c>"state:update"</c>).</param>
    /// <param name="data">Optional payload data to include.</param>
    public void PostMessage(string type, object? data = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["type"] = type,
            ["data"] = data,
        };

        var json = JsonSerializer.Serialize(payload);
        _webView?.PostMessage(json);
    }

    private void OnBridgeMessage(object? sender, string json)
    {
        BridgeMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<BridgeMessage>(json);
        }
        catch (JsonException ex)
        {
            _logger?.LogWarning(ex, "Failed to deserialize bridge message: {Json}", json);
            return;
        }

        if (message is null)
            return;

        switch (message.Type)
        {
            case "audio:play":
                HandlePlayAudio(message.Data?.GetProperty("url").GetString());
                break;
            case "audio:stop":
                HandleStopAudio();
                break;
            case "audio:setMusicVolume":
                HandleSetMusicVolume(message.Data?.GetProperty("volume").GetInt32() ?? 80);
                break;
            case "audio:setSfxVolume":
                HandleSetSfxVolume(message.Data?.GetProperty("volume").GetInt32() ?? 100);
                break;
            case "audio:setMuted":
                HandleSetMuted(message.Data?.GetProperty("muted").GetBoolean() ?? false);
                break;
            case "notification:show":
                HandleShowNotification(
                    message.Data?.GetProperty("title").GetString() ?? "",
                    message.Data?.GetProperty("message").GetString() ?? "");
                break;
            case "clipboard:copy":
                HandleClipboardCopy(message.Data?.GetProperty("text").GetString() ?? "");
                break;
            case "bridge:ready":
                _logger?.LogInformation("Native bridge is ready (WebView reported bridge:ready).");
                break;
            default:
                _logger?.LogDebug("Unhandled bridge message type: {Type}", message.Type);
                break;
        }
    }

    private void HandlePlayAudio(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return;

        try
        {
            _audioPlayer.PlayMusicAsync(url);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to play audio: {Url}", url);
        }
    }

    private void HandleStopAudio()
    {
        try
        {
            _audioPlayer.StopMusic();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to stop audio.");
        }
    }

    private void HandleSetMusicVolume(int volume)
    {
        try
        {
            _audioPlayer.SetMusicVolume(volume);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to set music volume to {Volume}.", volume);
        }
    }

    private void HandleSetSfxVolume(int volume)
    {
        try
        {
            _audioPlayer.SetSfxVolume(volume);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to set SFX volume to {Volume}.", volume);
        }
    }

    private void HandleSetMuted(bool muted)
    {
        try
        {
            _audioPlayer.SetMuted(muted);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to set muted state to {Muted}.", muted);
        }
    }

    private static void HandleShowNotification(string title, string message)
    {
        // On Windows, use the native notification API.
        if (OperatingSystem.IsWindows())
        {
            // Fallback — Avalonia doesn't have a built-in notification system.
            // Could be extended to use Windows Toast notifications via COM.
            System.Diagnostics.Debug.WriteLine($"Notification: {title} — {message}");
        }
    }

    private static void HandleClipboardCopy(string text)
    {
        // Clipboard access in Avalonia requires a TopLevel reference.
        // This is a best-effort implementation; the Blazor UI typically handles
        // clipboard access via the browser's built-in navigator.clipboard API.
        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime lifetime)
            {
                var clipboard = lifetime.MainWindow?.Clipboard;
                clipboard?.SetTextAsync(text);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Clipboard copy failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Deserialized bridge message from the WebView JavaScript interop.
    /// </summary>
    private sealed record BridgeMessage
    {
        /// <summary>The message type (e.g. <c>"audio:play"</c>, <c>"notification:show"</c>).</summary>
        public string Type { get; init; } = string.Empty;

        /// <summary>Optional payload data as a JSON element.</summary>
        public System.Text.Json.JsonElement? Data { get; init; }
    }
}
