using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Microsoft.Web.WebView2.Core;

namespace Veldrath.Client.Controls;

/// <summary>
/// Wraps a WebView2 control for embedding game UI inside the Avalonia desktop client.
/// Uses <see cref="NativeControlHost"/> to host a native Win32 child window that contains
/// the WebView2 control via the <c>Microsoft.Web.WebView2.Core</c> API.
///
/// This control is Windows-only (WebView2 runtime requirement).
/// When the runtime is unavailable, <see cref="IsAvailable"/> will be <c>false</c> and
/// consumers should fall back to native Avalonia game views.
/// </summary>
[ExcludeFromCodeCoverage]
public class GameWebView : NativeControlHost
{
    private IntPtr _childHwnd = IntPtr.Zero;
    private CoreWebView2Controller? _controller;
    private CoreWebView2? _coreWebView;
    private string? _pendingNavigateUrl;

    /// <summary>Gets whether the WebView2 runtime is available on this machine.</summary>
    public static bool IsAvailable
    {
        get
        {
            try
            {
                return OperatingSystem.IsWindows()
                    && CoreWebView2Environment.GetAvailableBrowserVersionString() is not null;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Raised when the underlying CoreWebView2 is fully initialized and the initial navigation
    /// has completed.
    /// </summary>
    public event EventHandler? NavigationCompleted;

    /// <summary>
    /// Raised when a JSON message is received from the JavaScript bridge
    /// (<c>window.chrome.webview.postMessage</c>).
    /// </summary>
    public event EventHandler<string>? BridgeMessageReceived;

    /// <summary>
    /// Navigates the WebView2 control to the specified URL.
    /// Can be called before the control is fully initialized; the navigation will be deferred.
    /// </summary>
    /// <param name="url">The URL to navigate to (typically <c>http://localhost:{port}/Game/CharacterSelect</c>).</param>
    public void Navigate(string url)
    {
        if (_coreWebView is not null)
        {
            _coreWebView.Navigate(url);
        }
        else
        {
            _pendingNavigateUrl = url;
        }
    }

    /// <summary>
    /// Sends a JSON message to the JavaScript bridge in the WebView via
    /// <c>window.chrome.webview.postMessage</c>.
    /// </summary>
    /// <param name="jsonPayload">The serialized JSON payload to send.</param>
    public void PostMessage(string jsonPayload)
    {
        try
        {
            _coreWebView?.PostWebMessageAsJson(jsonPayload);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebView2 PostMessage failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        if (!OperatingSystem.IsWindows())
            return base.CreateNativeControlCore(parent);

        _childHwnd = CreateChildWindow(parent.Handle);
        _ = InitializeWebView2Async(_childHwnd);

        return new PlatformHandle(_childHwnd, "HWND");
    }

    /// <inheritdoc />
    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        if (_controller is not null)
        {
            try { _controller.Close(); } catch { }
            _controller = null;
        }

        _coreWebView = null;

        if (_childHwnd != IntPtr.Zero)
        {
            DestroyWindow(_childHwnd);
            _childHwnd = IntPtr.Zero;
        }

        base.DestroyNativeControlCore(control);
    }

    private async Task InitializeWebView2Async(IntPtr hwnd)
    {
        try
        {
            var environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: null,
                options: null);

            _controller = await environment.CreateCoreWebView2ControllerAsync(hwnd);
            _coreWebView = _controller.CoreWebView2;

            // Configure WebView2 settings.
            _coreWebView.Settings.IsScriptEnabled = true;
            _coreWebView.Settings.AreDefaultScriptDialogsEnabled = true;
            _coreWebView.Settings.IsWebMessageEnabled = true;
            _coreWebView.Settings.IsZoomControlEnabled = false;
            _coreWebView.Settings.AreDevToolsEnabled = false;
            _coreWebView.Settings.AreDefaultContextMenusEnabled = false;

            // Listen for messages from the JavaScript bridge.
            _coreWebView.WebMessageReceived += (_, args) =>
            {
                BridgeMessageReceived?.Invoke(this, args.TryGetWebMessageAsString() ?? "{}");
            };

            // Suppress navigation to external URLs (stay within the embedded server).
            _coreWebView.NavigationStarting += (_, args) =>
            {
                if (args.Uri is not null
                    && !args.Uri.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase)
                    && !args.Uri.StartsWith("about:blank", StringComparison.OrdinalIgnoreCase))
                {
                    args.Cancel = true;
                }
            };

            _coreWebView.NewWindowRequested += (_, args) =>
            {
                args.Handled = true; // Prevent new windows.
            };

            _coreWebView.NavigationCompleted += (_, _) =>
            {
                NavigationCompleted?.Invoke(this, EventArgs.Empty);
            };

            // Navigate to any pending URL.
            if (_pendingNavigateUrl is not null)
            {
                _coreWebView.Navigate(_pendingNavigateUrl);
                _pendingNavigateUrl = null;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebView2 initialization failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a native Win32 child window that will host the WebView2 control.
    /// The window is initially sized to 1x1 and will be resized by the Avalonia layout system.
    /// </summary>
    private static IntPtr CreateChildWindow(IntPtr parentHwnd)
    {
        const string className = "VeldrathWebView2Host";

        // Register window class if not already registered.
        var hInstance = Marshal.GetHINSTANCE(typeof(GameWebView).Module);
        var wc = new WNDCLASSEX
        {
            cbSize = Marshal.SizeOf<WNDCLASSEX>(),
            style = 0,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate<WndProc>(DefWindowProc),
            hInstance = hInstance,
            lpszClassName = className,
        };

        if (RegisterClassEx(ref wc) == 0 && Marshal.GetLastWin32Error() != 0x582) // 0x582 = CLASS_ALREADY_EXISTS
        {
            // Window class already registered or error — proceed anyway.
        }

        var hwnd = CreateWindowEx(
            0,                                   // dwExStyle
            className,                           // lpClassName
            null,                                // lpWindowName
            unchecked((int)0x40000000),          // dwStyle = WS_CHILD
            0, 0, 1, 1,                         // x, y, width, height
            parentHwnd,                          // hWndParent
            IntPtr.Zero,                         // hMenu
            hInstance,                           // hInstance
            IntPtr.Zero);                        // lpParam

        return hwnd;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private static readonly WndProc DefWindowProc = DefWindowProcStatic;

    private static IntPtr DefWindowProcStatic(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    #region Win32 P/Invoke

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public int cbSize;
        public int style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hBrush;
        public string lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx([In] ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        int dwExStyle,
        string lpClassName,
        string? lpWindowName,
        int dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    #endregion
}
