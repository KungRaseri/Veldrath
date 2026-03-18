using System.Collections.ObjectModel;
using System.Windows.Input;
using ReactiveUI;

namespace RealmForge.ViewModels;

public class FileTreeNodeViewModel : ReactiveObject
{
    private bool _isExpanded;

    public string Name { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
    public bool IsDirectory { get; init; }
    public Guid? EntityId { get; init; }
    public string? TableName { get; init; }

    // Populated only on typeKey-level directory nodes
    public string? Domain      { get; init; }
    public string? TypeKey     { get; init; }
    public string? DomainLabel { get; init; }  // Human-readable domain for breadcrumbs
    public string? ActivityKey { get; init; }  // Which activity bar section this node belongs to

    // Wired by MainWindowViewModel after tree load
    public ICommand? NewEntityCommand { get; set; }
    public ICommand? DeleteCommand { get; set; }

    public ObservableCollection<FileTreeNodeViewModel> Children { get; } = new();

    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    public string  Icon     => IsDirectory ? "▶" : "·";
    public string? IconPath { get; init; }
    public int EntityCount  => Children.Sum(c => c.Children.Count);
}
