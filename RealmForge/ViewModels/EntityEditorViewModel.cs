using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using RealmForge.Services;
using Serilog;

namespace RealmForge.ViewModels;

public class EntityEditorViewModel : ReactiveObject
{
    private readonly ContentEditorService _editorService;
    private string _originalJson;
    private string _json;
    private bool _isDirty;
    private bool _isSaving;
    private string? _statusMessage;

    public EntityEditorViewModel(
        Guid entityId, string tableName, string displayName,
        ContentEditorService editorService, string initialJson)
    {
        EntityId = entityId;
        TableName = tableName;
        DisplayName = displayName;
        _editorService = editorService;
        _originalJson = initialJson;
        _json = initialJson;

        this.WhenAnyValue(x => x.Json)
            .Subscribe(j => IsDirty = j != _originalJson);

        var canSave = this.WhenAnyValue(
            x => x.IsDirty, x => x.IsSaving,
            (dirty, saving) => dirty && !saving);

        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync, canSave);
        DiscardCommand = ReactiveCommand.Create(Discard, this.WhenAnyValue(x => x.IsDirty));

        SaveCommand.ThrownExceptions
            .Subscribe(ex => Log.Error(ex, "Save error in EntityEditorViewModel"));
    }

    public Guid EntityId { get; }
    public string TableName { get; }
    public string DisplayName { get; }

    public string Json
    {
        get => _json;
        set => this.RaiseAndSetIfChanged(ref _json, value);
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set => this.RaiseAndSetIfChanged(ref _isDirty, value);
    }

    public bool IsSaving
    {
        get => _isSaving;
        private set => this.RaiseAndSetIfChanged(ref _isSaving, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> DiscardCommand { get; }

    private void Discard()
    {
        Json = _originalJson;
        StatusMessage = null;
    }

    private async Task SaveAsync()
    {
        IsSaving = true;
        StatusMessage = null;
        try
        {
            var success = await _editorService.SaveEntityJsonAsync(EntityId, TableName, Json);
            if (success)
            {
                _originalJson = Json;
                IsDirty = false;
                StatusMessage = "Saved successfully.";
            }
            else
            {
                StatusMessage = "Save failed — see logs for details.";
            }
        }
        finally
        {
            IsSaving = false;
        }
    }
}
