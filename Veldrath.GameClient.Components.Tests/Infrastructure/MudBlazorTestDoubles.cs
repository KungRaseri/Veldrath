using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using MudBlazor.Interop;
using MudBlazor.Services;

namespace Veldrath.GameClient.Components.Tests.Infrastructure;

/// <summary>
/// No-op <see cref="IKeyInterceptorService"/> double. Implements both
/// <see cref="IDisposable"/> and <see cref="IAsyncDisposable"/> so bUnit's
/// synchronous test-container disposal does not throw for MudBlazor's
/// async-dispose-only production service.
/// </summary>
public sealed class NoopKeyInterceptorService : IKeyInterceptorService, IDisposable
{
    /// <inheritdoc />
    public Task SubscribeAsync(IKeyInterceptorObserver observer, KeyInterceptorOptions options) => Task.CompletedTask;

    /// <inheritdoc />
    public Task SubscribeAsync(string elementId, KeyInterceptorOptions options, Action<KeyMapBuilder> configure) => Task.CompletedTask;

    /// <inheritdoc />
    public Task SubscribeAsync(string elementId, KeyInterceptorOptions options, IKeyDownObserver keyDown, IKeyUpObserver keyUp) => Task.CompletedTask;

    /// <inheritdoc />
    public Task SubscribeAsync(string elementId, KeyInterceptorOptions options, Action<KeyboardEventArgs> keyDown, Action<KeyboardEventArgs> keyUp) => Task.CompletedTask;

    /// <inheritdoc />
    public Task SubscribeAsync(string elementId, KeyInterceptorOptions options, Func<KeyboardEventArgs, Task> keyDown, Func<KeyboardEventArgs, Task> keyUp) => Task.CompletedTask;

    /// <inheritdoc />
    public Task DispatchAsync(string elementId, KeyEventKind kind, KeyboardEventArgs args) => Task.CompletedTask;

    /// <inheritdoc />
    public Task UpdateKeyAsync(IKeyInterceptorObserver observer, KeyOptions option) => Task.CompletedTask;

    /// <inheritdoc />
    public Task UpdateKeyAsync(string elementId, KeyOptions option) => Task.CompletedTask;

    /// <inheritdoc />
    public Task UnsubscribeAsync(IKeyInterceptorObserver observer) => Task.CompletedTask;

    /// <inheritdoc />
    public Task UnsubscribeAsync(string elementId) => Task.CompletedTask;

    /// <inheritdoc />
    public void Dispose() { }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Creates no-op <see cref="IResizeObserver"/> instances for bUnit tests.
/// </summary>
public sealed class NoopResizeObserverFactory : IResizeObserverFactory
{
    /// <inheritdoc />
    public IResizeObserver Create() => new NoopResizeObserver();

    /// <inheritdoc />
    public IResizeObserver Create(ResizeObserverOptions options) => new NoopResizeObserver();
}

/// <summary>
/// No-op <see cref="IResizeObserver"/> double with both synchronous and
/// asynchronous disposal so bUnit can dispose the test container synchronously.
/// </summary>
public sealed class NoopResizeObserver : IResizeObserver, IDisposable
{
#pragma warning disable CS0067 // Test double never raises resize callbacks.
    /// <inheritdoc />
    public event SizeChanged? OnResized;
#pragma warning restore CS0067

    /// <inheritdoc />
    public Task<BoundingClientRect> Observe(ElementReference element) =>
        Task.FromResult(default(BoundingClientRect));

    /// <inheritdoc />
    public Task<IEnumerable<BoundingClientRect>> Observe(IEnumerable<ElementReference> elements) =>
        Task.FromResult<IEnumerable<BoundingClientRect>>([]);

    /// <inheritdoc />
    public Task Unobserve(ElementReference element) => Task.CompletedTask;

    /// <inheritdoc />
    public double GetWidth(ElementReference reference) => 0;

    /// <inheritdoc />
    public double GetHeight(ElementReference reference) => 0;

    /// <inheritdoc />
    public BoundingClientRect GetSizeInfo(ElementReference reference) => default;

    /// <inheritdoc />
    public bool IsElementObserved(ElementReference reference) => false;

    /// <inheritdoc />
    public void Dispose() { }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
