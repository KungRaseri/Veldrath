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
    private string _dbStatus = "Connecting…";
    private string? _treePaneMessage;

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
        private set
        {
            this.RaiseAndSetIfChanged(ref _isLoadingTree, value);
            this.RaisePropertyChanged(nameof(IsTreeVisible));
        }
    }

    /// <summary>Short status text shown in the sidebar footer (e.g. "Connected" / connection error).</summary>
    public string DbStatus
    {
        get => _dbStatus;
        private set => this.RaiseAndSetIfChanged(ref _dbStatus, value);
    }

    /// <summary>Non-null only when the database is unreachable — explains the error.</summary>
    public string? TreePaneMessage
    {
        get => _treePaneMessage;
        private set
        {
            this.RaiseAndSetIfChanged(ref _treePaneMessage, value);
            this.RaisePropertyChanged(nameof(HasTreePaneMessage));
            this.RaisePropertyChanged(nameof(IsTreeVisible));
        }
    }

    public bool HasTreePaneMessage => TreePaneMessage is not null;

    /// <summary>True when the tree should be shown — DB is reachable and not currently loading.</summary>
    public bool IsTreeVisible => !_isLoadingTree && !HasTreePaneMessage;

    /// <summary>
    /// Bound to TreeView.SelectedItem — opens leaf nodes in the editor, TypeKey directories in the list.
    /// </summary>
    public FileTreeNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedNode, value);
            if (value is { IsDirectory: false })
                _ = OpenEntityAsync(value);
            else if (value is { IsDirectory: true, TableName: not null })
                OpenEntityList(value);
        }
    }

    public ObservableCollection<FileTreeNodeViewModel> TreeNodes { get; } = new();

    public ReactiveCommand<Unit, Unit> TogglePaneCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleThemeCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshTreeCommand { get; }

    public async Task LoadTreeAsync()
    {
        IsLoadingTree = true;
        TreePaneMessage = null;
        try
        {
            var nodes = await _contentTreeService.BuildTreeAsync();
            TreeNodes.Clear();
            foreach (var n in nodes)
            {
                WireNodeCommands(n);
                TreeNodes.Add(n);
            }

            var entityCount = nodes.Sum(n => n.Children.Sum(c => c.Children.Count));
            DbStatus = entityCount == 0
                ? "Connected — no entities yet"
                : $"Connected · {entityCount} entities";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load content tree");
            DbStatus = "DB unavailable";
            TreePaneMessage = "Cannot reach the database.\nMake sure Docker / Postgres\nis running on port 5433.";
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

    private async Task OpenEntityByIdAsync(Guid entityId, string tableName)
    {
        var entity = await _contentEditorService.LoadEntityAsync(entityId, tableName);
        if (entity is null) return;
        CurrentPage = new EntityEditorViewModel(entity, tableName, _contentEditorService);
    }

    private void OpenEntityList(FileTreeNodeViewModel typeKeyNode)
    {
        CurrentPage = new EntityListViewModel(
            domainLabel:  typeKeyNode.DomainLabel ?? typeKeyNode.Domain ?? "",
            typeKeyLabel: typeKeyNode.Name,
            tableName:    typeKeyNode.TableName!,
            domain:       typeKeyNode.Domain!,
            typeKey:      typeKeyNode.TypeKey!,
            service:      _contentEditorService,
            onOpen:    row => _ = OpenEntityByIdAsync(row.EntityId, row.TableName),
            onNew:     () => _ = StartNewEntityAsync(typeKeyNode),
            onDeleted: deletedId =>
            {
                if (CurrentPage is EntityEditorViewModel vm && vm.EntityId == deletedId)
                    CurrentPage = new HomeViewModel();
                _ = LoadTreeAsync();
            });
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

