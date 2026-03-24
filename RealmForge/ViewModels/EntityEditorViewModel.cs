using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using ReactiveUI;
using RealmEngine.Data.Entities;
using RealmForge.Services;
using RealmUnbound.Contracts.Content;
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

        JunctionEditors = BuildJunctionEditors(entity);
        if (JunctionEditors?.Count > 0)
            _ = InitializeJunctionEditorsAsync();

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

    // Identity (read-only)
    public Guid EntityId => _entity.Id;
    public string TableName { get; }
    public string EntityHeading { get; private set; }
    public string TypeKey { get; private set; }

    // Common ContentBase fields
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

    // Status
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

    // Type-specific field groups (Stats, Traits, Effects, …)
    public IReadOnlyList<FieldGroupViewModel> FieldGroups { get; private set; }
    // Junction sub-editors (LootTable entries, RecipeIngredients, AbilityPools…)
    public IReadOnlyList<JunctionEditorViewModel>? JunctionEditors { get; private set; }
    // Commands
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> DiscardCommand { get; }

    // Internals
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
                if (JunctionEditors?.Count > 0)
                    foreach (var editor in JunctionEditors)
                        await SaveJunctionEditorAsync(editor);

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

        // Reload junction editors
        JunctionEditors = BuildJunctionEditors(_entity);
        this.RaisePropertyChanged(nameof(JunctionEditors));
        if (JunctionEditors?.Count > 0)
            await InitializeJunctionEditorsAsync();

        IsDirty = false;
        StatusMessage = null;
    }

    private IReadOnlyList<FieldGroupViewModel> BuildFieldGroups(ContentBase entity)
    {
        var baseProps = typeof(ContentBase)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet();

        // Try to find a schema for this entity type to use explicit labels + constraints.
        _entityTypeToSchemaKey.TryGetValue(entity.GetType(), out var schemaKey);
        var schema = schemaKey is not null ? ContentSchemaRegistry.Get(schemaKey) : null;

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

            // Match the schema's group by group property name ("Stats", "Traits", etc.)
            var schemaGroup = schema?.Groups.FirstOrDefault(g =>
                string.Equals(g.Label, groupProp.Name, StringComparison.OrdinalIgnoreCase));

            var fields = groupProp.PropertyType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p =>
                {
                    // Build the dot-path this field would use in the schema (e.g. "stats.manaCost")
                    var dotPath = $"{groupProp.Name.ToLowerInvariant()}.{char.ToLower(p.Name[0])}{p.Name[1..]}";
                    var descriptor = schemaGroup?.Fields.FirstOrDefault(f =>
                        string.Equals(f.Name, dotPath, StringComparison.OrdinalIgnoreCase));
                    return new FieldRowViewModel(p, nested, descriptor);
                })
                .ToList();

            foreach (var field in fields)
                field.FieldChanged += (_, _) => IsDirty = true;

            var groupLabel = schemaGroup?.Label ?? FormatGroupName(groupProp.Name);
            groups.Add(new FieldGroupViewModel(groupLabel, fields));
        }

        return groups;
    }

    private static bool IsCollectionProperty(PropertyInfo p) =>
        p.PropertyType is { IsGenericType: true } t
        && t.GetGenericTypeDefinition() == typeof(ICollection<>);

    private static string FormatGroupName(string name) =>
        System.Text.RegularExpressions.Regex
            .Replace(name, "(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ");

    // Entity type → ContentSchemaRegistry key map
    private static readonly Dictionary<Type, string> _entityTypeToSchemaKey = new()
    {
        [typeof(Power)]             = "power",
        [typeof(Species)]         = "species",
        [typeof(ActorClass)]      = "class",
        [typeof(ActorArchetype)]  = "archetype",
        [typeof(ActorInstance)]   = "instance",
        [typeof(Background)]      = "background",
        [typeof(Skill)]           = "skill",
        [typeof(Item)]            = "item",
        [typeof(Material)]        = "material",
        [typeof(MaterialProperty)]= "materialproperty",
        [typeof(Enchantment)]     = "enchantment",
        // Power combines former ability and spell schema keys
        [typeof(Quest)]           = "quest",
        [typeof(Recipe)]          = "recipe",
        [typeof(LootTable)]       = "loottable",
        [typeof(Organization)]    = "organization",
        [typeof(ZoneLocation)]   = "zonelocation",
        [typeof(Dialogue)]        = "dialogue",
    };

    // Junction editor infrastructure
    private IReadOnlyList<JunctionEditorViewModel>? BuildJunctionEditors(ContentBase entity)
    {
        var editors = new List<JunctionEditorViewModel>();

        if (entity is LootTable)
            editors.Add(new JunctionEditorViewModel(JunctionEditorType.LootEntry,       "Loot Entries",       entity.Id));
        if (entity is Recipe)
            editors.Add(new JunctionEditorViewModel(JunctionEditorType.RecipeIngredient, "Ingredients",        entity.Id));
        if (entity is Species)
            editors.Add(new JunctionEditorViewModel(JunctionEditorType.AbilityPool,      "Innate Abilities",   entity.Id));
        if (entity is ActorArchetype)
            editors.Add(new JunctionEditorViewModel(JunctionEditorType.ArchetypePool,    "Ability Pool",       entity.Id));
        if (entity is ActorClass)
            editors.Add(new JunctionEditorViewModel(JunctionEditorType.ClassUnlock,      "Ability Unlocks",    entity.Id));
        if (entity is ActorInstance)
            editors.Add(new JunctionEditorViewModel(JunctionEditorType.AbilityPool,      "Additional Abilities", entity.Id));

        if (editors.Count == 0) return null;

        foreach (var ed in editors)
            ed.DataChanged += (_, _) => IsDirty = true;

        return editors;
    }

    private async Task InitializeJunctionEditorsAsync()
    {
        if (JunctionEditors is null) return;
        foreach (var editor in JunctionEditors)
        {
            try   { await LoadJunctionDataAsync(editor); }
            catch (Exception ex) { Log.Error(ex, "Failed to load junction data for {Type}", editor.EditorType); }
        }
    }

    private async Task LoadJunctionDataAsync(JunctionEditorViewModel editor)
    {
        switch (editor.EditorType)
        {
            case JunctionEditorType.LootEntry:
            {
                var entries = await _service.LoadLootTableEntriesAsync(editor.OwnerId);
                editor.LoadRows(entries.Select(e => new JunctionRowViewModel(JunctionEditorType.LootEntry)
                {
                    ItemDomain   = e.ItemDomain,
                    ItemSlug     = e.ItemSlug,
                    DropWeight   = e.DropWeight,
                    QuantityMin  = e.QuantityMin,
                    QuantityMax  = e.QuantityMax,
                    IsGuaranteed = e.IsGuaranteed,
                }));
                break;
            }
            case JunctionEditorType.RecipeIngredient:
            {
                var rows = await _service.LoadRecipeIngredientsAsync(editor.OwnerId);
                editor.LoadRows(rows.Select(i => new JunctionRowViewModel(JunctionEditorType.RecipeIngredient)
                {
                    ItemDomain = i.ItemDomain,
                    ItemSlug   = i.ItemSlug,
                    Quantity   = i.Quantity,
                    IsOptional = i.IsOptional,
                }));
                break;
            }
            case JunctionEditorType.AbilityPool:
            {
                IReadOnlyList<Guid> ids;
                if (_entity is Species)
                    ids = [.. (await _service.LoadSpeciesAbilityPoolAsync(editor.OwnerId)).Select(p => p.PowerId)];
                else
                    ids = [.. (await _service.LoadInstanceAbilityPoolAsync(editor.OwnerId)).Select(p => p.PowerId)];

                var slugs = await _service.GetAbilitySlugsAsync(ids);
                editor.LoadRows(ids.Select(id => new JunctionRowViewModel(JunctionEditorType.AbilityPool)
                {
                    AbilitySlug = slugs.GetValueOrDefault(id, id.ToString()),
                }));
                break;
            }
            case JunctionEditorType.ArchetypePool:
            {
                var pools = await _service.LoadArchetypeAbilityPoolAsync(editor.OwnerId);
                var ids   = pools.Select(p => p.PowerId).Distinct().ToList();
                var slugs = await _service.GetAbilitySlugsAsync(ids);
                editor.LoadRows(pools.Select(p => new JunctionRowViewModel(JunctionEditorType.ArchetypePool)
                {
                    AbilitySlug = slugs.GetValueOrDefault(p.PowerId, p.PowerId.ToString()),
                    UseChance   = (decimal)p.UseChance,
                }));
                break;
            }
            case JunctionEditorType.ClassUnlock:
            {
                var unlocks = await _service.LoadClassAbilityUnlocksAsync(editor.OwnerId);
                var ids     = unlocks.Select(u => u.PowerId).Distinct().ToList();
                var slugs   = await _service.GetAbilitySlugsAsync(ids);
                editor.LoadRows(unlocks.Select(u => new JunctionRowViewModel(JunctionEditorType.ClassUnlock)
                {
                    AbilitySlug   = slugs.GetValueOrDefault(u.PowerId, u.PowerId.ToString()),
                    LevelRequired = u.LevelRequired,
                    Rank          = u.Rank,
                }));
                break;
            }
        }
    }

    private async Task SaveJunctionEditorAsync(JunctionEditorViewModel editor)
    {
        switch (editor.EditorType)
        {
            case JunctionEditorType.LootEntry:
                await _service.SaveLootTableEntriesAsync(editor.OwnerId,
                    [.. editor.Rows
                        .Where(r => !string.IsNullOrWhiteSpace(r.ItemSlug))
                        .Select(r => new LootTableEntry
                        {
                            LootTableId  = editor.OwnerId,
                            ItemDomain   = r.ItemDomain.Trim(),
                            ItemSlug     = r.ItemSlug.Trim(),
                            DropWeight   = (int)r.DropWeight,
                            QuantityMin  = (int)r.QuantityMin,
                            QuantityMax  = (int)r.QuantityMax,
                            IsGuaranteed = r.IsGuaranteed,
                        })]);
                break;

            case JunctionEditorType.RecipeIngredient:
                await _service.SaveRecipeIngredientsAsync(editor.OwnerId,
                    [.. editor.Rows
                        .Where(r => !string.IsNullOrWhiteSpace(r.ItemSlug))
                        .Select(r => new RecipeIngredient
                        {
                            RecipeId   = editor.OwnerId,
                            ItemDomain = r.ItemDomain.Trim(),
                            ItemSlug   = r.ItemSlug.Trim(),
                            Quantity   = (int)r.Quantity,
                            IsOptional = r.IsOptional,
                        })]);
                break;

            case JunctionEditorType.AbilityPool:
            {
                List<string> slugs = [.. editor.Rows.Select(r => r.AbilitySlug.Trim()).Where(s => s.Length > 0)];
                if (_entity is Species)
                    await _service.SaveSpeciesAbilityPoolAsync(editor.OwnerId, slugs);
                else
                    await _service.SaveInstanceAbilityPoolAsync(editor.OwnerId, slugs);
                break;
            }
            case JunctionEditorType.ArchetypePool:
                await _service.SaveArchetypeAbilityPoolAsync(editor.OwnerId,
                    [.. editor.Rows
                        .Where(r => !string.IsNullOrWhiteSpace(r.AbilitySlug))
                        .Select(r => (r.AbilitySlug.Trim(), (float)r.UseChance))]);
                break;

            case JunctionEditorType.ClassUnlock:
                await _service.SaveClassAbilityUnlocksAsync(editor.OwnerId,
                    [.. editor.Rows
                        .Where(r => !string.IsNullOrWhiteSpace(r.AbilitySlug))
                        .Select(r => (r.AbilitySlug.Trim(), (int)r.LevelRequired, (int)r.Rank))]);
                break;
        }
    }
}

