using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;

namespace Veldrath.Client.ViewModels;

/// <summary>Represents a single outgoing connection link from the character's current zone location.</summary>
public sealed class ZoneConnectionLinkViewModel : ViewModelBase
{
    /// <summary>Gets the connection type identifier (e.g. <c>"path"</c>, <c>"door"</c>).</summary>
    public string ConnectionType { get; }

    /// <summary>Gets the destination location slug.</summary>
    public string ToLocationSlug { get; }

    /// <summary>Gets whether the connection can currently be traversed.</summary>
    public bool IsTraversable { get; }

    /// <summary>Gets the label shown on the connection button (e.g. <c>"path →"</c>).</summary>
    public string Label => $"{ConnectionType} →";

    /// <summary>Gets the command that traverses this connection.</summary>
    public ReactiveCommand<Unit, Unit> TraverseCommand { get; }

    /// <summary>Initializes a new instance of <see cref="ZoneConnectionLinkViewModel"/>.</summary>
    /// <param name="toLocationSlug">The slug of the destination location.</param>
    /// <param name="connectionType">The type of connection (e.g. <c>"path"</c>).</param>
    /// <param name="isTraversable">Whether the connection is currently open.</param>
    /// <param name="onTraverse">Async callback invoked when the player traverses this connection.</param>
    public ZoneConnectionLinkViewModel(
        string toLocationSlug,
        string connectionType,
        bool isTraversable,
        Func<Task> onTraverse)
    {
        ConnectionType  = connectionType;
        ToLocationSlug  = toLocationSlug;
        IsTraversable   = isTraversable;
        TraverseCommand = ReactiveCommand.CreateFromTask(
            onTraverse,
            canExecute: Observable.Return(isTraversable));
    }
}
