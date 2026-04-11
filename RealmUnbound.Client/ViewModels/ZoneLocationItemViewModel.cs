using System.Reactive;
using ReactiveUI;

namespace Veldrath.Client.ViewModels;

/// <summary>Display model for a single zone location shown in the in-game locations panel.</summary>
public sealed class ZoneLocationItemViewModel : ViewModelBase
{
    private bool _isCurrent;

    /// <summary>Initializes a new instance of <see cref="ZoneLocationItemViewModel"/>.</summary>
    /// <param name="slug">The slug identifier of this location.</param>
    /// <param name="displayName">The display name.</param>
    /// <param name="typeKey">The location type key (e.g. "dungeon", "town", "environment").</param>
    /// <param name="minLevel">Optional minimum character level recommendation.</param>
    /// <param name="isCurrent">Whether the character is currently at this location.</param>
    /// <param name="onNavigate">Async callback invoked when the player navigates to this location.</param>
    public ZoneLocationItemViewModel(string slug, string displayName, string typeKey,
        int? minLevel, bool isCurrent, Func<Task>? onNavigate = null)
    {
        Slug         = slug;
        DisplayName  = displayName;
        TypeKey      = typeKey;
        MinLevel     = minLevel;
        _isCurrent   = isCurrent;
        NavigateCommand = onNavigate is not null && !isCurrent
            ? ReactiveCommand.CreateFromTask(onNavigate)
            : null;
    }

    /// <summary>Gets the slug identifier of this location.</summary>
    public string Slug { get; }

    /// <summary>Gets the display name of this location.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the location type key classification.</summary>
    public string TypeKey { get; }

    /// <summary>Gets the minimum recommended character level, or <see langword="null"/> if not specified.</summary>
    public int? MinLevel { get; }

    /// <summary>Gets or sets whether the character is currently at this location.</summary>
    public bool IsCurrent
    {
        get => _isCurrent;
        set
        {
            this.RaiseAndSetIfChanged(ref _isCurrent, value);
            this.RaisePropertyChanged(nameof(CanNavigate));
        }
    }

    /// <summary>Gets whether the character can navigate to this location (i.e. they are not already here).</summary>
    public bool CanNavigate => !IsCurrent;

    /// <summary>Gets the command that navigates to this location, or <see langword="null"/> when this is the current location.</summary>
    public ReactiveCommand<Unit, Unit>? NavigateCommand { get; }
}
