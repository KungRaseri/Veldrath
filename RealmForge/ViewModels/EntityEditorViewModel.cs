using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using ReactiveUI;
using RealmEngine.Data.Entities;
using RealmForge.Services;
using Serilog;

namespace RealmForge.ViewModels;

public class EntityEditorViewModel : ReactiveObject
{
    private readonly ContentEditorService _service;
    private ContentBase _entity;

    private string _slug;
    private string? _displayName;
    private int _rarityWeight;
    private bool _isActive;
    private bool _isDirty;
    private bool _isSaving;
    private string? _statusMessage;

    public EntityEditorViewModel(ContentBase entity, string tableName, ContentEditorService service)
    {
        _service = service;
        _entity = entity;
        TableName = tableName;

        _slug = entity.Slug;
        _displayName = entity.DisplayName;
        _rarityWeight = entity.RarityWeight;
        _isActive = entity.IsActive;

        // EntityHeading: prefer DisplayName, fall back to Slug
        EntityHeading = entity.DisplayName ?? entity.Slug;
        TypeKey = entity.TypeKey;

        // Mark dirty when any base field changes
        this.WhenAnyValue(x => x.Slug, x => x.DisplayName, x => x.RarityWeight, x => x.IsActive)
            .Skip(1)
            .Subscribe(_ => IsDirty = true);

        FieldGroups = BuildFieldGroups(entity);

        var canSave = this.WhenAnyValue(
            x => x.IsDirty, x => x.IsSaving,
            (dirty, saving) => dirty && !saving);

        SaveCommand    = ReactiveCommand.CreateFromTask(SaveAsync, canSave);
        DiscardCommand = ReactiveCommand.CreateFromTask(DiscardAsync,
            this.WhenAnyValue(x => x.IsDirty));

        SaveCommand.ThrownExceptions
            .Subscribe(ex => Log.Error(ex, "Save error"));
        DiscardCommand.ThrownExceptions
            .Subscribe(ex => Log.Error(ex, "Discard error"));
    }

    // ── Identity (read-only) ────────────────────────────────────────────

    public Guid EntityId => _entity.Id;
    public string TableName { get; }
    public string EntityHeading { get; private set; }
    public string TypeKey { get; private set; }

    // ── Common ContentBase fields ───────────────────────────────────────

    public string Slug
    {
        get => _slug;
        set => this.RaiseAndSetIfChanged(ref _slug, value);
    }

    public string? DisplayName
    {
        get => _displayName;
        set => this.RaiseAndSetIfChanged(ref _displayName, value);
    }

    public int RarityWeight
    {
        get => _rarityWeight;
        set => this.RaiseAndSetIfChanged(ref _rarityWeight, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => this.RaiseAndSetIfChanged(ref _isActive, value);
    }

    // ── Status ──────────────────────────────────────────────────────────

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

    // ── Type-specific field groups (Stats, Traits, Effects, …) ─────────

    public IReadOnlyList<FieldGroupViewModel> FieldGroups { get; private set; }

    // ── Commands ────────────────────────────────────────────────────────

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> DiscardCommand { get; }

    // ── Internals ───────────────────────────────────────────────────────

    private async Task SaveAsync()
    {
        IsSaving = true;
        StatusMessage = null;
        try
        {
            // Write base fields back to the entity before persisting
            _entity.Slug = Slug;
            _entity.DisplayName = DisplayName;
            _entity.RarityWeight = RarityWeight;
            _entity.IsActive = IsActive;
            // FieldRows already write directly to the entity's nested objects in-place

            var success = await _service.SaveEntityAsync(_entity, TableName);
            if (success)
            {
                IsDirty = false;
                StatusMessage = "Saved.";
            }
            else
            {
                StatusMessage = "Save failed — check logs for details.";
            }
        }
        finally
        {
            IsSaving = false;
        }
    }

    private async Task DiscardAsync()
    {
        var reloaded = await _service.LoadEntityAsync(EntityId, TableName);
        if (reloaded is null)
        {
            StatusMessage = "Could not reload entity from database.";
            return;
        }

        _entity = reloaded;
        Slug = _entity.Slug;
        DisplayName = _entity.DisplayName;
        RarityWeight = _entity.RarityWeight;
        IsActive = _entity.IsActive;
        EntityHeading = _entity.DisplayName ?? _entity.Slug;
        TypeKey = _entity.TypeKey;
        this.RaisePropertyChanged(nameof(EntityHeading));
        this.RaisePropertyChanged(nameof(TypeKey));

        FieldGroups = BuildFieldGroups(_entity);
        this.RaisePropertyChanged(nameof(FieldGroups));

        IsDirty = false;
        StatusMessage = null;
    }

    private IReadOnlyList<FieldGroupViewModel> BuildFieldGroups(ContentBase entity)
    {
        var baseProps = typeof(ContentBase)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet();

        var groups = new List<FieldGroupViewModel>();

        foreach (var groupProp in entity.GetType()
                     .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => !baseProps.Contains(p.Name)
                                 && !IsCollectionProperty(p)
                                 && p.PropertyType.IsClass
                                 && p.PropertyType != typeof(string)))
        {
            // Ensure nested JSONB object is initialised
            var nested = groupProp.GetValue(entity)
                         ?? Activator.CreateInstance(groupProp.PropertyType)!;
            if (groupProp.GetValue(entity) is null)
                groupProp.SetValue(entity, nested);

            var fields = groupProp.PropertyType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => new FieldRowViewModel(p, nested))
                .ToList();

            foreach (var field in fields)
                field.FieldChanged += (_, _) => IsDirty = true;

            groups.Add(new FieldGroupViewModel(FormatGroupName(groupProp.Name), fields));
        }

        return groups;
    }

    private static bool IsCollectionProperty(PropertyInfo p) =>
        p.PropertyType is { IsGenericType: true } t
        && t.GetGenericTypeDefinition() == typeof(ICollection<>);

    private static string FormatGroupName(string name) =>
        System.Text.RegularExpressions.Regex
            .Replace(name, "(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ");
}

