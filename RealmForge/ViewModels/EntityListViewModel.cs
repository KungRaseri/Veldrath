using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using RealmForge.Services;
using Serilog;

namespace RealmForge.ViewModels;

public class EntityListViewModel : ReactiveObject
{
    private readonly ContentEditorService _service;
    private readonly Action<EntityListRowViewModel> _onOpen;
    private readonly Action _onNew;
    private readonly Action<Guid> _onDeleted;

    private string _filterText = string.Empty;
    private bool _isLoading;
    private EntityListRowViewModel? _pendingDelete;
    private List<EntityListRowViewModel> _allRows = [];

    public EntityListViewModel(
        string domainLabel,
        string typeKeyLabel,
        string tableName,
        string domain,
        string typeKey,
        ContentEditorService service,
        Action<EntityListRowViewModel> onOpen,
        Action onNew,
        Action<Guid> onDeleted)
    {
        DomainLabel   = domainLabel;
        TypeKeyLabel  = typeKeyLabel;
        TableName     = tableName;
        Domain        = domain;
        TypeKey       = typeKey;
        _service      = service;
        _onOpen       = onOpen;
        _onNew        = onNew;
        _onDeleted    = onDeleted;

        NewEntityCommand = ReactiveCommand.Create(_onNew);
        CancelDeleteCommand = ReactiveCommand.Create(() => { PendingDelete = null; });

        var canConfirm = this.WhenAnyValue(x => x.PendingDelete).Select(p => p is not null);
        ConfirmDeleteCommand = ReactiveCommand.CreateFromTask(ConfirmDeleteAsync, canConfirm);
        RefreshCommand       = ReactiveCommand.CreateFromTask(LoadAsync);

        RefreshCommand.ThrownExceptions.Subscribe(ex =>
            Log.Error(ex, "Failed to load entity list for {TableName}/{TypeKey}", tableName, typeKey));
        ConfirmDeleteCommand.ThrownExceptions.Subscribe(ex =>
            Log.Error(ex, "Delete failed for {TableName}", tableName));

        _ = LoadAsync();
    }

    public string DomainLabel  { get; }
    public string TypeKeyLabel { get; }
    public string TableName    { get; }
    public string Domain       { get; }
    public string TypeKey      { get; }

    public bool IsLoading
    {
        get => _isLoading;
        private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public string FilterText
    {
        get => _filterText;
        set
        {
            this.RaiseAndSetIfChanged(ref _filterText, value);
            ApplyFilter();
        }
    }

    public EntityListRowViewModel? PendingDelete
    {
        get => _pendingDelete;
        private set => this.RaiseAndSetIfChanged(ref _pendingDelete, value);
    }

    public int TotalCount => _allRows.Count;

    public ObservableCollection<EntityListRowViewModel> Items { get; } = new();

    public ReactiveCommand<Unit, Unit> NewEntityCommand     { get; }
    public ReactiveCommand<Unit, Unit> ConfirmDeleteCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelDeleteCommand  { get; }
    public ReactiveCommand<Unit, Unit> RefreshCommand       { get; }

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var dtos = await _service.GetEntityListAsync(TableName, TypeKey);
            _allRows = [];

            foreach (var dto in dtos)
            {
                // Use local variable so commands capture the correct row reference
                EntityListRowViewModel? row = null;
                row = new EntityListRowViewModel
                {
                    EntityId     = dto.EntityId,
                    Slug         = dto.Slug,
                    DisplayName  = dto.DisplayName,
                    RarityWeight = dto.RarityWeight,
                    IsActive     = dto.IsActive,
                    UpdatedAt    = dto.UpdatedAt,
                    TableName    = TableName
                };
                row.OpenCommand          = ReactiveCommand.Create(() => _onOpen(row!));
                row.RequestDeleteCommand = ReactiveCommand.Create(() => { PendingDelete = row!; });
                _allRows.Add(row);
            }

            ApplyFilter();
            this.RaisePropertyChanged(nameof(TotalCount));
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        Items.Clear();
        var filtered = string.IsNullOrWhiteSpace(FilterText)
            ? _allRows
            : _allRows.Where(r =>
                r.Slug.Contains(FilterText, StringComparison.OrdinalIgnoreCase)
                || (r.DisplayName?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ?? false));
        foreach (var row in filtered) Items.Add(row);
    }

    private async Task ConfirmDeleteAsync()
    {
        if (PendingDelete is null) return;
        var toDelete  = PendingDelete;
        PendingDelete = null;

        var ok = await _service.DeleteEntityAsync(toDelete.EntityId, TableName);
        if (!ok) return;

        _allRows.Remove(toDelete);
        ApplyFilter();
        this.RaisePropertyChanged(nameof(TotalCount));
        _onDeleted(toDelete.EntityId);
    }
}
