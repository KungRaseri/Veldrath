using System.Collections.ObjectModel;
using System.Reactive;
using Avalonia.Controls;
using ReactiveUI;
using RealmForge.Services;
using RealmForge.Views;
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
    private string _selectedActivityKey = "actors";
    private Dictionary<string, List<FileTreeNodeViewModel>> _nodesByActivity = new();
    private FileTreeNodeViewModel? _selectedDomainGroup;

    public MainWindowViewModel(
        EditorSettingsService settingsService,
        ContentEditorService contentEditorService,
        ContentTreeService contentTreeService)
    {
        _contentEditorService = contentEditorService;
        _contentTreeService = contentTreeService;

        const string iconBase = "avares://RealmForge/Resources/Icons/domains";
        ActivitySections =
        [
            new("actors",  "Actors",  $"{iconBase}/act-actors.png"),
            new("items",   "Items",   $"{iconBase}/act-items.png"),
            new("world",   "World",   $"{iconBase}/act-world.png"),
            new("powers",  "Powers",  $"{iconBase}/act-powers.png"),
            new("systems", "Systems", $"{iconBase}/act-systems.png"),
        ];
        ActivitySections[0].IsActive = true;

        TogglePaneCommand   = ReactiveCommand.Create(() => { IsPaneOpen = !IsPaneOpen; });
        ToggleThemeCommand  = ReactiveCommand.Create(() => { IsDarkMode = !IsDarkMode; });
        RefreshTreeCommand  = ReactiveCommand.CreateFromTask(LoadTreeAsync);
        ShowAboutCommand    = ReactiveCommand.CreateFromTask(ShowAboutAsync);
        SelectActivityCommand = ReactiveCommand.Create<string>(key =>
        {
            if (key == _selectedActivityKey)
                IsPaneOpen = !IsPaneOpen;
            else
            {
                SelectedActivityKey = key;
                IsPaneOpen = true;
            }
        });

        OpenTypeKeyCommand = ReactiveCommand.Create<FileTreeNodeViewModel>(node =>
        {
            if (node.IsDirectory && node.TableName is not null)
                OpenEntityList(node);
        });

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

    public string SelectedActivityKey
    {
        get => _selectedActivityKey;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedActivityKey, value);
            foreach (var s in ActivitySections)
                s.IsActive = s.Key == value;
            FilterTreeNodes();
        }
    }

    public IReadOnlyList<ActivitySectionViewModel> ActivitySections { get; }

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

    public FileTreeNodeViewModel? SelectedDomainGroup
    {
        get => _selectedDomainGroup;
        set => this.RaiseAndSetIfChanged(ref _selectedDomainGroup, value);
    }

    public ReactiveCommand<Unit, Unit> TogglePaneCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleThemeCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshTreeCommand { get; }
    public ReactiveCommand<string, Unit> SelectActivityCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowAboutCommand { get; }
    public ReactiveCommand<FileTreeNodeViewModel, Unit> OpenTypeKeyCommand { get; }

    public async Task LoadTreeAsync()
    {
        IsLoadingTree = true;
        TreePaneMessage = null;
        try
        {
            var nodes = await _contentTreeService.BuildTreeAsync();

            // Wire commands for all nodes once, before grouping by activity
            foreach (var n in nodes)
                WireNodeCommands(n);

            _nodesByActivity = nodes
                .GroupBy(n => n.ActivityKey ?? "systems")
                .ToDictionary(g => g.Key, g => g.ToList());

            var entityCount = nodes.Sum(n => n.Children.Sum(c => c.Children.Count));
            DbStatus = entityCount == 0
                ? "Connected — no entities yet"
                : $"Connected · {entityCount} entities";

            FilterTreeNodes();
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

    private void FilterTreeNodes()
    {
        TreeNodes.Clear();
        if (_nodesByActivity.TryGetValue(_selectedActivityKey, out var nodes))
            foreach (var n in nodes)
                TreeNodes.Add(n);
        SelectedDomainGroup = TreeNodes.FirstOrDefault();
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
        _ = LoadTreeAsync();
    }

    private Window? _ownerWindow;

    public void SetOwnerWindow(Window window) => _ownerWindow = window;

    private async Task ShowAboutAsync()
    {
        var dialog = new Window
        {
            Title             = "About RealmForge",
            Width             = 560,
            SizeToContent     = SizeToContent.Height,
            CanResize         = false,
            SystemDecorations = Avalonia.Controls.SystemDecorations.Full,
            Background        = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1e1e1e")),
            Content           = new AboutView { DataContext = new AboutViewModel() },
        };

        if (_ownerWindow is not null)
            await dialog.ShowDialog(_ownerWindow);
        else
            dialog.Show();
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

