using System.Collections.ObjectModel;
using ReactiveUI;

namespace RealmForge.ViewModels;

public class FileTreeNodeViewModel : ReactiveObject
{
    private bool _isExpanded;

    public string Name { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
    public bool IsDirectory { get; init; }
    public ObservableCollection<FileTreeNodeViewModel> Children { get; } = new();

    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    public string Icon => IsDirectory ? "📁" : "📄";
}
