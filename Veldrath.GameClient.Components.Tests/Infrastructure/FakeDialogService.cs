using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Veldrath.GameClient.Components.Tests.Infrastructure;

/// <summary>
/// Configurable stub for <see cref="IDialogService"/> used in bUnit tests.
/// Records the last dialog invocation and allows tests to control the dialog result.
/// </summary>
public sealed class FakeDialogService : IDialogService
{
    /// <summary>Gets the last dialog reference that was shown.</summary>
    public IDialogReference? LastDialog { get; private set; }

    /// <summary>Gets the last dialog parameters that were passed.</summary>
    public DialogParameters? LastParameters { get; private set; }

    /// <summary>
    /// Gets or sets the value the dialog should return via <see cref="DialogResult.Ok(object?)"/>.
    /// Default is <see langword="true"/> (simulates "Resume" click).
    /// </summary>
    public bool DialogResultValue { get; set; } = true;

    /// <inheritdoc />
    public IDialogReference Show<T>(string? title, DialogParameters parameters, DialogOptions? options)
        where T : ComponentBase
    {
        LastDialog = new FakeDialogReference(DialogResultValue);
        LastParameters = parameters;
        return LastDialog;
    }

    /// <inheritdoc />
    public IDialogReference Show<T>(string? title, DialogParameters parameters, DialogOptions? options, RenderFragment? titleContent)
        where T : ComponentBase
        => throw new NotSupportedException("Use the simpler Show<T> overload in tests.");

    /// <inheritdoc />
    public IDialogReference Show(Type componentType, string? title, DialogParameters parameters, DialogOptions? options)
        => throw new NotSupportedException("Use the generic ShowAsync<T> in tests.");

    // The primary method used by GameEntry. Must match IDialogService constraint.
    /// <inheritdoc />
    Task<IDialogReference> IDialogService.ShowAsync<T>(string? title, DialogParameters parameters, DialogOptions? options)
    {
        LastDialog = new FakeDialogReference(DialogResultValue);
        LastParameters = parameters;
        return Task.FromResult(LastDialog);
    }

    // All other ShowAsync overloads use explicit interface implementation to avoid CS0425.
    Task<IDialogReference> IDialogService.ShowAsync<T>() =>
        throw new NotSupportedException();
    Task<IDialogReference> IDialogService.ShowAsync<T>(string? title) =>
        throw new NotSupportedException();
    Task<IDialogReference> IDialogService.ShowAsync<T>(string? title, DialogOptions options) =>
        throw new NotSupportedException();
    Task<IDialogReference> IDialogService.ShowAsync<T>(string? title, DialogParameters parameters) =>
        throw new NotSupportedException();
    Task<IDialogReference> IDialogService.ShowAsync<T>(DialogParameters parameters) =>
        throw new NotSupportedException();
    Task<IDialogReference> IDialogService.ShowAsync(Type componentType, string? title, DialogParameters parameters, DialogOptions? options) =>
        throw new NotSupportedException();
    Task<IDialogReference> IDialogService.ShowAsync(Type componentType) =>
        throw new NotSupportedException();
    Task<IDialogReference> IDialogService.ShowAsync(Type componentType, string? title) =>
        throw new NotSupportedException();
    Task<IDialogReference> IDialogService.ShowAsync(Type componentType, string? title, DialogOptions? options) =>
        throw new NotSupportedException();
    Task<IDialogReference> IDialogService.ShowAsync(Type componentType, string? title, DialogParameters parameters) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public IDialogReference CreateReference() => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<bool?> ShowMessageBoxAsync(string? title, string message, string yesText = "Ok",
        string? noText = null, string? cancelText = null, DialogOptions? options = null)
        => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<bool?> ShowMessageBoxAsync(string? title, MarkupString markupMessage, string yesText = "Ok",
        string? noText = null, string? cancelText = null, DialogOptions? options = null)
        => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<bool?> ShowMessageBoxAsync(MessageBoxOptions messageBoxOptions, DialogOptions? options = null)
        => throw new NotSupportedException();

    /// <inheritdoc />
    public void ShowMessageBox(DialogOptions? options, string? title, MarkupString markupMessage, string yesText = "Ok")
        => throw new NotSupportedException();

    /// <inheritdoc />
    public void Close(IDialogReference dialog) { }

    /// <inheritdoc />
    public void Close(IDialogReference dialog, DialogResult? result) { }

    /// <inheritdoc />
    public event Func<IDialogReference, Task>? DialogInstanceAddedAsync { add { } remove { } }

    /// <inheritdoc />
    public event Action<IDialogReference, DialogResult?>? OnDialogCloseRequested { add { } remove { } }
}

/// <summary>
/// Configurable stub for <see cref="IDialogReference"/> used in bUnit tests.
/// Returns a pre-configured <see cref="DialogResult"/> immediately.
/// </summary>
internal sealed class FakeDialogReference : IDialogReference
{
    private readonly bool _resultValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeDialogReference"/> class.
    /// </summary>
    /// <param name="resultValue">The boolean value to include in the dialog result.</param>
    public FakeDialogReference(bool resultValue)
    {
        _resultValue = resultValue;
    }

    /// <inheritdoc />
    public Guid Id { get; } = Guid.NewGuid();

    /// <inheritdoc />
    public DialogOptions Options { get; } = new();

    /// <inheritdoc />
    public object Dialog { get; set; } = null!;

    /// <inheritdoc />
    public Task<DialogResult?> Result => Task.FromResult<DialogResult?>(DialogResult.Ok(_resultValue));

    /// <inheritdoc />
    public RenderFragment? RenderFragment { get; set; }

    /// <inheritdoc />
    public TaskCompletionSource<bool> RenderCompleteTaskCompletionSource { get; } = new();

    /// <inheritdoc />
    public void Close() { }

    /// <inheritdoc />
    public void Close(DialogResult? result) { }

    /// <inheritdoc />
    public bool Dismiss(DialogResult? result) => false;

    /// <inheritdoc />
    public Task<T?> GetReturnValueAsync<T>() => throw new NotSupportedException();

    /// <inheritdoc />
    public object? GetReturnValue() => throw new NotSupportedException();

    /// <inheritdoc />
    public void InjectDialog(object? obj) { }

    /// <inheritdoc />
    public void InjectRenderFragment(RenderFragment rf) => RenderFragment = rf;

    /// <inheritdoc />
    public void InjectOptions(DialogOptions options) { }
}
