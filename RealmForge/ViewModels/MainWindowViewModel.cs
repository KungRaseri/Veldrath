using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;
using RealmForge.Services;
using Serilog;

namespace RealmForge.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
    private readonly ContentEditorService _contentEditorService;
    private readonly ContentTreeService _contentTreeService;
    private object? _currentPage;
    private bool _isPaneOpen = true;
    private bool _isDarkMode = true;
    private bool _isLoadingTree;
    private FileTreeNodeViewModel? _selectedNode;

    public MainWindowViewModel(
        EditorSettingsService settingsService,
        ContentEditorService contentEditorService,
        ContentTreeService contentTreeService)
    {
        _contentEditorService = contentEditorService;
        _contentTreeService = contentTreeService;

        TogglePaneCommand = ReactiveCommand.Create(() => { IsPaneOpen = !IsPaneOpen; });
        ToggleThemeCommand = ReactiveCommand.Create(() => { IsDarkMode = !IsDarkMode; });
        RefreshTreeCommand = ReactiveCommand.CreateFromTask(LoadTreeAsync);

        CurrentPage = new HomeViewModel();
        _ = LoadSettingsAsync(settingsService);
        _ = LoadTreeAsync();
    }

    public object? CurrentPage
    {
        get => _currentPage;
        private set => this.RaiseAndSetIfChanged(ref _currentPage, value);
    }

    public bool IsPaneOpen
    {
        get => _isPaneOpen;
        set => this.RaiseAndSetIfChanged(ref _isPaneOpen, value);
    }

    public bool IsDarkMode
    {
        get => _isDarkMode;
        set => this.RaiseAndSetIfChanged(ref _isDarkMode, value);
    }

    public bool IsLoadingTree
    {
        get => _isLoadingTree;
        private set => this.RaiseAndSetIfChanged(ref _isLoadingTree, value);
    }

    /// <summary>
    /// Bound to TreeView.SelectedItem — opens leaf nodes in the editor automatically.
    /// </summary>
    public FileTreeNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedNode, value);
            if (value is { IsDirectory: false })
                _ = OpenEntityAsync(value);
        }
    }

    public ObservableCollection<FileTreeNodeViewModel> TreeNodes { get; } = new();

    public ReactiveCommand<Unit, Unit> TogglePaneCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleThemeCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshTreeCommand { get; }

    public async Task LoadTreeAsync()
    {
        IsLoadingTree = true;
        try
        {
            var nodes = await _contentTreeService.BuildTreeAsync();
            TreeNodes.Clear();
            foreach (var n in nodes)
            {
                WireNodeCommands(n);
                TreeNodes.Add(n);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load content tree");
        }
        finally
        {
            IsLoadingTree = false;
        }
    }

    private void WireNodeCommands(FileTreeNodeViewModel node)
    {
        // TypeKey directory nodes get a "New Entity" command
        if (node.IsDirectory && node.TableName is not null)
            node.NewEntityCommand = ReactiveCommand.CreateFromTask(
                () => StartNewEntityAsync(node));

        // Leaf nodes get a "Delete" command
        if (!node.IsDirectory)
            node.DeleteCommand = ReactiveCommand.CreateFromTask(
                () => DeleteEntityAsync(node));

        foreach (var child in node.Children)
            WireNodeCommands(child);
    }

    private async Task OpenEntityAsync(FileTreeNodeViewModel node)
    {
        if (node.EntityId is null || node.TableName is null) return;
        var entity = await _contentEditorService.LoadEntityAsync(node.EntityId.Value, node.TableName);
        if (entity is null) return;
        CurrentPage = new EntityEditorViewModel(entity, node.TableName, _contentEditorService);
    }

    private Task StartNewEntityAsync(FileTreeNodeViewModel typeKeyNode)
    {
        CurrentPage = new NewEntityViewModel(
            typeKeyNode,
            _contentEditorService,
            onCreated: editor =>
            {
                CurrentPage = editor;
                _ = LoadTreeAsync();
            },
            onCancel: () => CurrentPage = new HomeViewModel());
        return Task.CompletedTask;
    }

    private async Task DeleteEntityAsync(FileTreeNodeViewModel leafNode)
    {
        if (leafNode.EntityId is null || leafNode.TableName is null) return;
        var deleted = await _contentEditorService.DeleteEntityAsync(
            leafNode.EntityId.Value, leafNode.TableName);
        if (!deleted) return;

        // Clear editor if it had the deleted entity open
        if (CurrentPage is EntityEditorViewModel editor && editor.EntityId == leafNode.EntityId)
            CurrentPage = new HomeViewModel();

        await LoadTreeAsync();
    }

    private async Task LoadSettingsAsync(EditorSettingsService settingsService)
    {
        try
        {
            var settings = await settingsService.LoadSettingsAsync();
            IsDarkMode = settings.Theme == "Dark";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load settings for theme");
        }
    }
}

